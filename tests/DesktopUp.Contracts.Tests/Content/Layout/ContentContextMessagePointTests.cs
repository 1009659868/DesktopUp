using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentContextMessagePointTests
{
    [Fact]
    public void QueuedBodyPressAndPinReleaseRemainDifferentOnNegativeCoordinateDesktop()
    {
        // A hidden, owned native window: no pointer injection, desktop writes or activation.
        nint previous = SetThreadDpiAwarenessContext(new(-4));
        nint window = CreateWindowEx(0x08000080, "STATIC", "context coordinate fixture", 0x80000000,
            -5000, -5000, 300, 300, 0, 0, 0, 0);
        try
        {
            Assert.NotEqual(0, window);
            var frame = new DesktopPixelRect(-5000, -5000, 300, 300);
            Assert.True(ContentContextMessagePoint.TryRead(window, Packed(50, 50), frame, out var press));
            Assert.True(ContentContextMessagePoint.TryRead(window, Packed(188, 12), frame, out var release));
            var pin = ContentPinPresentationPolicy.Bounds(new(0, 0, 200, 100));
            (double X, double Y) Local(PointerScreenPoint point) => (point.X-frame.X, point.Y-frame.Y);
            Assert.Equal(ContentPinContextAction.Cancel,
                ContentPinPresentationPolicy.ResolveContext([pin], Local(press), Local(release)));
            Assert.Equal(ContentPinContextAction.ReleasePin,
                ContentPinPresentationPolicy.ResolveContext([pin], Local(release), Local(release)));
            // A stale published frame must not be used to reinterpret an event as a new target.
            Assert.False(ContentContextMessagePoint.TryRead(window, Packed(188, 12), frame with { X = -4999 }, out _));
            Assert.True(ContentContextMessagePoint.TryRead(window, Packed(-3, -4), frame, out var outside));
            Assert.Equal(new PointerScreenPoint(-5003, -5004), outside);
        }
        finally
        {
            if (window != 0) DestroyWindow(window);
            if (previous != 0) SetThreadDpiAwarenessContext(previous);
        }
    }

    private static nint Packed(int x, int y) => new(unchecked((int)((uint)(ushort)x | ((uint)(ushort)y << 16))));
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint extended, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
}
