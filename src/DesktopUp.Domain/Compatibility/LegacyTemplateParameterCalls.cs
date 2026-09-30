namespace DesktopUp.Domain;
public static partial class TemplateRecipeInstancePolicy
{
    // Source compatibility for v3/v4 callers and old probes. New runtime code uses CurrentValues/DefaultValues.
    public static LampTemplateParameters Current(PosterPresentationParameters parameters, string templateId) =>
        LegacyLampValues.FromValues(CurrentValues(parameters, templateId));
    public static LampTemplateParameters Defaults(TemplateRecipe recipe) => LegacyLampValues.FromValues(DefaultValues(recipe));    public static LampTemplateParameters Set(LampTemplateParameters result, string binding, TemplateParameterValue value) => binding switch
    {
        "lamp.color" => result with { LightColor = value.Color! }, "lamp.softness" => result with { Softness = value.Number!.Value },
        "lamp.intensity" => result with { Intensity = value.Number!.Value }, "surface.background" => result with { Background = value.Color! },
        "item.color" => result with { Card = value.Color! }, "surface.opacity" => result with { BackgroundOpacity = value.Number!.Value },
        "scene.motion" => result with { MotionEnabled = value.Boolean!.Value }, "lamp.follow" => result with { FollowPointer = value.Boolean!.Value },
        "lamp.sway" => result with { IdleSway = value.Number!.Value }, "lamp.speed" => result with { MotionSpeed = value.Number!.Value },
        _ => throw new ArgumentException("未知绑定。")
    };
    public static TemplateParameterValue Get(LampTemplateParameters value, string binding) => binding switch
    {
        "lamp.color" => TemplateParameterValue.FromColor(value.LightColor), "lamp.softness" => TemplateParameterValue.FromNumber(value.Softness),
        "lamp.intensity" => TemplateParameterValue.FromNumber(value.Intensity), "surface.background" => TemplateParameterValue.FromColor(value.Background),
        "item.color" => TemplateParameterValue.FromColor(value.Card), "surface.opacity" => TemplateParameterValue.FromNumber(value.BackgroundOpacity),
        "scene.motion" => TemplateParameterValue.FromBoolean(value.MotionEnabled), "lamp.follow" => TemplateParameterValue.FromBoolean(value.FollowPointer),
        "lamp.sway" => TemplateParameterValue.FromNumber(value.IdleSway), "lamp.speed" => TemplateParameterValue.FromNumber(value.MotionSpeed),
        _ => throw new ArgumentException("未知绑定。")
    };
}

public sealed partial record TemplateRecipeInstance
{
    public TemplateRecipeInstance(TemplateRecipe recipe, LampTemplateParameters parameters) : this(recipe) =>
        Values = LegacyLampValues.ForRecipe(recipe, parameters);
    public LampTemplateParameters Parameters
    {
        get => LegacyLampValues.FromValues(Values);
        init => Values = LegacyLampValues.ForRecipe(Recipe, value);
    }
}
public static class LegacyLampValues
{
    public static TemplateParameterValues? Current(PosterPresentationParameters parameters, string id) =>
        id == "lamp" ? ToValues(parameters.Lamp ?? LampTemplatePolicy.Defaults) : null;
    public static PosterPresentationParameters? TryUpdate(PosterPresentationParameters parameters, string id, TemplateParameterValues values)
    {
        if (id != "lamp") return null;
        var value = FromValues(values);
        if (ToValues(value) != values) throw new ArgumentException("Unrepresentable legacy lamp values.");
        if (LampTemplatePolicy.Validate(value) is { } error) throw new ArgumentException(error);
        return parameters with { Lamp = value };
    }
    private static readonly string[] Bindings = ["lamp.color", "lamp.softness", "lamp.intensity", "surface.background",
        "item.color", "surface.opacity", "scene.motion", "lamp.follow", "lamp.sway", "lamp.speed"];
    public static TemplateParameterValues ToValues(LampTemplateParameters value) =>
        new(Bindings.Select(binding => new KeyValuePair<string, TemplateParameterValue>(binding, TemplateRecipeInstancePolicy.Get(value, binding))));
    public static LampTemplateParameters FromValues(TemplateParameterValues values)
    {
        var result = LampTemplatePolicy.Defaults;
        foreach (string binding in Bindings)
            if (values.Bindings.TryGetValue(binding, out var value)) result = TemplateRecipeInstancePolicy.Set(result, binding, value);
        return result;
    }
    public static TemplateParameterValues ForRecipe(TemplateRecipe recipe, LampTemplateParameters value)
    {
        var defaults = TemplateRecipeInstancePolicy.DefaultValues(recipe);
        var projected = ToValues(value);
        var result = new TemplateParameterValues(defaults.Bindings.Select(pair =>
            new KeyValuePair<string, TemplateParameterValue>(pair.Key, projected.Bindings.GetValueOrDefault(pair.Key) ?? pair.Value)));
        // Never silently discard a legacy override which this recipe cannot represent.
        var represented = FromValues(result);
        if (represented != value) result = result.Set("legacy.unrepresented", TemplateParameterValue.FromBoolean(true));
        return result;
    }
}
