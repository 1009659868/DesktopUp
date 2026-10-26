using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed class PosterInputIslandManager : IInputRegionHost
{
    private const string WindowClassName = "DesktopUp.V3.PosterInputIsland";
    private const uint WsPopup = 0x80000000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint LwaAlpha = 0x00000002;
    private const uint WmDestroy = 0x0002;
    private const uint WmPaint = 0x000F;
    private const uint WmEraseBackground = 0x0014;
    private const uint WmSetCursor = 0x0020;
    private const uint WmMouseActivate = 0x0021;
    private const uint WmWindowPosChanging = 0x0046;
    private const uint WmNcDestroy = 0x0082;
    private const uint WmCancelMode = 0x001F;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMouseMove = 0x0200;
    private const uint WmMouseLeave = 0x02A3;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmCaptureChanged = 0x0215;
    private const uint WmTimer = 0x0113;
    private const uint ContentEscapeTimerId = 0xD04;
    private const int VkEscape = 0x1B;
    private const int VkLButton = 0x01;
    private const int VkRButton = 0x02;
    private const int MaNoActivate = 3;
    private const int HtClient = 1;
    private const int RgnAnd = 1;
    private const int RgnOr = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int ErrorClassAlreadyExists = 1410;
    private const int BlackBrush = 4;
    private const int IdcArrow = 32512;
    private const int IdcSizeNs = 32645;
    private const int IdcSizeWe = 32644;
    private const int IdcSizeNwse = 32642;
    private const int IdcSizeNesw = 32643;
    private static readonly nint HwndBottom = new(1);
    private static readonly object ClassGate = new();
    private static readonly Dictionary<nint, PosterInputIslandManager> Instances = [];
    private static readonly WindowProc WindowProcDelegate = SharedWindowProc;
    private static ushort s_windowClass;

    private readonly IDesktopLayerAttachment _attachment;
    private readonly IResourceLedger _ledger;
    private readonly IStructuredLog _log;
    private readonly Func<bool> _reassertClusterBottom;
    private readonly PosterInputRouter _router;
    private readonly PosterInputSessionKey _session;
    private readonly nint _visualHandle;
    private readonly NativeDesktopPointerPlatform _contextNative = new();
    private PosterInputHit? _contextPress;
    private PointerScreenPoint? _contextPressPoint;
    private DesktopShellHostSnapshot? _contextDesktopHost;
    private PosterInputCapture? _capture;
    private bool _routingSuspended;
    private PosterDropReception? _dropReception;
    private PosterInputLayoutSnapshot? _layout;
    private PosterInputRegion? _pressedRegion;
    private DesktopPixelRect _frame;
    private NativePoint _pressCursor;
    private DesktopPixelRect _pressFrame;
    private bool _gestureMoved;
    private bool _trackingPointerLeave;
    private bool _pointerObservationFaultReported;
    private bool _disposed;
    private long _desktopGeneration;
    private IDisposable? _windowLease;

    public PosterInputIslandManager(
        IDesktopLayerAttachment attachment,
        IResourceLedger ledger,
        IStructuredLog log,
        Func<bool> reassertClusterBottom,
        PosterInputRouter router, PosterInputSessionKey session, nint visualHandle)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(reassertClusterBottom);
        _attachment = attachment;
        _ledger = ledger;
        _log = log;
        _reassertClusterBottom = reassertClusterBottom;
        _router = router;
        _session = session;
        _visualHandle = visualHandle;
        _router.Register(session);
    }

    public nint WindowHandle { get; private set; }

    public long AppliedLayoutRevision => _layout?.Revision ?? 0;
    public IPosterDropRegistration InstallDropReceiver(Func<IncomingDropSurface?> surface,
        Action<IncomingDropTicket> accept,Action<IncomingDropPreview?>? changed=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_routingSuspended||WindowHandle==0||_contextDesktopHost is null||_dropReception?.IsReady==true)
            throw new InvalidOperationException("Input host is not ready for drop registration.");
        _dropReception?.Dispose();
        _dropReception=new PosterDropReception(WindowHandle,_frame,
            ()=>!_routingSuspended&&_router.Capture is null&&!DesktopMenuInteraction.IsActive?surface():null,
            accept,_ledger,_log,changed);
        return _dropReception;
    }
    public bool IsNativeInputTransparent => new NativeDesktopMenuPlatform().IsInputTransparent(WindowHandle);

    public event EventHandler<PosterInputEventArgs>? Input;
    // Local native movement only; no global hook, timer, capture or authored selection.
    public event Action<PointerScreenPoint?>? ContentHover;
    public event EventHandler<PosterContentContextEventArgs>? ContextRequested;
    public event EventHandler<TitleNativeInputEventArgs>? TitleInput;
    private bool _titleClaimed;
    private long _titleSequence;
    private bool _contentClaimed;
    private long _contentSequence;
    private nuint _contentEscapeTimer;
    private IDisposable? _contentCaptureLease;
    private NativePoint _contentLatestPoint;
    public bool OwnsTitleCapture(PosterInputCapture capture) => _router.Owns(capture) &&
        _capture == capture && GetCapture() == WindowHandle;

    public static DesktopMenuSurface[] MenuSurfaces(PosterInputRouter router)
    {
        lock (Instances)
            return Instances.Values.Where(i => !i._disposed && ReferenceEquals(i._router, router))
                .Select(i => new DesktopMenuSurface(i._visualHandle, i.WindowHandle,
                    i.CancelPointerGesture, i._reassertClusterBottom)).ToArray();
    }

    // Reuse the existing capture-cancel path for stale spatial/environment work.
    public void CancelPointerGesture()
    {
        _contextPress = null; _contextPressPoint = null;
        EndPointerGesture(cancelled: true);
    }

    public void SuspendRouting()
    {
        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        EndPointerGesture(cancelled: true);
        DisableRoutingNative();
    }

    private void DisableRoutingNative()
    {
        ContentHover?.Invoke(null);
        _dropReception?.Suspend();
        _contextPress = null; _contextPressPoint = null;
        _router.Invalidate(_session);
        _routingSuspended = true;
        if (_disposed || WindowHandle == 0) return;
        nint empty = CreateRectRgn(0, 0, 0, 0);
        if (empty != 0 && SetWindowRgn(WindowHandle, empty, redraw: false) != 0) return;
        if (empty != 0) _ = DeleteObject(empty);
        // A zero-alpha layered window also stops intercepting pointer input.
        bool transparent = SetLayeredWindowAttributes(WindowHandle, 0, 0, LwaAlpha);
        _log.Write(transparent ? "warning" : "error", "poster.input_suspend_region_failed", new { transparent });
    }

    public bool Create(
        DesktopShellHostSnapshot desktopHost,
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DesktopMenuInteraction.CancelActive();
        if (WindowHandle != 0)
        {
            return ApplyLayout(frame, layout) && Reattach(desktopHost);
        }
        if (!ValidateFrameAndLayout(frame, layout))
        {
            return false;
        }

        EnsureWindowClass();
        nint instance = GetModuleHandle(null);
        WindowHandle = CreateWindowEx(
            WsExLayered | WsExToolWindow | WsExNoActivate,
            WindowClassName,
            string.Empty,
            WsPopup,
            frame.X,
            frame.Y,
            frame.Width,
            frame.Height,
            0,
            0,
            instance,
            0);
        if (WindowHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        lock (Instances)
        {
            Instances[WindowHandle] = this;
        }

        if (!SetLayeredWindowAttributes(WindowHandle, 0, 1, LwaAlpha) ||
            !ApplyLayout(frame, layout))
        {
            Dispose();
            return false;
        }

        DesktopLayerAttachResult attached = _attachment.Attach(
            WindowHandle,
            desktopHost,
            showWindow: true,
            inputTransparent: false);
        if (!attached.Succeeded)
        {
            _log.Write("error", "poster.input_island_attach_failed", new
            {
                handle = $"0x{WindowHandle:X}",
                desktopHost.Generation,
                attached.Error,
                attached.NativeError
            });
            Dispose();
            return false;
        }

        _desktopGeneration = desktopHost.Generation;
        _contextDesktopHost = desktopHost;
        _windowLease = _ledger.Acquire(
            "window.poster-input-island",
            "desktop-owned-shaped-native-window");
        _log.Write("info", "poster.input_island_created", new
        {
            handle = $"0x{WindowHandle:X}",
            desktopHost.Generation,
            layout.Revision,
            regionCount = layout.Regions.Count,
            frame
        });
        return true;
    }

    // Paint/input motion may not reposition HWNDs, cancel menus or replace a held gesture.
    // A blocked tick leaves BOTH the old hit map and old painted pose in place.
    public bool CanAnimateTitle => CanPublishTitle(false);
    public bool CanUpdateTitleSize => CanPublishTitle(true);
    private bool CanPublishTitle(bool sizeChange) => !_disposed && WindowHandle != 0 && !_routingSuspended && _layout is not null &&
        !DesktopMenuInteraction.ProtectsSurface(WindowHandle) &&
        _capture is null && _pressedRegion is null && _contextPress is null && _router.Capture is null &&
        _contextNative.TryReadProtection(out var protection) && TitleCloudInputGeometry.AllowsProtection(protection,sizeChange);

    public bool TryApplyTitleMotion(PosterInputLayoutSnapshot layout, bool sizeChange = false)
    {
        if (!CanPublishTitle(sizeChange) || _layout is null ||
            !TitleCloudInputGeometry.IsTitleOnlyReplacement(_layout, layout) || !ValidateFrameAndLayout(_frame, layout) ||
            !_router.IsCurrent(_session, _layout.Revision)) return false;
        nint region = BuildWindowRegion(layout);
        if (region == 0) return false;
        if (SetWindowRgn(WindowHandle, region, redraw: false) == 0) { _ = DeleteObject(region); return false; }
        if (_router.Publish(_session, _frame, layout)) { _layout = layout; return true; }
        // Publication is UI-thread atomic, but retain a native rollback for invariant failure.
        nint previous = BuildWindowRegion(_layout);
        if (previous != 0 && SetWindowRgn(WindowHandle, previous, redraw: false) != 0) return false;
        if (previous != 0) _ = DeleteObject(previous);
        DisableRoutingNative();
        throw new InvalidOperationException("Cannot restore cloud input after rejected publication.");
    }

    // Read-only native evidence, not injection: compare the actual HRGN with published geometry.
    public bool VerifyTitleRegionForProbe(int height)
    {
        if (_layout is null || WindowHandle == 0) return false;
        nint region = CreateRectRgn(0, 0, 0, 0);
        if (region == 0) return false;
        try
        {
            if (GetWindowRgn(WindowHandle, region) == 0) return false;
            for (int y = 0; y < Math.Min(height, _layout.FrameHeight); y++)
                for (int x = 0; x < _layout.FrameWidth; x++)
                {
                    bool expected = x >= 0 && y >= 0 && x < _layout.FrameWidth && y < _layout.FrameHeight;
                    if (PtInRegion(region, x, y) != expected) return false;
                }
            return true;
        }
        finally { _ = DeleteObject(region); }
    }

    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PtInRegion(nint region, int x, int y);

    public bool ApplyLayout(DesktopPixelRect frame, PosterInputLayoutSnapshot layout) =>
        ApplyLayout(frame, layout, boundsAlreadyApplied: false);

    public bool ApplyLayout(
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout,
        bool boundsAlreadyApplied)
    {
        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        if (_disposed || WindowHandle == 0 ||
            !ValidateFrameAndLayout(frame, layout))
        {
            return false;
        }

        // A successful group batch already moved both HWNDs. Verify instead of moving twice.
        bool moved = boundsAlreadyApplied ? GetWindowRect(WindowHandle, out var actual) &&
            actual.Left == frame.X && actual.Top == frame.Y &&
            actual.Right - actual.Left == frame.Width && actual.Bottom - actual.Top == frame.Height : SetWindowPos(
            WindowHandle,
            0,
            frame.X,
            frame.Y,
            frame.Width,
            frame.Height,
            SwpNoZOrder | SwpNoActivate);
        // Verify the LIVE rectangular region: a native reset must not become a cache hit.
        bool sameRegion = moved && GetWindowRgnBox(WindowHandle, out var box) == 2 &&
            box.Left == 0 && box.Top == 0 && box.Right == frame.Width && box.Bottom == frame.Height;
        nint region = moved && !sameRegion ? BuildWindowRegion(layout) : 0;
        if (!moved || !sameRegion && (region == 0 || SetWindowRgn(WindowHandle, region, redraw: true) == 0))
        {
            if (region != 0) _ = DeleteObject(region);
            DisableRoutingNative();
            return false;
        }

        // A successful SetWindowRgn transfers region ownership to Windows.
        _frame = frame;
        _layout = layout;
        if ((!_routingSuspended || SetLayeredWindowAttributes(WindowHandle, 0, 1, LwaAlpha)) &&
            _router.Publish(_session, frame, layout))
        {
            _routingSuspended = false;
            _dropReception?.ApplyFrame(frame);
            return true;
        }
        DisableRoutingNative();
        return false;
    }

    public bool Reattach(DesktopShellHostSnapshot desktopHost)
    {
        if (_disposed || WindowHandle == 0)
        {
            return false;
        }
        if (_desktopGeneration == desktopHost.Generation &&
            _attachment.IsAttached(WindowHandle, desktopHost))
        {
            return true;
        }

        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        DesktopLayerAttachResult result = _attachment.Attach(
            WindowHandle,
            desktopHost,
            showWindow: false,
            inputTransparent: false);
        if (result.Succeeded)
        {
            _desktopGeneration = desktopHost.Generation;
            _contextDesktopHost = desktopHost;
            _dropReception?.Suspend();
            if(!_routingSuspended)_dropReception?.ApplyFrame(_frame);
        }
        return result.Succeeded;
    }

    public bool ReassertDesktopBottom(DesktopShellHostSnapshot desktopHost)
    {
        return !_disposed &&
        WindowHandle != 0 &&
        _desktopGeneration == desktopHost.Generation &&
        _attachment.ReassertDesktopBottom(WindowHandle, desktopHost);
    }

    public bool IsAttached(DesktopShellHostSnapshot desktopHost) =>
        !_disposed &&
        WindowHandle != 0 &&
        _desktopGeneration == desktopHost.Generation &&
        _attachment.IsAttached(WindowHandle, desktopHost);

    private nint HandleMessage(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam)
    {
        MenuInputDiagnostics.Observe(windowHandle, message, lParam, "input-island");
        if (message == WmMouseLeave)
        {
            _trackingPointerLeave = false;
            _contextPress = null; _contextPressPoint = null;
        }
        if (message == WmMouseMove && !_trackingPointerLeave && (_router.HasPointerObservers || ContentHover is not null))
        {
            var tracking = new PointerLeaveTracking { Size = (uint)Marshal.SizeOf<PointerLeaveTracking>(), Flags = 2, Window = windowHandle };
            _trackingPointerLeave = TrackMouseEvent(ref tracking);
        }
        if (message is WmMouseMove or WmMouseLeave or WmLButtonUp or WmMouseWheel or WmCaptureChanged)
        {
            if (_router.NotifyPointerActivity(_session, AppliedLayoutRevision) is { } error && !_pointerObservationFaultReported)
            {
                _pointerObservationFaultReported = true;
                _log.Write("warning", "poster.pointer_observation_failed", new { error.Message });
            }
        }
        if (message == WmMouseMove) RefreshContentHover();
        else if (message is WmMouseLeave or WmLButtonDown or WmRButtonDown or WmCaptureChanged or WmCancelMode)
            ContentHover?.Invoke(null);
        switch (message)
        {
            case WmMouseActivate:
                if (!DesktopMenuInteraction.ProtectsSurface(windowHandle)) _ = _reassertClusterBottom();
                return new nint(MaNoActivate);
            case WmWindowPosChanging:
                if (!DesktopMenuInteraction.PreserveSurfaceZOrder(windowHandle, lParam)) ConstrainZOrder(lParam);
                break;
            case WmLButtonDown:
                _contextPress = null; _contextPressPoint = null;
                // A chord with the right button must not convert a context press into a drag.
                if ((GetAsyncKeyState(VkRButton) & 0x8000) != 0) return 0;
                BeginPointerGesture(lParam);
                // Title arbitration precedes automatic observation of this capture.
                _router.NotifyPointerActivity(_session, AppliedLayoutRevision);
                return 0;
            case WmRButtonDown:
                BeginContentContext(lParam);
                return 0;
            case WmRButtonUp:
                CompleteContentContext(lParam);
                return 0;
            case WmMouseMove:
                ContinuePointerGesture();
                return 0;
            case WmMouseWheel:
                RouteMouseWheel(wParam, lParam);
                return 0;
            case WmLButtonUp:
                if (CancelContentForEscapeOrLostCapture()) return 0;
                EndPointerGesture(cancelled: false);
                return 0;
            case WmTimer:
                if (_contentEscapeTimer != 0 && wParam == _contentEscapeTimer)
                {
                    _ = CancelContentForEscapeOrLostCapture();
                    return 0;
                }
                break;
            case WmCancelMode:
                _contextPress = null; _contextPressPoint = null;
                EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.CancelMode);
                return 0;
            case WmCaptureChanged:
                _contextPress = null; _contextPressPoint = null;
                if (_pressedRegion is not null && GetCapture() != windowHandle)
                {
                    EndPointerGesture(cancelled: true, releaseCapture: false, cause: TitleNativeEndReason.CaptureTransferred);
                }
                return 0;
            case WmSetCursor:
                if (SetPointerCursor())
                {
                    return new nint(1);
                }
                break;
            case WmEraseBackground:
                return new nint(1);
            case WmPaint:
                PaintTransparentInputSurface(windowHandle);
                return 0;
            case WmDestroy:
                _contextPress = null; _contextPressPoint = null;
                if (_contentClaimed) EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.RouteInvalidated);
                StopContentCaptureTimer();
                _contentClaimed = false;
                _pressedRegion = null;
                break;
            case WmNcDestroy:
                lock (Instances)
                {
                    Instances.Remove(windowHandle);
                }
                break;
        }

        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    public void RefreshContentHover()
    {
        if (ContentHover is null) return;
        PointerScreenPoint? point = null;
        if (!_disposed && !_routingSuspended && _router.Capture is null && GetCapture() == 0 &&
            (GetAsyncKeyState(VkLButton) & 0x8000) == 0 &&
            _contextNative.TryReadCursor(out var current) && _contextNative.ReceiverRoot(current) == WindowHandle &&
            _router.Resolve(current.X, current.Y, [_session])?.Region.Kind is
                PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle)
            point = current;
        try { ContentHover.Invoke(point); }
        catch (Exception error) { _log.Write("warning", "poster.hover_paint_failed", new { error.Message }); }
    }

    private void BeginContentContext(nint messagePoint)
    {
        _contextPress = null; _contextPressPoint = null;
        if (!TryReadContentContextHit(messagePoint, out var hit, out var point)) return;
        _contextPress = hit;
        _contextPressPoint = point;
        if (!_trackingPointerLeave)
        {
            var tracking = new PointerLeaveTracking
                { Size = (uint)Marshal.SizeOf<PointerLeaveTracking>(), Flags = 2, Window = WindowHandle };
            _trackingPointerLeave = TrackMouseEvent(ref tracking);
            if (!_trackingPointerLeave) { _contextPress = null; _contextPressPoint = null; }
        }
    }

    private void CompleteContentContext(nint messagePoint)
    {
        var press = _contextPress;
        var pressPoint = _contextPressPoint;
        _contextPress = null; _contextPressPoint = null; // Consume once before any callback, including a re-entrant menu open.
        if (press is null || !TryReadContentContextHit(messagePoint, out var release, out var point) || release != press ||
            _router.ResolveContext(press, point, new[] { _session }, pressPoint) is not { } request) return;
        ContextRequested?.Invoke(this, new(request));
    }

    private bool TryReadContentContextHit(nint messagePoint, out PosterInputHit? hit, out PointerScreenPoint point)
    {
        hit = null;
        point = default;
        // Context intent has no capture lease, timer, focus transfer or global listener. Check
        // native capture as well as the router, including another foreground thread's capture.
        if (_disposed || _routingSuspended || WindowHandle == 0 || _pressedRegion is not null ||
            _capture is not null || _router.Capture is not null || GetCapture() != 0 ||
            (GetAsyncKeyState(VkLButton) & 0x8000) != 0 ||
            !_contextNative.TryReadProtection(out var protection) ||
            (protection & (AutoPostureProtection.PointerCapture | AutoPostureProtection.Menu)) != 0 ||
            !ContentContextMessagePoint.TryRead(WindowHandle, messagePoint, _frame, out point) ||
            !_contextNative.TryReadCursor(out var currentPoint) || _contextNative.ReceiverRoot(currentPoint) != WindowHandle ||
            _contextNative.ReceiverRoot(point) != WindowHandle ||
            _contextDesktopHost is not { } host || !_attachment.IsAttached(WindowHandle, host) ||
            !_contextNative.IsDesktopReceiver(host.Identity.ContainerHandle, host) ||
            !_contextNative.IsSurfaceCurrent(_visualHandle, WindowHandle, _frame) ||
            ResolveRoute(new NativePoint { X = point.X, Y = point.Y }) is not { } route ||
            !ReferenceEquals(route.Owner, this) || route.Hit.Region.Kind is not (PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentViewport) ||
            !PosterContentInputPolicy.HasValidIdentity(route.Hit.Region) ||
            _contextNative.ReadWindowOrder() is not { } order) return false;
        foreach (nint window in order)
        {
            if (window == WindowHandle)
            {
                hit = route.Hit;
                return true;
            }
            if (window == _visualHandle || window == host.Identity.ContainerHandle) continue;
            // WindowFromPhysicalPoint skips disabled windows. The existing observer's
            // region-aware check also rejects visible occluders above this input HWND.
            if (_contextNative.CoversPoint(window, point)) return false;
        }
        return false;
    }

    private void BeginPointerGesture(nint lParam)
    {
        if (!GetCursorPos(out NativePoint point) || ResolveRoute(point) is not { } route) return;
        route.Owner.BeginResolvedGesture(route.Hit, point);
    }

    private void BeginResolvedGesture(PosterInputHit hit, NativePoint point)
    {
        if (PosterContentInputPolicy.IsTarget(hit.Region.Kind) && !PosterContentInputPolicy.HasValidIdentity(hit.Region)) return;
        if (_disposed || _pressedRegion is not null || (_capture = _router.Begin(hit)) is null) return;
        if (MenuInputDiagnostics.Collecting) MenuInputDiagnostics.Route(WindowHandle, new
        {
            phase = "capture-begin", hit.Session, hit.LayoutRevision, hit.Frame,
            target = hit.Region.Kind.ToString(), hit.Region.Index, hit.Region.Bounds,
            screenX = point.X, screenY = point.Y
        });
        _pressedRegion = hit.Region;
        _pressFrame = hit.Frame;
        _gestureMoved = false;
        _pressCursor = point;
        _ = SetCapture(WindowHandle);
        if (GetCapture() != WindowHandle) EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.CaptureUnavailable);
        else if (PosterContentInputPolicy.IsTarget(hit.Region.Kind) && _capture is { } contentCapture)
        {
            // Claim before dispatch so synchronous invalidation during Press emits a content cancellation.
            _contentClaimed = true;
            var input = CreateContentEvent(PosterInputEventKind.Pressed, contentCapture, point);
            Input?.Invoke(this, input);
            if (_capture != contentCapture || !_contentClaimed) return;
            if (!input.Handled || !StartContentCaptureTimer())
                EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.Programmatic);
        }
        else if (hit.Region.Kind == PosterInputTargetKind.TitleBar && _capture is { } capture)
        {
            var input = new TitleNativeInputEventArgs(TitleNativeInputKind.Press, capture,
                new(point.X, point.Y), ++_titleSequence);
            TitleInput?.Invoke(this, input);
            _titleClaimed = input.Handled && _capture == capture;
        }
    }

    private void ContinuePointerGesture()
    {
        if (_pressedRegion is not null && !_router.Owns(_capture))
        { EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.RouteInvalidated); return; }
        if (CancelContentForEscapeOrLostCapture()) return;
        if (_pressedRegion is not { } target || !IsGestureTarget(target.Kind) ||
            !GetCursorPos(out NativePoint cursor))
        {
            if (_titleClaimed || _contentClaimed && _pressedRegion is { } content && PosterContentInputPolicy.CanDrag(content.Kind))
                EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.CursorUnavailable);
            return;
        }

        if (_titleClaimed && _capture is { } capture)
        {
            TitleInput?.Invoke(this, new(TitleNativeInputKind.Move, capture,
                new(cursor.X, cursor.Y), ++_titleSequence));
            return;
        }
        int deltaX = cursor.X - _pressCursor.X;
        int deltaY = cursor.Y - _pressCursor.Y;
        if (!_gestureMoved && !PosterContentInputPolicy.CrossedDragThreshold(deltaX, deltaY))
        {
            return;
        }

        _gestureMoved = true;
        if (_contentClaimed && _capture is { } contentCapture)
        {
            Input?.Invoke(this, CreateContentEvent(PosterInputEventKind.GesturePreview, contentCapture, cursor));
            return;
        }
        RaiseInput(
            PosterInputEventKind.GesturePreview,
            target,
            deltaX,
            deltaY);
    }

    private PosterInputEventArgs CreateContentEvent(PosterInputEventKind kind, PosterInputCapture capture,
        NativePoint point, TitleNativeEndReason endReason = TitleNativeEndReason.None)
    {
        if (endReason == TitleNativeEndReason.CursorUnavailable) point = _contentLatestPoint;
        else _contentLatestPoint = point;
        return PosterContentInputPolicy.Event(capture, kind, new(_pressCursor.X, _pressCursor.Y),
            new(point.X, point.Y), AppliedLayoutRevision, ++_contentSequence, endReason);
    }

    private bool StartContentCaptureTimer()
    {
        if (_contentEscapeTimer != 0) return true;
        try
        {
            _contentEscapeTimer = SetTimer(WindowHandle, ContentEscapeTimerId, 33, 0);
            if (_contentEscapeTimer == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot observe Escape during content capture.");
            _contentCaptureLease = _ledger.Acquire("timer.content-capture", "capture-only-native-escape-observer");
            return true;
        }
        catch (Exception exception)
        {
            StopContentCaptureTimer();
            _log.Write("warning", "poster.content_capture_timer_failed", new { exception.Message });
            return false;
        }
    }

    private void StopContentCaptureTimer()
    {
        nuint timer = _contentEscapeTimer;
        _contentEscapeTimer = 0;
        if (timer != 0 && WindowHandle != 0) _ = KillTimer(WindowHandle, timer);
        _contentCaptureLease?.Dispose();
        _contentCaptureLease = null;
    }

    private bool CancelContentForEscapeOrLostCapture()
    {
        if (!_contentClaimed || _capture is null) return false;
        if (!_router.Owns(_capture))
        {
            EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.RouteInvalidated);
            return true;
        }
        if (GetCapture() != WindowHandle)
        {
            EndPointerGesture(cancelled: true, releaseCapture: false, cause: TitleNativeEndReason.CaptureTransferred);
            return true;
        }
        // Never queried outside our own live content capture. No global hook or key suppression.
        if ((GetAsyncKeyState(VkEscape) & 0x8000) == 0) return false;
        EndPointerGesture(cancelled: true, cause: TitleNativeEndReason.CancelMode);
        return true;
    }


    private void RouteMouseWheel(nuint wParam, nint lParam)
    {
        var point = new NativePoint
        {
            X = GetSignedLowWord(lParam),
            Y = GetSignedHighWord(lParam)
        };
        if (ResolveRoute(point) is not { } route ||
            route.Hit.Region.Kind is not (PosterInputTargetKind.Item or
                PosterInputTargetKind.ItemScroll or PosterInputTargetKind.ContentItem or
                PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport))
        {
            return;
        }

        if (PosterContentInputPolicy.IsTarget(route.Hit.Region.Kind))
        {
            if (!PosterContentInputPolicy.HasValidIdentity(route.Hit.Region)) return;
            route.Owner.Input?.Invoke(route.Owner, new PosterInputEventArgs(
                PosterInputEventKind.Scrolled, route.Hit.Region.Kind, route.Hit.Region.Index,
                route.Hit.Frame, 0, 0, route.Hit.LayoutRevision, unchecked((short)((long)wParam >> 16)))
            {
                Session = route.Hit.Session, ContentItemId = route.Hit.Region.ContentItemId,
                ContentSnapshotGeneration = route.Hit.Region.ContentSnapshotGeneration,
                LatestPoint = new(point.X, point.Y), Sequence = ++route.Owner._contentSequence
            });
            return;
        }

        route.Owner.RaiseInput(
            PosterInputEventKind.Scrolled,
            route.Hit.Region,
            0,
            0,
            unchecked((short)((long)wParam >> 16)));
    }

    public bool HandOffContentCapture(PosterInputCapture capture)
    {
        if (_disposed || !_contentClaimed || _capture != capture || !_router.Owns(capture) || GetCapture() != WindowHandle) return false;
        // OLE becomes the sole capture owner. This is neither an internal drop nor a cancel.
        _contentClaimed = false; _titleClaimed = false; StopContentCaptureTimer();
        _pressedRegion = null; _router.End(_capture); _capture = null; _gestureMoved = false;
        _ = ReleaseCapture(); return true;
    }
    private void EndPointerGesture(bool cancelled, bool releaseCapture = true,
        TitleNativeEndReason cause = TitleNativeEndReason.Programmatic)
    {
        if (_pressedRegion is not { } target)
        {
            return;
        }

        bool owns = _router.Owns(_capture);
        bool cursorAvailable = GetCursorPos(out NativePoint cursor);
        bool claimed = _titleClaimed;
        bool contentTarget = PosterContentInputPolicy.IsTarget(target.Kind);
        var titleCapture = _capture;
        _titleClaimed = false;
        _contentClaimed = false;
        StopContentCaptureTimer();
        cancelled |= !owns || !cursorAvailable || (!claimed && !contentTarget && !_gestureMoved &&
            (_capture is null || !_router.CanInvoke(_capture, cursor.X, cursor.Y)));
        // Preserve the observed cause before ending router/capture ownership. A stale route or
        // missing cursor is a hard failure even when WM_CAPTURECHANGED originally woke us.
        var endReason = cancelled ? TitleNativeEndPolicy.Resolve(cause, owns, cursorAvailable) : TitleNativeEndReason.None;
        _pressedRegion = null;
        _router.End(_capture);
        _capture = null;
        if (releaseCapture && GetCapture() == WindowHandle)
        {
            _ = ReleaseCapture();
        }

        if (claimed && titleCapture is not null)
        {
            TitleInput?.Invoke(this, new(cancelled ? TitleNativeInputKind.Cancel : TitleNativeInputKind.Release,
                titleCapture, new(cursor.X, cursor.Y), ++_titleSequence, CanInvoke: !cancelled, EndReason: endReason));
            _gestureMoved = false;
            return;
        }
        if (contentTarget && titleCapture is not null)
        {
            // Release is addressed to the captured stable item, not an index from the latest reflow.
            // The adapter owns selection/threshold/commit semantics; no old Item or outer-frame fallback.
            bool contentMoved = _gestureMoved || PosterContentInputPolicy.CanDrag(target.Kind) &&
                PosterContentInputPolicy.CrossedDragThreshold(cursor.X - _pressCursor.X, cursor.Y - _pressCursor.Y);
            PosterInputEventKind contentKind = cancelled ? PosterInputEventKind.GestureCancelled :
                contentMoved ? PosterInputEventKind.GestureCompleted : PosterInputEventKind.Invoked;
            Input?.Invoke(this, CreateContentEvent(contentKind, titleCapture, cursor, endReason));
            _gestureMoved = false;
            return;
        }
        int deltaX = cursor.X - _pressCursor.X;
        int deltaY = cursor.Y - _pressCursor.Y;
        PosterInputEventKind eventKind = cancelled
            ? PosterInputEventKind.GestureCancelled
            : _gestureMoved
                ? PosterInputEventKind.GestureCompleted
                : PosterInputEventKind.Invoked;
        if (MenuInputDiagnostics.Collecting) MenuInputDiagnostics.Route(WindowHandle, new
        {
            phase = "dispatch", session = _session, kind = eventKind.ToString(), target = target.Kind.ToString(),
            target.Index, target.Bounds, cancelled, owns, screenX = cursor.X, screenY = cursor.Y
        });
        RaiseInput(eventKind, target, deltaX, deltaY);
        _gestureMoved = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointerLeaveTracking
    {
        public uint Size, Flags;
        public nint Window;
        public uint HoverTime;
    }
    [DllImport("user32.dll")]
    private static extern bool TrackMouseEvent(ref PointerLeaveTracking tracking);

    private void RaiseInput(
        PosterInputEventKind eventKind,
        PosterInputRegion target,
        int deltaX,
        int deltaY,
        int wheelDelta = 0) => Input?.Invoke(
            this,
            new PosterInputEventArgs(
                eventKind,
                target.Kind,
                target.Index,
                _pressFrame,
                deltaX,
                deltaY,
                _layout?.Revision ?? 0,
                wheelDelta) { Session = _session });

    private (PosterInputIslandManager Owner, PosterInputHit Hit)? ResolveRoute(NativePoint point)
    {
        // Defense against a queued pre-menu message. Native transparency supplies actual
        // Popup delivery; merely dropping this route would not fix hover.
        if (DesktopMenuInteraction.BlocksInput(WindowHandle)) return null;
        PosterInputIslandManager[] candidates;
        lock (Instances) candidates = Instances.Values.Where(i => !i._disposed && ReferenceEquals(i._router, _router)).ToArray();
        var visuals = candidates.ToDictionary(i => i._visualHandle);
        var order = new List<PosterInputSessionKey>();
        var seen = new HashSet<nint>();
        // Inspect order only. Never reorder clusters to resolve an input ambiguity.
        for (nint hwnd = GetTopWindow(0); hwnd != 0 && seen.Count < 4096 && seen.Add(hwnd); hwnd = GetWindow(hwnd, 2))
            if (visuals.TryGetValue(hwnd, out var owner) && IsWindowVisible(hwnd) && IsWindowVisible(owner.WindowHandle))
                order.Add(owner._session);
        var hit = _router.Resolve(point.X, point.Y, order);
        var receiver = hit is null ? null : candidates.FirstOrDefault(i => i._session == hit.Session);
        return receiver is null || hit is null ? null : (receiver, hit);
    }

    private PosterInputRegion? HitTest(int x, int y)
    {
        if (_layout is null)
        {
            return null;
        }
        foreach (PosterInputRegion region in _layout.Regions)
        {
            if (!PosterInputGeometry.CapturesPointer(region)) continue;
            DesktopPixelRect bounds = region.Bounds;
            if (x >= bounds.X && x < bounds.Right &&
                y >= bounds.Y && y < bounds.Bottom)
            {
                return region;
            }
        }
        return null;
    }

    private bool SetPointerCursor()
    {
        if (!GetCursorPos(out NativePoint point)) return false;
        PosterInputRegion? resolved = _router.Owns(_capture) ? _pressedRegion : ResolveRoute(point)?.Hit.Region;
        if (resolved is not { } target)
        {
            return false;
        }

        int cursorId = target.Kind switch
        {
            PosterInputTargetKind.ResizeLeft or
            PosterInputTargetKind.ResizeRight => IdcSizeWe,
            PosterInputTargetKind.ResizeTop or
            PosterInputTargetKind.ResizeBottom => IdcSizeNs,
            PosterInputTargetKind.ResizeTopLeft or
            PosterInputTargetKind.ResizeBottomRight or
            PosterInputTargetKind.ContentSpanHandle => IdcSizeNwse,
            PosterInputTargetKind.ResizeTopRight or
            PosterInputTargetKind.ResizeBottomLeft => IdcSizeNesw,
            _ => IdcArrow
        };
        _ = SetCursor(LoadCursor(0, new nint(cursorId)));
        return true;
    }

    private static bool IsGestureTarget(PosterInputTargetKind kind) => kind is
        PosterInputTargetKind.ContentViewport or
        PosterInputTargetKind.ContentItem or
        PosterInputTargetKind.ContentSpanHandle or
        PosterInputTargetKind.TitleBar or
        PosterInputTargetKind.EditSurface or
        PosterInputTargetKind.ResizeLeft or
        PosterInputTargetKind.ResizeRight or
        PosterInputTargetKind.ResizeTop or
        PosterInputTargetKind.ResizeBottom or
        PosterInputTargetKind.ResizeTopLeft or
        PosterInputTargetKind.ResizeTopRight or
        PosterInputTargetKind.ResizeBottomLeft or
        PosterInputTargetKind.ResizeBottomRight;

    private static bool ValidateFrameAndLayout(
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout) =>
        frame.IsUsable &&
        layout.Revision > 0 &&
        frame.Width == layout.FrameWidth &&
        frame.Height == layout.FrameHeight &&
        layout.Regions.All(region =>
            region.Bounds.IsUsable &&
            region.Bounds.X >= 0 &&
            region.Bounds.Y >= 0 &&
            region.Bounds.Right <= layout.FrameWidth &&
            region.Bounds.Bottom <= layout.FrameHeight);

    // Material transparency does not imply input transparency. Blank areas belong
    // to the poster too; logical regions still decide whether a command may run.
    internal static nint BuildWindowRegion(PosterInputLayoutSnapshot layout) =>
        CreateRectRgn(0, 0, layout.FrameWidth, layout.FrameHeight);

    private static void ConstrainZOrder(nint lParam)
    {
        if (lParam == 0)
        {
            return;
        }
        WindowPos position = Marshal.PtrToStructure<WindowPos>(lParam);
        if ((position.Flags & SwpNoZOrder) != 0 || position.HwndInsertAfter == HwndBottom)
        {
            return;
        }
        position.HwndInsertAfter = HwndBottom;
        position.Flags |= SwpNoActivate;
        Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
    }

    private static void PaintTransparentInputSurface(nint windowHandle)
    {
        nint device = BeginPaint(windowHandle, out PaintStruct paint);
        if (device != 0)
        {
            _ = FillRect(device, ref paint.Paint, GetStockObject(BlackBrush));
        }
        _ = EndPaint(windowHandle, ref paint);
    }

    private static void EnsureWindowClass()
    {
        lock (ClassGate)
        {
            if (s_windowClass != 0)
            {
                return;
            }

            var windowClass = new WindowClassEx
            {
                Size = (uint)Marshal.SizeOf<WindowClassEx>(),
                Instance = GetModuleHandle(null),
                Cursor = LoadCursor(0, new nint(IdcArrow)),
                Background = GetStockObject(BlackBrush),
                WindowProc = Marshal.GetFunctionPointerForDelegate(WindowProcDelegate),
                ClassName = WindowClassName
            };
            s_windowClass = RegisterClassEx(ref windowClass);
            if (s_windowClass == 0 && Marshal.GetLastPInvokeError() != ErrorClassAlreadyExists)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
    }

    private static nint SharedWindowProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam)
    {
        PosterInputIslandManager? instance;
        lock (Instances)
        {
            Instances.TryGetValue(windowHandle, out instance);
        }
        return instance is null
            ? DefWindowProc(windowHandle, message, wParam, lParam)
            : instance.HandleMessage(windowHandle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        StopContentCaptureTimer();
        _dropReception?.Dispose();_dropReception=null;
        _contentClaimed = false;
        _disposed = true;
        _contextPress = null; _contextPressPoint = null;
        _contextDesktopHost = null;
        _router.Remove(_session);
        _capture = null;
        Input = null;
        ContextRequested = null;
        _pressedRegion = null;

        nint windowHandle = WindowHandle;
        WindowHandle = 0;
        _desktopGeneration = 0;
        _layout = null;
        if (windowHandle != 0)
        {
            if (GetCapture() == windowHandle)
            {
                _ = ReleaseCapture();
            }
            _ = _attachment.Detach(windowHandle);
            _ = DestroyWindow(windowHandle);
            lock (Instances)
            {
                Instances.Remove(windowHandle);
            }
            _log.Write("info", "poster.input_island_disposed", new
            {
                handle = $"0x{windowHandle:X}"
            });
        }
        _windowLease?.Dispose();
        _windowLease = null;
    }

    private static int GetSignedLowWord(nint value) => unchecked((short)(long)value);
    private static int GetSignedHighWord(nint value) => unchecked((short)((long)value >> 16));

    private delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public nint Device;
        public int Erase;
        public NativeRect Paint;
        public int Restore;
        public int IncUpdate;
        public int Reserved0;
        public int Reserved1;
        public int Reserved2;
        public int Reserved3;
        public int Reserved4;
        public int Reserved5;
        public int Reserved6;
        public int Reserved7;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public nint Hwnd;
        public nint HwndInsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public nint WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [DllImport("user32.dll")]
    private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern int GetWindowRgnBox(nint window, out NativeRect rectangle);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(
        nint windowHandle,
        uint colorKey,
        byte alpha,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(nint windowHandle, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int CombineRgn(nint destination, nint first, nint second, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);

    [DllImport("gdi32.dll")]
    private static extern nint GetStockObject(int index);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint windowHandle, out PaintStruct paint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPaint(nint windowHandle, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint device, ref NativeRect rectangle, nint brush);

    [DllImport("user32.dll")]
    private static extern nint SetCapture(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint GetCapture();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nuint SetTimer(nint windowHandle, nuint timerId, uint intervalMilliseconds, nint timerProc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KillTimer(nint windowHandle, nuint timerId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint windowHandle, ref NativePoint point);

    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern nint LoadCursor(nint instance, nint cursorName);

    [DllImport("user32.dll")]
    private static extern nint SetCursor(nint cursor);
}

// Query only while handling a wheel event already routed to the current poster. No hook or key capture.
public static class NativeInputKeys
{
    public static bool ControlDown => (GetAsyncKeyState(0x11) & 0x8000) != 0;
    public static bool ShiftDown => (GetAsyncKeyState(0x10) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
