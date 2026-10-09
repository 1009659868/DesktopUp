using DesktopUp.Domain;

namespace DesktopUp;

// Verification only: re-reading persisted recipes creates fresh dictionaries/property lists.
// Compare their full values, not collection identity; never use paint-only eligibility as equality.
internal static class PresentationReadbackEquality
{
    internal static bool Same(ResolvedPosterPresentation before, ResolvedPosterPresentation after)
    {
        if (before with { Parameters = after.Parameters } != after ||
            before.Parameters with { RecipeInstances = after.Parameters.RecipeInstances } != after.Parameters) return false;
        var left = before.Parameters.RecipeInstances; var right = after.Parameters.RecipeInstances;
        if (left is null || right is null) return left is null && right is null;
        if (left.Count != right.Count) return false;
        return left.All(pair => right.TryGetValue(pair.Key, out var value) && pair.Value.Values == value.Values &&
            pair.Value.Recipe with { Schema = value.Recipe.Schema } == value.Recipe &&
            pair.Value.Recipe.Schema with { Properties = value.Recipe.Schema.Properties } == value.Recipe.Schema &&
            pair.Value.Recipe.Schema.Properties.SequenceEqual(value.Recipe.Schema.Properties));
    }
}
