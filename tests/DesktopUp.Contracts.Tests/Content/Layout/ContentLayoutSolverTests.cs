using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutSolverTests
{
    internal static ContentLayoutDocument Doc(int count = 6) => ContentLayoutSamples.Equal("A", count);
    internal static ContentLayoutViewport View(double width = 354, double height = 180) => new(width, height);
    internal static void Safe(ContentLayoutSnapshot snapshot)
    {
        Assert.Equal(snapshot.Items.Count, snapshot.Items.Select(i => i.ItemId).Distinct().Count());
        foreach (var item in snapshot.Items)
        {
            Assert.True(double.IsFinite(item.Bounds.Bottom) && double.IsFinite(item.Bounds.Right));
            Assert.True(item.Bounds.Width >= 48 && item.Bounds.Height >= 48);
            int cross = snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.Column : item.Row;
            int main = snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.Row : item.Column;
            double crossSpan = snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.ColumnSpan : item.RowSpan;
            double mainSpan = snapshot.FlowAxis == ContentFlowAxis.Vertical ? item.RowSpan : item.ColumnSpan;
            Assert.InRange(cross, 0, ContentLayoutPolicy.MaximumColumns - 1);
            Assert.InRange(main, 0, ContentLayoutPolicy.MaximumRow);
            Assert.True(cross + crossSpan <= ContentLayoutPolicy.MaximumColumns && main + mainSpan <= ContentLayoutPolicy.MaximumRow + 1);
        }
        for (int i = 0; i < snapshot.Items.Count; i++)
        for (int j = i + 1; j < snapshot.Items.Count; j++)
            Assert.False(snapshot.Items[i].Bounds.Intersects(snapshot.Items[j].Bounds), $"Overlap {i}/{j}");
    }

    [Theory]
    [InlineData("illustration", 9, 450, 830)]
    [InlineData("covers", 10, 450, 678)]
    [InlineData("landscape", 8, 546, 362)]
    public void Reference_geometry_is_exact_and_preserves_regional_composition(string kind, int count, double width, double height)
    {
        var d = kind switch { "illustration" => ContentLayoutSamples.Illustration("A"), "covers" => ContentLayoutSamples.Covers("A"), _ => ContentLayoutSamples.Landscape("A") };
        string original = ContentLayoutPolicy.Serialize(d);
        // Exact author geometry is asserted in sufficient space. Ordinary manual narrowing is a
        // separate responsive projection contract, not a promise to retain off-screen coordinates.
        var s = ContentLayoutSolver.Resolve(d, View(1000, 1000));
        Assert.Equal(count, s.Items.Count); Assert.Equal(width, s.ExtentWidthDip); Assert.Equal(height, s.ExtentHeightDip);
        foreach (var item in s.Items)
        {
            var p = d.Placements[item.SourceIndex]; var r = d.Regions.Single(r => r.Id == p.RegionId);
            Assert.Equal(r.Column + p.Column, item.Column); Assert.Equal(r.Row + p.Row, item.Row);
            Assert.Equal(p.ColumnSpan, item.ColumnSpan); Assert.Equal(p.RowSpan, item.RowSpan);
        }
        Assert.Equal(original, ContentLayoutPolicy.Serialize(d)); Safe(s);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(100)] [InlineData(1000)]
    public void Finite_capacity_deterministic_no_overlap_or_omission(int count)
    {
        var d = Doc(count); var a = ContentLayoutSolver.Resolve(d, View()); var b = ContentLayoutSolver.Resolve(d, View());
        Assert.Equal(count, a.Items.Count); Assert.Equal(a.Items, b.Items); Safe(a);
        Assert.Equal(Enumerable.Range(0, count), a.Items.Select(p => p.ReadOrder));
    }

    [Fact]
    public void Maximum_spans_and_capacity_terminate_inside_finite_row_range()
    {
        var d = Doc(1000); d = d with { Placements = d.Placements.Select(p => p with { ColumnSpan = 32, RowSpan = 32 }).ToArray() };
        var s = ContentLayoutSolver.Resolve(d, View(954, 100));
        Assert.Equal(32000 * 30 - 6, s.ExtentHeightDip); Safe(s);
    }

    [Fact]
    public void Input_array_order_does_not_change_business_order_or_geometry()
    {
        var d = Doc(); var a = ContentLayoutSolver.Resolve(d, View());
        var b = ContentLayoutSolver.Resolve(d with { Placements = d.Placements.Reverse().ToArray() }, View());
        Assert.Equal(a.Items.Select(p => (p.ItemId,p.ReadOrder,p.Bounds)), b.Items.Select(p => (p.ItemId,p.ReadOrder,p.Bounds)));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)] [InlineData(1000001)]
    public void Invalid_viewport_is_explicitly_rejected(double width) => Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(Doc(), View(width)));

    [Theory]
    [InlineData("span")] [InlineData("foreign")] [InlineData("duplicate")] [InlineData("position")] [InlineData("capacity")] [InlineData("future")]
    public void Invalid_intent_cannot_enter_solver(string kind)
    {
        var d = Doc(); var items = d.Placements.ToArray();
        d = kind switch
        {
            "capacity" => Doc(1001), "future" => d with { EngineVersion = 2 },
            _ => d with { Placements = items.Select((p,i) => i != 0 ? p : kind switch {
                "span" => p with { ColumnSpan = ContentLayoutPolicy.MaximumSpan + 1 }, "foreign" => p with { ItemId = "B:x" },
                "duplicate" => p with { ItemId = items[1].ItemId }, _ => p with { Column = -1, Row = 0 } }).ToArray() }
        };
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(d, View()));
    }

    [Fact]
    public void Readable_minimum_is_effective_only_and_capacity_conflict_is_domain_rejection()
    {
        var d = Doc(1) with { BaseCellDip = 16, BaseGapDip = 0 };
        d = d with { Placements = [d.Placements[0] with { ColumnSpan = 1, RowSpan = 1 }] };
        var s = ContentLayoutSolver.Resolve(d, View() with { Scale = .8 });
        Assert.Equal(3.75, s.Items[0].ColumnSpan); Assert.Equal(1, d.Placements[0].ColumnSpan);
        Assert.Contains("readable-minimum", s.Items[0].Diagnostic); Safe(s);
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(d with { MaximumColumns = 2 }, View() with { Scale = .8 }));
    }

    [Fact]
    public void Responsive_shrink_then_restore_never_rewrites_preferred_span_or_drifts()
    {
        var d = Doc(1); d = d with { Placements = [d.Placements[0] with { ColumnSpan = 12 }] };
        var wide = ContentLayoutSolver.Resolve(d, View(354)); var small = ContentLayoutSolver.Resolve(d, View(114));
        Assert.Equal(12, small.Items[0].ColumnSpan);
        Assert.Equal(ContentLayoutSpaceStatus.InsufficientCrossAxis, small.SpaceStatus);
        Assert.True(small.IsLayoutReadOnly);
        Assert.DoesNotContain(small.Diagnostics, diagnostic => diagnostic.Contains("squeeze"));
        for (int i = 0; i < 100; i++)
        {
            ContentLayoutSolver.Resolve(d, View(114));
            Assert.Equal(wide.Items, ContentLayoutSolver.Resolve(d, View(354)).Items);
        }
        Assert.Equal(12, d.Placements[0].ColumnSpan);
    }

    [Theory]
    [InlineData(ContentLayoutMode.Manual)] [InlineData(ContentLayoutMode.Anchored)]
    public void Manual_or_fixed_keeps_authored_width_and_reports_compatibility_conflict(ContentLayoutMode mode)
    {
        var d = Doc(1) with { Mode = mode };
        d = d with { Placements = [d.Placements[0] with { Column = 8, Row = 0, FixedPosition = true }] };
        var s = ContentLayoutSolver.Resolve(d, View(100,100) with { ScrollXDip = 240 });
        Assert.Equal(354, s.ExtentWidthDip); Assert.Equal(8, s.Items[0].Column); Assert.Equal(4, s.Items[0].ColumnSpan);
        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, s.SpaceStatus);
        Assert.True(s.IsLayoutReadOnly);
        // A default single-axis projection cannot claim that an incompatible cross-axis item is reachable.
        Assert.Equal(0, s.ScrollXDip);
        Assert.Null(s.HitTest(1, 1)); Assert.Null(s.HitTest(100, 1)); Safe(s);
    }

    [Fact]
    public void All_regions_share_occupancy_and_future_fixed_card_is_reserved_before_flow()
    {
        var d = Doc(2) with { Mode = ContentLayoutMode.Anchored, Regions = [new("main",0),new("other",1)] };
        d = d with { Placements = [d.Placements[0], d.Placements[1] with { RegionId="other", FixedPosition=true,Column=0,Row=0 }] };
        var s = ContentLayoutSolver.Resolve(d, View(114));
        Assert.Equal(4, s.Items[0].Row); Assert.Equal(0, s.Items[1].Row); Safe(s);
    }

    [Theory]
    [InlineData(114)]
    [InlineData(234)]
    public void Legacy_regions_keep_origins_and_report_conflict_without_rewriting_global_pins(double width)
    {
        var d = Doc(4) with { Mode=ContentLayoutMode.Anchored, Regions=[new("main",0,0,4),new("side",1,4,4)] };
        d = d with { Placements=d.Placements.Select((p,i)=>p with { RegionId=i<2?"main":"side", PinnedIndex=i==3?1:null }).ToArray() };
        var s = ContentLayoutSolver.Resolve(d, View(width));
        Assert.Equal(d.Placements[3].ItemId,s.Items[0].ItemId);
        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, s.SpaceStatus);
        Assert.DoesNotContain("region-folded:side", s.Diagnostics);
        Assert.Equal(4,s.Items[0].RegionColumn); Assert.Equal(4,d.Regions[1].Column); Safe(s);
    }

    [Fact]
    public void Reserved_blank_has_extent_even_without_items_and_cards_cannot_fill_it()
    {
        var d = Doc(0) with { ReservedSpaces=[new(0,0,8,8)] };
        var s = ContentLayoutSolver.Resolve(d, View()); Assert.Empty(s.Items); Assert.Equal(234,s.ExtentWidthDip); Assert.Equal(234,s.ExtentHeightDip);
        d = Doc(1) with { ReservedSpaces=d.ReservedSpaces };
        s = ContentLayoutSolver.Resolve(d,View(234)); Assert.Equal(8,s.Items[0].Row); Safe(s);
    }

    [Fact]
    public void Fixed_overlap_reserved_and_row_exhaustion_reject_without_partial_result()
    {
        var d=Doc(1) with { Mode=ContentLayoutMode.Manual,ReservedSpaces=[new(0,0,4,4)] };
        d=d with { Placements=[d.Placements[0] with { Column=0,Row=0 }] };
        Assert.Throws<ContentLayoutException>(()=>ContentLayoutSolver.Resolve(d,View()));
        Assert.Throws<ContentLayoutException>(()=>ContentLayoutSolver.Resolve(d with { ReservedSpaces=[],Placements=[d.Placements[0] with { Row=65535 }] },View()));
    }

    [Fact]
    public void Reclip_is_geometry_stable_and_hit_testing_uses_only_visible_intersection()
    {
        var a=ContentLayoutSolver.Resolve(Doc(),View(234,150));
        var b=ContentLayoutSolver.Reclip(a,View(234,40) with { ScrollYDip=120,Generation=9 });
        Assert.Equal(a.Items.Select(p=>p.Bounds),b.Items.Select(p=>p.Bounds)); Assert.Equal(a.ExtentHeightDip,b.ExtentHeightDip);
        Assert.Equal(9,b.ViewportGeneration); Assert.Equal(40,b.Items[2].Clip.Height); Assert.Equal(b.Items[2].ItemId,b.HitTest(2,2)!.ItemId);
        Assert.Null(b.HitTest(2,40)); Assert.Null(b.HitTest(115,2));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Reclip_rejects_stale_cross_axis_or_metric_witness(ContentFlowAxis axis)
    {
        var viewport = View(234, 234);
        var snapshot = ContentLayoutSolver.Resolve(Doc() with { FlowAxis = axis }, viewport);
        var crossChanged = axis == ContentFlowAxis.Vertical ? viewport with { WidthDip = 114 } : viewport with { HeightDip = 114 };
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Reclip(snapshot, crossChanged));
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Reclip(snapshot, viewport with { Scale = 1.2 }));
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Reclip(snapshot, viewport with { Spacing = 1.2 }));
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Reclip(snapshot, viewport with { ThemeMetricsVersion = 2 }));
        var mainChanged = axis == ContentFlowAxis.Vertical ? viewport with { HeightDip = 114 } : viewport with { WidthDip = 114 };
        Assert.Equal(snapshot.Items.Select(i => i.Bounds), ContentLayoutSolver.Reclip(snapshot, mainChanged).Items.Select(i => i.Bounds));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Minimum_cross_boundary_cannot_relabel_nonzero_legacy_cross_position_as_normal(ContentFlowAxis axis)
    {
        var a = new ContentPlacement("A:a", 0, axis == ContentFlowAxis.Vertical ? 4 : 2, axis == ContentFlowAxis.Vertical ? 2 : 4)
            { Column = 0, Row = 0, FixedPosition = true };
        var b = new ContentPlacement("A:b", 1, 2, 2) { Column = 2, Row = 2, FixedPosition = true };
        var doc = new ContentLayoutDocument("A", [a, b])
            { Mode = ContentLayoutMode.Anchored, BaseCellDip = 48, BaseGapDip = 0, FlowAxis = axis };
        string original = ContentLayoutPolicy.Serialize(doc);
        var viewport = axis == ContentFlowAxis.Vertical ? View(192, 500) : View(500, 192);
        var snapshot = ContentLayoutSolver.Resolve(doc, viewport);
        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, snapshot.SpaceStatus);
        Assert.Equal(2, snapshot.Items[1].Column);
        Assert.Equal(2, snapshot.Items[1].Row);
        Assert.Equal(original, ContentLayoutPolicy.Serialize(doc));
    }

    [Fact]
    public void Scroll_anchor_tracks_identity_inside_offset_and_deleted_item_uses_next_order()
    {
        var d=Doc(10); var a=ContentLayoutSolver.Resolve(d,View(234,60) with { ScrollYDip=125 });
        var anchor=a.Anchor()!; var b=ContentLayoutSolver.Resolve(d,View(114,60),anchor);
        Assert.Equal(b.Items.Single(p=>p.ItemId==anchor.ItemId).Bounds.Y+5,b.ScrollYDip);
        var removed=d with { Placements=d.Placements.Where(p=>p.ItemId!=anchor.ItemId).ToArray() };
        var c=ContentLayoutSolver.Resolve(removed,View(114,60),anchor);
        Assert.Equal(c.Items.First(p=>p.ReadOrder>=anchor.PreviousReadOrder).Bounds.Y,c.ScrollYDip);
    }

    [Fact]
    public void Zero_viewport_is_valid_and_cannot_hit_any_item()
    {
        var s=ContentLayoutSolver.Resolve(Doc(),View(0,0)); Assert.All(s.Items,p=>Assert.True(p.Clip.IsEmpty)); Assert.Null(s.HitTest(0,0)); Safe(s);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Both_axes_support_one_thousand_maximum_span_items_without_squeezing(ContentFlowAxis axis)
    {
        var document = Doc(1000) with { FlowAxis = axis };
        document = document with { Placements = document.Placements.Select(p => p with { ColumnSpan = 32, RowSpan = 32 }).ToArray() };
        string authored = ContentLayoutPolicy.Serialize(document);
        var viewport = axis == ContentFlowAxis.Vertical ? View(954, 100) : View(100, 954);

        var result = ContentLayoutSolver.Resolve(document, viewport);

        Assert.Equal(ContentLayoutSpaceStatus.Normal, result.SpaceStatus);
        Assert.Equal(1000, result.Items.Count);
        Assert.Equal(32, result.CrossAxisCells);
        Assert.Equal(954, result.MinimumCrossAxisDip);
        Assert.Equal(32000 * 30 - 6, axis == ContentFlowAxis.Vertical ? result.ExtentHeightDip : result.ExtentWidthDip);
        Assert.All(result.Items, item => { Assert.Equal(32, item.ColumnSpan); Assert.Equal(32, item.RowSpan); });
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Minimum_cross_boundary_has_one_item_per_band_even_when_small_items_could_share(ContentFlowAxis axis)
    {
        var document = Doc(3) with { FlowAxis = axis };
        document = document with { Placements = document.Placements.Select((p, i) => p with
            { ColumnSpan = i == 0 ? 4 : 2, RowSpan = i == 0 ? 4 : 2 }).ToArray() };
        var result = ContentLayoutSolver.Resolve(document,
            axis == ContentFlowAxis.Vertical ? View(114, 500) : View(500, 114));

        Assert.Equal(ContentLayoutSpaceStatus.Normal, result.SpaceStatus);
        Assert.Equal(new[] { 0, 4, 6 }, result.Items.Select(i => axis == ContentFlowAxis.Vertical ? i.Row : i.Column));
        Assert.All(result.Items, i => Assert.Equal(0, axis == ContentFlowAxis.Vertical ? i.Column : i.Row));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Insufficient_cross_axis_is_explicit_preserves_spans_and_does_not_invent_viewport(ContentFlowAxis axis)
    {
        var document = Doc(1) with { FlowAxis = axis };
        document = document with { Placements = [document.Placements[0] with { ColumnSpan = 12, RowSpan = 12 }] };
        var viewport = View(114, 114);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);

        Assert.Equal(ContentLayoutSpaceStatus.InsufficientCrossAxis, snapshot.SpaceStatus);
        Assert.True(snapshot.IsLayoutReadOnly);
        Assert.Equal(354, snapshot.MinimumCrossAxisDip);
        Assert.Equal(114, snapshot.AvailableCrossAxisDip);
        Assert.Equal(12, snapshot.Items[0].ColumnSpan);
        Assert.Equal(12, snapshot.Items[0].RowSpan);
        Assert.True(snapshot.Items[0].Clip.Width <= viewport.WidthDip && snapshot.Items[0].Clip.Height <= viewport.HeightDip);
        Assert.DoesNotContain(snapshot.Diagnostics, d => d.Contains("squeeze") || d.Contains("viewport-height-clamped"));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Legacy_coordinate_overflow_is_not_reclassified_as_passive_item_shortage(ContentFlowAxis axis)
    {
        var document = Doc(1) with { FlowAxis = axis, Mode = ContentLayoutMode.Anchored };
        document = document with { Placements = [document.Placements[0] with
            { Column = axis == ContentFlowAxis.Vertical ? 6 : 0, Row = axis == ContentFlowAxis.Vertical ? 0 : 6,
                ColumnSpan = 2, RowSpan = 2, FixedPosition = true }] };
        string authored = ContentLayoutPolicy.Serialize(document);
        var snapshot = ContentLayoutSolver.Resolve(document, axis == ContentFlowAxis.Vertical ? View(54, 300) : View(300, 54));

        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, snapshot.SpaceStatus);
        Assert.True(snapshot.IsLayoutReadOnly);
        Assert.Equal(54, snapshot.MinimumCrossAxisDip);
        Assert.Equal(6, axis == ContentFlowAxis.Vertical ? snapshot.Items[0].Column : snapshot.Items[0].Row);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Legacy_region_coordinates_keep_their_physical_meaning_on_both_axes(ContentFlowAxis axis)
    {
        var document = Doc(2) with
        {
            FlowAxis = axis, Mode = ContentLayoutMode.Manual,
            Regions = [new("main", 0), new("side", 1, Column: 4, Columns: 6, Row: 2)]
        };
        document = document with { Placements =
        [
            document.Placements[0] with { Column = 0, Row = 0, ColumnSpan = 2, RowSpan = 2 },
            document.Placements[1] with { RegionId = "side", Column = 1, Row = 1, ColumnSpan = 2, RowSpan = 2 }
        ] };
        string authored = ContentLayoutPolicy.Serialize(document);

        var snapshot = ContentLayoutSolver.Resolve(document, View(1000, 1000));
        var side = snapshot.Items.Single(item => item.ItemId == document.Placements[1].ItemId);

        Assert.Equal(5, side.Column); Assert.Equal(3, side.Row);
        Assert.Equal(4, side.RegionColumn); Assert.Equal(2, side.RegionRow);
        Assert.Equal(2, side.ColumnSpan); Assert.Equal(2, side.RowSpan);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
        Safe(snapshot);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Explicit_space_overflow_preserves_constraint_and_does_not_raise_item_minimum(ContentFlowAxis axis)
    {
        var document = Doc(1) with { FlowAxis = axis };
        document = document with
        {
            Placements = [document.Placements[0] with { ColumnSpan = 2, RowSpan = 2 }],
            ReservedSpaces =
            [
                new(axis == ContentFlowAxis.Vertical ? 6 : 0, axis == ContentFlowAxis.Vertical ? 0 : 6, 2, 2)
                    { Kind = ContentReservedKind.Explicit, Name = "authored keep-out" },
                new(0, 0, 2, 2) { Kind = ContentReservedKind.Decorative, Name = "non-blocking ornament" }
            ]
        };
        string authored = ContentLayoutPolicy.Serialize(document);
        var snapshot = ContentLayoutSolver.Resolve(document,
            axis == ContentFlowAxis.Vertical ? View(54, 500) : View(500, 54));

        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, snapshot.SpaceStatus);
        Assert.Equal(54, snapshot.MinimumCrossAxisDip);
        Assert.Equal(54, snapshot.AvailableCrossAxisDip);
        Assert.Equal(0, snapshot.Items[0].Column); Assert.Equal(0, snapshot.Items[0].Row);
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
        Safe(snapshot);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Configured_capacity_conflict_is_rejected_despite_ample_physical_space(ContentFlowAxis axis)
    {
        var document = Doc(1) with { FlowAxis = axis, MaximumColumns = 2 };
        string authored = ContentLayoutPolicy.Serialize(document);
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(document, View(1000, 1000)));
        Assert.Equal(authored, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Empty_content_has_no_fabricated_item_minimum(ContentFlowAxis axis)
    {
        var snapshot = ContentLayoutSolver.Resolve(Doc(0) with { FlowAxis = axis }, View(0, 0));
        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.MinimumCrossAxisDip);
        Assert.Equal(ContentLayoutSpaceStatus.Normal, snapshot.SpaceStatus);
    }
}
