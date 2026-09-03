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

internal sealed class PosterVisualWindow(
    IDesktopLayerAttachment attachment,
    IResourceLedger ledger,
    IStructuredLog log) : IDisposable
{
    private const double SafeMarginDip = 24;
    private const double HeaderHeightDip = 60;
    private const double MetaHeightDip = 130;
    private const double ItemsTopInsetDip = 10;
    private const double FooterHeightDip = 42;
    private const double HorizontalPaddingDip = 28;
    private const double ItemGapDip = 12;
    private const double ItemCardHeightDip = 82;
    private const double ItemRowHeightDip = 118;
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
    private TextBlock? _lockGlyph;
    private TextBlock? _collapseGlyph;
    private TextBlock? _modeLabel;
    private nint _windowHandle;
    private IDisposable? _windowLease;
    private PosterInputIslandManager? _inputIsland;
    private DesktopShellHostSnapshot? _desktopHost;
    private DisplayMonitorSnapshot? _monitor;
    private DesktopPixelRect _bounds;
    private PosterPlacementState _placement;
    private PosterFrameGeometry _geometry;
    private PosterOperatingMode _mode = PosterOperatingMode.Using;
    private long _layoutRevision;
    private long _attachedDesktopGeneration;
    private long _appliedTopologyGeneration;
    private int _itemColumnCount = -1;
    private double _itemScrollOffsetDip;
    private DispatcherQueueTimer? _gestureFrameTimer;
    private PendingGestureFrame? _pendingGestureFrame;
    private bool _gestureFrameScheduled;
    private bool _disposed;

    public nint WindowHandle => _windowHandle;
    public bool IsEditing => _mode == PosterOperatingMode.Editing;

    public DesktopRecoveryGenerationPair? CurrentGenerationPair =>
        _attachedDesktopGeneration > 0 && _appliedTopologyGeneration > 0
            ? new DesktopRecoveryGenerationPair(
                _attachedDesktopGeneration,
                _appliedTopologyGeneration)
            : null;

    public bool Show(
        DesktopShellHostSnapshot desktopHost,
        DisplayTopologySnapshot displayTopology)
    {
        if (_window is not null)
        {
            return Recover(desktopHost, displayTopology, request: 0);
        }

        DisplayMonitorSnapshot? primaryMonitor = displayTopology.PrimaryMonitor;
        if (primaryMonitor is null)
        {
            log.Write("error", "poster.show_failed", new { error = "primary-monitor-missing" });
            return false;
        }

        _monitor = primaryMonitor;
        _bounds = CreateDefaultBounds(primaryMonitor);
        _geometry = GeometryFromBounds(
            _bounds,
            primaryMonitor,
            isLocked: false,
            isCollapsed: false,
            revision: 1);
        _placement = PlacementFromBounds(_bounds, primaryMonitor);

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
            ReassertCurrentClusterBottom);
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

                bool restored = ReassertDesktopBottom(_desktopHost);
                log.Write(
                    restored ? "info" : "warning",
                    "poster.post_composition_reasserted",
                    new
                    {
                        restored,
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
            displayTopology.PrimaryMonitor is not { } primaryMonitor)
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
                checked(_geometry.Revision + 1),
                _geometry.ExpandedHeightDip);
            _appliedTopologyGeneration = displayTopology.Generation;
            log.Write("info", "poster_reproject.committed", new
            {
                request,
                generation = displayTopology.Generation,
                bounds = expectedBounds,
                primaryMonitor.DeviceName,
                primaryMonitor.DpiX,
                primaryMonitor.DpiY
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
        if (_window is null || _monitor is null || _inputIsland is null)
        {
            return;
        }
        if (args.LayoutRevision != _inputIsland.AppliedLayoutRevision)
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

        if (args.EventKind == PosterInputEventKind.GestureCancelled)
        {
            CancelScheduledGestureFrame();
            _ = ApplyFrame(args.InitialFrame);
            return;
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
        if (!ApplyFrame(candidate))
        {
            _ = ApplyFrame(args.InitialFrame);
            return;
        }

        if (args.EventKind == PosterInputEventKind.GestureCompleted)
        {
            _geometry = GeometryFromBounds(
                candidate,
                _monitor,
                _geometry.IsLocked,
                _geometry.IsCollapsed,
                checked(_geometry.Revision + 1),
                _geometry.ExpandedHeightDip);
            _placement = PlacementFromBounds(candidate, _monitor);
            log.Write("info", "poster.geometry_committed", new
            {
                gesture = gesture.ToString(),
                geometryRevision = _geometry.Revision,
                layoutRevision = _layoutRevision,
                bounds = candidate
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
        if (pending is not { } frame || ApplyFrame(frame.Candidate))
        {
            return;
        }

        _ = ApplyFrame(frame.InitialFrame);
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
                _geometry = PosterFrameGeometryPolicy.ToggleLocked(_geometry);
                UpdateVisualState();
                _ = _inputIsland?.ApplyLayout(_bounds, BuildInputLayout(_bounds));
                log.Write("info", "poster.lock_changed", new
                {
                    _geometry.IsLocked,
                    _geometry.Revision,
                    layoutRevision = _layoutRevision
                });
                break;
            case PosterInputTargetKind.CollapseButton:
                ToggleCollapsed();
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

    private void ToggleCollapsed()
    {
        if (_monitor is null)
        {
            return;
        }

        PosterFrameGeometry next = PosterFrameGeometryPolicy.ToggleCollapsed(_geometry);
        int height = next.IsCollapsed
            ? ToPhysicalY(PosterFrameGeometryPolicy.CollapsedHeightDip, _monitor)
            : ToPhysicalY(next.ExpandedHeightDip, _monitor);
        DesktopPixelRect work = _monitor.WorkArea;
        var nextBounds = new DesktopPixelRect(
            _bounds.X,
            _bounds.Y,
            _bounds.Width,
            Math.Min(height, work.Bottom - _bounds.Y));

        PosterFrameGeometry previous = _geometry;
        _geometry = next;
        UpdateVisualState();
        if (!ApplyFrame(nextBounds))
        {
            _geometry = previous;
            UpdateVisualState();
            _ = ApplyFrame(_bounds);
            return;
        }

        _placement = PlacementFromBounds(nextBounds, _monitor);
        log.Write("info", "poster.collapse_changed", new
        {
            _geometry.IsCollapsed,
            _geometry.Revision,
            topPreserved = true,
            leftPreserved = true,
            widthPreserved = true,
            bounds = nextBounds
        });
    }

    private bool ApplyFrame(DesktopPixelRect frame)
    {
        if (_window is null || _inputIsland is null || !frame.IsUsable)
        {
            return false;
        }

        _window.AppWindow.MoveAndResize(ToRect(frame));
        if (!HasExpectedBounds(_window, frame))
        {
            return false;
        }

        _bounds = frame;
        RebuildItemGrid(FrameWidthDip(frame));
        UpdateItemScrollState(frame);
        return _inputIsland.ApplyLayout(frame, BuildInputLayout(frame));
    }

    private PosterInputLayoutSnapshot BuildInputLayout(DesktopPixelRect frame)
    {
        if (_monitor is null)
        {
            throw new InvalidOperationException("A monitor snapshot is required for input layout.");
        }

        long revision = checked(++_layoutRevision);
        double scaleX = _monitor.ScaleX;
        int headerHeight = ToPhysicalY(HeaderHeightDip, _monitor);
        int buttonSize = ToPhysicalX(34, _monitor);
        int buttonTop = ToPhysicalY(13, _monitor);
        int buttonGap = ToPhysicalX(10, _monitor);
        int right = ToPhysicalX(20, _monitor);
        int collapseX = frame.Width - right - buttonSize;
        int lockX = collapseX - buttonGap - buttonSize;
        var regions = new List<PosterInputRegion>
        {
            new(PosterInputTargetKind.LockButton, -1,
                new DesktopPixelRect(lockX, buttonTop, buttonSize, buttonSize)),
            new(PosterInputTargetKind.CollapseButton, -1,
                new DesktopPixelRect(collapseX, buttonTop, buttonSize, buttonSize))
        };

        if (!_geometry.IsCollapsed)
        {
            double widthDip = frame.Width / scaleX;
            int columns = ResolveColumns(widthDip);
            double rowStrideDip = ItemRowHeightDip + ItemGapDip;
            double availableDip = Math.Max(
                1,
                widthDip - (HorizontalPaddingDip * 2) - (ItemGapDip * (columns - 1)));
            double cardWidthDip = availableDip / columns;
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

            if (ResolveMaximumItemScrollOffsetDip(frame, columns) > 0)
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
            int edge = Math.Max(6, ToPhysicalX(8, _monitor));
            int corner = Math.Max(16, ToPhysicalX(20, _monitor));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottomRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - corner,
                    frame.Height - corner,
                    corner,
                    corner)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - edge,
                    headerHeight,
                    edge,
                    Math.Max(1, frame.Height - headerHeight - corner))));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottom,
                -1,
                new DesktopPixelRect(
                    0,
                    frame.Height - edge,
                    Math.Max(1, frame.Width - corner),
                    edge)));
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
            regions);
    }

    private Window CreateWindow()
    {
        var window = new Window
        {
            Title = "DesktopUp V3 NIGHT OPS",
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
            Background = new SolidColorBrush(Colors.Transparent)
        };
        _surface = new Border
        {
            Background = Brush(232, 5, 15, 24),
            BorderBrush = Brush(190, 105, 226, 240),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(24)
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
            Padding = new Thickness(24, 0, 20, 0)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var collection = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 10
        };
        collection.Children.Add(Text("DESKTOP COLLECTION", 10, 110, 255, 137, 158, 172));
        collection.Children.Add(new Border
        {
            Width = 86,
            Height = 1,
            Background = Brush(210, 76, 197, 218)
        });
        collection.Children.Add(Text("01", 10, 120, 255, 105, 222, 238));
        Grid.SetColumn(collection, 0);
        header.Children.Add(collection);

        _lockGlyph = CreateCircleGlyph("○");
        Grid.SetColumn(_lockGlyph, 1);
        header.Children.Add(_lockGlyph);
        _collapseGlyph = CreateCircleGlyph("⌄");
        Grid.SetColumn(_collapseGlyph, 3);
        header.Children.Add(_collapseGlyph);
        return header;
    }

    private static StackPanel CreateMeta()
    {
        var meta = new StackPanel
        {
            Margin = new Thickness(HorizontalPaddingDip, 22, HorizontalPaddingDip, 0),
            Spacing = 5
        };
        meta.Children.Add(Text("TACTICAL ARCHIVE / 01", 11, 120, 255, 103, 218, 235));
        meta.Children.Add(Text("NIGHT OPS", 36, 700, 255, 239, 244, 247));
        meta.Children.Add(Text("06 TITLES  ·  READY", 9, 130, 255, 119, 132, 145));
        return meta;
    }

    private Grid CreateFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(HorizontalPaddingDip, 0, HorizontalPaddingDip, 0),
            BorderBrush = Brush(42, 126, 147, 160),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(Text("LOCAL LIBRARY", 9, 110, 255, 100, 113, 126));
        _modeLabel = Text("USING  ·  DRAG TO MOVE", 9, 110, 255, 100, 113, 126);
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

    private static FrameworkElement CreateItem(PosterItem item)
    {
        var cell = new Grid();
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ItemCardHeightDip) });
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        var card = new Border
        {
            Background = Brush(205, 7, 15, 25),
            BorderBrush = Brush(80, 116, 140, 154),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7)
        };
        var cardGrid = new Grid();
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        var accent = new Border
        {
            Width = 24,
            Height = 118,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush(230, item.Red, item.Green, item.Blue),
            RenderTransform = new RotateTransform { Angle = 25 },
            RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5)
        };
        cardGrid.Children.Add(accent);
        var initials = Text(item.Initials, 24, 700, 255, 242, 246, 248);
        initials.HorizontalAlignment = HorizontalAlignment.Center;
        initials.VerticalAlignment = VerticalAlignment.Center;
        cardGrid.Children.Add(initials);
        var index = Text(item.Index, 8, 110, 255, 126, 137, 149);
        index.HorizontalAlignment = HorizontalAlignment.Center;
        index.VerticalAlignment = VerticalAlignment.Bottom;
        index.Margin = new Thickness(0, 0, 0, 6);
        Grid.SetColumn(index, 1);
        cardGrid.Children.Add(index);
        card.Child = cardGrid;
        cell.Children.Add(card);
        TextBlock name = Text(item.Name, 9, 115, 255, 171, 181, 190);
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
        if (_lockGlyph is not null)
        {
            _lockGlyph.Text = _geometry.IsLocked ? "●" : "○";
            _lockGlyph.Foreground = _geometry.IsLocked
                ? Brush(255, 216, 180, 96)
                : Brush(255, 169, 187, 198);
        }
        if (_collapseGlyph is not null)
        {
            _collapseGlyph.Text = _geometry.IsCollapsed ? "⌃" : "⌄";
        }
        if (_modeLabel is not null)
        {
            _modeLabel.Text = _mode == PosterOperatingMode.Editing
                ? "EDITING  ·  RESIZE ENABLED"
                : "USING  ·  DRAG TO MOVE";
        }
        _surface.BorderBrush = _mode == PosterOperatingMode.Editing
            ? Brush(190, 105, 226, 240)
            : Brush(0, 0, 0, 0);
        _surface.BorderThickness = _mode == PosterOperatingMode.Editing
            ? new Thickness(1)
            : new Thickness(0);
    }

    private static DesktopPixelRect ResolveGestureFrame(
        PosterGestureKind gesture,
        DesktopPixelRect initial,
        int deltaX,
        int deltaY,
        DisplayMonitorSnapshot monitor)
    {
        DesktopPixelRect work = monitor.WorkArea;
        if (gesture == PosterGestureKind.Move)
        {
            int x = Math.Clamp(initial.X + deltaX, work.X, work.Right - initial.Width);
            int y = Math.Clamp(initial.Y + deltaY, work.Y, work.Bottom - initial.Height);
            return initial with { X = x, Y = y };
        }

        int minimumWidth = ToPhysicalX(PosterFrameGeometryPolicy.MinimumWidthDip, monitor);
        int minimumHeight = ToPhysicalY(PosterFrameGeometryPolicy.MinimumExpandedHeightDip, monitor);
        int maximumWidth = Math.Min(
            work.Right - initial.X,
            Math.Max(minimumWidth, (int)Math.Round(work.Width * 0.45)));
        int maximumHeight = Math.Max(minimumHeight, work.Bottom - initial.Y);
        int width = initial.Width;
        int height = initial.Height;
        if (gesture is PosterGestureKind.ResizeRight or PosterGestureKind.ResizeBottomRight)
        {
            width = Math.Clamp(initial.Width + deltaX, minimumWidth, maximumWidth);
        }
        if (gesture is PosterGestureKind.ResizeBottom or PosterGestureKind.ResizeBottomRight)
        {
            height = Math.Clamp(initial.Height + deltaY, minimumHeight, maximumHeight);
        }
        return initial with { Width = width, Height = height };
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

    private static int ResolveColumns(double widthDip) => widthDip switch
    {
        >= 440 => 3,
        >= 310 => 2,
        _ => 1
    };

    private static TextBlock CreateCircleGlyph(string glyph) => new()
    {
        Text = glyph,
        Width = 34,
        Height = 34,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 19,
        Foreground = Brush(255, 169, 187, 198)
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
        _surface = null;
        _itemsScrollViewer = null;
        _itemsGrid = null;
        _lockGlyph = null;
        _collapseGlyph = null;
        _modeLabel = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        CancelScheduledGestureFrame();
        if (_gestureFrameTimer is not null)
        {
            _gestureFrameTimer.Tick -= OnGestureFrameTick;
            _gestureFrameTimer = null;
        }
        if (_inputIsland is not null)
        {
            _inputIsland.Input -= OnInput;
            _inputIsland.Dispose();
            _inputIsland = null;
        }
        if (_window is not null)
        {
            _ = attachment.Detach(_windowHandle);
            _window.Close();
            _window = null;
            log.Write("info", "poster.disposed", new { handle = $"0x{_windowHandle:X}" });
            _windowHandle = 0;
            _desktopHost = null;
            _monitor = null;
            _attachedDesktopGeneration = 0;
            _appliedTopologyGeneration = 0;
            ClearVisualReferences();
        }
        _windowLease?.Dispose();
        _windowLease = null;
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
}
