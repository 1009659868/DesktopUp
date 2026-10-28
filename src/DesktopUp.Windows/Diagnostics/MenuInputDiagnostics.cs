using System.Runtime.InteropServices;
using System.Text;

namespace DesktopUp.Windows;

// Opt-in observations only. No input injection, hooks, window/style changes or routing decisions.
public static class MenuInputDiagnostics
{
    public static bool Enabled { get; } = IsEnabled();
    public static event Action<object>? NativeMessage;
    public static bool Collecting => Enabled && NativeMessage is not null;
    [ThreadStatic] private static bool s_observing;
    private static long s_lastMove;

    public static void Route(nint receiver, object detail)
    {
        if (!Collecting || s_observing) return;
        s_observing = true;
        try { NativeMessage?.Invoke(new { source = "resolved-input-route", receiver = Window(receiver), detail }); }
        catch { }
        finally { s_observing = false; }
    }

    private static bool IsEnabled() => AllowsIsolatedTrace(
        Environment.GetEnvironmentVariable("DESKTOPUP_V3_MENU_DIAGNOSTICS"),
        Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));

    internal static bool AllowsIsolatedTrace(string? flag, string? root, string normalRoot)
    {
        try
        {
            if (flag != "1" || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return false;
            string normal = Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalRoot));
            string actual = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            return !actual.Equals(normal, StringComparison.OrdinalIgnoreCase) &&
                !actual.StartsWith(normal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void Observe(nint receiver, uint message, nint lParam, string source)
    {
        if (!Enabled || NativeMessage is null || s_observing ||
            message is not (0x0084 or 0x0021 or 0x0200 or 0x0201 or 0x0202 or 0x0215)) return;
        long now = Environment.TickCount64;
        if (message is 0x0200 or 0x0084 && now - s_lastMove < 125) return;
        if (message is 0x0200 or 0x0084) s_lastMove = now;
        s_observing = true;
        try
        {
            uint packed = GetMessagePos();
            var queuedPoint = new Point { X = unchecked((short)packed), Y = unchecked((short)(packed >> 16)) };
            NativeMessage?.Invoke(new
            {
                source, message = $"0x{message:X4}", receiver = Window(receiver),
                queuedScreenPoint = new { queuedPoint.X, queuedPoint.Y },
                lParam = $"0x{lParam:X}", current = PointerSnapshot()
            });
        }
        catch { /* Diagnostics must never alter the window procedure's outcome. */ }
        finally { s_observing = false; }
    }

    public static object PointerSnapshot()
    {
        bool available = GetCursorPos(out Point point);
        return new
        {
            available, screenX = point.X, screenY = point.Y,
            // A query is not evidence of message delivery; Observe records that separately.
            windowFromPoint = available ? Window(WindowFromPoint(point)) : null,
            capture = Window(GetCapture())
        };
    }

    public static object[] OwnWindowSnapshot()
    {
        var windows = new List<object>();
        var seen = new HashSet<nint>();
        for (nint hwnd = GetTopWindow(0); hwnd != 0 && seen.Count < 4096 && seen.Add(hwnd); hwnd = GetWindow(hwnd, 2))
        {
            _ = GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId != Environment.ProcessId || !IsWindowVisible(hwnd)) continue;
            windows.Add(new { zIndex = seen.Count - 1, window = Window(hwnd) });
            EnumChildWindows(hwnd, (child, unused) =>
            {
                _ = GetWindowThreadProcessId(child, out uint childProcess);
                if (childProcess == Environment.ProcessId && IsWindowVisible(child))
                    windows.Add(new { topLevel = $"0x{hwnd:X}", window = Window(child) });
                return true;
            }, 0);
        }
        return windows.ToArray();
    }

    public static object Window(nint hwnd)
    {
        uint threadId = GetWindowThreadProcessId(hwnd, out uint processId);
        // No titles, paths or geometry from other applications are collected.
        if (hwnd == 0 || processId != Environment.ProcessId)
            return new { handle = $"0x{hwnd:X}", ownProcess = false };
        var name = new StringBuilder(256);
        _ = GetClassName(hwnd, name, name.Capacity);
        bool haveRect = GetWindowRect(hwnd, out Rect rect);
        var origin = new Point();
        bool haveOrigin = ClientToScreen(hwnd, ref origin);
        return new
        {
            handle = $"0x{hwnd:X}", ownProcess = true, threadId, className = name.ToString(),
            owner = $"0x{GetWindow(hwnd, 4):X}", parent = $"0x{GetParent(hwnd):X}",
            style = $"0x{GetWindowLongPtr(hwnd, -16):X}", exStyle = $"0x{GetWindowLongPtr(hwnd, -20):X}",
            dpi = GetDpiForWindow(hwnd), visible = IsWindowVisible(hwnd),
            haveRect, rect = new { rect.Left, rect.Top, rect.Right, rect.Bottom },
            haveOrigin, clientOrigin = new { origin.X, origin.Y }
        };
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    private delegate bool EnumProc(nint hwnd, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetMessagePos();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumProc callback, nint parameter);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
}
