using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace DesktopUp.Domain;

public sealed record TemplateRecipeBase(string Background, string Card, bool Heading);
public sealed record TemplateRecipe(TemplateParameterSchema Schema, string Family, TemplateRecipeBase Base)
{
    public string Id => Schema.TemplateId;
    public string Name => Schema.Name;
    public string EffectiveTemplateKey => Schema.RendererCapability == TemplateParameterSchemas.LampCapability
        ? "lamp" : Base.Heading ? "night-ops" : "basic-box";
}
public sealed record TemplateRecipeInstance(TemplateRecipe Recipe, LampTemplateParameters Parameters);

/// <summary>Bounded declarative recipes. No paths, resources, executable text or plug-ins.</summary>
public static class TemplateRecipeCodec
{
    public const string Format = "desktopup.template";
    public const int MaximumBytes = 65536;
    public static TemplateRecipe Parse(string source)
    {
        if (source is null || Encoding.UTF8.GetByteCount(source) > MaximumBytes)
            throw new ArgumentException("模板配方不得超过 64 KiB。");
        try
        {
            using var document = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 12 });
            return Parse(document.RootElement);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or OverflowException)
        { throw new ArgumentException("配方 JSON 缺少字段、类型错误或格式无效。", e); }
    }
    internal static TemplateRecipe Parse(JsonElement root)
    {
        Members(root, "format", "schemaVersion", "id", "version", "name", "family", "renderer", "base", "properties");
        if (Text(root, "format") != Format || Integer(root, "schemaVersion") != 1)
            throw new ArgumentException("不支持的模板格式或 schemaVersion。");
        string capability = Text(root, "renderer") switch
        {
            "lamp@1" => TemplateParameterSchemas.LampCapability,
            "minimal@1" => TemplateParameterSchemas.BasicCapability,
            _ => throw new ArgumentException("未知渲染能力。")
        };
        var basis = root.GetProperty("base"); Members(basis, "background", "card", "heading");
        if (basis.GetProperty("heading").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException("heading 必须为布尔值。");
        var parameters = root.GetProperty("properties");
        if (parameters.ValueKind != JsonValueKind.Array || parameters.GetArrayLength() > TemplateParameterSchemas.MaximumProperties)
            throw new ArgumentException("参数声明最多 20 项。");
        var definitions = new List<TemplateParameterDefinition>();
        foreach (var property in parameters.EnumerateArray())
        {
            Members(property, "key", "bind", "type", "label", "group", "default", "description", "unit", "min", "max", "step");
            var kind = Text(property, "type") switch
            {
                "color" => TemplateParameterKind.Color, "range" => TemplateParameterKind.Range,
                "boolean" => TemplateParameterKind.Boolean, _ => throw new ArgumentException("不支持的参数控件。")
            };
            var value = property.GetProperty("default");
            TemplateParameterValue parsed = kind switch
            {
                TemplateParameterKind.Color when value.ValueKind == JsonValueKind.String => TemplateParameterValue.FromColor(value.GetString()!),
                TemplateParameterKind.Range when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) => TemplateParameterValue.FromNumber(n),
                TemplateParameterKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => TemplateParameterValue.FromBoolean(value.GetBoolean()),
                _ => throw new ArgumentException("参数默认值类型错误。")
            };
            definitions.Add(new(Text(property, "key"), Text(property, "bind"), kind, Text(property, "label"),
                Optional(property, "group", "专属设置"), parsed, Optional(property, "description", ""), Optional(property, "unit", ""),
                Number(property, "min"), Number(property, "max"), Number(property, "step")));
        }
        var recipe = new TemplateRecipe(new(Text(root, "id"), Text(root, "name"), capability, definitions,
            Integer(root, "schemaVersion"), Integer(root, "version")), Text(root, "family"),
            new(Text(basis, "background"), Text(basis, "card"), basis.GetProperty("heading").GetBoolean()));
        return Freeze(recipe);
    }
    public static TemplateRecipe Freeze(TemplateRecipe recipe)
    {
        if (recipe is null || recipe.Base is null || !Color(recipe.Base.Background) || !Color(recipe.Base.Card) ||
            string.IsNullOrWhiteSpace(recipe.Family) || recipe.Family.Length > 24 || recipe.Family.Any(char.IsControl))
            throw new ArgumentException("配方材质或分类无效。");
        return recipe with { Schema = TemplateParameterSchemas.Freeze(recipe.Schema) };
    }
    public static string Serialize(TemplateRecipe recipe)
    {
        recipe = Freeze(recipe);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject(); writer.WriteString("format", Format); writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("id", recipe.Id); writer.WriteNumber("version", recipe.Schema.TemplateVersion);
            writer.WriteString("name", recipe.Name); writer.WriteString("family", recipe.Family);
            writer.WriteString("renderer", recipe.Schema.RendererCapability == TemplateParameterSchemas.LampCapability ? "lamp@1" : "minimal@1");
            writer.WritePropertyName("base"); writer.WriteStartObject();
            writer.WriteString("background", recipe.Base.Background); writer.WriteString("card", recipe.Base.Card);
            writer.WriteBoolean("heading", recipe.Base.Heading); writer.WriteEndObject();
            writer.WritePropertyName("properties"); writer.WriteStartArray();
            foreach (var p in recipe.Schema.Properties)
            {
                writer.WriteStartObject(); writer.WriteString("key", p.Key); writer.WriteString("bind", p.Binding);
                writer.WriteString("type", p.Kind.ToString().ToLowerInvariant()); writer.WriteString("label", p.Label); writer.WriteString("group", p.Group);
                writer.WritePropertyName("default"); WriteValue(writer, p.DefaultValue);
                if (p.Hint.Length > 0) writer.WriteString("description", p.Hint);
                if (p.Unit.Length > 0) writer.WriteString("unit", p.Unit);
                if (p.Minimum is { } min) writer.WriteNumber("min", min);
                if (p.Maximum is { } max) writer.WriteNumber("max", max);
                if (p.Step is { } step) writer.WriteNumber("step", step);
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        if (output.Length > MaximumBytes) throw new ArgumentException("模板配方不得超过 64 KiB。");
        return Encoding.UTF8.GetString(output.ToArray());
    }
    public static void WriteValue(Utf8JsonWriter writer, TemplateParameterValue value)
    {
        if (value.Color is { } color) writer.WriteStringValue(color);
        else if (value.Boolean is { } boolean) writer.WriteBooleanValue(boolean);
        else writer.WriteNumberValue(value.Number!.Value);
    }
    internal static void Members(JsonElement node, params string[] allowed)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new ArgumentException("配方章节必须是对象。");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in node.EnumerateObject())
            if (!seen.Add(p.Name) || !allowed.Contains(p.Name, StringComparer.Ordinal)) throw new ArgumentException("未知或重复字段：" + p.Name);
    }
    private static string Text(JsonElement node, string key) => node.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
        ? p.GetString()! : throw new ArgumentException("缺少或无效的文字字段：" + key);
    private static string Optional(JsonElement node, string key, string fallback) => node.TryGetProperty(key, out _) ? Text(node, key) : fallback;
    private static int Integer(JsonElement node, string key) => node.TryGetProperty(key, out var p) && p.TryGetInt32(out int i)
        ? i : throw new ArgumentException("无效的整数字段：" + key);
    private static double? Number(JsonElement node, string key) => !node.TryGetProperty(key, out var p) ? null :
        p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out double n) && double.IsFinite(n) ? n : throw new ArgumentException("无效的数值字段：" + key);
    private static bool Color(string value) => value is { Length: 7 } && value[0] == '#' && !value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF");
}

public static class TemplateRecipeCatalog
{
    public const int MaximumTemplates = 64;
    public const int MaximumCatalogBytes = 2 * 1024 * 1024;
    public static IReadOnlyList<TemplateRecipe> BuiltIns => Array.AsReadOnly(new[]
    {
        new TemplateRecipe(TemplateParameterSchemas.ForTemplate("night-ops")!, "极简主义", new("#202E2A", "#2D433A", true)),
        new TemplateRecipe(TemplateParameterSchemas.ForTemplate("lamp")!, "交互光影", new("#17221E", "#354237", true)),
        new TemplateRecipe(TemplateParameterSchemas.ForTemplate("basic-box")!, "极简主义", new("#282B30", "#456078", false))
    });
    public static IReadOnlyList<TemplateRecipe> Read(string source)
    {
        if (source is null || Encoding.UTF8.GetByteCount(source) > MaximumCatalogBytes) throw new ArgumentException("模板册过大。");
        try
        {
            using var doc = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 14 });
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() + BuiltIns.Count > MaximumTemplates)
                throw new ArgumentException("模板册数量超过上限。");
            var ids = BuiltIns.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            var result = new List<TemplateRecipe>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var recipe = TemplateRecipeCodec.Parse(row.GetRawText());
                if (!ids.Add(recipe.Id)) throw new ArgumentException("模板 ID 已存在；请为副本使用新的 ID。");
                result.Add(recipe);
            }
            return result.AsReadOnly();
        }
        catch (JsonException e) { throw new ArgumentException("模板册 JSON 无效。", e); }
    }
    public static IReadOnlyList<TemplateRecipe> All(string source) => Array.AsReadOnly(BuiltIns.Concat(Read(source)).ToArray());
    public static string Add(string source, TemplateRecipe recipe)
    {
        var list = Read(source).ToList(); list.Add(TemplateRecipeCodec.Freeze(recipe));
        string result = "[" + string.Join(",", list.Select(TemplateRecipeCodec.Serialize)) + "]";
        Read(result); return result;
    }
}

public static class TemplateRecipeInstancePolicy
{
    public static LampTemplateParameters Current(PosterPresentationParameters parameters, string templateId)
    {
        if (parameters.RecipeInstances?.TryGetValue(templateId, out var instance) == true) return instance.Parameters;
        if (templateId == "lamp") return parameters.Lamp ?? LampTemplatePolicy.Defaults;
        var builtIn = TemplateRecipeCatalog.BuiltIns.FirstOrDefault(r => r.Id == templateId);
        return builtIn is null ? LampTemplatePolicy.Defaults : Defaults(builtIn) with { Background = parameters.Theme.Background };
    }
    public static LampTemplateParameters Defaults(TemplateRecipe recipe)
    {
        recipe = TemplateRecipeCodec.Freeze(recipe);
        var result = LampTemplatePolicy.Defaults with { Background = recipe.Base.Background, Card = recipe.Base.Card };
        foreach (var p in recipe.Schema.Properties) result = Set(result, p.Binding, p.DefaultValue);
        return result;
    }
    public static LampTemplateParameters Set(LampTemplateParameters result, string binding, TemplateParameterValue value) => binding switch
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
    public static string? Validate(IReadOnlyDictionary<string, TemplateRecipeInstance>? instances)
    {
        if (instances is null) return null;
        if (instances.Count > TemplateRecipeCatalog.MaximumTemplates) return "模板实例超过上限。";
        foreach (var (key, instance) in instances)
        {
            if (instance is null || instance.Recipe is null || instance.Parameters is null || key != instance.Recipe.Id || PosterPresentationPolicy.IsBuiltIn(key)) return "模板实例身份无效。";
            try { TemplateRecipeCodec.Freeze(instance.Recipe); }
            catch (ArgumentException e) { return e.Message; }
            if (LampTemplatePolicy.Validate(instance.Parameters) is { } error) return error;
            if (instance.Recipe.Schema.Properties.Any(p => !TemplateParameterSchemas.Accepts(p, Get(instance.Parameters, p.Binding))))
                return "实例参数不符合配方声明。";
            var represented = Defaults(instance.Recipe);
            foreach (var p in instance.Recipe.Schema.Properties) represented = Set(represented, p.Binding, Get(instance.Parameters, p.Binding));
            if (represented != instance.Parameters) return "实例包含配方未声明的覆盖。";
        }
        return null;
    }
    public static PosterPresentationParameters Select(PosterPresentationParameters parameters, TemplateRecipe recipe)
    {
        recipe = TemplateRecipeCodec.Freeze(recipe);
        if (PosterPresentationPolicy.IsBuiltIn(recipe.Id)) return parameters;
        var instances = parameters.RecipeInstances?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        if (!instances.ContainsKey(recipe.Id)) instances.Add(recipe.Id, new(recipe, Defaults(recipe)));
        return parameters with { RecipeInstances = new ReadOnlyDictionary<string, TemplateRecipeInstance>(instances) };
    }
    public static PosterPresentationParameters Update(PosterPresentationParameters parameters, string templateId, LampTemplateParameters value)
    {
        if (templateId == "lamp") return parameters with { Lamp = value };
        if (parameters.RecipeInstances is not { } prior || !prior.TryGetValue(templateId, out var existing)) throw new ArgumentException("所选模板实例不存在。");
        var instances = prior.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        instances[templateId] = existing with { Parameters = value };
        var result = parameters with { RecipeInstances = new ReadOnlyDictionary<string, TemplateRecipeInstance>(instances) };
        if (Validate(result.RecipeInstances) is { } error) throw new ArgumentException(error);
        return result;
    }
}
