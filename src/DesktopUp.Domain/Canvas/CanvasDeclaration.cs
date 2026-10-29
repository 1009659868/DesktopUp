using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace DesktopUp.Domain;

// Design data only. Resource IDs are hashes, never source paths or platform objects.
public sealed record CanvasValue(string? Text = null, double? Number = null, bool? Boolean = null, string? Parameter = null);
public sealed record CanvasBox(double X = 0, double Y = 0, double Width = 1, double Height = 1);
public sealed record CanvasArtboard(double Width, double Height);
public sealed record CanvasSurface(string Shape = "rectangle", double Radius = 24);
public sealed record CanvasTextStyle(string Family = "sans", double LetterSpacing = 0);
public sealed record CanvasParameter(string Key, string Label, string Group, string Kind, CanvasValue Default,
    double Minimum = 0, double Maximum = 1, int MaxLength = 120);
public sealed record CanvasNode(string Id, string? Parent, string Type, CanvasBox Box,
    IReadOnlyDictionary<string, CanvasValue> Properties, double Rotation = 0, string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Hidden = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Locked = false,
    CanvasTextStyle? Typography = null);
public sealed record CanvasRecipe(string Id, int Version, string Name, IReadOnlyList<CanvasNode> Nodes,
    IReadOnlyList<CanvasParameter> Parameters, [property: JsonRequired] string Format = CanvasDeclaration.Format,
    [property: JsonRequired] int ProtocolVersion = 1, string? ContentForeground = null, CanvasArtboard? Artboard = null,
    CanvasSurface? Surface = null, CanvasBehavior? Behavior = null);
public sealed record CanvasDesign(CanvasRecipe Recipe, IReadOnlyDictionary<string, CanvasValue> Overrides);
public sealed record CanvasDesignState(long Revision, CanvasDesign? Current, CanvasDesign? Previous,
    bool CanUndo, string RequestId, CanvasArtboard? PreviousFrame = null);
public sealed record CanvasProperty(string Kind, double Minimum = 0, double Maximum = 1);
public sealed record CanvasCapability(string Id, bool Children, IReadOnlyDictionary<string, CanvasProperty> Properties);

/// <summary>The actual bounded runtime capability catalog, shared by validation and the editor.</summary>
public static class CanvasCapabilities
{
    public const string Revision = "canvas-j1-3.3";
    private static IReadOnlyDictionary<string, CanvasProperty> Props(params (string, CanvasProperty)[] values) =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, CanvasProperty>(values.ToDictionary(x => x.Item1, x => x.Item2));
    public static IReadOnlyList<CanvasCapability> All { get; } = Array.AsReadOnly(new CanvasCapability[]
    {
        new("layout.canvas@1", true, Props()),
        new("layout.group@1", true, Props()),
        new("draw.image@1", false, Props(("asset", new("asset")), ("opacity", new("number")))),
        new("layout.stack@1", true, Props(("padding", new("number", 0, 48)), ("gap", new("number", 0, 40)))),
        new("draw.panel@1", true, Props(("fill", new("color")), ("opacity", new("number")), ("radius", new("number", 0, 40)))),
        new("draw.ellipse@1", false, Props(("fill", new("color")), ("opacity", new("number")))),
        new("draw.text@1", false, Props(("text", new("string")), ("color", new("color")), ("fontSize", new("number", 10, 96)))),
        new("content.slot@1", false, Props()),
        new("host.title@1", false, Props(("text", new("string")), ("color", new("color")),
            ("actions", new("color")), ("fontSize", new("number", 10, 32)))),
        new("effect.rain@1", false, Props(("intensity", new("number"))))
    });
    public static CanvasCapability? Find(string id) => All.FirstOrDefault(x => x.Id == id);
}

public static class CanvasDeclaration
{
    public const string Format = "desktopup.canvas-recipe";
    public const int MaximumBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, MaxDepth = 16
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string source)
    {
        if (source is null || Encoding.UTF8.GetByteCount(source) > MaximumBytes * 3) throw new ArgumentException("画板数据超过预算。");
        try
        {
            using var doc = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 16 });
            Unique(doc.RootElement);
            return JsonSerializer.Deserialize<T>(source, Options) ?? throw new ArgumentException("画板数据为空。");
        }
        catch (JsonException e) { throw new ArgumentException("画板格式或字段不受支持，原数据保留。", e); }
    }
    public static CanvasDesign Freeze(CanvasDesign design)
    {
        // Copy only on authoring/commit boundaries. Rendering consumes this immutable copy.
        var copy = Read<CanvasDesign>(Serialize(design));
        Validate(copy);
        var recipe = copy.Recipe with
        {
            Nodes = Array.AsReadOnly(copy.Recipe.Nodes.Select(n => n with { Properties =
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, CanvasValue>(n.Properties.ToDictionary(x => x.Key, x => x.Value)) }).ToArray()),
            Parameters = Array.AsReadOnly(copy.Recipe.Parameters.ToArray()),
            Behavior = copy.Recipe.Behavior is { } behavior ? behavior with {
                Rules = behavior.Rules is null ? null : Array.AsReadOnly(behavior.Rules.ToArray()),
                Prefabs = behavior.Prefabs is null ? null : Array.AsReadOnly(behavior.Prefabs.ToArray()),
                Bindings = behavior.Bindings is null ? null : Array.AsReadOnly(behavior.Bindings.ToArray()) } : null
        };
        return copy with { Recipe = recipe, Overrides = new System.Collections.ObjectModel.ReadOnlyDictionary<string, CanvasValue>(copy.Overrides.ToDictionary(x => x.Key, x => x.Value)) };
    }
    public static string Digest(CanvasDesign design) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(design))));
    public static CanvasValue Resolve(CanvasDesign design, CanvasValue value) => value.Parameter is { } key
        ? design.Overrides.GetValueOrDefault(key) ?? design.Recipe.Parameters.First(p => p.Key == key).Default : value;
    public static CanvasValue Property(CanvasDesign design, CanvasNode node, string key) => Resolve(design, node.Properties[key]);
    public static void Validate(CanvasDesign design)
    {
        if (design?.Recipe is not { } recipe || design.Overrides is null || recipe.Nodes is null || recipe.Parameters is null ||
            recipe.Format != Format || recipe.ProtocolVersion is not (1 or 2 or 3) || !Key(recipe.Id) || recipe.Version < 1 ||
            !Text(recipe.Name, 64) || recipe.Nodes.Count is < 2 or > 128 || recipe.Parameters.Count > 64 ||
            Encoding.UTF8.GetByteCount(Serialize(recipe)) > MaximumBytes) throw new ArgumentException("不支持或不完整的画板声明。");
        if (recipe.Behavior is not null && recipe.ProtocolVersion < 3) throw new ArgumentException("状态行为需要整张画板协议。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (recipe.Surface is { } surface && (recipe.ProtocolVersion < 3 || recipe.Artboard is null ||
            surface.Shape is not ("rectangle" or "rounded" or "ellipse") || !In(surface.Radius, 0, 512)))
            throw new ArgumentException("海报外形或画板版本无效。");
        if (recipe.ProtocolVersion == 3 && (recipe.Surface is null || recipe.Nodes.Count(n => n?.Type == "host.title@1") != 1))
            throw new ArgumentException("画板海报需保留外形声明和唯一标题操作区。");
        if (recipe.Artboard is { } artboard && (recipe.ProtocolVersion < 2 || !In(artboard.Width, 240, 2048) || !In(artboard.Height, 240, 2048)))
            throw new ArgumentException("设计画板尺寸需为240～2048 DIP。");
        if (recipe.ContentForeground is { } foreground && (recipe.ProtocolVersion < 2 || !Accepts("color", new(Text: foreground))))
            throw new ArgumentException("内容文字颜色不合法。");
        var parameters = new Dictionary<string, CanvasParameter>(StringComparer.Ordinal);
        foreach (var p in recipe.Parameters)
        {
            if (p is null || !Key(p.Key) || !Text(p.Label, 64) || !Text(p.Group, 32) || !parameters.TryAdd(p.Key, p) ||
                !double.IsFinite(p.Minimum) || !double.IsFinite(p.Maximum) || p.Minimum > p.Maximum || p.MaxLength is < 1 or > 120 ||
                !Accepts(p.Kind, p.Default, p.Minimum, p.Maximum, p.MaxLength)) throw new ArgumentException("画板参数声明无效。");
        }
        foreach (var (key, value) in design.Overrides)
            if (!parameters.TryGetValue(key, out var p) || !Accepts(p.Kind, value, p.Minimum, p.Maximum, p.MaxLength))
                throw new ArgumentException("实例覆盖不符合参数声明：" + key);
        int slots = 0;
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, CanvasNode>(StringComparer.Ordinal);
        foreach (var n in recipe.Nodes)
        {
            if (n is null || !Key(n.Id) || !ids.Add(n.Id) || n.Properties is null || n.Box is not { } b ||
                !In(b.X, 0, 1) || !In(b.Y, 0, 1) || !In(b.Width, .01, 1) || !In(b.Height, .01, 1) ||
                b.X + b.Width > 1.000001 || b.Y + b.Height > 1.000001 || CanvasCapabilities.Find(n.Type) is not { } capability)
                throw new ArgumentException("画板节点或能力无效。");
            if (n.Parent is null)
            { if (nodes.Count != 0 || n.Type != "layout.canvas@1" || b != new CanvasBox()) throw new ArgumentException("画板必须有且仅有一个根节点。"); depths[n.Id] = 0; }
            else
            {
                if (!nodes.TryGetValue(n.Parent, out var parent) || CanvasCapabilities.Find(parent.Type)?.Children != true || depths[n.Parent] >= 15)
                    throw new ArgumentException("节点父级必须先声明，且支持子节点。");
                depths[n.Id] = depths[n.Parent] + 1;
            }
            nodes.Add(n.Id, n);
            if (n.Typography is { } typography && (recipe.ProtocolVersion < 2 || n.Type != "draw.text@1" ||
                typography.Family is not ("sans" or "serif" or "mono") || !In(typography.LetterSpacing, 0, 8)))
                throw new ArgumentException("文字样式不受支持。");
            if (n.Parent is null && n.Hidden || recipe.ProtocolVersion == 1 && (n.Hidden || n.Locked))
                throw new ArgumentException("根画板不能隐藏，图层状态需使用新版声明。");
            if (!In(n.Rotation, -180, 180) || n.Name is not null && !Text(n.Name, 64) ||
                n.Rotation != 0 && (n.Type is "layout.canvas@1" or "layout.stack@1" or "content.slot@1" or "host.title@1") ||
                recipe.ProtocolVersion == 1 && (n.Rotation != 0 || n.Type is "layout.group@1" or "draw.image@1"))
                throw new ArgumentException("节点变换或版本不受支持。真实内容保持轴对齐，装饰与组合可旋转。");
            if (n.Type == "host.title@1" && (recipe.ProtocolVersion < 3 || n.Parent != recipe.Nodes[0].Id || n.Hidden))
                throw new ArgumentException("标题操作区保持根画板坐标且不能隐藏；可清空标题文字。");
            if (n.Properties.Count != capability.Properties.Count) throw new ArgumentException("节点属性缺失或多余。");
            foreach (var (key, value) in n.Properties)
            {
                if (!capability.Properties.TryGetValue(key, out var p)) throw new ArgumentException("未知节点属性。");
                if (value?.Parameter is { } binding)
                {
                    if (value.Text is not null || value.Number is not null || value.Boolean is not null || !parameters.TryGetValue(binding, out var parameter) ||
                        parameter.Kind != p.Kind || p.Kind == "number" && (parameter.Minimum < p.Minimum || parameter.Maximum > p.Maximum))
                        throw new ArgumentException("参数绑定类型或范围不符。");
                }
                if (value is null || !Accepts(p.Kind, Resolve(design, value), p.Minimum, p.Maximum, 120)) throw new ArgumentException("节点值不合法。");
            }
            if (n.Type == "content.slot@1")
            {
                slots++;
                if (n.Hidden) throw new ArgumentException("真实内容区不能隐藏。");
                if (recipe.ProtocolVersion == 1 && n.Id != "members") throw new ArgumentException("旧版内容槽必须绑定members。");
                for (var parent = n.Parent; parent is not null; parent = nodes[parent].Parent)
                    if (nodes[parent].Rotation != 0 || nodes[parent].Hidden) throw new ArgumentException("真实内容的祖先不可旋转或隐藏。");
            }
        }
        if (slots != 1) throw new ArgumentException("必须保留一个且仅一个内容槽。");
        CanvasBehaviorPolicy.Validate(recipe.Behavior, recipe.Nodes);
        if (recipe.Nodes.Where(n => n.Type == "draw.image@1").Select(n => Property(design, n, "asset").Text).Distinct().Count() > 8)
            throw new ArgumentException("单个作品最多使用 8 份不同图片，重复实例共享资源。");
        // Stacks are measured flow containers. Only text plus one final content slot are supported.
        foreach (var stack in nodes.Values.Where(n => n.Type == "layout.stack@1"))
        {
            var children = nodes.Values.Where(n => n.Parent == stack.Id).ToArray();
            if (children.Length == 0 || children[^1].Type != "content.slot@1" || children[..^1].Any(n => n.Type != "draw.text@1"))
                throw new ArgumentException("流式布局只支持文字后接内容槽。");
        }
    }
    public static void ValidateState(CanvasDesignState state)
    {
        if (state.Revision < 1 || !Text(state.RequestId, 128)) throw new ArgumentException("画板版本无效。");
        if (state.Current is { } current) Validate(current);
        if (state.Previous is { } previous) Validate(previous);
        if (state.PreviousFrame is { } frame && (!In(frame.Width, 1, 16384) || !In(frame.Height, 1, 16384)))
            throw new ArgumentException("撤销外框尺寸无效。");
    }
    public static bool Accepts(string kind, CanvasValue? value, double min = 0, double max = 1, int length = 120)
    {
        if (value is null || value.Parameter is not null || (value.Text is null ? 0 : 1) + (value.Number is null ? 0 : 1) + (value.Boolean is null ? 0 : 1) != 1) return false;
        return kind switch
        {
            "number" => value.Number is { } n && In(n, min, max),
            "boolean" => value.Boolean.HasValue,
            "string" => value.Text is { } s && s.Length <= length && !s.Any(c => char.IsControl(c) && c != '\n'),
            "color" => value.Text is { Length: 7 } color && color[0] == '#' && !color.AsSpan(1).ContainsAnyExcept("0123456789ABCDEFabcdef"),
            "asset" => value.Text is { Length: 64 } hash && !hash.AsSpan().ContainsAnyExcept("0123456789abcdef"),
            _ => false
        };
    }
    private static bool Key(string? s) => s is { Length: > 0 and <= 64 } && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool Text(string? s, int max) => !string.IsNullOrWhiteSpace(s) && s.Length <= max && !s.Any(char.IsControl);
    private static bool In(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
    private static void Unique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject()) { if (!seen.Add(p.Name)) throw new ArgumentException("重复JSON字段。"); Unique(p.Value); }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) Unique(item);
    }
}
