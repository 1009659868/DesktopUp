using System.Globalization;

namespace DesktopUp.Domain;

public sealed record CanvasBehaviorPrefab(string Id, string Name, string Script, string Language = CanvasScript.Language);
public sealed record CanvasBehaviorBinding(string Id, string Source, string Event, string Prefab, string Target,
    string Condition = "always", bool Enabled = true);

public static class CanvasBehaviorPrefabs
{
    public static CanvasBehaviorPrefab Toggle { get; } = new("builtin-toggle", "收纳／展开 · 调用方法", "// 样例1：调用宿主提供的收纳／展开方法\nposter.toggle();");
    public static CanvasBehaviorPrefab ToggleExpanded { get; } = new("builtin-toggle-expanded", "收纳／展开 · 重写逻辑",
        "// 样例2：不用toggle，显式重写切换状态的判断\n// expand/collapse仍由宿主执行安全检查与窗口过渡\nif (collapsed) {\n    poster.expand();\n} else {\n    poster.collapse();\n}");
    public static IReadOnlyList<CanvasBehaviorPrefab> Templates { get; } = Array.AsReadOnly(new[] { Toggle, ToggleExpanded });
    public static string ToggleSource(CanvasDesign design) => design.Recipe.Nodes.Single(n => n.Type == "host.title@1").Id + "/toggle";
    public static IReadOnlyList<string> Events { get; } = Array.AsReadOnly(new[] { "pointer.click", "pointer.enter", "pointer.leave", "poster.expanded", "poster.collapsed" });
    public static bool CanBind(CanvasNode node) => CanvasBehaviorPolicy.IsDecoration(node) || node.Type == "layout.group@1";
    public static CanvasBehavior ForEditing(CanvasDesign design)
    {
        var old = design.Recipe.Behavior ?? new();
        if (old.Prefabs is not null) return old;
        // Templates are offered by the editor without occupying every work's budget.
        // Preserve room for all 32 legacy rules plus the default host binding.
        var prefabs = new List<CanvasBehaviorPrefab> { Toggle };
        var bindings = new List<CanvasBehaviorBinding>();
        if (design.Recipe.Nodes.Any(n => n.Type == "host.title@1")) bindings.Add(new("default-toggle", ToggleSource(design), "pointer.click", Toggle.Id, "poster"));
        foreach (var rule in old.Rules ?? [])
        {
            string id = "legacy-" + prefabs.Count;
            string script = $"animate(\"{rule.Action}\", {rule.Value.ToString("R", CultureInfo.InvariantCulture)}, {rule.DurationMs}, \"{rule.Easing}\");";
            prefabs.Add(new(id, "旧规则 " + prefabs.Count, script));
            bindings.Add(new("binding-" + bindings.Count, rule.Source, rule.Event, id, rule.Target));
        }
        return old with { Rules = null, Prefabs = prefabs, Bindings = bindings };
    }
    public static void Validate(CanvasBehavior behavior, IReadOnlyList<CanvasNode> nodes)
    {
        if (behavior.Prefabs is null && behavior.Bindings is null) return;
        if (behavior.Prefabs is null || behavior.Bindings is null || behavior.Prefabs.Count > 33 || behavior.Bindings.Count > 64 || (behavior.Rules?.Count ?? 0) != 0)
            throw new ArgumentException("行为预制体或绑定超过预算，不能与旧规则混用。");
        var programs = new Dictionary<string, CanvasProgram>(StringComparer.Ordinal);
        foreach (var prefab in behavior.Prefabs)
        {
            if (prefab is null || !Key(prefab.Id) || !programs.TryAdd(prefab.Id, ValidatePrefab(prefab))) throw new ArgumentException("重复或无效的行为预制体。");
        }
        var title = nodes.SingleOrDefault(n => n.Type == "host.title@1")?.Id;
        var ids = new HashSet<string>();
        bool Source(string id) => id == "poster" || id == title + "/toggle" || nodes.Any(n => n.Id == id && CanBind(n));
        bool Target(string id) => nodes.Any(n => n.Id == id && CanvasBehaviorPolicy.IsDecoration(n)) && !nodes.Any(n => n.Parent == id);
        foreach (var binding in behavior.Bindings)
        {
            if (binding is null || !Key(binding.Id) || !ids.Add(binding.Id) || !Source(binding.Source) || !Events.Contains(binding.Event) ||
                !programs.TryGetValue(binding.Prefab, out var program) || binding.Condition is not ("always" or "expanded" or "collapsed") ||
                binding.Event.StartsWith("poster.", StringComparison.Ordinal) && binding.Source != "poster" ||
                program.ChangesDecoration && !Target(binding.Target) || !program.ChangesDecoration && binding.Target != "poster" && !Target(binding.Target) ||
                program.ChangesPoster && binding.Event != "pointer.click")
                throw new ArgumentException("无效行为绑定：收展仅由点击触发；视觉动作只能作用于装饰叶节点。");
        }
    }
    public static CanvasProgram ValidatePrefab(CanvasBehaviorPrefab prefab)
    {
        if (prefab is null || !Key(prefab.Id) || string.IsNullOrWhiteSpace(prefab.Name) || prefab.Name.Length > 64 || prefab.Name.Any(char.IsControl) || prefab.Language != CanvasScript.Language)
            throw new ArgumentException("行为预制体名称、ID或语言版本无效。");
        return CanvasScript.Compile(prefab.Script);
    }
    public static void RequireUnlockedEdit(CanvasDesign design, CanvasBehavior? next)
    {
        var locked = CanvasLayerState.Resolve(design).Locked;
        string Protected(CanvasBehavior? value)
        {
            bool IsLocked(string id) => locked.Contains(id) || id.EndsWith("/toggle", StringComparison.Ordinal) && locked.Contains(id[..^7]);
            var bindings = (value?.Bindings ?? []).Where(b => IsLocked(b.Target) || IsLocked(b.Source)).ToArray();
            var ids = bindings.Select(b => b.Prefab).ToHashSet();
            return CanvasDeclaration.Serialize(new { Rules = (value?.Rules ?? []).Where(r => IsLocked(r.Target) || IsLocked(r.Source)).ToArray(),
                Bindings = bindings, Prefabs = (value?.Prefabs ?? []).Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id).ToArray() });
        }
        if (Protected(next) != Protected(design.Recipe.Behavior)) throw new ArgumentException("请先解锁相关图层再修改其行为。");
    }
    private static bool Key(string? id) => id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
