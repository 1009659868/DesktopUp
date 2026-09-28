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
    private static TemplateParameterDefinition Color(string key, string binding, string label, string group, string value, string hint = "") =>
        new(key, binding, TemplateParameterKind.Color, label, group, TemplateParameterValue.FromColor(value), hint);
    private static TemplateParameterDefinition Range(string key, string binding, string label, string group, double value, string hint = "") =>
        new(key, binding, TemplateParameterKind.Range, label, group, TemplateParameterValue.FromNumber(value), hint, "%", 0, 100, 1);
    private static readonly TemplateParameterSchema Lamp = Freeze(new("lamp", "深夜书桌", LampCapability,
    [
        Color("lightColor", "lamp.color", "灯光颜色", "光影", LampTemplatePolicy.Defaults.LightColor, "仅当前海报的灯光，不改变控制中心配色。"),
        Range("softness", "lamp.softness", "柔和度", "光影", LampTemplatePolicy.Defaults.Softness),
        Range("intensity", "lamp.intensity", "灯光强度", "光影", LampTemplatePolicy.Defaults.Intensity),
        Color("background", "surface.background", "桌面底色", "材质", LampTemplatePolicy.Defaults.Background),
        Color("card", "item.color", "卡片颜色", "材质", LampTemplatePolicy.Defaults.Card),
        Range("backgroundOpacity", "surface.opacity", "背景不透明度", "材质", LampTemplatePolicy.Defaults.BackgroundOpacity,
            "只改变海报背景透明度，文字与 Item 不会一起变透明。"),
        new("motionEnabled", "scene.motion", TemplateParameterKind.Boolean, "环境动效", "动效", TemplateParameterValue.FromBoolean(true)),
        new("followPointer", "lamp.follow", TemplateParameterKind.Boolean, "灯光跟随鼠标", "动效", TemplateParameterValue.FromBoolean(true)),
        Range("idleSway", "lamp.sway", "闲置轻摆", "动效", 35),
        Range("motionSpeed", "lamp.speed", "摆动速度", "动效", 40)
    ]));
    private static readonly TemplateParameterSchema Night = Freeze(new("night-ops", "夜航", BasicCapability, []));
    private static readonly TemplateParameterSchema Basic = Freeze(new("basic-box", "基础收纳", BasicCapability, []));

    public static TemplateParameterSchema? ForTemplate(string? id) => id switch
    { "lamp" => Lamp, "night-ops" => Night, "basic-box" => Basic, _ => null };

    public static TemplateParameterSchema Freeze(TemplateParameterSchema schema)
    {
        if (Validate(schema) is { } error) throw new ArgumentException(error);
        return schema with { Properties = Array.AsReadOnly(schema.Properties.ToArray()) };
    }

    public static string? Validate(TemplateParameterSchema? schema)
    {
        if (schema is null || !Key(schema.TemplateId) || !Text(schema.Name, 40) || schema.SchemaVersion != 1 ||
            schema.TemplateVersion is < 1 or > 999 || schema.RendererCapability is not (LampCapability or BasicCapability) ||
            schema.Properties is null || schema.Properties.Count > MaximumProperties) return "Unknown or invalid built-in parameter description.";
        var keys = new HashSet<string>(StringComparer.Ordinal); var bindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in schema.Properties)
        {
            if (property is null || !Key(property.Key) || !keys.Add(property.Key) || !bindings.Add(property.Binding) ||
                !Text(property.Label, 30) || !Text(property.Group, 20) || !OptionalText(property.Hint, 160) || !OptionalText(property.Unit, 8))
                return "Invalid or duplicate parameter identity, binding or text.";
            var expected = BindingKind(schema.RendererCapability, property.Binding);
            if (expected is null || expected != property.Kind) return "Unknown capability binding or mismatched control type.";
            if (property.Kind == TemplateParameterKind.Range)
            {
                if (property.Minimum is not { } min || property.Maximum is not { } max || property.Step is not { } step ||
                    !double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(step) || min < 0 || max > 100 || min >= max ||
                    step <= 0 || step > max - min || (max - min) / step > 1000 || !OnStep(max, min, step))
                    return "Parameter range exceeds the supported paint capability.";
            }
            else if (property.Minimum is not null || property.Maximum is not null || property.Step is not null || property.Unit.Length != 0)
                return "Color parameters cannot declare numeric ranges or units.";
            if (!Accepts(property, property.DefaultValue) || property.Kind == TemplateParameterKind.Range &&
                !OnStep(property.DefaultValue.Number!.Value, property.Minimum!.Value, property.Step!.Value)) return "Invalid parameter default.";
        }
        return null;
    }

    public static TemplateParameterKind? BindingKind(string capability, string binding) => capability is LampCapability or BasicCapability ? binding switch
    {
        "surface.background" or "item.color" => TemplateParameterKind.Color,
        "surface.opacity" => TemplateParameterKind.Range,
        "lamp.color" when capability == LampCapability => TemplateParameterKind.Color,
        "lamp.softness" or "lamp.intensity" or "lamp.sway" or "lamp.speed" when capability == LampCapability => TemplateParameterKind.Range,
        "scene.motion" or "lamp.follow" when capability == LampCapability => TemplateParameterKind.Boolean,
        _ => null
    } : null;

    // Existing saved finite percentages need not lie on the UI slider step; reading them must not round author intent.
    public static bool Accepts(TemplateParameterDefinition property, TemplateParameterValue? value) => value is not null &&
        (property.Kind == TemplateParameterKind.Boolean ? value.Color is null && value.Number is null && value.Boolean is not null :
         property.Kind == TemplateParameterKind.Color ? value.Number is null && value.Boolean is null && value.Color is { Length: 7 } text && text[0] == '#' &&
            !text.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") :
            value.Color is null && value.Boolean is null && value.Number is { } number && double.IsFinite(number) && number >= property.Minimum && number <= property.Maximum);
    public static IReadOnlyDictionary<string, TemplateParameterValue> Defaults(TemplateParameterSchema schema)
    {
        if (Validate(schema) is { } error) throw new ArgumentException(error);
        return new ReadOnlyDictionary<string, TemplateParameterValue>(schema.Properties.ToDictionary(p => p.Key, p => p.DefaultValue, StringComparer.Ordinal));
    }
    private static bool OnStep(double value, double min, double step) => Math.Abs((value - min) / step - Math.Round((value - min) / step)) < 1e-7;
    private static bool Text(string? value, int max) => value is { Length: > 0 } && value.Length <= max && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    private static bool OptionalText(string? value, int max) => value is not null && (value.Length == 0 || Text(value, max));
    private static bool Key(string? value) => value is { Length: > 0 and <= 48 } && value[0] is >= 'a' and <= 'z' &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && value is not ("constructor" or "prototype" or "__proto__");
}
