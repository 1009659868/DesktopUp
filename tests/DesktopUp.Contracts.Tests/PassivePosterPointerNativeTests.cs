using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class PassivePosterPointerNativeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void Real_offscreen_hwnds_preserve_input_shape_focus_bounds_and_order()
    {
        // A tiny owned Win32 fixture beyond the virtual desktop. No cursor movement, clicks,
        // topmost window, Explorer mutation or DesktopUp app/profile is involved.
        nint foreground = GetForegroundWindow();
        nint context = SetThreadDpiAwarenessContext(new(-4)); // physical geometry (same as App manifest)
        string name = "DesktopUp.M08.PointerFixture." + Guid.NewGuid().ToString("N");
        WindowProc callback = DefWindowProc;
        var cls = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(),
            Procedure = Marshal.GetFunctionPointerForDelegate(callback), ClassName = name, Instance = GetModuleHandle(null) };
        nint visual = 0, input = 0, blocker = 0;
        Assert.NotEqual((ushort)0, RegisterClassEx(ref cls));
        try
        {
            int x = GetSystemMetrics(76) + GetSystemMetrics(78) + 256;
            int y = GetSystemMetrics(77) + 128;
            visual = CreateWindowEx(0x08000080, name, "pointer fixture visual", 0x80000000, x, y, 300, 800, 0, 0, cls.Instance, 0);
            input = CreateWindowEx(0x08000080, name, "pointer fixture input", 0x80000000, x, y, 300, 360, 0, 0, cls.Instance, 0);
            Assert.NotEqual(0, visual); Assert.NotEqual(0, input);
            nint region = CreateRectRgn(0, 0, 300, 48);
            Assert.NotEqual(0, region);
            if (SetWindowRgn(input, region, false) == 0) { DeleteObject(region); Assert.Fail("SetWindowRgn failed"); }
            ShowWindow(visual, 4); ShowWindow(input, 4); // SW_SHOWNOACTIVATE
            Assert.True(GetWindowRect(input, out var inputRect));
            var bounds = new DesktopPixelRect(inputRect.Left, inputRect.Top, 300, 360);
            var title = new PointerScreenPoint(bounds.X + 40, bounds.Y + 24);
            var blank = new PointerScreenPoint(bounds.X + 40, bounds.Y + 200);
            var native = new NativeDesktopPointerPlatform();
            Assert.True(native.IsSurfaceCurrent(visual, input, bounds));
            Assert.False(native.IsSurfaceCurrent(visual, input, bounds with { Height = 800 }));
            Assert.True(native.CoversPoint(input, title));
            Assert.False(native.CoversPoint(input, blank));
            Assert.Equal(input, native.ReceiverRoot(title));
            Assert.Equal(visual, native.ReceiverRoot(blank));

            blocker = CreateWindowEx(0x08000080, name, "pointer fixture disabled blocker", 0x80000000,
                bounds.X, bounds.Y, 300, 360, 0, 0, cls.Instance, 0);
            Assert.NotEqual(0, blocker); ShowWindow(blocker, 4); EnableWindow(blocker, false);
            Assert.True(native.CoversPoint(blocker, title));
            Assert.False(IsWindowEnabled(blocker));
            nint actualReceiver = native.ReceiverRoot(title);
            // Physical-point lookup can report the disabled window on this Windows build;
            // do not impose WindowFromPoint's documented skip rule on another API.
            Assert.Contains(actualReceiver, new[] { blocker, input });
            output.WriteLine($"DisabledOccluderReturned={actualReceiver == blocker}; RegionOccludes=True");
            output.WriteLine($"CursorProbe={native.TryReadCursor(out _)}; ProtectionProbe={native.TryReadProtection(out _)}");
            var before = native.ReadWindowOrder()!.Where(h => h == visual || h == input || h == blocker).ToArray();
            Assert.Equal(3, before.Length);
            for (int i = 0; i < 20; i++)
            {
                _ = native.ReceiverRoot(title); _ = native.CoversPoint(input, blank);
                Assert.True(native.IsSurfaceCurrent(visual, input, bounds));
            }
            Assert.Equal(before, native.ReadWindowOrder()!.Where(h => h == visual || h == input || h == blocker));
            Assert.True(GetWindowRect(input, out var after)); Assert.Equal(inputRect, after);
            Assert.False(native.CoversPoint(input, blank)); // no region mutation
            Assert.Equal(foreground, GetForegroundWindow());
        }
        finally
        {
            if (blocker != 0) DestroyWindow(blocker);
            if (input != 0) DestroyWindow(input);
            if (visual != 0) DestroyWindow(visual);
            UnregisterClass(name, cls.Instance);
            if (context != 0) SetThreadDpiAwarenessContext(context);
            GC.KeepAlive(callback);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Size, Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName, ClassName;
        public nint SmallIcon;
    }
    private delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WindowClass cls);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] private static extern nint DefWindowProc(nint hwnd, uint msg, nuint w, nint l);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint hwnd, bool enable);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
}
