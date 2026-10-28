using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

internal static class ContentContextMessagePoint
{
    // WM_RBUTTON* carries signed client pixels from that event. Sampling the current
    // cursor instead loses the press witness when DOWN/UP wait in the UI queue.
    internal static bool TryRead(nint window, nint messagePoint, DesktopPixelRect expectedFrame,
        out PointerScreenPoint point)
    {
        point = default;
        if (window == 0 || !GetWindowRect(window, out var frame) ||
            frame.Left != expectedFrame.X || frame.Top != expectedFrame.Y ||
            frame.Right != expectedFrame.Right || frame.Bottom != expectedFrame.Bottom) return false;
        var client = new Point { X = unchecked((short)(messagePoint.ToInt64() & 0xffff)),
            Y = unchecked((short)((messagePoint.ToInt64() >> 16) & 0xffff)) };
        if (!ClientToScreen(window, ref client)) return false;
        point = new(client.X, client.Y);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref Point point);
}
