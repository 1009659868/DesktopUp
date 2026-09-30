using System.Collections.ObjectModel;

namespace DesktopUp.Domain;

public enum TemplateParameterKind { Color, Range, Boolean }
public sealed record TemplateParameterValue(string? Color = null, double? Number = null, bool? Boolean = null)
{
    public static TemplateParameterValue FromColor(string value) => new(Color: value);
    public static TemplateParameterValue FromNumber(double value) => new(Number: value);
    public static TemplateParameterValue FromBoolean(bool value) => new(Boolean: value);
}
public sealed record TemplateParameterDefinition(string Key, string Binding, TemplateParameterKind Kind,
    string Label, string Group, TemplateParameterValue DefaultValue, string Hint = "", string Unit = "",
    double? Minimum = null, double? Maximum = null, double? Step = null);
public sealed record TemplateParameterSchema(string TemplateId, string Name, string RendererCapability,
    IReadOnlyList<TemplateParameterDefinition> Properties, int SchemaVersion = 1, int TemplateVersion = 1);

/// <summary>Trusted built-in descriptions, not a package registry or executable template format.
/// UI generation uses Kind; adapters use Binding. Parameter keys and labels are not renderer switches.</summary>
public static class TemplateParameterSchemas
{
    public const int MaximumProperties = 20;
    public const string LampCapability = "lamp.paint@1";
    public const string BasicCapability = "builtin.surface@1";
    private static readonly TemplateParameterSchema Night = Freeze(new("night-ops", "夜航", BasicCapability, []));
    private static readonly TemplateParameterSchema Basic = Freeze(new("basic-box", "基础收纳", BasicCapability, []));

    public static TemplateParameterSchema? ForTemplate(string? id) => id switch
    { "night-ops" => Night, "basic-box" => Basic, _ => SceneCapabilityRegistry.Templates.FirstOrDefault(r => r.Id == id)?.Schema };

    public static TemplateParameterSchema Freeze(TemplateParameterSchema schema, bool stored = false)
    {
        if (Validate(schema, stored) is { } error) throw new ArgumentException(error);
        return schema with { Properties = Array.AsReadOnly(schema.Properties.ToArray()) };
    }

    public static string? Validate(TemplateParameterSchema? schema, bool stored = false)
    {
        if (schema is null || !Key(schema.TemplateId) || !Text(schema.Name, 40) || schema.SchemaVersion != 1 ||
            schema.TemplateVersion is < 1 or > 999 || !CapabilityKey(schema.RendererCapability) || !stored && !SceneCapabilityRegistry.Available(schema.RendererCapability) ||
            schema.Properties is null || schema.Properties.Count > MaximumProperties) return "Unknown or invalid built-in parameter description.";
        var keys = new HashSet<string>(StringComparer.Ordinal); var bindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in schema.Properties)
        {
            if (property is null || !Key(property.Key) || !keys.Add(property.Key) || !bindings.Add(property.Binding) ||
                !Text(property.Label, 30) || !Text(property.Group, 20) || !OptionalText(property.Hint, 160) || !OptionalText(property.Unit, 8))
                return "Invalid or duplicate parameter identity, binding or text.";
            var expected = BindingKind(schema.RendererCapability, property.Binding);
            if ((!stored || SceneCapabilityRegistry.Available(schema.RendererCapability)) && (expected is null || expected != property.Kind) || !Key(property.Binding)) return "Unknown capability binding or mismatched control type.";
            if (property.Kind == TemplateParameterKind.Range)
            {
                if (property.Minimum is not { } min || property.Maximum is not { } max || property.Step is not { } step ||
                    !double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(step) || min < 0 || max > 100 || min >= max ||
                    step <= 0 || step > max - min || (max - min) / step > 1000 || !OnStep(max, min, step))
                    return "Parameter range exceeds the supported paint capability.";
                var limit = SceneCapabilityRegistry.Find(schema.RendererCapability)?.BuiltIn.Schema.Properties.FirstOrDefault(p => p.Binding == property.Binding);
                if (limit is not null && (min < limit.Minimum || max > limit.Maximum))
                    return "Parameter range exceeds its declared module capability.";
            }
            else if (property.Minimum is not null || property.Maximum is not null || property.Step is not null || property.Unit.Length != 0)
                return "Color parameters cannot declare numeric ranges or units.";
            if (!Accepts(property, property.DefaultValue) || property.Kind == TemplateParameterKind.Range &&
                !OnStep(property.DefaultValue.Number!.Value, property.Minimum!.Value, property.Step!.Value)) return "Invalid parameter default.";
        }
        return null;
    }

    public static TemplateParameterKind? BindingKind(string capability, string binding) =>
        SceneCapabilityRegistry.Find(capability)?.BuiltIn.Schema.Properties.FirstOrDefault(p => p.Binding == binding)?.Kind ??
        (capability == BasicCapability ? binding switch
        {
            "surface.background" or "item.color" => TemplateParameterKind.Color,
            "surface.opacity" => TemplateParameterKind.Range,
            _ => (TemplateParameterKind?)null
        } : null);
    private static bool CapabilityKey(string? value)
    {
        if (value is null) return false;
        var parts = value.Split('@');
        return parts.Length == 2 && Key(parts[0]) && int.TryParse(parts[1], out int version) && version is >= 1 and <= 999;
    }
    // Existing saved finite percentages need not lie on the UI slider step; reading them must not round author intent.
    public static bool Accepts(TemplateParameterDefinition property, TemplateParameterValue? value) => value is not null &&
        (property.Kind == TemplateParameterKind.Boolean ? value.Color is null && value.Number is null && value.Boolean is not null :
         property.Kind == TemplateParameterKind.Color ? value.Number is null && value.Boolean is null && value.Color is { Length: 7 } text && text[0] == '#' &&
            !text.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") :
            value.Color is null && value.Boolean is null && value.Number is { } number && double.IsFinite(number) && number >= property.Minimum && number <= property.Maximum);
    public static IReadOnlyDictionary<string, TemplateParameterValue> Defaults(TemplateParameterSchema schema, bool stored = false)
    {
        if (Validate(schema, stored) is { } error) throw new ArgumentException(error);
        return new ReadOnlyDictionary<string, TemplateParameterValue>(schema.Properties.ToDictionary(p => p.Key, p => p.DefaultValue, StringComparer.Ordinal));
    }
    private static bool OnStep(double value, double min, double step) => Math.Abs((value - min) / step - Math.Round((value - min) / step)) < 1e-7;
    private static bool Text(string? value, int max) => value is { Length: > 0 } && value.Length <= max && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    private static bool OptionalText(string? value, int max) => value is not null && (value.Length == 0 || Text(value, max));
    private static bool Key(string? value) => value is { Length: > 0 and <= 48 } && value[0] is >= 'a' and <= 'z' &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && value is not ("constructor" or "prototype" or "__proto__");
}
