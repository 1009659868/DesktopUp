using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentWheelScrollPolicyTests
{
    private static (ContentLayoutSnapshot Snapshot, ContentLayoutViewport Viewport, ContentLayoutDocument Document) Basis(
        ContentFlowAxis axis, bool loop = true, int count = 64)
    {
        var document = ContentLayoutSamples.Equal("wheel", count) with { FlowAxis = axis, LoopScroll = loop };
        var viewport = new ContentLayoutViewport(280, 220);
        return (ContentLayoutSolver.Resolve(document, viewport), viewport, document);
    }

    private static double Along(ContentBrowseOffsets offsets, ContentFlowAxis axis) =>
        axis == ContentFlowAxis.Vertical ? offsets.ScrollYDip : offsets.ScrollXDip;

    private static ContentLayoutViewport At(ContentLayoutViewport view, ContentBrowseOffsets offsets) =>
        view with { ScrollXDip = offsets.ScrollXDip, ScrollYDip = offsets.ScrollYDip };

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void UpFromHeadRevealsTailAndOppositeNotchReturnsToHead(ContentFlowAxis axis)
    {
        var (snapshot, viewport, document) = Basis(axis);
        string author = ContentLayoutPolicy.Serialize(document);
        double pitch = snapshot.CellDip + snapshot.GapDip;
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);
        Assert.True(ContentLayoutFlow.IsLooping(snapshot, viewport));
        var up = ContentWheelScrollPolicy.Advance(snapshot, viewport, 120);
        Assert.Equal(period - pitch, Along(up, axis), 8);
        var scrolled = ContentLayoutSolver.Reclip(snapshot, At(viewport, up));
        var tiles = ContentLayoutFlow.ProjectTiles(scrolled, At(viewport, up));
        Assert.Contains(tiles, t => t.ItemId == snapshot.Items[^1].ItemId && t.LoopIndex == 0);
        Assert.Contains(tiles, t => t.ItemId == snapshot.Items[0].ItemId && t.LoopIndex == 1);
        Assert.Equal(0, Along(ContentWheelScrollPolicy.Advance(scrolled, At(viewport, up), -120), axis), 8);
        Assert.Equal(author, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void SmallDeltasAccumulateAndReverseWithoutLosingPhase(ContentFlowAxis axis)
    {
        var (snapshot, viewport, _) = Basis(axis);
        var current = new ContentBrowseOffsets();
        for (int i = 0; i < 6; i++) current = ContentWheelScrollPolicy.Advance(snapshot, At(viewport, current), 20);
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);
        Assert.Equal(period - snapshot.CellDip - snapshot.GapDip, Along(current, axis), 7);
        for (int i = 0; i < 6; i++) current = ContentWheelScrollPolicy.Advance(snapshot, At(viewport, current), -20);
        Assert.True(Math.Min(Along(current, axis), period - Along(current, axis)) < 1e-7);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void LongSignedSequenceRetainsFractionalPhaseAndSingleIdentities(ContentFlowAxis axis)
    {
        var (snapshot, viewport, _) = Basis(axis);
        var offset = new ContentBrowseOffsets();
        long total = 0;
        int[] deltas = [120, 20, -30, 240, -360, 1, -1];
        double pitch = snapshot.CellDip + snapshot.GapDip, period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);
        for (int i = 0; i < 1000; i++)
        {
            int delta = deltas[i % deltas.Length]; total += delta;
            offset = ContentWheelScrollPolicy.Advance(snapshot, At(viewport, offset), delta);
            double expected = ((-total / 120d * pitch) % period + period) % period;
            Assert.Equal(expected, Along(offset, axis), 6);
            var scrolled = ContentLayoutSolver.Reclip(snapshot, At(viewport, offset));
            var tiles = ContentLayoutFlow.ProjectTiles(scrolled, At(viewport, offset));
            Assert.Equal(tiles.Count, tiles.Select(t => (t.ItemId, t.LoopIndex)).Distinct().Count());
            Assert.All(tiles, t => Assert.Contains(snapshot.Items, item => item.ItemId == t.ItemId && item.SourceIndex == t.SourceIndex));
        }
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void FiniteAndDisabledLoopClampAtBothEnds(ContentFlowAxis axis)
    {
        var (snapshot, viewport, _) = Basis(axis, loop: false);
        Assert.Equal(0, Along(ContentWheelScrollPolicy.Advance(snapshot, viewport, 120), axis));
        var tail = ContentWheelScrollPolicy.Advance(snapshot, viewport, -32760);
        double maximum = axis == ContentFlowAxis.Vertical ? snapshot.ExtentHeightDip - viewport.HeightDip : snapshot.ExtentWidthDip - viewport.WidthDip;
        Assert.Equal(maximum, Along(tail, axis));
        var loop = snapshot with { LoopScroll = true };
        var wrapped = ContentWheelScrollPolicy.Advance(loop, viewport, 120);
        Assert.Equal(maximum, Along(ContentWheelScrollPolicy.Advance(snapshot, At(viewport, wrapped), 0), axis));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void EmptyAndShortContentNeverAcquireLoopOffset(ContentFlowAxis axis)
    {
        foreach (int count in new[] { 0, 1 })
        {
            var (snapshot, viewport, _) = Basis(axis, count: count);
            Assert.False(ContentLayoutFlow.IsLooping(snapshot, viewport));
            foreach (int delta in new[] { 120, -120, 20, -20 })
                Assert.Equal(new ContentBrowseOffsets(), ContentWheelScrollPolicy.Advance(snapshot, viewport, delta));
        }
    }
}
