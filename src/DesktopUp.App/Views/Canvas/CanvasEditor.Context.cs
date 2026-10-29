using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using global::Windows.ApplicationModel.DataTransfer;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private void AssetDescription(CanvasNode node)
    {
        string id = CanvasDeclaration.Property(document.Design, node, "asset").Text!;
        var asset = workspace.Assets().Single(a => a.Id == id);
        var text = new TextBox { Text = asset.Description ?? "", MaxLength = 512, AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinWidth = 0, MaxHeight = 100, FontSize = 12,
            PlaceholderText = "描述主体、颜色、构图；不会自动上传素材" };
        text.LostFocus += (_, _) =>
        {
            if (rebuildingProperties || !text.IsLoaded || closed || text.Text == (asset.Description ?? "")) return;
            Try(() => { asset = workspace.DescribeAsset(id, text.Text, asset.DescriptionRevision);
                notice.Text = "素材说明已保存 · 同一素材共享，不随画板撤销，不自动发送。"; });
        };
        AddField("素材说明 · 库内共享", text, true);
    }
    private void CopySceneData() => Try(() =>
    {
        // Initial legacy import is stable; edits are already revised by Invalidate.
        if (CanvasDeclaration.Digest(session!.Design ?? session.ReadCurrentDesign()) != CanvasDeclaration.Digest(document.Design))
            session.Revise(document.Design);
        var packet = session.DescribeScene(draft: true);
        var data = new DataPackage(); data.SetText(CanvasSceneDescription.Serialize(packet)); Clipboard.SetContent(data);
        notice.Text = packet.Content.Status == "current" ? "设计数据已复制 · 含同代匿名位置，无路径／图片字节；未发送网络。" :
            "设计数据已复制 · 内容位置尚未同代发布，已明确标记；预览后可再读取。";
    });
    private void CollapsedStyleProperties()
    {
        var value = document.Design.Recipe.Behavior ?? new();
        var mode = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0, FontSize = 11 };
        mode.Items.Add("继承底板"); mode.Items.Add("自定颜色"); mode.SelectedIndex = value.CollapsedMode == "custom" ? 1 : 0;
        mode.SelectionChanged += (_, _) =>
        {
            if (rebuildingProperties || !mode.IsLoaded || selected.Count != 0) return;
            Edit(() => SetBehavior((document.Design.Recipe.Behavior ?? new()) with { CollapsedMode = mode.SelectedIndex == 1 ? "custom" : "inherit" }));
        };
        AddField("收起外观", mode, true);
        if (value.CollapsedMode != "custom") return;
        ColorField("收起底色", value.CollapsedColor, null, color => SetBehavior((document.Design.Recipe.Behavior ?? new()) with { CollapsedColor = color }));
        Number("收起不透明度 %", value.CollapsedOpacity * 100, 0, 100, null,
            opacity => SetBehavior((document.Design.Recipe.Behavior ?? new()) with { CollapsedOpacity = opacity / 100 }));
    }
    private void SetBehavior(CanvasBehavior value)
    {
        CanvasBehaviorPrefabs.RequireUnlockedEdit(document.Design, value);
        document.Set(document.Design with { Recipe = document.Design.Recipe with { Behavior = value } });
    }
}
