using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class TemplateHeadingComponentTests
{
    private static TemplateRecipe Recipe(params TemplateHeadingComponent[] components)
    {
        var basic = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "basic-box");
        return TemplateRecipeCodec.Freeze(basic with
        {
            Schema = basic.Schema with { TemplateId = "component-test", SchemaVersion = 2 },
            Base = basic.Base with { Heading = components.Length > 0 }, Components = components
        });
    }

    [Fact]
    public void Legacy_bytes_and_complete_heading_geometry_are_unchanged()
    {
        foreach (var recipe in TemplateRecipeCatalog.BuiltIns)
        {
            string before = TemplateRecipeCodec.Serialize(recipe);
            Assert.Contains("\"schemaVersion\": 1", before); Assert.DoesNotContain("\"components\"", before);
            Assert.Equal(before, TemplateRecipeCodec.Serialize(TemplateRecipeCodec.Parse(before)));
        }
        foreach (double fontSize in new[] { 18d, 24d, 36d, 48d })
            Assert.Equal(Math.Ceiling(81 + fontSize * 1.35), TemplateHeadingComposition.Height(TemplateHeadingComposition.Legacy, fontSize));
    }

    [Theory]
    [InlineData("")] [InlineData("title")] [InlineData("count")]
    [InlineData("subtitle")] [InlineData("count,title")] [InlineData("title,subtitle,count")]
    public void Components_roundtrip_in_order_and_only_mounted_rows_reserve_space(string names)
    {
        var components = names.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => Enum.Parse<TemplateHeadingComponent>(n, true)).ToArray();
        var recipe = Recipe(components);
        string json = TemplateRecipeCodec.Serialize(recipe);
        var read = TemplateRecipeCodec.Parse(json);
        Assert.Equal(components, read.HeadingComponents);
        Assert.Equal(json, TemplateRecipeCodec.Serialize(read));
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateHeadingComponent>)read.Components!).Clear());
        var parameters = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults("basic-box"), recipe);
        var poster = PosterConfigurationFactory.NewBasicBox("heading", "DISPLAY1") with
        { TemplateKey = recipe.Id, LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) };
        var presentation = PosterPresentationPolicy.Resolve(poster);
        Assert.True(presentation.CanEdit, presentation.Warning);
        Assert.Equal(components, presentation.HeadingComponents);
        Assert.Equal(components.Length > 0, presentation.IsContentHeadingVisible);
        Assert.Equal(TemplateHeadingComposition.Height(components, parameters.Content.TitleFontSizeDip), presentation.MetaHeightDip);
        var hidden = parameters with { HeadingVisibility = new TemplateHeadingVisibility([]).Set(recipe.Id, false) };
        Assert.Equal(0, PosterPresentationPolicy.Resolve(poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(hidden) }).MetaHeightDip);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("unknown")] [InlineData("missing")]
    [InlineData("type")] [InlineData("inconsistent")] [InlineData("renderer")]
    [InlineData("legacy")] [InlineData("future")]
    public void Invalid_composition_cannot_write_catalog_or_poster(string fault)
    {
        using var f = new Fixture();
        var root = JsonNode.Parse(TemplateRecipeCodec.Serialize(Recipe(TemplateHeadingComponent.Title)))!.AsObject();
        switch (fault)
        {
            case "duplicate": root["components"]!.AsArray().Add("title"); break;
            case "unknown": root["components"]!.AsArray().Add("script"); break;
            case "missing": root.Remove("components"); break;
            case "type": root["components"] = true; break;
            case "inconsistent": root["base"]!["heading"] = false; break;
            case "renderer": root["renderer"] = "lamp@1"; break;
            case "legacy": root["schemaVersion"] = 1; break;
            case "future": root["schemaVersion"] = 3; break;
        }
        var before = f.Current;
        var result = f.Coordinator.Execute(new RegisterTemplateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            before.Revision, StateReadSet.ForPoster(f.Main, before.PostersById[f.Main].EntityVersion) with
            { SettingsVersion = before.ControlCenter.Settings.EntityVersion }, root.ToJsonString(), f.Main));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(before.Revision, f.Current.Revision);
        Assert.Equal(before.PostersById[f.Main].Poster, f.Current.PostersById[f.Main].Poster);
    }

    [Fact]
    public void Import_is_instance_local_and_same_declaration_can_refresh_paint_without_rebuilding()
    {
        using var f = new Fixture(); string other = f.NewPoster();
        var before = f.Current; var recipe = Recipe(TemplateHeadingComponent.Count, TemplateHeadingComponent.Title);
        var result = f.Coordinator.Execute(new RegisterTemplateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            before.Revision, StateReadSet.ForPoster(f.Main, before.PostersById[f.Main].EntityVersion) with
            { SettingsVersion = before.ControlCenter.Settings.EntityVersion }, TemplateRecipeCodec.Serialize(recipe), f.Main));
        Assert.True(result.Succeeded, result.Error);
        var after = f.Current;
        Assert.Equal(before.PostersById[other].Poster, after.PostersById[other].Poster);
        var presentation = after.PostersById[f.Main].Presentation;
        var reread = PosterPresentationPolicy.Resolve(after.PostersById[f.Main].Poster);
        Assert.True(TemplatePresentationRefreshPolicy.CanPaint(presentation, reread, "a", "a", "b", "b"));
        var reordered = recipe with { Components = [TemplateHeadingComponent.Title, TemplateHeadingComponent.Count] };
        var changed = presentation.Parameters with { RecipeInstances = new Dictionary<string, TemplateRecipeInstance>
            { [recipe.Id] = new(reordered) } };
        Assert.False(TemplatePresentationRefreshPolicy.CanPaint(presentation, reread with { Parameters = changed }, "a", "a", "b", "b"));
        Assert.Equal(recipe.Components, TemplateRecipeCopy.Create(recipe, "copy", "copy", new Dictionary<string, TemplateParameterValue>()).Components);
    }
}
