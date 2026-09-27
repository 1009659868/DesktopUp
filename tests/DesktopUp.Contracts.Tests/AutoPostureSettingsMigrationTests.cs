using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSettingsTests
{
    private static void SeedV6(Fixture f)
    {
        LegacyStateDatabase.Create(f.DatabasePath, 6);
        f.Execute("""
            INSERT INTO posters (poster_id,title,subtitle,monitor_device_name,fallback_x_dip,fallback_y_dip,
                width_dip,expanded_height_dip,horizontal_anchor,vertical_anchor,normalized_width,normalized_expanded_height,
                is_locked,is_collapsed,auto_collapse_enabled,layout_kind,layout_parameters_json,template_key,state_revision,
                created_utc,updated_utc,purpose,is_visible,stack_order,entity_version,collection_id,theme_instance_id,
                layout_generation,last_layout_operation_id)
            VALUES ('A','旧海报','keep','DISPLAY-OLD',-100.25,40.5,400,700,.2,.3,.4,.7,
                1,1,1,'grid','{"legacy":"keep"}','custom',8,'2026-09-01T00:00:00Z','2026-09-02T00:00:00Z',
                'games',0,2,5,'collection-A','theme-A',3,'old-layout');
            INSERT INTO state_operations VALUES ('old-layout',7,8,'2026-09-02T00:00:00Z');
            UPDATE state_head SET revision=8,last_operation_id='old-layout';
            UPDATE poster_profile SET profile_initialized=1,catalog_version=4,state_revision=8;
            UPDATE app_settings SET dynamic_effects_paused=1,performance_mode='static',launch_at_startup_requested=1,
                entity_version=4,state_revision=7;
            INSERT INTO spatial_operations VALUES ('old-layout','A',1,'active','{"keep":"proof"}',8);
            INSERT INTO spatial_participants VALUES ('old-layout','A',3,'{"keep":"participant"}',NULL);
            INSERT INTO spatial_continuity VALUES ('display-old',1,'{"keep":"continuity"}',8);
            INSERT INTO state_command_receipts VALUES ('old-no-change','keep-hash','no_change',8,NULL,'A');
            """);
    }

    // Compare every historical column; fields introduced after these fixtures are asserted separately.
    private static string Rows(Fixture f, string table, string? path = null)
    {
        using var c = f.Open(path); using var q = c.CreateCommand();
        q.CommandText = $"SELECT * FROM {table} ORDER BY 1;";
        using var r = q.ExecuteReader(); var rows = new List<object?[]>();
        while (r.Read()) rows.Add(Enumerable.Range(0, r.FieldCount).Where(i => r.GetName(i) is not ("auto_collapse_delay_seconds" or "placement_pending" or "content_layout_json" or "center_theme" or "template_catalog_json"))
            .Select(i => r.IsDBNull(i) ? null : new { Name = r.GetName(i), Type = r.GetDataTypeName(i), Value = r.GetValue(i) }).Cast<object?>().ToArray());
        return JsonSerializer.Serialize(rows);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Migration_preserves_both_values_of_the_existing_auto_switch(bool enabled)
    {
        using var f = new Fixture(false); SeedV6(f);
        f.Execute($"UPDATE posters SET auto_collapse_enabled={(enabled ? 1 : 0)};");
        Assert.True(f.Store.Initialize().Succeeded);
        var policy = AutoPosturePolicy.From(new SqlitePosterRepository(f.Store).Read("A")!.Poster);
        Assert.Equal(new AutoPosturePolicy(enabled, 3), policy);
    }

    [Fact]
    public void V6_migration_preserves_every_old_row_and_version_and_validates_source_backup()
    {
        using var f = new Fixture(false); SeedV6(f);
        string[] tables = ["posters", "app_settings", "poster_profile", "state_operations", "state_command_receipts",
            "poster_catalog_operations", "spatial_operations", "spatial_participants", "spatial_continuity"];
        var before = tables.ToDictionary(t => t, t => Rows(f, t));
        var result = f.Store.Initialize(); Assert.True(result.Succeeded, result.Error);
        Assert.Equal(6, result.PreviousSchemaVersion); Assert.Equal(12, result.Head!.SchemaVersion); Assert.Equal(8, result.Head.Revision);
        string backup = Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(6L, f.Scalar("PRAGMA user_version;", backup)); Assert.Equal("ok", f.Scalar("PRAGMA integrity_check;", backup));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM pragma_table_info('posters') WHERE name='auto_collapse_delay_seconds';", backup));
        foreach (string table in tables)
        {
            Assert.Equal(before[table], Rows(f, table)); Assert.Equal(before[table], Rows(f, table, backup));
        }
        Assert.Equal(3L, f.Scalar("SELECT auto_collapse_delay_seconds FROM posters;"));
        var poster = new SqlitePosterRepository(f.Store).Read("A")!;
        Assert.True(poster.Poster.AutoCollapseEnabled); Assert.True(poster.Poster.IsCollapsed); Assert.True(poster.Poster.IsLocked);
        Assert.Equal(5, poster.EntityVersion); Assert.Equal(3, poster.LayoutGeneration);
        Assert.Equal(StateStoreInitializationStatus.Ready, f.Store.Initialize().Status);
        Assert.Single(Directory.EnumerateFiles(f.Paths.Backups));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Empty_v6_preserves_initialized_flag_without_reseeding(bool initialized)
    {
        using var f = new Fixture(false); LegacyStateDatabase.Create(f.DatabasePath, 6);
        if (initialized) f.Execute("UPDATE poster_profile SET profile_initialized=1,catalog_version=9,state_revision=0;");
        Assert.True(f.Store.Initialize().Succeeded);
        var catalog = new SqlitePosterCatalogRepository(f.Store).List();
        Assert.Equal(initialized, catalog.ProfileInitialized); Assert.Empty(catalog.Posters);
        var result = f.Coordinator.EnsureInitialized(Seed());
        Assert.Equal(initialized ? 0 : 1, result.Posters.Count);
        if (initialized)
        {
            Assert.Equal(0, result.Revision); Assert.Equal(9, result.CatalogVersion); Assert.Equal(0, result.Tray.AutoPostureEnabledPosterCount);
        }
    }

    [Fact]
    public void Late_migration_failure_rolls_back_column_and_ledger_and_retains_v6_backup()
    {
        using var f = new Fixture(false); SeedV6(f); string before = Rows(f, "posters");
        f.Execute("""
            CREATE TRIGGER fail_v7 BEFORE UPDATE OF schema_version ON state_head WHEN NEW.schema_version=7
            BEGIN SELECT RAISE(ABORT,'late migration fixture'); END;
            """);
        var result = f.Store.Initialize(); Assert.False(result.Succeeded);
        Assert.Equal(6L, f.Scalar("PRAGMA user_version;")); Assert.Equal(6L, f.Scalar("SELECT schema_version FROM state_head;"));
        Assert.Equal(6L, f.Scalar("SELECT COUNT(*) FROM schema_migrations;"));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM pragma_table_info('posters') WHERE name='auto_collapse_delay_seconds';"));
        Assert.Equal(before, Rows(f, "posters"));
        string backup = Assert.Single(Directory.EnumerateFiles(f.Paths.Backups)); Assert.Equal(before, Rows(f, "posters", backup));
        f.Execute("DROP TRIGGER fail_v7;"); Assert.True(f.Store.Initialize().Succeeded); Assert.Equal(before, Rows(f, "posters"));
    }

    [Fact]
    public void Backup_failure_does_not_begin_v7_migration()
    {
        using var f = new Fixture(false); SeedV6(f);
        // This isolated fixture creates a file where only its own backup directory should be.
        Directory.Delete(f.Paths.Backups); File.WriteAllText(f.Paths.Backups, "fixture");
        Assert.Throws<IOException>(() => f.Store.Initialize());
        Assert.Equal(6L, f.Scalar("PRAGMA user_version;"));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM pragma_table_info('posters') WHERE name='auto_collapse_delay_seconds';"));
    }

    [Theory]
    [InlineData("0")] [InlineData("301")] [InlineData("1.5")] [InlineData("NULL")]
    public void Database_rejects_invalid_delay_without_changing_valid_row(string sqlValue)
    {
        using var f = new Fixture();
        Assert.Throws<SqliteException>(() => f.Execute($"UPDATE posters SET auto_collapse_delay_seconds={sqlValue};"));
        Assert.Equal(3L, f.Scalar("SELECT auto_collapse_delay_seconds FROM posters;"));
    }

    [Fact]
    public void Current_schema_missing_delay_column_fails_closed()
    {
        using var f = new Fixture(); f.Execute("ALTER TABLE posters DROP COLUMN auto_collapse_delay_seconds;");
        Assert.False(f.Store.Initialize().Succeeded); Assert.Equal(12L, f.Scalar("PRAGMA user_version;"));
    }

    [Fact]
    public void Current_backup_preserves_settings_and_unrelated_future_schema_is_rejected()
    {
        using var f = new Fixture(seed: Seed() with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 300 });
        var backup = f.Store.CreateBackup("m08-settings"); Assert.True(backup.Succeeded, backup.Error);
        string path = Path.Combine(f.Paths.Backups, backup.FileName!); Assert.True(f.Store.ValidateBackup(path).IsValid);
        Assert.Equal(300L, f.Scalar("SELECT auto_collapse_delay_seconds FROM posters;", path));
        // 12 is the current schema; 13 is the next unrelated future version and must still be rejected.
        f.Execute("PRAGMA user_version=13;");
        Assert.False(f.Store.Initialize().Succeeded); Assert.Equal(300L, f.Scalar("SELECT auto_collapse_delay_seconds FROM posters;"));
    }

    private static string V6Fingerprint<T>(T request)
    {
        // Independent old-wire shape: v6 has no delay property at any PosterDefinition node.
        // Retain the original serializer bytes/escaping for every other member.
        string json = JsonSerializer.Serialize(request);
        var old = JsonNode.Parse(json)!;
        void RemoveDelay(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                obj.Remove(nameof(PosterDefinition.AutoCollapseDelaySeconds));
                obj.Remove(nameof(PosterDefinition.PlacementPending));
                foreach (var pair in obj) RemoveDelay(pair.Value);
            }
            else if (node is JsonArray list) foreach (var child in list) RemoveDelay(child);
        }
        RemoveDelay(old);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(old.ToJsonString())));
    }

    [Fact]
    public void Default_delay_keeps_v6_wire_fingerprint_but_nondefault_participates_in_hash()
    {
        var p = Seed() with { Title = "旧海报" };
        var oldWire = new { kind = "create", input = p };
        Assert.Equal(V6Fingerprint(oldWire), StateRequestFingerprint.Compute(oldWire));
        Assert.NotEqual(StateRequestFingerprint.Compute(oldWire), StateRequestFingerprint.Compute(new { kind = "create", input = p with { AutoCollapseDelaySeconds = 9 } }));
    }

    [Fact]
    public void V6_command_receipt_replays_after_migration_without_mutation_or_duplicate_commit()
    {
        using var f = new Fixture(false); SeedV6(f);
        var command = new PosterStateCommand("old-command", StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
            7, "A", Seed() with { Title = "old-result" }) { ReadSet = StateReadSet.ForPoster("A", 4) };
        string hash = V6Fingerprint(new { protocol = "desktopup-state-command-v2", type = nameof(PosterStateCommand), command });
        f.Execute($"INSERT INTO state_command_receipts VALUES('old-command','{hash}','committed',7,8,'A');");
        Assert.True(f.Store.Initialize().Succeeded);
        // Invalid spatial fixture JSON is intentionally only row-preservation evidence in SeedV6;
        // remove it here so this receipt test exercises the standard full projection loader.
        f.Execute("DELETE FROM spatial_continuity; DELETE FROM spatial_participants; DELETE FROM spatial_operations;");
        f.Coordinator.Refresh(); string before = Rows(f, "posters");
        var replay = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Replayed, replay.Status); Assert.Equal(8, replay.CommittedRevision);
        Assert.Equal(8, f.Current.Revision); Assert.Equal(before, Rows(f, "posters"));
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command with
            { DesiredState = command.DesiredState with { AutoCollapseDelaySeconds = 9 } }).Status);
    }

    [Fact]
    public void V6_direct_catalog_receipt_is_still_replayable_after_migration()
    {
        using var f = new Fixture(false); SeedV6(f);
        var input = Seed() with { PosterId = "new", ThemeInstanceId = "new", StackOrder = 0 };
        string hash = V6Fingerprint(new { kind = "create", input });
        f.Execute($"INSERT INTO poster_catalog_operations VALUES('old-layout','{hash}','A');");
        Assert.True(f.Store.Initialize().Succeeded);
        var replay = new SqlitePosterCatalogRepository(f.Store).Create(7, "old-layout", Seed());
        Assert.Equal(PosterCatalogWriteStatus.Replayed, replay.Status); Assert.Equal("A", replay.PosterId);
        Assert.Equal(8, f.Store.ReadHead().Revision); Assert.Single(replay.Snapshot!.Posters);
    }
}
