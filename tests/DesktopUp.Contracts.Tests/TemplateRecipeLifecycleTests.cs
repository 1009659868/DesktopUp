using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class TemplateRecipeLifecycleTests
{
    private static TemplateRecipe Recipe(string id = "my.desk")
    {
        var lamp = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "lamp");
        return TemplateRecipeCodec.Freeze(lamp with { Schema = lamp.Schema with { TemplateId = id, Name = "我的台灯",
            Properties = lamp.Schema.Properties.Select((p, i) => p with { Key = "field" + i, Label = "参数 " + i }).ToArray() } });
    }

    [Fact]
    public void RecipeControlsRoundTripThroughKnownBindingsAndCannotMutateDefinition()
    {
        var recipe = Recipe(); string json = TemplateRecipeCodec.Serialize(recipe);
        var parsed = TemplateRecipeCodec.Parse(json);
        Assert.Equal(json, TemplateRecipeCodec.Serialize(parsed));
        Assert.Contains(parsed.Schema.Properties, p => p.Kind == TemplateParameterKind.Boolean && p.Binding == "lamp.follow");
        var defaults = TemplateRecipeInstancePolicy.Defaults(parsed);
        var values = TemplateParameterBindingAdapter.Read(parsed.Schema, defaults).ToDictionary(p => p.Key, p => p.Value);
        string follow = parsed.Schema.Properties.Single(p => p.Binding == "lamp.follow").Key;
        values[follow] = TemplateParameterValue.FromBoolean(false);
        var modified = TemplateParameterBindingAdapter.ComposeDraft(parsed.Schema, values, defaults);
        Assert.False(modified.FollowPointer); Assert.True(defaults.FollowPointer);
        var copy = TemplateRecipeCopy.Create(parsed, "my.copy", "副本", values);
        Assert.False(TemplateRecipeInstancePolicy.Defaults(copy).FollowPointer);
        Assert.True(TemplateRecipeInstancePolicy.Defaults(parsed).FollowPointer);
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateParameterDefinition>)parsed.Schema.Properties).Clear());
    }

    [Theory]
    [InlineData("unknown-root")] [InlineData("unknown-binding")] [InlineData("duplicate-key")]
    [InlineData("duplicate-binding")] [InlineData("unknown-renderer")] [InlineData("bad-default")]
    [InlineData("missing-base")] [InlineData("missing-default")] [InlineData("missing-properties")]
    [InlineData("version-text")] [InlineData("version-null")] [InlineData("version-boolean")]
    [InlineData("wrong-type")] [InlineData("duplicate-json")]
    [InlineData("oversized")]
    public void InvalidRecipesNeverMutateTheRegistryOrPoster(string fault)
    {
        using var f = new Fixture(); var before = f.Current;
        var root = JsonNode.Parse(TemplateRecipeCodec.Serialize(Recipe()))!.AsObject();
        var properties = root["properties"]!.AsArray();
        switch (fault)
        {
            case "unknown-root": root["script"] = "anything"; break;
            case "unknown-binding": properties[0]!["bind"] = "shell.execute"; break;
            case "duplicate-key": properties[1]!["key"] = properties[0]!["key"]!.GetValue<string>(); break;
            case "duplicate-binding": properties[1]!["bind"] = properties[0]!["bind"]!.GetValue<string>(); break;
            case "unknown-renderer": root["renderer"] = "unknown@1"; break;
            case "bad-default": properties[1]!["default"] = 9000; break;
            case "missing-base": root.Remove("base"); break;
            case "missing-default": properties[0]!.AsObject().Remove("default"); break;
            case "missing-properties": root.Remove("properties"); break;
            case "version-text": root["schemaVersion"] = "x"; break;
            case "version-null": root["schemaVersion"] = null; break;
            case "version-boolean": root["schemaVersion"] = false; break;
            case "wrong-type": properties[6]!["default"] = "true"; break;
        }
        string source = root.ToJsonString();
        if (fault == "duplicate-json") source = source.Replace("\"version\":1", "\"version\":1,\"version\":1");
        if (fault == "oversized") source = new string(' ', TemplateRecipeCodec.MaximumBytes + 1);
        var result = f.Coordinator.Execute(f.Import(source, "A"));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(before.Revision, f.Current.Revision);
        Assert.Equal("[]", f.Settings.TemplateCatalogJson);
        Assert.Equal(before.PostersById["A"].Poster, f.Current.PostersById["A"].Poster);
    }

    [Fact]
    public void ImportAndUseIsOneCasTransactionAndReplayDuplicateAndStaleCandidatesCannotPartiallyWrite()
    {
        using var f = new Fixture(); var recipe = Recipe(); var before = f.Current;
        var command = f.Import(TemplateRecipeCodec.Serialize(recipe), "A");
        var committed = f.Coordinator.Execute(command);
        Assert.True(committed.Succeeded, committed.Error); Assert.Equal(before.Revision + 1, committed.CurrentRevision);
        Assert.Equal(recipe.Id, f.Current.PostersById["A"].Poster.TemplateKey);
        Assert.Equal("lamp", f.Current.PostersById["A"].Presentation.EffectiveTemplateKey);
        Assert.Single(TemplateRecipeCatalog.Read(f.Settings.TemplateCatalogJson));
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        var duplicate = f.Coordinator.Execute(f.Import(command.SourceJson, "A"));
        Assert.Equal(StateCommandStatus.ValidationFailed, duplicate.Status); Assert.Equal(committed.CurrentRevision, f.Current.Revision);
        var stale = command with { RequestId = Guid.NewGuid().ToString("N"), SourceJson = TemplateRecipeCodec.Serialize(Recipe("another.desk")) };
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(stale).Status);
        Assert.Single(TemplateRecipeCatalog.Read(f.Settings.TemplateCatalogJson));
    }

    [Fact]
    public void RegisterCopyDoesNotSelectAndDifferentPostersAndTemplatesRetainValuesAcrossReopen()
    {
        using var f = new Fixture(); f.AddPoster();
        var first = Recipe(); var second = Recipe("my.second");
        Assert.True(f.Coordinator.Execute(f.Import(TemplateRecipeCodec.Serialize(first), "A")).Succeeded);
        var unchanged = f.Current.PostersById["A"].Poster;
        Assert.True(f.Coordinator.Execute(f.Import(TemplateRecipeCodec.Serialize(second), null)).Succeeded);
        Assert.Equal(unchanged, f.Current.PostersById["A"].Poster);
        var values = f.Current.PostersById["A"].Presentation.TemplateParameters with { Intensity = 13, MotionEnabled = false, FollowPointer = false, IdleSway = 7 };
        f.Edit("A", PosterSetting.TemplateParameters, lamp: values);
        f.Edit("A", PosterSetting.Template, second.Id, second);
        f.Edit("A", PosterSetting.TemplateParameters, lamp: TemplateRecipeInstancePolicy.Defaults(second) with { Intensity = 88 });
        f.Edit("B", PosterSetting.Template, first.Id, first);
        Assert.Equal(70, f.Current.PostersById[f.SecondId].Presentation.TemplateParameters.Intensity);
        f.Edit("A", PosterSetting.Template, "lamp");
        f.Edit("A", PosterSetting.Lamp, lamp: LampTemplatePolicy.Defaults with { Intensity = 41 });
        f.Edit("A", PosterSetting.Template, first.Id, first);
        Assert.Equal(values, f.Current.PostersById["A"].Presentation.TemplateParameters);
        Assert.Equal(41, f.Current.PostersById["A"].Presentation.Parameters.Lamp!.Intensity);
        f.Reopen();
        Assert.Equal(values, f.Current.PostersById["A"].Presentation.TemplateParameters);
        Assert.Equal(70, f.Current.PostersById[f.SecondId].Presentation.TemplateParameters.Intensity);
        Assert.Equal(2, TemplateRecipeCatalog.Read(f.Settings.TemplateCatalogJson).Count);
        f.Edit("A", PosterSetting.Template, second.Id, second);
        Assert.Equal(88, f.Current.PostersById["A"].Presentation.TemplateParameters.Intensity);
    }

    [Fact]
    public void NarrowRecipeRejectsOutOfRangeDraftAndForgedUnregisteredSelection()
    {
        using var f = new Fixture(); var recipe = Recipe();
        recipe = recipe with { Schema = recipe.Schema with { Properties = recipe.Schema.Properties.Select(p =>
            p.Binding == "lamp.intensity" ? p with { Maximum = 50, DefaultValue = TemplateParameterValue.FromNumber(30) } : p).ToArray() } };
        Assert.True(f.Coordinator.Execute(f.Import(TemplateRecipeCodec.Serialize(recipe), "A")).Succeeded);
        var target = f.Current.PostersById["A"]; var session = new ControlCenterSettingsSession();
        Assert.False(session.Update(new(target, PosterSetting.TemplateParameters, Lamp: target.Presentation.TemplateParameters with { Intensity = 80 })));
        session.Finish(); Assert.Null(session.TakeNext());
        var unregistered = Recipe("missing.template");
        var result = f.Coordinator.Execute(new PosterSettingEdit(target, PosterSetting.Template, Text: unregistered.Id, Recipe: unregistered).Command("forged"));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(recipe.Id, f.Current.PostersById["A"].Poster.TemplateKey);
    }

    [Fact]
    public void CenterThemeIsIndependentPersistedAndOrdinarySettingsCannotReplaceCatalog()
    {
        using var f = new Fixture(); Assert.True(f.Coordinator.Execute(f.Import(TemplateRecipeCodec.Serialize(Recipe()), "A")).Succeeded);
        var poster = f.Current.PostersById["A"].Poster;
        var settings = f.Current.ControlCenter.Settings;
        var good = new SettingsStateCommand("theme", StateCommandSource.ControlCenter, SettingsStateCommandKind.SetCenterTheme,
            f.Current.Revision, f.Settings with { CenterTheme = "ink" }) { ReadSet = StateReadSet.ForSettings(settings.EntityVersion) };
        Assert.True(f.Coordinator.Execute(good).Succeeded); Assert.Equal(poster, f.Current.PostersById["A"].Poster);
        var bad = good with { RequestId = "tampered", ExpectedRevision = f.Current.Revision,
            DesiredState = f.Settings with { CenterTheme = "blue", TemplateCatalogJson = "[]" },
            ReadSet = StateReadSet.ForSettings(f.Current.ControlCenter.Settings.EntityVersion) };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(bad).Status);
        f.Reopen(); Assert.Equal("ink", f.Settings.CenterTheme); Assert.Single(TemplateRecipeCatalog.Read(f.Settings.TemplateCatalogJson));
        var exported = PortableProfileCodec.Parse(PortableProfileCodec.Export(new SqliteStateCommandRepository(f.Store).Read()));
        Assert.Equal("ink", exported.Settings.CenterTheme);
        Assert.Equal(1, exported.TemplateCatalog!.Value.GetArrayLength());
    }

    [Fact]
    public void LegacyLampVersionThreeKeepsExplicitPaintAndGainsDefaultMotionWithoutAReadWrite()
    {
        const string json = """{"schemaVersion":3,"templates":{"lamp":{"lightColor":"#123456","softness":23,"intensity":44,"background":"#112233","card":"#223344","backgroundOpacity":77}}} """;
        var poster = Fixture.Seed("A") with { LayoutParametersJson = json, TemplateKey = "lamp" };
        var resolved = PosterPresentationPolicy.Resolve(poster);
        Assert.True(resolved.CanEdit, resolved.Warning); Assert.Equal(44, resolved.TemplateParameters.Intensity);
        Assert.True(resolved.TemplateParameters.FollowPointer); Assert.Equal(35, resolved.TemplateParameters.IdleSway);
        Assert.Equal(json, poster.LayoutParametersJson);
    }

    [Fact]
    public void MalformedStoredRecipeIsReadOnlyAndCannotBeSilentlyOverwritten()
    {
        var parameters = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults(), Recipe());
        var valid = PosterPresentationPolicy.Serialize(parameters);
        foreach (string missing in new[] { "definition", "values" })
        {
            var root = JsonNode.Parse(valid)!.AsObject(); root["recipes"]![0]!.AsObject().Remove(missing);
            string raw = root.ToJsonString();
            var resolved = PosterPresentationPolicy.Resolve(Fixture.Seed("A") with { TemplateKey = "my.desk", LayoutParametersJson = raw });
            Assert.True(resolved.IsFallback);
            Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), raw));
        }
    }

    [Fact]
    public void SwitchingToMinimalDoesNotBorrowLastLampMaterialOrMotionPreferences()
    {
        var parameters = PosterPresentationPolicy.Defaults() with { Lamp = new("#123456", Background: "#112233", Card: "#445566", MotionEnabled: false) };
        var current = TemplateRecipeInstancePolicy.Current(parameters, "night-ops");
        Assert.Equal("#202E2A", current.Background); Assert.Equal("#2D433A", current.Card);
        Assert.True(current.MotionEnabled);
    }

    [Fact]
    public void V11MigrationAddsIndependentDefaultsAndKeepsRevisionPosterAndOriginalBackup()
    {
        using var f = new Fixture(); var before = f.Current;
        using (var connection = new SqliteConnection($"Data Source={System.IO.Path.Combine(f.Path, "state", "desktopup.sqlite3")};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = StorageSchemaFixture.Drop19+"DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DROP TABLE content_transfer_items; DROP TABLE content_transfers; DROP TABLE managed_storage_collections; DROP TABLE managed_storage_roots; DROP TABLE content_icon_assets; DROP TABLE content_open_facts; DROP TABLE content_open_requests; DROP TABLE content_demo_archive; DROP TABLE content_poster_items; DROP TABLE content_poster_bindings; DROP TABLE content_memberships; DROP TABLE content_collections; DROP TABLE content_items; DROP TABLE content_head; ALTER TABLE app_settings DROP COLUMN center_theme; ALTER TABLE app_settings DROP COLUMN template_catalog_json; DELETE FROM schema_migrations WHERE version>=12; UPDATE state_head SET schema_version=11; PRAGMA user_version=11;";
            command.ExecuteNonQuery();
        }
        using var upgraded = new SqliteStateStore(new UserDataPaths(f.Path)); var result = upgraded.Initialize();
        Assert.True(result.Succeeded, result.Error); Assert.Equal(11, result.PreviousSchemaVersion); Assert.Equal(25, result.Head!.SchemaVersion);
        Assert.Equal(before.Revision, result.Head.Revision);
        var snapshot = new SqliteStateCommandRepository(upgraded).Read();
        Assert.Equal(before.PostersById["A"].Poster, snapshot.Catalog.Posters.Single().Poster);
        Assert.Equal("light", snapshot.Settings.Settings.CenterTheme); Assert.Equal("[]", snapshot.Settings.Settings.TemplateCatalogJson);
        string backup = System.IO.Path.Combine(new UserDataPaths(f.Path).Backups, result.PreMigrationBackupFileName!);
        Assert.Equal(11, upgraded.ValidateBackup(backup).SchemaVersion);
    }

    [Fact]
    public void DefaultSettingsKeepHistoricalReceiptHashAndThemeOrCatalogChangesParticipate()
    {
        var settings = new DesktopUpSettings(true, DesktopUpPerformanceMode.Static, false);
        var oldWire = new { Settings = new { settings.DynamicEffectsPaused, settings.PerformanceMode, settings.LaunchAtStartupRequested } };
        string oldHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(oldWire)));
        Assert.Equal(oldHash, StateRequestFingerprint.Compute(new { Settings = settings }));
        Assert.NotEqual(oldHash, StateRequestFingerprint.Compute(new { Settings = settings with { CenterTheme = "ink" } }));
        Assert.NotEqual(oldHash, StateRequestFingerprint.Compute(new { Settings = settings with
            { TemplateCatalogJson = TemplateRecipeCatalog.Add("[]", Recipe()) } }));
    }

    [Fact]
    public void CopyRetainsExplicitBaseMaterialWithoutInventingUndeclaredParameters()
    {
        var source = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "night-ops") with { Base = new("#112233", "#445566", true) };
        var copy = TemplateRecipeCopy.Create(source, "my.night", "我的夜航", TemplateParameterSchemas.Defaults(source.Schema));
        Assert.Equal(source.Base, copy.Base);
        Assert.Equal("#112233", TemplateRecipeInstancePolicy.Defaults(copy).Background);
        Assert.Equal("#445566", TemplateRecipeInstancePolicy.Defaults(copy).Card);
        Assert.Empty(copy.Schema.Properties);
    }

    private sealed class Fixture : IDisposable
    {
        public static PosterDefinition Seed(string id) => new(id, id, "Items", "display", 0, 600, 300, 600, 0, .6, .3, .6,
            false, true, false, "adaptive-grid", "{}", "lamp");
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopUp.Recipe.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; private set; }
        public string SecondId { get; private set; } = "";
        public StateProjectionCoordinator Coordinator { get; private set; }
        public DesktopUpProjection Current => Coordinator.Refresh();
        public DesktopUpSettings Settings => Current.ControlCenter.Settings.Settings;
        public Fixture()
        {
            Store = new(new UserDataPaths(Path)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store)); Coordinator.EnsureInitialized(Seed("A"));
        }
        public RegisterTemplateCommand Import(string source, string? id)
        {
            var current = Current;
            var reads = id is null ? StateReadSet.ForSettings(current.ControlCenter.Settings.EntityVersion) :
                StateReadSet.ForPoster(id, current.PostersById[id].EntityVersion) with { SettingsVersion = current.ControlCenter.Settings.EntityVersion };
            return new(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, current.Revision, reads, source, id);
        }
        public void Edit(string id, PosterSetting setting, string text = "", TemplateRecipe? recipe = null, LampTemplateParameters? lamp = null)
        {
            if (id == "B") id = SecondId;
            var result = Coordinator.Execute(new PosterSettingEdit(Current.PostersById[id], setting, Text: text, Recipe: recipe, Lamp: lamp)
                .Command(Guid.NewGuid().ToString("N"))); Assert.True(result.Succeeded, result.Error);
        }
        public void AddPoster()
        {
            var current = Current;
            var result = Coordinator.Execute(new CreatePosterCommand("add", StateCommandSource.ControlCenter, current.Revision,
                StateReadSet.ForCatalog(current.CatalogVersion), Seed("B")));
            Assert.True(result.Succeeded, result.Error);
            SecondId = result.ResultPosterId!;
        }
        public void Reopen()
        {
            Store.Dispose(); Store = new(new UserDataPaths(Path)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store)); Coordinator.Refresh();
        }
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(Path, true); }
    }
}
