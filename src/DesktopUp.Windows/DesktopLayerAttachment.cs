using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

internal interface IDesktopWindowApi
{
    bool IsWindow(nint windowHandle);
    nint GetOwner(nint windowHandle);
    nint SetOwner(nint windowHandle, nint ownerHandle);
    nint GetStyle(nint windowHandle);
    nint GetExtendedStyle(nint windowHandle);
    nint SetStyle(nint windowHandle, nint style);
    nint SetExtendedStyle(nint windowHandle, nint style);
    bool ApplyFrameAndNonTopmost(nint windowHandle);
    bool PlaceAtDesktopSiblingBottom(nint windowHandle, bool showWindow);
    bool ApplyInputTransparentComposition(nint windowHandle);
    bool ApplyRoundedRegion(
        nint windowHandle,
        int width,
        int height,
        int cornerRadius);
    bool ApplyPreparedBounds(nint windowHandle, DesktopPixelRect bounds);
    bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds) => false;
    bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds, IReadOnlyDictionary<nint, uint> flags) => false;
    bool RequestFrameRedraw(nint windowHandle) => false;
    bool VerifyVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible) => false;
    DesktopVisualSurfaceReadback InspectVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible) =>
        new(false, "surface-inspection-unsupported");
    void RemoveDwmBorder(nint windowHandle);
    int GetLastError();
}

public sealed class DesktopLayerAttachment : IDesktopLayerAttachment
{
    private const long WsCaption = 0x00C00000L;
    private const long WsBorder = 0x00800000L;
    private const long WsDlgFrame = 0x00400000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsExTopMost = 0x00000008L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExNoActivate = 0x08000000L;

    private readonly object _gate = new();
    private readonly IDesktopWindowApi _windows;
    private readonly IExplorerDesktopApi _desktop;
    private readonly Dictionary<nint, OriginalWindowState> _original = [];
    private readonly Dictionary<nint, long> _attachedGenerations = [];
    private readonly HashSet<nint> _inputTransparentWindows = [];

    public DesktopLayerAttachment()
        : this(new DesktopWindowNativeApi(), new ExplorerDesktopNativeApi())
    {
    }

    internal DesktopLayerAttachment(
        IDesktopWindowApi windows,
        IExplorerDesktopApi desktop)
    {
        _windows = windows;
        _desktop = desktop;
    }

    public DesktopLayerAttachResult Attach(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost,
        bool showWindow = false,
        bool inputTransparent = false)
    {
        lock (_gate)
        {
            if (!_windows.IsWindow(windowHandle))
            {
                return Failure(windowHandle, desktopHost, 0, "poster-window-invalid");
            }
            if (!_desktop.IsValid(desktopHost.Identity))
            {
                return Failure(windowHandle, desktopHost, 0, "desktop-host-invalid");
            }

            if (!_original.ContainsKey(windowHandle))
            {
                _original.Add(windowHandle, new OriginalWindowState(
                    _windows.GetOwner(windowHandle),
                    _windows.GetStyle(windowHandle),
                    _windows.GetExtendedStyle(windowHandle)));
            }

            long style = _windows.GetStyle(windowHandle).ToInt64();
            style &= ~(WsCaption | WsBorder | WsDlgFrame | WsThickFrame);
            _ = _windows.SetStyle(windowHandle, new nint(style));

            long extendedStyle = _windows.GetExtendedStyle(windowHandle).ToInt64();
            extendedStyle |= WsExToolWindow | WsExNoActivate;
            if (inputTransparent)
            {
                extendedStyle |= WsExTransparent | WsExLayered;
            }
            extendedStyle &= ~WsExTopMost;
            _ = _windows.SetExtendedStyle(windowHandle, new nint(extendedStyle));
            if (inputTransparent &&
                !_windows.ApplyInputTransparentComposition(windowHandle))
            {
                int error = _windows.GetLastError();
                RestoreOriginalState(windowHandle);
                return Failure(
                    windowHandle,
                    desktopHost,
                    _windows.GetOwner(windowHandle),
                    "input-transparent-composition-failed",
                    error);
            }
            _windows.RemoveDwmBorder(windowHandle);

            nint expectedOwner = desktopHost.Identity.IconViewHandle;
            if (_windows.GetOwner(windowHandle) != expectedOwner)
            {
                Marshal.SetLastPInvokeError(0);
                _ = _windows.SetOwner(windowHandle, expectedOwner);
            }

            nint actualOwner = _windows.GetOwner(windowHandle);
            if (actualOwner != expectedOwner)
            {
                int error = _windows.GetLastError();
                RestoreOriginalState(windowHandle);
                return Failure(windowHandle, desktopHost, actualOwner, "owner-readback-mismatch", error);
            }

            if (!_windows.ApplyFrameAndNonTopmost(windowHandle) ||
                !_windows.PlaceAtDesktopSiblingBottom(windowHandle, showWindow))
            {
                int error = _windows.GetLastError();
                RestoreOriginalState(windowHandle);
                return Failure(windowHandle, desktopHost, _windows.GetOwner(windowHandle), "z-order-apply-failed", error);
            }

            actualOwner = _windows.GetOwner(windowHandle);
            bool succeeded = actualOwner == expectedOwner;
            if (succeeded)
            {
                _attachedGenerations[windowHandle] = desktopHost.Generation;
                if (inputTransparent)
                {
                    _inputTransparentWindows.Add(windowHandle);
                }
            }

            return new DesktopLayerAttachResult(
                succeeded,
                windowHandle,
                expectedOwner,
                actualOwner,
                desktopHost.Generation,
                0,
                actualOwner == expectedOwner ? null : "owner-readback-mismatch");
        }
    }

    public bool ReassertDesktopBottom(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost)
    {
        lock (_gate)
        {
            if (!IsOwnershipAttachedCore(windowHandle, desktopHost))
            {
                return false;
            }

            long extendedStyle = _windows.GetExtendedStyle(windowHandle).ToInt64();
            bool inputTransparent = _inputTransparentWindows.Contains(windowHandle);
            long requiredStyle = WsExToolWindow | WsExNoActivate;
            if (inputTransparent)
            {
                requiredStyle |= WsExTransparent | WsExLayered;
            }
            long restingStyle = (extendedStyle | requiredStyle) & ~WsExTopMost;
            if (restingStyle != extendedStyle)
            {
                _ = _windows.SetExtendedStyle(windowHandle, new nint(restingStyle));
                if (!_windows.ApplyFrameAndNonTopmost(windowHandle))
                {
                    return false;
                }
            }
            if (inputTransparent &&
                !_windows.ApplyInputTransparentComposition(windowHandle))
            {
                return false;
            }

            if (!_windows.PlaceAtDesktopSiblingBottom(windowHandle, showWindow: false))
            {
                return false;
            }

            long actualExtendedStyle = _windows.GetExtendedStyle(windowHandle).ToInt64();
            return IsAttachedCore(windowHandle, desktopHost) &&
                (actualExtendedStyle & WsExTopMost) == 0 &&
                (actualExtendedStyle & WsExNoActivate) != 0 &&
                (!inputTransparent ||
                    (actualExtendedStyle & (WsExTransparent | WsExLayered)) ==
                        (WsExTransparent | WsExLayered));
        }
    }

    public bool ApplyRoundedRegion(
        nint windowHandle,
        int width,
        int height,
        int cornerRadius)
    {
        lock (_gate)
        {
            return _windows.IsWindow(windowHandle) &&
                width > 0 &&
                height > 0 &&
                cornerRadius >= 0 &&
                cornerRadius <= Math.Min(width, height) / 2 &&
                _windows.ApplyRoundedRegion(
                    windowHandle,
                    width,
                    height,
                    cornerRadius);
        }
    }

    public bool ApplyPreparedBounds(
        nint windowHandle,
        DesktopPixelRect bounds)
    {
        lock (_gate)
        {
            return bounds.IsUsable &&
                _original.ContainsKey(windowHandle) &&
                _windows.IsWindow(windowHandle) &&
                _windows.ApplyPreparedBounds(windowHandle, bounds);
        }
    }

    public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds)
    {
        lock (_gate)
        {
            return bounds.Count > 0 && bounds.All(p => p.Value.IsUsable && _original.ContainsKey(p.Key) && _windows.IsWindow(p.Key)) &&
                _windows.ApplyPreparedBoundsBatch(bounds);
        }
    }

    public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds, DesktopFramePhase phase)
    {
        if (phase == DesktopFramePhase.Regular) return ApplyPreparedBoundsBatch(bounds);
        lock (_gate)
        {
            if (!Enum.IsDefined(phase) || bounds.Count == 0 ||
                bounds.Any(p => !p.Value.IsUsable || !_original.ContainsKey(p.Key) || !_windows.IsWindow(p.Key))) return false;
            var flags = bounds.Keys.ToDictionary(h => h, h =>
                DesktopPresentationFlags.Resolve(phase, _inputTransparentWindows.Contains(h)));
            return _windows.ApplyPreparedBoundsBatch(bounds, flags);
        }
    }

    // After final layout, the whole HWND batch, HRGN and input restoration. Not a GPU fence.
    public bool CompletePreparedVisualFrame(IReadOnlyCollection<nint> windows)
    {
        lock (_gate)
        {
            if (windows.Count == 0 || windows.Any(h => !_original.ContainsKey(h) ||
                !_inputTransparentWindows.Contains(h) || !_windows.IsWindow(h))) return false;
            bool applied = true;
            foreach (var window in windows) applied &= _windows.RequestFrameRedraw(window);
            return applied;
        }
    }

    public bool VerifyVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible)
        => InspectVisualSurface(window, surface, visible).Verified;

    public DesktopVisualSurfaceReadback InspectVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible)
    {
        lock (_gate)
        {
            if (!_original.ContainsKey(window)) return new(false, "visual-not-registered");
            if (!_inputTransparentWindows.Contains(window)) return new(false, "visual-not-input-transparent");
            if (!_windows.IsWindow(window)) return new(false, "visual-window-invalid");
            return _windows.InspectVisualSurface(window, surface, visible);
        }
    }

    public bool Detach(nint windowHandle)
    {
        lock (_gate)
        {
            if (!_original.ContainsKey(windowHandle))
            {
                return true;
            }
            return RestoreOriginalState(windowHandle);
        }
    }

    public bool IsAttached(nint windowHandle, DesktopShellHostSnapshot desktopHost)
    {
        lock (_gate)
        {
            return IsAttachedCore(windowHandle, desktopHost);
        }
    }

    private bool IsAttachedCore(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost)
    {
        if (!IsOwnershipAttachedCore(windowHandle, desktopHost))
        {
            return false;
        }

        if (!_inputTransparentWindows.Contains(windowHandle))
        {
            return true;
        }

        long extendedStyle = _windows.GetExtendedStyle(windowHandle).ToInt64();
        return (extendedStyle & (WsExTransparent | WsExLayered)) ==
            (WsExTransparent | WsExLayered);
    }

    private bool IsOwnershipAttachedCore(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost) =>
        _windows.IsWindow(windowHandle) &&
        _desktop.IsValid(desktopHost.Identity) &&
        _attachedGenerations.TryGetValue(windowHandle, out long generation) &&
        generation == desktopHost.Generation &&
        _windows.GetOwner(windowHandle) == desktopHost.Identity.IconViewHandle;

    private bool RestoreOriginalState(nint windowHandle)
    {
        _attachedGenerations.Remove(windowHandle);
        _inputTransparentWindows.Remove(windowHandle);
        if (!_original.Remove(windowHandle, out OriginalWindowState? original))
        {
            return true;
        }
        if (!_windows.IsWindow(windowHandle))
        {
            return true;
        }

        _ = _windows.SetOwner(windowHandle, original.Owner);
        _ = _windows.SetStyle(windowHandle, original.Style);
        _ = _windows.SetExtendedStyle(windowHandle, original.ExtendedStyle);
        _ = _windows.ApplyFrameAndNonTopmost(windowHandle);
        return _windows.GetOwner(windowHandle) == original.Owner;
    }

    private static DesktopLayerAttachResult Failure(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost,
        nint actualOwner,
        string error,
        int nativeError = 0) => new(
            false,
            windowHandle,
            desktopHost.Identity.IconViewHandle,
            actualOwner,
            desktopHost.Generation,
            nativeError,
            error);

    private sealed record OriginalWindowState(
        nint Owner,
        nint Style,
        nint ExtendedStyle);
}

internal sealed class DesktopWindowNativeApi : IDesktopWindowApi
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwlpHwndParent = -8;
    private static readonly nint HwndBottom = new(1);
    private static readonly nint HwndNotTopMost = new(-2);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoCopyBits = 0x0100;
    private const uint SwpDeferErase = 0x2000;
    private const uint LwaAlpha = 0x00000002;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;
    private const uint DwmColorNone = 0xFFFFFFFE;

    public bool IsWindow(nint windowHandle) => IsWindowNative(windowHandle);
    public nint GetOwner(nint windowHandle) => GetWindowLongPtr(windowHandle, GwlpHwndParent);
    public nint SetOwner(nint windowHandle, nint ownerHandle) =>
        SetWindowLongPtr(windowHandle, GwlpHwndParent, ownerHandle);
    public nint GetStyle(nint windowHandle) => GetWindowLongPtr(windowHandle, GwlStyle);
    public nint GetExtendedStyle(nint windowHandle) => GetWindowLongPtr(windowHandle, GwlExStyle);
    public nint SetStyle(nint windowHandle, nint style) =>
        SetWindowLongPtr(windowHandle, GwlStyle, style);
    public nint SetExtendedStyle(nint windowHandle, nint style) =>
        SetWindowLongPtr(windowHandle, GwlExStyle, style);

    public bool ApplyFrameAndNonTopmost(nint windowHandle) => SetWindowPos(
        windowHandle,
        HwndNotTopMost,
        0,
        0,
        0,
        0,
        SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);

    public bool PlaceAtDesktopSiblingBottom(nint windowHandle, bool showWindow)
    {
        uint flags = SwpNoMove | SwpNoSize | SwpNoActivate;
        if (showWindow)
        {
            flags |= SwpShowWindow;
        }

        return SetWindowPos(
            windowHandle,
            HwndBottom,
            0,
            0,
            0,
            0,
            flags);
    }

    public bool ApplyInputTransparentComposition(nint windowHandle) =>
        SetLayeredWindowAttributes(windowHandle, 0, 255, LwaAlpha);

    public bool ApplyRoundedRegion(
        nint windowHandle,
        int width,
        int height,
        int cornerRadius)
    {
        int diameter = checked(cornerRadius * 2);
        nint region = CreateRoundRectRgn(
            0,
            0,
            checked(width + 1),
            checked(height + 1),
            diameter,
            diameter);
        if (region == 0)
        {
            return false;
        }

        if (SetWindowRgn(windowHandle, region, redraw: false) != 0)
        {
            // A successful SetWindowRgn transfers region ownership to Windows.
            return true;
        }

        _ = DeleteObject(region);
        return false;
    }

    public bool ApplyPreparedBounds(
        nint windowHandle,
        DesktopPixelRect bounds) => SetWindowPos(
            windowHandle,
            0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            DesktopZOrderPolicy.SwpNoZOrder |
            SwpNoActivate |
            SwpNoCopyBits |
            SwpDeferErase);

    public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds)
    {
        nint batch = BeginDeferWindowPos(bounds.Count);
        return ApplyBatch(batch, bounds, bounds.Keys.ToDictionary(h => h, _ => DesktopZOrderPolicy.SpatialBatchFlags));
    }

    public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds, IReadOnlyDictionary<nint, uint> flags)
        => ApplyBatch(BeginDeferWindowPos(bounds.Count), bounds, flags);

    private static bool ApplyBatch(nint batch, IReadOnlyDictionary<nint, DesktopPixelRect> bounds, IReadOnlyDictionary<nint, uint> flags)
    {
        if (batch == 0) return false;
        foreach (var entry in bounds)
        {
            var frame = entry.Value;
            batch = DeferWindowPos(batch, entry.Key, 0, frame.X, frame.Y, frame.Width, frame.Height,
                // DeferWindowPos has a narrower documented flag set than SetWindowPos.
                // SWP_DEFERERASE (0x2000) makes this batch fail with ERROR_INVALID_PARAMETER.
                flags[entry.Key]);
            if (batch == 0) return false;
        }
        return EndDeferWindowPos(batch);
    }

    public bool RequestFrameRedraw(nint windowHandle) => RedrawWindow(windowHandle, 0, 0,
        0x0001 /* INVALIDATE */ | 0x0080 /* ALLCHILDREN */ | 0x0100 /* UPDATENOW */);

    public bool VerifyVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible)
        => InspectVisualSurface(window, surface, visible).Verified;

    public DesktopVisualSurfaceReadback InspectVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible)
    {
        DesktopPixelRect? native = GetWindowRect(window, out var windowRect) ? Pixels(windowRect) : null;
        DesktopPixelRect? client = GetClientRect(window, out var clientRect) ? Pixels(clientRect) : null;
        DesktopPixelRect? clip = null;
        int? regionResult = null, boxResult = null;
        nint region = CreateRectRgn(0, 0, 0, 0);
        if (region != 0)
        {
            try
            {
                regionResult = GetWindowRgn(window, region);
                if (regionResult != 0)
                {
                    boxResult = GetRgnBox(region, out var clipRect);
                    if (boxResult != 0) clip = Pixels(clipRect);
                }
            }
            finally { _ = DeleteObject(region); }
        }
        return DesktopVisualSurfaceReadback.Compare(surface, visible, native, client, clip, regionResult, boxResult);
    }

    private static DesktopPixelRect Pixels(SurfaceRect rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    [StructLayout(LayoutKind.Sequential)] private struct SurfaceRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out SurfaceRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out SurfaceRect rect);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int GetRgnBox(nint region, out SurfaceRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(nint window, nint updateRect, nint updateRegion, uint flags);

    public void RemoveDwmBorder(nint windowHandle)
    {
        int cornerPreference = DwmwcpDoNotRound;
        uint borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmwaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmwaBorderColor,
            ref borderColor,
            sizeof(uint));
    }

    public int GetLastError() => Marshal.GetLastPInvokeError();

    [DllImport("user32.dll", EntryPoint = "IsWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowNative(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint BeginDeferWindowPos(int count);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint DeferWindowPos(nint batch, nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndDeferWindowPos(nint batch);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

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
    private static extern int SetWindowRgn(
        nint windowHandle,
        nint region,
        [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref uint value,
        int valueSize);
}
