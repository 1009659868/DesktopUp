using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopUp.Windows;

internal sealed class NativeDesktopMenuPlatform : IDesktopMenuNative
{
    public bool Exists(nint window) => IsWindow(window);
    public bool IsInputTransparent(nint window) => (GetWindowLongPtr(window, -20).ToInt64() & 0x20) != 0;
    public bool SetInputTransparent(nint window, bool transparent)
    {
        if (!IsOwnWindow(window)) return false;
        long style = GetWindowLongPtr(window, -20).ToInt64();
        _ = SetWindowLongPtr(window, -20, new nint(transparent ? style | 0x20 : style & ~0x20L));
        return SetWindowPos(window, 0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020 | 0x0200) &&
            IsInputTransparent(window) == transparent;
    }

    public nint FindPopup(nint owner)
    {
        nint match = 0;
        foreach (var window in ReadZOrder())
        {
            if (!IsOwnWindow(window.Handle) || GetWindow(window.Handle, 4) != owner) continue;
            var name = new StringBuilder(128);
            _ = GetClassName(window.Handle, name, name.Capacity);
            // Version-specific native bridge, already observed in the diagnostic build.
            // Fail closed on an unknown/multiple bridge rather than mutating another HWND.
            if (name.ToString() != "Microsoft.UI.Content.PopupWindowSiteBridge") continue;
            if (match != 0) return 0;
            match = window.Handle;
        }
        return match;
    }

    public IReadOnlyList<DesktopMenuZWindow> ReadZOrder()
    {
        var result = new List<DesktopMenuZWindow>();
        var seen = new HashSet<nint>();
        for (nint hwnd = GetTopWindow(0); hwnd != 0 && seen.Count < 4096 && seen.Add(hwnd); hwnd = GetWindow(hwnd, 2))
            if (IsWindowVisible(hwnd)) result.Add(new(hwnd, (GetWindowLongPtr(hwnd, -20).ToInt64() & 8) != 0));
        return result;
    }

    public int? LastPlacementError { get; private set; }
    public string? LastPlacementFailure { get; private set; }
    public bool IsOwnUiWindow(nint window) => IsOwnWindow(window);
    public bool IsOwnedPopup(nint popup, nint owner) => IsOwnWindow(popup) && IsOwnWindow(owner) &&
        IsWindowVisible(popup) && GetWindow(popup, 4) == owner;
    public bool PlacePopupAfterOwnWindow(nint popup, nint input, nint owner)
    {
        if (!IsOwnedPopup(popup, owner) || !IsOwnInput(input))
        {
            LastPlacementError = null; LastPlacementFailure = "own-window-validation-failed";
            return false;
        }
        return PlaceOwnWindowAfter(popup, input);
    }
    public bool PlaceInputAfterPopup(nint input, nint popup, nint owner)
    {
        if (!IsOwnedPopup(popup, owner) || !IsOwnInput(input))
        {
            LastPlacementError = null; LastPlacementFailure = "own-window-validation-failed";
            return false;
        }
        return PlaceOwnWindowAfter(input, popup);
    }
    private bool IsOwnInput(nint input)
    {
        if (!IsOwnWindow(input) || !IsWindowVisible(input)) return false;
        var name = new StringBuilder(128);
        _ = GetClassName(input, name, name.Capacity);
        return name.ToString() == "DesktopUp.V3.PosterInputIsland";
    }
    private bool PlaceOwnWindowAfter(nint window, nint insertAfter)
    {
        LastPlacementError = null; LastPlacementFailure = null;
        if (!IsOwnWindow(window) || !IsOwnWindow(insertAfter)) { LastPlacementFailure = "not-own-ui-thread"; return false; }
        bool placed = SetWindowPos(window, insertAfter, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0200);
        if (!placed)
        {
            LastPlacementError = Marshal.GetLastPInvokeError();
            LastPlacementFailure = "set-window-pos";
        }
        return placed;
    }

    public object DescribeWindow(nint window)
    {
        bool exists = IsWindow(window);
        uint thread = GetWindowThreadProcessId(window, out uint process);
        bool ownProcess = process == Environment.ProcessId;
        // Foreign windows contribute only identity/visibility/style ordering evidence, never
        // their text, class, owner chain, bounds or executable path.
        if (!ownProcess) return new { handle = $"0x{window:X}", exists, ownProcess,
            visible = IsWindowVisible(window), topmost = (GetWindowLongPtr(window, -20).ToInt64() & 8) != 0 };
        var name = new StringBuilder(128);
        _ = GetClassName(window, name, name.Capacity);
        return new
        {
            handle = $"0x{window:X}", exists, ownProcess, thread, currentThread = GetCurrentThreadId(),
            className = name.ToString(), owner = $"0x{GetWindow(window, 4):X}",
            visible = IsWindowVisible(window), topmost = (GetWindowLongPtr(window, -20).ToInt64() & 8) != 0,
            style = $"0x{GetWindowLongPtr(window, -16):X}", exStyle = $"0x{GetWindowLongPtr(window, -20):X}"
        };
    }
    public IDisposable WatchPopup(nint popup, Action<uint, nint> beforeMessage) => new PopupHook(popup, beforeMessage);

    public IReadOnlyList<DesktopMenuHit> SamplePopup(nint popup)
    {
        if (!IsOwnWindow(popup) || !GetWindowRect(popup, out var rect)) return [];
        int inset = Math.Max(16, (int)GetDpiForWindow(popup) / 6);
        if (rect.Right - rect.Left <= inset * 2 || rect.Bottom - rect.Top <= inset * 2) return [];
        var points = new List<DesktopMenuHit>();
        for (int row = 0; row < 5; row++)
            for (int column = 0; column < 3; column++)
            {
                int x = rect.Left + inset + (rect.Right - rect.Left - inset * 2) * column / 2;
                int y = rect.Top + inset + (rect.Bottom - rect.Top - inset * 2) * row / 4;
                nint hit = WindowFromPoint(new Point { X = x, Y = y });
                _ = GetWindowThreadProcessId(hit, out uint process);
                points.Add(new(x, y, hit == popup || IsChild(popup, hit), $"0x{hit:X}", process == Environment.ProcessId));
            }
        return points;
    }

    private static bool IsOwnWindow(nint hwnd)
    {
        uint thread = GetWindowThreadProcessId(hwnd, out uint process);
        return hwnd != 0 && process == Environment.ProcessId && thread == GetCurrentThreadId();
    }

    private sealed class PopupHook : IDisposable
    {
        private static readonly HashSet<PopupHook> LiveHooks = [];
        private readonly nint _window;
        private readonly SubclassProc _callback;
        private bool _installed;
        private const nuint Id = 0xD34D;

        public PopupHook(nint window, Action<uint, nint> beforeMessage)
        {
            if (!IsOwnWindow(window)) throw new InvalidOperationException("Popup must belong to this UI thread.");
            _window = window;
            _callback = (hwnd, message, wParam, lParam, subclass, data) =>
            {
                try { beforeMessage(message, lParam); }
                catch { /* No managed exception may cross the native window procedure. */ }
                if (message == 0x0082)
                {
                    _ = RemoveWindowSubclass(hwnd, _callback!, Id); // delegate assigned before native registration
                    _installed = false;
                    lock (LiveHooks) LiveHooks.Remove(this);
                }
                return DefSubclassProc(hwnd, message, wParam, lParam);
            };
            _installed = SetWindowSubclass(window, _callback, Id, 0);
            if (!_installed) throw new Win32Exception(Marshal.GetLastPInvokeError());
            lock (LiveHooks) LiveHooks.Add(this);
        }
        public void Dispose()
        {
            if (!_installed) return;
            if (IsWindow(_window) && !RemoveWindowSubclass(_window, _callback, Id))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            _installed = false;
            lock (LiveHooks) LiveHooks.Remove(this);
        }
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
}
