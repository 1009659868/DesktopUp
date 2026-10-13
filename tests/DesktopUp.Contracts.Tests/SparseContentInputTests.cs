using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class SparseContentInputTests
{
    private static readonly PosterInputSessionKey Front = new("front", 1), Back = new("back", 1);
    private static readonly DesktopPixelRect Frame = new(0, 0, 300, 400);
    private static PosterInputRegion Item(DesktopPixelRect bounds) => new(PosterInputTargetKind.ContentItem, 0, bounds)
        { ContentItemId = "item", ContentSnapshotGeneration = 1 };
    private static PosterInputLayoutSnapshot Layout(bool editing = false) => new(1, 300, 400,
        new PosterInputRegion[] {
            new(PosterInputTargetKind.TitleBar, -1, new(0, 0, 300, 40)),
            Item(new(20, 80, 100, 100)),
            new(PosterInputTargetKind.ContentViewport, -1, new(0, 60, 300, 300)) { ContentSnapshotGeneration = 1 }
        }.Concat(editing ? new[] { new PosterInputRegion(PosterInputTargetKind.EditSurface, -1, Frame) } : []).ToArray(), 8);

    [Fact]
    public void EmptyViewportOwnsMarqueeWhileItemsAndUpperPosterKeepPriority()
    {
        var router = new PosterInputRouter(); router.Register(Front); router.Register(Back);
        Assert.True(router.Publish(Front, Frame, Layout()));
        Assert.True(router.Publish(Back, Frame, new(1, 300, 400, [Item(new(180, 220, 100, 100))])));
        Assert.Equal(Front, router.Resolve(50, 100, [Front, Back])!.Session);
        var blank=router.Resolve(200, 240, [Front, Back])!;
        Assert.Equal(PosterInputTargetKind.ContentViewport,blank.Region.Kind);
        var capture=router.Begin(blank);Assert.NotNull(capture);router.End(capture);
        Assert.Equal(Back, router.Resolve(200, 240, [Back, Front])!.Session);
        router.Invalidate(Front);
        Assert.Equal(Back, router.Resolve(200, 240, [Front, Back])!.Session);
        Assert.Null(router.Resolve(150, 320, [Front, Back]));
        var viewport = Layout().Regions.Single(r => r.Kind == PosterInputTargetKind.ContentViewport);
        Assert.Null(router.Begin(new(Front, 1, Frame, viewport)));
        Assert.Null(router.Capture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualGdiRegionOwnsWholeFrameIndependentlyOfEditingAndMaterial(bool editing)
    {
        nint region = PosterInputIslandManager.BuildWindowRegion(Layout(editing));
        Assert.NotEqual(0, region);
        try
        {
            Assert.True(PtInRegion(region, 50, 20));
            Assert.True(PtInRegion(region, 50, 100));
            Assert.True(PtInRegion(region, 150, 100)); // between cards
            Assert.True(PtInRegion(region, 250, 320)); // empty content body
            Assert.True(PtInRegion(region, 150, 50)); // decorative heading gap
            Assert.True(PtInRegion(region, 0, 0)); // visual corners do not alter input ownership
            Assert.False(PtInRegion(region, 300, 200));
            Assert.False(PtInRegion(region, 150, 400));
        }
        finally { Assert.True(DeleteObject(region)); }
    }

    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);
}
