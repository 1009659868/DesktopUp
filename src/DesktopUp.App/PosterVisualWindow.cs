using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow(
    IDesktopLayerAttachment attachment,
    IStateCommandService stateCommands,
    IResourceLedger ledger,
    IStructuredLog log,
    PosterSpatialRuntime spatial,
    PosterInputRouter inputRouter, PosterInputSessionKey inputSession,
    Action<PosterRemovalRequest> requestRemoval,
    Action? showControlCenter = null,
    Action<bool>? editingStateChanged = null) : IDisposable
{
    private const double SafeMarginDip = 24;
    private const double SurfaceCornerRadiusDip = 8;
    private const double HeaderHeightDip = PosterTitleBarLayoutPolicy.HeaderHeightDip;
    private const double ItemsTopInsetDip = PosterContentMetrics.ItemsTopInsetDip;
    private const double FooterHeightDip = PosterContentMetrics.FooterHeightDip;
    private const double HorizontalPaddingDip = PosterPresentationPolicy.HorizontalPaddingDip;
    private static readonly PosterItem[] Items =
    [
        new("AL", "AFTERLIGHT", "01", 132, 86, 190),
        new("GS", "GHOST SIGNAL", "02", 68, 154, 171),
        new("IV", "IRON VEIL", "03", 177, 69, 73),
        new("LO", "LAST ORBIT", "04", 181, 118, 48),
        new("SR", "SABLE RUN", "05", 65, 103, 173),
        new("HC", "HOLLOW CROWN", "06", 64, 143, 113)
    ];

    private Window? _window;
    private Border? _surface;
    private ScrollViewer? _itemsScrollViewer;
    private Grid? _itemsGrid;
    private TextBlock? _collapseGlyph;
    private TextBlock? _moreGlyph;
    private TextBlock? _modeLabel;
    private MenuFlyout? _posterMenu;
    private MenuInputDiagnostic? _menuDiagnostic;
    private DesktopMenuInteraction? _menuInteraction;
    private nint _windowHandle;
    private IDisposable? _windowLease;
    private PosterInputIslandManager? _inputIsland;
    private DesktopShellHostSnapshot? _desktopHost;
    private DisplayMonitorSnapshot? _monitor;
    private DesktopPixelRect _bounds;
    private PosterPlacementState _placement;
    private PosterFrameGeometry _geometry;
    private PosterDefinition? _authoritativePoster;
    private long _stateRevision;
    private long _posterRevision;
    private long _posterEntityVersion;
    private string? _activeCommandRequestId;
    private PosterOperatingMode _mode = PosterOperatingMode.Using;
    private long _layoutRevision;
    private long _attachedDesktopGeneration;
    private long _appliedTopologyGeneration;
    private int _itemColumnCount = -1;
    private double _itemScrollOffsetDip;
    private DispatcherQueueTimer? _gestureFrameTimer;
    private PendingGestureFrame? _pendingGestureFrame;
    private bool _gestureFrameScheduled;
    private VisualRegionState? _appliedVisualRegion;
    private bool _disposed;
    private SpatialGestureSession? _spatialGesture;

    public nint WindowHandle => _windowHandle;
    public bool IsEditing => _mode == PosterOperatingMode.Editing;
    public string? PosterId => _authoritativePoster?.PosterId;
    public void RegisterSpatialSurface() => spatial.Register(this);
    public void SuspendInput()
    {
        _menuInteraction?.Cancel();
        _posterMenu?.Hide();
        _posterMenu = null;
        _inputIsland?.SuspendRouting();
        inputRouter.Invalidate(inputSession);
    }
    public DispatcherQueue SpatialDispatcher => _window!.DispatcherQueue;

    public void BeginPosturePreview(string requestId, bool revealContents = true)
    {
        CancelScheduledGestureFrame();
        _inputIsland?.CancelPointerGesture();
        _activeCommandRequestId = requestId;
        if (revealContents) _geometry = _geometry with { IsCollapsed = false };
        UpdateVisualState();
    }
    public bool PresentPostureFrame(DesktopPixelRect frame) => !_disposed && ApplyFrame(frame);
    internal sealed record PreparedSpatialFrame(PosterVisualWindow Owner, DesktopPixelRect Bounds, PosterInputLayoutSnapshot Input);
    public PreparedSpatialFrame? PrepareSpatialFrame(DesktopPixelRect frame)
    {
        if (_disposed || _window is null || _inputIsland is null || !frame.IsUsable) return null;
        var input = BuildInputLayout(frame);
        // Preparation must not clip or resize a live surface before its peers move.
        return new(this, frame, input);
    }
    public bool CommitSpatialFrames(IReadOnlyList<PreparedSpatialFrame> frames)
    {
        var layouts = new List<(FrameworkElement Root, double Width, double Height)>();
        try
        {
        var windows = new Dictionary<nint, DesktopPixelRect>();
        foreach (var frame in frames)
        {
            var owner = frame.Owner;
            if (owner._disposed || owner._inputIsland is null) return false;
            windows.Add(owner._windowHandle, frame.Bounds);
            windows.Add(owner._inputIsland.WindowHandle, frame.Bounds);
        }
        // This runs from the render callback for animated batches. Arrange the XAML content
        // for the SAME bounds before moving HWNDs; don't wait for a later WM_SIZE layout pass.
        foreach (var frame in frames)
        {
            var owner = frame.Owner;
            if (owner._window?.Content is not FrameworkElement root || owner._monitor is null) return false;
            layouts.Add((root, root.Width, root.Height));
            owner.RebuildItemGrid(owner.FrameWidthDip(frame.Bounds)); owner.UpdateItemScrollState(frame.Bounds);
            double width = frame.Bounds.Width / owner._monitor.ScaleX, height = frame.Bounds.Height / owner._monitor.ScaleY;
            root.Width = width; root.Height = height;
            root.Measure(new global::Windows.Foundation.Size(width, height));
            root.Arrange(new global::Windows.Foundation.Rect(0, 0, width, height));
            root.UpdateLayout();
            if (Math.Abs(root.ActualWidth - width) > .5 || Math.Abs(root.ActualHeight - height) > .5)
            {
                log.Write("warning", "spatial.xaml_layout_mismatch", new { owner.PosterId, width, height, root.ActualWidth, root.ActualHeight });
                return false;
            }
        }
        bool applied = attachment.ApplyPreparedBoundsBatch(windows);
        bool nativeApplied = applied;
        // Always read every participant after a partial native failure. Recovery then uses the
        // single committed projection, not whichever windows happened to move first.
        foreach (var frame in frames)
        {
            var owner = frame.Owner;
            bool matches = owner._window is not null && HasExpectedBounds(owner._window, frame.Bounds);
            applied &= matches;
            // Native clipping follows the WHOLE position batch, never the per-owner prepare loop.
            bool regionApplied = matches && owner.ApplyVisualRegion(owner._windowHandle, frame.Bounds);
            applied &= regionApplied;
            if (matches) owner._bounds = frame.Bounds;
            bool inputApplied = matches && owner._inputIsland is not null && owner._inputIsland.ApplyLayout(frame.Bounds, frame.Input);
            applied &= inputApplied;
            if (!nativeApplied || !matches || !inputApplied || !regionApplied) log.Write("warning", "spatial.batch_readback_failed", new
            {
                owner.PosterId, nativeApplied, matches, inputApplied, regionApplied, expected = frame.Bounds,
                appPosition = owner._window?.AppWindow.Position, appSize = owner._window?.AppWindow.Size,
                nativeError = System.Runtime.InteropServices.Marshal.GetLastPInvokeError()
            });
        }
        return applied;
        }
        finally
        {
            // Return to normal stretch layout; drag/resize and later theme projections must not
            // inherit a fixed root size from an animation. Native bounds now match this layout.
            foreach (var layout in layouts) { layout.Root.Width = layout.Width; layout.Root.Height = layout.Height; }
        }
    }
    public PreparedSpatialFrame? PreparePostureRestore()
    {
        _activeCommandRequestId = null;
        if (_disposed || _monitor is null || !spatial.TryGetProjection(out var current) ||
            current?.PostersById.GetValueOrDefault(PosterId ?? "") is not { } target || !AdoptPosterProjection(target, _monitor, applyFrame: false)) return null;
        return PrepareSpatialFrame(_bounds);
    }

    public bool TryReadSpatial(Guid runtimeId, out SpatialEnvironment? environment, out SpatialPoster? poster, out long entityVersion)
    {
        environment = null; poster = null; entityVersion = _posterEntityVersion;
        if (_disposed || _window is null || _monitor is null || _authoritativePoster is null ||
            CurrentGenerationPair is not { } generations || !HasExpectedBounds(_window, _bounds)) return false;
        environment = new(_monitor.DeviceName, PosterSpatialRuntime.Rect(_monitor.WorkArea), _monitor.DpiX, _monitor.DpiY,
            generations.DisplayTopologyGeneration, generations.DesktopShellGeneration, runtimeId);
        spatial.TryGetProjection(out var projection);
        var state = projection?.PostersById.GetValueOrDefault(PosterId!);
        poster = new(PosterId!, PosterSpatialRuntime.Rect(_bounds), ToPhysicalY(_authoritativePoster.ExpandedHeightDip, _monitor),
            _authoritativePoster.IsVisible, _authoritativePoster.IsLocked, _authoritativePoster.IsCollapsed,
            state?.LayoutGeneration ?? 0, state?.LastLayoutOperationId);
        return true;
    }

    internal void CancelSpatialGesture(string reason)
    {
        var gesture = _spatialGesture;
        _spatialGesture = null;
        gesture?.Cancel();
        spatial.End(this);
        CancelScheduledGestureFrame();
        _inputIsland?.CancelPointerGesture();
        if (gesture is not null && !_disposed && _monitor is not null && spatial.TryGetProjection(out var current) &&
            current?.PostersById.GetValueOrDefault(PosterId ?? "") is { } target)
            _ = AdoptPosterProjection(target, _monitor, applyFrame: true);
        if (gesture is not null) log.Write("info", "spatial.gesture_cancelled", new { reason, gesture.OperationId });
    }

    public DesktopRecoveryGenerationPair? CurrentGenerationPair =>
        _attachedDesktopGeneration > 0 && _appliedTopologyGeneration > 0
            ? new DesktopRecoveryGenerationPair(
                _attachedDesktopGeneration,
                _appliedTopologyGeneration)
            : null;

    public bool Show(
        DesktopShellHostSnapshot desktopHost,
        DisplayTopologySnapshot displayTopology,
        PosterSurfaceProjection state)
    {
        if (_disposed) return false;
        if (_window is not null)
        {
            return Recover(desktopHost, displayTopology, request: 0);
        }

        DisplayMonitorSnapshot? primaryMonitor = ResolveMonitor(displayTopology, state.Poster.MonitorDeviceName);
        if (primaryMonitor is null)
        {
            log.Write("error", "poster.show_failed", new { error = "primary-monitor-missing" });
            return false;
        }

        _monitor = primaryMonitor;
        if (!state.Poster.IsVisible)
        {
            return false;
        }
        if (!AdoptPosterProjection(state, primaryMonitor, applyFrame: false)) return false;
        if (!string.Equals(
                state.Poster.MonitorDeviceName,
                primaryMonitor.DeviceName,
                StringComparison.OrdinalIgnoreCase))
        {
            log.Write("info", "poster.monitor_fallback_applied", new
            {
                savedMonitor = state.Poster.MonitorDeviceName,
                targetMonitor = primaryMonitor.DeviceName,
                state.Revision
            });
        }

        Window window = CreateWindow();
        nint windowHandle = WindowNative.GetWindowHandle(window);
        window.AppWindow.MoveAndResize(ToRect(_bounds));
        if (!HasExpectedBounds(window, _bounds))
        {
            window.Close();
            ClearVisualReferences();
            log.Write("error", "poster.show_failed", new
            {
                error = "initial-bounds-readback-mismatch",
                expected = _bounds
            });
            return false;
        }
        if (!ApplyVisualRegion(windowHandle, _bounds))
        {
            window.Close();
            ClearVisualReferences();
            log.Write("error", "poster.show_failed", new
            {
                error = "initial-rounded-region-failed",
                expected = _bounds,
                cornerRadius = ResolveOuterCornerRadiusPixels()
            });
            return false;
        }

        DesktopLayerAttachResult result = attachment.Attach(
            windowHandle,
            desktopHost,
            showWindow: true,
            inputTransparent: true);
        LogAttachment("poster.attached", result);
        if (!result.Succeeded)
        {
            window.Close();
            ClearVisualReferences();
            return false;
        }

        _window = window;
        _windowHandle = windowHandle;
        _desktopHost = desktopHost;
        _attachedDesktopGeneration = desktopHost.Generation;
        _appliedTopologyGeneration = displayTopology.Generation;
        _windowLease = ledger.Acquire("window.poster-visual", "desktop-owned-winui-window");

        _inputIsland = new PosterInputIslandManager(
            attachment,
            ledger,
            log,
            ReassertCurrentClusterBottom, inputRouter, inputSession, _windowHandle);
        _inputIsland.Input += OnInput;
        PosterInputLayoutSnapshot inputLayout = BuildInputLayout(_bounds);
        if (!_inputIsland.Create(desktopHost, _bounds, inputLayout))
        {
            Dispose();
            return false;
        }

        if (!ReassertDesktopBottom(desktopHost))
        {
            Dispose();
            return false;
        }
        QueuePostCompositionRepair();

        log.Write("info", "poster.shown", new
        {
            handle = $"0x{windowHandle:X}",
            inputHandle = $"0x{_inputIsland.WindowHandle:X}",
            owner = $"0x{desktopHost.Identity.IconViewHandle:X}",
            desktopGeneration = desktopHost.Generation,
            displayGeneration = displayTopology.Generation,
            stateRevision = _stateRevision,
            posterRevision = _posterRevision,
            atomicDesktopBottomShow = true,
            visualInputTransparent = true,
            inputRegionCount = inputLayout.Regions.Count,
            bounds = _bounds
        });
        return true;
    }

    private void QueuePostCompositionRepair()
    {
        if (_window is null)
        {
            return;
        }

        _ = _window.DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                if (_disposed || _desktopHost is null)
                {
                    return;
                }

                bool regionRestored = ApplyVisualRegion(
                    _windowHandle,
                    _bounds,
                    force: true);
                bool restored = regionRestored && ReassertDesktopBottom(_desktopHost);
                log.Write(
                    restored ? "info" : "warning",
                    "poster.post_composition_reasserted",
                    new
                    {
                        restored,
                        regionRestored,
                        handle = $"0x{_windowHandle:X}",
                        _desktopHost.Generation
                    });
            });
    }

    public bool Recover(
        DesktopShellHostSnapshot desktopHost,
        DisplayTopologySnapshot displayTopology,
        long request)
    {
        if (_window is null ||
            _windowHandle == 0 ||
            _inputIsland is null ||
            ResolveMonitor(displayTopology, _authoritativePoster?.MonitorDeviceName) is not { } primaryMonitor)
        {
            return false;
        }

        bool needsReattach =
            _attachedDesktopGeneration != desktopHost.Generation ||
            !attachment.IsAttached(_windowHandle, desktopHost) ||
            !_inputIsland.IsAttached(desktopHost);
        if (needsReattach)
        {
            log.Write("info", "poster_reattach.started", new
            {
                request,
                previousGeneration = _attachedDesktopGeneration,
                targetGeneration = desktopHost.Generation,
                visualHandle = $"0x{_windowHandle:X}",
                inputHandle = $"0x{_inputIsland.WindowHandle:X}"
            });
            DesktopLayerAttachResult visual = attachment.Attach(
                _windowHandle,
                desktopHost,
                showWindow: false,
                inputTransparent: true);
            LogAttachment("poster.reattached", visual);
            if (!visual.Succeeded ||
                !_inputIsland.Reattach(desktopHost) ||
                !attachment.IsAttached(_windowHandle, desktopHost) ||
                !_inputIsland.IsAttached(desktopHost))
            {
                return false;
            }

            _attachedDesktopGeneration = desktopHost.Generation;
            log.Write("info", "poster_reattach.committed", new
            {
                request,
                generation = desktopHost.Generation,
                visualHandle = $"0x{_windowHandle:X}",
                inputHandle = $"0x{_inputIsland.WindowHandle:X}",
                owner = $"0x{desktopHost.Identity.IconViewHandle:X}"
            });
        }

        _desktopHost = desktopHost;
        _monitor = primaryMonitor;
        // Reproject from committed DIP geometry, not a previous screen's clamped presentation.
        if (_appliedTopologyGeneration != displayTopology.Generation && _authoritativePoster is { } saved &&
            !AdoptPosterProjection(new PosterSurfaceProjection(_stateRevision, _posterRevision, saved,
                !_geometry.IsLocked, !_geometry.IsLocked, true) { EntityVersion = _posterEntityVersion },
                primaryMonitor, applyFrame: false)) return false;
        DesktopPixelRect expectedBounds =
            PosterGeometryProjector.Project(_placement, primaryMonitor);
        bool needsReproject =
            _appliedTopologyGeneration != displayTopology.Generation ||
            !HasExpectedBounds(_window, expectedBounds);
        if (needsReproject)
        {
            log.Write("info", "poster_reproject.started", new
            {
                request,
                previousGeneration = _appliedTopologyGeneration,
                targetGeneration = displayTopology.Generation,
                expectedBounds
            });
            if (!ApplyFrame(expectedBounds))
            {
                return false;
            }

            _geometry = GeometryFromBounds(
                expectedBounds,
                primaryMonitor,
                _geometry.IsLocked,
                _geometry.IsCollapsed,
                _geometry.Revision,
                _geometry.ExpandedHeightDip);
            _appliedTopologyGeneration = displayTopology.Generation;
            log.Write("info", "poster_reproject.committed", new
            {
                request,
                generation = displayTopology.Generation,
                bounds = expectedBounds,
                primaryMonitor.DeviceName,
                primaryMonitor.DpiX,
                primaryMonitor.DpiY,
                stateRevision = _stateRevision,
                persisted = false
            });
        }
        else if (!_inputIsland.ApplyLayout(_bounds, BuildInputLayout(_bounds)))
        {
            return false;
        }

        return ReassertDesktopBottom(desktopHost) &&
            HasExpectedBounds(_window, expectedBounds);
    }

    public bool SetEditing(bool editing)
    {
        PosterOperatingMode target = editing
            ? PosterOperatingMode.Editing
            : PosterOperatingMode.Using;
        if (_mode == target)
        {
            return true;
        }

        if (_spatialGesture is not null) CancelSpatialGesture("mode-changed");
        _mode = target;
        UpdateVisualState();
        bool applied = _inputIsland?.ApplyLayout(
            _bounds,
            BuildInputLayout(_bounds)) == true;
        log.Write(applied ? "info" : "warning", "poster.mode_changed", new
        {
            mode = _mode.ToString(),
            _geometry.IsLocked,
            _geometry.IsCollapsed,
            layoutRevision = _layoutRevision,
            applied
        });
        if (applied)
        {
            editingStateChanged?.Invoke(editing);
        }
        return applied;
    }

    public bool ReassertDesktopBottom(DesktopShellHostSnapshot desktopHost)
    {
        if (_window is null ||
            _inputIsland is null ||
            _attachedDesktopGeneration != desktopHost.Generation)
        {
            return false;
        }

        if (DesktopMenuInteraction.ProtectsSurface(_windowHandle)) return IsAttached(desktopHost);
        return _inputIsland.ReassertDesktopBottom(desktopHost) &&
            attachment.ReassertDesktopBottom(_windowHandle, desktopHost);
    }

    public bool IsAttached(DesktopShellHostSnapshot desktopHost) =>
        _window is not null &&
        _inputIsland is not null &&
        _attachedDesktopGeneration == desktopHost.Generation &&
        attachment.IsAttached(_windowHandle, desktopHost) &&
        _inputIsland.IsAttached(desktopHost);

    private bool ReassertCurrentClusterBottom() =>
        _desktopHost is not null && ReassertDesktopBottom(_desktopHost);

    private void OnInput(object? sender, PosterInputEventArgs args)
    {
        if (_disposed || args.Session != inputSession || !ReferenceEquals(sender, _inputIsland)) return;
        if (args.EventKind == PosterInputEventKind.GestureCancelled)
        {
            if (_spatialGesture is not null) CancelSpatialGesture("pointer-capture-cancelled");
            return;
        }
        if (_window is null || _monitor is null || _inputIsland is null)
        {
            return;
        }
        if (args.LayoutRevision != _inputIsland.AppliedLayoutRevision || !inputRouter.IsCurrent(inputSession, args.LayoutRevision))
        {
            log.Write("warning", "poster.input_stale", new
            {
                args.LayoutRevision,
                appliedRevision = _inputIsland.AppliedLayoutRevision,
                args.EventKind,
                args.TargetKind
            });
            return;
        }

        if (args.EventKind == PosterInputEventKind.Scrolled)
        {
            ScrollItems(args.WheelDelta);
            return;
        }

        if (args.EventKind == PosterInputEventKind.Invoked)
        {
            HandleInvocation(args);
            return;
        }

        if (!TryResolveGesture(args.TargetKind, out PosterGestureKind gesture) ||
            !LockPermissionPolicy.CanBeginGesture(_mode, _geometry, gesture))
        {
            return;
        }

        if (_spatialGesture is null)
        {
            _spatialGesture = spatial.Begin(this, args.InitialFrame, gesture, _mode,
                Math.Min(_monitor.WorkArea.Width, ToPhysicalX(240, _monitor)),
                Math.Min(_monitor.WorkArea.Height, ToPhysicalY(240, _monitor)));
            if (_spatialGesture is null)
            {
                CancelSpatialGesture("spatial-snapshot-unavailable");
                return;
            }
        }

        DesktopPixelRect candidate = ResolveGestureFrame(
            gesture,
            args.InitialFrame,
            args.DeltaX,
            args.DeltaY,
            _monitor);
        if (args.EventKind == PosterInputEventKind.GesturePreview)
        {
            ScheduleGestureFrame(candidate, args.InitialFrame);
            return;
        }

        CancelScheduledGestureFrame();
        if (!PresentSpatialCandidate(candidate)) return;
        candidate = _bounds;

        if (args.EventKind == PosterInputEventKind.GestureCompleted)
        {
            PosterFrameGeometry candidateGeometry = GeometryFromBounds(
                candidate,
                _monitor,
                _geometry.IsLocked,
                _geometry.IsCollapsed,
                _geometry.Revision,
                _geometry.ExpandedHeightDip);
            bool committed;
            try { committed = CommitPosterState(
                    candidate,
                    candidateGeometry,
                    gesture == PosterGestureKind.Move
                        ? PosterStateCommandKind.Move
                        : PosterStateCommandKind.Resize,
                    $"geometry-{gesture.ToString().ToLowerInvariant()}"); }
            finally { _spatialGesture = null; spatial.End(this); }
            if (!committed)
            {
                return;
            }
            log.Write("info", "poster.geometry_committed", new
            {
                gesture = gesture.ToString(),
                stateRevision = _stateRevision,
                posterRevision = _posterRevision,
                layoutRevision = _layoutRevision,
                bounds = _bounds
            });
        }
    }

    private void ScheduleGestureFrame(
        DesktopPixelRect candidate,
        DesktopPixelRect initialFrame)
    {
        if (_window is null)
        {
            return;
        }

        _pendingGestureFrame = new PendingGestureFrame(candidate, initialFrame);
        if (_gestureFrameTimer is null)
        {
            _gestureFrameTimer = _window.DispatcherQueue.CreateTimer();
            _gestureFrameTimer.Interval = TimeSpan.FromMilliseconds(16);
            _gestureFrameTimer.IsRepeating = false;
            _gestureFrameTimer.Tick += OnGestureFrameTick;
        }
        if (_gestureFrameScheduled)
        {
            return;
        }

        _gestureFrameScheduled = true;
        _gestureFrameTimer.Start();
    }

    private void OnGestureFrameTick(DispatcherQueueTimer sender, object args)
    {
        _gestureFrameScheduled = false;
        PendingGestureFrame? pending = _pendingGestureFrame;
        _pendingGestureFrame = null;
        if (pending is { } frame) _ = PresentSpatialCandidate(frame.Candidate);
    }

    private bool PresentSpatialCandidate(DesktopPixelRect candidate)
    {
        if (_spatialGesture is not { } session || !spatial.IsCurrent(this, session))
        {
            CancelSpatialGesture("gesture-read-set-or-environment-stale");
            return false;
        }
        SpatialPlan plan = session.Preview(PosterSpatialRuntime.Rect(candidate));
        if (!plan.Succeeded)
        {
            CancelSpatialGesture("spatial-candidate-invalid");
            return false;
        }
        // Preserve the contact/slide path. A skipped timer sample is never accepted as presented.
        foreach (SpatialPathLeg leg in plan.Path)
        {
            if (_spatialGesture != session || !ApplyFrame(PosterSpatialRuntime.Pixels(leg.To)))
            {
                CancelSpatialGesture("native-frame-rejected");
                return false;
            }
        }
        if (_spatialGesture != session || !session.Accept(plan))
        {
            CancelSpatialGesture("gesture-trace-invalidated");
            return false;
        }
        return true;
    }

    private void CancelScheduledGestureFrame()
    {
        _pendingGestureFrame = null;
        _gestureFrameScheduled = false;
        _gestureFrameTimer?.Stop();
    }

    private void HandleInvocation(PosterInputEventArgs args)
    {
        switch (args.TargetKind)
        {
            case PosterInputTargetKind.LockButton:
                ToggleLocked("legacy-button");
                break;
            case PosterInputTargetKind.CollapseButton:
                ToggleCollapsed();
                break;
            case PosterInputTargetKind.MoreButton:
                ShowPosterMenu();
                break;
            case PosterInputTargetKind.Item:
                if (LockPermissionPolicy.CanActivateItem(_geometry) &&
                    args.TargetIndex >= 0 && args.TargetIndex < Items.Length)
                {
                    log.Write("info", "poster.test_item_invoked", new
                    {
                        index = args.TargetIndex,
                        item = Items[args.TargetIndex].Name,
                        mode = _mode.ToString()
                    });
                }
                break;
        }
    }

    private void ToggleLocked(string source)
    {
        PosterFrameGeometry previous = _geometry;
        _geometry = PosterFrameGeometryPolicy.ToggleLocked(_geometry);
        UpdateVisualState();
        bool applied = _inputIsland?.ApplyLayout(
            _bounds,
            BuildInputLayout(_bounds)) == true;
        if (!applied)
        {
            _geometry = previous;
            UpdateVisualState();
            _ = _inputIsland?.ApplyLayout(_bounds, BuildInputLayout(_bounds));
        }
        else
        {
            applied = CommitPosterState(
                _bounds,
                _geometry,
                PosterStateCommandKind.SetLocked,
                $"lock-{source}");
        }
        log.Write(applied ? "info" : "warning", "poster.lock_changed", new
        {
            source,
            applied,
            _geometry.IsLocked,
            stateRevision = _stateRevision,
            posterRevision = _posterRevision,
            layoutRevision = _layoutRevision
        });
    }

    private void ToggleCollapsed()
    {
        spatial.ToggleCollapsed(this);
    }

    internal static PosterDefinition CreateInitialDefault(DisplayMonitorSnapshot monitor) =>
        CreateDefaultPoster(CreateDefaultBounds(monitor), monitor);

    private static DisplayMonitorSnapshot? ResolveMonitor(DisplayTopologySnapshot topology, string? savedName) =>
        topology.Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, savedName, StringComparison.OrdinalIgnoreCase))
        ?? topology.PrimaryMonitor;

    private static PosterDefinition CreateDefaultPoster(
        DesktopPixelRect bounds,
        DisplayMonitorSnapshot monitor)
    {
        PosterPlacementState placement = PlacementFromBounds(bounds, monitor);
        DesktopPixelRect work = monitor.WorkArea;
        return new PosterDefinition(
            BuiltInStateIdentities.PrimaryPosterId,
            "NIGHT OPS",
            "TACTICAL ARCHIVE / 01",
            monitor.DeviceName,
            (bounds.X - work.X) / monitor.ScaleX,
            (bounds.Y - work.Y) / monitor.ScaleY,
            bounds.Width / monitor.ScaleX,
            bounds.Height / monitor.ScaleY,
            placement.HorizontalAnchor,
            placement.VerticalAnchor,
            Math.Clamp(bounds.Width / (double)work.Width, 0.000001, 1),
            Math.Clamp(bounds.Height / (double)work.Height, 0.000001, 1),
            IsLocked: false,
            IsCollapsed: false,
            AutoCollapseEnabled: false,
            LayoutKind: "adaptive-grid",
            LayoutParametersJson: PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults()),
            TemplateKey: "night-ops");
    }

    private bool AdoptPosterProjection(
        PosterSurfaceProjection snapshot,
        DisplayMonitorSnapshot monitor,
        bool applyFrame)
    {
        PosterDefinition poster = snapshot.Poster;
        DesktopPixelRect work = monitor.WorkArea;
        double workWidthDip = work.Width / monitor.ScaleX;
        double workHeightDip = work.Height / monitor.ScaleY;
        double widthDip = Math.Clamp(
            poster.WidthDip,
            Math.Min(PosterFrameGeometryPolicy.MinimumWidthDip, workWidthDip),
            workWidthDip);
        double expandedHeightDip = Math.Clamp(
            poster.ExpandedHeightDip,
            Math.Min(PosterFrameGeometryPolicy.MinimumExpandedHeightDip, workHeightDip),
            workHeightDip);
        double visibleHeightDip = poster.IsCollapsed
            ? Math.Min(PosterFrameGeometryPolicy.CollapsedHeightDip, workHeightDip)
            : expandedHeightDip;
        var placement = new PosterPlacementState(
            widthDip,
            visibleHeightDip,
            poster.HorizontalAnchor,
            poster.VerticalAnchor);
        if (spatial.ResolveEffectiveBounds(snapshot, monitor) is not { } projected) return false;
        placement = PlacementFromBounds(projected, monitor);

        _stateRevision = snapshot.Revision;
        _posterRevision = snapshot.PosterRevision;
        _posterEntityVersion = snapshot.EntityVersion;
        _authoritativePoster = poster;
        _placement = placement;
        _geometry = GeometryFromBounds(
            projected,
            monitor,
            poster.IsLocked,
            poster.IsCollapsed,
            snapshot.PosterRevision,
            expandedHeightDip);
        ApplyPresentation(snapshot.Presentation);
        UpdateVisualState();
        if (applyFrame)
        {
            return ApplyFrame(projected);
        }

        _bounds = projected;
        return true;
    }

    private PosterDefinition CapturePosterDefinition(
        DesktopPixelRect bounds,
        PosterFrameGeometry geometry,
        DisplayMonitorSnapshot monitor)
    {
        PosterDefinition seed = _authoritativePoster ??
            CreateDefaultPoster(bounds, monitor);
        PosterPlacementState placement = PlacementFromBounds(bounds, monitor);
        DesktopPixelRect work = monitor.WorkArea;
        double expandedHeightPixels = geometry.ExpandedHeightDip * monitor.ScaleY;
        return seed with
        {
            MonitorDeviceName = monitor.DeviceName,
            FallbackXDip = (bounds.X - work.X) / monitor.ScaleX,
            FallbackYDip = (bounds.Y - work.Y) / monitor.ScaleY,
            WidthDip = bounds.Width / monitor.ScaleX,
            ExpandedHeightDip = geometry.ExpandedHeightDip,
            HorizontalAnchor = placement.HorizontalAnchor,
            VerticalAnchor = placement.VerticalAnchor,
            NormalizedWidth = Math.Clamp(
                bounds.Width / (double)work.Width,
                0.000001,
                1),
            NormalizedExpandedHeight = Math.Clamp(
                expandedHeightPixels / work.Height,
                0.000001,
                1),
            IsLocked = geometry.IsLocked,
            IsCollapsed = geometry.IsCollapsed
        };
    }

    public bool ApplyProjection(StateProjectionChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_window is null || _monitor is null)
        {
            return false;
        }
        DesktopUpProjection projection = change.Projection;
        if (projection.Revision < _stateRevision)
        {
            log.Write("info", "poster.projection_stale_ignored", new
            {
                incomingRevision = projection.Revision,
                currentRevision = _stateRevision,
                change.RequestId
            });
            return true;
        }
        if (string.Equals(
                change.RequestId,
                _activeCommandRequestId,
                StringComparison.Ordinal))
        {
            return true;
        }
        if (_authoritativePoster is null || !projection.PostersById.TryGetValue(_authoritativePoster.PosterId, out PosterSurfaceProjection? target))
        {
            Dispose();
            return true;
        }
        if (target.PosterRevision == _posterRevision)
        {
            _stateRevision = projection.Revision;
            _posterEntityVersion = target.EntityVersion;
            _authoritativePoster = target.Poster;
            return true;
        }

        bool applied = AdoptPosterProjection(
            target,
            _monitor,
            applyFrame: true);
        log.Write(applied ? "info" : "error", "poster.projection_applied", new
        {
            projection.Revision,
            target.PosterRevision,
            source = change.Source.ToString(),
            change.RequestId,
            applied
        });
        return applied;
    }

    private bool CommitPosterState(
        DesktopPixelRect appliedBounds,
        PosterFrameGeometry candidateGeometry,
        PosterStateCommandKind commandKind,
        string commandName)
    {
        if (_monitor is null || _authoritativePoster is null)
        {
            return false;
        }

        PosterDefinition desired = commandKind switch
        {
            PosterStateCommandKind.SetLocked => _authoritativePoster with
            {
                IsLocked = candidateGeometry.IsLocked
            },
            PosterStateCommandKind.SetCollapsed => _authoritativePoster with
            {
                IsCollapsed = candidateGeometry.IsCollapsed
            },
            _ => CapturePosterDefinition(
                appliedBounds,
                candidateGeometry,
                _monitor)
        };
        SpatialGestureSession? gesture = commandKind is PosterStateCommandKind.Move or PosterStateCommandKind.Resize
            ? _spatialGesture : null;
        if (gesture is not null)
        {
            if (!spatial.IsCurrent(this, gesture))
            {
                CancelSpatialGesture("commit-read-set-stale");
                return false;
            }
            desired = SpatialGestureCommitPolicy.Capture(_authoritativePoster, gesture.Presented, gesture.Initial.Environment,
                commandKind == PosterStateCommandKind.Resize ? appliedBounds.Height / (_monitor.DpiY / 96d) : _authoritativePoster.ExpandedHeightDip);
        }
        string requestId = gesture?.OperationId ?? $"poster-{commandName}-{Guid.NewGuid():N}";
        _activeCommandRequestId = requestId;
        StateCommandResult result;
        try
        {
            result = stateCommands.Execute(new PosterStateCommand(
                requestId,
                StateCommandSource.Poster,
                commandKind,
                gesture?.ObservedRevision ?? _stateRevision,
                desired.PosterId,
                desired)
            {
                ReadSet = gesture?.Reads ?? StateReadSet.ForPoster(desired.PosterId, _posterEntityVersion),
                SpatialGesture = gesture?.Freeze()
            });
        }
        finally
        {
            _activeCommandRequestId = null;
        }

        if (result.Succeeded && result.Projection is { } committed &&
            committed.PostersById.TryGetValue(desired.PosterId, out PosterSurfaceProjection? committedTarget))
        {
            _stateRevision = committed.Revision;
            _posterRevision = committedTarget.PosterRevision;
            _posterEntityVersion = committedTarget.EntityVersion;
            _authoritativePoster = committedTarget.Poster;
            if (gesture is not null) spatial.RecordEffectiveFrame(committedTarget, appliedBounds, gesture.Initial.Environment);
            _bounds = appliedBounds;
            _placement = PlacementFromBounds(appliedBounds, _monitor);
            _geometry = GeometryFromBounds(
                appliedBounds,
                _monitor,
                committedTarget.Poster.IsLocked,
                committedTarget.Poster.IsCollapsed,
                committedTarget.PosterRevision,
                committedTarget.Poster.ExpandedHeightDip);
            UpdateVisualState();
            log.Write("info", "poster.state_committed", new
            {
                commandName,
                status = result.Status.ToString(),
                result.RequestId,
                stateRevision = _stateRevision,
                posterRevision = _posterRevision,
                bounds = _bounds,
                _geometry.IsLocked,
                _geometry.IsCollapsed
            });
            return true;
        }

        log.Write("warning", "poster.state_commit_rejected", new
        {
            commandName,
            status = result.Status.ToString(),
            result.RequestId,
            result.ExpectedRevision,
            result.CurrentRevision,
            result.Error
        });
        if (result.Projection is { } latest && !latest.PostersById.ContainsKey(desired.PosterId))
        {
            Dispose();
            return false;
        }
        PosterSurfaceProjection fallback = result.Projection?.PostersById.GetValueOrDefault(desired.PosterId) ??
            new PosterSurfaceProjection(
                _stateRevision,
                _posterRevision,
                _authoritativePoster,
                CanMove: !_authoritativePoster.IsLocked,
                CanResize: !_authoritativePoster.IsLocked &&
                    !_authoritativePoster.IsCollapsed,
                CanToggleCollapsed: true) { EntityVersion = _posterEntityVersion };
        bool restored = AdoptPosterProjection(fallback, _monitor, applyFrame: true);
        log.Write(restored ? "info" : "error", "poster.state_restored", new
        {
            commandName,
            restored,
            stateRevision = _stateRevision,
            posterRevision = _posterRevision,
            bounds = _bounds
        });
        return false;
    }

    private bool ApplyFrame(DesktopPixelRect frame)
    {
        if (_window is null || _inputIsland is null || !frame.IsUsable)
        {
            return false;
        }

        RebuildItemGrid(FrameWidthDip(frame));
        UpdateItemScrollState(frame);
        PosterInputLayoutSnapshot inputLayout = BuildInputLayout(frame);
        if (!ApplyVisualRegion(_windowHandle, frame))
        {
            return false;
        }

        if (!attachment.ApplyPreparedBounds(_windowHandle, frame))
        {
            return false;
        }
        if (!HasExpectedBounds(_window, frame))
        {
            return false;
        }

        _bounds = frame;
        return _inputIsland.ApplyLayout(frame, inputLayout);
    }

    private PosterInputLayoutSnapshot BuildInputLayout(DesktopPixelRect frame)
    {
        if (_monitor is null)
        {
            throw new InvalidOperationException("A monitor snapshot is required for input layout.");
        }

        long revision = checked(++_layoutRevision);
        double scaleX = _monitor.ScaleX;
        PosterTitleBarLayout titleBar =
            PosterTitleBarLayoutPolicy.Resolve(frame.Width / scaleX);
        int headerHeight = ToPhysicalY(titleBar.HeaderHeightDip, _monitor);
        DesktopPixelRect collapseBounds = ToPhysicalRect(
            titleBar.CollapseAction,
            _monitor);
        DesktopPixelRect moreBounds = ToPhysicalRect(
            titleBar.MoreAction,
            _monitor);
        var regions = new List<PosterInputRegion>
        {
            new(PosterInputTargetKind.CollapseButton, -1,
                collapseBounds),
            new(PosterInputTargetKind.MoreButton, -1,
                moreBounds)
        };

        if (!_geometry.IsCollapsed)
        {
            double widthDip = frame.Width / scaleX;
            int columns = ResolveColumns(widthDip);
            double rowStrideDip = ItemRowHeightDip + ItemGapDip;
            double cardWidthDip = PosterPresentationPolicy.CellWidth(widthDip, columns, ItemGapDip);
            int itemStartY = ToPhysicalY(
                HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip,
                _monitor);
            int itemViewportBottom = Math.Max(
                itemStartY,
                frame.Height - ToPhysicalY(FooterHeightDip, _monitor));
            for (int index = 0; index < Items.Length; index++)
            {
                int column = index % columns;
                int row = index / columns;
                int x = ToPhysicalX(
                    HorizontalPaddingDip + (column * (cardWidthDip + ItemGapDip)),
                    _monitor);
                int y = itemStartY + ToPhysicalYOffset(
                    (row * rowStrideDip) - _itemScrollOffsetDip,
                    _monitor);
                int width = ToPhysicalX(cardWidthDip, _monitor);
                int top = Math.Max(y, itemStartY);
                int bottom = Math.Min(
                    y + ToPhysicalY(ItemCardHeightDip, _monitor),
                    itemViewportBottom);
                if (bottom > top)
                {
                    regions.Add(new PosterInputRegion(
                        PosterInputTargetKind.Item,
                        index,
                        new DesktopPixelRect(x, top, width, bottom - top)));
                }
            }

            if (itemViewportBottom > itemStartY && ResolveMaximumItemScrollOffsetDip(frame, columns) > 0)
            {
                int scrollRailWidth = Math.Max(8, ToPhysicalX(12, _monitor));
                regions.Add(new PosterInputRegion(
                    PosterInputTargetKind.ItemScroll,
                    -1,
                    new DesktopPixelRect(
                        frame.Width - scrollRailWidth,
                        itemStartY,
                        scrollRailWidth,
                        Math.Max(1, itemViewportBottom - itemStartY))));
            }
        }

        if (_mode == PosterOperatingMode.Editing &&
            !_geometry.IsLocked &&
            !_geometry.IsCollapsed)
        {
            int edgeX = Math.Max(6, ToPhysicalX(8, _monitor));
            int edgeY = Math.Max(6, ToPhysicalY(8, _monitor));
            int cornerX = Math.Max(16, ToPhysicalX(20, _monitor));
            int cornerY = Math.Max(16, ToPhysicalY(20, _monitor));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTopLeft,
                -1,
                new DesktopPixelRect(0, 0, cornerX, cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTopRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - cornerX,
                    0,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottomLeft,
                -1,
                new DesktopPixelRect(
                    0,
                    frame.Height - cornerY,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottomRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - cornerX,
                    frame.Height - cornerY,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeLeft,
                -1,
                new DesktopPixelRect(
                    0,
                    cornerY,
                    edgeX,
                    Math.Max(1, frame.Height - (cornerY * 2)))));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - edgeX,
                    cornerY,
                    edgeX,
                    Math.Max(1, frame.Height - (cornerY * 2)))));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTop,
                -1,
                new DesktopPixelRect(
                    cornerX,
                    0,
                    Math.Max(1, frame.Width - (cornerX * 2)),
                    edgeY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottom,
                -1,
                new DesktopPixelRect(
                    cornerX,
                    frame.Height - edgeY,
                    Math.Max(1, frame.Width - (cornerX * 2)),
                    edgeY)));
        }

        regions.Add(new PosterInputRegion(
            PosterInputTargetKind.TitleBar,
            -1,
            new DesktopPixelRect(0, 0, frame.Width, headerHeight)));
        if (_mode == PosterOperatingMode.Editing && !_geometry.IsLocked)
        {
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.EditSurface,
                -1,
                new DesktopPixelRect(0, 0, frame.Width, frame.Height)));
        }

        return new PosterInputLayoutSnapshot(
            revision,
            frame.Width,
            frame.Height,
            PosterInputRegionClipPolicy.Clip(frame.Width, frame.Height, regions),
            ResolveOuterCornerRadiusPixels());
    }

    private Window CreateWindow()
    {
        var window = new Window
        {
            Title = "DesktopUp V3 · " + (_authoritativePoster?.Title ?? "Desktop collection"),
            ExtendsContentIntoTitleBar = false,
            Content = CreateContent()
        };
        window.AppWindow.IsShownInSwitchers = false;
        window.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
        return window;
    }

    private UIElement CreateContent()
    {
        var root = new Grid
        {
            // The native HRGN owns the outer transparency. Keep the WinUI root
            // painted so a newly exposed resize strip never falls through to
            // the HWND class/default background while composition catches up.
            Background = ColorBrush(_presentation.Parameters.Theme.Background, 245)
        };
        _surface = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = Brush(0, 0, 0, 0),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(SurfaceCornerRadiusDip)
        };
        root.Children.Add(_surface);

        var structure = new Grid();
        structure.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeightDip) });
        structure.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MetaHeightDip) });
        structure.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        structure.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FooterHeightDip) });
        _surface.Child = structure;

        Grid header = CreateHeader();
        Grid.SetRow(header, 0);
        structure.Children.Add(header);

        StackPanel meta = CreateMeta();
        meta.Visibility = MetaHeightDip == 0 ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRow(meta, 1);
        structure.Children.Add(meta);

        _itemsGrid = new Grid
        {
            Margin = new Thickness(HorizontalPaddingDip, 0, HorizontalPaddingDip, 0)
        };
        _itemsScrollViewer = new ScrollViewer
        {
            Content = _itemsGrid,
            Margin = new Thickness(0, ItemsTopInsetDip, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Disabled,
            IsVerticalRailEnabled = true
        };
        Grid.SetRow(_itemsScrollViewer, 2);
        structure.Children.Add(_itemsScrollViewer);

        Grid footer = CreateFooter();
        Grid.SetRow(footer, 3);
        structure.Children.Add(footer);
        RebuildItemGrid(_geometry.WidthDip);
        UpdateVisualState();
        return root;
    }

    private Grid CreateHeader()
    {
        var header = new Grid
        {
            Padding = new Thickness(
                PosterTitleBarLayoutPolicy.LeftPaddingDip,
                PosterTitleBarLayoutPolicy.TopPaddingDip,
                PosterTitleBarLayoutPolicy.RightPaddingDip,
                PosterTitleBarLayoutPolicy.BottomPaddingDip)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(PosterTitleBarLayoutPolicy.ActionGapDip)
        });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var collection = Text(_authoritativePoster?.Title ?? "Desktop collection", 14, 400, 255, 242, 242, 242);
        collection.Foreground = ColorBrush(_presentation.Parameters.Theme.Foreground);
        collection.TextTrimming = TextTrimming.CharacterEllipsis;
        collection.MaxLines = 1;
        Grid.SetColumn(collection, 0);
        header.Children.Add(collection);

        _collapseGlyph = CreateCircleGlyph("⌄");
        Grid.SetColumn(_collapseGlyph, 1);
        header.Children.Add(_collapseGlyph);
        _moreGlyph = CreateCircleGlyph("⋯");
        Grid.SetColumn(_moreGlyph, 3);
        header.Children.Add(_moreGlyph);
        return header;
    }

    private void ShowPosterMenu()
    {
        if (_disposed || _moreGlyph is null || _window is null || _inputIsland is null)
        {
            return;
        }

        _posterMenu?.Hide();
        long menuLayout = _inputIsland.AppliedLayoutRevision;
        long menuEntity = _posterEntityVersion;
        MenuInputDiagnostic? diagnostic = null;
        DesktopMenuInteraction? interaction = null;
        bool MenuCurrent(string command)
        {
            bool accepted = !_disposed && _posterEntityVersion == menuEntity && inputRouter.IsCurrent(inputSession, menuLayout);
            diagnostic?.Guard(command, accepted, menuEntity, menuLayout);
            return accepted;
        }
        var flyout = new MenuFlyout();
        var lockItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = _geometry.IsLocked ? "解除锁定" : "锁定",
            Icon = new FontIcon { Glyph = "\uE72E" }
        });
        lockItem.Click += (_, _) => { if (MenuCurrent("lock")) ToggleLocked("more-menu"); };
        flyout.Items.Add(lockItem);

        var editItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = _mode == PosterOperatingMode.Editing ? "完成编辑" : "进入编辑",
            Icon = new FontIcon { Glyph = "\uE70F" }
        });
        editItem.Click += (_, _) => { if (MenuCurrent("edit")) _ = SetEditing(_mode != PosterOperatingMode.Editing); };
        flyout.Items.Add(editItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var newFileItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = "新建文件",
            Icon = new FontIcon { Glyph = "\uE8A5" },
            IsEnabled = false
        });
        ToolTipService.SetToolTip(newFileItem, "将在 V3-05 文件能力中接入");
        flyout.Items.Add(newFileItem);

        var newFolderItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = "新建文件夹",
            Icon = new FontIcon { Glyph = "\uE8F4" },
            IsEnabled = false
        });
        ToolTipService.SetToolTip(newFolderItem, "将在 V3-05 文件能力中接入");
        flyout.Items.Add(newFolderItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var controlCenterItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = "打开控制中心",
            Icon = new FontIcon { Glyph = "\uE713" },
            IsEnabled = showControlCenter is not null
        });
        controlCenterItem.Click += (_, _) => { if (MenuCurrent("control-center")) showControlCenter?.Invoke(); };
        flyout.Items.Add(controlCenterItem);

        var removeItem = StyleMenuItem(new MenuFlyoutItem
        {
            Text = "删除海报…", Icon = new FontIcon { Glyph = "\uE74D" }
        });
        removeItem.Click += (_, _) =>
        {
            if (!MenuCurrent("remove-click")) return;
            var request = new PosterRemovalRequest(inputSession.PosterId, menuEntity,
                StateCommandSource.Poster, inputSession, menuLayout);
            flyout.Hide();
            _ = _window?.DispatcherQueue.TryEnqueue(() => { if (MenuCurrent("remove-dispatch")) requestRemoval(request); });
        };
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(removeItem);

        _posterMenu = flyout;
        _menuDiagnostic?.Dispose();
        _menuDiagnostic = MenuInputDiagnostics.Enabled ? MenuInputDiagnostic.TryCreate(flyout, _window.DispatcherQueue, log,
            () => new { PosterId, inputSession.Epoch, mode = _mode.ToString(), _posterEntityVersion,
                menuEntity, menuLayout, appliedLayout = _inputIsland?.AppliedLayoutRevision,
                current = !_disposed && _posterEntityVersion == menuEntity && inputRouter.IsCurrent(inputSession, menuLayout),
                frame = _bounds, visual = $"0x{_windowHandle:X}", island = $"0x{_inputIsland?.WindowHandle:X}" }) : null;
        diagnostic = _menuDiagnostic;
        flyout.Opened += (_, _) =>
        {
            if (ReferenceEquals(_posterMenu, flyout)) _ = interaction?.ActivatePopup();
        };
        flyout.Closed += (_, _) =>
        {
            interaction?.Dispose();
            if (ReferenceEquals(_menuInteraction, interaction)) _menuInteraction = null;
            diagnostic?.Dispose();
            if (ReferenceEquals(_menuDiagnostic, diagnostic)) _menuDiagnostic = null;
            if (ReferenceEquals(_posterMenu, flyout))
            {
                _posterMenu = null;
            }
            if (!_disposed)
            {
                bool restored = ReassertCurrentClusterBottom();
                log.Write(
                    restored ? "info" : "warning",
                    "poster.more_menu_closed",
                    new { desktopBottomRestored = restored });
            }
        };

        try
        {
            interaction = DesktopMenuInteraction.Begin(_windowHandle,
                PosterInputIslandManager.MenuSurfaces(inputRouter), () => flyout.Hide(),
                action => { _ = _window?.DispatcherQueue.TryEnqueue(() => action()); }, ledger, log);
            _menuInteraction = interaction;
            flyout.ShowAt(_moreGlyph);
            log.Write("info", "poster.more_menu_opened", new
            {
                _geometry.IsLocked,
                mode = _mode.ToString()
            });
        }
        catch (Exception exception)
        {
            interaction?.Cancel();
            if (ReferenceEquals(_menuInteraction, interaction)) _menuInteraction = null;
            diagnostic?.Dispose();
            if (ReferenceEquals(_menuDiagnostic, diagnostic)) _menuDiagnostic = null;
            _posterMenu = null;
            _ = ReassertCurrentClusterBottom();
            log.Write("warning", "poster.more_menu_failed", new
            {
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message
            });
        }
    }

    private static T StyleMenuItem<T>(T item)
        where T : MenuFlyoutItemBase
    {
        item.FontFamily = new FontFamily(
            "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI");
        item.FontSize = 13;
        item.MinHeight = 36;
        item.Padding = new Thickness(10, 8, 10, 8);
        return item;
    }

    private StackPanel CreateMeta()
    {
        var meta = new StackPanel
        {
            Margin = new Thickness(HorizontalPaddingDip, 18, HorizontalPaddingDip, 0),
            Spacing = 5
        };
        var subtitle = Text(_authoritativePoster?.Subtitle ?? "", 11, 500, 230, 153, 206, 255);
        subtitle.Foreground = ColorBrush(_presentation.Parameters.Theme.Accent, 230);
        subtitle.MaxLines = 1; subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        var title = Text(_authoritativePoster?.Title ?? "Desktop collection", ContentParameters.TitleFontSizeDip, 700, 255, 242, 242, 242);
        title.Foreground = ColorBrush(_presentation.Parameters.Theme.Foreground);
        title.MaxLines = 1; title.TextTrimming = TextTrimming.CharacterEllipsis;
        meta.Children.Add(subtitle); meta.Children.Add(title);
        meta.Children.Add(Text($"{Items.Length:00} DEMO ITEMS", 10, 400, 170, 198, 198, 198));
        return meta;
    }

    private Grid CreateFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(HorizontalPaddingDip, 0, HorizontalPaddingDip, 0),
            BorderBrush = Brush(16, 255, 255, 255),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(Text($"{Items.Length} DEMO ITEMS", 10, 400, 150, 198, 198, 198));
        _modeLabel = Text("USING  ·  DRAG TO MOVE", 10, 400, 150, 198, 198, 198);
        Grid.SetColumn(_modeLabel, 1);
        footer.Children.Add(_modeLabel);
        return footer;
    }

    private void RebuildItemGrid(double widthDip)
    {
        if (_itemsGrid is null)
        {
            return;
        }
        int columns = ResolveColumns(widthDip);
        if (_itemColumnCount == columns)
        {
            return;
        }

        _itemColumnCount = columns;
        _itemsGrid.Children.Clear();
        _itemsGrid.ColumnDefinitions.Clear();
        _itemsGrid.RowDefinitions.Clear();
        _itemsGrid.ColumnSpacing = ItemGapDip;
        _itemsGrid.RowSpacing = ItemGapDip;
        for (int column = 0; column < columns; column++)
        {
            _itemsGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }
        int rows = (int)Math.Ceiling(Items.Length / (double)columns);
        for (int row = 0; row < rows; row++)
        {
            _itemsGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(ItemRowHeightDip)
            });
        }

        for (int index = 0; index < Items.Length; index++)
        {
            FrameworkElement card = CreateItem(Items[index]);
            Grid.SetColumn(card, index % columns);
            Grid.SetRow(card, index / columns);
            _itemsGrid.Children.Add(card);
        }
    }

    private void ScrollItems(int wheelDelta)
    {
        if (_monitor is null || _inputIsland is null ||
            _geometry.IsCollapsed || wheelDelta == 0)
        {
            return;
        }

        int columns = ResolveColumns(FrameWidthDip(_bounds));
        double maximumOffset = ResolveMaximumItemScrollOffsetDip(_bounds, columns);
        double nextOffset = PosterItemViewportPolicy.ResolveWheelOffset(
            _itemScrollOffsetDip,
            wheelDelta,
            ItemRowHeightDip + ItemGapDip,
            maximumOffset);
        if (Math.Abs(nextOffset - _itemScrollOffsetDip) < 0.5)
        {
            return;
        }

        _itemScrollOffsetDip = nextOffset;
        ApplyItemScrollOffset();
        bool applied = _inputIsland.ApplyLayout(_bounds, BuildInputLayout(_bounds));
        log.Write(applied ? "info" : "warning", "poster.items_scrolled", new
        {
            wheelDelta,
            offsetDip = _itemScrollOffsetDip,
            maximumOffsetDip = maximumOffset,
            columns,
            layoutRevision = _layoutRevision,
            applied
        });
    }

    private void UpdateItemScrollState(DesktopPixelRect frame)
    {
        int columns = ResolveColumns(FrameWidthDip(frame));
        double maximumOffset = ResolveMaximumItemScrollOffsetDip(frame, columns);
        _itemScrollOffsetDip = Math.Clamp(_itemScrollOffsetDip, 0, maximumOffset);
        ApplyItemScrollOffset();
    }

    private void ApplyItemScrollOffset()
    {
        if (_itemsScrollViewer is null)
        {
            return;
        }

        if (Math.Abs(_itemsScrollViewer.VerticalOffset - _itemScrollOffsetDip) < 0.5)
        {
            return;
        }
        if (_itemsScrollViewer.ChangeView(
            null,
            _itemScrollOffsetDip,
            null,
            disableAnimation: true))
        {
            return;
        }
        if (_window is not null)
        {
            _ = _window.DispatcherQueue.TryEnqueue(() =>
                _ = _itemsScrollViewer?.ChangeView(
                    null,
                    _itemScrollOffsetDip,
                    null,
                    disableAnimation: true));
        }
    }

    private double ResolveMaximumItemScrollOffsetDip(
        DesktopPixelRect frame,
        int columns)
    {
        if (_monitor is null)
        {
            return 0;
        }

        double viewportHeight = Math.Max(
            0,
            (frame.Height / _monitor.ScaleY) -
                HeaderHeightDip - MetaHeightDip - FooterHeightDip - ItemsTopInsetDip);
        return PosterItemViewportPolicy.ResolveMaximumOffset(
            Items.Length,
            columns,
            ItemRowHeightDip,
            ItemGapDip,
            viewportHeight);
    }

    private FrameworkElement CreateItem(PosterItem item)
    {
        var cell = new Grid();
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ItemCardHeightDip) });
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ContentParameters.LabelHeightDip + 7) });
        var card = new Border
        {
            Background = Brush(230, 22, 24, 29),
            BorderBrush = Brush(24, 255, 255, 255),
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(4)
        };
        var cardGrid = new Grid();
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        var accent = new Border
        {
            Width = 24,
            Height = Math.Max(24, ItemCardHeightDip * .8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush(230, item.Red, item.Green, item.Blue),
            RenderTransform = new RotateTransform { Angle = 25 },
            RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5)
        };
        if (_presentation.EffectiveTemplateKey == "night-ops") cardGrid.Children.Add(accent);
        var initials = Text(item.Initials, ContentParameters.IconSizeDip * .5, 700, 255, 242, 246, 248);
        initials.Width = ContentParameters.IconSizeDip;
        initials.Height = ContentParameters.IconSizeDip;
        initials.TextAlignment = TextAlignment.Center;
        initials.Foreground = ColorBrush(_presentation.Parameters.Theme.Foreground);
        initials.HorizontalAlignment = HorizontalAlignment.Center;
        initials.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumnSpan(initials, 2);
        cardGrid.Children.Add(initials);
        var index = Text(item.Index, 8, 110, 255, 126, 137, 149);
        index.HorizontalAlignment = HorizontalAlignment.Center;
        index.VerticalAlignment = VerticalAlignment.Bottom;
        index.Margin = new Thickness(0, 0, 0, 6);
        Grid.SetColumn(index, 1);
        cardGrid.Children.Add(index);
        card.Child = cardGrid;
        cell.Children.Add(card);
        TextBlock name = Text(item.Name, ContentParameters.LabelFontSizeDip, 400, 185, 218, 218, 218);
        name.Foreground = ColorBrush(_presentation.Parameters.Theme.Foreground, 185);
        name.MaxLines = 1;
        name.Margin = new Thickness(0, 7, 0, 0);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(name, 1);
        cell.Children.Add(name);
        return cell;
    }

    private void UpdateVisualState()
    {
        if (_surface is null)
        {
            return;
        }
        if (_itemsGrid is not null)
        {
            _itemsGrid.Visibility = _geometry.IsCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        if (_itemsScrollViewer is not null)
        {
            _itemsScrollViewer.Visibility = _geometry.IsCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        if (_collapseGlyph is not null)
        {
            _collapseGlyph.Text = _geometry.IsCollapsed ? "⌃" : "⌄";
        }
        if (_modeLabel is not null)
        {
            _modeLabel.Text = _geometry.IsLocked
                ? "LOCKED"
                : _mode == PosterOperatingMode.Editing
                    ? "EDITING  ·  RESIZE ENABLED"
                    : "USING  ·  DRAG TO MOVE";
        }
        _surface.BorderBrush = _mode == PosterOperatingMode.Editing
            ? ColorBrush(_presentation.Parameters.Theme.Accent, 230)
            : Brush(0, 0, 0, 0);
        _surface.BorderThickness = _mode == PosterOperatingMode.Editing
            ? new Thickness(1)
            : new Thickness(0);
    }

    private bool ApplyVisualRegion(
        nint windowHandle,
        DesktopPixelRect frame,
        bool force = false)
    {
        int cornerRadius = ResolveOuterCornerRadiusPixels();
        var requested = new VisualRegionState(
            windowHandle,
            frame.Width,
            frame.Height,
            cornerRadius);
        if (!force && _appliedVisualRegion == requested)
        {
            return true;
        }

        bool applied = windowHandle != 0 &&
            attachment.ApplyRoundedRegion(
                windowHandle,
                frame.Width,
                frame.Height,
                cornerRadius);
        if (applied)
        {
            _appliedVisualRegion = requested;
        }
        return applied;
    }

    private int ResolveOuterCornerRadiusPixels()
    {
        if (_monitor is null)
        {
            return checked((int)SurfaceCornerRadiusDip);
        }

        double scale = Math.Min(_monitor.ScaleX, _monitor.ScaleY);
        return Math.Max(
            1,
            checked((int)Math.Round(SurfaceCornerRadiusDip * scale)));
    }

    private static DesktopPixelRect ResolveGestureFrame(
        PosterGestureKind gesture,
        DesktopPixelRect initial,
        int deltaX,
        int deltaY,
        DisplayMonitorSnapshot monitor)
    {
        return PosterFrameResizePolicy.Resolve(
            gesture,
            initial,
            deltaX,
            deltaY,
            monitor.WorkArea,
            ToPhysicalX(PosterFrameGeometryPolicy.MinimumWidthDip, monitor),
            ToPhysicalY(PosterFrameGeometryPolicy.MinimumExpandedHeightDip, monitor));
    }

    private static bool TryResolveGesture(
        PosterInputTargetKind target,
        out PosterGestureKind gesture)
    {
        gesture = target switch
        {
            PosterInputTargetKind.TitleBar or PosterInputTargetKind.EditSurface =>
                PosterGestureKind.Move,
            PosterInputTargetKind.ResizeRight => PosterGestureKind.ResizeRight,
            PosterInputTargetKind.ResizeBottom => PosterGestureKind.ResizeBottom,
            PosterInputTargetKind.ResizeBottomRight => PosterGestureKind.ResizeBottomRight,
            PosterInputTargetKind.ResizeLeft => PosterGestureKind.ResizeLeft,
            PosterInputTargetKind.ResizeTop => PosterGestureKind.ResizeTop,
            PosterInputTargetKind.ResizeTopLeft => PosterGestureKind.ResizeTopLeft,
            PosterInputTargetKind.ResizeTopRight => PosterGestureKind.ResizeTopRight,
            PosterInputTargetKind.ResizeBottomLeft => PosterGestureKind.ResizeBottomLeft,
            _ => (PosterGestureKind)(-1)
        };
        return (int)gesture >= 0;
    }

    private static DesktopPixelRect CreateDefaultBounds(DisplayMonitorSnapshot monitor)
    {
        DesktopPixelRect work = monitor.WorkArea;
        double workWidthDip = work.Width / monitor.ScaleX;
        double workHeightDip = work.Height / monitor.ScaleY;
        double widthDip = Math.Clamp(
            Math.Max(workWidthDip * 0.22, 280),
            PosterFrameGeometryPolicy.MinimumWidthDip,
            workWidthDip * 0.45);
        double heightDip = Math.Max(
            PosterFrameGeometryPolicy.MinimumExpandedHeightDip,
            workHeightDip - (SafeMarginDip * 2));
        int width = ToPhysicalX(widthDip, monitor);
        int height = Math.Min(ToPhysicalY(heightDip, monitor), work.Height);
        int marginX = ToPhysicalX(SafeMarginDip, monitor);
        int marginY = ToPhysicalY(SafeMarginDip, monitor);
        return new DesktopPixelRect(
            Math.Max(work.X, work.Right - width - marginX),
            Math.Min(work.Bottom - height, work.Y + marginY),
            width,
            height);
    }

    private static PosterFrameGeometry GeometryFromBounds(
        DesktopPixelRect bounds,
        DisplayMonitorSnapshot monitor,
        bool isLocked,
        bool isCollapsed,
        long revision,
        double? preservedExpandedHeightDip = null) => new(
            (bounds.X - monitor.WorkArea.X) / monitor.ScaleX,
            (bounds.Y - monitor.WorkArea.Y) / monitor.ScaleY,
            bounds.Width / monitor.ScaleX,
            isCollapsed
                ? Math.Max(
                    PosterFrameGeometryPolicy.MinimumExpandedHeightDip,
                    preservedExpandedHeightDip ?? bounds.Height / monitor.ScaleY)
                : bounds.Height / monitor.ScaleY,
            isLocked,
            isCollapsed,
            revision);

    private static PosterPlacementState PlacementFromBounds(
        DesktopPixelRect bounds,
        DisplayMonitorSnapshot monitor)
    {
        DesktopPixelRect work = monitor.WorkArea;
        double xRange = Math.Max(1, work.Width - bounds.Width);
        double yRange = Math.Max(1, work.Height - bounds.Height);
        return new PosterPlacementState(
            bounds.Width / monitor.ScaleX,
            bounds.Height / monitor.ScaleY,
            Math.Clamp((bounds.X - work.X) / xRange, 0, 1),
            Math.Clamp((bounds.Y - work.Y) / yRange, 0, 1));
    }

    private double FrameWidthDip(DesktopPixelRect frame) =>
        _monitor is null ? frame.Width : frame.Width / _monitor.ScaleX;

    private int ResolveColumns(double widthDip) => PosterPresentationPolicy.ResolveColumns(widthDip, ContentParameters);

    private static TextBlock CreateCircleGlyph(string glyph) => new()
    {
        Text = glyph,
        Width = PosterTitleBarLayoutPolicy.ActionSizeDip,
        Height = PosterTitleBarLayoutPolicy.ActionSizeDip,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 14,
        Foreground = Brush(225, 226, 226, 226)
    };

    private static TextBlock Text(
        string value,
        double size,
        int weight,
        byte alpha,
        byte red,
        byte green,
        byte blue) => new()
    {
        Text = value,
        FontSize = size,
        FontWeight = new global::Windows.UI.Text.FontWeight { Weight = (ushort)weight },
        FontFamily = new FontFamily("Segoe UI Variable Text"),
        Foreground = Brush(alpha, red, green, blue),
        VerticalAlignment = VerticalAlignment.Center,
        CharacterSpacing = size <= 11 ? 100 : 0
    };

    private static SolidColorBrush Brush(byte alpha, byte red, byte green, byte blue) =>
        new(ColorHelper.FromArgb(alpha, red, green, blue));

    private static int ToPhysicalX(double dip, DisplayMonitorSnapshot monitor) =>
        Math.Max(1, checked((int)Math.Round(dip * monitor.ScaleX)));

    private static int ToPhysicalY(double dip, DisplayMonitorSnapshot monitor) =>
        Math.Max(1, checked((int)Math.Round(dip * monitor.ScaleY)));

    private static DesktopPixelRect ToPhysicalRect(
        PosterTitleBarActionLayout action,
        DisplayMonitorSnapshot monitor) => new(
            checked((int)Math.Round(action.XDip * monitor.ScaleX)),
            checked((int)Math.Round(action.YDip * monitor.ScaleY)),
            ToPhysicalX(action.SizeDip, monitor),
            ToPhysicalY(action.SizeDip, monitor));

    private static int ToPhysicalYOffset(double dip, DisplayMonitorSnapshot monitor) =>
        checked((int)Math.Round(dip * monitor.ScaleY));

    private static RectInt32 ToRect(DesktopPixelRect bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static bool HasExpectedBounds(Window window, DesktopPixelRect expected)
    {
        PointInt32 position = window.AppWindow.Position;
        SizeInt32 size = window.AppWindow.Size;
        return position.X == expected.X &&
            position.Y == expected.Y &&
            size.Width == expected.Width &&
            size.Height == expected.Height;
    }

    private void LogAttachment(string eventName, DesktopLayerAttachResult result)
    {
        log.Write(result.Succeeded ? "info" : "error", eventName, new
        {
            result.Succeeded,
            windowHandle = $"0x{result.WindowHandle:X}",
            expectedOwner = $"0x{result.ExpectedOwner:X}",
            actualOwner = $"0x{result.ActualOwner:X}",
            result.DesktopGeneration,
            result.NativeError,
            result.Error
        });
    }

    private void ClearVisualReferences()
    {
        _menuInteraction?.Cancel();
        _menuInteraction = null;
        MenuFlyout? menu = _posterMenu;
        _posterMenu = null;
        menu?.Hide();
        _surface = null;
        _itemsScrollViewer = null;
        _itemsGrid = null;
        _collapseGlyph = null;
        _moreGlyph = null;
        _modeLabel = null;
        _appliedVisualRegion = null;
    }

    public void Dispose()
    {
        _menuInteraction?.Cancel();
        _menuInteraction = null;
        _menuDiagnostic?.Dispose();
        _menuDiagnostic = null;
        StopExpansionFeedback();
        spatial.Remove(this);
        _spatialGesture?.Cancel();
        _spatialGesture = null;
        if (_disposed && _window is null && _inputIsland is null && _windowLease is null) return;
        _disposed = true;
        var failures = new List<Exception>();
        void Cleanup(Action action)
        {
            try { action(); } catch (Exception exception) { failures.Add(exception); }
        }
        CancelScheduledGestureFrame();
        if (_gestureFrameTimer is not null)
        {
            _gestureFrameTimer.Tick -= OnGestureFrameTick;
            _gestureFrameTimer = null;
        }
        if (_inputIsland is not null)
        {
            _inputIsland.Input -= OnInput;
            Cleanup(() => { _inputIsland.Dispose(); _inputIsland = null; });
        }
        if (_window is not null)
        {
            Cleanup(() => { _ = attachment.Detach(_windowHandle); });
            Cleanup(() =>
            {
                _window.Close();
                _window = null;
                log.Write("info", "poster.disposed", new { handle = $"0x{_windowHandle:X}" });
                _windowHandle = 0;
                _desktopHost = null;
                _monitor = null;
                _attachedDesktopGeneration = 0;
                _appliedTopologyGeneration = 0;
                ClearVisualReferences();
            });
        }
        if (_window is null) Cleanup(() => { _windowLease?.Dispose(); _windowLease = null; });
        if (failures.Count != 0) throw new AggregateException("Poster session cleanup failed.", failures);
    }

    private sealed record PosterItem(
        string Initials,
        string Name,
        string Index,
        byte Red,
        byte Green,
        byte Blue);

    private readonly record struct PendingGestureFrame(
        DesktopPixelRect Candidate,
        DesktopPixelRect InitialFrame);

    private readonly record struct VisualRegionState(
        nint WindowHandle,
        int Width,
        int Height,
        int CornerRadius);
}
