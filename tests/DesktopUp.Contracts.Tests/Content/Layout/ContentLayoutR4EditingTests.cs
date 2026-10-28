using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutR4EditingTests
{
    private static ContentLayoutDocument Auto(int count = 3) => new("r4", Enumerable.Range(0, count)
        .Select(i => new ContentPlacement("r4:item-" + i, i, 2, 2)).ToArray())
        { SchemaVersion = 2, BaseCellDip = 48, BaseGapDip = 0 };
    private static ContentLayoutViewport View(double width = 384, double height = 384) => new(width, height, Generation: 7);
    private static ContentLayoutDocument Manual() => Auto() with
    {
        Mode = ContentLayoutMode.Manual,
        Placements =
        [
            new("r4:item-0", 0, 4, 2) { Column = 0, Row = 0, ManualSize = true },
            new("r4:item-1", 1, 2, 2) { Column = 6, Row = 0, ManualSize = true },
            new("r4:item-2", 2, 2, 1) { Column = 0, Row = 4, ManualSize = true }
        ]
    };

    [Fact]
    public void Enter_manual_captures_every_presented_member_but_saves_automatic_author_intent()
    {
        var author = Auto(6) with { Mode = ContentLayoutMode.Anchored };
        author = author with { Placements = author.Placements.Select((p, i) => p with
            { PinnedIndex = i == 0 ? 1 : null, ManualSize = i == 0, ColumnSpan = i == 0 ? 3 : 2 }).ToArray() };
        var effective = author with { Placements = author.Placements.Select((p, i) => i == 0 ? p : p with
            { Order = 9 - i, ColumnSpan = 4, RowSpan = 3 }).ToArray() };
        var viewport = View(height: 100);
        var displayed = ContentLayoutSolver.Resolve(effective, viewport);

        var result = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, viewport, displayed);

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentLayoutPolicy.SupportedSchemaVersion, result.Document.SchemaVersion);
        Assert.Equal(ContentLayoutMode.Anchored, result.Document.SavedAutomaticMode);
        Assert.Equal(author.Placements.Count, result.Document.Placements.Count);
        foreach (var placement in result.Document.Placements)
        {
            var prior = author.Placements.Single(p => p.ItemId == placement.ItemId);
            var slot = displayed.Items.Single(p => p.ItemId == placement.ItemId);
            Assert.Equal(slot.Column, placement.Column); Assert.Equal(slot.Row, placement.Row);
            Assert.Equal(slot.ColumnSpan, placement.ColumnSpan); Assert.Equal(slot.RowSpan, placement.RowSpan);
            Assert.Equal(prior.ColumnSpan, placement.SavedAutomaticState!.ColumnSpan);
            Assert.Equal(prior.RowSpan, placement.SavedAutomaticState.RowSpan);
            Assert.Equal(prior.PinnedIndex, placement.SavedAutomaticState.PinnedIndex);
            Assert.Equal(prior.Order, placement.SavedAutomaticState.Order);
        }
    }

    [Fact]
    public void Manual_exit_A_restores_automatic_overrides_and_reentry_uses_the_new_current_picture()
    {
        var author = Auto() with { Mode = ContentLayoutMode.Anchored };
        author = author with { Placements = author.Placements.Select((p, i) => i == 0 ? p with
            { PinnedIndex = 1, ManualSize = true, ColumnSpan = 3 } : p).ToArray() };
        var entered = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }, View());
        Assert.True(entered.Accepted, entered.Error);
        var resized = ContentLayoutEditing.Apply(entered.Document, new(ContentLayoutEditKind.Resize, "r4:item-0")
            { ColumnSpan = 4, RowSpan = 3 }, View());
        Assert.True(resized.Accepted, resized.Error);

        var exited = ContentLayoutEditing.Apply(resized.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Automatic }, View());

        Assert.True(exited.Accepted, exited.Error);
        Assert.Equal(ContentLayoutMode.Anchored, exited.Document.Mode);
        Assert.Null(exited.Document.SavedAutomaticMode);
        var restored = exited.Document.Placements[0];
        Assert.Equal(3, restored.ColumnSpan); Assert.Equal(2, restored.RowSpan);
        Assert.Equal(1, restored.PinnedIndex); Assert.True(restored.ManualSize);
        Assert.All(exited.Document.Placements, p => Assert.Null(p.SavedAutomaticState));
        var reentered = ContentLayoutEditing.Apply(exited.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, View());
        Assert.True(reentered.Accepted, reentered.Error);
        Assert.Equal(3, reentered.Document.Placements[0].ColumnSpan);
        Assert.Equal(2, reentered.Document.Placements[0].RowSpan);
    }

    [Fact]
    public void Automatic_drag_changes_only_the_targets_order_pin_without_creating_hard_coordinates()
    {
        var author = Auto();
        var result = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-2")
            { TargetReadOrder = 0 }, View());
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(ContentLayoutMode.Automatic, result.Document.Mode);
        Assert.Equal(ContentLayoutPolicy.SupportedSchemaVersion, result.Document.SchemaVersion);
        Assert.Equal(1, result.Document.Placements[2].PinnedIndex);
        Assert.All(result.Document.Placements, p => { Assert.False(p.FixedPosition); Assert.Null(p.Column); Assert.Null(p.Row); });
        Assert.Equal(author.Placements[0], result.Document.Placements[0]);
        Assert.Equal(author.Placements[1], result.Document.Placements[1]);
        Assert.Equal("r4:item-2", ContentLayoutSolver.Resolve(result.Document, View()).Items[0].ItemId);
    }

    [Fact]
    public void Automatic_drag_skips_other_pins_and_noop_does_not_upgrade_or_add_a_pin()
    {
        var author = Auto(4) with { Mode = ContentLayoutMode.Anchored };
        author = author with { Placements = author.Placements.Select((p, i) => p with
            { PinnedIndex = i < 2 ? i + 1 : null }).ToArray() };
        var result = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-3")
            { TargetReadOrder = 0 }, View());
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(3, result.Document.Placements[3].PinnedIndex);
        Assert.Equal(1, result.Document.Placements[0].PinnedIndex);
        Assert.Equal(2, result.Document.Placements[1].PinnedIndex);
        var plain = Auto();
        var noop = ContentLayoutEditing.Apply(plain, new(ContentLayoutEditKind.Move, "r4:item-1")
            { TargetReadOrder = 1 }, View());
        Assert.True(noop.Accepted, noop.Error); Assert.Same(plain, noop.Document);
        Assert.Null(noop.Document.Placements[1].PinnedIndex);
    }

    [Fact]
    public void Legacy_manual_can_edit_in_mode_but_cannot_invent_an_automatic_return_state()
    {
        var legacy = Manual();
        var edited = ContentLayoutEditing.Apply(legacy, new(ContentLayoutEditKind.Move, "r4:item-2")
            { Column = 0, Row = 7 }, View());
        Assert.True(edited.Accepted, edited.Error);
        Assert.Null(edited.Document.SavedAutomaticMode);
        var exited = ContentLayoutEditing.Apply(edited.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Automatic }, View());
        Assert.False(exited.Accepted); Assert.Same(edited.Document, exited.Document);
    }

    [Fact]
    public void Valid_manual_edit_adopts_the_entire_adapted_picture_once_but_noop_does_not()
    {
        var author = Manual();
        var narrow = View(width: 192);
        var noop = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-2")
            { Column = 0, Row = 4 }, narrow);
        Assert.True(noop.Accepted, noop.Error); Assert.Same(author, noop.Document);

        var moved = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-2")
            { Column = 0, Row = 7 }, narrow);

        Assert.True(moved.Accepted, moved.Error); Assert.True(moved.AdoptsProjection);
        Assert.Equal(author.DocumentVersion + 1, moved.Document.DocumentVersion);
        Assert.Equal(0, moved.Document.Placements[1].Column); Assert.Equal(2, moved.Document.Placements[1].Row);
        Assert.Equal(7, moved.Document.Placements[2].Row);
        var widened = ContentLayoutSolver.Resolve(moved.Document, View());
        Assert.Equal(0, widened.Items.Single(i => i.ItemId == "r4:item-1").Column);
    }

    [Fact]
    public void Horizontal_manual_move_can_save_long_main_coordinates_in_schema_three()
    {
        var author = Auto(2) with { Mode = ContentLayoutMode.Manual, FlowAxis = ContentFlowAxis.HorizontalLoop };
        author = author with { Placements = author.Placements.Select((p, i) => p with
            { Column = i * 4, Row = 0, ManualSize = true }).ToArray() };
        var moved = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-1")
            { Column = 120, Row = 0 }, View());
        Assert.True(moved.Accepted, moved.Error);
        Assert.Equal(ContentLayoutPolicy.SupportedSchemaVersion, moved.Document.SchemaVersion); Assert.Equal(120, moved.Document.Placements[1].Column);
        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(moved.Document), "r4", out var read, out var error), error);
        Assert.Equal(120, read!.Placements[1].Column);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("version")]
    [InlineData("membership")]
    [InlineData("generation")]
    [InlineData("readonly")]
    public void Invalid_presented_basis_never_partially_enters_manual(string kind)
    {
        var author = Auto(); var view = View();
        var snapshot = ContentLayoutSolver.Resolve(author, view);
        snapshot = kind switch
        {
            "identity" => snapshot with { PosterId = "other" },
            "version" => snapshot with { DocumentVersion = 99 },
            "membership" => snapshot with { Items = snapshot.Items.Take(1).ToArray() },
            "generation" => snapshot with { ViewportGeneration = 99 },
            _ => snapshot with { SpaceStatus = ContentLayoutSpaceStatus.InsufficientCrossAxis }
        };
        var result = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, view, snapshot);
        Assert.False(result.Accepted); Assert.Same(author, result.Document);
    }

    [Fact]
    public void Payload_missing_schema_is_not_promoted_to_the_latest_version()
    {
        string json = ContentLayoutPolicy.Serialize(Auto()).Replace("\"schemaVersion\":2,", "", StringComparison.Ordinal);
        Assert.False(ContentLayoutPolicy.TryRead(json, "r4", out _, out _));
    }

    [Fact]
    public void Automatic_size_override_retains_the_original_basis_across_repeated_resize_and_manual_roundtrip()
    {
        var author = Auto();
        var first = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Resize, "r4:item-0")
            { ColumnSpan = 3, RowSpan = 2 }, View());
        Assert.True(first.Accepted, first.Error);
        Assert.Equal(new ContentAutomaticSizeBasis(2, 2), first.Document.Placements[0].AutomaticSizeBasis);
        var second = ContentLayoutEditing.Apply(first.Document, new(ContentLayoutEditKind.Resize, "r4:item-0")
            { ColumnSpan = 4, RowSpan = 3 }, View());
        Assert.True(second.Accepted, second.Error);
        var entered = ContentLayoutEditing.Apply(second.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, View());
        Assert.True(entered.Accepted, entered.Error);
        Assert.Equal(new ContentAutomaticSizeBasis(2, 2), entered.Document.Placements[0].SavedAutomaticState!.AutomaticSizeBasis);
        var exited = ContentLayoutEditing.Apply(entered.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Automatic }, View());
        Assert.True(exited.Accepted, exited.Error);
        var restored = ContentLayoutEditing.Apply(exited.Document, new(ContentLayoutEditKind.SizeOverride, "r4:item-0")
            { Enabled = false }, View());
        Assert.True(restored.Accepted, restored.Error);
        Assert.Equal(2, restored.Document.Placements[0].ColumnSpan);
        Assert.Equal(2, restored.Document.Placements[0].RowSpan);
        Assert.False(restored.Document.Placements[0].ManualSize);
        Assert.Null(restored.Document.Placements[0].AutomaticSizeBasis);
        Assert.Equal(author.Placements[1], restored.Document.Placements[1] with { Version = author.Placements[1].Version });
    }

    [Fact]
    public void Legacy_manual_size_without_basis_rejects_restore_and_plain_automatic_restore_is_a_noop()
    {
        var author = Auto();
        var noop = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.SizeOverride, "r4:item-0")
            { Enabled = false }, View());
        Assert.True(noop.Accepted, noop.Error); Assert.Same(author, noop.Document);
        var legacy = author with { Placements = author.Placements.Select((p, i) => i == 0 ? p with
            { ManualSize = true, ColumnSpan = 3 } : p).ToArray() };
        var rejected = ContentLayoutEditing.Apply(legacy, new(ContentLayoutEditKind.SizeOverride, "r4:item-0")
            { Enabled = false }, View());
        Assert.False(rejected.Accepted); Assert.Same(legacy, rejected.Document);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Manual_collision_push_is_axis_symmetric_and_preserves_unaffected_peers(ContentFlowAxis axis)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        var author = Auto(3) with { Mode = ContentLayoutMode.Manual, FlowAxis = axis };
        author = author with { Placements = author.Placements.Select((p, i) => p with
            { Column = vertical ? i * 2 : 0, Row = vertical ? 0 : i * 2, ManualSize = true }).ToArray() };
        var result = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Move, "r4:item-0")
            { Column = vertical ? 2 : 0, Row = vertical ? 0 : 2 }, View());
        Assert.True(result.Accepted, result.Error);
        var source = result.Document.Placements[0];
        var pushed = result.Document.Placements[1];
        Assert.Equal(vertical ? 2 : 0, source.Column); Assert.Equal(vertical ? 0 : 2, source.Row);
        Assert.Equal(2, pushed.Column); Assert.Equal(2, pushed.Row);
        Assert.Equal(author.Placements[2], result.Document.Placements[2]);
        Assert.Equal(author.DocumentVersion + 1, result.Document.DocumentVersion);
    }

    [Fact]
    public void Legacy_automatic_dormant_pins_are_not_activated_by_reading_or_silent_upgrade()
    {
        var old = Auto() with { Placements = Auto().Placements.Select((p, i) => i == 2 ? p with { PinnedIndex = 1 } : p).ToArray() };
        Assert.Equal("r4:item-0", ContentLayoutSolver.Resolve(old, View()).Items[0].ItemId);
        var edit = ContentLayoutEditing.Apply(old, new(ContentLayoutEditKind.Resize, "r4:item-0")
            { ColumnSpan = 3, RowSpan = 2 }, View());
        Assert.False(edit.Accepted); Assert.Same(old, edit.Document);
        var current = old with { SchemaVersion = 3 };
        Assert.Equal("r4:item-2", ContentLayoutSolver.Resolve(current, View()).Items[0].ItemId);
    }

    [Fact]
    public void Return_state_requires_schema_three_and_all_manual_members()
    {
        var manual = Manual();
        var states = manual.Placements.Select(p => p with
            { SavedAutomaticState = new(p.Order, 2, 2, false, null) }).ToArray();
        var complete = manual with { SchemaVersion = 3, SavedAutomaticMode = ContentLayoutMode.Automatic, Placements = states };
        Assert.Null(ContentLayoutPolicy.Validate(complete));
        Assert.NotNull(ContentLayoutPolicy.Validate(complete with { SchemaVersion = 2 }));
        Assert.NotNull(ContentLayoutPolicy.Validate(complete with { Mode = ContentLayoutMode.Automatic }));
        Assert.NotNull(ContentLayoutPolicy.Validate(complete with { SavedAutomaticMode = null }));
        Assert.NotNull(ContentLayoutPolicy.Validate(complete with { Placements = states.Select((p, i) => i == 0 ? p with
            { SavedAutomaticState = null } : p).ToArray() }));
        Assert.NotNull(ContentLayoutPolicy.Validate(Auto() with { SchemaVersion = 3, Placements = Auto().Placements.Select((p, i) =>
            i == 0 ? p with { AutomaticSizeBasis = new(2, 2) } : p).ToArray() }));
    }

    [Fact]
    public void Enabling_size_override_freezes_presented_heat_size_but_restore_uses_author_basis_and_keeps_pin()
    {
        var author = Auto() with { SchemaVersion = 3 };
        author = author with { Placements = author.Placements.Select((p, i) => i == 0 ? p with { PinnedIndex = 1 } : p).ToArray() };
        var effective = author with { Placements = author.Placements.Select((p, i) => i == 0 ? p with
            { ColumnSpan = 4, RowSpan = 3 } : p).ToArray() };
        var snapshot = ContentLayoutSolver.Resolve(effective, View());
        var frozen = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.SizeOverride, "r4:item-0")
            { Enabled = true }, View(), snapshot);
        Assert.True(frozen.Accepted, frozen.Error);
        Assert.Equal(4, frozen.Document.Placements[0].ColumnSpan);
        Assert.Equal(3, frozen.Document.Placements[0].RowSpan);
        Assert.Equal(new ContentAutomaticSizeBasis(2, 2), frozen.Document.Placements[0].AutomaticSizeBasis);
        var restored = ContentLayoutEditing.Apply(frozen.Document, new(ContentLayoutEditKind.SizeOverride, "r4:item-0")
            { Enabled = false }, View());
        Assert.True(restored.Accepted, restored.Error);
        Assert.Equal(2, restored.Document.Placements[0].ColumnSpan);
        Assert.Equal(2, restored.Document.Placements[0].RowSpan);
        Assert.Equal(1, restored.Document.Placements[0].PinnedIndex);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"3\"")]
    [InlineData("{}")]
    [InlineData("3.5")]
    public void Invalid_source_schema_kind_is_a_read_failure_not_an_exception(string schema)
    {
        string json = ContentLayoutPolicy.Serialize(Auto()).Replace("\"schemaVersion\":2", "\"schemaVersion\":" + schema, StringComparison.Ordinal);
        Assert.False(ContentLayoutPolicy.TryRead(json, "r4", out _, out _));
    }
}
