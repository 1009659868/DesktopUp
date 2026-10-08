using System.Collections.ObjectModel;

namespace DesktopUp.Domain;

/// <summary>Immutable, structurally comparable typed bindings. No renderer-specific fields.</summary>
public sealed record TemplateParameterValues
{
    public IReadOnlyDictionary<string, TemplateParameterValue> Bindings { get; }
    public TemplateParameterValues(IEnumerable<KeyValuePair<string, TemplateParameterValue>> bindings) =>
        Bindings = new ReadOnlyDictionary<string, TemplateParameterValue>(bindings.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    public TemplateParameterValues Set(string binding, TemplateParameterValue value)
    {
        var copy = Bindings.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        copy[binding] = value; return new(copy);
    }
    public TemplateParameterValue Get(string binding) => Bindings.TryGetValue(binding, out var value)
        ? value : throw new ArgumentException("Missing declared binding: " + binding);
    public string Color(string binding, string fallback) => Bindings.TryGetValue(binding, out var value) ? value.Color ?? fallback : fallback;
    public double Number(string binding, double fallback) => Bindings.TryGetValue(binding, out var value) ? value.Number ?? fallback : fallback;
    public bool Boolean(string binding, bool fallback) => Bindings.TryGetValue(binding, out var value) ? value.Boolean ?? fallback : fallback;
    public bool Equals(TemplateParameterValues? other) => other is not null && Bindings.Count == other.Bindings.Count &&
        Bindings.All(pair => other.Bindings.TryGetValue(pair.Key, out var value) && value == pair.Value);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pair in Bindings.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        return hash.ToHashCode();
    }
}

public sealed record SceneCapabilityDefinition(string Id, TemplateRecipe BuiltIn, bool ExposeTemplate = true,
    bool TransparentSurface = false, bool ReplacesMetaText = false,
    PosterTitleChromeKind TitleChrome = PosterTitleChromeKind.Basic, string? TitleColorBinding = null,
    IReadOnlyList<TemplateRecipe>? PreviousBuiltIns = null);

/// <summary>Build-linked trusted declarations. No filesystem scanning or runtime code loading.</summary>
public static class SceneCapabilityRegistry
{
    private static readonly Dictionary<string, SceneCapabilityDefinition> Modules = new(StringComparer.Ordinal);
    public static void Register(SceneCapabilityDefinition module)
    {
        if (module.Id != module.BuiltIn.Schema.RendererCapability || Modules.ContainsKey(module.Id) ||
            Modules.Values.Any(m => m.BuiltIn.Id == module.BuiltIn.Id)) throw new ArgumentException("Duplicate capability or template.");
        Modules.Add(module.Id, module);
        try
        {
            var previous=module.PreviousBuiltIns ?? [];
            if(previous.Select(p=>p.Schema.TemplateVersion).Distinct().Count()!=previous.Count || previous.Count>16 ||
                previous.Any(p=>p.Id!=module.BuiltIn.Id || p.Schema.RendererCapability!=module.Id ||
                    p.Schema.TemplateVersion>=module.BuiltIn.Schema.TemplateVersion ||
                    p.Base!=module.BuiltIn.Base || p.Family!=module.BuiltIn.Family || p.Name!=module.BuiltIn.Name ||
                    p.Schema.Properties.Count>=module.BuiltIn.Schema.Properties.Count ||
                    p.Schema.Properties.Any(property=>!module.BuiltIn.Schema.Properties.Contains(property))))
                throw new ArgumentException("Built-in revisions must only append parameters to exact declared predecessors.");
            Modules[module.Id] = module with { BuiltIn = TemplateRecipeCodec.Freeze(module.BuiltIn),
                PreviousBuiltIns=Array.AsReadOnly(previous.Select(p=>TemplateRecipeCodec.Freeze(p)).ToArray()) };
        }
        catch { Modules.Remove(module.Id); throw; }
    }
    public static SceneCapabilityDefinition? Find(string capability) => Modules.GetValueOrDefault(capability);
    public static IReadOnlyList<TemplateRecipe> Templates => Array.AsReadOnly(Modules.Values.Where(m => m.ExposeTemplate)
        .OrderBy(m => m.Id, StringComparer.Ordinal).Select(m => m.BuiltIn).ToArray());
    public static bool Available(string capability) => capability == TemplateParameterSchemas.BasicCapability || Find(capability) is not null;
}
