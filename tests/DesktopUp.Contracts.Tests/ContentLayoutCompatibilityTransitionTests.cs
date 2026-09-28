using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Theory]
    [InlineData("legacy-anchor-to-automatic")]
    [InlineData("automatic-schema-downgrade")]
    [InlineData("dormant-pin-to-anchor")]
    public void Compatibility_generic_command_cannot_change_pin_meaning_through_mode_or_schema(string change)
    {
        var before = TransitionAuthor() with
        {
            SchemaVersion = change == "automatic-schema-downgrade" ? 3 : 2,
            Mode = change == "legacy-anchor-to-automatic" ? ContentLayoutMode.Anchored : ContentLayoutMode.Automatic
        };
        before = before with { Placements = before.Placements.Select((p, i) => i == 0 ? p with { PinnedIndex = 3 } : p).ToArray() };
        var desired = TransitionVersion(before, before with
        {
            SchemaVersion = 2,
            Mode = change == "dormant-pin-to-anchor" ? ContentLayoutMode.Anchored : ContentLayoutMode.Automatic
        });
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        // Both documents are independently legal, but retaining the same pin bytes changes their meaning.
        string Order(ContentLayoutDocument document) => string.Join(",", ContentLayoutSolver.Resolve(document, TransitionViewport())
            .Items.OrderBy(p => p.ReadOrder).Select(p => p.ItemId));
        Assert.NotEqual(Order(before), Order(desired));
        AssertCompatibilityRejectedWithoutWrite(before, desired, "pin-meaning");
    }

    [Theory]
    [InlineData("region-origin")]
    [InlineData("remove-unused-region")]
    [InlineData("member-region")]
    [InlineData("remove-explicit")]
    [InlineData("remove-decorative")]
    [InlineData("reserved-kind")]
    [InlineData("reserved-name")]
    public void Compatibility_generic_command_preserves_regions_reserved_intent_and_member_region(string change)
    {
        var before = TransitionAuthor() with
        {
            Regions = [new("main", 0), new("side", 1, 8, 8, 8)],
            ReservedSpaces = [new(0, 4, 2, 2) { Name = "explicit keep-out" },
                new(0, 0, 2, 2) { Kind = ContentReservedKind.Decorative, Name = "decoration" }]
        };
        if (change == "member-region") before = before with
        {
            Placements = before.Placements.Select((p, i) => i == 0 ? p with { RegionId = "side" } : p).ToArray()
        };
        var desired = change switch
        {
            "region-origin" => before with { Regions = [before.Regions[0], before.Regions[1] with { Column = 10 }] },
            "remove-unused-region" => before with { Regions = [before.Regions[0]] },
            "member-region" => before with { Placements = before.Placements.Select(p => p with { RegionId = "main" }).ToArray() },
            "remove-explicit" => before with { ReservedSpaces = [before.ReservedSpaces[1]] },
            "remove-decorative" => before with { ReservedSpaces = [before.ReservedSpaces[0]] },
            "reserved-kind" => before with { ReservedSpaces = [before.ReservedSpaces[0] with { Kind = ContentReservedKind.Decorative }, before.ReservedSpaces[1]] },
            "reserved-name" => before with { ReservedSpaces = [before.ReservedSpaces[0] with { Name = "" }, before.ReservedSpaces[1]] },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        desired = TransitionVersion(before, desired);
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        AssertCompatibilityRejectedWithoutWrite(before, desired, "constraint-meaning");
    }

    [Fact]
    public void Compatibility_undo_of_old_schema_size_override_may_restore_schema_two_without_dormant_pins()
    {
        var before = TransitionAuthor();
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var history = new ContentLayoutHistory();
        var edit = ContentLayoutEditing.Apply(before, new(ContentLayoutEditKind.Resize, before.Placements[0].ItemId)
            { ColumnSpan = 3, RowSpan = 2 }, TransitionViewport());
        Assert.True(edit.Accepted, edit.Error);
        Assert.Equal(3, edit.Document.SchemaVersion);
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, before, edit.Document, "compatibility-resize").Status);
        var resized = ReadTransition(f);
        history.Record(before, resized);
        var undo = history.ProposeUndo(resized);
        Assert.NotNull(undo);
        Assert.Equal(2, undo.Document.SchemaVersion);
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, resized, undo.Document, "compatibility-undo").Status);
        var restored = ReadTransition(f);
        Assert.True(history.Confirm(undo, restored));
        Assert.True(ContentLayoutEditing.SameState(before, restored, true));
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(ContentLayoutPolicy.Serialize(restored), new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
        var redo = history.ProposeRedo(restored);
        Assert.NotNull(redo);
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, restored, redo.Document, "compatibility-redo").Status);
        Assert.True(history.Confirm(redo, ReadTransition(f)));
        Assert.True(ContentLayoutEditing.SameState(resized, ReadTransition(f), true));
        Assert.Equal(3L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Compatibility_manual_command_cannot_erase_or_forge_the_current_automatic_size_basis(bool erase)
    {
        var original = TransitionAuthor();
        var resized = ContentLayoutEditing.Apply(original, new(ContentLayoutEditKind.Resize, original.Placements[0].ItemId)
            { ColumnSpan = 3, RowSpan = 2 }, TransitionViewport());
        Assert.True(resized.Accepted, resized.Error);
        var entered = ContentLayoutEditing.Apply(resized.Document, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, TransitionViewport());
        Assert.True(entered.Accepted, entered.Error);
        var before = entered.Document;
        Assert.Equal(new ContentAutomaticSizeBasis(2, 2), before.Placements[0].AutomaticSizeBasis);
        var desired = TransitionVersion(before, before with
        {
            Placements = before.Placements.Select((p, i) => i == 0
                ? p with { AutomaticSizeBasis = erase ? null : new(7, 7) } : p).ToArray()
        });
        Assert.Equal(before.Placements[0].SavedAutomaticState, desired.Placements[0].SavedAutomaticState);
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        AssertCompatibilityRejectedWithoutWrite(before, desired, "manual-size-basis");
    }

    [Fact]
    public void Compatibility_automatic_span_change_must_record_size_override_and_original_basis()
    {
        var before = TransitionAuthor();
        var desired = TransitionVersion(before, before with
        {
            Placements = before.Placements.Select((p, i) => i == 0 ? p with { ColumnSpan = 3 } : p).ToArray()
        });
        Assert.False(desired.Placements[0].ManualSize);
        Assert.Null(desired.Placements[0].AutomaticSizeBasis);
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        AssertCompatibilityRejectedWithoutWrite(before, desired, "unrecorded-size-override");
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Compatibility_legacy_automatic_dormant_pin_cannot_be_changed_under_the_same_schema(bool erase)
    {
        var before = TransitionAuthor();
        before = before with { Placements = before.Placements.Select((p, i) => i == 0 ? p with { PinnedIndex = 3 } : p).ToArray() };
        var desired = TransitionVersion(before, before with
        {
            Placements = before.Placements.Select((p, i) => i == 0 ? p with { PinnedIndex = erase ? null : 2 } : p).ToArray()
        });
        Assert.Equal(before.SchemaVersion, desired.SchemaVersion);
        Assert.Equal(before.Mode, desired.Mode);
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        AssertCompatibilityRejectedWithoutWrite(before, desired, "dormant-pin-change");
    }

    [Fact]
    public void Compatibility_demo_replacement_cannot_create_a_regional_composition_that_ordinary_undo_cannot_leave()
    {
        var before = TransitionAuthor();
        var desired = ContentLayoutSamples.Landscape(before.PosterId) with
        {
            DocumentVersion = before.DocumentVersion + 1,
            ItemSetVersion = before.ItemSetVersion + 1
        };
        Assert.True(desired.Regions.Count > 1);
        Assert.False(before.Placements.Select(p => p.ItemId).ToHashSet(StringComparer.Ordinal)
            .SetEquals(desired.Placements.Select(p => p.ItemId)));
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        AssertCompatibilityRejectedWithoutWrite(before, desired, "regional-demo-replacement", replaceMembers: true);
    }

    [Fact]
    public void Compatibility_first_automatic_drag_upgrades_pin_semantics_and_undo_restores_pin_free_old_schema()
    {
        var before = TransitionAuthor();
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var history = new ContentLayoutHistory();
        var moved = ContentLayoutEditing.Apply(before, new(ContentLayoutEditKind.Move, before.Placements[0].ItemId)
            { TargetReadOrder = 2 }, TransitionViewport());
        Assert.True(moved.Accepted, moved.Error);
        Assert.Equal(3, moved.Document.Placements[0].PinnedIndex);
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, before, moved.Document, "first-pin").Status);
        history.Record(before, ReadTransition(f));
        var undo = history.ProposeUndo(ReadTransition(f))!;
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, ReadTransition(f), undo.Document, "undo-first-pin").Status);
        var restored = ReadTransition(f);
        Assert.True(history.Confirm(undo, restored));
        Assert.Equal(2, restored.SchemaVersion);
        Assert.True(ContentLayoutEditing.SameState(before, restored, true));
        var redo = history.ProposeRedo(restored)!;
        Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, restored, redo.Document, "redo-first-pin").Status);
        Assert.True(history.Confirm(redo, ReadTransition(f)));
    }

    private static void AssertCompatibilityRejectedWithoutWrite(ContentLayoutDocument before, ContentLayoutDocument desired, string requestId,
        bool replaceMembers = false)
    {
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var initial = f.Repository.Read();
        var entry = initial.Catalog.Posters[0];
        var result = f.Coordinator.Execute(new ContentLayoutCommand(requestId, StateCommandSource.Poster, initial.Revision, "A",
            StateReadSet.ForPoster("A", entry.EntityVersion), before.DocumentVersion, before.ItemSetVersion, desired, replaceMembers));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(initial.Revision, f.Repository.Read().Revision);
        Assert.Equal(initial.Catalog.Posters[0], f.Repository.Read().Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(ContentLayoutPolicy.Serialize(before), new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
    }
}
