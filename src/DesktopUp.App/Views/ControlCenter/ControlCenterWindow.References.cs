using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private ContentReferenceRepairUseCase? _referenceRepair;
    private Action<StateCommandResult>? _referenceResult;
    internal void ConfigureReferenceRepair(ContentReferenceRepairUseCase repair, Action<StateCommandResult> result)
    { _referenceRepair = repair; _referenceResult = result; }

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

    private async Task RepairSelectedReference() => await PortableAction(async () =>
    {
        if (_referenceRepair is null || _view?.SelectedPosterId is not { } targetId ||
            _readCommitted?.Invoke() is not { } basis) return;
        if (DesktopInteractionConflicts(targetId))
        { await PortableDialog("暂不可编辑", "请先结束海报上的拖动或收展，再编辑引用。"); return; }
        var target = ContentReferenceRepairUseCase.Capture(basis, targetId, SelectedContentId);
        if (target is null)
        { await PortableDialog("请选择真实内容", "先选择已添加的真实 Item，再修复／更换其来源。DEMO 项目没有文件来源。"); return; }
        var pathInput = new TextBox { Header = "新来源绝对路径", TextWrapping = TextWrapping.Wrap,
            MaxLength = 100 * 8192, MinWidth = 420, Text = target.OriginalPath };
        while (true)
        {
            var body = new StackPanel { Spacing = 10 };
            body.Children.Add(new TextBlock { Text = $"目标：{target.Title}\n只建立引用，不复制、搬移、重命名或删除原文件。", TextWrapping = TextWrapping.Wrap });
            body.Children.Add(pathInput);
            var method = new ComboBox { Header = "下一步", SelectedIndex = 0,
                ItemsSource = new[] { "预览以上路径", "选择一个文件后继续", "选择一个文件夹后继续" } };
            body.Children.Add(method);
            if (!await ReferenceDialog("修复所选卡片来源", body, "继续")) return;
            if (method.SelectedIndex == 0) break;
            var picker = new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(this), false,
                _portableLedger!, _portableLog!, purpose: method.SelectedIndex == 2 ? NativePickerPurpose.ReferenceFolder : NativePickerPurpose.ReferenceFile);
            _portablePicker = picker;
            string? path;
            try { path = await picker.Completion; }
            finally { if (ReferenceEquals(_portablePicker, picker)) _portablePicker = null; }
            if (_editingClosed || _portableStopping) return;
            if (path is not null) pathInput.Text = path;
            body.Children.Remove(pathInput);
        }
        var inputs = pathInput.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (inputs.Length != 1)
        { await PortableDialog("未生成预览", "修复需指定一个新来源。"); return; }
        var preview = await _referenceRepair.PreviewAsync(target, inputs[0]);
        if (_editingClosed || _portableStopping) return;
        if (preview.Source.Item is null)
        { await PortableDialog("没有可添加的来源", preview.Source.Input + "\n" + preview.Source.Message); return; }
        var rows = new StackPanel { Spacing = 16 };
        rows.Children.Add(new TextBlock { Text = preview.Source.Input + "\n" + preview.Source.Message, TextWrapping = TextWrapping.Wrap });
        var choice = new ComboBox { ItemsSource = new[] { "跳过", "使用此来源修复卡片" }, SelectedIndex = 1, MinWidth = 320 };
        rows.Children.Add(choice);
        rows.Children.Add(new TextBlock { Text = "修复当前卡片来源，顺序／图钉／尺寸不变。所在海报：\n" +
            string.Join("\n", target.AffectedPosters.Select(p => $"• {p.Title} ({p.Id})")), TextWrapping = TextWrapping.Wrap });
        rows.Children.Add(new TextBlock { Text = "原文件不动。卡片可双击打开，并在所选项目菜单中定位来源。",
            TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        if (!await ReferenceDialog("确认引用预览", rows, "修复") || choice.SelectedIndex == 0) return;
        var result = await _referenceRepair.ApplyAsync(preview,
            () => !_editingClosed && !_portableStopping && !target.AffectedPosters.Any(p => DesktopInteractionConflicts(p.Id)));
        if (_editingClosed || _portableStopping) return;
        if (result.Status == ContentRepairStatus.SourceChanged)
        { await PortableDialog("来源已变化", "确认后来源不可用或已变化，请重新添加／修复并预览。当前配置未改变。"); return; }
        if (result.Status == ContentRepairStatus.HostUnavailable)
        { await PortableDialog("未应用", "受影响海报正在交互，请结束操作后重新预览。"); return; }
        var command = result.Command!;
        _referenceResult?.Invoke(command);
        await PortableDialog(command.Succeeded ? "引用已保存" : "未应用", command.Succeeded ?
            "引用关系及布局已保存，重启后保留。原文件未移动或删除。" : command.Error ?? "配置版本已变化，请重新预览。");
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
