using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PresentationReadbackEqualityTests
{
    private static ResolvedPosterPresentation Original()
    {
        var recipe = TemplateRecipeCatalog.BuiltIns.Single(x => x.Id == "lamp");
        var other = recipe with { Schema = recipe.Schema with { TemplateId = "other" } };
        var parameters = PosterPresentationPolicy.Defaults("lamp") with
        {
            RecipeInstances = new Dictionary<string, TemplateRecipeInstance>
            { [recipe.Id] = new(recipe), [other.Id] = new(other) }
        };
        return new("lamp", parameters, PosterContentMetrics.Current, true, false, null) { SelectedRecipeId = "lamp" };
    }
    private static ResolvedPosterPresentation Clone(ResolvedPosterPresentation value) => value with
    {
        Parameters = value.Parameters with { RecipeInstances = value.Parameters.RecipeInstances!.Reverse().ToDictionary(
            p => p.Key, p => p.Value with { Recipe = TemplateRecipeCodec.Parse(TemplateRecipeCodec.Serialize(p.Value.Recipe)) }) }
    };
    [Fact]
    public void Reparsed_declarations_match_by_value_not_dictionary_or_list_reference()
    {
        var before = Original(); var after = Clone(before);
        Assert.NotEqual(before, after); // The old record-only probe rejected this exact valid case.
        Assert.True(PresentationReadbackEquality.Same(before, after));
        Assert.True(PresentationReadbackEquality.Same(after, before));
    }
    [Theory]
    [InlineData("selected-values")]
    [InlineData("unselected-values")]
    [InlineData("declaration-name")]
    [InlineData("declaration-version")]
    [InlineData("property-label")]
    [InlineData("property-order")]
    [InlineData("base")]
    [InlineData("missing")]
    [InlineData("added")]
    [InlineData("title-hidden")]
    [InlineData("title-colors")]
    [InlineData("content")]
    [InlineData("selected-id")]
    [InlineData("warning")]
    [InlineData("editing")]
    public void Actual_readback_changes_are_not_hidden(string change)
    {
        var before = Original(); var after = Clone(before);
        var recipes = after.Parameters.RecipeInstances!.ToDictionary(p => p.Key, p => p.Value);
        var item = recipes["lamp"]; var recipe = item.Recipe;
        switch (change)
        {
            case "selected-values": item = item with { Values = item.Values.Set("surface.opacity", TemplateParameterValue.FromNumber(25)) }; break;
            case "unselected-values": recipes["other"] = recipes["other"] with { Values = item.Values.Set("surface.opacity", TemplateParameterValue.FromNumber(25)) }; break;
            case "declaration-name": recipe = recipe with { Schema = recipe.Schema with { Name = "Changed" } }; break;
            case "declaration-version": recipe = recipe with { Schema = recipe.Schema with { TemplateVersion = 2 } }; break;
            case "property-label": recipe = recipe with { Schema = recipe.Schema with { Properties = recipe.Schema.Properties.Select((p,i) => i == 0 ? p with { Label = "Changed" } : p).ToArray() } }; break;
            case "property-order": recipe = recipe with { Schema = recipe.Schema with { Properties = recipe.Schema.Properties.Reverse().ToArray() } }; break;
            case "base": recipe = recipe with { Base = recipe.Base with { Background = "#010203" } }; break;
            case "added": recipes.Add("extra", item); break;
            case "title-hidden": after = after with { Parameters = after.Parameters with { ShowTitle = false } }; break;
            case "title-colors": after = after with { Parameters = after.Parameters with { TitleColors = new("#010203") } }; break;
            case "content": after = after with { Parameters = after.Parameters with { Content = after.Parameters.Content with { GapDip = 77 } } }; break;
            case "selected-id": after = after with { SelectedRecipeId = "other" }; break;
            case "warning": after = after with { Warning = "Changed" }; break;
            case "editing": after = after with { CanEdit = false }; break;
        }
        recipes["lamp"] = item with { Recipe = recipe };
        if (change == "missing") recipes.Remove("other");
        after = after with { Parameters = after.Parameters with { RecipeInstances = recipes } };
        Assert.False(PresentationReadbackEquality.Same(before, after));
    }
    [Fact]
    public void Null_recipes_and_empty_recipes_are_not_conflated()
    {
        var value = Original();
        var noRecipes = value with { Parameters = value.Parameters with { RecipeInstances = null } };
        Assert.True(PresentationReadbackEquality.Same(noRecipes, noRecipes with { }));
        Assert.False(PresentationReadbackEquality.Same(noRecipes, noRecipes with
        { Parameters = noRecipes.Parameters with { RecipeInstances = new Dictionary<string, TemplateRecipeInstance>() } }));
    }
}
