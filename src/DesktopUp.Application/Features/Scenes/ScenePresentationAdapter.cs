using DesktopUp.Domain;

namespace DesktopUp.Application;
public sealed record SceneSurfacePaint(string Background, byte BackgroundAlpha, string? Card);
public sealed record SceneRenderParameters(string Capability, SceneSurfacePaint Surface,
    IReadOnlyDictionary<string, TemplateParameterValue> Bindings)
{
    public bool ReplacesMetaText { get; init; }
    public CanvasDesign? Canvas { get; init; }
}

/// <summary>Read-only generic projection. Unknown modules never execute or overwrite saved values.</summary>
public static class ScenePresentationAdapter
{
    public static SceneRenderParameters Resolve(ResolvedPosterPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.Parameters.Canvas?.Current is { } canvas)
            return new("canvas.paint@1", new("#20282D", 0, null), new Dictionary<string, TemplateParameterValue>()) { Canvas = canvas };
        string id = presentation.SelectedRecipeId ?? presentation.EffectiveTemplateKey;
        var recipe = TemplateRecipeInstancePolicy.RecipeFor(presentation.Parameters, id);
        var values = presentation.TemplateValues;
        bool material = presentation.Parameters.RecipeInstances?.ContainsKey(id) == true || id is not ("night-ops" or "basic-box");
        var paint = material
            ? new SceneSurfacePaint(values.Color("surface.background", presentation.Parameters.Theme.Background),
                (byte)Math.Round(values.Number("surface.opacity", 100) * 255 / 100), values.Color("item.color", "#354237"))
            : new SceneSurfacePaint(presentation.Parameters.Theme.Background, 245, null);
        string capability = recipe?.Schema.RendererCapability ?? TemplateParameterSchemas.BasicCapability;
        var module = SceneCapabilityRegistry.Find(capability);
        if (module?.TransparentSurface == true) paint = paint with { BackgroundAlpha = 0 };
        if (presentation.Parameters.Surface is { } surface)
            paint = paint with { Background = surface.Color, BackgroundAlpha = (byte)Math.Round(surface.OpacityPercent * 255 / 100) };
        if (!SceneCapabilityRegistry.Available(capability)) capability = TemplateParameterSchemas.BasicCapability;
        return new(capability, paint, capability == TemplateParameterSchemas.BasicCapability ? new TemplateParameterValues([]).Bindings : values.Bindings)
        { ReplacesMetaText = module?.ReplacesMetaText == true };
    }
}
