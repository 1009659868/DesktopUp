using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Allows only the current controlled template's paint values to change without rebuilding content.</summary>
public static class TemplatePresentationRefreshPolicy
{
    public static bool CanPaint(ResolvedPosterPresentation previous, ResolvedPosterPresentation next,
        string? previousTitle, string? nextTitle, string? previousSubtitle, string? nextSubtitle)
    {
        if (previousTitle != nextTitle || previousSubtitle != nextSubtitle ||
            previous.EffectiveTemplateKey is not (LampTemplatePolicy.TemplateKey or "night-ops" or "basic-box") ||
            previous with { Parameters = next.Parameters } != next) return false;
        var before = previous.Parameters; var after = next.Parameters;
        if (before.Content != after.Content || before.Theme != after.Theme || before.Preferences != after.Preferences) return false;

        string selected = previous.SelectedRecipeId ?? previous.EffectiveTemplateKey;
        bool builtIn = selected == LampTemplatePolicy.TemplateKey;
        if (!builtIn && before.Lamp != after.Lamp) return false;
        var oldRecipes = before.RecipeInstances;
        var newRecipes = after.RecipeInstances;
        if ((oldRecipes?.Count ?? 0) != (newRecipes?.Count ?? 0)) return false;
        if (oldRecipes is null || oldRecipes.Count == 0) return builtIn;
        if (newRecipes is null || !oldRecipes.Keys.Order(StringComparer.Ordinal).SequenceEqual(
            newRecipes.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)) return false;
        if (!builtIn && (!oldRecipes.TryGetValue(selected, out var selectedRecipe) ||
            selectedRecipe.Recipe.EffectiveTemplateKey != previous.EffectiveTemplateKey)) return false;
        foreach (var pair in oldRecipes)
        {
            var updated = newRecipes[pair.Key];
            // Records alone compare dictionary/list references. Readback reparses them, so
            // compare every declaration value and every nonselected instance structurally.
            if (!SameRecipe(pair.Value.Recipe, updated.Recipe) ||
                (builtIn || pair.Key != selected) && pair.Value.Parameters != updated.Parameters) return false;
        }
        return true;
    }

    private static bool SameRecipe(TemplateRecipe before, TemplateRecipe after) =>
        before with { Schema = after.Schema } == after &&
        before.Schema with { Properties = after.Schema.Properties } == after.Schema &&
        before.Schema.Properties.SequenceEqual(after.Schema.Properties);
}
