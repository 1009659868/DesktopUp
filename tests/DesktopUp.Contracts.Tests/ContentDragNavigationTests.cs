using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentDragNavigationTests
{
    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void CA01_actual_copy_70_to_95_retains_the_ten_DIP_grab_point(ContentFlowAxis axis)
    {
        var (snapshot, view) = Scene(axis);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).Single(t => t.ItemId == "p:head");
        Assert.Equal(1, tile.LoopIndex);
        var nav = ContentDragNavigation.Begin(snapshot, view, tile,
            axis == ContentFlowAxis.Vertical ? 10 : 80, axis == ContentFlowAxis.Vertical ? 80 : 10);
        var ghost = nav.Ghost(axis == ContentFlowAxis.Vertical ? 0 : 25, axis == ContentFlowAxis.Vertical ? 25 : 0);
        Assert.Equal(95, axis == ContentFlowAxis.Vertical ? ghost.Y : ghost.X);
        var anchor = nav.EndAnchor(axis == ContentFlowAxis.Vertical ? 0 : 25, axis == ContentFlowAxis.Vertical ? 25 : 0);
        Assert.Equal(10, axis == ContentFlowAxis.Vertical ? anchor.InsideYDip : anchor.InsideXDip);
        Assert.Equal(105, axis == ContentFlowAxis.Vertical ? anchor.ViewportYDip : anchor.ViewportXDip);
        // Cancel back to authoring: follow the grabbed head, not the still-visible tail.
        var offsets = ContentLayoutFlow.RestoreAnchor(snapshot, view, anchor);
        Assert.Equal(925, axis == ContentFlowAxis.Vertical ? offsets.ScrollYDip : offsets.ScrollXDip);
        var restored = ContentLayoutSolver.Reclip(snapshot, view with { ScrollXDip = offsets.ScrollXDip, ScrollYDip = offsets.ScrollYDip });
        Assert.Equal(95, Along(ContentLayoutFlow.ProjectTiles(restored, view).Single(t => t.ItemId == "p:head").Bounds, axis));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void CA02_navigation_keeps_unwrapped_displacement_across_forward_and_reverse_seams(ContentFlowAxis axis)
    {
        var (snapshot, view) = Scene(axis);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).Single(t => t.ItemId == "p:head");
        var nav = ContentDragNavigation.Begin(snapshot, view, tile, tile.Clip.X + 10, tile.Clip.Y + 10);
        var forward = nav.Advance(80);
        Assert.Equal(80, forward.TravelDip);
        Assert.Equal(30, Along(forward.ProjectOffsets(snapshot, view), axis));
        var reverse = forward.Advance(-120);
        Assert.Equal(-40, reverse.TravelDip);
        Assert.Equal(910, Along(reverse.ProjectOffsets(snapshot, view), axis));
        Assert.Equal(70, Along(reverse.Ghost(0, 0), axis));
        for (int i = 0; i < 1000; i++) nav = nav.Advance(7.25).Advance(-7.25);
        Assert.Equal(0, nav.TravelDip);
        Assert.Equal(950, Along(nav.ProjectOffsets(snapshot, view), axis));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void CA07_finite_end_anchor_clamps_only_as_necessary_and_uses_latest_members(ContentFlowAxis axis)
    {
        var (snapshot, view) = Scene(axis);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).Single(t => t.ItemId == "p:head");
        var nav = ContentDragNavigation.Begin(snapshot, view, tile, tile.Clip.X + 10, tile.Clip.Y + 10);
        var anchor = nav.EndAnchor(0, 0);
        var finite = snapshot with { LoopScroll = false };
        var restored = ContentLayoutFlow.RestoreAnchor(finite, view, anchor);
        Assert.Equal(0, Along(restored, axis));
        var changed = finite with { Items = finite.Items.Where(t => t.ItemId != "p:head").ToArray() };
        restored = ContentLayoutFlow.RestoreAnchor(changed, view, anchor);
        Assert.Equal(680, Along(restored, axis));
        Assert.DoesNotContain(changed.Items, t => t.ItemId == "p:head");
    }

    [Fact]
    public void Finite_navigation_stops_at_edges_without_accumulating_rejected_distance()
    {
        var (snapshot, view) = Scene(ContentFlowAxis.Vertical);
        view = view with { ScrollYDip = 640 };
        snapshot = ContentLayoutSolver.Reclip(snapshot with { LoopScroll = false }, view);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).Single();
        var nav = ContentDragNavigation.Begin(snapshot, view, tile, tile.Clip.X + 1, tile.Clip.Y + 1);
        nav = nav.Advance(1000);
        Assert.Equal(40, nav.TravelDip);
        Assert.Equal(680, nav.ProjectOffsets(snapshot, view).ScrollYDip);
        nav = nav.Advance(-8);
        Assert.Equal(672, nav.ProjectOffsets(snapshot, view).ScrollYDip);
    }

    [Fact]
    public void Invalid_capture_and_unbounded_navigation_fail_without_mutating_the_original_session()
    {
        var (snapshot, view) = Scene(ContentFlowAxis.Vertical);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).First();
        Assert.Throws<ContentLayoutException>(() => ContentDragNavigation.Begin(snapshot, view, tile with { LoopIndex = 4 }, 10, 80));
        var nav = ContentDragNavigation.Begin(snapshot, view, tile, tile.Clip.X + 1, tile.Clip.Y + 1);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, 16_000_001 })
        {
            Assert.Throws<ContentLayoutException>(() => nav.Advance(invalid));
            Assert.Throws<ContentLayoutException>(() => nav.Ghost(invalid, 0));
        }
        Assert.Equal(0, nav.TravelDip);
        var edge = nav.EndAnchor(-1000, 1000);
        Assert.Equal(0, edge.ViewportXDip);
        Assert.InRange(edge.ViewportYDip, 0, Math.BitDecrement(view.HeightDip));
    }

    private static double Along(ContentRect r, ContentFlowAxis axis) => axis == ContentFlowAxis.Vertical ? r.Y : r.X;
    private static double Along(ContentBrowseOffsets o, ContentFlowAxis axis) => axis == ContentFlowAxis.Vertical ? o.ScrollYDip : o.ScrollXDip;
    private static (ContentLayoutSnapshot Snapshot, ContentLayoutViewport View) Scene(ContentFlowAxis axis)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        var view = new ContentLayoutViewport(vertical ? 240 : 300, vertical ? 300 : 240,
            ScrollXDip: vertical ? 0 : 950, ScrollYDip: vertical ? 950 : 0, Generation: 7);
        ContentRect Rect(double main, double size) => vertical ? new(0, main, 80, size) : new(main, 0, size, 80);
        var snapshot = new ContentLayoutSnapshot("p", 1, 1, 7, 1, 5, 24, 20,
            vertical ? 240 : 980, vertical ? 980 : 240, view.ScrollXDip, view.ScrollYDip,
            [new("p:head", 0, 0, 0, 0, 2, 2, Rect(20, 80), default),
             new("p:tail", 1, 1, 0, 0, 2, 2, Rect(900, 80), default)], [])
            { FlowAxis = axis, LoopScroll = true };
        return (ContentLayoutSolver.Reclip(snapshot, view), view);
    }
}
