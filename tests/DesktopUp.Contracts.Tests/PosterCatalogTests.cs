using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterCatalogTests
{
    [Fact]
    public void FreshCatalogIsUninitializedAndListDoesNotWrite()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        PosterCatalogSnapshot snapshot = catalog.List();
        Assert.Equal(0, snapshot.Revision);
        Assert.Equal(0, snapshot.CatalogVersion);
        Assert.False(snapshot.ProfileInitialized);
        Assert.Empty(snapshot.Posters);
        Assert.Equal(0, store.ReadHead().Revision);
    }

    [Fact]
    public void LegacyDefaultEntryPointInitializesExactlyOnce()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var posters = new SqlitePosterRepository(store);
        PosterStateSnapshot first = posters.GetOrCreateDefault(Configuration());
        Assert.Equal(first, posters.GetOrCreateDefault(Configuration() with { Title = "ignored" }));
        PosterCatalogSnapshot catalog = new SqlitePosterCatalogRepository(store).List();
        Assert.True(catalog.ProfileInitialized);
        Assert.Equal(1, catalog.CatalogVersion);
        Assert.Equal(1, first.EntityVersion);
        Assert.Equal(first, Assert.Single(catalog.Posters));
    }

    [Fact]
    public void CreateAndCloneAllocateIndependentIdentitiesAndKeepConfiguration()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var configuration = Configuration() with
        {
            Title = "文件收藏", Purpose = "work", CollectionId = "collection-shared",
            IsLocked = true, IsCollapsed = true, IsVisible = false,
            LayoutParametersJson = "{\"iconSize\":48}", StackOrder = 100
        };
        PosterCatalogWriteResult a = catalog.Create(0, "create-a", configuration);
        PosterCatalogWriteResult b = catalog.Create(1, "create-b", configuration with { Title = "Games" });
        PosterCatalogWriteResult clone = catalog.CloneConfiguration(2, "clone-a", a.PosterId!);
        Assert.True(a.Succeeded, a.Error);
        Assert.True(b.Succeeded, b.Error);
        Assert.True(clone.Succeeded, clone.Error);
        PosterCatalogSnapshot snapshot = catalog.List();
        Assert.Equal(3, snapshot.Revision);
        Assert.Equal(3, snapshot.CatalogVersion);
        Assert.Equal(3, snapshot.Posters.Select(p => p.Poster.PosterId).Distinct().Count());
        Assert.Equal(3, snapshot.Posters.Select(p => p.Poster.ThemeInstanceId).Distinct().Count());
        Assert.All(snapshot.Posters, p => Assert.Equal(snapshot.Revision, p.Revision));
        PosterDefinition original = snapshot.Posters[0].Poster;
        PosterDefinition copied = snapshot.Posters[2].Poster;
        Assert.Equal(configuration with
        {
            PosterId = original.PosterId, ThemeInstanceId = original.ThemeInstanceId, StackOrder = 0
        }, original);
        Assert.Equal(original with
        {
            PosterId = copied.PosterId, ThemeInstanceId = copied.ThemeInstanceId, StackOrder = 2
        }, copied);
        Assert.Equal("collection-shared", copied.CollectionId);
        Assert.Equal(1, snapshot.Posters[0].EntityVersion);
        Assert.Equal(1, snapshot.Posters[2].EntityVersion);
    }

    [Fact]
    public void CreateReplayAcrossRestartReturnsOriginalIdentityWithoutAnotherCommit()
    {
        using var fixture = new Fixture();
        string id;
        using (var store = fixture.Open())
        {
            var result = new SqlitePosterCatalogRepository(store).Create(0, "create-once", Configuration());
            Assert.True(result.Succeeded, result.Error);
            id = result.PosterId!;
        }
        using var reopened = fixture.Open();
        var replay = new SqlitePosterCatalogRepository(reopened).Create(0, "create-once", Configuration());
        Assert.Equal(PosterCatalogWriteStatus.Replayed, replay.Status);
        Assert.Equal(id, replay.PosterId);
        Assert.Equal(1, replay.CommittedRevision);
        Assert.Single(replay.Snapshot!.Posters);
        Assert.Equal(1, reopened.ReadHead().Revision);
    }

    [Fact]
    public void RemovingLastPosterAndReplayingOldCreateCannotResurrectIt()
    {
        using var fixture = new Fixture();
        using (var store = fixture.Open())
        {
            var catalog = new SqlitePosterCatalogRepository(store);
            var first = catalog.Create(0, "a", Configuration());
            Assert.True(catalog.Remove(1, "remove-a", first.PosterId!).Succeeded);
        }
        using var reopened = fixture.Open();
        var restored = new SqlitePosterCatalogRepository(reopened);
        Assert.True(restored.List().ProfileInitialized);
        Assert.Empty(restored.List().Posters);
        Assert.Equal(2, restored.List().CatalogVersion);
        Assert.Throws<InvalidOperationException>(() =>
            new SqlitePosterRepository(reopened).GetOrCreateDefault(Configuration()));
        var replay = restored.Create(0, "a", Configuration());
        Assert.Equal(PosterCatalogWriteStatus.Replayed, replay.Status);
        Assert.Empty(replay.Snapshot!.Posters);
        Assert.Equal(1, replay.CommittedRevision);
        Assert.Equal(2, replay.CurrentRevision);
        Assert.Equal(PosterCatalogWriteStatus.Replayed, restored.Remove(1, "remove-a", replay.PosterId!).Status);
        Assert.Equal(2, reopened.ReadHead().Revision);
    }

    [Fact]
    public void ExplicitEmptyInitializationSurvivesRestartAndAllowsLaterCreation()
    {
        using var fixture = new Fixture();
        using (var store = fixture.Open())
        {
            var catalog = new SqlitePosterCatalogRepository(store);
            Assert.True(catalog.InitializeEmptyProfile(0, "init-empty").Succeeded);
            Assert.Equal(PosterCatalogWriteStatus.Replayed, catalog.InitializeEmptyProfile(0, "init-empty").Status);
            Assert.Equal(PosterCatalogWriteStatus.ValidationFailed, catalog.InitializeEmptyProfile(1, "init-again").Status);
        }
        using var reopened = fixture.Open();
        var restored = new SqlitePosterCatalogRepository(reopened);
        Assert.True(restored.List().ProfileInitialized);
        Assert.Equal(0, restored.List().CatalogVersion);
        Assert.Throws<InvalidOperationException>(() =>
            new SqlitePosterRepository(reopened).GetOrCreateDefault(Configuration()));
        Assert.True(restored.Create(1, "manual-create", Configuration()).Succeeded);
    }

    [Fact]
    public void EntityVersionsAndCatalogMembershipVersionAreIndependentOfGlobalRevision()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var posters = new SqlitePosterRepository(store);
        var a = catalog.Create(0, "a", Configuration());
        var b = catalog.Create(1, "b", Configuration());
        var original = posters.Read(a.PosterId!)!;
        Assert.True(posters.Save(2, "geometry-a", original.Poster with
        {
            WidthDip = 640, IsLocked = true, IsCollapsed = true, IsVisible = false
        }).Succeeded);
        var changed = posters.Read(a.PosterId!)!;
        Assert.Equal(2, changed.EntityVersion);
        Assert.Equal(1, posters.Read(b.PosterId!)!.EntityVersion);
        Assert.Equal(2, catalog.List().CatalogVersion);
        Assert.True(posters.Save(3, "stack-a", changed.Poster with { StackOrder = 5 }).Succeeded);
        var settings = new SqliteSettingsRepository(store);
        Assert.True(settings.Save(4, "pause", settings.Read().Settings with { DynamicEffectsPaused = true }).Succeeded);
        var current = catalog.List();
        Assert.Equal(5, current.Revision);
        Assert.Equal(3, current.CatalogVersion);
        Assert.Equal(b.PosterId, current.Posters[0].Poster.PosterId);
        Assert.Equal(3, current.Posters[1].EntityVersion);
        Assert.False(current.Posters[1].Poster.IsVisible);
    }

    [Fact]
    public void MissingTargetsNeverAdvanceRevisionOrCreateEntries()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        Assert.Equal(PosterCatalogWriteStatus.NotFound, catalog.Remove(0, "remove", "missing").Status);
        Assert.Equal(PosterCatalogWriteStatus.NotFound, catalog.CloneConfiguration(0, "clone", "missing").Status);
        Assert.Equal(0, store.ReadHead().Revision);
        Assert.Empty(catalog.List().Posters);
        Assert.False(catalog.List().ProfileInitialized);
    }

    [Fact]
    public async Task ConcurrentConnectionsCommitOnlyOneRequestAtTheSameRevision()
    {
        using var fixture = new Fixture();
        using var first = fixture.Open();
        using var second = fixture.Open();
        var results = await Task.WhenAll(
            Task.Run(() => new SqlitePosterCatalogRepository(first).Create(0, "first", Configuration())),
            Task.Run(() => new SqlitePosterCatalogRepository(second).Create(0, "second", Configuration())));
        Assert.Single(results.Where(r => r.Status == PosterCatalogWriteStatus.Committed));
        Assert.Single(results.Where(r => r.Status == PosterCatalogWriteStatus.Conflict));
        Assert.Single(new SqlitePosterCatalogRepository(first).List().Posters);
        Assert.Equal(1, first.ReadHead().Revision);
    }

    [Fact]
    public void ReusedOperationWithDifferentPayloadOrKindIsRejectedWithoutMutation()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var a = catalog.Create(0, "same-id", Configuration());
        Assert.Equal(PosterCatalogWriteStatus.ValidationFailed,
            catalog.Create(0, "same-id", Configuration() with { Title = "wrong" }).Status);
        Assert.Equal(PosterCatalogWriteStatus.ValidationFailed,
            catalog.Remove(0, "same-id", a.PosterId!).Status);
        Assert.True(store.ExecuteCompareAndSwap(1, "legacy-operation").Succeeded);
        Assert.Equal(PosterCatalogWriteStatus.ValidationFailed,
            catalog.Create(1, "legacy-operation", Configuration()).Status);
        Assert.Single(catalog.List().Posters);
        Assert.Equal(2, store.ReadHead().Revision);
    }

    [Fact]
    public void FailureAfterInsertRollsBackPosterProfileAndBothJournals()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        fixture.Execute("""
            CREATE TRIGGER fail_receipt BEFORE INSERT ON poster_catalog_operations
            BEGIN SELECT RAISE(ABORT, 'fixture receipt failure'); END;
            """);
        var catalog = new SqlitePosterCatalogRepository(store);
        Assert.Equal(PosterCatalogWriteStatus.Failed, catalog.Create(0, "retry", Configuration()).Status);
        Assert.Empty(catalog.List().Posters);
        Assert.False(catalog.List().ProfileInitialized);
        Assert.Equal(0, catalog.List().CatalogVersion);
        Assert.Equal(0, store.ReadHead().Revision);
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM state_operations;"));
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM poster_catalog_operations;"));
        fixture.Execute("DROP TRIGGER fail_receipt;");
        Assert.True(catalog.Create(0, "retry", Configuration()).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidAssociationDoesNotCommit(string collectionId)
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var result = new SqlitePosterCatalogRepository(store).Create(
            0, "invalid", Configuration() with { CollectionId = collectionId });
        Assert.Equal(PosterCatalogWriteStatus.ValidationFailed, result.Status);
        Assert.Equal(0, store.ReadHead().Revision);
    }

    [Fact]
    public void V2MigrationPreservesEveryLegacyFieldAndCreatesValidatedSourceBackup()
    {
        using var fixture = new Fixture();
        LegacyStateDatabase.Create(fixture.DatabasePath, 2);
        fixture.Execute("""
            INSERT INTO posters VALUES (
                'poster-main', '旧海报', '旧标题', 'DISPLAY-OLD',
                -620.25, 38.75, 512, 880, 0.31, 0.45, 0.41, 0.88,
                1, 1, 1, 'grid', '{"iconSize":36,"custom":"keep"}', 'custom-template',
                6, '2026-09-01T01:02:03Z', '2026-09-02T04:05:06Z');
            UPDATE app_settings SET dynamic_effects_paused=1, performance_mode='static',
                launch_at_startup_requested=1, state_revision=7, updated_utc='2026-09-02T04:05:07Z';
            UPDATE state_head SET revision=7, last_operation_id='old-save';
            INSERT INTO state_operations VALUES('old-save',6,7,'2026-09-02T04:05:07Z');
            """);
        object? originalPoster = fixture.Scalar(LegacyPosterRow);
        object? originalSettings = fixture.Scalar(
            "SELECT json_array(dynamic_effects_paused, performance_mode, launch_at_startup_requested, state_revision, updated_utc) FROM app_settings;");
        using var store = new SqliteStateStore(fixture.Paths);
        StateStoreInitializationResult result = store.Initialize();
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StateStoreInitializationStatus.Migrated, result.Status);
        Assert.Equal(2, result.PreviousSchemaVersion);
        Assert.Equal(4, result.Head!.SchemaVersion);
        Assert.Equal(7, result.Head.Revision);
        Assert.Equal(originalPoster, fixture.Scalar(LegacyPosterRow));
        Assert.Equal(originalSettings, fixture.Scalar(
            "SELECT json_array(dynamic_effects_paused, performance_mode, launch_at_startup_requested, state_revision, updated_utc) FROM app_settings;"));
        PosterCatalogSnapshot catalog = new SqlitePosterCatalogRepository(store).List();
        PosterStateSnapshot poster = Assert.Single(catalog.Posters);
        Assert.True(catalog.ProfileInitialized);
        Assert.Equal(1, catalog.CatalogVersion);
        Assert.Equal("poster-main", poster.Poster.PosterId);
        Assert.Equal("theme-poster-main", poster.Poster.ThemeInstanceId);
        Assert.Null(poster.Poster.CollectionId);
        Assert.True(poster.Poster.IsVisible);
        Assert.Equal("general", poster.Poster.Purpose);
        Assert.Equal(0, poster.Poster.StackOrder);
        Assert.Equal(1, poster.EntityVersion);
        Assert.Equal(6, poster.PosterRevision);
        string backupPath = Path.Combine(fixture.Paths.Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(2L, fixture.Scalar("PRAGMA user_version;", backupPath));
        Assert.Equal(originalPoster, fixture.Scalar(LegacyPosterRow, backupPath));
        Assert.Equal("ok", fixture.Scalar("PRAGMA integrity_check;", backupPath));
        Assert.Equal(1L, fixture.Scalar("SELECT COUNT(*) FROM state_operations;"));
        Assert.Equal(StateStoreInitializationStatus.Ready, store.Initialize().Status);
        Assert.Single(Directory.EnumerateFiles(fixture.Paths.Backups));
        var currentBackup = store.CreateBackup("catalog");
        Assert.True(currentBackup.Succeeded, currentBackup.Error);
        Assert.True(store.ValidateBackup(Path.Combine(fixture.Paths.Backups, currentBackup.FileName!)).IsValid);
    }

    [Fact]
    public void EmptyV2MigrationRemainsEligibleForOneDefaultSeed()
    {
        using var fixture = new Fixture();
        LegacyStateDatabase.Create(fixture.DatabasePath, 2);
        using var store = fixture.Open();
        Assert.False(new SqlitePosterCatalogRepository(store).List().ProfileInitialized);
        var posters = new SqlitePosterRepository(store);
        Assert.Equal(1, posters.GetOrCreateDefault(Configuration()).Revision);
        Assert.Equal(1, posters.GetOrCreateDefault(Configuration()).Revision);
    }

    [Fact]
    public void MigrationFailureRollsBackNewColumnsAndRetainsOriginalDatabaseAndBackup()
    {
        using var fixture = new Fixture();
        LegacyStateDatabase.Create(fixture.DatabasePath, 2);
        // Fail late in v3, after ALTER/UPDATE/index statements have already executed.
        fixture.Execute("CREATE TABLE poster_profile (user_marker TEXT); INSERT INTO poster_profile VALUES('keep');");
        Assert.Equal("keep", fixture.Scalar("SELECT user_marker FROM poster_profile;"));
        Assert.Equal(2L, fixture.Scalar("PRAGMA user_version;"));
        using var store = new SqliteStateStore(fixture.Paths);
        var failed = store.Initialize();
        Assert.True(failed.Status == StateStoreInitializationStatus.Failed,
            $"Status={failed.Status}; Schema={fixture.Scalar("SELECT sql FROM sqlite_schema WHERE name='poster_profile';")}; Head={failed.Head}; Error={failed.Error}");
        Assert.Equal(2L, fixture.Scalar("PRAGMA user_version;"));
        Assert.Equal(2L, fixture.Scalar("SELECT schema_version FROM state_head;"));
        Assert.Equal(2L, fixture.Scalar("SELECT COUNT(*) FROM schema_migrations;"));
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM pragma_table_info('posters') WHERE name='entity_version';"));
        Assert.Equal("keep", fixture.Scalar("SELECT user_marker FROM poster_profile;"));
        Assert.Single(Directory.EnumerateFiles(fixture.Paths.Backups));
        fixture.Execute("DROP TABLE poster_profile;");
        Assert.True(store.Initialize().Succeeded);
    }

    [Fact]
    public void CloneReplayUsesOriginalReceiptEvenAfterSourceChangesOrIsRemoved()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var source = catalog.Create(0, "source", Configuration());
        var clone = catalog.CloneConfiguration(1, "clone-once", source.PosterId!);
        var posters = new SqlitePosterRepository(store);
        Assert.True(posters.Save(2, "source-edit",
            posters.Read(source.PosterId!)!.Poster with { Title = "changed" }).Succeeded);
        Assert.True(catalog.Remove(3, "remove-source", source.PosterId!).Succeeded);
        var replay = catalog.CloneConfiguration(1, "clone-once", source.PosterId!);
        Assert.Equal(PosterCatalogWriteStatus.Replayed, replay.Status);
        Assert.Equal(clone.PosterId, replay.PosterId);
        Assert.Equal("NIGHT OPS", Assert.Single(replay.Snapshot!.Posters).Poster.Title);
        Assert.Equal(4, store.ReadHead().Revision);
    }

    [Fact]
    public void RemoveFailurePreservesExistingPosterAndProfileVersions()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var created = catalog.Create(0, "create", Configuration());
        fixture.Execute("""
            CREATE TRIGGER fail_remove_receipt BEFORE INSERT ON poster_catalog_operations
            BEGIN SELECT RAISE(ABORT, 'fixture remove failure'); END;
            """);
        Assert.Equal(PosterCatalogWriteStatus.Failed, catalog.Remove(1, "remove", created.PosterId!).Status);
        Assert.Equal(created.PosterId, Assert.Single(catalog.List().Posters).Poster.PosterId);
        Assert.Equal(1, catalog.List().Revision);
        Assert.Equal(1, catalog.List().CatalogVersion);
        Assert.Equal(1L, fixture.Scalar("SELECT COUNT(*) FROM state_operations;"));
    }

    [Fact]
    public async Task ConcurrentFirstRunSeedsTheLegacyDefaultOnlyOnce()
    {
        using var fixture = new Fixture();
        using var first = fixture.Open();
        using var second = fixture.Open();
        var results = await Task.WhenAll(
            Task.Run(() => new SqlitePosterRepository(first).GetOrCreateDefault(Configuration())),
            Task.Run(() => new SqlitePosterRepository(second).GetOrCreateDefault(Configuration())));
        Assert.Equal(results[0], results[1]);
        Assert.Equal(1, first.ReadHead().Revision);
        Assert.Single(new SqlitePosterCatalogRepository(first).List().Posters);
    }

    [Fact]
    public void MissingCurrentProfileFailsClosedWithoutResettingTheDatabase()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var catalog = new SqlitePosterCatalogRepository(store);
        var created = catalog.Create(0, "create", Configuration());
        fixture.Execute("DELETE FROM poster_profile;");
        Assert.Equal(StateStoreInitializationStatus.Failed, store.Initialize().Status);
        Assert.Equal(created.PosterId, fixture.Scalar("SELECT poster_id FROM posters;"));
        Assert.Equal(1L, fixture.Scalar("SELECT revision FROM state_head;"));
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM poster_profile;"));
    }

    [Fact]
    public void V3MigrationAddsCommandSchemaWithoutChangingSettingsOrEmptyProfile()
    {
        using var fixture = new Fixture();
        LegacyStateDatabase.Create(fixture.DatabasePath, 3);
        fixture.Execute("UPDATE app_settings SET dynamic_effects_paused=1, state_revision=7; UPDATE state_head SET revision=7; UPDATE poster_profile SET profile_initialized=1;");
        using var store = new SqliteStateStore(fixture.Paths);
        var result = store.Initialize();
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3, result.PreviousSchemaVersion);
        Assert.Equal(4, result.Head!.SchemaVersion);
        Assert.Equal(7, result.Head.Revision);
        var settings = new SqliteSettingsRepository(store).Read();
        Assert.True(settings.Settings.DynamicEffectsPaused);
        Assert.Equal(1, settings.EntityVersion);
        Assert.Equal(7, settings.SettingsRevision);
        string backupPath = Path.Combine(fixture.Paths.Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(3, store.ValidateBackup(backupPath).SchemaVersion);
        var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(store));
        Assert.Empty(coordinator.EnsureInitialized(Configuration()).Posters);
        Assert.Equal(7, store.ReadHead().Revision);
    }

    [Fact]
    public void V4MigrationFailureRollsBackAddedColumnAndKeepsV3Ledger()
    {
        using var fixture = new Fixture();
        LegacyStateDatabase.Create(fixture.DatabasePath, 3);
        fixture.Execute("CREATE TABLE state_command_receipts (broken TEXT);");
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.Equal(StateStoreInitializationStatus.Failed, store.Initialize().Status);
        Assert.Equal(3L, fixture.Scalar("PRAGMA user_version;"));
        Assert.Equal(3L, fixture.Scalar("SELECT COUNT(*) FROM schema_migrations;"));
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM pragma_table_info('app_settings') WHERE name='entity_version';"));
    }

    [Fact]
    public void MissingCurrentCommandSchemaFailsClosed()
    {
        using var fixture = new Fixture();
        using var store = fixture.Open();
        fixture.Execute("DROP TABLE state_command_receipts;");
        Assert.Equal(StateStoreInitializationStatus.Failed, store.Initialize().Status);
        Assert.Equal(4L, fixture.Scalar("PRAGMA user_version;"));
    }

    private const string LegacyPosterRow = """
        SELECT json_array(poster_id, title, subtitle, monitor_device_name, fallback_x_dip,
            fallback_y_dip, width_dip, expanded_height_dip, horizontal_anchor, vertical_anchor,
            normalized_width, normalized_expanded_height, is_locked, is_collapsed,
            auto_collapse_enabled, layout_kind, layout_parameters_json, template_key,
            state_revision, created_utc, updated_utc) FROM posters WHERE poster_id='poster-main';
        """;


    private static PosterDefinition Configuration() => new(
        BuiltInStateIdentities.PrimaryPosterId, "NIGHT OPS", "TACTICAL ARCHIVE / 01",
        "DISPLAY1", 1450, 24, 420, 720, 0.97, 0.02, 0.22, 0.7,
        false, false, false, "grid", "{}", "night-ops");

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.Catalog.Tests", Guid.NewGuid().ToString("N"));
        public Fixture() { Paths = new UserDataPaths(_root); Paths.EnsureCreated(); }
        public UserDataPaths Paths { get; }
        public string DatabasePath => Path.Combine(Paths.State, "desktopup.sqlite3");
        public SqliteStateStore Open()
        {
            var store = new SqliteStateStore(Paths);
            var result = store.Initialize();
            Assert.True(result.Succeeded, result.Error);
            return store;
        }
        public object? Scalar(string sql, string? databasePath = null)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath ?? DatabasePath};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }
        public void Execute(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
