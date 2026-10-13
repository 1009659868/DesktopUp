using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Func<ContentReferenceCommand, StateCommandResult>? _applyReferences;
    internal void ConfigureReferences(Func<ContentReferenceCommand, StateCommandResult> apply) => _applyReferences = apply;

    private async Task RemoveSelectedReference() => await PortableAction(async () =>
    {
        if (_view?.SelectedPosterId is not { } id || SelectedContentId is not { } item ||
            _readCommitted?.Invoke() is not { } basis || !basis.PostersById.TryGetValue(id, out var owner)) return;
        if (basis.ContentCatalog.Bindings.All(b => b.PosterId != id) || DesktopInteractionConflicts(id)) return;
        var body = new StackPanel { Spacing = 10 };
        var selected = _contentRuntime?.Invoke()?.ReadContentEditor(id)?.SelectedIds.ToArray() ?? [item];
        if (selected.Length == 0) return;
        body.Children.Add(new TextBlock { Text = $"仅从当前海报移除 {selected.Length} 张卡片，不删除或移动原文件。", TextWrapping = TextWrapping.Wrap });
        if (!await ReferenceDialog("移除引用", body, "仅移除此关联")) return;
        var command = new ContentDetachCommand("detach-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
            basis.Revision, new(new Dictionary<string,long> { [id] = owner.EntityVersion }, ContentVersion: basis.ContentVersion), id, selected, true);
        string? error = _contentRuntime?.Invoke()?.DetachContent(command);
        await PortableDialog(error is null ? "引用已移除" : "未移除", error ?? "原文件和其他海报均未改变。");
    });

    private async Task EditReferences(bool repair) => await PortableAction(async () =>
    {
        if (_view?.SelectedPosterId is not { } targetId || _readCommitted?.Invoke() is not { } basis ||
            !basis.PostersById.TryGetValue(targetId, out var target) || _applyReferences is null) return;
        if (DesktopInteractionConflicts(targetId)) { await PortableDialog("暂不可编辑", "请先结束海报上的拖动或收展，再编辑引用。"); return; }
        var relation = repair && SelectedContentId is { } selected ? ContentRelationPolicy.Resolve(basis.ContentCatalog, targetId, selected) : null;
        if (repair && relation is null) { await PortableDialog("请选择真实内容", "先选择已添加的真实 Item，再修复／更换其来源。DEMO 项目没有文件来源。"); return; }
        string[] affected = repair ? ContentReferencePolicy.AffectedPosters(basis.ContentCatalog, relation!.Item.Id).ToArray() : [targetId];
        var paths = new TextBox { Header = repair ? "新来源绝对路径" : "本机文件／文件夹路径（每行一项，最多 100 项）",
            AcceptsReturn = !repair, TextWrapping = TextWrapping.Wrap, MaxLength = 100 * 8192, MinWidth = 420,
            Text = repair ? relation!.Item.Target.Locator : "" };
        while (true)
        {
            var body = new StackPanel { Spacing = 10 };
            body.Children.Add(new TextBlock { Text = $"目标：{target.Poster.Title}\n只建立引用，不复制、搬移、重命名或删除原文件。", TextWrapping = TextWrapping.Wrap });
            body.Children.Add(paths);
            var method = new ComboBox { Header = "下一步", SelectedIndex = 0, ItemsSource = new[] { "预览以上路径", "选择一个文件后继续", "选择一个文件夹后继续" } };
            body.Children.Add(method);
            if (!await ReferenceDialog(repair ? "修复所选卡片来源" : "添加内容引用", body, "继续")) return;
            if (method.SelectedIndex == 0) break;
            var picker = new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(this), false,
                _portableLedger!, _portableLog!, purpose: method.SelectedIndex == 2 ? NativePickerPurpose.ReferenceFolder : NativePickerPurpose.ReferenceFile);
            _portablePicker = picker;
            string? path;
            try { path = await picker.Completion; }
            finally { if (ReferenceEquals(_portablePicker, picker)) _portablePicker = null; }
            if (_editingClosed || _portableStopping) return;
            if (path is not null) paths.Text = repair || string.IsNullOrWhiteSpace(paths.Text) ? path : paths.Text + "\n" + path;
            body.Children.Remove(paths);
        }
        var inputs = paths.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (inputs.Length is < 1 or > ContentReferencePolicy.MaximumBatch || repair && inputs.Length != 1)
        { await PortableDialog("未生成预览", repair ? "修复需指定一个新来源。" : "请输入 1～100 条绝对路径。"); return; }
        // Local fixed/removable metadata only; no remote enumeration or UI-thread filesystem access.
        var previews = await Task.Run(() => inputs.Select(p => LocalContentReferenceProbe.Inspect(p)).ToArray());
        if (_editingClosed || _portableStopping) return;
        var rows = new StackPanel { Spacing = 16 };
        var choices = new List<(LocalReferencePreview Preview, ComboBox Choice, List<ContentReferenceEntry> Entries)>();
        var seen = new List<ContentItem>();
        foreach (var preview in previews)
        {
            rows.Children.Add(new TextBlock { Text = preview.Input + "\n" + preview.Message, TextWrapping = TextWrapping.Wrap });
            if (preview.Item is not { } item) continue; // explicit skip, no invented placeholder entity
            var entries = new List<ContentReferenceEntry> { new(item, ContentReferenceChoice.Skip), new(item, ContentReferenceChoice.CreateIndependent) };
            var labels = new List<string> { "跳过", repair ? "使用此来源修复卡片" : "添加" };
            var options = ContentReferencePreviewPolicy.ForItem(basis.ContentCatalog, item, seen);
            if (!repair) foreach (var duplicate in options.Reusable)
            {
                string owners = duplicate.FromThisBatch ? "本批前项（须先添加）" : string.Join("、",
                    duplicate.PosterIds.Select(id => basis.PostersById.TryGetValue(id, out var poster) ? poster.Poster.Title : id));
                rows.Children.Add(new TextBlock {Text=$"已在 {owners} 中；保持单一归属，不重复添加。",TextWrapping=TextWrapping.Wrap});
            }
            if(!repair && options.Reusable.Count>0){entries.RemoveAt(1);labels.RemoveAt(1);}
            var choice = new ComboBox { ItemsSource = labels, SelectedIndex = repair || options.DefaultChoice == ContentReferenceChoice.CreateIndependent ? 1 : 0, MinWidth = 320 };
            rows.Children.Add(choice); choices.Add((preview, choice, entries)); seen.Add(item);
        }
        if (choices.Count == 0) { await PortableDialog("没有可添加的来源", string.Join("\n", previews.Select(p => p.Input + "\n" + p.Message))); return; }
        bool conversion = !repair && basis.ContentCatalog.Bindings.All(b => b.PosterId != targetId);
        var confirmConversion = new CheckBox { Content = "替换此海报的 DEMO 构图；旧构图归档，新引用从自动布局开始", IsChecked = false };
        if (conversion) rows.Children.Add(confirmConversion);
        if (repair)
            rows.Children.Add(new TextBlock { Text = "修复当前卡片来源，顺序／图钉／尺寸不变。所在海报：\n" +
                string.Join("\n", affected.Select(id => $"• {basis.PostersById[id].Poster.Title} ({id})")), TextWrapping = TextWrapping.Wrap });
        rows.Children.Add(new TextBlock { Text = "原文件不动。卡片可双击打开，并在所选项目菜单中定位来源。", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        if (!await ReferenceDialog("确认引用预览", rows, repair ? "修复" : "确认添加")) return;
        var selectedRows = choices.Where(c => c.Entries[c.Choice.SelectedIndex].Choice != ContentReferenceChoice.Skip).ToArray();
        if (selectedRows.Length == 0) return;
        if (conversion && confirmConversion.IsChecked != true) { await PortableDialog("未添加", "尚未确认替换 DEMO 构图，当前配置保持不变。"); return; }
        if (!await Task.Run(() => selectedRows.All(c => LocalContentReferenceProbe.IsCurrent(c.Preview))))
        { await PortableDialog("来源已变化", "确认后来源不可用或已变化，请重新添加／修复并预览。当前配置未改变。"); return; }
        if (_editingClosed || _portableStopping) return;
        if (affected.Any(DesktopInteractionConflicts)) { await PortableDialog("未应用", "受影响海报正在交互，请结束操作后重新预览。"); return; }
        var command = new ContentReferenceCommand("reference-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
            basis.Revision, new(affected.ToDictionary(id => id, id => basis.PostersById[id].EntityVersion), ContentVersion: basis.ContentVersion), targetId,
            selectedRows.Select(c => c.Entries[c.Choice.SelectedIndex]).ToArray(), conversion, relation?.Item.Id);
        var result = _applyReferences(command);
        await PortableDialog(result.Succeeded ? "引用已保存" : "未应用", result.Succeeded ?
            "引用关系及布局已保存，重启后保留。原文件未移动或删除。" : result.Error ?? "配置版本已变化，请重新预览。");
    });

    private async Task<bool> ReferenceDialog(string title, StackPanel body, string confirm)
    {
        if (_editingClosed || _portableStopping) return false;
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = title,
            Content = new ScrollViewer { MaxHeight = 430, Content = body }, PrimaryButtonText = confirm,
            CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
        ApplyPaperDialogStyle(dialog); _portableDialog = dialog;
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary && !_editingClosed && !_portableStopping; }
        finally { if (ReferenceEquals(_portableDialog, dialog)) _portableDialog = null; }
    }
}
