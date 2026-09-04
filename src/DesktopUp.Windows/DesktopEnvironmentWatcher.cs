using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopUp.Windows;

[Flags]
public enum DesktopEnvironmentChangeKind
{
    None = 0,
    DesktopShell = 1,
    DisplayTopology = 2
}

public sealed record DesktopEnvironmentChangedEventArgs(
    DesktopEnvironmentChangeKind Kind,
    string Reason);

public sealed record DesktopPointerActivationGuardedEventArgs(
    bool DesktopBottomRestored,
    string? Error);

public sealed record DesktopLayerActivationGuardedEventArgs(
    string Reason,
    bool DesktopBottomRestored,
    string? Error);

public sealed record DesktopZOrderChangeConstrainedEventArgs(
    nint RequestedInsertAfter);

internal static class DesktopPointerActivationPolicy
{
    internal const uint WmMouseActivate = 0x0021;
    internal const uint WmNcHitTest = 0x0084;
    internal const int MaNoActivate = 3;
    internal const int HtTransparent = -1;

    public static bool ShouldSuppress(uint message) =>
        message == WmMouseActivate;
}

internal static class DesktopLayerActivationPolicy
{
    internal const uint WmActivate = 0x0006;
    internal const uint WmActivateApp = 0x001C;

    public static bool ShouldReassert(uint message) =>
        message is WmActivate or WmActivateApp;

    public static string ResolveReason(uint message) => message switch
    {
        WmActivate => "wm-activate",
        WmActivateApp => "wm-activateapp",
        _ => $"message-0x{message:X4}"
    };
}

internal static class DesktopZOrderPolicy
{
    internal const uint WmWindowPosChanging = 0x0046;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal static readonly nint HwndBottom = new(1);

    public static bool ShouldConstrain(uint flags, nint requestedInsertAfter) =>
        (flags & SwpNoZOrder) == 0 && requestedInsertAfter != HwndBottom;
}

internal static class DesktopEnvironmentSignalClassifier
{
    internal const uint WmSettingChange = 0x001A;
    internal const uint WmDisplayChange = 0x007E;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint SpiSetWorkArea = 0x002F;

    public static DesktopEnvironmentChangeKind Classify(
        uint message,
        nuint wParam,
        string? settingArea,
        uint taskbarCreatedMessage)
    {
        if (message == taskbarCreatedMessage)
        {
            return DesktopEnvironmentChangeKind.DesktopShell |
                DesktopEnvironmentChangeKind.DisplayTopology;
        }

        if (message is WmDisplayChange or WmDpiChanged)
        {
            return DesktopEnvironmentChangeKind.DisplayTopology;
        }

        if (message == WmSettingChange &&
            (wParam == SpiSetWorkArea || IsRelevantSettingArea(settingArea)))
        {
            return DesktopEnvironmentChangeKind.DisplayTopology;
        }

        return DesktopEnvironmentChangeKind.None;
    }

    private static bool IsRelevantSettingArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return false;
        }

        return area.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("WorkArea", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("Taskbar", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("Tray", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("StuckRects", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("ShellState", StringComparison.OrdinalIgnoreCase) ||
            area.Contains("AppBar", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DesktopEnvironmentWatcher : IDisposable
{
    private const uint WmEraseBackground = 0x0014;
    private const uint WmNcDestroy = 0x0082;
    private const uint PosterFallbackColorRef = 0x002A2421;
    private static readonly nuint SubclassId = 0xD35C;
    private readonly nint _windowHandle;
    private readonly uint _taskbarCreatedMessage;
    private readonly SubclassProc _subclassProc;
    private readonly Func<bool> _reassertDesktopBottom;
    private nint _fallbackBrush;
    private bool _installed;
    private bool _disposed;
    private bool _layerRepairInProgress;

    public DesktopEnvironmentWatcher(
        nint windowHandle,
        Func<bool> reassertDesktopBottom)
    {
        if (windowHandle == 0 || !IsWindow(windowHandle))
        {
            throw new ArgumentException("A valid top-level window is required.", nameof(windowHandle));
        }
        ArgumentNullException.ThrowIfNull(reassertDesktopBottom);

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _windowHandle = windowHandle;
        _reassertDesktopBottom = reassertDesktopBottom;
        _subclassProc = WindowSubclassProc;
        _fallbackBrush = CreateSolidBrush(PosterFallbackColorRef);
        if (_fallbackBrush == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        _installed = SetWindowSubclass(
            _windowHandle,
            _subclassProc,
            SubclassId,
            0);
        if (!_installed)
        {
            ReleaseFallbackBrush();
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    public event EventHandler<DesktopEnvironmentChangedEventArgs>? Changed;
    public event EventHandler<DesktopPointerActivationGuardedEventArgs>?
        PointerActivationGuarded;
    public event EventHandler<DesktopLayerActivationGuardedEventArgs>?
        LayerActivationGuarded;
    public event EventHandler<DesktopZOrderChangeConstrainedEventArgs>?
        ZOrderChangeConstrained;

    private nint WindowSubclassProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmNcDestroy)
        {
            _installed = false;
            _disposed = true;
            ReleaseFallbackBrush();
        }
        else if (message == WmEraseBackground &&
            wParam != 0 &&
            _fallbackBrush != 0 &&
            GetClientRect(windowHandle, out NativeRect client))
        {
            _ = FillRect((nint)wParam, ref client, _fallbackBrush);
            return new nint(1);
        }
        else if (message == DesktopZOrderPolicy.WmWindowPosChanging)
        {
            ConstrainPendingZOrder(lParam);
        }
        else if (message == DesktopPointerActivationPolicy.WmNcHitTest)
        {
            return new nint(DesktopPointerActivationPolicy.HtTransparent);
        }
        else if (DesktopPointerActivationPolicy.ShouldSuppress(message))
        {
            (bool restored, string? error) = TryReassertDesktopBottom();

            PointerActivationGuarded?.Invoke(
                this,
                new DesktopPointerActivationGuardedEventArgs(restored, error));
            return new nint(DesktopPointerActivationPolicy.MaNoActivate);
        }
        else if (DesktopLayerActivationPolicy.ShouldReassert(message))
        {
            (bool restored, string? error) = TryReassertDesktopBottom();
            LayerActivationGuarded?.Invoke(
                this,
                new DesktopLayerActivationGuardedEventArgs(
                    DesktopLayerActivationPolicy.ResolveReason(message),
                    restored,
                    error));
        }
        else
        {
            string? settingArea =
                message == DesktopEnvironmentSignalClassifier.WmSettingChange &&
                lParam != 0
                    ? Marshal.PtrToStringUni(lParam)
                    : null;
            DesktopEnvironmentChangeKind kind =
                DesktopEnvironmentSignalClassifier.Classify(
                    message,
                    wParam,
                    settingArea,
                    _taskbarCreatedMessage);
            if (kind != DesktopEnvironmentChangeKind.None)
            {
                Changed?.Invoke(
                    this,
                    new DesktopEnvironmentChangedEventArgs(
                        kind,
                        ResolveReason(message)));
            }
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void ConstrainPendingZOrder(nint lParam)
    {
        if (lParam == 0 || _layerRepairInProgress)
        {
            return;
        }

        try
        {
            WindowPos position = Marshal.PtrToStructure<WindowPos>(lParam);
            if (!DesktopZOrderPolicy.ShouldConstrain(
                    position.Flags,
                    position.HwndInsertAfter))
            {
                return;
            }

            nint requestedInsertAfter = position.HwndInsertAfter;
            position.HwndInsertAfter = DesktopZOrderPolicy.HwndBottom;
            position.Flags |= DesktopZOrderPolicy.SwpNoActivate;
            Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
            ZOrderChangeConstrained?.Invoke(
                this,
                new DesktopZOrderChangeConstrainedEventArgs(
                    requestedInsertAfter));
        }
        catch (Exception exception)
        {
            LayerActivationGuarded?.Invoke(
                this,
                new DesktopLayerActivationGuardedEventArgs(
                    "wm-windowposchanging",
                    false,
                    $"{exception.GetType().FullName}: {exception.Message}"));
        }
    }

    private (bool Restored, string? Error) TryReassertDesktopBottom()
    {
        if (_layerRepairInProgress)
        {
            return (true, null);
        }

        _layerRepairInProgress = true;
        try
        {
            return (_reassertDesktopBottom(), null);
        }
        catch (Exception exception)
        {
            return (
                false,
                $"{exception.GetType().FullName}: {exception.Message}");
        }
        finally
        {
            _layerRepairInProgress = false;
        }
    }

    private string ResolveReason(uint message)
    {
        if (message == _taskbarCreatedMessage)
        {
            return "taskbar-created";
        }

        return message switch
        {
            DesktopEnvironmentSignalClassifier.WmDisplayChange => "wm-displaychange",
            DesktopEnvironmentSignalClassifier.WmDpiChanged => "wm-dpichanged",
            DesktopEnvironmentSignalClassifier.WmSettingChange => "wm-settingchange",
            _ => $"message-0x{message:X4}"
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_installed && IsWindow(_windowHandle))
        {
            _ = RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        }
        _installed = false;
        ReleaseFallbackBrush();
        Changed = null;
        PointerActivationGuarded = null;
        LayerActivationGuarded = null;
        ZOrderChangeConstrained = null;
    }

    private void ReleaseFallbackBrush()
    {
        nint brush = _fallbackBrush;
        _fallbackBrush = 0;
        if (brush != 0)
        {
            _ = DeleteObject(brush);
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate nint SubclassProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    [DllImport("user32.dll", EntryPoint = "IsWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint windowHandle,
        SubclassProc subclassProc,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint windowHandle,
        SubclassProc subclassProc,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint device, ref NativeRect rectangle, nint brush);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);
}
