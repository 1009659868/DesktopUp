using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutFlowPolicyTests
{
    private static ContentLayoutDocument Doc(int count = 3) => ContentLayoutPolicy.Freeze(
        new ContentLayoutDocument("p", Array.AsReadOnly(Enumerable.Range(0, count)
            .Select(i => new ContentPlacement("p:item-" + i.ToString("D4", CultureInfo.InvariantCulture), i)).ToArray())));

    [Fact]
    public void Setting_axis_advances_one_version_and_keeps_every_placement()
    {
        var d = Doc();
        var result = ContentLayoutFlowPolicy.SetFlowAxis(d, ContentFlowAxis.HorizontalLoop);

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentFlowAxis.HorizontalLoop, result.Document.FlowAxis);
        Assert.Equal(d.DocumentVersion + 1, result.Document.DocumentVersion);
        Assert.Equal(d.Placements, result.Document.Placements);
        Assert.Equal(d.ItemSetVersion, result.Document.ItemSetVersion);
    }

    [Fact]
    public void Same_axis_is_a_no_op_that_consumes_no_version()
    {
        var d = Doc();
        var result = ContentLayoutFlowPolicy.SetFlowAxis(d, ContentFlowAxis.Vertical);

        Assert.True(result.Accepted, result.Error);
        Assert.Same(d, result.Document);
    }

    [Fact]
    public void Heat_driven_size_is_opt_in_and_reversible()
    {
        var d = Doc();
        var on = ContentLayoutFlowPolicy.SetHeatDrivenSize(d, true);
        Assert.True(on.Accepted, on.Error);
        Assert.True(on.Document.AllowHeatDrivenSize);

        var off = ContentLayoutFlowPolicy.SetHeatDrivenSize(on.Document, false);
        Assert.True(off.Accepted, off.Error);
        Assert.False(off.Document.AllowHeatDrivenSize);
        Assert.Equal(d.DocumentVersion + 2, off.Document.DocumentVersion);
    }

    [Fact]
    public void LoopScrollIsIndependentOfTheAxisAndReversible()
    {
        var d = Doc();
        var on = ContentLayoutFlowPolicy.SetLoopScroll(d, true);
        Assert.True(on.Accepted, on.Error);
        Assert.True(on.Document.LoopScroll);
        // The switch never changes the axis, sizes or order.
        Assert.Equal(ContentFlowAxis.Vertical, on.Document.FlowAxis);
        Assert.Equal(d.Placements, on.Document.Placements);

        var off = ContentLayoutFlowPolicy.SetLoopScroll(on.Document, false);
        Assert.True(off.Accepted, off.Error);
        Assert.False(off.Document.LoopScroll);
        Assert.Equal(d.DocumentVersion + 2, off.Document.DocumentVersion);
    }

    [Fact]
    public void Manual_axis_switch_preserves_mode_physical_authoring_and_independent_loop_in_one_version()
    {
        // DEC-025: axis changes do not convert mode, rotate physical cards or alter loop preference.
        var d = Doc() with
        {
            Mode = ContentLayoutMode.Manual,
            LoopScroll = true,
            Placements = Array.AsReadOnly(new[]
            {
                new ContentPlacement("p:item-0000", 0) { Column = 0, Row = 0, ColumnSpan = 4, RowSpan = 4 },
                new ContentPlacement("p:item-0001", 1) { Column = 4, Row = 0, ColumnSpan = 4, RowSpan = 4 },
                new ContentPlacement("p:item-0002", 2) { Column = 0, Row = 4, ColumnSpan = 4, RowSpan = 4 }
            })
        };

        var result = ContentLayoutFlowPolicy.SetFlowAxis(d, ContentFlowAxis.HorizontalLoop);
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentFlowAxis.HorizontalLoop, result.Document.FlowAxis);
        Assert.Equal(ContentLayoutMode.Manual, result.Document.Mode);
        Assert.Equal(d.Placements, result.Document.Placements);
        Assert.Equal(d.SavedAutomaticMode, result.Document.SavedAutomaticMode);
        Assert.Equal(d.DocumentVersion + 1, result.Document.DocumentVersion);
        Assert.Equal(d.ItemSetVersion, result.Document.ItemSetVersion);
        Assert.True(result.Document.LoopScroll);
    }

    [Fact]
    public void Axis_switch_reports_conflicting_pins_instead_of_clearing_them()
    {
        // Fixed rows beyond the canonical 64-cell cross-axis capacity: a hard position is an explicit
        // intent, so the switch is reported rather than relocating or unpinning the user's cards.
        var d = Doc() with
        {
            Mode = ContentLayoutMode.Anchored,
            Placements = Array.AsReadOnly(new[]
            {
                new ContentPlacement("p:item-0000", 0) { Column = 0, Row = 50, FixedPosition = true },
                new ContentPlacement("p:item-0001", 1) { Column = 0, Row = 58, FixedPosition = true },
                new ContentPlacement("p:item-0002", 2) { Column = 0, Row = 63, FixedPosition = true }
            })
        };

        Assert.NotNull(ContentLayoutFlowPolicy.CanSetFlowAxis(d, ContentFlowAxis.HorizontalLoop));
        var result = ContentLayoutFlowPolicy.SetFlowAxis(d, ContentFlowAxis.HorizontalLoop);
        Assert.False(result.Accepted);
        Assert.NotNull(result.Error);
        Assert.Equal(d.FlowAxis, result.Document.FlowAxis);
        Assert.Equal(d.DocumentVersion, result.Document.DocumentVersion);
        Assert.All(result.Document.Placements, p => Assert.True(p.FixedPosition));
    }

    [Fact]
    public void Axis_switch_preserves_complete_manual_return_intent_without_consuming_item_versions()
    {
        var automatic = Doc();
        var entered = ContentLayoutEditing.Apply(automatic, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, new(354, 180));
        Assert.True(entered.Accepted, entered.Error);
        var before = entered.Document;
        Assert.NotNull(before.SavedAutomaticMode);
        Assert.All(before.Placements, p => Assert.NotNull(p.SavedAutomaticState));
        var changed = ContentLayoutFlowPolicy.SetFlowAxis(before, ContentFlowAxis.HorizontalLoop);
        Assert.True(changed.Accepted, changed.Error);
        Assert.Equal(ContentLayoutMode.Manual, changed.Document.Mode);
        Assert.Equal(before.Placements, changed.Document.Placements);
        Assert.Equal(before.SavedAutomaticMode, changed.Document.SavedAutomaticMode);
        Assert.Equal(before.DocumentVersion + 1, changed.Document.DocumentVersion);
        Assert.Equal(before.LoopScroll, changed.Document.LoopScroll);
    }
}
