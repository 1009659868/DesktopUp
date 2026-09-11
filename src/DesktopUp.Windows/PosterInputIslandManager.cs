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
    private const uint WmMouseMove = 0x0200;
    private const uint WmMouseLeave = 0x02A3;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmCaptureChanged = 0x0215;
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
    private PosterInputCapture? _capture;
    private bool _routingSuspended;
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
    public bool IsNativeInputTransparent => new NativeDesktopMenuPlatform().IsInputTransparent(WindowHandle);

    public event EventHandler<PosterInputEventArgs>? Input;

    public static DesktopMenuSurface[] MenuSurfaces(PosterInputRouter router)
    {
        lock (Instances)
            return Instances.Values.Where(i => !i._disposed && ReferenceEquals(i._router, router))
                .Select(i => new DesktopMenuSurface(i._visualHandle, i.WindowHandle,
                    i.CancelPointerGesture, i._reassertClusterBottom)).ToArray();
    }

    // Reuse the existing capture-cancel path for stale spatial/environment work.
    public void CancelPointerGesture() => EndPointerGesture(cancelled: true);

    public void SuspendRouting()
    {
        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        EndPointerGesture(cancelled: true);
        DisableRoutingNative();
    }

    private void DisableRoutingNative()
    {
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

    public bool ApplyLayout(
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout)
    {
        DesktopMenuInteraction.CancelForSurface(WindowHandle);
        if (_disposed || WindowHandle == 0 ||
            !ValidateFrameAndLayout(frame, layout))
        {
            return false;
        }

        nint region = BuildWindowRegion(layout);
        if (region == 0)
        {
            return false;
        }

        bool moved = SetWindowPos(
            WindowHandle,
            0,
            frame.X,
            frame.Y,
            frame.Width,
            frame.Height,
            SwpNoZOrder | SwpNoActivate);
        if (!moved || SetWindowRgn(WindowHandle, region, redraw: true) == 0)
        {
            _ = DeleteObject(region);
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
        }
        return result.Succeeded;
    }

    public bool ReassertDesktopBottom(DesktopShellHostSnapshot desktopHost) =>
        !_disposed &&
        WindowHandle != 0 &&
        _desktopGeneration == desktopHost.Generation &&
        _attachment.ReassertDesktopBottom(WindowHandle, desktopHost);

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
        if (message == WmMouseLeave) _trackingPointerLeave = false;
        if (message == WmMouseMove && !_trackingPointerLeave && _router.HasPointerObservers)
        {
            var tracking = new PointerLeaveTracking { Size = (uint)Marshal.SizeOf<PointerLeaveTracking>(), Flags = 2, Window = windowHandle };
            _trackingPointerLeave = TrackMouseEvent(ref tracking);
        }
        if (message is WmMouseMove or WmMouseLeave or WmLButtonDown or WmLButtonUp or WmMouseWheel or WmCaptureChanged)
        {
            if (_router.NotifyPointerActivity(_session, AppliedLayoutRevision) is { } error && !_pointerObservationFaultReported)
            {
                _pointerObservationFaultReported = true;
                _log.Write("warning", "poster.pointer_observation_failed", new { error.Message });
            }
        }
        switch (message)
        {
            case WmMouseActivate:
                if (!DesktopMenuInteraction.ProtectsSurface(windowHandle)) _ = _reassertClusterBottom();
                return new nint(MaNoActivate);
            case WmWindowPosChanging:
                if (!DesktopMenuInteraction.PreserveSurfaceZOrder(windowHandle, lParam)) ConstrainZOrder(lParam);
                break;
            case WmLButtonDown:
                BeginPointerGesture(lParam);
                return 0;
            case WmMouseMove:
                ContinuePointerGesture();
                return 0;
            case WmMouseWheel:
                RouteMouseWheel(wParam, lParam);
                return 0;
            case WmLButtonUp:
                EndPointerGesture(cancelled: false);
                return 0;
            case WmCancelMode:
                EndPointerGesture(cancelled: true);
                return 0;
            case WmCaptureChanged:
                if (_pressedRegion is not null && GetCapture() != windowHandle)
                {
                    EndPointerGesture(cancelled: true, releaseCapture: false);
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

    private void BeginPointerGesture(nint lParam)
    {
        if (!GetCursorPos(out NativePoint point) || ResolveRoute(point) is not { } route) return;
        route.Owner.BeginResolvedGesture(route.Hit, point);
    }

    private void BeginResolvedGesture(PosterInputHit hit, NativePoint point)
    {
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
        if (GetCapture() != WindowHandle) EndPointerGesture(cancelled: true);
    }

    private void ContinuePointerGesture()
    {
        if (_pressedRegion is not null && !_router.Owns(_capture)) { EndPointerGesture(cancelled: true); return; }
        if (_pressedRegion is not { } target || !IsGestureTarget(target.Kind) ||
            !GetCursorPos(out NativePoint cursor))
        {
            return;
        }

        int deltaX = cursor.X - _pressCursor.X;
        int deltaY = cursor.Y - _pressCursor.Y;
        if (!_gestureMoved && ((long)deltaX * deltaX) + ((long)deltaY * deltaY) < 25)
        {
            return;
        }

        _gestureMoved = true;
        RaiseInput(
            PosterInputEventKind.GesturePreview,
            target,
            deltaX,
            deltaY);
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
                PosterInputTargetKind.ItemScroll))
        {
            return;
        }

        route.Owner.RaiseInput(
            PosterInputEventKind.Scrolled,
            route.Hit.Region,
            0,
            0,
            unchecked((short)((long)wParam >> 16)));
    }

    private void EndPointerGesture(bool cancelled, bool releaseCapture = true)
    {
        if (_pressedRegion is not { } target)
        {
            return;
        }

        bool owns = _router.Owns(_capture);
        bool cursorAvailable = GetCursorPos(out NativePoint cursor);
        cancelled |= !owns || !cursorAvailable || (!_gestureMoved &&
            (_capture is null || !_router.CanInvoke(_capture, cursor.X, cursor.Y)));
        _pressedRegion = null;
        _router.End(_capture);
        _capture = null;
        if (releaseCapture && GetCapture() == WindowHandle)
        {
            _ = ReleaseCapture();
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
            PosterInputTargetKind.ResizeBottomRight => IdcSizeNwse,
            PosterInputTargetKind.ResizeTopRight or
            PosterInputTargetKind.ResizeBottomLeft => IdcSizeNesw,
            _ => IdcArrow
        };
        _ = SetCursor(LoadCursor(0, new nint(cursorId)));
        return true;
    }

    private static bool IsGestureTarget(PosterInputTargetKind kind) => kind is
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

    private static nint BuildWindowRegion(PosterInputLayoutSnapshot layout)
    {
        nint combined = CreateRectRgn(0, 0, 0, 0);
        if (combined == 0)
        {
            return 0;
        }

        foreach (PosterInputRegion input in layout.Regions)
        {
            DesktopPixelRect bounds = input.Bounds;
            nint part = CreateRectRgn(bounds.X, bounds.Y, bounds.Right, bounds.Bottom);
            if (part == 0 || CombineRgn(combined, combined, part, RgnOr) == 0)
            {
                if (part != 0)
                {
                    _ = DeleteObject(part);
                }
                _ = DeleteObject(combined);
                return 0;
            }
            _ = DeleteObject(part);
        }

        if (layout.OuterCornerRadius > 0)
        {
            int diameter = checked(layout.OuterCornerRadius * 2);
            nint outer = CreateRoundRectRgn(
                0,
                0,
                checked(layout.FrameWidth + 1),
                checked(layout.FrameHeight + 1),
                diameter,
                diameter);
            if (outer == 0 || CombineRgn(combined, combined, outer, RgnAnd) == 0)
            {
                if (outer != 0)
                {
                    _ = DeleteObject(outer);
                }
                _ = DeleteObject(combined);
                return 0;
            }
            _ = DeleteObject(outer);
        }
        return combined;
    }

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
        _disposed = true;
        _router.Remove(_session);
        _capture = null;
        Input = null;
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
