using System.Text.Json;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Distribution seed, not a migration or a replacement for a saved poster/theme reset.
public sealed record PosterCreationDefaults(string TemplateKey, double? BackgroundOpacityPercent)
{
    public const int MaximumJsonLength = 4096;

    public static PosterCreationDefaults Parse(string json)
    {
        if (json.Length > MaximumJsonLength) throw new ArgumentException("Creation defaults exceed 4 KiB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!names.Add(property.Name) || property.Name is not ("schemaVersion" or "templateKey" or "backgroundOpacityPercent"))
                throw new ArgumentException("Unknown or duplicate creation-default field.");
        var root = document.RootElement;
        if (names.Count != 3 || root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new ArgumentException("Unsupported or incomplete creation defaults.");
        var opacity = root.GetProperty("backgroundOpacityPercent");
        var result = new PosterCreationDefaults(root.GetProperty("templateKey").GetString() ?? "",
            opacity.ValueKind == JsonValueKind.Null ? null : opacity.GetDouble());
        result.Validate();
        return result;
    }

    public PosterDefinition ApplyToNew(PosterDefinition seed)
    {
        Validate();
        var presentation = PosterPresentationPolicy.Defaults(TemplateKey);
        if (TemplateKey == LampTemplatePolicy.TemplateKey)
            presentation = presentation with { Lamp = LampTemplatePolicy.Defaults with
                { BackgroundOpacity = BackgroundOpacityPercent!.Value } };
        return seed with { TemplateKey = TemplateKey,
            LayoutParametersJson = PosterPresentationPolicy.Serialize(presentation) };
    }

    private void Validate()
    {
        if (!PosterPresentationPolicy.IsBuiltIn(TemplateKey)) throw new ArgumentException("Unknown built-in template.");
        if (TemplateKey == LampTemplatePolicy.TemplateKey)
        {
            if (BackgroundOpacityPercent is not { } value || !double.IsFinite(value) || value is < 0 or > 100)
                throw new ArgumentException("Lamp background opacity must be in 0..100.");
        }
        else if (BackgroundOpacityPercent is not null)
            throw new ArgumentException("Background opacity is supported by lamp only; use null for other templates.");
    }
}
