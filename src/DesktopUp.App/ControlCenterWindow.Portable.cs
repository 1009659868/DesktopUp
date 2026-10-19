using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DesktopUp.Windows;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private static string DescribePortable(PortableImportPlan plan)
    {
        var all = plan.Creates.Concat(plan.Updates).ToDictionary(p => p.PosterId);
        var pending = plan.PendingPosterIds.Select(id => all.TryGetValue(id, out var p) ? p.Title : id);
        return (plan.IsRetry ? $"可安置 {plan.Updates.Count} 张。" :
            $"{(plan.Mode == PortableImportMode.Append ? "追加" : "替换")}：导入 {plan.Creates.Count} 张，移除本机 {plan.Removes.Count} 张海报配置。\n" +
            $"全局偏好：{(plan.ImportSettings ? "应用文件中的中心主题/动效/性能偏好（旧包未包含的设置保持本机）" : "保持本机")}。模板册及海报专属参数独立导入；替换海报不会清空本机模板册。不会移动或删除真实文件。") +
            $"\n等待安置 {plan.PendingPosterIds.Count} 张（保留原尺寸与状态，暂不显示）：\n" +
            string.Join("\n", pending.Select(n => "• " + n)) + "\n" + string.Join("\n", plan.Warnings.Distinct());
    }

    private PortableProfileUseCase? _portable;
    private Action? _requestRecovery;
    private bool _portableBusy;
    private bool _portableStopping;
    private TaskCompletionSource<bool>? _portableCompletion;
    private NativeProfilePicker? _portablePicker;
    private IStructuredLog? _portableLog;
    internal async Task CancelPortableAndWaitAsync()
    {
        _portableStopping = true;
        _iconCancellation?.Cancel();
        _transferStopping.Cancel();
        _portableLog?.Write("info", "portable.cancel_requested", new { picker = _portablePicker is not null, busy = _portableBusy });
        _portableDialog?.Hide();
        _portablePicker?.Cancel();
        if (_portableCompletion is { } completion) await completion.Task;
        _portableLog?.Write("info", "portable.cancel_completed");
    }
    private ContentDialog? _portableDialog;
    private IResourceLedger? _portableLedger;
    internal bool IsPortableInteractionActive => _portableBusy;
    internal void ConfigurePortable(PortableProfileUseCase controller, IResourceLedger ledger, IStructuredLog log, Action requestRecovery)
    {
        _portable = controller; _requestRecovery = requestRecovery; _portableLedger = ledger; _portableLog = log;
        AppWindow.Closing += async (_, args) =>
        {
            if (!_portableBusy) return;
            // The service must not lose this window while its picker/file operation still owns a lease.
            // Cancel the native picker too; await cancellation before releasing its owner.
            args.Cancel = true;
            await CancelPortableAndWaitAsync();
            if (!_editingClosed) Close();
        };
        Closed += (_, _) => _portableDialog?.Hide();
    }

    private async Task PortableAction(Func<Task> action)
    {
        if (_portableBusy || _portableStopping || _portable is null || _editingClosed || _catalogDialog is not null || _removalDialog is not null) return;
        if (_editor.IsDirty || _lampColorFlyout is not null || _settings.ProtectsInteraction || IsContentFieldDraftActive)
        {
            StateInfoBar.IsOpen = true; StateInfoBar.Severity = InfoBarSeverity.Warning;
            StateInfoBar.Title = "请先完成或取消当前参数编辑"; StateInfoBar.Message = "配置导入导出只使用已提交状态。"; return;
        }
        _portableBusy = true; SettingsInteractionChanged?.Invoke();
        _portableCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = _portableLedger?.Acquire("dialog.portable-profile", "configuration");
        try { await action(); }
        catch (Exception error)
        {
            if (!_editingClosed && !_portableStopping) await PortableDialog("操作未完成", error.Message + "\n请核对具体结果；文件任务以持久记录为准，不自动删除或回滚文件。");
        }
        finally
        {
            _portableDialog = null; _portableBusy = false;
            lease?.Dispose();
            _portableCompletion.TrySetResult(true); _portableCompletion = null;
            SettingsInteractionChanged?.Invoke();
        }
    }

    private async Task<bool> PortableDialog(string title, string message, string? confirm = null)
    {
        if (_editingClosed || _portableStopping) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot, Title = title,
            Content = new ScrollViewer { MaxHeight = 340, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } },
            PrimaryButtonText = confirm ?? "", CloseButtonText = confirm is null ? "关闭" : "取消",
            DefaultButton = ContentDialogButton.Close
        };
        ApplyPaperDialogStyle(dialog);
        _portableDialog = dialog;
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary && !_editingClosed && !_portableStopping; }
        finally { if (ReferenceEquals(_portableDialog, dialog)) _portableDialog = null; }
    }

    internal NativeProfilePicker? ActivePortablePickerForProbe => _portablePicker;
    internal Task RunPortablePickerForProbe(bool save, bool initiallyCancelled = false, NativePickerPurpose purpose=NativePickerPurpose.Profile) =>
        PortableAction(async () => { _ = await PickPortableFile(save, initiallyCancelled, purpose); });

    private async Task<string?> PickPortableFile(bool save, bool initiallyCancelled = false, NativePickerPurpose purpose=NativePickerPurpose.Profile)
    {
        var operation = new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(this),
            save, _portableLedger!, _portableLog!, initiallyCancelled,purpose);
        _portablePicker = operation;
        try { return await operation.Completion; }
        finally { if (ReferenceEquals(_portablePicker, operation)) _portablePicker = null; }
    }

    private async void PortableExport_Click(object sender, RoutedEventArgs e) => await PortableAction(async () =>
    {
        byte[] bytes = _portable!.Export(); // one committed snapshot, captured before the privacy prompt
        var document = PortableProfileCodec.Parse(bytes);
        if (!await PortableDialog("导出习惯配置",
            $"包含 {document.Posters.Count} 张海报的名称/副标题、位置、尺寸、状态、内容布局、标题显隐及颜色、当前和未选模板参数；另含模板册与中心主题/动效/性能设置。名称、模板名称及描述可能含个人信息。\n" +
            "不包含真实文件、应用安装、内容源路径、使用历史、密钥、启动权限或数据库内部版本。仅携带声明式模板数据，不携带模块代码或外部素材。\n" +
            "导出的是此时的已提交快照，之后的编辑不会混入。", "选择保存位置")) return;
        var file = await PickPortableFile(save: true);
        if (file is null || _editingClosed || _portableStopping) return;
        // The native save dialog owns the explicit overwrite confirmation.
        await Task.Run(() => _portable.Save(file, bytes, overwrite: true));
            if (!_editingClosed) await PortableDialog("导出完成", $"已保存 {document.Posters.Count} 张海报配置及模板册。\n{file}\n不包含实际文件、模块代码和外部素材。");
    });

    private async void PortableImport_Click(object sender, RoutedEventArgs e) => await PortableAction(async () =>
    {
        var file = await PickPortableFile(save: false);
        if (file is null || _editingClosed || _portableStopping) return;
        var mode = new ComboBox { Header = "导入方式", ItemsSource = new[] { "追加：保留本机已有海报", "替换：移除本机海报配置后导入" }, SelectedIndex = 0, MinWidth = 300 };
        var settings = new CheckBox { Content = "同时应用文件中的中心主题／全局动效／性能偏好", IsChecked = false };
        var body = new StackPanel { Spacing = 16 }; body.Children.Add(mode); body.Children.Add(settings);
        body.Children.Add(new TextBlock { Text = "下一步先预览，不立即应用。放不下的海报保留配置并列为等待安置；不会缩小、自动收起或推动已有海报。", TextWrapping = TextWrapping.Wrap });
        var options = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "导入配置", Content = body,
            PrimaryButtonText = "生成预览", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
        ApplyPaperDialogStyle(options);
        _portableDialog = options;
        try { if (await options.ShowAsync() != ContentDialogResult.Primary || _editingClosed || _portableStopping) return; }
        finally { _portableDialog = null; }
        var ticket = _portable!.Preview(file, mode.SelectedIndex == 1 ? PortableImportMode.Replace : PortableImportMode.Append, settings.IsChecked == true);
        string label = ticket.Plan.Mode == PortableImportMode.Replace ? "确认替换海报配置" : "确认追加";
        if (!await PortableDialog("确认导入影响", DescribePortable(ticket.Plan) +
            "\n应用前将备份本机数据库；任何变化或失败均不半应用。", label)) return;
        var result = _portable.Apply(ticket);
        if (!_editingClosed) await PortableDialog(result.Succeeded ? "导入完成" : "未应用", result.Message);
    });

    private async void PortablePending_Click(object sender, RoutedEventArgs e) => await PortableAction(async () =>
    {
        var names = _portable!.PendingNames();
        if (names.Count == 0) { await PortableDialog("等待安置", "当前没有等待安置的海报。"); return; }
        var ticket = _portable.PreviewPending();
        string message = "当前等待安置：\n" + string.Join("\n", names.Select(n => "• " + n)) + "\n\n" +
            DescribePortable(ticket.Plan);
        if (ticket.Plan.Updates.Count == 0) { await PortableDialog("空间仍不足", message + "\n请先腾出空间或连接更大的屏幕，再重试。原配置不变。"); return; }
        if (!await PortableDialog("重新安置预览", message, "安置可容纳的海报")) return;
        var result = _portable.Apply(ticket);
        if (!_editingClosed) await PortableDialog(result.Succeeded ? "安置完成" : "未应用", result.Message);
    });

    private async void PortableBackup_Click(object sender, RoutedEventArgs e) => await PortableAction(async () =>
    {
        var result = _portable!.Backup();
        await PortableDialog(result.Succeeded ? "备份完成" : "备份未完成",
            result.Succeeded ? $"已校验本机数据库备份：\n{result.FileName}\n备份不是跨机习惯配置。" : result.Error ?? "未知错误");
    });

    private async void PortableRecovery_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = false;
        await PortableAction(async () =>
        {
            confirmed = await PortableDialog("进入本机恢复",
                "将正常退出当前海报、控制中心和托盘，再打开离线恢复窗口。\n恢复会回退本机数据库，请在恢复窗口选择备份并再次确认；当前尚未执行恢复。", "退出并打开恢复");
        });
        // Release dialog/protection leases before application shutdown.
        if (confirmed && !_editingClosed) _requestRecovery?.Invoke();
    }
}
