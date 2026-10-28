using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

// Read system click preferences only when handling an already routed content click.
// No hook, keyboard polling, window activation or independent input owner.
public static class NativeContentInteraction
{
    public static ContentDoubleClickSettings DoubleClickSettings => new(GetDoubleClickTime(),
        Math.Max(1, GetSystemMetrics(36) / 2), Math.Max(1, GetSystemMetrics(37) / 2));

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
