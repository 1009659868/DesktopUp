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
    public void MovingACardPinsItWithoutEnteringManualMode()
    {
        var document = Document("p", 3);
        var edit = new ContentLayoutEdit(ContentLayoutEditKind.Move, document.Placements[0].ItemId) { Column = 4, Row = 0 };

        var result = ContentLayoutEditing.Apply(document, edit, Viewport());

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentLayoutMode.Anchored, result.Document.Mode);
        var moved = result.Document.Placements.Single(p => p.ItemId == document.Placements[0].ItemId);
        Assert.True(moved.FixedPosition, "Dragging must create a position override (pin) for that card only.");
        Assert.Equal(4, moved.Column);
        // The remaining cards stay automatic: no full-manual takeover is implied.
        Assert.False(result.Document.Placements[1].FixedPosition);
        Assert.False(result.Document.Placements[2].FixedPosition);
    }

    [Fact]
    public void HorizontalAxisClampsToMinimumUsableRows()
    {
        // A viewport too short for a minimum-readable card must still expose the minimum usable rows:
        // the container height is clamped up instead of producing a clipped, unreachable row.
        var document = Document("p", 2, ContentFlowAxis.HorizontalLoop);
        var snapshot = ContentLayoutSolver.Resolve(document, Viewport(200, 20));
        double pitch = snapshot.CellDip + snapshot.GapDip;
        int minimumRows = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + snapshot.GapDip) / pitch);

        Assert.All(snapshot.Items, i => Assert.True(i.RowSpan >= minimumRows));
        Assert.All(snapshot.Items, i => Assert.True(i.Bounds.Height >= ContentLayoutPolicy.MinimumReadableCardDip));
        Assert.Contains(snapshot.Diagnostics, d => d.StartsWith("viewport-height-clamped", StringComparison.Ordinal));
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

        var narrow = ContentLayoutSolver.Resolve(d, Viewport(114, 600));
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
}
