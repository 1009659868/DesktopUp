namespace DesktopUp.Domain;

// Versioned design data. Scripts use the closed CanvasScript grammar, never native code.
public sealed record CanvasBehavior(string CollapsedMode = "inherit", string CollapsedColor = "#282B30",
    double CollapsedOpacity = .96, IReadOnlyList<CanvasEventRule>? Rules = null,
    IReadOnlyList<CanvasBehaviorPrefab>? Prefabs = null, IReadOnlyList<CanvasBehaviorBinding>? Bindings = null);
public sealed record CanvasEventRule(string Id, string Event, string Source, string Target, string Action,
    double Value, int DurationMs = 180, string Easing = "smooth");
public sealed record CanvasStatePaint(string Color, double Opacity);

public static class CanvasBehaviorPolicy
{
    public static readonly IReadOnlyList<string> Events = Array.AsReadOnly(new[] { "poster.expanded", "pointer.enter", "pointer.leave", "pointer.click" });
    public static readonly IReadOnlyList<string> Actions = Array.AsReadOnly(new[] { "opacity", "offsetX", "offsetY", "rotation" });
    public static bool IsDecoration(CanvasNode n) => n.Type?.StartsWith("draw.", StringComparison.Ordinal) == true;
    public static void Validate(CanvasBehavior? behavior, IReadOnlyList<CanvasNode> nodes)
    {
        if (behavior is null) return;
        if (behavior.CollapsedMode is not ("inherit" or "custom") || !double.IsFinite(behavior.CollapsedOpacity) ||
            behavior.CollapsedOpacity is < 0 or > 1 || !CanvasDeclaration.Accepts("color", new(Text: behavior.CollapsedColor)))
            throw new ArgumentException("收起外观不合法。");
        var rules = behavior.Rules ?? [];
        CanvasBehaviorPrefabs.Validate(behavior, nodes);
        if (nodes.Any(n => n?.Id == "poster")) throw new ArgumentException("poster为事件源保留名称，请使用其他节点ID。");
        if (rules.Count > 32) throw new ArgumentException("每张画板最多32条事件规则。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        bool Leaf(string id) => nodes.Any(n => n is not null && n.Id == id && IsDecoration(n)) && !nodes.Any(n => n?.Parent == id);
        foreach (var rule in rules)
        {
            if (rule is null || rule.Id is not { Length: > 0 and <= 64 } || !ids.Add(rule.Id) ||
                !Events.Contains(rule.Event) || !Actions.Contains(rule.Action) || !Leaf(rule.Target) ||
                rule.Source != "poster" && !Leaf(rule.Source) || rule.Event == "poster.expanded" && rule.Source != "poster" ||
                !double.IsFinite(rule.Value) || rule.DurationMs is < 0 or > 2000 || rule.Easing is not ("linear" or "smooth") ||
                (rule.Action == "opacity" ? rule.Value is < 0 or > 1 : Math.Abs(rule.Value) > (rule.Action == "rotation" ? 180 : 256)))
                throw new ArgumentException("事件／动作不受支持或超出预算；只允许操作装饰叶节点。");
        }
    }

    public static CanvasStatePaint Collapsed(CanvasDesign design, string fallbackColor, double fallbackOpacity)
    {
        var behavior = design.Recipe.Behavior ?? new();
        if (behavior.CollapsedMode == "custom") return new(behavior.CollapsedColor, behavior.CollapsedOpacity);
        var hidden = CanvasLayerState.Resolve(design).Hidden;
        var plate = design.Recipe.Nodes.FirstOrDefault(n => n.Type == "draw.panel@1" &&
            n.Parent == design.Recipe.Nodes[0].Id && n.Box == new CanvasBox() && !hidden.Contains(n.Id));
        return plate is null ? new(fallbackColor, fallbackOpacity) :
            new(CanvasDeclaration.Property(design, plate, "fill").Text!, CanvasDeclaration.Property(design, plate, "opacity").Number!.Value);
    }
}
