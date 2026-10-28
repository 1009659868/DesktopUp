using System.Globalization;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

/// <summary>
/// Contract tests for the single-axis revision: one scroll axis per poster, head-to-tail wrapping
/// for the horizontal strip, ordinary empty ground stays droppable, and preserved regions are
/// explicit, visible and removable.
/// </summary>
public sealed class ContentLayoutFlowTests
{
    private static ContentLayoutDocument Document(string id, int count,
        ContentFlowAxis axis = ContentFlowAxis.Vertical, int span = 4, bool loop = false)
    {
        var placements = Enumerable.Range(0, count)
            .Select(i => new ContentPlacement(id + ":item-" + i.ToString("D4", CultureInfo.InvariantCulture), i, span, span))
            .ToArray();
        return ContentLayoutPolicy.Freeze(new ContentLayoutDocument(id, placements) { FlowAxis = axis, LoopScroll = loop });
    }

    private static ContentLayoutViewport Viewport(double width = 300, double height = 300,
        double scrollX = 0, double scrollY = 0) => new(width, height, scrollX, scrollY, 1, 1, 1, 1);

    [Fact]
    public void VerticalAxisNeverScrollsSideways()
    {
        var snapshot = ContentLayoutSolver.Resolve(Document("p", 6), Viewport(300, 300, scrollX: 999, scrollY: 40));

        Assert.Equal(ContentFlowAxis.Vertical, snapshot.FlowAxis);
        // Sideways scrolling is suppressed; the extent still covers every card.
        Assert.Equal(0, snapshot.ScrollXDip);
        Assert.All(snapshot.Items, i => Assert.True(i.Bounds.Right <= snapshot.ExtentWidthDip + 1e-7));
        Assert.True(snapshot.ScrollYDip >= 0);
    }

    [Fact]
    public void HorizontalStripWrapsWhenContentExceedsViewport()
    {
        var document = Document("p", 10, ContentFlowAxis.HorizontalLoop, loop: true);
        var viewport = Viewport(100, 300);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);

        Assert.Equal(ContentFlowAxis.HorizontalLoop, snapshot.FlowAxis);
        Assert.True(snapshot.LoopScroll);
        Assert.True(ContentLayoutFlow.IsLooping(snapshot, viewport));
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);
        Assert.True(period > viewport.WidthDip);
        // Height adapts to the poster: nothing scrolls vertically on this axis.
        Assert.True(snapshot.ExtentHeightDip <= viewport.HeightDip + 0.5);
        Assert.Equal(0, snapshot.ScrollYDip);
    }

    [Fact]
    public void LoopProjectionKeepsSingleIdentityAcrossTheSeam()
    {
        var document = Document("p", 10, ContentFlowAxis.HorizontalLoop, loop: true);
        var viewport = Viewport(100, 300);
        var baseSnapshot = ContentLayoutSolver.Resolve(document, viewport);
        double period = ContentLayoutFlow.LoopPeriodDip(baseSnapshot, viewport);

        // Scroll close to the end of one period: the first cards must reappear from the tail side
        // while the last cards are still leaving, i.e. real head-to-tail continuity.
        var scrolled = ContentLayoutSolver.Reclip(baseSnapshot, viewport with { ScrollXDip = period - 10 });
        var tiles = ContentLayoutFlow.ProjectTiles(scrolled, viewport);

        Assert.NotEmpty(tiles);
        Assert.Contains(tiles, t => t.LoopIndex == 1 && t.ItemId == document.Placements[0].ItemId);
        Assert.Contains(tiles, t => t.LoopIndex == 0 && t.ItemId == document.Placements[^1].ItemId);
        // Identity stays single-owned even when a card is projected twice.
        Assert.Equal(tiles.Select(t => t.ItemId).Distinct().Count(), tiles.GroupBy(t => t.ItemId).Count());
    }

    [Fact]
    public void ContinuousScrollingNeverDrifts()
    {
        var document = Document("p", 10, ContentFlowAxis.HorizontalLoop, loop: true);
        var viewport = Viewport(100, 300);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        double period = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport);

        double scroll = 0;
        for (int i = 0; i < 1000; i++)
        {
            scroll = ContentLayoutFlow.AdvanceScrollX(scroll, 7.5, period);
            Assert.True(scroll >= 0 && scroll < period);
        }
        double expected = (7.5 * 1000) % period;
        Assert.Equal(expected, scroll, 6);
    }

    [Fact]
    public void ShortContentIsNeverDuplicated()
    {
        var document = Document("p", 2, ContentFlowAxis.HorizontalLoop);
        var viewport = Viewport(600, 300);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport);

        Assert.False(ContentLayoutFlow.IsLooping(snapshot, viewport));
        Assert.Equal(snapshot.Items.Count, tiles.Count);
        Assert.All(tiles, t => Assert.Equal(0, t.LoopIndex));
    }

    [Fact]
    public void DecorativeSpaceDoesNotBlockPlacement()
    {
        var document = Document("p", 3) with
        {
            ReservedSpaces = Array.AsReadOnly(new[]
            { new ContentReservedSpace(0, 0, 4, 4) { Kind = ContentReservedKind.Decorative } })
        };
        var snapshot = ContentLayoutSolver.Resolve(document, Viewport());

        var first = snapshot.Items.Single(i => i.ItemId == document.Placements[0].ItemId);
        Assert.Equal(0, first.Column);
        Assert.Equal(0, first.Row);
    }

    [Fact]
    public void ExplicitSpaceBlocksPlacementAndIsRemovable()
    {
        var space = new ContentReservedSpace(0, 0, 4, 4) { Kind = ContentReservedKind.Explicit };
        var document = Document("p", 3) with { ReservedSpaces = Array.AsReadOnly(new[] { space }) };

        var blocked = ContentLayoutSolver.Resolve(document, Viewport());
        var first = blocked.Items.Single(i => i.ItemId == document.Placements[0].ItemId);
        Assert.False(first.Column == 0 && first.Row == 0, "An explicit keep-out area must reserve its cells.");

        var cleared = ContentLayoutEditing.ClearReservedSpaces(document);
        Assert.Empty(cleared.ReservedSpaces);
        Assert.True(cleared.DocumentVersion > document.DocumentVersion);

        var freed = ContentLayoutSolver.Resolve(cleared, Viewport());
        var restored = freed.Items.Single(i => i.ItemId == document.Placements[0].ItemId);
        Assert.Equal(0, restored.Column);
        Assert.Equal(0, restored.Row);
    }

    [Fact]
    public void MovingAnAutomaticCardCreatesOnlyAnOrderPinWithoutEnteringManualMode()
    {
        var document = Document("p", 3);
        var edit = new ContentLayoutEdit(ContentLayoutEditKind.Move, document.Placements[0].ItemId) { TargetReadOrder = 1 };

        var result = ContentLayoutEditing.Apply(document, edit, Viewport());

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentLayoutMode.Automatic, result.Document.Mode);
        var moved = result.Document.Placements.Single(p => p.ItemId == document.Placements[0].ItemId);
        Assert.Equal(2, moved.PinnedIndex);
        Assert.False(moved.FixedPosition); Assert.Null(moved.Column); Assert.Null(moved.Row);
        Assert.Equal(moved.ItemId, ContentLayoutSolver.Resolve(result.Document, Viewport()).Items[1].ItemId);
        // The remaining cards stay automatic: no full-manual takeover is implied.
        Assert.Equal(document.Placements[1], result.Document.Placements[1]);
        Assert.Equal(document.Placements[2], result.Document.Placements[2]);
    }

    [Fact]
    public void Horizontal_shortage_reports_real_viewport_without_shrinking_cards()
    {
        // The logical card remains complete, while the real viewport is still only 20 DIP high.
        // The application must establish passive-space eligibility before enabling two-axis access.
        var document = Document("p", 2, ContentFlowAxis.HorizontalLoop);
        var snapshot = ContentLayoutSolver.Resolve(document, Viewport(200, 20));
        double pitch = snapshot.CellDip + snapshot.GapDip;
        int minimumRows = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + snapshot.GapDip) / pitch);

        Assert.All(snapshot.Items, i => Assert.True(i.RowSpan >= minimumRows));
        Assert.All(snapshot.Items, i => Assert.True(i.Bounds.Height >= ContentLayoutPolicy.MinimumReadableCardDip));
        Assert.Equal(ContentLayoutSpaceStatus.InsufficientCrossAxis, snapshot.SpaceStatus);
        Assert.Equal(20, snapshot.AvailableCrossAxisDip);
        Assert.True(snapshot.IsLayoutReadOnly);
        Assert.All(snapshot.Items, i => Assert.True(i.Clip.IsEmpty || i.Clip.Bottom <= 20));
        Assert.DoesNotContain(snapshot.Diagnostics, d => d.StartsWith("viewport-height-clamped", StringComparison.Ordinal));
    }

    [Fact]
    public void NarrowingWrapsCardsInsteadOfShrinkingOrHidingThem()
    {
        // L08: two cards sharing one line when wide must wrap onto separate lines when the container
        // narrows, with their authored spans untouched, so nothing is clipped or hidden sideways.
        var d = Document("p", 2);
        d = d with { Placements = Array.AsReadOnly(d.Placements.Select(p => p with { ColumnSpan = 5 }).ToArray()) };

        var wide = ContentLayoutSolver.Resolve(d, Viewport(354, 600));
        Assert.Equal(wide.Items[0].Bounds.Y, wide.Items[1].Bounds.Y);

        var narrow = ContentLayoutSolver.Resolve(d, Viewport(144, 600));
        Assert.Equal(ContentLayoutSpaceStatus.Normal, narrow.SpaceStatus);
        Assert.NotEqual(narrow.Items[0].Bounds.Y, narrow.Items[1].Bounds.Y);
        Assert.Equal(5, d.Placements[0].ColumnSpan);
        Assert.All(narrow.Items, i => Assert.True(i.Bounds.X >= 0 && i.Bounds.Right <= narrow.ExtentWidthDip + 1e-7));
    }

    [Fact]
    public void HeatDrivenSizeStaysOffByDefaultAndSurvivesPersistence()
    {
        var document = Document("p", 3);
        Assert.False(document.AllowHeatDrivenSize);

        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(document), "p", out var parsed, out var error), error);
        Assert.False(parsed!.AllowHeatDrivenSize);
        Assert.Equal(document.FlowAxis, parsed.FlowAxis);
    }

    [Fact]
    public void VerticalAxisLoopsOnlyWhenTheSwitchIsOn()
    {
        // L09: the axis is vertical and the switch is what turns looping on; the period follows the
        // vertical extent, symmetric to the horizontal strip.
        var viewport = Viewport(300, 120);
        var off = ContentLayoutSolver.Resolve(Document("p", 6), viewport);
        Assert.False(ContentLayoutFlow.IsLooping(off, viewport));

        var on = ContentLayoutSolver.Resolve(Document("p", 6, loop: true), viewport);
        Assert.True(ContentLayoutFlow.IsLooping(on, viewport));
        Assert.Equal(on.ExtentHeightDip + on.GapDip, ContentLayoutFlow.LoopPeriodDip(on, viewport), 6);
    }

    [Fact]
    public void LoopSwitchNeverChangesSqueezingOrWrapping()
    {
        // The switch is projection-only: identical geometry with looping on or off.
        var viewport = Viewport(114, 600);
        var off = ContentLayoutSolver.Resolve(Document("p", 2), viewport);
        var on = ContentLayoutSolver.Resolve(Document("p", 2, loop: true), viewport);

        Assert.Equal(off.Columns, on.Columns);
        Assert.Equal(off.Items.Select(i => i.Bounds), on.Items.Select(i => i.Bounds));
        Assert.Equal(off.ExtentHeightDip, on.ExtentHeightDip);
    }

    [Fact]
    public void ReferenceCompositionNoLongerAuthorsAHardKeepOutArea()
    {
        Assert.Empty(ContentLayoutSamples.Illustration("p").ReservedSpaces);
        Assert.Empty(ContentLayoutSamples.Covers("p").ReservedSpaces);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void FiniteProjectionClampsPastTheTailInsteadOfLosingAllTiles(ContentFlowAxis axis)
    {
        var snapshot = FlowSnapshot(axis) with { ScrollXDip = 9000, ScrollYDip = 9000 };
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, Viewport(200, 200));
        var tail = Assert.Single(tiles);
        Assert.Equal("p:last", tail.ItemId);
        Assert.Equal(100, axis == ContentFlowAxis.Vertical ? tail.Bounds.Y : tail.Bounds.X);
        Assert.Equal(0, axis == ContentFlowAxis.Vertical ? tail.Bounds.X : tail.Bounds.Y);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 100, false)] [InlineData(ContentFlowAxis.Vertical, 200, true)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 100, true)] [InlineData(ContentFlowAxis.HorizontalLoop, 200, false)]
    public void ShortOrExactlyOneScreenContentIgnoresStaleOffsets(ContentFlowAxis axis, double extent, bool loop)
    {
        var snapshot = FlowSnapshot(axis, extent, loop) with { ScrollXDip = 80, ScrollYDip = 80 };
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, Viewport(200, 200));
        Assert.False(ContentLayoutFlow.IsLooping(snapshot, Viewport(200, 200)));
        Assert.Equal(snapshot.Items.Count, tiles.Count);
        Assert.All(tiles, tile => Assert.Equal(0, tile.LoopIndex));
        Assert.Equal(0, axis == ContentFlowAxis.Vertical ? tiles[0].Bounds.Y : tiles[0].Bounds.X);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void EmptyContentNeverAcquiresLoopEligibilityFromAnExtentAlone(ContentFlowAxis axis)
    {
        var snapshot = FlowSnapshot(axis, loop: true) with { Items = Array.Empty<ContentResolvedPlacement>() };
        Assert.False(ContentLayoutFlow.IsLooping(snapshot, Viewport(200, 200)));
        Assert.Empty(ContentLayoutFlow.ProjectTiles(snapshot, Viewport(200, 200)));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void ReclipAcceptsNegativeLoopOffsetsAndNormalizesBothDirections(ContentFlowAxis axis)
    {
        var snapshot = FlowSnapshot(axis, loop: true);
        var viewport = Viewport(200, 200, -10, -10);
        var scrolled = ContentLayoutSolver.Reclip(snapshot, viewport);
        double expected = ContentLayoutFlow.LoopPeriodDip(snapshot, viewport) - 10;
        Assert.Equal(expected, axis == ContentFlowAxis.Vertical ? scrolled.ScrollYDip : scrolled.ScrollXDip);
        Assert.Equal(0, axis == ContentFlowAxis.Vertical ? scrolled.ScrollXDip : scrolled.ScrollYDip);
    }

    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void NonFiniteLoopPeriodsDoNotProduceANonFiniteOffset(double period)
    {
        Assert.Equal(0, ContentLayoutFlow.NormalizeScroll(-10, period));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void SignedOffsetsUseOneFiniteOrLoopContractAndNeverChangeIntent(ContentFlowAxis axis)
    {
        var basis = FlowSnapshot(axis);
        foreach (double input in new[] { -16_000_000d, -1020, -10, 0, 300, 1020, 16_000_000 })
        foreach (bool loop in new[] { false, true })
        {
            var snapshot = basis with { LoopScroll = loop };
            var viewport = Viewport(200, 200, input, input);
            var actual = ContentLayoutFlow.NormalizeOffsets(snapshot, viewport);
            double expected = loop ? ContentLayoutFlow.NormalizeScroll(input, 1020) : Math.Clamp(input, 0, 800);
            Assert.Equal(expected, axis == ContentFlowAxis.Vertical ? actual.ScrollYDip : actual.ScrollXDip);
            Assert.Equal(0, axis == ContentFlowAxis.Vertical ? actual.ScrollXDip : actual.ScrollYDip);
            Assert.Equal(loop, snapshot.LoopScroll);
            Assert.Equal(0, snapshot.ScrollXDip); Assert.Equal(0, snapshot.ScrollYDip);
        }
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void LoopTilesRemainBoundedAndRetainLogicalIdentityForSignedEpochs(ContentFlowAxis axis)
    {
        foreach (double input in new[] { -16_000_000d, -1030, -10, 0, 950, 1020, 16_000_000 })
        {
            var snapshot = FlowSnapshot(axis, loop: true) with { ScrollXDip = input, ScrollYDip = input };
            var tiles = ContentLayoutFlow.ProjectTiles(snapshot, Viewport(200, 200));
            Assert.InRange(tiles.Count, 0, snapshot.Items.Count * 3);
            Assert.Equal(tiles.Count, tiles.Select(t => (t.ItemId, t.LoopIndex)).Distinct().Count());
            foreach (var tile in tiles)
            {
                var source = Assert.Single(snapshot.Items, item => item.ItemId == tile.ItemId);
                Assert.Equal(source.SourceIndex, tile.SourceIndex); Assert.Equal(source.ReadOrder, tile.ReadOrder);
                Assert.Equal(source.Bounds.Width, tile.Bounds.Width); Assert.Equal(source.Bounds.Height, tile.Bounds.Height);
                Assert.InRange(tile.LoopIndex, -1, 1);
                Assert.True(tile.Clip.X >= 0 && tile.Clip.Y >= 0 && tile.Clip.Right <= 200 && tile.Clip.Bottom <= 200);
            }
        }
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void SeamAnchorUsesTheVisibleCopyAndRestoresPhaseAfterGeometryChanges(ContentFlowAxis axis)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        var viewport = Viewport(200, 200);
        var before = FlowSnapshot(axis, loop: true) with { ScrollXDip = vertical ? 0 : 950, ScrollYDip = vertical ? 950 : 0 };
        var tile = Assert.Single(ContentLayoutFlow.ProjectTiles(before, viewport), t => t.ItemId == "p:first" && t.LoopIndex == 1);
        var anchor = ContentLayoutFlow.CaptureAnchor(before, viewport, tile, vertical ? 20 : 80, vertical ? 80 : 20);
        Assert.Equal(10, vertical ? anchor.InsideYDip : anchor.InsideXDip);
        var after = FlowSnapshot(axis, 1200, true);
        after = after with { Items = after.Items.Select(item => item.ItemId == "p:first" ? item with
            { Bounds = item.Bounds with { X = vertical ? 0 : 50, Y = vertical ? 50 : 0 } } : item).ToArray() };
        var restored = ContentLayoutFlow.RestoreAnchor(after, viewport, anchor);
        Assert.Equal(1200, vertical ? restored.ScrollYDip : restored.ScrollXDip);
        var projected = ContentLayoutFlow.ProjectTiles(after with { ScrollXDip = restored.ScrollXDip, ScrollYDip = restored.ScrollYDip }, viewport);
        var first = Assert.Single(projected, t => t.ItemId == "p:first");
        Assert.Equal(anchor.ViewportXDip, first.Bounds.X + anchor.InsideXDip);
        Assert.Equal(anchor.ViewportYDip, first.Bounds.Y + anchor.InsideYDip);
    }

    [Fact]
    public void FiniteAnchorClampsOnlyAtTheHeadAndTailAndFallsBackPredictablyAfterRemoval()
    {
        var snapshot = FlowSnapshot(ContentFlowAxis.Vertical);
        var viewport = Viewport(200, 200);
        var head = new ContentBrowseAnchor("p:first", 0, 10, 30, 10, 190);
        Assert.Equal(0, ContentLayoutFlow.RestoreAnchor(snapshot, viewport, head).ScrollYDip);
        var tail = new ContentBrowseAnchor("p:last", 1, 10, 90, 10, 10);
        Assert.Equal(800, ContentLayoutFlow.RestoreAnchor(snapshot, viewport, tail).ScrollYDip);
        var removed = new ContentBrowseAnchor("p:removed", 1, 20, 70, 10, 50);
        var successor = snapshot with { Items = snapshot.Items.Select(i => i.ItemId == "p:last" ? i with { ReadOrder = 2 } : i).ToArray() };
        Assert.Equal(800, ContentLayoutFlow.RestoreAnchor(successor, viewport, removed).ScrollYDip);
        var predecessor = snapshot with { Items = snapshot.Items.Take(1).ToArray() };
        Assert.Equal(0, ContentLayoutFlow.RestoreAnchor(predecessor, viewport, removed).ScrollYDip);
        Assert.Equal(new ContentBrowseOffsets(0, 0), ContentLayoutFlow.RestoreAnchor(snapshot with { Items = [] }, viewport, removed));
    }

    [Fact]
    public void ChangingAxisKeepsTheInternalPhysicalPointWithoutRotatingTheCard()
    {
        var viewport = Viewport(200, 200);
        var vertical = FlowSnapshot(ContentFlowAxis.Vertical);
        var tile = ContentLayoutFlow.ProjectTiles(vertical, viewport)[0];
        var anchor = ContentLayoutFlow.CaptureAnchor(vertical, viewport, tile, 20, 30);
        var horizontal = FlowSnapshot(ContentFlowAxis.HorizontalLoop);
        horizontal = horizontal with { Items = horizontal.Items.Select(i => i.ItemId == anchor.ItemId ? i with
            { Bounds = new(300, 0, 100, 100) } : i).ToArray() };
        var offsets = ContentLayoutFlow.RestoreAnchor(horizontal, viewport, anchor);
        Assert.Equal(new ContentBrowseOffsets(300, 0), offsets);
        var restored = Assert.Single(ContentLayoutFlow.ProjectTiles(horizontal with { ScrollXDip = offsets.ScrollXDip }, viewport));
        Assert.Equal(20, restored.Bounds.X + anchor.InsideXDip);
        Assert.Equal(30, restored.Bounds.Y + anchor.InsideYDip);
        Assert.Equal(100, restored.Bounds.Width); Assert.Equal(100, restored.Bounds.Height);
    }

    [Fact]
    public void InsufficientSpaceRequiresExplicitOverflowEligibilityAndNeverRewritesLoopPreference()
    {
        var viewport = Viewport(180, 300, 90, 500);
        var snapshot = FlowSnapshot(ContentFlowAxis.Vertical, 500, true) with
        { ExtentWidthDip = 240, SpaceStatus = ContentLayoutSpaceStatus.InsufficientCrossAxis };
        Assert.False(ContentLayoutFlow.IsLooping(snapshot, viewport));
        Assert.Equal(new ContentBrowseOffsets(0, 200), ContentLayoutFlow.NormalizeOffsets(snapshot, viewport));
        Assert.Equal(new ContentBrowseOffsets(60, 200), ContentLayoutFlow.NormalizeOffsets(snapshot, viewport, ContentBrowseMode.ReadOnlyOverflow));
        Assert.True(snapshot.LoopScroll);
        Assert.Throws<ContentLayoutException>(() => ContentLayoutFlow.NormalizeOffsets(snapshot with
            { SpaceStatus = ContentLayoutSpaceStatus.LegacyConstraintConflict }, viewport, ContentBrowseMode.ReadOnlyOverflow));
        Assert.Throws<ContentLayoutException>(() => ContentLayoutFlow.NormalizeOffsets(snapshot with
            { SpaceStatus = ContentLayoutSpaceStatus.Normal }, viewport, ContentBrowseMode.ReadOnlyOverflow));
    }

    [Fact]
    public void AnchorCaptureRejectsAnOldProjectionAndAPointOutsideTheVisibleClip()
    {
        var viewport = Viewport(200, 200);
        var snapshot = FlowSnapshot(ContentFlowAxis.Vertical);
        var tile = ContentLayoutFlow.ProjectTiles(snapshot, viewport)[0];
        Assert.Throws<ContentLayoutException>(() => ContentLayoutFlow.CaptureAnchor(snapshot, viewport, tile, 150, 150));
        Assert.Throws<ContentLayoutException>(() => ContentLayoutFlow.CaptureAnchor(snapshot with { ScrollYDip = 40 }, viewport, tile, 10, 10));
    }

    private static ContentLayoutSnapshot FlowSnapshot(ContentFlowAxis axis, double extent = 1000, bool loop = false)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        var items = new List<ContentResolvedPlacement>
        {
            new("p:first", 0, 0, 0, 0, 4, 4, new(0, 0, 100, 100), default)
        };
        if (extent > 100)
            items.Add(new("p:last", 1, 1, 0, 0, 4, 4,
                new(vertical ? 0 : extent - 100, vertical ? extent - 100 : 0, 100, 100), default));
        return new("p", 1, 1, 1, 1, 4, 24, 20, vertical ? 100 : extent, vertical ? extent : 100,
            0, 0, items.AsReadOnly(), Array.Empty<string>()) { FlowAxis = axis, LoopScroll = loop };
    }
}
