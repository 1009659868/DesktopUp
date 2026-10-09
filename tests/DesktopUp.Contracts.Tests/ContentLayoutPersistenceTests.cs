using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical, false)]
    [InlineData(ContentFlowAxis.HorizontalLoop, false)]
    [InlineData(ContentFlowAxis.Vertical, true)]
    [InlineData(ContentFlowAxis.HorizontalLoop, true)]
    public void Read_only_canonical_projection_cannot_write_a_content_command(ContentFlowAxis axis, bool legacyOverflow)
    {
        using var f = new Fixture();
        var before = f.Repository.Read();
        var command = f.Edit();
        var desired = command.DesiredDocument with { FlowAxis = axis, MaximumColumns = legacyOverflow ? 8 : 2 };
        if (legacyOverflow)
            desired = desired with
            {
                Placements = desired.Placements.Select((p, index) => index == 0 ? p with
                {
                    FixedPosition = true,
                    Column = axis == ContentFlowAxis.Vertical ? 7 : 0,
                    Row = axis == ContentFlowAxis.Vertical ? 0 : 7
                } : p).ToArray()
            };
        var result = f.Coordinator.Execute(command with { DesiredDocument = desired });
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        var after = f.Repository.Read();
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Catalog.Posters[0], after.Catalog.Posters[0]);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(before.Catalog.Posters[0].Poster.ContentLayoutJson,
            new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
    }

    [Fact]
    public void Legacy_null_reads_are_virtual_stable_and_do_not_write_or_change_old_fingerprint()
    {
        using var f = new Fixture();
        var before = f.Repository.Read();
        Assert.Null(before.Catalog.Posters[0].Poster.ContentLayoutJson);
        Assert.DoesNotContain("ContentLayoutJson", JsonSerializer.Serialize(before.Catalog.Posters[0].Poster));
        Assert.True(ContentLayoutPolicy.TryRead(null, "A", out var first, out _));
        Assert.True(ContentLayoutPolicy.TryRead(null, "A", out var second, out _));
        Assert.Equal(ContentLayoutPolicy.Serialize(first!), ContentLayoutPolicy.Serialize(second!));
        Assert.Equal(before.Revision, f.Repository.Read().Revision);
        Assert.Null(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
    }

    [Fact]
    public void Semantic_edit_commits_once_replays_and_does_not_invalidate_outer_spatial_generation()
    {
        using var f = new Fixture();
        var before = f.Repository.Read();
        int events = 0; f.Coordinator.ProjectionChanged += (_, _) => events++;
        var command = f.Edit();
        var result = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(before.Revision + 1, result.CommittedRevision);
        Assert.Equal(1, events);
        var after = f.Repository.Read().Catalog.Posters[0];
        Assert.Equal(before.Catalog.Posters[0].EntityVersion + 1, after.EntityVersion);
        Assert.Equal(before.Catalog.Posters[0].LayoutGeneration, after.LayoutGeneration);
        Assert.Equal(before.Catalog.Posters[0].LastLayoutOperationId, after.LastLayoutOperationId);
        Assert.Equal(ContentLayoutPolicy.Serialize(command.DesiredDocument), after.Poster.ContentLayoutJson);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(1, events);
        Assert.Equal(before.Revision + 1, f.Repository.Read().Revision);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(after.Poster.ContentLayoutJson, new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
    }

    [Theory]
    [InlineData("missing-read")] [InlineData("document")] [InlineData("itemset")]
    [InlineData("changed-version")] [InlineData("unchanged-version")] [InlineData("foreign-owner")]
    public void Untrusted_content_command_cannot_skip_aggregate_or_semantic_versions(string kind)
    {
        using var f = new Fixture(); var command = f.Edit();
        command = kind switch
        {
            "missing-read" => command with { ReadSet = new(new Dictionary<string, long>()) },
            "document" => command with { ExpectedDocumentVersion = 55 },
            "itemset" => command with { ExpectedItemSetVersion = 55 },
            "changed-version" => command with { DesiredDocument = command.DesiredDocument with
                { Placements = command.DesiredDocument.Placements.Select((p, i) => i == 0 ? p with { Version = 1 } : p).ToArray() } },
            "unchanged-version" => command with { DesiredDocument = command.DesiredDocument with
                { Placements = command.DesiredDocument.Placements.Select((p, i) => i == 1 ? p with { Version = 2 } : p).ToArray() } },
            _ => command with { DesiredDocument = ContentLayoutSamples.Equal("B") with { DocumentVersion = 2 } }
        };
        long before = f.Repository.Read().Revision;
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before, f.Repository.Read().Revision);
        Assert.Null(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
    }

    [Fact]
    public void Same_poster_noncontent_intervention_conflicts_without_overwriting_it()
    {
        using var f = new Fixture(); var command = f.Edit();
        Assert.True(f.Rename("intervened").Succeeded);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(command).Status);
        Assert.Equal("intervened", f.Repository.Read().Catalog.Posters[0].Poster.Title);
        Assert.Null(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
    }

    [Fact]
    public void Explicit_membership_replacement_advances_itemset_once_and_preferences_are_independent()
    {
        using var f = new Fixture(); var c = f.Edit();
        var sample = ContentLayoutSamples.Covers("A") with { DocumentVersion = 2, ItemSetVersion = 2,
            ShowBaselineGrid = false, ShowSpanLabels = true };
        sample = sample with { Placements = sample.Placements.Select((p, i) => p with { Version = i < 6 ? 2 : 1 }).ToArray() };
        Assert.False(f.Coordinator.Execute(c with { DesiredDocument = sample }).Succeeded);
        var result = f.Coordinator.Execute(c with { DesiredDocument = sample, ReplaceItemSet = true });
        Assert.True(result.Succeeded, result.Error);
        Assert.True(ContentLayoutPolicy.TryRead(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson, "A", out var restored, out _));
        Assert.Equal(10, restored!.Placements.Count); Assert.Equal(2, restored.ItemSetVersion);
        Assert.False(restored.ShowBaselineGrid); Assert.True(restored.ShowSpanLabels);
    }

    [Fact]
    public void Unknown_original_is_preserved_by_rename_backup_restart_and_refuses_content_edit_or_clone()
    {
        const string raw = "{ \"engineId\":\"future\", \"schemaVersion\":77, \"custom\": [ 1, 2 ] }";
        using var f = new Fixture(seed: Seed() with { ContentLayoutJson = raw });
        Assert.True(f.Rename("renamed").Succeeded);
        var p = f.Repository.Read().Catalog.Posters[0];
        Assert.Equal(raw, p.Poster.ContentLayoutJson);
        var rejected = new ContentLayoutCommand("unknown", StateCommandSource.ControlCenter, p.Revision, "A",
            StateReadSet.ForPoster("A", p.EntityVersion), 1, 1, ContentLayoutSamples.Equal("A") with { DocumentVersion = 2 });
        Assert.False(f.Coordinator.Execute(rejected).Succeeded);
        var projection = f.Coordinator.Refresh();
        Assert.False(f.Coordinator.Execute(new ClonePosterCommand("unknown-clone", StateCommandSource.ControlCenter,
            projection.Revision, StateReadSet.ForPoster("A", p.EntityVersion) with { CatalogVersion = projection.CatalogVersion }, "A")).Succeeded);
        var backup = f.Store.CreateBackup("content-unknown"); Assert.True(backup.Succeeded, backup.Error);
        string path = Path.Combine(f.Paths.Backups, backup.FileName!);
        Assert.True(f.Store.ValidateBackup(path).IsValid);
        Assert.Equal(raw, f.Sql("SELECT content_layout_json FROM posters;", path));
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(raw, new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson);
    }

    [Theory] [InlineData("row")] [InlineData("receipt")]
    public void Sql_fault_rolls_back_document_entity_version_global_head_and_receipt(string stage)
    {
        using var f = new Fixture(); var before = f.Repository.Read();
        f.Sql("CREATE TRIGGER content_fault BEFORE " + (stage == "row" ? "UPDATE ON posters" : "INSERT ON state_command_receipts") +
            " BEGIN SELECT RAISE(ABORT,'content fault'); END;");
        Assert.Equal(StateCommandStatus.Failed, f.Coordinator.Execute(f.Edit()).Status);
        var after = f.Repository.Read();
        Assert.Equal(before.Revision, after.Revision); Assert.Equal(before.Catalog.Posters[0], after.Catalog.Posters[0]);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM state_command_receipts;"));
    }

    [Fact]
    public void Clone_remaps_owner_and_all_memberships_through_both_supported_repository_paths()
    {
        using var f = new Fixture(seed: Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(ContentLayoutSamples.Covers("A")) });
        var before = f.Coordinator.Refresh(); var entry = before.PostersById["A"];
        var result = f.Coordinator.Execute(new ClonePosterCommand("clone", StateCommandSource.ControlCenter, before.Revision,
            StateReadSet.ForPoster("A", entry.EntityVersion) with { CatalogVersion = before.CatalogVersion }, "A"));
        Assert.True(result.Succeeded, result.Error);
        var direct = new SqlitePosterCatalogRepository(f.Store).CloneConfiguration(result.CurrentRevision, "direct-clone", "A");
        Assert.True(direct.Succeeded, direct.Error);
        var all = f.Repository.Read().Catalog.Posters;
        Assert.Equal(3, all.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in all)
        {
            Assert.True(ContentLayoutPolicy.TryRead(row.Poster.ContentLayoutJson, row.Poster.PosterId, out var d, out var error), error);
            Assert.Equal(10, d!.Placements.Count);
            Assert.All(d.Placements, p => Assert.True(identities.Add(p.ItemId)));
        }
    }

    [Fact]
    public void V9_migration_keeps_legacy_parameters_null_layout_revision_and_source_backup()
    {
        using var f = new Fixture(); var before = f.Repository.Read();
        // Rolling back to v9 must clear every migration ledger row at or above the dropped version,
        // otherwise re-applying a later migration collides with the retained ledger entry.
        f.Sql("DROP TABLE content_open_facts; DROP TABLE content_open_requests; DROP TABLE content_demo_archive; DROP TABLE content_poster_items; DROP TABLE content_poster_bindings; DROP TABLE content_memberships; DROP TABLE content_collections; DROP TABLE content_items; DROP TABLE content_head; ALTER TABLE posters DROP COLUMN content_layout_json; ALTER TABLE app_settings DROP COLUMN center_theme; ALTER TABLE app_settings DROP COLUMN template_catalog_json; DELETE FROM schema_migrations WHERE version>=10; UPDATE state_head SET schema_version=9; PRAGMA user_version=9;");
        using var migrated = new SqliteStateStore(f.Paths); var result = migrated.Initialize();
        Assert.True(result.Succeeded, result.Error); Assert.Equal(9, result.PreviousSchemaVersion); Assert.Equal(14, result.Head!.SchemaVersion);
        Assert.Equal(before.Revision, result.Head.Revision);
        var row = new SqlitePosterRepository(migrated).Read("A")!;
        Assert.Equal(before.Catalog.Posters[0].Poster, row.Poster); Assert.Null(row.Poster.ContentLayoutJson);
        string backup = Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(9L, f.Sql("PRAGMA user_version;", backup));
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM pragma_table_info('posters') WHERE name='content_layout_json';", backup));
    }

    private static PosterDefinition Seed() => PosterConfigurationFactory.NewBasicBox("A", "DISPLAY1") with
        { PosterId = "A", ThemeInstanceId = "theme-A" };
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.ContentLayout.Tests", Guid.NewGuid().ToString("N"));
        public Fixture(PosterDefinition? seed = null)
        {
            Paths = new(_root); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Repository = new(Store); Coordinator = new(Repository); Coordinator.EnsureInitialized(seed ?? Seed());
        }
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; }
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public ContentLayoutCommand Edit()
        {
            var state = Repository.Read(); var entry = state.Catalog.Posters[0];
            Assert.True(ContentLayoutPolicy.TryRead(entry.Poster.ContentLayoutJson, "A", out var d, out _));
            var edit = ContentLayoutEditing.Apply(d!, new(ContentLayoutEditKind.Resize, d!.Placements[0].ItemId)
                { ColumnSpan = d.Placements[0].ColumnSpan + 1, RowSpan = d.Placements[0].RowSpan }, new(1024, 1024));
            Assert.True(edit.Accepted, edit.Error);
            var desired = edit.Document;
            return new("content-edit", StateCommandSource.Poster, state.Revision, "A", StateReadSet.ForPoster("A", entry.EntityVersion),
                d.DocumentVersion, d.ItemSetVersion, desired);
        }
        public StateCommandResult Rename(string title)
        {
            var p = Coordinator.Refresh(); var e = p.PostersById["A"];
            return Coordinator.Execute(new PosterStateCommand("rename", StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
                p.Revision, "A", e.Poster with { Title = title }) { ReadSet = StateReadSet.ForPoster("A", e.EntityVersion) });
        }
        public object? Sql(string sql, string? path = null)
        {
            using var connection = new SqliteConnection($"Data Source={path ?? Path.Combine(Paths.State, "desktopup.sqlite3")};Pooling=False");
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar();
        }
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
