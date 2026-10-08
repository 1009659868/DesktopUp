using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.R7PortableFidelityTests;

namespace DesktopUp.Contracts.Tests;

public sealed class R7PortableTransactionTests
{
    [Theory] [InlineData(PortableImportMode.Append)] [InlineData(PortableImportMode.Replace)]
    public void Complete_import_is_one_commit_replays_and_remains_editable_after_restart(PortableImportMode mode)
    {
        using var f = new Fixture(); var before = f.Repository.Read();
        var plan = f.Plan(mode); int notices = 0;
        f.Coordinator.ProjectionChanged += (_, _) => notices++;
        var command = new PortableImportCommand("full-import", plan);
        var result = f.Coordinator.ExecutePortable(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CommittedRevision); Assert.Equal(1, notices);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.ExecutePortable(command).Status);
        Assert.Equal(2, TemplateRecipeCatalog.Read(f.Repository.Read().Settings.Settings.TemplateCatalogJson).Count);
        if (mode == PortableImportMode.Append) Assert.Equal(before.Catalog.Posters[0].Poster, f.Repository.Read().Catalog.Posters[0].Poster);
        f.Reopen();
        var current = f.Coordinator.Refresh(); var imported = current.PostersById[plan.Creates[0].PosterId];
        Assert.False(imported.Presentation.Parameters.IsTitleVisible);
        Assert.Equal(25, imported.Presentation.TemplateValues.Number("surface.opacity", -1));
        var edit = f.Coordinator.Execute(new PosterSettingEdit(imported, PosterSetting.Spacing, Number: .89).Command("after-restart"));
        Assert.True(edit.Succeeded, edit.Error);
        imported = edit.Projection!.PostersById[imported.Poster.PosterId];
        Assert.False(imported.Presentation.Parameters.IsTitleVisible);
        Assert.Equal(25, imported.Presentation.TemplateValues.Number("surface.opacity", -1));
        Assert.Equal(.89, imported.Presentation.Parameters.Preferences!.Spacing);
        var again = Document(f.Repository.Read());
        Assert.Equal(5, again.Version); Assert.Equal(2, again.TemplateCatalog!.Value.GetArrayLength());
    }

    [Theory] [InlineData("settings")] [InlineData("poster")] [InlineData("receipt")]
    public void Catalog_and_instances_roll_back_together_on_sql_failure(string fault)
    {
        using var f = new Fixture(); var before = f.Repository.Read(); var plan = f.Plan();
        string condition = fault switch { "settings" => "BEFORE UPDATE ON app_settings",
            "poster" => "BEFORE INSERT ON posters", _ => "BEFORE INSERT ON state_command_receipts" };
        f.Sql("CREATE TRIGGER fail_import " + condition + " BEGIN SELECT RAISE(ABORT,'R7 fault'); END;");
        var result = f.Coordinator.ExecutePortable(new("failure", plan));
        Assert.Equal(StateCommandStatus.Failed, result.Status);
        Assert.Equal(StateRequestFingerprint.Compute(before), StateRequestFingerprint.Compute(f.Repository.Read()));
        f.Sql("DROP TRIGGER fail_import;");
        Assert.True(f.Coordinator.ExecutePortable(new("failure", plan)).Succeeded); // failed receipt not consumed
    }

    [Theory] [InlineData("catalog")] [InlineData("instance")] [InlineData("selected")]
    public void Tampered_mapping_or_payload_is_rejected_without_any_write(string fault)
    {
        using var f = new Fixture(); var before = f.Repository.Read(); var plan = f.Plan();
        plan = fault switch {
            "catalog" => plan with { Settings = plan.Settings! with { TemplateCatalogJson = before.Settings.Settings.TemplateCatalogJson } },
            "selected" => plan with { Creates = [plan.Creates[0] with { TemplateKey = "my.desk" }] },
            _ => plan with { Creates = [plan.Creates[0] with { LayoutParametersJson = before.Catalog.Posters[0].Poster.LayoutParametersJson }] } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.ExecutePortable(new("tamper", plan)).Status);
        Assert.Equal(StateRequestFingerprint.Compute(before), StateRequestFingerprint.Compute(f.Repository.Read()));
    }

    [Fact]
    public void Preview_is_side_effect_free_and_catalog_change_stales_it_even_with_global_import_off()
    {
        using var f = new Fixture(); var before = f.Repository.Read(); var plan = f.Plan();
        Assert.Equal(StateRequestFingerprint.Compute(before), StateRequestFingerprint.Compute(f.Repository.Read()));
        var registered = f.Coordinator.Execute(new RegisterTemplateCommand("intervening", StateCommandSource.ControlCenter,
            before.Revision, StateReadSet.ForSettings(before.Settings.EntityVersion), TemplateRecipeCodec.Serialize(Recipe("other.template"))));
        Assert.True(registered.Succeeded, registered.Error);
        var changed = f.Repository.Read();
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.ExecutePortable(new("stale", plan)).Status);
        Assert.Equal(StateRequestFingerprint.Compute(changed), StateRequestFingerprint.Compute(f.Repository.Read()));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.R7Portable.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; private set; }
        public SqliteStateCommandRepository Repository { get; private set; }
        public StateProjectionCoordinator Coordinator { get; private set; }
        public Fixture()
        {
            Paths = new(_root); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Repository = new(Store); Coordinator = new(Repository);
            Coordinator.EnsureInitialized(Poster(Recipe(color: "#FFFFFF")));
            var settings = Repository.Read().Settings;
            Assert.True(new SqliteSettingsRepository(Store).Save(settings.Revision, "local-catalog",
                settings.Settings with { TemplateCatalogJson = Catalog(Recipe(color: "#FFFFFF")), CenterTheme = "blue" }).Succeeded);
            Coordinator.Refresh(); Coordinator.BindPortableRuntime(_ => true);
        }
        public PortableImportPlan Plan(PortableImportMode mode = PortableImportMode.Append) =>
            PortableProfilePlanner.Plan(Document(State([Poster(Recipe())], Catalog(Recipe()))), Repository.Read(), Topology(), mode, false, () => "imported");
        public void Reopen()
        {
            Store.Dispose(); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Repository = new(Store); Coordinator = new(Repository); Coordinator.BindPortableRuntime(_ => true);
        }
        public void Sql(string sql)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = Path.Combine(Paths.State, "desktopup.sqlite3"), Pooling = false }.ToString());
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
        }
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
