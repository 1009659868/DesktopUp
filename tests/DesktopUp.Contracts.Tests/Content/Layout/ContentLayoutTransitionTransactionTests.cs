using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Fact]
    public void Known_read_only_source_cannot_be_silently_repaired_by_a_legal_desired_document()
    {
        var old = ContentLayoutSamples.Equal("A", 1) with { SchemaVersion = 2, Mode = ContentLayoutMode.Anchored, MaximumColumns = 8 };
        old = old with { Placements = [old.Placements[0] with { Column = 7, Row = 0, FixedPosition = true }] };
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(old) });
        var desired = old with { Mode = ContentLayoutMode.Automatic, DocumentVersion = 2,
            Placements = [old.Placements[0] with { Column = null, Row = null, FixedPosition = false, Version = 2 }] };
        var state = f.Repository.Read(); var entry = state.Catalog.Posters[0];
        var command = new ContentLayoutCommand("implicit-repair", StateCommandSource.ControlCenter, state.Revision, "A",
            StateReadSet.ForPoster("A", entry.EntityVersion), 1, 1, desired);
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command).Status);
        Assert.Equal(state.Revision, f.Repository.Read().Revision);
        Assert.Equal(entry, f.Repository.Read().Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enter_manual_must_retain_the_actual_automatic_intent_in_the_same_transaction(bool forged)
    {
        var old = ContentLayoutSamples.Equal("A", 1) with { SchemaVersion = 2 };
        old = old with { Placements = [old.Placements[0] with { ColumnSpan = 6, ManualSize = true }] };
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(old) });
        var p = old.Placements[0];
        var desired = old with { SchemaVersion = 3, Mode = ContentLayoutMode.Manual, SavedAutomaticMode = old.Mode, DocumentVersion = 2,
            Placements = [p with { Column = 0, Row = 0, Version = 2,
                SavedAutomaticState = new(p.Order, forged ? 4 : 6, p.RowSpan, p.ManualSize, p.PinnedIndex) }] };
        var state = f.Repository.Read(); var entry = state.Catalog.Posters[0];
        var command = new ContentLayoutCommand("manual-entry", StateCommandSource.Poster, state.Revision, "A",
            StateReadSet.ForPoster("A", entry.EntityVersion), 1, 1, desired);
        var result = f.Coordinator.Execute(command);
        Assert.Equal(forged ? StateCommandStatus.ValidationFailed : StateCommandStatus.Committed, result.Status);
        Assert.Equal(state.Revision + (forged ? 0 : 1), f.Repository.Read().Revision);
        if (forged) Assert.Equal(entry, f.Repository.Read().Catalog.Posters[0]);
        else Assert.Equal(ContentLayoutPolicy.Serialize(desired), f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stored_manual_return_state_cannot_be_erased_or_forged_by_an_in_mode_command(bool erase)
    {
        var entered = ContentLayoutEditing.Apply(TransitionAuthor(), new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, TransitionViewport());
        Assert.True(entered.Accepted, entered.Error);
        var before = entered.Document;
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var desired = before with { SavedAutomaticMode = erase ? null : before.SavedAutomaticMode,
            Placements = before.Placements.Select(p => p with { SavedAutomaticState = erase ? null : p.SavedAutomaticState! with
                { ColumnSpan = p.SavedAutomaticState.ColumnSpan + 1 } }).ToArray() };
        desired = TransitionVersion(before, desired);
        Assert.Null(ContentLayoutPolicy.Validate(desired));
        var state = f.Repository.Read();
        Assert.Equal(StateCommandStatus.ValidationFailed, ExecuteTransition(f, before, desired, "manual-tamper").Status);
        Assert.Equal(state.Revision, f.Repository.Read().Revision);
        Assert.Equal(state.Catalog.Posters[0], f.Repository.Read().Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stored_manual_exit_A_restores_exact_automatic_state_or_rejects_the_whole_transaction(bool forged)
    {
        var automatic = TransitionAuthor() with { Mode = ContentLayoutMode.Anchored };
        automatic = automatic with { Placements = automatic.Placements.Select((p, i) => i == 0
            ? p with { ColumnSpan = 3, ManualSize = true, PinnedIndex = 1 } : p).ToArray() };
        var entered = ContentLayoutEditing.Apply(automatic, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, TransitionViewport());
        Assert.True(entered.Accepted, entered.Error);
        var resized = ContentLayoutEditing.Apply(entered.Document, new(ContentLayoutEditKind.Resize, automatic.Placements[0].ItemId)
            { ColumnSpan = 4, RowSpan = 3 }, TransitionViewport());
        Assert.True(resized.Accepted, resized.Error);
        var before = resized.Document;
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var exited = ContentLayoutEditing.Apply(before, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Automatic }, TransitionViewport());
        Assert.True(exited.Accepted, exited.Error);
        var desired = exited.Document;
        if (forged) desired = TransitionVersion(before, desired with { Placements = desired.Placements.Select((p, i) =>
            i == 0 ? p with { ColumnSpan = p.ColumnSpan + 1 } : p).ToArray() });
        var initial = f.Repository.Read();
        var result = ExecuteTransition(f, before, desired, "exit-a");
        Assert.Equal(forged ? StateCommandStatus.ValidationFailed : StateCommandStatus.Committed, result.Status);
        Assert.Equal(initial.Revision + (forged ? 0 : 1), f.Repository.Read().Revision);
        if (forged) Assert.Equal(initial.Catalog.Posters[0], f.Repository.Read().Catalog.Posters[0]);
        else
        {
            var stored = ReadTransition(f);
            Assert.Null(stored.SavedAutomaticMode);
            Assert.All(stored.Placements, p => Assert.Null(p.SavedAutomaticState));
            Assert.Equal(automatic.Placements[0].ColumnSpan, stored.Placements[0].ColumnSpan);
            Assert.Equal(automatic.Placements[0].PinnedIndex, stored.Placements[0].PinnedIndex);
            using var reopened = new SqliteStateStore(f.Paths);
            Assert.True(reopened.Initialize().Succeeded);
            Assert.Equal(ContentLayoutPolicy.Serialize(desired), new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
        }
    }

    [Fact]
    public void Undo_redo_of_manual_entry_edit_and_exit_A_remain_valid_real_transactions()
    {
        var original = TransitionAuthor();
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(original) });
        var history = new ContentLayoutHistory();
        var current = original;
        var actions = new ContentLayoutEdit[]
        {
            new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual },
            new(ContentLayoutEditKind.Move, original.Placements[0].ItemId) { Column = 0, Row = 6 },
            new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic }
        };
        for (int i = 0; i < actions.Length; i++)
        {
            var edited = ContentLayoutEditing.Apply(current, actions[i], TransitionViewport());
            Assert.True(edited.Accepted, edited.Error);
            var result = ExecuteTransition(f, current, edited.Document, "forward-" + i);
            Assert.Equal(StateCommandStatus.Committed, result.Status);
            var stored = ReadTransition(f);
            history.Record(current, stored); current = stored;
        }
        var completed = current;
        for (int i = 0; i < actions.Length; i++)
        {
            var proposal = history.ProposeUndo(current);
            Assert.NotNull(proposal);
            var result = ExecuteTransition(f, current, proposal.Document, "undo-" + i);
            Assert.Equal(StateCommandStatus.Committed, result.Status);
            current = ReadTransition(f);
            Assert.True(history.Confirm(proposal, current));
        }
        Assert.True(ContentLayoutEditing.SameState(original, current, true));
        for (int i = 0; i < actions.Length; i++)
        {
            var proposal = history.ProposeRedo(current);
            Assert.NotNull(proposal);
            var result = ExecuteTransition(f, current, proposal.Document, "redo-" + i);
            Assert.Equal(StateCommandStatus.Committed, result.Status);
            current = ReadTransition(f);
            Assert.True(history.Confirm(proposal, current));
        }
        Assert.True(ContentLayoutEditing.SameState(completed, current, true));
        Assert.Equal(9L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Fact]
    public void Automatic_size_basis_is_saved_and_restored_after_manual_roundtrip_in_real_sqlite()
    {
        var original = TransitionAuthor();
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(original) });
        var current = original;
        var actions = new ContentLayoutEdit[]
        {
            new(ContentLayoutEditKind.Resize, original.Placements[0].ItemId) { ColumnSpan = 3, RowSpan = 2 },
            new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual },
            new(ContentLayoutEditKind.Resize, original.Placements[0].ItemId) { ColumnSpan = 4, RowSpan = 3 },
            new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic },
            new(ContentLayoutEditKind.SizeOverride, original.Placements[0].ItemId) { Enabled = false }
        };
        for (int i = 0; i < actions.Length; i++)
        {
            var edited = ContentLayoutEditing.Apply(current, actions[i], TransitionViewport());
            Assert.True(edited.Accepted, edited.Error);
            Assert.Equal(StateCommandStatus.Committed, ExecuteTransition(f, current, edited.Document, "size-basis-" + i).Status);
            current = ReadTransition(f);
            if (i < actions.Length - 1)
                Assert.Equal(new ContentAutomaticSizeBasis(2, 2), current.Placements[0].AutomaticSizeBasis);
            if (current.Mode == ContentLayoutMode.Manual)
                Assert.Equal(new ContentAutomaticSizeBasis(2, 2), current.Placements[0].SavedAutomaticState!.AutomaticSizeBasis);
        }
        Assert.False(current.Placements[0].ManualSize);
        Assert.Null(current.Placements[0].AutomaticSizeBasis);
        Assert.Equal(2, current.Placements[0].ColumnSpan);
        Assert.Equal(2, current.Placements[0].RowSpan);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(ContentLayoutPolicy.Serialize(current), new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct_automatic_command_cannot_forge_a_size_basis_or_invent_an_unknown_restore(bool unknownRestore)
    {
        var before = TransitionAuthor();
        if (unknownRestore) before = before with { Placements = before.Placements.Select((p, i) => i == 0
            ? p with { ManualSize = true, ColumnSpan = 3 } : p).ToArray() };
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var desired = before with { SchemaVersion = 3, Placements = before.Placements.Select((p, i) => i != 0 ? p : p with
        {
            ManualSize = !unknownRestore, ColumnSpan = unknownRestore ? 4 : 3,
            AutomaticSizeBasis = unknownRestore ? null : new(7, 7)
        }).ToArray() };
        desired = TransitionVersion(before, desired);
        Assert.Null(ContentLayoutPolicy.Validate(desired));
        var initial = f.Repository.Read();
        Assert.Equal(StateCommandStatus.ValidationFailed, ExecuteTransition(f, before, desired, "forged-size-source").Status);
        Assert.Equal(initial.Revision, f.Repository.Read().Revision);
        Assert.Equal(initial.Catalog.Posters[0], f.Repository.Read().Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Fact]
    public void Direct_same_mode_command_cannot_clear_a_compatible_legacy_hard_position()
    {
        var before = TransitionAuthor() with { Mode = ContentLayoutMode.Anchored };
        before = before with { Placements = before.Placements.Select((p, i) => i == 0 ? p with
            { FixedPosition = true, Column = 0, Row = 0 } : p).ToArray() };
        using var f = new Fixture(Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(before) });
        var desired = TransitionVersion(before, before with { Placements = before.Placements.Select((p, i) => i == 0
            ? p with { FixedPosition = false, Column = null, Row = null } : p).ToArray() });
        ContentLayoutCommandPolicy.ValidateAuthoring(before);
        ContentLayoutCommandPolicy.ValidateAuthoring(desired);
        var initial = f.Repository.Read();
        Assert.Equal(StateCommandStatus.ValidationFailed, ExecuteTransition(f, before, desired, "clear-hard").Status);
        Assert.Equal(initial.Revision, f.Repository.Read().Revision);
        Assert.Equal(initial.Catalog.Posters[0], f.Repository.Read().Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    private static ContentLayoutDocument TransitionAuthor() => new("A", Enumerable.Range(0, 3)
        .Select(i => new ContentPlacement("A:item-" + i, i, 2, 2)).ToArray())
        { SchemaVersion = 2, BaseCellDip = 48, BaseGapDip = 0 };
    private static ContentLayoutViewport TransitionViewport() => new(384, 384, Generation: 1);
    private static ContentLayoutDocument TransitionVersion(ContentLayoutDocument before, ContentLayoutDocument desired) => desired with
    {
        DocumentVersion = before.DocumentVersion + 1,
        Placements = desired.Placements.Select(p =>
        {
            var old = before.Placements.Single(q => q.ItemId == p.ItemId);
            return p with { Version = old.Version + ((p with { Version = old.Version }) == old ? 0 : 1) };
        }).ToArray()
    };
    private static StateCommandResult ExecuteTransition(Fixture fixture, ContentLayoutDocument before, ContentLayoutDocument desired, string requestId)
    {
        var state = fixture.Repository.Read(); var entry = state.Catalog.Posters[0];
        return fixture.Coordinator.Execute(new ContentLayoutCommand(requestId, StateCommandSource.Poster, state.Revision, "A",
            StateReadSet.ForPoster("A", entry.EntityVersion), before.DocumentVersion, before.ItemSetVersion, desired));
    }
    private static ContentLayoutDocument ReadTransition(Fixture fixture)
    {
        Assert.True(ContentLayoutPolicy.TryRead(fixture.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson,
            "A", out var document, out var error), error);
        return document!;
    }
}
