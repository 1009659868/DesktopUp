using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

// R3-D: ordinary manual projection, based on the manual composition review §4.2/§4.3.
// Only Resolve is exercised here; these tests do not certify editing, persistence or native input.
public sealed class ContentManualProjectionTests
{
    private const double Cell = 48;

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Eight_six_four_eight_preserves_authored_gaps_and_restores_the_exact_composition(ContentFlowAxis axis)
    {
        // §4.3, expressed in normalized cross/main coordinates. The horizontal fixture is
        // transposed input; the solver must preserve each input card's own physical spans.
        var document = Manual(axis,
            Item(axis, "C", 0, 0, 4, 2, 1),
            Item(axis, "A", 2, 0, 0, 4, 2),
            Item(axis, "B", 1, 6, 0, 2, 2));
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(axis, 8));
        Positions(original, ("A", 0, 0), ("B", 6, 0), ("C", 0, 4));

        for (int cycle = 0; cycle < 3; cycle++)
        {
            var six = ContentLayoutSolver.Resolve(document, View(axis, 6));
            var four = ContentLayoutSolver.Resolve(document, View(axis, 4));
            Positions(six, ("A", 0, 0), ("B", 4, 0), ("C", 0, 4));
            Positions(four, ("A", 0, 0), ("B", 0, 2), ("C", 0, 4));
            AssertNormalProjection(document, six, 6, "A", "B", "C");
            AssertNormalProjection(document, four, 4, "A", "B", "C");
            var restored = ContentLayoutSolver.Resolve(document, View(axis, 8));
            Assert.Equal(original.Items, restored.Items);
            Assert.Equal(original.ExtentWidthDip, restored.ExtentWidthDip);
            Assert.Equal(original.ExtentHeightDip, restored.ExtentHeightDip);
            Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
        }
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void B_c_a_counterexample_keeps_visual_read_order_after_clamping_and_collision(ContentFlowAxis axis)
    {
        // §4.2: only removing overlap leaves A at (0,1), visually before C at (1,1).
        // A must move behind C; stale automatic Order and source array order are not manual order.
        var document = Manual(axis,
            Item(axis, "C", 1, 2, 0, 1, 1),
            Item(axis, "A", 0, 0, 1, 1, 1),
            Item(axis, "B", 2, 1, 0, 1, 1));
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(axis, 4));
        Positions(original, ("B", 1, 0), ("C", 2, 0), ("A", 0, 1));

        var projected = ContentLayoutSolver.Resolve(document, View(axis, 2));
        Positions(projected, ("B", 1, 0), ("C", 1, 1), ("A", 0, 2));
        AssertNormalProjection(document, projected, 2, "B", "C", "A");

        var minimum = ContentLayoutSolver.Resolve(document, View(axis, 1));
        Positions(minimum, ("B", 0, 0), ("C", 0, 1), ("A", 0, 2));
        AssertNormalProjection(document, minimum, 1, "B", "C", "A");
        Assert.Equal(original.Items, ContentLayoutSolver.Resolve(document, View(axis, 4)).Items);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Mixed_spans_keep_identities_size_and_visual_order_without_compacting_distant_gaps(ContentFlowAxis axis)
    {
        var document = Manual(axis,
            Item(axis, "tail", 0, 5, 6, 3, 1),
            Item(axis, "wide", 5, 3, 0, 4, 2),
            Item(axis, "distant", 2, 1, 12, 1, 2),
            Item(axis, "tall", 4, 0, 3, 2, 3),
            Item(axis, "small", 9, 0, 0, 1, 1));
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(axis, 8));
        foreach (int crossCells in new[] { 7, 5, 4, 6, 8, 4, 8 })
        {
            var snapshot = ContentLayoutSolver.Resolve(document, View(axis, crossCells));
            AssertNormalProjection(document, snapshot, crossCells, "small", "wide", "tall", "tail", "distant");
            Assert.Equal(snapshot.Items, ContentLayoutSolver.Resolve(document, View(axis, crossCells)).Items);
            Assert.Equal(12, MainCell(snapshot, snapshot.Items.Single(i => i.ItemId == "manual:distant")));
            if (crossCells == 4)
            {
                // At M even the small items occupy separate bands; area does not reorder them.
                Assert.All(snapshot.Items, i => Assert.Equal(0, Cross(snapshot, i)));
                for (int i = 1; i < snapshot.Items.Count; i++)
                    Assert.True(MainCell(snapshot, snapshot.Items[i]) >=
                        MainCell(snapshot, snapshot.Items[i - 1]) + MainSpan(snapshot, snapshot.Items[i - 1]));
            }
            if (crossCells == 8) Assert.Equal(original.Items, snapshot.Items);
            Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
        }
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Passive_shortage_is_read_only_preserves_size_and_restores_from_original_intent(ContentFlowAxis axis)
    {
        var document = Manual(axis, Item(axis, "A", 0, 0, 0, 4, 2), Item(axis, "B", 1, 6, 4, 2, 1));
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(axis, 8));
        var viewport = View(axis, 3);
        var shortage = ContentLayoutSolver.Resolve(document, viewport);

        Assert.Equal(ContentLayoutSpaceStatus.InsufficientCrossAxis, shortage.SpaceStatus);
        Assert.True(shortage.IsLayoutReadOnly);
        Assert.Equal(4 * Cell, shortage.MinimumCrossAxisDip);
        Assert.Equal(3 * Cell, shortage.AvailableCrossAxisDip);
        AssertIdentityAndSize(document, shortage);
        foreach (var item in shortage.Items.Where(i => !i.Clip.IsEmpty))
        {
            Assert.InRange(item.Clip.X, 0, viewport.WidthDip);
            Assert.InRange(item.Clip.Y, 0, viewport.HeightDip);
            Assert.True(item.Clip.Right <= viewport.WidthDip && item.Clip.Bottom <= viewport.HeightDip);
        }
        Assert.DoesNotContain(shortage.Diagnostics, d => d.Contains("squeeze", StringComparison.Ordinal));
        Assert.Equal(original.Items, ContentLayoutSolver.Resolve(document, View(axis, 8)).Items);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Hard_fixed_coordinate_is_not_clamped_as_an_ordinary_manual_position(ContentFlowAxis axis)
    {
        var document = Manual(axis, Item(axis, "fixed", 0, 6, 0, 2, 2) with { FixedPosition = true });
        string authored = ContentLayoutPolicy.Serialize(document);
        var result = ContentLayoutSolver.Resolve(document, View(axis, 2));

        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, result.SpaceStatus);
        Assert.True(result.IsLayoutReadOnly);
        Assert.Equal(2 * Cell, result.MinimumCrossAxisDip);
        Positions(result, ("fixed", 6, 0));
        AssertIdentityAndSize(document, result);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Explicit_reserved_space_overflow_is_not_dropped_or_treated_as_ordinary_whitespace(ContentFlowAxis axis)
    {
        var space = axis == ContentFlowAxis.Vertical ? new ContentReservedSpace(6, 0, 2, 2)
            : new ContentReservedSpace(0, 6, 2, 2);
        var document = Manual(axis, Item(axis, "A", 0, 0, 0, 2, 2)) with
        { ReservedSpaces = [space with { Kind = ContentReservedKind.Explicit, Name = "keep this gap" }] };
        string authored = ContentLayoutPolicy.Serialize(document);
        var result = ContentLayoutSolver.Resolve(document, View(axis, 2));

        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, result.SpaceStatus);
        Assert.True(result.IsLayoutReadOnly);
        Assert.Equal(2 * Cell, result.MinimumCrossAxisDip);
        Assert.True((axis == ContentFlowAxis.Vertical ? result.ExtentWidthDip : result.ExtentHeightDip) >= 8 * Cell);
        Positions(result, ("A", 0, 0));
        AssertIdentityAndSize(document, result);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void One_thousand_manual_members_project_to_single_bands_and_restore_without_omission(ContentFlowAxis axis)
    {
        var document = Manual(axis, Enumerable.Range(0, ContentLayoutPolicy.MaximumItems)
            .Select(i => Item(axis, "item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                ContentLayoutPolicy.MaximumItems - i, i % 32, i / 32 * 2, 1, 1)).Reverse().ToArray());
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(axis, 32));
        var narrow = ContentLayoutSolver.Resolve(document, View(axis, 1));

        Assert.Equal(ContentLayoutSpaceStatus.Normal, narrow.SpaceStatus);
        AssertIdentityAndSize(document, narrow);
        Assert.Equal(ContentLayoutPolicy.MaximumItems, narrow.Items.Count);
        Assert.All(narrow.Items, i =>
        {
            Assert.Equal(0, Cross(narrow, i));
            Assert.InRange(MainCell(narrow, i) + MainSpan(narrow, i), 1, ContentLayoutPolicy.MaximumRow + 1);
        });
        for (int i = 1; i < narrow.Items.Count; i++)
            Assert.True(MainCell(narrow, narrow.Items[i]) >= MainCell(narrow, narrow.Items[i - 1]) + MainSpan(narrow, narrow.Items[i - 1]));
        Assert.Equal(narrow.Items, ContentLayoutSolver.Resolve(document, View(axis, 1)).Items);
        Assert.Equal(original.Items, ContentLayoutSolver.Resolve(document, View(axis, 32)).Items);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Over_capacity_manual_input_is_explicitly_rejected(ContentFlowAxis axis)
    {
        var document = Manual(axis, Enumerable.Range(0, ContentLayoutPolicy.MaximumItems + 1)
            .Select(i => Item(axis, "item-" + i, i, i % 32, i / 32 * 2, 1, 1)).ToArray());
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(document, View(axis, 1)));
        Assert.Equal(ContentLayoutPolicy.MaximumItems + 1, document.Placements.Count);
    }

    [Fact]
    public void Projection_exhausting_the_main_axis_rejects_without_partial_result_or_authoring_change()
    {
        // Both authored rectangles fit before narrowing. The second cannot move past the first
        // at C=M without crossing the bounded main-axis end. Horizontal authored Column retains
        // its existing schema bound, so this is deliberately a vertical exhaustion fixture.
        var document = Manual(ContentFlowAxis.Vertical,
            Item(ContentFlowAxis.Vertical, "A", 0, 0, ContentLayoutPolicy.MaximumRow - 2, 2, 2),
            Item(ContentFlowAxis.Vertical, "B", 1, 2, ContentLayoutPolicy.MaximumRow - 2, 2, 2));
        string authored = ContentLayoutPolicy.Serialize(document);
        var original = ContentLayoutSolver.Resolve(document, View(ContentFlowAxis.Vertical, 4));
        Assert.Equal(2, original.Items.Count);

        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(document, View(ContentFlowAxis.Vertical, 2)));
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
        Assert.Equal(original.Items, ContentLayoutSolver.Resolve(document, View(ContentFlowAxis.Vertical, 4)).Items);
    }

    private static ContentPlacement Item(ContentFlowAxis axis, string key, int order,
        int cross, int main, int crossSpan, int mainSpan) => axis == ContentFlowAxis.Vertical
        ? new("manual:" + key, order, crossSpan, mainSpan) { Column = cross, Row = main, ManualSize = true }
        : new("manual:" + key, order, mainSpan, crossSpan) { Column = main, Row = cross, ManualSize = true };

    private static ContentLayoutDocument Manual(ContentFlowAxis axis, params ContentPlacement[] placements) => new("manual", placements)
    { Mode = ContentLayoutMode.Manual, FlowAxis = axis, BaseCellDip = Cell, BaseGapDip = 0, DocumentVersion = 7, ItemSetVersion = 3 };

    private static ContentLayoutViewport View(ContentFlowAxis axis, int crossCells) => axis == ContentFlowAxis.Vertical
        ? new(crossCells * Cell, 10 * Cell) : new(10 * Cell, crossCells * Cell);

    private static int Cross(ContentLayoutSnapshot snapshot, ContentResolvedPlacement item) =>
        snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.Column : item.Row;
    private static int MainCell(ContentLayoutSnapshot snapshot, ContentResolvedPlacement item) =>
        snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.Row : item.Column;
    private static double CrossSpan(ContentLayoutSnapshot snapshot, ContentResolvedPlacement item) =>
        snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.ColumnSpan : item.RowSpan;
    private static double MainSpan(ContentLayoutSnapshot snapshot, ContentResolvedPlacement item) =>
        snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.RowSpan : item.ColumnSpan;

    private static void Positions(ContentLayoutSnapshot snapshot, params (string Key, int Cross, int Main)[] expected)
    {
        Assert.Equal(expected.Select(p => ("manual:" + p.Key, p.Cross, p.Main)),
            snapshot.Items.OrderBy(i => i.ReadOrder).Select(i => (i.ItemId, Cross(snapshot, i), MainCell(snapshot, i))));
    }

    private static void AssertIdentityAndSize(ContentLayoutDocument document, ContentLayoutSnapshot snapshot)
    {
        Assert.Equal(document.Placements.Select(p => p.ItemId).Order(StringComparer.Ordinal),
            snapshot.Items.Select(p => p.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal(document.Placements.Count, snapshot.Items.Select(i => i.ItemId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(document.DocumentVersion, snapshot.DocumentVersion);
        Assert.Equal(document.ItemSetVersion, snapshot.ItemSetVersion);
        foreach (var item in snapshot.Items)
        {
            var authored = document.Placements[item.SourceIndex];
            Assert.Equal(authored.ItemId, item.ItemId);
            Assert.Equal(authored.ColumnSpan, item.ColumnSpan);
            Assert.Equal(authored.RowSpan, item.RowSpan);
            Assert.Equal(authored.ColumnSpan * Cell, item.Bounds.Width);
            Assert.Equal(authored.RowSpan * Cell, item.Bounds.Height);
            Assert.True(double.IsFinite(item.Bounds.Right) && double.IsFinite(item.Bounds.Bottom));
        }
    }

    private static void AssertNormalProjection(ContentLayoutDocument document, ContentLayoutSnapshot snapshot,
        int crossCells, params string[] stableOrder)
    {
        Assert.Equal(ContentLayoutSpaceStatus.Normal, snapshot.SpaceStatus);
        Assert.False(snapshot.IsLayoutReadOnly);
        AssertIdentityAndSize(document, snapshot);
        Assert.Equal(stableOrder.Select(id => "manual:" + id), snapshot.Items.OrderBy(i => i.ReadOrder).Select(i => i.ItemId));
        // Read the resulting geometry independently of the declared ReadOrder. This catches B/A/C.
        Assert.Equal(stableOrder.Select(id => "manual:" + id), snapshot.Items.OrderBy(i => MainCell(snapshot, i))
            .ThenBy(i => Cross(snapshot, i)).ThenBy(i => i.ItemId, StringComparer.Ordinal).Select(i => i.ItemId));
        Assert.All(snapshot.Items, item =>
        {
            Assert.InRange(Cross(snapshot, item), 0, crossCells - 1);
            Assert.InRange(Cross(snapshot, item) + CrossSpan(snapshot, item), 1, crossCells);
            var authored = document.Placements[item.SourceIndex];
            int originalMain = snapshot.FlowAxis == ContentFlowAxis.Vertical ? authored.Row!.Value : authored.Column!.Value;
            Assert.True(MainCell(snapshot, item) >= originalMain, "Projection must not backfill an authored main-axis gap.");
        });
        for (int i = 0; i < snapshot.Items.Count; i++)
        for (int j = i + 1; j < snapshot.Items.Count; j++)
            Assert.False(snapshot.Items[i].Bounds.Intersects(snapshot.Items[j].Bounds),
                $"Manual projection overlaps {snapshot.Items[i].ItemId} and {snapshot.Items[j].ItemId}.");
    }
}
