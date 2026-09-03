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
