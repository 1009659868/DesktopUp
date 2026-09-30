using System.Collections.ObjectModel;

namespace DesktopUp.Domain;

public static partial class TemplateRecipeInstancePolicy
{
    public static TemplateRecipe? RecipeFor(PosterPresentationParameters parameters, string id) =>
        parameters.RecipeInstances?.GetValueOrDefault(id)?.Recipe ?? TemplateRecipeCatalog.BuiltIns.FirstOrDefault(r => r.Id == id);
    public static TemplateParameterValues CurrentValues(PosterPresentationParameters parameters, string templateId)
    {
        if (parameters.RecipeInstances?.TryGetValue(templateId, out var instance) == true) return instance.Values;
        if (LegacyLampValues.Current(parameters, templateId) is { } legacy) return legacy;
        var recipe = RecipeFor(parameters, templateId);
        return (recipe is null ? new TemplateParameterValues([]) : DefaultValues(recipe))
            .Set("surface.background", TemplateParameterValue.FromColor(parameters.Theme.Background));
    }
    public static TemplateParameterValues DefaultValues(TemplateRecipe recipe)
    {
        recipe = TemplateRecipeCodec.Freeze(recipe, stored: true);
        var bindings = SceneCapabilityRegistry.Find(recipe.Schema.RendererCapability)?.BuiltIn.Schema.Properties
            .ToDictionary(p => p.Binding, p => p.DefaultValue, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        bindings["surface.background"] = TemplateParameterValue.FromColor(recipe.Base.Background);
        bindings["item.color"] = TemplateParameterValue.FromColor(recipe.Base.Card);
        bindings.TryAdd("surface.opacity", TemplateParameterValue.FromNumber(100));
        foreach (var property in recipe.Schema.Properties) bindings[property.Binding] = property.DefaultValue;
        return new(bindings);
    }
    public static string? Validate(IReadOnlyDictionary<string, TemplateRecipeInstance>? instances)
    {
        if (instances is null) return null;
        if (instances.Count > TemplateRecipeCatalog.MaximumTemplates) return "模板实例超过上限。";
        foreach (var (id, instance) in instances)
        {
            if (instance is null || instance.Recipe is null || instance.Values is null || id != instance.Recipe.Id ||
                id is "lamp" or "night-ops" or "basic-box") return "模板实例身份无效。";
            try
            {
                TemplateRecipeCodec.Freeze(instance.Recipe, stored: true);
                var represented = DefaultValues(instance.Recipe);
                foreach (var property in instance.Recipe.Schema.Properties)
                {
                    var value = instance.Values.Get(property.Binding);
                    if (!TemplateParameterSchemas.Accepts(property, value)) return "实例参数不符合配方声明。";
                    represented = represented.Set(property.Binding, value);
                }
                if (represented != instance.Values) return "实例包含配方未声明的覆盖。";
            }
            catch (ArgumentException error) { return error.Message; }
        }
        return null;
    }
    public static PosterPresentationParameters Select(PosterPresentationParameters parameters, TemplateRecipe recipe)
    {
        recipe = TemplateRecipeCodec.Freeze(recipe);
        if (recipe.Id is "lamp" or "night-ops" or "basic-box") return parameters;
        var instances = parameters.RecipeInstances?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        if (!instances.ContainsKey(recipe.Id)) instances.Add(recipe.Id, new(recipe));
        return parameters with { RecipeInstances = new ReadOnlyDictionary<string, TemplateRecipeInstance>(instances) };
    }
    public static PosterPresentationParameters UpdateValues(PosterPresentationParameters parameters, string templateId, TemplateParameterValues values)
    {
        var recipe = RecipeFor(parameters, templateId) ?? throw new ArgumentException("所选模板能力不可用。");
        TemplateRecipeCodec.Freeze(recipe);
        if (LegacyLampValues.TryUpdate(parameters, templateId, values) is { } legacy) return legacy;
        if (parameters.RecipeInstances is not { } prior || !prior.TryGetValue(templateId, out var existing))
            throw new ArgumentException("所选模板实例不存在。");
        var instances = prior.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        instances[templateId] = existing with { Values = values };
        if (Validate(instances) is { } error) throw new ArgumentException(error);
        return parameters with { RecipeInstances = new ReadOnlyDictionary<string, TemplateRecipeInstance>(instances) };
    }
    // Explicit old-API boundary; a future renderer never needs a Lamp value.
    public static PosterPresentationParameters Update(PosterPresentationParameters parameters, string templateId, LampTemplateParameters value) =>
        UpdateValues(parameters, templateId, LegacyLampValues.ForRecipe(RecipeFor(parameters, templateId) ??
            throw new ArgumentException("Unknown template."), value));
}
