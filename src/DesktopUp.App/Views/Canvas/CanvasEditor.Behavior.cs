using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private Button behaviorLauncher = null!;
    private bool behaviorTree;
    private CanvasBehavior Behavior => document.Design.Recipe.Behavior ?? CanvasBehaviorPrefabs.ForEditing(document.Design);
    private static string EventLabel(string value) => value switch
    { "poster.expanded" => "展开完成", "poster.collapsed" => "收纳完成", "pointer.enter" => "指针进入", "pointer.leave" => "指针离开", _ => "单击" };
    private void ShowBehaviorEditor()
    {
        StopStudioRun(); behaviorTree = !behaviorTree;
        if (!behaviorTree) CloseScriptEditor();
        SetActive(behaviorLauncher, behaviorTree); RefreshLayers();
    }
    private void RefreshBehaviorTree()
    {
        layers.Children.Clear(); layerCount.Text = "行为";
        layers.Children.Add(Command("＋ 新建脚本行为", "", () => OpenScript(new(CanvasAuthoring.NewId(), "新行为", "// self是绑定中选择的装饰目标\nanimate(\"opacity\", 0.6, 180, \"smooth\");"))));
        layers.Children.Add(new TextBlock { Text = "样例模板 · 点击创建副本", FontSize = 11, Margin = new(3, 8, 3, 3), Foreground = Ink("#ADB1B0"), TextWrapping = TextWrapping.Wrap });
        foreach (var template in CanvasBehaviorPrefabs.Templates)
            layers.Children.Add(Command(template.Name, "", () => OpenScript(template with { Id = CanvasAuthoring.NewId() })));
        layers.Children.Add(new TextBlock { Text = "作品内预制体", FontSize = 11, Margin = new(3, 8, 3, 3), Foreground = Ink("#ADB1B0") });
        foreach (var prefab in Behavior.Prefabs ?? [])
        {
            var row = Command(prefab.Name, "", () => OpenScript(prefab));
            row.HorizontalAlignment = HorizontalAlignment.Stretch; row.HorizontalContentAlignment = HorizontalAlignment.Left;
            layers.Children.Add(row);
        }
        layers.Children.Add(new TextBlock { Text = "行为库 · 点击复制到作品", FontSize = 11, Margin = new(3, 12, 3, 3), Foreground = Ink("#ADB1B0"), TextWrapping = TextWrapping.Wrap });
        foreach (var prefab in workspace.Behaviors()) layers.Children.Add(Command(prefab.Name, "", () => OpenScript(prefab with { Id = CanvasAuthoring.NewId() })));
        layers.Children.Add(Command("返回图层", "layers", ShowBehaviorEditor));
    }
    private void BehaviorBindings(StackPanel panel, string source)
    {
        panel.Children.Add(new TextBlock { Text = source == CanvasBehaviorPrefabs.ToggleSource(document.Design) ? "v 按钮行为 · 按顺序执行" : "行为绑定 · 按顺序执行", FontSize = 12 });
        var behavior = Behavior;
        foreach (var binding in (behavior.Bindings ?? []).Where(b => b.Source == source))
        {
            var row = new StackPanel { Spacing = 5 };
            var enabled = new CheckBox { Content = Behavior.Prefabs!.Single(p => p.Id == binding.Prefab).Name + " · " + EventLabel(binding.Event), IsChecked = binding.Enabled, FontSize = 11 };
            enabled.Click += (_, _) => Edit(() => SetBehavior(Behavior with { Bindings = Behavior.Bindings!.Select(b => b.Id == binding.Id ? b with { Enabled = enabled.IsChecked == true } : b).ToArray() }));
            row.Children.Add(enabled);
            row.Children.Add(new TextBlock { Text = "条件: " + binding.Condition + " · self: " + (document.Design.Recipe.Nodes.FirstOrDefault(n => n.Id == binding.Target)?.Name ?? binding.Target), FontSize = 10, TextWrapping = TextWrapping.Wrap });
            var settings = new StackPanel { Spacing = 6 };
            void Option(string header, string value, IEnumerable<(string Id, string Text)> choices, Func<CanvasBehaviorBinding, string, CanvasBehaviorBinding> update)
            {
                var box = new ComboBox { Header = header, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 11 };
                foreach (var (id, text) in choices) box.Items.Add(new ComboBoxItem { Tag = id, Content = text });
                box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == value);
                box.SelectionChanged += (_, _) => {
                    if (!box.IsLoaded || box.SelectedItem is not ComboBoxItem chosen || (string)chosen.Tag == value) return;
                    Edit(() => SetBehavior(Behavior with { Bindings = Behavior.Bindings!.Select(b => b.Id == binding.Id ? update(b, (string)chosen.Tag) : b).ToArray() }));
                };
                settings.Children.Add(box);
            }
            Option("触发事件", binding.Event, CanvasBehaviorPrefabs.Events.Where(e => source == "poster" || !e.StartsWith("poster.", StringComparison.Ordinal)).Select(e => (e, EventLabel(e))), (b, value) => b with { Event = value });
            Option("触发条件", binding.Condition, [("always", "总是"), ("expanded", "仅展开时"), ("collapsed", "仅收纳时")], (b, value) => b with { Condition = value });
            Option("动作目标 self", binding.Target, BehaviorTargets(), (b, value) => b with { Target = value });
            row.Children.Add(new Expander { Header = "触发与目标", Content = settings, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
            ActionRow(row, ("↑", () => MoveBinding(binding, -1)), ("↓", () => MoveBinding(binding, 1)),
                ("代码", () => { behaviorTree = true; RefreshLayers(); OpenScript(Behavior.Prefabs!.Single(p => p.Id == binding.Prefab)); }),
                ("移除", () => Edit(() => SetBehavior(Behavior with { Bindings = Behavior.Bindings!.Where(b => b.Id != binding.Id).ToArray() }))));
            panel.Children.Add(row);
        }
        ComboBox Choice(string label, IEnumerable<(string Id, string Text)> entries)
        {
            var box = new ComboBox { Header = label, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0, FontSize = 11 };
            foreach (var (id, text) in entries) box.Items.Add(new ComboBoxItem { Tag = id, Content = text });
            if (box.Items.Count != 0) box.SelectedIndex = 0;
            panel.Children.Add(box); return box;
        }
        var prefabs = Choice("添加预制体", (behavior.Prefabs ?? []).Select(p => (p.Id, p.Name)));
        var events = Choice("触发事件", CanvasBehaviorPrefabs.Events.Where(e => source == "poster" || !e.StartsWith("poster.", StringComparison.Ordinal)).Select(e => (e, EventLabel(e))));
        var condition = Choice("触发条件", new[] { ("always", "总是"), ("expanded", "仅展开时"), ("collapsed", "仅收纳时") });
        var targets = Choice("动作目标 self", BehaviorTargets());
        string Selected(ComboBox box) => ((ComboBoxItem)box.SelectedItem).Tag as string ?? "";
        panel.Children.Add(Command("添加绑定", "plus", () => Edit(() => SetBehavior(Behavior with { Bindings = Behavior.Bindings!.Append(new(CanvasAuthoring.NewId(), source, Selected(events), Selected(prefabs), Selected(targets), Selected(condition))).ToArray() }))));
    }
    private IEnumerable<(string Id, string Text)> BehaviorTargets() => new[] { ("poster", "海报（仅收展）") }.Concat(
        document.Design.Recipe.Nodes.Where(n => CanvasBehaviorPolicy.IsDecoration(n) && !document.Design.Recipe.Nodes.Any(c => c.Parent == n.Id)).Select(n => (n.Id, n.Name ?? n.Id)));
    private void MoveBinding(CanvasBehaviorBinding binding, int offset) => Edit(() =>
    {
        var entries = Behavior.Bindings!.ToList(); int at = entries.FindIndex(b => b.Id == binding.Id);
        int next = at + offset;
        while (next >= 0 && next < entries.Count && entries[next].Source != binding.Source) next += offset;
        if (next < 0 || next >= entries.Count) return;
        (entries[at], entries[next]) = (entries[next], entries[at]); SetBehavior(Behavior with { Bindings = entries });
    });
}
