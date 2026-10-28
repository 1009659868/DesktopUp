using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class R7PortableFidelityTests
{
    internal static TemplateRecipe Recipe(string id = "my.desk", string color = "#123456")
    {
        var source = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "lamp");
        return TemplateRecipeCodec.Freeze(source with { Schema = source.Schema with { TemplateId = id, Name = "我的书桌" },
            Base = source.Base with { Background = color } });
    }
    internal static string Catalog(params TemplateRecipe[] recipes) => "[" + string.Join(",", recipes.Select(r => TemplateRecipeCodec.Serialize(r, stored: true))) + "]";
    internal static PosterDefinition Poster(params TemplateRecipe[] recipes)
    {
        var parameters = PosterPresentationPolicy.Defaults("basic-box") with { Preferences = new(1.13, .73),
            TitleColors = new("#123ABC", "#FEDCBA"), ShowTitle = false };
        foreach (var recipe in recipes)
        {
            parameters = TemplateRecipeInstancePolicy.Select(parameters, recipe);
            var values = TemplateRecipeInstancePolicy.CurrentValues(parameters, recipe.Id)
                .Set("surface.opacity", TemplateParameterValue.FromNumber(25))
                .Set("lamp.follow", TemplateParameterValue.FromBoolean(false));
            parameters = TemplateRecipeInstancePolicy.UpdateValues(parameters, recipe.Id, values);
        }
        return PosterConfigurationFactory.NewBasicBox("完整配置", "DISPLAY1") with { PosterId = "source", ThemeInstanceId = "source-theme",
            TemplateKey = recipes.FirstOrDefault()?.Id ?? "basic-box", LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) };
    }
    internal static DesktopStateSnapshot State(PosterDefinition[] posters, string catalog = "[]", string theme = "ink") => new(
        new(12, 4, true, posters.Select(p => new PosterStateSnapshot(12, 11, p, DateTimeOffset.UnixEpoch, 7)).ToArray()),
        new(12, 10, new(false, DesktopUpPerformanceMode.FramesPerSecond30, true) { CenterTheme = theme, TemplateCatalogJson = catalog }, DateTimeOffset.UnixEpoch, 5));
    internal static DisplayTopologySnapshot Topology() => new("topology", [new("DISPLAY1", true,
        new(0, 0, 3840, 2160), new(0, 0, 3840, 2160), 96, 96)], 1, DateTimeOffset.UnixEpoch);
    internal static PortableProfileDocument Document(DesktopStateSnapshot state) => PortableProfileCodec.Parse(PortableProfileCodec.Export(state));

    [Fact]
    public void Complete_v5_retains_hidden_title_and_selected_unselected_instances_and_unused_catalog()
    {
        var a = Recipe(); var b = Recipe("my.other"); var unused = Recipe("my.unused");
        var source = State([Poster(a, b)], Catalog(a, b, unused));
        var bytes = PortableProfileCodec.Export(source);
        var doc = PortableProfileCodec.Parse(bytes);
        Assert.Equal(5, doc.Version);
        var plan = PortableProfilePlanner.Plan(doc, State([], theme: "blue"), Topology(), PortableImportMode.Append, true, () => "new");
        Assert.Equal("ink", plan.Settings!.CenterTheme);
        Assert.Equal(3, TemplateRecipeCatalog.Read(plan.Settings.TemplateCatalogJson).Count);
        var imported = Assert.Single(plan.Creates);
        var presentation = PosterPresentationPolicy.Resolve(imported);
        Assert.True(presentation.CanEdit, presentation.Warning);
        Assert.False(presentation.Parameters.IsTitleVisible);
        Assert.Equal(new PosterTitleColors("#123ABC", "#FEDCBA"), presentation.Parameters.TitleColors);
        Assert.Equal(2, presentation.Parameters.RecipeInstances!.Count);
        Assert.All(presentation.Parameters.RecipeInstances.Values, i => {
            Assert.Equal(25, i.Values.Number("surface.opacity", -1)); Assert.False(i.Values.Boolean("lamp.follow", true)); });
        Assert.True(JsonElement.DeepEquals(doc.Posters[0].LayoutParameters, JsonSerializer.Deserialize<JsonElement>(imported.LayoutParametersJson)));
        Assert.Equal(bytes, PortableProfileCodec.Export(State([imported], plan.Settings.TemplateCatalogJson, plan.Settings.CenterTheme)));
    }

    [Theory] [InlineData(PortableImportMode.Append)] [InlineData(PortableImportMode.Replace)]
    public void Conflicting_template_keeps_local_definition_and_imports_copy_without_global_preferences(PortableImportMode mode)
    {
        var imported = Recipe(); var local = Recipe(color: "#FFFFFF");
        var original = Poster(local) with { PosterId = "local", ThemeInstanceId = "local-theme" };
        var current = State([original], Catalog(local), "blue");
        var doc = Document(State([Poster(imported)], Catalog(imported)));
        var plan = PortableProfilePlanner.Plan(doc, current, Topology(), mode, false, () => "new");
        Assert.Equal("blue", plan.Settings!.CenterTheme);
        var catalog = TemplateRecipeCatalog.Read(plan.Settings.TemplateCatalogJson);
        Assert.Equal(2, catalog.Count);
        Assert.Equal(TemplateRecipeCodec.Serialize(local), TemplateRecipeCodec.Serialize(catalog.Single(r => r.Id == local.Id)));
        var created = Assert.Single(plan.Creates);
        Assert.NotEqual(imported.Id, created.TemplateKey);
        Assert.Equal(created.TemplateKey, PosterPresentationPolicy.Resolve(created).SelectedRecipeId);
        Assert.Equal(imported.Base, catalog.Single(r => r.Id == created.TemplateKey).Base);
        Assert.Equal(original, current.Catalog.Posters[0].Poster);
        Assert.Contains(plan.Warnings, w => w.Contains("副本"));
        var repeated = PortableProfilePlanner.Plan(doc, current, Topology(), mode, false, () => "new");
        Assert.Equal(plan.Settings, repeated.Settings); Assert.Equal(plan.Creates, repeated.Creates);
    }

    [Fact]
    public void Embedded_definition_wins_over_different_source_catalog_without_losing_either()
    {
        var embedded = Recipe(); var catalog = Recipe(color: "#FFFFFF");
        var doc = Document(State([Poster(embedded)], Catalog(catalog)));
        var plan = PortableProfilePlanner.Plan(doc, State([]), Topology(), PortableImportMode.Append, false, () => "new");
        var created = Assert.Single(plan.Creates); var resolved = PosterPresentationPolicy.Resolve(created);
        Assert.Equal(embedded.Base, resolved.Parameters.RecipeInstances![created.TemplateKey].Recipe.Base);
        Assert.Equal(2, TemplateRecipeCatalog.Read(plan.Settings!.TemplateCatalogJson).Count);
    }

    [Fact]
    public void Zero_posters_still_round_trips_unused_template_catalog_and_theme()
    {
        var source = State([], Catalog(Recipe()));
        var plan = PortableProfilePlanner.Plan(Document(source), State([], theme: "blue"), Topology(), PortableImportMode.Replace, true);
        Assert.Empty(plan.Creates); Assert.Equal("ink", plan.Settings!.CenterTheme);
        Assert.Single(TemplateRecipeCatalog.Read(plan.Settings.TemplateCatalogJson));
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Old_versions_keep_local_theme_and_catalog_but_reject_new_fields_even_when_null(int version)
    {
        var source = State([PosterConfigurationFactory.NewBasicBox("旧包", "DISPLAY1")], theme: "light");
        var doc = Document(source) with { Version = version, TemplateCatalog = null,
            Settings = new(true, DesktopUpPerformanceMode.Static), Posters = Document(source).Posters.Select(p => p with { ContentLayout = null }).ToArray() };
        var old = PortableProfileCodec.Freeze(doc);
        var current = State([], Catalog(Recipe()), "blue");
        var plan = PortableProfilePlanner.Plan(old, current, Topology(), PortableImportMode.Replace, true, () => "old");
        Assert.Equal("blue", plan.Settings!.CenterTheme); Assert.Equal(current.Settings.Settings.TemplateCatalogJson, plan.Settings.TemplateCatalogJson);
        Assert.True(plan.Settings.DynamicEffectsPaused); Assert.True(plan.Settings.LaunchAtStartupRequested);
        var tree = JsonNode.Parse(PortableProfileCodec.Encode(doc))!;
        tree["settings"]!["centerTheme"] = null;
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Theory] [InlineData("theme-missing")] [InlineData("theme-null")] [InlineData("theme-invalid")]
    [InlineData("catalog-missing")] [InlineData("catalog-null")] [InlineData("duplicate-id")]
    [InlineData("script")] [InlineData("bad-renderer")] [InlineData("missing-value")]
    [InlineData("unknown-value")] [InlineData("future-presentation")] [InlineData("future-recipe")]
    public void New_format_is_strict_and_never_discards_unsupported_fields(string fault)
    {
        var r = Recipe(); var tree = JsonNode.Parse(PortableProfileCodec.Export(State([Poster(r)], Catalog(r))))!;
        var presentation = tree["posters"]![0]!["layoutParameters"]!;
        switch (fault)
        {
            case "theme-missing": tree["settings"]!.AsObject().Remove("centerTheme"); break;
            case "theme-null": tree["settings"]!["centerTheme"] = null; break;
            case "theme-invalid": tree["settings"]!["centerTheme"] = "future"; break;
            case "catalog-missing": tree.AsObject().Remove("templateCatalog"); break;
            case "catalog-null": tree["templateCatalog"] = null; break;
            case "duplicate-id": tree["templateCatalog"]!.AsArray().Add(tree["templateCatalog"]![0]!.DeepClone()); break;
            case "script": tree["templateCatalog"]![0]!["script"] = "run.exe"; break;
            case "bad-renderer": tree["templateCatalog"]![0]!["renderer"] = "C:/secret.dll"; break;
            case "missing-value": presentation["recipes"]![0]!["values"]!.AsObject().Remove("lightColor"); break;
            case "unknown-value": presentation["recipes"]![0]!["values"]!["future"] = 3; break;
            case "future-presentation": presentation["schemaVersion"] = 99; break;
            case "future-recipe": tree["templateCatalog"]![0]!["schemaVersion"] = 99; break;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(tree.ToJsonString());
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(bytes));
    }

    [Fact]
    public void Reimport_identical_or_copied_declarations_reuses_catalog_entries()
    {
        var r = Recipe(); var doc = Document(State([Poster(r)], Catalog(r)));
        var current = State([], Catalog(Recipe(color: "#FFFFFF")));
        var first = PortableProfilePlanner.Plan(doc, current, Topology(), PortableImportMode.Append, false, () => "first");
        var second = PortableProfilePlanner.Plan(doc, State(first.Creates.ToArray(), first.Settings!.TemplateCatalogJson), Topology(),
            PortableImportMode.Append, false, () => "second");
        Assert.Null(second.Settings);
        Assert.Equal(first.Creates[0].TemplateKey, second.Creates[0].TemplateKey);
    }

    [Fact]
    public void Catalog_overflow_rejects_before_allocating_poster_identities()
    {
        var full = Enumerable.Range(0, TemplateRecipeCatalog.MaximumTemplates - TemplateRecipeCatalog.BuiltIns.Count)
            .Select(i => Recipe("local." + i)).ToArray();
        var current = State([], Catalog(full)); int ids = 0;
        Assert.Throws<ArgumentException>(() => PortableProfilePlanner.Plan(Document(State([Poster(Recipe())], Catalog(Recipe()))),
            current, Topology(), PortableImportMode.Append, false, () => { ids++; return "new"; }));
        Assert.Equal(0, ids); Assert.Equal(Catalog(full), current.Settings.Settings.TemplateCatalogJson);
    }

    [Fact]
    public void Missing_renderer_definition_and_values_are_retained_as_read_only_without_execution()
    {
        var recipe = Recipe();
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State([Poster(recipe)], Catalog(recipe))))!;
        tree["templateCatalog"]![0]!["renderer"] = "uninstalled.weather@2";
        tree["posters"]![0]!["layoutParameters"]!["recipes"]![0]!["definition"]!["renderer"] = "uninstalled.weather@2";
        var document = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var plan = PortableProfilePlanner.Plan(document, State([]), Topology(), PortableImportMode.Append, false, () => "missing");
        var created = plan.Creates.Single();
        Assert.False(PosterPresentationPolicy.Resolve(created).CanEdit);
        Assert.True(JsonElement.DeepEquals(document.Posters[0].LayoutParameters, JsonSerializer.Deserialize<JsonElement>(created.LayoutParametersJson)));
        Assert.False(SceneCapabilityRegistry.Available("uninstalled.weather@2"));
        Assert.Single(TemplateRecipeCatalog.Read(plan.Settings!.TemplateCatalogJson, stored: true));
        Assert.Contains(plan.Warnings, w => w.Contains("只读"));
        var exported = Document(State([created], plan.Settings.TemplateCatalogJson));
        Assert.True(JsonElement.DeepEquals(document.Posters[0].LayoutParameters, exported.Posters[0].LayoutParameters));
    }

    [Fact]
    public void All_installed_scene_parameters_and_legacy_lamp_round_trip_with_false_and_fractional_values()
    {
        foreach (var scene in SceneCapabilityRegistry.Templates)
        {
            // Built-in lamp uses its legacy adapter; other modules use declarative instances.
            var p = PosterPresentationPolicy.Defaults("night-ops");
            p = TemplateRecipeInstancePolicy.Select(p, scene);
            var values = TemplateRecipeInstancePolicy.CurrentValues(p, scene.Id);
            foreach (var property in scene.Schema.Properties)
                values = values.Set(property.Binding, property.Kind switch {
                    TemplateParameterKind.Boolean => TemplateParameterValue.FromBoolean(false),
                    TemplateParameterKind.Color => TemplateParameterValue.FromColor("#123456"),
                    _ => TemplateParameterValue.FromNumber(property.Minimum!.Value + (property.Maximum!.Value - property.Minimum.Value) * .37) });
            p = TemplateRecipeInstancePolicy.UpdateValues(p, scene.Id, values) with { ShowTitle = false };
            var source = Poster() with { TemplateKey = scene.Id, LayoutParametersJson = PosterPresentationPolicy.Serialize(p) };
            var document = Document(State([source]));
            var plan = PortableProfilePlanner.Plan(document, State([]), Topology(), PortableImportMode.Append, false, () => "scene");
            Assert.Null(plan.Settings);
            Assert.Equal(scene.Id, plan.Creates[0].TemplateKey);
            Assert.True(JsonElement.DeepEquals(document.Posters[0].LayoutParameters, JsonSerializer.Deserialize<JsonElement>(plan.Creates[0].LayoutParametersJson)));
            Assert.Equal(values, TemplateRecipeInstancePolicy.CurrentValues(PosterPresentationPolicy.Resolve(plan.Creates[0]).Parameters, scene.Id));
        }
    }
}
