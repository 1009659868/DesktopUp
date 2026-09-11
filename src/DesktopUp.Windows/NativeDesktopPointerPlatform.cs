using System.Runtime.InteropServices;
using System.Text;
using DesktopUp.Application;

namespace DesktopUp.Windows;

internal sealed class NativeDesktopPointerPlatform : IDesktopPointerNative
{
    public bool TryReadCursor(out PointerScreenPoint point)
    {
        point = default;
        // Never switch/attach input desktops. Secure/unavailable desktops fail closed.
        nint input = OpenInputDesktop(0, false, 0x0001); // DESKTOP_READOBJECTS
        if (input == 0) return false;
        try
        {
            var inputName = DesktopName(input);
            if (inputName is null || inputName != DesktopName(GetThreadDesktop(GetCurrentThreadId())) ||
                !GetPhysicalCursorPos(out var p)) return false;
            point = new(p.X, p.Y);
            return true;
        }
        finally { _ = CloseDesktop(input); }
    }

    public bool TryReadProtection(out AutoPostureProtection protection)
    {
        protection = AutoPostureProtection.None;
        // Read physical left/right/middle/X buttons, only the high (currently down) bit.
        foreach (int button in new[] { 1, 2, 4, 5, 6 })
            if ((GetAsyncKeyState(button) & 0x8000) != 0) protection |= AutoPostureProtection.PointerButton;
        if (GetCapture() != 0) protection |= AutoPostureProtection.PointerCapture;
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(0, ref info)) return false;
        if (info.Capture != 0 || info.MoveSize != 0 || (info.Flags & 2) != 0)
            protection |= AutoPostureProtection.PointerCapture;
        if (info.MenuOwner != 0 || (info.Flags & (4 | 8 | 16)) != 0)
            protection |= AutoPostureProtection.Menu;
        return true;
    }

    public nint ReceiverRoot(PointerScreenPoint point)
    {
        nint receiver = WindowFromPhysicalPoint(new() { X = point.X, Y = point.Y });
        // GA_ROOT, NOT ROOTOWNER: a popup must not inherit its desktop owner's authority.
        return receiver == 0 ? 0 : GetAncestor(receiver, 2);
    }

    public IReadOnlyList<nint>? ReadWindowOrder()
    {
        var order = new List<nint>();
        var seen = new HashSet<nint>();
        for (nint hwnd = GetTopWindow(0); hwnd != 0; hwnd = GetWindow(hwnd, 2))
        {
            if (order.Count >= 4096 || !seen.Add(hwnd)) return null;
            order.Add(hwnd);
        }
        return order;
    }

    public bool IsSurfaceCurrent(nint visual, nint input, DesktopPixelRect frame)
    {
        if (!IsPresented(visual) || !IsPresented(input) || !GetWindowRect(input, out var rect)) return false;
        _ = GetWindowThreadProcessId(visual, out uint visualProcess);
        _ = GetWindowThreadProcessId(input, out uint inputProcess);
        return visualProcess == (uint)Environment.ProcessId && inputProcess == visualProcess &&
            rect.Left == frame.X && rect.Top == frame.Y && rect.Right == frame.Right && rect.Bottom == frame.Bottom;
    }

    // An alternate/rebuilding desktop root is ambiguous, not an ordinary occluding app.
    // Its class alone can only withhold authority, never grant hover permission.
    public bool IsDesktopRoot(nint root) => ClassName(root) is "Progman" or "WorkerW";

    public bool IsDesktopReceiver(nint root, DesktopShellHostSnapshot shell)
    {
        var id = shell.Identity;
        // Identity was obtained by the shared Explorer locator. Revalidate live handles/class/PID;
        // neither any window in explorer.exe nor an owner chain alone is an allow-list.
        if (id.IsEmpty || root != id.ContainerHandle || !IsWindow(id.ContainerHandle) || !IsWindow(id.IconViewHandle) ||
            GetParent(id.IconViewHandle) != id.ContainerHandle || ClassName(id.IconViewHandle) != "SHELLDLL_DefView" ||
            ClassName(id.ContainerHandle) is not ("Progman" or "WorkerW")) return false;
        _ = GetWindowThreadProcessId(id.ContainerHandle, out uint containerProcess);
        _ = GetWindowThreadProcessId(id.IconViewHandle, out uint iconProcess);
        return containerProcess == id.ProcessId && iconProcess == id.ProcessId;
    }

    public bool CoversPoint(nint window, PointerScreenPoint point)
    {
        if (!IsPresented(window) || !GetWindowRect(window, out var r) ||
            point.X < r.Left || point.Y < r.Top || point.X >= r.Right || point.Y >= r.Bottom) return false;
        nint region = CreateRectRgn(0, 0, 0, 0);
        if (region == 0) return true; // unknown shape conservatively occludes
        try
        {
            int kind = GetWindowRgn(window, region);
            return kind == 0 || PtInRegion(region, point.X - r.Left, point.Y - r.Top);
        }
        finally { _ = DeleteObject(region); }
    }

    private static bool IsPresented(nint hwnd) => IsWindowVisible(hwnd) && !IsIconic(hwnd) &&
        (DwmGetWindowAttribute(hwnd, 14, out int cloaked, sizeof(int)) != 0 || cloaked == 0);

    private static string ClassName(nint hwnd)
    {
        var text = new StringBuilder(256);
        return GetClassName(hwnd, text, text.Capacity) > 0 ? text.ToString() : string.Empty;
    }
    private static string? DesktopName(nint desktop)
    {
        var text = new StringBuilder(256);
        return desktop != 0 && GetUserObjectInformation(desktop, 2, text, text.Capacity * 2, out _) ? text.ToString() : null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    {
        public uint Size, Flags;
        public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public Rect CaretRect;
    }
    [DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint WindowFromPhysicalPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] private static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder text, int bytes, out int needed);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder text, int count);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint hwnd, nint region);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
}
