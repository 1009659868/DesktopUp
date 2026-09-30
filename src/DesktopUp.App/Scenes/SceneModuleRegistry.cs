using DesktopUp.Domain;
using Microsoft.UI.Xaml.Controls;
namespace DesktopUp;
// Only trusted build-linked initializers register factories. There is no dynamic code loader.
internal static class SceneModuleRegistry
{
    private static readonly Dictionary<string, Func<IPosterSceneInstance>> Factories = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Func<TemplateRecipe, Grid>> Samples = new(StringComparer.Ordinal);
    internal static void Register(string capability, Func<IPosterSceneInstance> factory, Func<TemplateRecipe, Grid>? sample = null)
    {
        Factories.Add(capability, factory);
        if (sample is not null) Samples.Add(capability, sample);
    }
    internal static Grid? CreateSample(TemplateRecipe recipe) =>
        Samples.TryGetValue(recipe.Schema.RendererCapability, out var factory) ? factory(recipe) : null;
    internal static IPosterSceneInstance? Create(string capability) =>
        Factories.TryGetValue(capability, out var factory) ? factory() : null;
}
