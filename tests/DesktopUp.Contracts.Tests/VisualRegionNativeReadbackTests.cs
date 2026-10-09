using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class VisualRegionNativeReadbackTests
{
    [Theory]
    [InlineData(60, 0)]
    [InlineData(60, 8)]
    [InlineData(150, 8)]
    [InlineData(360, 8)]
    public void Direct_read_keeps_capacity_and_installed_visible_region_distinct(int height, int radius)
    {
        WithWindow((window, surface) =>
        {
            var api = new DesktopWindowNativeApi();
            Assert.True(api.ApplyRoundedRegion(window, 300, height, radius));
            var visible = surface with { Height = height };
            for (int i = 0; i < 20; i++)
            {
                var read = api.InspectVisualSurface(window, surface, visible);
                Assert.True(read.Verified, read.Failure);
                Assert.Equal("GetWindowRgnBox", read.RegionReadMethod);
                Assert.Equal(new DesktopPixelRect(0, 0, 300, height), read.Region);
                Assert.Equal(surface, read.Window);
            }
        });
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("empty")]
    [InlineData("offset")]
    [InlineData("wrong-height")]
    [InlineData("wrong-window")]
    public void Actual_native_defects_still_fail_closed_without_installing_or_repairing_anything(string kind)
    {
        WithWindow((window, surface) =>
        {
            var api = new DesktopWindowNativeApi();
            Assert.True(api.ApplyRoundedRegion(window, 300, 60, 8));
            if (kind == "absent") Assert.NotEqual(0, SetWindowRgn(window, 0, false));
            if (kind == "empty") Install(window, CreateRectRgn(0, 0, 0, 0));
            if (kind == "offset") Install(window, CreateRectRgn(1, 0, 300, 60));
            int before = GetWindowRgnBox(window, out var beforeBox);
            var visible = surface with { Height = kind == "wrong-height" ? 61 : 60 };
            var expected = kind == "wrong-window" ? surface with { X = surface.X + 1 } : surface;
            var result = api.InspectVisualSurface(window, expected, visible);
            Assert.False(result.Verified);
            Assert.Equal(before, GetWindowRgnBox(window, out var afterBox));
            Assert.Equal(beforeBox, afterBox); // Verification may not mutate missing/wrong HRGN into a pass.
            if (kind == "absent") Assert.Equal("window-region-read-failed", result.Failure);
        });
    }

    [Fact]
    public void Failed_region_copy_is_not_proof_that_the_installed_region_is_absent()
    {
        WithWindow((window, surface) =>
        {
            var api = new DesktopWindowNativeApi();
            Assert.True(api.ApplyRoundedRegion(window, 300, 60, 8));
            // Deterministic COPY-API fault (invalid destination), not a claim about the
            // cause of Windows' intermittent failure with a freshly allocated destination.
            Assert.Equal(0, GetWindowRgn(window, 0));
            Assert.Equal(3, GetWindowRgnBox(window, out var box));
            Assert.Equal(new Rect(0, 0, 300, 60), box);
            Assert.True(api.InspectVisualSurface(window, surface, surface with { Height = 60 }).Verified);
        });
    }

    [Fact]
    public void Geometry_verification_uses_one_direct_query_not_a_temporary_region_copy_or_retry()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.NotNull(root);
        string source = File.ReadAllText(Path.Combine(root.FullName, "src/DesktopUp.Windows/DesktopLayerAttachment.cs"));
        string reader = source.Split("internal sealed class DesktopWindowNativeApi")[1]
            .Split("public DesktopVisualSurfaceReadback InspectVisualSurface")[1].Split("private static DesktopPixelRect Pixels")[0];
        Assert.Contains("GetWindowRgnBox(window", reader);
        Assert.DoesNotContain("CreateRectRgn", reader);
        Assert.DoesNotContain("GetWindowRgn(window", reader);
        Assert.DoesNotContain("SetWindowRgn(", reader);
        Assert.Equal(2, reader.Split("GetWindowRgnBox(window").Length);
        string script = File.ReadAllText(Path.Combine(root.FullName, "scripts/smoke-auto-lifetime.ps1"));
        Assert.Contains("if ($Mode -ne 'continuous')", script);
    }

    private static void WithWindow(Action<nint, DesktopPixelRect> check)
    {
        nint context = SetThreadDpiAwarenessContext(new(-4));
        var surface = new DesktopPixelRect(GetSystemMetrics(76) + GetSystemMetrics(78) + 400, 200, 300, 360);
        nint window = CreateWindowEx(0x08000080, "STATIC", "region-readback-fixture", 0x80000000,
            surface.X, surface.Y, surface.Width, surface.Height, 0, 0, 0, 0);
        try { Assert.NotEqual(0, window); check(window, surface); }
        finally
        {
            if (window != 0) Assert.True(DestroyWindow(window));
            if (context != 0) _ = SetThreadDpiAwarenessContext(context);
        }
    }
    private static void Install(nint window, nint region)
    {
        Assert.NotEqual(0, region);
        if (SetWindowRgn(window, region, false) != 0) return;
        _ = DeleteObject(region); Assert.Fail("Could not install fixture region.");
    }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Rect(int Left, int Top, int Right, int Bottom);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("user32.dll")] private static extern int GetWindowRgnBox(nint window, out Rect box);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint region);
}
