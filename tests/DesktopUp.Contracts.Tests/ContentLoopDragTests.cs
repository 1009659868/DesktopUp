using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLoopDragTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 0)] [InlineData(ContentFlowAxis.Vertical, 1)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 0)] [InlineData(ContentFlowAxis.HorizontalLoop, 1)]
    public void First_press_on_each_actual_seam_copy_accepts_a_direct_drag(ContentFlowAxis axis, int cycle)
    {
        var (doc, view, snapshot) = Scene(axis);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).First(t => t.LoopIndex == cycle);
        var result = ContentDragPolicy.Map(doc, snapshot, view, tile, ContentDragKind.Move,
            axis == ContentFlowAxis.Vertical ? 96 : 0, axis == ContentFlowAxis.Vertical ? 0 : 96);
        Assert.True(result.Accepted, result.Error);
        Assert.True(doc.LoopScroll);
        Assert.All(doc.Placements, p => Assert.Null(p.PinnedIndex));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, ContentLayoutMode.Automatic)]
    [InlineData(ContentFlowAxis.HorizontalLoop, ContentLayoutMode.Automatic)]
    [InlineData(ContentFlowAxis.Vertical, ContentLayoutMode.Manual)]
    [InlineData(ContentFlowAxis.HorizontalLoop, ContentLayoutMode.Manual)]
    public void Whole_cycles_of_navigation_do_not_author_a_new_identity_pin_or_position(ContentFlowAxis axis, ContentLayoutMode mode)
    {
        var (doc, view, snapshot) = Scene(axis, mode);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).First(t => t.LoopIndex == 1);
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, view);
        foreach (double delta in new[] { -3 * period, -period, 0, period, 3 * period })
        {
            var result = ContentDragPolicy.Map(doc, snapshot, view, tile, ContentDragKind.Move, 0, 0,
                axis == ContentFlowAxis.Vertical ? 0 : delta, axis == ContentFlowAxis.Vertical ? delta : 0);
            Assert.True(result.Accepted, result.Error);
            Assert.True(result.NoChange);
        }
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Manual_drop_removes_the_visible_copy_period_before_saving_cells(ContentFlowAxis axis)
    {
        var (doc, view, snapshot) = Scene(axis, ContentLayoutMode.Manual);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, view).First(t => t.LoopIndex == 1);
        var slot = snapshot.Items.Single(t => t.ItemId == tile.ItemId);
        var result = ContentDragPolicy.Map(doc, snapshot, view, tile, ContentDragKind.Move,
            axis == ContentFlowAxis.Vertical ? 0 : 48, axis == ContentFlowAxis.Vertical ? 48 : 0);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(slot.Column + (axis == ContentFlowAxis.Vertical ? 0 : 1), result.Edit!.Column);
        Assert.Equal(slot.Row + (axis == ContentFlowAxis.Vertical ? 1 : 0), result.Edit.Row);
        var edited = ContentLayoutEditing.Apply(doc, result.Edit, view, snapshot);
        Assert.True(edited.Accepted, edited.Error);
        Assert.True(edited.Document.LoopScroll);
        Assert.Equal(doc.Placements.Count, edited.Document.Placements.Select(p => p.ItemId).Distinct().Count());
    }

    private static (ContentLayoutDocument Doc, ContentLayoutViewport View, ContentLayoutSnapshot Snapshot) Scene(
        ContentFlowAxis axis, ContentLayoutMode mode = ContentLayoutMode.Automatic)
    {
        var items = Enumerable.Range(0, 9).Select(i => new ContentPlacement("loop:item-" + i, i, 2, 2)
        {
            Column = mode == ContentLayoutMode.Manual ? (axis == ContentFlowAxis.Vertical ? 0 : i * 3) : null,
            Row = mode == ContentLayoutMode.Manual ? (axis == ContentFlowAxis.Vertical ? i * 3 : 0) : null
        }).ToArray();
        var doc = new ContentLayoutDocument("loop", items)
        { Mode = mode, FlowAxis = axis, LoopScroll = true, BaseCellDip = 48, BaseGapDip = 0, MaximumColumns = 8 };
        var view = axis == ContentFlowAxis.Vertical ? new ContentLayoutViewport(288, 144, Generation: 9)
            : new ContentLayoutViewport(144, 288, Generation: 9);
        var snapshot = ContentLayoutSolver.Resolve(doc, view);
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, view);
        view = axis == ContentFlowAxis.Vertical ? view with { ScrollYDip = period - 48 } : view with { ScrollXDip = period - 48 };
        return (doc, view, ContentLayoutSolver.Resolve(doc, view));
    }
}
