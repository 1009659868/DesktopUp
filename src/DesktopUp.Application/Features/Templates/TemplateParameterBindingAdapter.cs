using System.Collections.ObjectModel;
using System.Text.Json;
using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Paint-only adapter into the existing per-poster Lamp section. It owns no state.</summary>
public static class TemplateParameterBindingAdapter
{
    public static IReadOnlyDictionary<string, TemplateParameterValue> Read(TemplateParameterSchema schema, TemplateParameterValues value)
    {
        RequireSchema(schema); ArgumentNullException.ThrowIfNull(value);
        return new ReadOnlyDictionary<string, TemplateParameterValue>(schema.Properties.ToDictionary(p => p.Key, p => value.Get(p.Binding), StringComparer.Ordinal));
    }
    // v3/v4 caller adapter only. Forms and renderers use the generic overload.
    public static IReadOnlyDictionary<string, TemplateParameterValue> Read(TemplateParameterSchema schema, LampTemplateParameters value) =>
        Read(schema, LegacyLampValues.ToValues(value));
    public static string? ValidateValues(TemplateParameterSchema schema, IReadOnlyDictionary<string, TemplateParameterValue> values)
    {
        if (TemplateParameterSchemas.Validate(schema) is { } error) return error;
        if (values is null || values.Count != schema.Properties.Count || schema.Properties.Any(p =>
            !values.TryGetValue(p.Key, out var value) || !TemplateParameterSchemas.Accepts(p, value))) return "Parameter values do not match their description.";
        return null;
    }

    // Invalid in-progress text remains a protected draft; validation still gates preview/commit.
    public static TemplateParameterValues ComposeDraft(TemplateParameterSchema schema,
        IReadOnlyDictionary<string, TemplateParameterValue> values, TemplateParameterValues basis)
    {
        RequireSchema(schema); ArgumentNullException.ThrowIfNull(values); ArgumentNullException.ThrowIfNull(basis);
        if (values.Count != schema.Properties.Count || schema.Properties.Any(p => !values.ContainsKey(p.Key)))
            throw new ArgumentException("Every declared parameter and no unknown key is required.");
        var result = basis;
        foreach (var property in schema.Properties)
        {
            var value = values[property.Key];
            if (value is null || property.Kind == TemplateParameterKind.Color && (value.Color is null || value.Number is not null || value.Boolean is not null) ||
                property.Kind == TemplateParameterKind.Range && (value.Number is null || value.Color is not null || value.Boolean is not null) ||
                property.Kind == TemplateParameterKind.Boolean && (value.Boolean is null || value.Color is not null || value.Number is not null))
                throw new ArgumentException("Parameter value type does not match its binding.");
            result = result.Set(property.Binding, value);
        }
        return result;
    }
    public static TemplateParameterValues Reset(TemplateParameterSchema schema, TemplateParameterValues basis) =>
        ComposeDraft(schema, TemplateParameterSchemas.Defaults(schema), basis);
    public static LampTemplateParameters ComposeDraft(TemplateParameterSchema schema,
        IReadOnlyDictionary<string, TemplateParameterValue> values, LampTemplateParameters basis) =>
        LegacyLampValues.FromValues(ComposeDraft(schema, values, LegacyLampValues.ToValues(basis)));
    public static LampTemplateParameters Reset(TemplateParameterSchema schema, LampTemplateParameters basis) =>
        ComposeDraft(schema, TemplateParameterSchemas.Defaults(schema), basis);    private static void RequireSchema(TemplateParameterSchema schema)
    { if (TemplateParameterSchemas.Validate(schema) is { } error) throw new ArgumentException(error); }
}

public sealed record TemplateParameterRecipeView(string Title, string DefinitionJson, string InstanceJson,
    string CapabilitySummary, string InstanceSummary, string LimitsSummary, string AvailabilitySummary)
{
    public static TemplateParameterRecipeView Create(string templateKey, PosterPresentationParameters parameters)
    {
        var schema = TemplateParameterSchemas.ForTemplate(templateKey);
        var options = new JsonSerializerOptions { WriteIndented = true };
        if (schema is null) return new("模板参数说明", JsonSerializer.Serialize(new { templateKey, supported = false }, options), "{}",
            "当前模板没有已支持的参数声明。", "原配置已保留；此窗口不修改设置。", "未知能力不会执行。",
            "本地只读说明，AI 未连接；模板装入、留副本及版本替换尚未开放。");
        object Value(TemplateParameterValue value) => value.Color is { } color ? color : value.Boolean is { } boolean ? boolean : value.Number!.Value;
        string definition = TemplateRecipeCodec.Serialize(TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == templateKey));
        var values = TemplateParameterBindingAdapter.Read(schema, TemplateRecipeInstancePolicy.CurrentValues(parameters, templateKey));
        string instance = JsonSerializer.Serialize(values.ToDictionary(p => p.Key, p => Value(p.Value), StringComparer.Ordinal), options);
        return new(schema.Name + " · 参数配方", definition, instance,
            schema.RendererCapability == TemplateParameterSchemas.LampCapability
                ? "lamp@1：灯光、材质、鼠标跟随与闲置轻摆。"
                : "builtin.surface@1：内置海报表面；当前没有额外参数控件。",
            "模板默认值与当前海报设置分开保存。调参只修改当前海报，不改模板默认或其他海报；切换模板保留灯光设置。",
            $"仅支持已声明的颜色与数值控件、已知能力绑定，最多 {TemplateParameterSchemas.MaximumProperties} 项。颜色为 #RRGGBB，数值须在范围内；不改变项目排列和操作区域，不执行脚本或外部资源。",
            "可装入的新 ID 配方；不替换现有模板，不执行未知能力。");
    }
}
