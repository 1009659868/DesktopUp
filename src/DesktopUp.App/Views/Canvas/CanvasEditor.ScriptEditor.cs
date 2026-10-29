using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private Border? scriptPanel;
    private readonly Dictionary<string, (string Name, string Script)> scriptBuffers = [];
    private void OpenScript(CanvasBehaviorPrefab prefab)
    {
        StopStudioRun(); CloseScriptEditor(); string work = current;
        string bufferKey = work + "/" + prefab.Id;
        var buffer = scriptBuffers.GetValueOrDefault(bufferKey, (prefab.Name, prefab.Script));
        var grid = new Grid { Padding = new(18), RowSpacing = 12 };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var name = new TextBox { Text = buffer.Name, MaxLength = 64, Header = "预制体名称 · 修改仅影响本作品" }; grid.Children.Add(name);
        var code = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 14, MaxLength = 8192,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
            Text = buffer.Script };
        name.TextChanged += (_, _) => scriptBuffers[bufferKey] = (name.Text, code.Text);
        code.TextChanged += (_, _) => scriptBuffers[bufferKey] = (name.Text, code.Text);
        ScrollViewer.SetVerticalScrollBarVisibility(code, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(code, ScrollBarVisibility.Auto);
        Grid.SetRow(code, 1); grid.Children.Add(code);
        var help = new TextBlock { Text = "受限脚本 v1 · 不运行C#/JS、循环或系统命令\nposter.toggle();  poster.expand();  poster.collapse();\nset(\"opacity\", 1);  animate(\"offsetY\", 12, 180, \"smooth\");\nwait(180);  if (collapsed) { ... } else { ... }\n动作作用于绑定的self；animate等待过渡完成后继续下一句。", FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ink("#ADB1B0") };
        Grid.SetRow(help, 2); grid.Children.Add(help);
        var footer = new StackPanel { Spacing = 8 }; var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        void Save(bool library)
        {
            bool workSaved = false;
            try
            {
                if (current != work) throw new ArgumentException("作品已切换，请重新打开脚本。");
                var next = prefab with { Name = name.Text, Script = CanvasScript.NormalizeLineEndings(code.Text) };
                CanvasBehaviorPrefabs.ValidatePrefab(next);
                var entries = (Behavior.Prefabs ?? []).Where(p => p.Id != next.Id).Append(next).ToArray();
                var candidate = document.Design with { Recipe = document.Design.Recipe with { Behavior = Behavior with { Prefabs = entries } } };
                candidate = CanvasDeclaration.Freeze(candidate);
                CanvasBehaviorPrefabs.RequireUnlockedEdit(document.Design, candidate.Recipe.Behavior);
                // Save means durable authoring, not only an in-memory edit. It never applies
                // the design to the desktop. Library and work are independent resources.
                workspace.SaveWork(work, names[work], candidate, showContentReference);
                workSaved = true;
                document.Set(candidate); Invalidate(); Render(); scriptBuffers.Remove(bufferKey);
                if (library) { workspace.SaveBehavior(next); RefreshLayers(); }
                status.Text = library ? "已保存草稿并存入行为库；未应用到桌面，其他作品不变。" : "脚本与草稿已保存；重开可继续，未应用到桌面。";
            }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
            { status.Text = (workSaved ? "草稿已保存；后续操作未完成：" : "未保存，编辑内容保留：") + error.Message; }
        }
        ActionRow(footer, ("校验并保存", () => Save(false)), ("存入行为库", () => Save(true)),
            ("复制为新行为", () => OpenScript(new(CanvasAuthoring.NewId(), name.Text + " 副本", code.Text))), ("返回画板", CloseScriptEditor));
        footer.Children.Add(status); Grid.SetRow(footer, 3); grid.Children.Add(footer);
        scriptPanel = new Border { Background = Ink("#202225"), Child = grid };
        Canvas.SetZIndex(scriptPanel, 2000); stage.Children.Add(scriptPanel);
    }
    private void CloseScriptEditor()
    { if (scriptPanel is not null) stage.Children.Remove(scriptPanel); scriptPanel = null; }
}
