using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private string? selectedTitleControl;

    private IReadOnlyList<CanvasTitleControl> TitleControls(CanvasNode node)
    {
        var placed = geometry!.Nodes.Single(p => p.Node.Id == node.Id);
        return CanvasTitleControls.Resolve(node.Id, placed.Bounds, board.Width, board.Height);
    }
    private CanvasTitleControl? SelectedTitleControl => selected.Count == 1 &&
        document.Design.Recipe.Nodes.FirstOrDefault(n => n.Id == selected.Single()) is { Type: "host.title@1" } node
            ? TitleControls(node).FirstOrDefault(c => c.Id == selectedTitleControl) : null;

    private static string ControlLabel(CanvasTitleControl control) => control.Role switch
    { "toggle" => "收展按钮 v", "name" => "标题文字", _ => "更多菜单 …" };

    private void SelectTitleControl(CanvasTitleControl control)
    { Select(control.Parent); selectedTitleControl = control.Id; Render(); }

    private bool SelectTitleControlAt(Point point)
    {
        var title = geometry!.Nodes.Single(p => p.Node.Type == "host.title@1").Node;
        // Host controls own these pixels in both the desktop and editor. Merely selecting
        // the control is allowed when locked; mutating bindings still checks the parent lock.
        var hit = TitleControls(title).FirstOrDefault(c => CanvasEditingGeometry.Hit(c.BoundsDip, 0, point.X, point.Y));
        if (hit is null) return false;
        SelectTitleControl(hit); return true;
    }

    private void TitleControlRows(CanvasNode node)
    {
        foreach (var control in TitleControls(node))
        {
            var button = Command(ControlLabel(control), "", () => SelectTitleControl(control));
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Margin = new(16, 0, 0, 0); button.FontSize = 11; button.Padding = new(5, 5, 3, 5);
            SetActive(button, SelectedTitleControl?.Id == control.Id); layers.Children.Add(button);
        }
    }

    private void TitleControlProperties(CanvasTitleControl control)
    {
        inspector.Children.Add(new TextBlock { Text = ControlLabel(control), FontSize = 14 });
        inspector.Children.Add(new TextBlock { Text = control.Id, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Ink("#ADB1B0") });
        inspector.Children.Add(new TextBlock { Text = control.CanBind
            ? "行为只由这个 v 按钮触发，不绑定标题文字或整条标题栏。位置随父操作区布局，保留原生命中及安全间距。"
            : control.Role == "more" ? "原生更多菜单由宿主管理，不能被脚本替换。" : "标题文字不承载收展行为；选中父操作区可调整文字和整体排版。",
            FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ink("#ADB1B0") });
        inspector.Children.Add(Command("选择父操作区", "", () => { Select(control.Parent); Render(); }));
        if (!control.CanBind) return;
        inspector.Children.Add(new TextBlock { Text = "两个收展样例效果等价；替换时先停用旧绑定，避免一次点击切换两次。", FontSize = 11,
            TextWrapping = TextWrapping.Wrap, Foreground = Ink("#ADB1B0") });
        var panel = new StackPanel { Spacing = 8 }; inspector.Children.Add(panel);
        BehaviorBindings(panel, control.Id);
        // Domain validation is authoritative as well; disabling the controls is just feedback.
        if (layerState.Locked.Contains(control.Parent))
        {
            panel.IsHitTestVisible = false; panel.Opacity = .5;
            inspector.Children.Add(new TextBlock { Text = "父操作区已锁定；解锁后编辑行为。", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        }
    }

    private void RequireWholeLayerSelection()
    {
        if (SelectedTitleControl is not null) throw new ArgumentException("这是宿主子控件；请选择父操作区调整布局，不能单独删除或复制安全入口。");
    }
}
