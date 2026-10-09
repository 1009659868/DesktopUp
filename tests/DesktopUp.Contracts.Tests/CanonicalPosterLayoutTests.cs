using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSettingsTests
{
    // Captured shipped BUG-005 payload, deliberately independent of the new default factory.
    private const string OldGridJson = """{"schemaVersion":1,"content":{"itemWidthDip":100,"itemHeightDip":72,"iconSizeDip":40,"gapDip":12,"labelFontSizeDip":12,"titleFontSizeDip":24},"theme":{"background":"#282B30","foreground":"#F2F2F2","accent":"#68BDFF"}}""";

    private static void StartV7Layout(Fixture f)
    {
        LegacyStateDatabase.Create(f.DatabasePath, 7);
        f.Execute("""
            INSERT INTO state_operations VALUES('old-create',3,4,'2026-09-15T00:00:00Z');
            UPDATE state_head SET revision=4,last_operation_id='old-create';
            UPDATE poster_profile SET profile_initialized=1,catalog_version=2,state_revision=4;
            """);
    }

    private static void AddV7Layout(Fixture f, string id = "A", string? json = null,
        string kind = "grid", string template = "basic-box", string? receiptPrefix = "controlcenter-catalog-")
    {
        using var c = f.Open(); using var q = c.CreateCommand();
        q.CommandText = """
            INSERT INTO posters (poster_id,title,subtitle,monitor_device_name,fallback_x_dip,fallback_y_dip,
                width_dip,expanded_height_dip,horizontal_anchor,vertical_anchor,normalized_width,normalized_expanded_height,
                is_locked,is_collapsed,auto_collapse_enabled,layout_kind,layout_parameters_json,template_key,state_revision,
                created_utc,updated_utc,purpose,is_visible,stack_order,entity_version,collection_id,theme_instance_id,
                layout_generation,last_layout_operation_id,auto_collapse_delay_seconds)
            VALUES ($id,'Keep title','Keep subtitle','DISPLAY-OLD',-10.5,25,300,360,.2,.4,.2,.4,
                1,1,1,$kind,$json,$template,4,'2026-09-14T00:00:00Z','2026-09-15T00:00:00Z',
                'games',0,1,7,'collection-A','theme-'||$id,3,'old-create',9);
            """;
        q.Parameters.AddWithValue("$id", id); q.Parameters.AddWithValue("$kind", kind);
        q.Parameters.AddWithValue("$json", json ?? OldGridJson); q.Parameters.AddWithValue("$template", template);
        q.ExecuteNonQuery();
        if (receiptPrefix is not null)
        {
            q.CommandText = """
                INSERT INTO state_command_receipts VALUES($request,'original-request-hash','committed',3,4,$id);
                """;
            q.Parameters.AddWithValue("$request", receiptPrefix + id); q.ExecuteNonQuery();
        }
    }

    [Theory]
    [InlineData(StateCommandSource.ControlCenter)]
    [InlineData(StateCommandSource.Tray)]
    public void New_production_configuration_can_edit_template_macro_and_restart(StateCommandSource source)
    {
        using var f = new Fixture(seed: PosterConfigurationFactory.NewBasicBox("Existing", "DISPLAY1") with { PosterId = "A" });
        var before = f.Current;
        var config = PosterConfigurationFactory.NewBasicBox("New", "DISPLAY1");
        Assert.Equal(OldGridJson, config.LayoutParametersJson);
        Assert.Equal(PosterPresentationPolicy.AdaptiveGridLayout, config.LayoutKind);
        var created = f.Coordinator.Execute(new CreatePosterCommand("new-layout", source, before.Revision,
            StateReadSet.ForCatalog(before.CatalogVersion), config));
        Assert.True(created.Succeeded, created.Error);
        string id = created.ResultPosterId!;
        Assert.True(f.Current.PostersById[id].Presentation.CanEdit);
        foreach (var edit in new[] { PosterSetting.Template, PosterSetting.Scale, PosterSetting.Spacing })
        {
            var p = f.Current.PostersById[id];
            var result = f.Coordinator.Execute(new PosterSettingEdit(p, edit, Number: 1.25, Text: "night-ops").Command("edit-" + edit));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(p.Revision + 1, result.CurrentRevision);
        }
        using var reloaded = new SqliteStateStore(f.Paths); Assert.True(reloaded.Initialize().Succeeded);
        var after = new StateProjectionCoordinator(new SqliteStateCommandRepository(reloaded)).Refresh();
        Assert.Equal("night-ops", after.PostersById[id].Poster.TemplateKey);
        Assert.Equal(new PosterContentPreferences(1.25, 1.25), after.PostersById[id].Presentation.Parameters.Preferences);
        Assert.Equal(before.PostersById["A"].Poster, after.PostersById["A"].Poster);
        Assert.Equal(created.Projection!.PostersById[id].Poster.WidthDip, after.PostersById[id].Poster.WidthDip);
    }

    [Fact]
    public void V7_known_created_layouts_repair_in_one_revision_with_backup_and_no_geometry_changes()
    {
        using var f = new Fixture(false); StartV7Layout(f);
        AddV7Layout(f); AddV7Layout(f, "B", receiptPrefix: "tray-create-");
        string receipts = Rows(f, "state_command_receipts"), profile = Rows(f, "poster_profile");
        var result = f.Store.Initialize(); Assert.True(result.Succeeded, result.Error);
        Assert.Equal(14, result.Head!.SchemaVersion); Assert.Equal(5, result.Head.Revision);
        string backup = Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!);
        // The migration validates its source schema before committing; public ValidateBackup expects current schema.
        Assert.Equal(7, f.Store.ValidateBackup(backup).SchemaVersion);
        Assert.Equal("ok", f.Scalar("PRAGMA integrity_check;", backup));
        Assert.Equal(7L, f.Scalar("PRAGMA user_version;", backup));
        Assert.Equal(2L, f.Scalar("SELECT COUNT(*) FROM posters WHERE layout_kind='grid';", backup));
        foreach (string id in new[] { "A", "B" })
        {
            var row = new SqlitePosterRepository(f.Store).Read(id)!;
            Assert.Equal("adaptive-grid", row.Poster.LayoutKind); Assert.Equal(OldGridJson, row.Poster.LayoutParametersJson);
            Assert.Equal(8, row.EntityVersion); Assert.Equal(3, row.LayoutGeneration);
            Assert.Equal("old-create", row.LastLayoutOperationId);
            Assert.Equal(-10.5, row.Poster.FallbackXDip); Assert.Equal(25, row.Poster.FallbackYDip);
            Assert.Equal(300, row.Poster.WidthDip); Assert.Equal(360, row.Poster.ExpandedHeightDip);
            Assert.Equal(.4, row.Poster.VerticalAnchor);
            Assert.True(row.Poster.IsLocked && row.Poster.IsCollapsed && row.Poster.AutoCollapseEnabled);
            Assert.False(row.Poster.IsVisible); Assert.Equal(9, row.Poster.AutoCollapseDelaySeconds);
            Assert.Equal("theme-" + id, row.Poster.ThemeInstanceId); Assert.Equal("collection-A", row.Poster.CollectionId);
            Assert.Equal("Keep title", row.Poster.Title); Assert.Equal("Keep subtitle", row.Poster.Subtitle);
            Assert.True(PosterPresentationPolicy.Resolve(row.Poster).CanEdit);
        }
        Assert.Equal(receipts, Rows(f, "state_command_receipts")); Assert.Equal(profile, Rows(f, "poster_profile"));
        Assert.Equal(5L, f.Scalar("SELECT committed_revision FROM state_operations WHERE operation_id='migration-v8-canonical-created-layout';"));
        Assert.Equal(StateStoreInitializationStatus.Ready, f.Store.Initialize().Status);
        Assert.Equal(5, f.Store.ReadHead().Revision); Assert.Single(Directory.EnumerateFiles(f.Paths.Backups));
        var p = f.Current.PostersById["A"];
        Assert.True(f.Coordinator.Execute(new PosterSettingEdit(p, PosterSetting.Scale, 1.2).Command("after-repair")).Succeeded);
        Assert.Equal(1.2, f.Current.PostersById["A"].Presentation.Parameters.Preferences!.Scale);
    }

    [Theory]
    [InlineData("no-receipt")] [InlineData("unrelated-receipt")] [InlineData("custom")]
    [InlineData("future-json")] [InlineData("different-json")] [InlineData("legacy-json")]
    [InlineData("already-canonical")] [InlineData("unknown-layout")]
    public void V8_does_not_guess_about_other_grid_or_unknown_configurations(string variant)
    {
        using var f = new Fixture(false); StartV7Layout(f);
        AddV7Layout(f, json: variant switch { "future-json" => """{"schemaVersion":99}""",
            "different-json" => OldGridJson.Replace("100", "120"), "legacy-json" => "{}", _ => OldGridJson },
            kind: variant == "already-canonical" ? "adaptive-grid" : variant == "unknown-layout" ? "future-layout" : "grid",
            template: variant == "custom" ? "custom" : "basic-box",
            receiptPrefix: variant == "no-receipt" ? null : variant == "unrelated-receipt" ? "other-" : "controlcenter-catalog-");
        string before = Rows(f, "posters");
        Assert.True(f.Store.Initialize().Succeeded);
        Assert.Equal(before, Rows(f, "posters")); Assert.Equal(4, f.Store.ReadHead().Revision);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_operations WHERE operation_id='migration-v8-canonical-created-layout';"));
    }

    [Fact]
    public void V8_late_failure_rolls_back_repair_versions_and_operation_and_keeps_backup()
    {
        using var f = new Fixture(false); StartV7Layout(f); AddV7Layout(f);
        string before = Rows(f, "posters"), ops = Rows(f, "state_operations");
        f.Execute("""
            CREATE TRIGGER fail_v8 BEFORE INSERT ON schema_migrations WHEN NEW.version=8
            BEGIN SELECT RAISE(ABORT,'fixture late v8 failure'); END;
            """);
        Assert.False(f.Store.Initialize().Succeeded);
        Assert.Equal(before, Rows(f, "posters")); Assert.Equal(ops, Rows(f, "state_operations"));
        Assert.Equal(7L, f.Scalar("PRAGMA user_version;")); Assert.Equal(4L, f.Scalar("SELECT revision FROM state_head;"));
        Assert.Single(Directory.EnumerateFiles(f.Paths.Backups));
    }

    [Fact]
    public void V8_backup_failure_does_not_repair_any_record()
    {
        using var f = new Fixture(false); StartV7Layout(f); AddV7Layout(f);
        string before = Rows(f, "posters");
        Directory.Delete(f.Paths.Backups); File.WriteAllText(f.Paths.Backups, "fixture");
        Assert.Throws<IOException>(() => f.Store.Initialize());
        Assert.Equal(before, Rows(f, "posters")); Assert.Equal(7L, f.Scalar("PRAGMA user_version;"));
    }
}
