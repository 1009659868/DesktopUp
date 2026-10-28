using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class TemplatePresentationRefreshPolicyTests
{
    private static TemplateRecipe Recipe(string id) => TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "lamp") with
    { Schema = TemplateParameterSchemas.ForTemplate("lamp")! with { TemplateId = id } };

    private static ResolvedPosterPresentation Custom()
    {
        var parameters = PosterPresentationPolicy.Defaults("lamp") with
        {
            Lamp = new(Intensity: 12),
            RecipeInstances = new Dictionary<string, TemplateRecipeInstance>(StringComparer.Ordinal)
            {
                ["custom-a"] = new(Recipe("custom-a"), new(Intensity: 45)),
                ["custom-b"] = new(Recipe("custom-b"), new(Intensity: 80))
            }
        };
        return new("lamp", parameters, PosterContentMetrics.Current, true, false, null) { SelectedRecipeId = "custom-a" };
    }

    private static bool CanPaint(ResolvedPosterPresentation before, ResolvedPosterPresentation after) =>
        TemplatePresentationRefreshPolicy.CanPaint(before, after, "My desk", "My desk", "Collection", "Collection");

    private static ResolvedPosterPresentation Clone(ResolvedPosterPresentation value) => value with
    {
        Parameters = value.Parameters with
        {
            RecipeInstances = value.Parameters.RecipeInstances!.ToDictionary(pair => pair.Key, pair => pair.Value with
            {
                Recipe = pair.Value.Recipe with { Schema = pair.Value.Recipe.Schema with
                    { Properties = pair.Value.Recipe.Schema.Properties.Select(property => property with { }).ToArray() } },
                Parameters = pair.Value.Parameters with { }
            }, StringComparer.Ordinal)
        }
    };

    [Fact]
    public void Reparsed_dictionary_and_property_lists_preserve_paint_only_eligibility()
    {
        var before = Custom(); var after = Clone(before);
        Assert.NotSame(before.Parameters.RecipeInstances, after.Parameters.RecipeInstances);
        Assert.NotSame(before.Parameters.RecipeInstances!["custom-a"].Recipe.Schema.Properties,
            after.Parameters.RecipeInstances!["custom-a"].Recipe.Schema.Properties);
        Assert.True(CanPaint(before, after));
    }

    [Fact]
    public void Only_selected_custom_instance_values_can_change_without_rebuilding()
    {
        var before = Custom(); var after = Clone(before);
        var recipes = after.Parameters.RecipeInstances!.ToDictionary(pair => pair.Key, pair => pair.Value);
        recipes["custom-a"] = recipes["custom-a"] with { Parameters = new(LightColor: "#ABCDEF", Softness: 12,
            BackgroundOpacity: 30, FollowPointer: false, IdleSway: 90, MotionSpeed: 19) };
        after = after with { Parameters = after.Parameters with { RecipeInstances = recipes } };
        Assert.True(CanPaint(before, after));
        recipes["custom-b"] = recipes["custom-b"] with { Parameters = new(Intensity: 99) };
        Assert.False(CanPaint(before, after));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("version")]
    [InlineData("name")]
    [InlineData("property-label")]
    [InlineData("property-default")]
    [InlineData("base")]
    [InlineData("family")]
    public void Recipe_catalog_or_definition_changes_require_rebuilding(string change)
    {
        var before = Custom(); var after = Clone(before);
        var recipes = after.Parameters.RecipeInstances!.ToDictionary(pair => pair.Key, pair => pair.Value);
        var selected = recipes["custom-a"]; var recipe = selected.Recipe;
        switch (change)
        {
            case "add": recipes.Add("custom-c", new(Recipe("custom-c"), new())); break;
            case "remove": recipes.Remove("custom-b"); break;
            case "version": recipe = recipe with { Schema = recipe.Schema with { TemplateVersion = 2 } }; break;
            case "name": recipe = recipe with { Schema = recipe.Schema with { Name = "Another desk" } }; break;
            case "property-label": recipe = recipe with { Schema = recipe.Schema with { Properties = recipe.Schema.Properties
                .Select((p, i) => i == 0 ? p with { Label = "Another light" } : p).ToArray() } }; break;
            case "property-default": recipe = recipe with { Schema = recipe.Schema with { Properties = recipe.Schema.Properties
                .Select((p, i) => i == 0 ? p with { DefaultValue = TemplateParameterValue.FromColor("#102030") } : p).ToArray() } }; break;
            case "base": recipe = recipe with { Base = recipe.Base with { Background = "#102030" } }; break;
            case "family": recipe = recipe with { Family = "Another family" }; break;
        }
        recipes["custom-a"] = selected with { Recipe = recipe };
        Assert.False(CanPaint(before, after with { Parameters = after.Parameters with { RecipeInstances = recipes } }));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("subtitle")]
    [InlineData("metrics")]
    [InlineData("content")]
    [InlineData("preferences")]
    [InlineData("theme")]
    [InlineData("selected")]
    [InlineData("renderer")]
    [InlineData("editing")]
    [InlineData("builtin-override")]
    public void Text_geometry_template_and_other_values_are_never_swallowed_by_paint_only(string change)
    {
        var before = Custom(); var after = Clone(before);
        after = change switch
        {
            "metrics" => after with { Metrics = after.Metrics with { Version = "new-metrics" } },
            "content" => after with { Parameters = after.Parameters with { Content = after.Parameters.Content with { TitleFontSizeDip = 28 } } },
            "preferences" => after with { Parameters = after.Parameters with { Preferences = new(Scale: 1.2) } },
            "theme" => after with { Parameters = after.Parameters with { Theme = after.Parameters.Theme with { Accent = "#102030" } } },
            "selected" => after with { SelectedRecipeId = "custom-b" },
            "renderer" => after with { EffectiveTemplateKey = "basic-box" },
            "editing" => after with { CanEdit = false },
            "builtin-override" => after with { Parameters = after.Parameters with { Lamp = new(Intensity: 99) } },
            _ => after
        };
        Assert.False(TemplatePresentationRefreshPolicy.CanPaint(before, after, "My desk", change == "title" ? "Changed" : "My desk",
            "Collection", change == "subtitle" ? "Changed" : "Collection"));
    }

    [Fact]
    public void Builtin_lamp_can_still_refresh_without_a_custom_recipe_dictionary()
    {
        var before = new ResolvedPosterPresentation("lamp", PosterPresentationPolicy.Defaults("lamp"), PosterContentMetrics.Current,
            true, false, null) { SelectedRecipeId = "lamp" };
        Assert.True(CanPaint(before, before with { Parameters = before.Parameters with { Lamp = new(Intensity: 10) } }));
    }

    [Fact]
    public void Custom_minimal_background_and_card_values_use_the_same_geometry_preserving_gate()
    {
        var recipe = Recipe("custom-a");
        recipe = recipe with { Schema = recipe.Schema with { RendererCapability = TemplateParameterSchemas.BasicCapability,
            Properties = recipe.Schema.Properties.Where(p => p.Binding is "surface.background" or "item.color" or "surface.opacity").ToArray() } };
        var before = Custom() with { EffectiveTemplateKey = "night-ops" };
        before = before with { Parameters = before.Parameters with { RecipeInstances = new Dictionary<string, TemplateRecipeInstance>
            { ["custom-a"] = new(recipe, new()) } } };
        var after = Clone(before);
        after = after with { Parameters = after.Parameters with { RecipeInstances = new Dictionary<string, TemplateRecipeInstance>
            { ["custom-a"] = new(recipe, new(Background: "#203040", Card: "#506070", BackgroundOpacity: 25)) } } };
        Assert.True(CanPaint(before, after));
    }

    [Fact]
    public void Builtin_minimal_without_recipe_does_not_treat_lamp_storage_as_its_paint()
    {
        var before = new ResolvedPosterPresentation("night-ops", PosterPresentationPolicy.Defaults(), PosterContentMetrics.Current,
            true, false, null) { SelectedRecipeId = "night-ops" };
        Assert.False(CanPaint(before, before with { Parameters = before.Parameters with { Lamp = new(Intensity: 10) } }));
    }
}
