using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private Grid fields = null!;
    private int fieldCount;
    private void Properties()
    {
        if (rebuildingProperties || document is null) return;
        rebuildingProperties = true;
        try
        {
            inspector.Children.Clear(); inspector.Orientation = narrow ? Orientation.Horizontal : Orientation.Vertical;
            if (SelectedTitleControl is { } control) { TitleControlProperties(control); return; }
            var head = new StackPanel { Spacing = 6, Width = narrow ? 120 : double.NaN };
            var title = new TextBox { Text = selected.Count == 1 ? Current(selected.Single()).Name ?? Label(Current(selected.Single()).Type) : names[current],
                MaxLength = 64, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, BorderThickness = new(0), Background = null, Padding = new(0, 4, 0, 4), MinWidth = 0 };
            string? nodeId = selected.Count == 1 ? selected.Single() : null;
            title.LostFocus += (_, _) =>
            {
                if (rebuildingProperties || !title.IsLoaded || string.IsNullOrWhiteSpace(title.Text)) return;
                if (nodeId is null) { names[current] = title.Text; RefreshWorks(); artboardLabel.Text = names[current] + $" · {board.Width:0} × {board.Height:0}"; }
                else if (CanEditField(nodeId) && title.Text != Current(nodeId).Name) Edit(() => document.Replace(Current(nodeId) with { Name = title.Text }));
            };
            head.Children.Add(title);
            head.Children.Add(new TextBlock { Text = selected.Count == 0 ? "画布 · 设计草稿" : selected.Count > 1 ? $"已选择 {selected.Count} 个对象" : Label(Current(selected.Single()).Type),
                FontSize = 11, Foreground = Ink("#ADB1B0") });
            inspector.Children.Add(head);
            fields = new Grid { ColumnSpacing = 8, RowSpacing = 9, Width = narrow ? 280 : double.NaN };
            for (int i = 0; i < (narrow ? 4 : 2); i++) fields.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            fieldCount = 0; inspector.Children.Add(fields);
            var actions = new StackPanel { Spacing = 7, Width = narrow ? 200 : double.NaN };
            actions.Children.Add(new TextBlock { Text = "排列与复用", FontSize = 11, Foreground = Ink("#ADB1B0") }); inspector.Children.Add(actions);
            if (selected.Count == 0 || selected.Count == 1 && CanvasBehaviorPrefabs.CanBind(Current(selected.Single())))
            {
                var bindings = new StackPanel { Spacing = 8, Width = narrow ? 220 : double.NaN }; inspector.Children.Add(bindings);
                string source = selected.Count == 0 ? "poster" : selected.Single();
                BehaviorBindings(bindings, source);
            }
            if (selected.Count != 1)
            {
                if (selected.Count == 0)
                {
                    Number("画板宽", board.Width, 240, 2048, null, v => SetArtboardSize(v, board.Height));
                    Number("画板高", board.Height, 240, 2048, null, v => SetArtboardSize(board.Width, v));
                    SurfaceProperties();
                    CollapsedStyleProperties();
                }
                if (selected.Count > 1)
                {
                    ActionRow(actions, ("组合", GroupSelection), ("复制", Duplicate));
                    ActionRow(actions, ("存入组合库", SaveCombinationPrompt), ("删除", DeleteSelection));
                }
                else
                {
                    var label = new TextBlock { Text = "直接绘制，或从素材／组合库拖入。\n真实内容通过预览海报核对。", FontSize = 11, Foreground = Ink("#ADB1B0"), TextWrapping = TextWrapping.Wrap };
                    actions.Children.Add(label);
                    ActionRow(actions, ("撤销已应用设计", () => Try(() => { session!.Preview(undo: true); Commit(); })));
                }
                return;
            }
            var node = Current(selected.Single()); bool editable = !layerState.Locked.Contains(node.Id);
            if (node.Type == "host.title@1")
                actions.Children.Add(Command("编辑 v 按钮行为", "", () => SelectTitleControl(TitleControls(node).Single(c => c.Role == "toggle"))));
            var parent = geometry!.Nodes.First(p => p.Node.Id == node.Parent).Bounds;
            Number("X", node.Box.X * parent.Width, 0, parent.Width, node.Id, v => Box(node.Id, b => b with { X = v / parent.Width }));
            Number("Y", node.Box.Y * parent.Height, 0, parent.Height, node.Id, v => Box(node.Id, b => b with { Y = v / parent.Height }));
            Number("宽", node.Box.Width * parent.Width, 1, parent.Width, node.Id, v => Box(node.Id, b => b with { Width = v / parent.Width }));
            Number("高", node.Box.Height * parent.Height, 1, parent.Height, node.Id, v => Box(node.Id, b => b with { Height = v / parent.Height }));
            if (node.Type.StartsWith("draw.", StringComparison.Ordinal) || node.Type == "layout.group@1")
                Number("旋转 °", node.Rotation, -180, 180, node.Id, v => document.Replace(Current(node.Id) with { Rotation = v }));
            foreach (var property in CanvasCapabilities.Find(node.Type)!.Properties.OrderBy(p => p.Key == "opacity" ? 0 : p.Key == "fill" ? 1 : 2))
            {
                string key = property.Key; var value = CanvasDeclaration.Property(document.Design, node, key);
                string label = key switch { "fill" => "填色", "color" => "文字颜色", "actions" => "按钮颜色", "opacity" => "不透明度 %", "radius" => "圆角", "fontSize" => "字号", "text" => "文字", _ => key };
                if (property.Value.Kind == "number")
                {
                    double scale = key == "opacity" ? 100 : 1;
                    Number(label, value.Number!.Value * scale, property.Value.Minimum * scale, property.Value.Maximum * scale, node.Id,
                        v => SetProperty(node.Id, key, new(Number: v / scale)));
                }
                else if (property.Value.Kind == "color")
                    ColorField(label, value.Text!, node.Id, color => SetProperty(node.Id, key, new(Text: color)));
                else if (key == "text")
                {
                    var text = new TextBox { Text = value.Text, MaxLength = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 0, MinHeight = 30, MaxHeight = 80, FontSize = 12, Padding = new(5) };
                    text.LostFocus += (_, _) => { if (!rebuildingProperties && text.IsLoaded && CanEditField(node.Id) && text.Text != CanvasDeclaration.Property(document.Design, Current(node.Id), key).Text) Edit(() => SetProperty(node.Id, key, new(Text: text.Text))); };
                    AddField(label, text, true);
                }
            }
            if (node.Type == "draw.image@1") AssetDescription(node);
            if (node.Type == "content.slot@1")
                ColorField("文件名称颜色", document.Design.Recipe.ContentForeground ?? "#F2F2F2", node.Id,
                    color => document.Set(document.Design with { Recipe = document.Design.Recipe with { ContentForeground = color } }));
            if (node.Type == "draw.text@1")
            {
                var font = new ComboBox { MinWidth = 0, MinHeight = 30, FontSize = 11, Padding = new(5), HorizontalAlignment = HorizontalAlignment.Stretch };
                foreach (string family in new[] { "sans", "serif", "mono" }) font.Items.Add(new ComboBoxItem { Content = family switch { "serif" => "衬线", "mono" => "等宽", _ => "无衬线" }, Tag = family });
                font.SelectedIndex = node.Typography?.Family switch { "serif" => 1, "mono" => 2, _ => 0 };
                font.SelectionChanged += (_, _) =>
                {
                    if (rebuildingProperties || !font.IsLoaded || !CanEditField(node.Id) || font.SelectedItem is not ComboBoxItem choice) return;
                    Edit(() => document.Replace(Current(node.Id) with { Typography = (Current(node.Id).Typography ?? new()) with { Family = (string)choice.Tag } }));
                };
                AddField("字体", font);
                Number("字距", node.Typography?.LetterSpacing ?? 0, 0, 8, node.Id,
                    v => document.Replace(Current(node.Id) with { Typography = (Current(node.Id).Typography ?? new()) with { LetterSpacing = v } }));
            }
            ActionRow(actions, ("上移一层", () => Edit(() => Reorder(node.Id, 1))), ("下移一层", () => Edit(() => Reorder(node.Id, -1))));
            if (node.Type == "layout.group@1")
                ActionRow(actions, ("进入组合", () => { context = node.Id; selected.Clear(); Render(); }), ("拆分组合", UngroupSelection));
            if (!document.Subtree([node.Id]).Any(n => n.Type is "content.slot@1" or "host.title@1"))
            {
                ActionRow(actions, ("复制", Duplicate), ("保存组合", SaveCombinationPrompt));
                ActionRow(actions, ("删除对象", DeleteSelection));
            }
            SetControlsEnabled(fields, editable); SetControlsEnabled(actions, editable);
            if (!editable) head.Children.Add(new TextBlock { Text = "已锁定 · 在图层栏解锁", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Ink("#7BC9AD") });
        }
        finally { rebuildingProperties = false; }
    }
    private bool CanEditField(string id) => !closed && selected.Count == 1 && selected.Contains(id)
        && document.Design.Recipe.Nodes.Any(n => n.Id == id) && !layerState.Locked.Contains(id);
    private void SetArtboardSize(double width, double height)
    {
        document.Set(document.Design with { Recipe = document.Design.Recipe with { Artboard = new(width, height) } });
        UpdateArtboard(); Fit();
    }
    private void AddField(string label, FrameworkElement control, bool wide = false)
    {
        int cols = fields.ColumnDefinitions.Count;
        if (wide && fieldCount % cols != 0) fieldCount += cols - fieldCount % cols;
        int row = fieldCount / cols, column = fieldCount % cols;
        while (fields.RowDefinitions.Count <= row) fields.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Ink("#ADB1B0") }); panel.Children.Add(control);
        Grid.SetRow(panel, row); Grid.SetColumn(panel, column); if (wide) Grid.SetColumnSpan(panel, cols); fields.Children.Add(panel);
        fieldCount += wide ? cols : 1;
    }
    private void Number(string label, double value, double min, double max, string? id, Action<double> set)
    {
        var field = new NumberBox { Value = Math.Round(value, 2), Minimum = min, Maximum = max,
            SmallChange = .1, LargeChange = 10, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            FontSize = 12, Padding = new(5, 3, 5, 3), MinWidth = 0, MinHeight = 30 };
        void CommitField()
        {
            if (rebuildingProperties || !field.IsLoaded || closed || (id is null ? selected.Count != 0 : !CanEditField(id)) || !double.IsFinite(field.Value) || Math.Abs(field.Value - value) <= .005) return;
            Edit(() => set(field.Value));
        }
        field.LostFocus += (_, _) => CommitField();
        field.KeyDown += (_, e) => { if (e.Key == global::Windows.System.VirtualKey.Enter) { CommitField(); viewport.Focus(FocusState.Programmatic); e.Handled = true; } };
        AddField(label, field);
    }
    private void ColorField(string label, string value, string? id, Action<string> set)
    {
        var b = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinWidth = 0, MinHeight = 30, Padding = new(5), Background = Ink("#34373B") };
        var swatch = new Border { Height = 17, Background = Ink(value), CornerRadius = new(2) }; b.Content = swatch;
        ToolTipService.SetToolTip(b, value);
        var picker = new ColorPicker { Color = Ink(value).Color, IsAlphaEnabled = false, IsHexInputVisible = true, IsColorChannelTextInputVisible = false, IsColorSliderVisible = false, Width = 250 };
        var content = new StackPanel { Spacing = 8 }; content.Children.Add(picker);
        var flyout = new Flyout { Content = content };
        content.Children.Add(Command("应用颜色", "", () =>
        {
            if (id is null ? selected.Count != 0 : !CanEditField(id)) { flyout.Hide(); return; }
            var color = picker.Color; string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            flyout.Hide(); if (!string.Equals(hex, value, StringComparison.OrdinalIgnoreCase)) Edit(() => set(hex));
        }));
        b.Flyout = flyout; AddField(label, b);
    }
    private static void ActionRow(Panel panel, params (string Label, Action Action)[] commands)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var command in commands) { var b = Command(command.Label, "", command.Action); b.FontSize = 11; b.Background = Ink("#292B2F"); b.Padding = new(5); row.Children.Add(b); }
        panel.Children.Add(row);
    }
    private void SaveCombinationPrompt()
    {
        Try(() =>
        {
            if (selected.Count == 0 || selected.Any(layerState.Locked.Contains) || document.Subtree(selected).Any(n => n.Type is "content.slot@1" or "host.title@1"))
                throw new ArgumentException("请选择未锁定的装饰对象；不保存真实文件区域。");
            var selection = selected.ToArray(); string work = current;
            var panel = new StackPanel { Spacing = 10, Width = 240 };
            panel.Children.Add(new TextBlock { Text = "存入组合库", FontSize = 14 });
            var name = new TextBox { Text = selection.Length == 1 ? Current(selection[0]).Name ?? "我的组合" : "我的组合", MaxLength = 64 };
            panel.Children.Add(name);
            panel.Children.Add(new TextBlock { Text = "保存装饰快照；拖入其他作品后独立编辑。", FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.Bottom };
            void Save()
            {
                if (work != current || string.IsNullOrWhiteSpace(name.Text)) return;
                Try(() => { SaveCombination(selection, name.Text.Trim()); flyout.Hide(); });
            }
            panel.Children.Add(Command("保存组合", "save", Save));
            name.KeyDown += (_, e) => { if (e.Key == global::Windows.System.VirtualKey.Enter) { Save(); e.Handled = true; } };
            flyout.ShowAt(contextBar); name.Focus(FocusState.Programmatic); name.SelectAll();
        });
    }
    private void SaveCombination(IEnumerable<string> selection, string name)
    {
        var ids = selection.ToArray(); var source = new CanvasAuthoring(document.Design);
        string root = ids.Length == 1 ? ids[0] : source.Group(ids);
        var nodes = source.Export(root);
        var box = CanvasLayout.ForAuthoring(source.Design, board.Width, board.Height).Nodes.Single(n => n.Node.Id == root).Bounds;
        var size = new CanvasCombinationSize(box.Width, box.Height);
        var behavior = source.ExportBehavior(root);
        workspace.SaveCombination(new(CanvasCombination.Digest(nodes, size, behavior), name, nodes, size, behavior)); groupsLibrary = true; RefreshLibrary();
        notice.Text = "“" + name + "”已存入组合库 · 可切换作品拖入，实例互不影响。";
    }
    private CanvasNode Current(string id) => document.Design.Recipe.Nodes.First(n => n.Id == id);
    private static void SetControlsEnabled(Panel panel, bool enabled)
    {
        foreach (var child in panel.Children)
        {
            if (child is Control control) control.IsEnabled = enabled;
            if (child is Panel nested) SetControlsEnabled(nested, enabled);
        }
    }
    private void Box(string id, Func<CanvasBox, CanvasBox> change)
    { var n = Current(id); document.Replace(n with { Box = CanvasEditingGeometry.Constrain(change(n.Box)) }); }
    private void SetProperty(string id, string key, CanvasValue value)
    { var n = Current(id); var props = n.Properties.ToDictionary(p => p.Key, p => p.Value); props[key] = value; document.Replace(n with { Properties = props }); }
    private void Reorder(string id, int offset)
    {
        if (document.IsLocked(id)) throw new ArgumentException("请先解锁对象。");
        var n = Current(id); var siblings = document.Design.Recipe.Nodes.Where(p => p.Parent == n.Parent).ToArray();
        int position = Array.FindIndex(siblings, p => p.Id == id), target = position + offset; if (target < 0 || target >= siblings.Length) return;
        var tree = document.Subtree([id]); var ids = tree.Select(p => p.Id).ToHashSet(); var other = document.Subtree([siblings[target].Id]);
        var nodes = document.Design.Recipe.Nodes.Where(p => !ids.Contains(p.Id)).ToList();
        int index = offset < 0 ? nodes.FindIndex(p => p.Id == other[0].Id) : nodes.FindLastIndex(p => other.Any(o => o.Id == p.Id)) + 1;
        nodes.InsertRange(index, tree); document.Set(document.WithNodes(nodes));
    }
}
