using System;
using System.Runtime.InteropServices;
using System.Text;

// External measurement preflight, compatible with Windows PowerShell 5.1/.NET Framework.
// Records availability only; never switches/attaches desktops or records cursor coordinates.
public static class DesktopUpM08InputProbe
{
    [DllImport("user32.dll", SetLastError=true)] static extern IntPtr OpenInputDesktop(uint f, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError=true)] static extern bool GetPhysicalCursorPos(out Point p);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint t);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint="GetUserObjectInformationW", CharSet=CharSet.Unicode)]
    static extern bool Info(IntPtr h, int n, StringBuilder b, int s, out int used);
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    static string Name(IntPtr h)
    {
        var b = new StringBuilder(256); int used;
        return h != IntPtr.Zero && Info(h, 2, b, 512, out used) ? b.ToString() : null;
    }
    public static string[] Read()
    {
        var h = OpenInputDesktop(0, false, 1); int e = h == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        string input = null, thread = Name(GetThreadDesktop(GetCurrentThreadId()));
        try { input = Name(h); } finally { if (h != IntPtr.Zero) CloseDesktop(h); }
        Point p; bool cursor = GetPhysicalCursorPos(out p); int ce = cursor ? 0 : Marshal.GetLastWin32Error();
        return new[] { (h != IntPtr.Zero).ToString(), e.ToString(), input, thread, cursor.ToString(), ce.ToString(), GetForegroundWindow().ToString() };
    }
}
