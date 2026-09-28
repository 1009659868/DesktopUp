using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentDragPolicyTests
{
    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Total_motion_and_scroll_map_from_the_frozen_local_slot_in_both_axes(ContentFlowAxis axis)
    {
        var document = Document(ContentLayoutMode.Manual, axis, 1);
        document = document with { Placements = [document.Placements[0] with { Column = 2, Row = 2, ManualSize = true }] };
        var viewport = View(axis, 144);
        var capture = Capture(document, viewport);
        var result = Map(capture, ContentDragKind.Move, 48, 48,
            axis == ContentFlowAxis.HorizontalLoop ? 48 : 0, axis == ContentFlowAxis.Vertical ? 48 : 0);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 3 : 4, result.Edit!.Column);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 4 : 3, result.Edit.Row);
        Assert.Null(result.Edit.TargetReadOrder);
        Assert.Equal(2, document.Placements[0].Column); Assert.Equal(2, document.Placements[0].Row);
    }

    [Fact]
    public void Manual_region_origins_are_removed_once_and_never_persisted_as_local_coordinates()
    {
        var document = Document(ContentLayoutMode.Manual, ContentFlowAxis.Vertical, 1) with
        { Regions = [new("main", 0, 2, 4, 3)] };
        document = document with { Placements = [document.Placements[0] with { Column = 1, Row = 2, ManualSize = true }] };
        var capture = Capture(document, new(384, 480, Generation: 9));
        var result = Map(capture, ContentDragKind.Move, 48, 48);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(2, result.Edit!.Column); Assert.Equal(3, result.Edit.Row);
        Assert.Equal(3, capture.Tile.Bounds.X / 48); Assert.Equal(5, capture.Tile.Bounds.Y / 48);
    }

    [Theory]
    [InlineData(-48, 0)] [InlineData(0, -48)] [InlineData(240, 0)]
    public void Invalid_manual_drop_is_rejected_instead_of_clamped_to_an_edge(double x, double y)
    {
        var document = Document(ContentLayoutMode.Manual, ContentFlowAxis.Vertical, 1);
        document = document with { Placements = [document.Placements[0] with { Column = 0, Row = 0 }] };
        var result = Map(Capture(document, View(document.FlowAxis)), ContentDragKind.Move, x, y);
        Assert.False(result.Accepted); Assert.Null(result.Edit); Assert.NotNull(result.Error);
    }

    [Fact]
    public void A_horizontal_manual_drop_keeps_long_main_axis_coordinates()
    {
        var document = Document(ContentLayoutMode.Manual, ContentFlowAxis.HorizontalLoop, 1);
        document = document with { Placements = [document.Placements[0] with { Column = 100, Row = 0 }] };
        var viewport = new ContentLayoutViewport(144, 288, ScrollXDip: 4704, Generation: 9);
        var result = Map(Capture(document, viewport), ContentDragKind.Move, 48, 0);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(101, result.Edit!.Column); Assert.Equal(0, result.Edit.Row);
    }

    [Theory] [InlineData(ContentDragKind.Move)] [InlineData(ContentDragKind.Resize)]
    public void Subcell_motion_and_return_to_origin_are_noops_even_after_a_valid_candidate(ContentDragKind kind)
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical), View(ContentFlowAxis.Vertical));
        Assert.True(Map(capture, kind, 20, -20).NoChange);
        Assert.True(Map(capture, kind, 96, 0).Accepted);
        Assert.True(Map(capture, kind, 0, 0).NoChange);
        Assert.Null(capture.Document.Placements[0].PinnedIndex);
        Assert.False(capture.Document.Placements[0].ManualSize);
    }

    [Fact]
    public void Resize_starts_at_effective_spans_and_zero_motion_does_not_author_a_heat_override()
    {
        var author = Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 1);
        var effective = author with { Placements = [author.Placements[0] with { ColumnSpan = 4, RowSpan = 3 }] };
        var viewport = new ContentLayoutViewport(384, 384, Generation: 9);
        var snapshot = ContentLayoutSolver.Resolve(effective, viewport);
        var capture = new Press(author, snapshot, viewport, ContentLayoutFlow.ProjectTiles(snapshot, viewport).Single());
        Assert.True(Map(capture, ContentDragKind.Resize, 0, 0).NoChange);
        var result = Map(capture, ContentDragKind.Resize, 48, 0);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(5, result.Edit!.ColumnSpan); Assert.Equal(3, result.Edit.RowSpan);
        Assert.Equal(2, author.Placements[0].ColumnSpan);
    }

    [Theory] [InlineData(-96)] [InlineData(1536)]
    public void Resize_rejects_zero_or_oversized_spans_without_clamping(double delta)
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 1), View(ContentFlowAxis.Vertical));
        var result = Map(capture, ContentDragKind.Resize, delta, 0);
        Assert.False(result.Accepted); Assert.Null(result.Edit);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void An_automatic_card_at_the_cross_edge_may_resize_and_reflow(ContentFlowAxis axis)
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, axis), View(axis), 2);
        var result = Map(capture, ContentDragKind.Resize, axis == ContentFlowAxis.Vertical ? 48 : 0,
            axis == ContentFlowAxis.Vertical ? 0 : 48);
        Assert.True(result.Accepted, result.Error);
        var edited = ContentLayoutEditing.Apply(capture.Document, result.Edit!, capture.Viewport, capture.Snapshot);
        Assert.True(edited.Accepted, edited.Error);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Automatic_before_after_insertion_removes_the_source_once_and_is_axis_symmetric(ContentFlowAxis axis)
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, axis), View(axis));
        var withinBand = Map(capture, ContentDragKind.Move, axis == ContentFlowAxis.Vertical ? 96 : 0,
            axis == ContentFlowAxis.Vertical ? 0 : 96);
        Assert.Equal(1, withinBand.Edit!.TargetReadOrder);
        var nextBand = Map(capture, ContentDragKind.Move, axis == ContentFlowAxis.Vertical ? 0 : 96,
            axis == ContentFlowAxis.Vertical ? 96 : 0);
        Assert.Equal(3, nextBand.Edit!.TargetReadOrder);
        Assert.Null(nextBand.Edit.Column); Assert.Null(nextBand.Edit.Row);
        var returnToOriginalInsertion = Map(capture, ContentDragKind.Move, axis == ContentFlowAxis.Vertical ? -48 : 0,
            axis == ContentFlowAxis.Vertical ? 0 : -48);
        Assert.True(returnToOriginalInsertion.NoChange);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Main_axis_gaps_choose_the_nearest_band_and_tail_insertion_is_bounded(ContentFlowAxis axis)
    {
        var document = Document(ContentLayoutMode.Anchored, axis, 3);
        document = document with { Placements = document.Placements.Select((p, i) => p with
            { FixedPosition = i == 2, Column = i == 2 ? (axis == ContentFlowAxis.Vertical ? 0 : 6) : null,
                Row = i == 2 ? (axis == ContentFlowAxis.Vertical ? 6 : 0) : null }).ToArray() };
        var capture = Capture(document, View(axis, 480));
        // The first band ends at cell 2, the next begins at cell 6. Cell 4 is the
        // equidistant gap; the documented tie goes to the earlier band.
        var gap = Map(capture, ContentDragKind.Move, axis == ContentFlowAxis.Vertical ? 0 : 144,
            axis == ContentFlowAxis.Vertical ? 144 : 0);
        Assert.True(gap.NoChange);
        var tail = Map(capture, ContentDragKind.Move, 480, 480);
        Assert.True(tail.Accepted, tail.Error);
        Assert.Equal(2, tail.Edit!.TargetReadOrder);
        Assert.InRange(tail.Edit.TargetReadOrder!.Value, 0, document.Placements.Count - 1);
    }

    [Fact]
    public void A_multiband_card_has_one_sequence_position_and_frozen_mapping_never_chases_its_preview()
    {
        var document = Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 5);
        document = document with { Placements = document.Placements.Select((p, i) => i == 0 ? p with { ColumnSpan = 4, RowSpan = 6 } : p).ToArray() };
        var capture = Capture(document, View(document.FlowAxis, 480), 4);
        var result = Map(capture, ContentDragKind.Move, 192, -192);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(3, result.Edit!.TargetReadOrder);
        var preview = ContentLayoutEditing.Apply(document, result.Edit, capture.Viewport, capture.Snapshot);
        Assert.True(preview.Accepted, preview.Error);
        Assert.Equal(result, Map(capture, ContentDragKind.Move, 192, -192));
        Assert.True(Map(capture, ContentDragKind.Move, 0, 0).NoChange);
        var large = Capture(document, capture.Viewport);
        Assert.Equal(2, Map(large, ContentDragKind.Move, 192, 0).Edit!.TargetReadOrder);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Clipped_press_tiles_and_unwrapped_finite_scroll_keep_the_original_logical_origin(ContentFlowAxis axis)
    {
        var viewport = View(axis, 96);
        viewport = axis == ContentFlowAxis.Vertical ? viewport with { ScrollYDip = 48 } : viewport with { ScrollXDip = 48 };
        var capture = Capture(Document(ContentLayoutMode.Automatic, axis, 9), viewport);
        Assert.True(capture.Tile.Bounds.X < 0 || capture.Tile.Bounds.Y < 0);
        var result = Map(capture, ContentDragKind.Move, 0, 0,
            axis == ContentFlowAxis.HorizontalLoop ? 96 : 0, axis == ContentFlowAxis.Vertical ? 96 : 0);
        Assert.Equal(3, result.Edit!.TargetReadOrder);
    }

    [Theory] [InlineData(0)] [InlineData(1)]
    public void Every_actual_loop_copy_explicitly_waits_for_the_seam_transaction(int requestedCycle)
    {
        var document = Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 9) with { LoopScroll = true };
        var viewport = View(document.FlowAxis, 144);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);
        viewport = viewport with { ScrollYDip = period - 48 };
        snapshot = ContentLayoutSolver.Resolve(document, viewport);
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport);
        var tile = tiles.First(t => t.LoopIndex == requestedCycle);
        var result = ContentDragPolicy.Map(document, snapshot, viewport, tile, ContentDragKind.Move, 48, 0);
        Assert.False(result.Accepted); Assert.Contains("Loop", result.Error!); Assert.Null(result.Edit);
    }

    [Fact]
    public void A_short_loop_preference_uses_the_same_finite_mapping_without_inventing_copies()
    {
        var document = Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 2) with { LoopScroll = true };
        var capture = Capture(document, View(document.FlowAxis, 384));
        Assert.False(ContentLayoutFlow.IsLooping(capture.Snapshot, capture.Viewport));
        Assert.Equal(1, Map(capture, ContentDragKind.Move, 96, 0).Edit!.TargetReadOrder);
    }

    [Theory] [InlineData("stale-viewport")] [InlineData("foreign-tile")] [InlineData("forged-copy")]
    [InlineData("missing-member")] [InlineData("read-only")]
    public void A_stale_or_incomplete_press_witness_is_an_explicit_failure(string invalid)
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical), View(ContentFlowAxis.Vertical));
        capture = invalid switch
        {
            "stale-viewport" => capture with { Viewport = capture.Viewport with { Generation = 10 } },
            "foreign-tile" => capture with { Tile = capture.Tile with { ItemId = "unknown" } },
            "forged-copy" => capture with { Tile = capture.Tile with { OffsetYDip = 100, Bounds = capture.Tile.Bounds with { Y = 100 } } },
            "missing-member" => capture with { Snapshot = capture.Snapshot with { Items = capture.Snapshot.Items.Skip(1).ToArray() } },
            "read-only" => capture with { Snapshot = capture.Snapshot with { SpaceStatus = ContentLayoutSpaceStatus.LegacyConstraintConflict } },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var result = Map(capture, ContentDragKind.Move, 48, 0);
        Assert.False(result.Accepted); Assert.NotNull(result.Error); Assert.Null(result.Edit);
    }

    [Fact]
    public void Empty_content_and_nonfinite_or_impossible_displacements_are_explicit_failures()
    {
        var capture = Capture(Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical), View(ContentFlowAxis.Vertical));
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.MaxValue })
            Assert.False(Map(capture, ContentDragKind.Move, invalid, 0).Accepted);
        Assert.False(Map(capture, ContentDragKind.Move, 0, 0, 48, 0).Accepted);
        Assert.False(Map(capture, ContentDragKind.Move, 0, 0, 0, -48).Accepted);
        Assert.False(Map(capture, ContentDragKind.Move, 0, 0, 0, 1000).Accepted);
        var empty = Document(ContentLayoutMode.Automatic, ContentFlowAxis.Vertical, 0);
        var snapshot = ContentLayoutSolver.Resolve(empty, capture.Viewport);
        Assert.False(ContentDragPolicy.Map(empty, snapshot, capture.Viewport, capture.Tile, ContentDragKind.Move, 48, 0).Accepted);
    }

    private sealed record Press(ContentLayoutDocument Document, ContentLayoutSnapshot Snapshot, ContentLayoutViewport Viewport, ContentLayoutTile Tile);
    private static ContentLayoutDocument Document(ContentLayoutMode mode, ContentFlowAxis axis, int count = 6) => new("drag",
        Enumerable.Range(0, count).Select(i => new ContentPlacement("drag:item-" + i, i, 2, 2)).ToArray())
        { Mode = mode, FlowAxis = axis, BaseCellDip = 48, BaseGapDip = 0, MaximumColumns = 8 };
    private static ContentLayoutViewport View(ContentFlowAxis axis, double main = 144) => axis == ContentFlowAxis.Vertical
        ? new(288, main, Generation: 9) : new(main, 288, Generation: 9);
    private static Press Capture(ContentLayoutDocument document, ContentLayoutViewport viewport, int index = 0)
    {
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, viewport).Single(t => t.SourceIndex == index);
        return new(document, snapshot, viewport, tile);
    }
    private static ContentDragResult Map(Press capture, ContentDragKind kind, double x, double y, double scrollX = 0, double scrollY = 0) =>
        ContentDragPolicy.Map(capture.Document, capture.Snapshot, capture.Viewport, capture.Tile, kind, x, y, scrollX, scrollY);
}
