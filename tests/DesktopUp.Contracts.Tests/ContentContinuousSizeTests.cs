using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentContinuousSizeTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Fractional_size_roundtrips_and_paints_exactly_while_reserving_safe_grid_cells(ContentFlowAxis axis)
    {
        var document = ContentLayoutSamples.Equal("A", 3) with { FlowAxis = axis };
        var viewport = new ContentLayoutViewport(500, 500);
        var edited = ContentLayoutEditing.Apply(document, new(ContentLayoutEditKind.Resize, document.Placements[0].ItemId)
            { ColumnSpan = 3.125, RowSpan = 2.875 }, viewport);
        Assert.True(edited.Accepted, edited.Error);
        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(edited.Document), "A", out var reopened, out var error), error);
        Assert.True(ContentLayoutEditing.SameState(edited.Document, reopened!));
        var snapshot = ContentLayoutSolver.Resolve(reopened!, viewport);
        Assert.Equal(87.75, snapshot.Items[0].Bounds.Width);
        Assert.Equal(80.25, snapshot.Items[0].Bounds.Height);
        ContentLayoutSolverTests.Safe(snapshot);
        var manual = ContentLayoutEditing.Apply(reopened!, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }, viewport);
        Assert.True(manual.Accepted, manual.Error);
        var restored = ContentLayoutEditing.Apply(manual.Document, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic }, viewport);
        Assert.True(restored.Accepted, restored.Error);
        Assert.Equal(reopened!.Placements[0].ColumnSpan, restored.Document.Placements[0].ColumnSpan);
        var history = new ContentLayoutHistory();
        history.Record(document, edited.Document);
        Assert.Equal(4, history.ProposeUndo(edited.Document)!.Document.Placements[0].ColumnSpan);
    }

    [Fact]
    public void Small_drag_is_a_persistent_size_change_not_an_animation_or_grid_jump()
    {
        var document = ContentLayoutSamples.Equal("A", 1);
        var viewport = new ContentLayoutViewport(600, 600);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, viewport).Single();
        var drag = ContentDragPolicy.Map(document, snapshot, viewport, tile, ContentDragKind.Resize, 1.5, 2.25);
        Assert.True(drag.Accepted, drag.Error);
        Assert.Equal(4.05, drag.Edit!.ColumnSpan);
        Assert.Equal(4.075, drag.Edit.RowSpan);
        var edited = ContentLayoutEditing.Apply(document, drag.Edit, viewport, snapshot);
        Assert.True(edited.Accepted, edited.Error);
        Assert.Equal(115.5, edited.PreviewSnapshot!.Items[0].Bounds.Width, 8);
        Assert.True(ContentDragPolicy.Map(document, snapshot, viewport, tile, ContentDragKind.Move, 1.5, 2.25).NoChange);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Poster_cross_axis_minimum_tracks_largest_effective_item_and_chrome(ContentFlowAxis axis)
    {
        var document = ContentLayoutSamples.Equal("A", 2) with { FlowAxis = axis };
        var small = ContentLayoutSolver.Resolve(document, new(800, 800, Scale: .4));
        var large = ContentLayoutSolver.Resolve(document, new(800, 800, Scale: 3));
        var smallFrame = ContentFrameMinimumPolicy.FromSnapshot(small, 34, 70);
        var largeFrame = ContentFrameMinimumPolicy.FromSnapshot(large, 34, 70);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 96 : 240, smallFrame.WidthDip);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 240 : 126.4, smallFrame.HeightDip, 8);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 340 : 240, largeFrame.WidthDip);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 240 : 376, largeFrame.HeightDip);
        var minimumViewport = axis == ContentFlowAxis.Vertical
            ? new ContentLayoutViewport(small.MinimumCrossAxisDip, 800, Scale: .4)
            : new ContentLayoutViewport(800, small.MinimumCrossAxisDip, Scale: .4);
        var atMinimum = ContentLayoutSolver.Resolve(document, minimumViewport);
        Assert.False(atMinimum.IsLayoutReadOnly);
        Assert.All(atMinimum.Items, p => Assert.Equal(0, axis == ContentFlowAxis.Vertical ? p.Column : p.Row));
    }

    [Fact]
    public void Old_integer_documents_remain_readable_but_do_not_claim_fractional_schema_support()
    {
        var document = ContentLayoutSamples.Equal("A", 1) with { SchemaVersion = 3 };
        Assert.Null(ContentLayoutPolicy.Validate(document));
        Assert.NotNull(ContentLayoutPolicy.Validate(document with { Placements = [document.Placements[0] with { ColumnSpan = 4.125 }] }));
        Assert.NotNull(ContentLayoutPolicy.Validate(document with { SchemaVersion = 4,
            Placements = [document.Placements[0] with { ColumnSpan = double.NaN }] }));
    }
}
