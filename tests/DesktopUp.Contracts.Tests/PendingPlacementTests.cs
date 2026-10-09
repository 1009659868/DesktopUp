using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSettingsTests
{
    [Fact]
    public void Placement_pending_defaults_false_and_repository_roundtrip_keeps_visibility_intent()
    {
        using var f = new Fixture();
        var repository = new SqlitePosterRepository(f.Store);
        var initial = repository.Read("A")!;
        Assert.False(initial.Poster.PlacementPending);
        var pending = initial.Poster with { PlacementPending = true, AutoCollapseEnabled = true, IsVisible = true };
        var save = repository.Save(initial.Revision, "pending-placement", pending);
        Assert.True(save.Succeeded, save.Error);
        Assert.Equal(pending, save.Snapshot!.Poster);
        Assert.Equal(initial.LayoutGeneration + 1, save.Snapshot.LayoutGeneration);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var restored = new SqlitePosterRepository(reopened).Read("A")!;
        Assert.Equal(pending, restored.Poster);
        var placed = repository.Save(restored.Revision, "place-poster", pending with { PlacementPending = false });
        Assert.True(placed.Succeeded, placed.Error);
        Assert.True(placed.Snapshot!.Poster.IsVisible);
        Assert.False(placed.Snapshot.Poster.PlacementPending);
        Assert.Equal(restored.LayoutGeneration + 1, placed.Snapshot.LayoutGeneration);
    }

    [Fact]
    public void V8_pending_migration_preserves_existing_state_versions_and_validated_source_backup()
    {
        using var f = new Fixture(false);
        LegacyStateDatabase.Create(f.DatabasePath, 8);
        f.Execute("""
            INSERT INTO state_operations VALUES('old-create',3,4,'2026-09-15T00:00:00Z');
            UPDATE state_head SET revision=4,last_operation_id='old-create';
            UPDATE poster_profile SET profile_initialized=1,catalog_version=2,state_revision=4;
            """);
        AddV7Layout(f, kind: PosterPresentationPolicy.AdaptiveGridLayout, receiptPrefix: null);
        string before = PendingFields(f, excludePending: true);
        string profile = Rows(f, "poster_profile"), settings = Rows(f, "app_settings");
        var result = f.Store.Initialize();
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(8, result.PreviousSchemaVersion);
        Assert.Equal(14, result.Head!.SchemaVersion);
        Assert.Equal(4, result.Head.Revision);
        Assert.Equal(before, PendingFields(f, excludePending: true));
        Assert.Equal(profile, Rows(f, "poster_profile"));
        Assert.Equal(settings, Rows(f, "app_settings"));
        Assert.False(new SqlitePosterRepository(f.Store).Read("A")!.Poster.PlacementPending);
        string backup = Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(8L, f.Scalar("PRAGMA user_version;", backup));
        Assert.Equal("ok", f.Scalar("PRAGMA quick_check;", backup));
        Assert.Equal(0L, f.Scalar("SELECT count(*) FROM pragma_table_info('posters') WHERE name='placement_pending';", backup));
        Assert.Equal(StateStoreInitializationStatus.Ready, f.Store.Initialize().Status);
        Assert.Single(Directory.GetFiles(f.Paths.Backups, "*.sqlite3"));
    }

    [Fact]
    public void V9_late_migration_fault_rolls_back_column_and_keeps_v8_backup()
    {
        using var f = new Fixture(false);
        LegacyStateDatabase.Create(f.DatabasePath, 8);
        f.Execute("""
            CREATE TRIGGER fail_v9 BEFORE INSERT ON schema_migrations WHEN NEW.version=9
            BEGIN SELECT RAISE(ABORT,'v9 injected failure'); END;
            """);
        var result = f.Store.Initialize();
        Assert.False(result.Succeeded);
        Assert.Equal(8L, f.Scalar("PRAGMA user_version;"));
        Assert.Equal(0L, f.Scalar("SELECT count(*) FROM pragma_table_info('posters') WHERE name='placement_pending';"));
        Assert.Equal(8L, f.Scalar("SELECT count(*) FROM schema_migrations;"));
        Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(8L, f.Scalar("PRAGMA user_version;", Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!)));
    }

    [Theory]
    [InlineData("-1")] [InlineData("2")] [InlineData("NULL")] [InlineData("0.5")]
    public void Pending_column_rejects_invalid_values_without_state_change(string value)
    {
        using var f = new Fixture();
        Assert.Throws<SqliteException>(() => f.Execute($"UPDATE posters SET placement_pending={value};"));
        Assert.False(f.Current.PostersById["A"].Poster.PlacementPending);
    }

    [Fact]
    public void Missing_current_pending_column_fails_closed()
    {
        using var f = new Fixture();
        f.Execute("ALTER TABLE posters DROP COLUMN placement_pending;");
        Assert.False(f.Store.Initialize().Succeeded);
        Assert.Equal(14L, f.Scalar("PRAGMA user_version;"));
    }

    [Fact]
    public void Pending_field_false_preserves_v8_request_fingerprint_but_true_changes_it()
    {
        var p = Seed();
        var oldWire = JsonNode.Parse(JsonSerializer.Serialize(p))!.AsObject();
        oldWire.Remove(nameof(PosterDefinition.PlacementPending));
        oldWire.Remove(nameof(PosterDefinition.AutoCollapseDelaySeconds));
        string legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(oldWire.ToJsonString(
            new JsonSerializerOptions { WriteIndented = false }))));
        Assert.Equal(legacyHash, StateRequestFingerprint.Compute(p));
        Assert.NotEqual(legacyHash, StateRequestFingerprint.Compute(p with { PlacementPending = true }));
    }

    private static string PendingFields(Fixture f, bool excludePending)
    {
        using var c = f.Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT * FROM posters ORDER BY poster_id;";
        using var reader = q.ExecuteReader();
        var rows = new List<SortedDictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i) != "content_layout_json" && (!excludePending || reader.GetName(i) != "placement_pending"))
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows);
    }
}

public sealed partial class PosterSessionRegistryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pending_catalog_roundtrip_does_not_create_a_session_on_start_recovery_or_visibility_toggle(bool visible)
    {
        using var f = new Fixture();
        string id = f.Add(Configuration() with { PlacementPending = true, IsVisible = visible, AutoCollapseEnabled = true });
        Assert.True(f.Recover());
        Assert.Single(f.Registry.Sessions);
        Assert.DoesNotContain(f.Factory.Created, p => p.PosterId == id);
        var projected = f.Current.PostersById[id];
        Assert.Equal(visible, projected.Poster.IsVisible);
        Assert.False(projected.IsEffectivelyVisible);
        Assert.False(projected.CanMove);
        Assert.False(projected.CanResize);
        Assert.False(projected.CanToggleCollapsed);
        Assert.Equal(1, f.Current.Tray.PendingPlacementPosterCount);
        Assert.Equal(1, f.Current.Tray.VisiblePosterCount);
        Assert.Contains("等待安置", f.Current.Tray.ToolTipText);
        foreach (bool intent in new[] { true, false, true })
        {
            Assert.True(f.Set(id, p => p with { IsVisible = intent }, PosterStateCommandKind.SetVisible).Succeeded);
            Assert.True(f.Current.PostersById[id].Poster.PlacementPending);
            Assert.True(f.Recover(2));
            Assert.DoesNotContain(f.Registry.Sessions, p => p.PosterId == id);
        }
        var restart = f.Reopen();
        Assert.True(restart.PostersById[id].Poster.PlacementPending);
        Assert.True(restart.PostersById[id].Poster.IsVisible);
        var selection = new ControlCenterSelection();
        selection.ApplyProjection(restart.ControlCenter); selection.Select(id);
        var view = selection.BuildView(f.Registry.Sessions, false)!;
        Assert.Equal("等待安置", view.Posters.Single(p => p.PosterId == id).RuntimeStatus);
        Assert.Null(view.CreatePosterCommand(PosterStateCommandKind.SetCollapsed, "pending-collapse"));
    }

    [Fact]
    public void Pending_transition_cancels_old_incarnation_and_only_placement_release_can_recreate_it()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        var old = f.Factory.For(Main);
        var oldEpoch = Assert.Single(f.Registry.Sessions).Epoch;
        SetPending(f, Main, true);
        Assert.Empty(f.Registry.Sessions);
        Assert.True(old.Disposed);
        Assert.True(old.Token.IsCancellationRequested);
        f.Registry.SuspendEnvironment(10);
        f.Registry.SetGloballyHidden(true); f.Registry.SetGloballyHidden(false);
        Assert.True(f.Recover(10));
        Assert.Empty(f.Registry.Sessions);
        Assert.Single(f.Factory.Created);
        SetPending(f, Main, false);
        Assert.Single(f.Registry.Sessions);
        Assert.True(Assert.Single(f.Registry.Sessions).Epoch > oldEpoch);
        Assert.Equal(2, f.Factory.Created.Count);
        Assert.False(f.Current.PostersById[Main].Poster.PlacementPending);
        Assert.True(f.Current.PostersById[Main].Poster.IsVisible);
    }

    [Theory]
    [InlineData(PosterStateCommandKind.Move)]
    [InlineData(PosterStateCommandKind.Resize)]
    [InlineData(PosterStateCommandKind.SetCollapsed)]
    public void Pending_posters_reject_ordinary_geometry_and_posture_commands(PosterStateCommandKind kind)
    {
        using var f = new Fixture();
        string id = f.Add(Configuration() with { PlacementPending = true });
        var before = f.Current;
        var result = f.Set(id, p => kind switch
        {
            PosterStateCommandKind.Move => p with { FallbackXDip = 50 },
            PosterStateCommandKind.Resize => p with { WidthDip = 600 },
            _ => p with { IsCollapsed = !p.IsCollapsed }
        }, kind);
        Assert.Equal(StateCommandStatus.PermissionDenied, result.Status);
        Assert.Equal(before.Revision, f.Current.Revision);
        Assert.Equal(before.PostersById[id].Poster, f.Current.PostersById[id].Poster);
    }

    [Theory]
    [InlineData(PosterStateCommandKind.Rename)]
    [InlineData(PosterStateCommandKind.SetLocked)]
    [InlineData(PosterStateCommandKind.SetVisible)]
    [InlineData(PosterStateCommandKind.SetAutoPosture)]
    public void Non_spatial_settings_preserve_pending_and_cannot_clear_it(PosterStateCommandKind kind)
    {
        using var f = new Fixture();
        string id = f.Add(Configuration() with { PlacementPending = true });
        PosterDefinition Change(PosterDefinition p) => kind switch
        {
            PosterStateCommandKind.Rename => p with { Title = "Renamed" },
            PosterStateCommandKind.SetLocked => p with { IsLocked = true },
            PosterStateCommandKind.SetVisible => p with { IsVisible = false },
            _ => p with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 9 }
        };
        Assert.True(f.Set(id, Change, kind).Succeeded);
        Assert.True(f.Current.PostersById[id].Poster.PlacementPending);
        var rejected = f.Set(id, p => Change(p) with { PlacementPending = false }, kind);
        Assert.Equal(StateCommandStatus.ValidationFailed, rejected.Status);
        Assert.True(f.Current.PostersById[id].Poster.PlacementPending);
    }

    private static void SetPending(Fixture f, string id, bool pending)
    {
        using var store = new SqliteStateStore(f.Paths);
        Assert.True(store.Initialize().Succeeded);
        var poster = f.Current.PostersById[id].Poster;
        var result = new SqlitePosterRepository(store).Save(f.Current.Revision, Id(), poster with { PlacementPending = pending });
        Assert.True(result.Succeeded, result.Error);
        f.Coordinator.Refresh();
    }
}
