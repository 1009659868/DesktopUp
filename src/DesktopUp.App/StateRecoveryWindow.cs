using DesktopUp.Infrastructure;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

/// <summary>
/// Startup-only recovery surface. The App owns the single-instance lease and resource ledger;
/// this window never creates posters, a tray, a state store or another process.
/// </summary>
public sealed class StateRecoveryWindow : Window
{
    private readonly StateBackupRecovery _recovery;
    private readonly Action _requestNormalRestart;
    private readonly Action _exit;
    private readonly Grid _root = new() { Padding = new Thickness(24), RowSpacing = 14 };
    private readonly InfoBar _notice = new() { IsOpen = true, IsClosable = false };
    private readonly InfoBar _pending = new()
    {
        IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Warning,
        Title = "上次恢复尚未完成",
        Message = "正常启动和选择备份恢复已暂停。请明确确认“处理上次中断”；不会自动处理或覆盖现有证据。"
    };
    private readonly ListView _backups = new() { SelectionMode = ListViewSelectionMode.Single, MinHeight = 150 };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly TextBlock _archive = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly Button _refresh = new() { Content = "重新检查备份" };
    private readonly Button _restore = new() { Content = "恢复所选备份…" };
    private readonly Button _resolve = new() { Content = "处理上次中断…" };
    private readonly Button _restart = new() { Content = "尝试正常启动" };
    private readonly Button _exitButton = new() { Content = "退出" };
    private ContentDialog? _dialog;
    private bool _loaded;
    private bool _closed;
    private bool _restartReady = true;

    public StateRecoveryWindow(StateBackupRecovery recovery, string reason, Action requestNormalRestart, Action exit)
    {
        _recovery = recovery;
        _requestNormalRestart = requestNormalRestart;
        _exit = exit;
        Title = "DesktopUp · 本机状态恢复";
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(880, 740));
        AppWindow.IsShownInSwitchers = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Spacing = 10 };
        heading.Children.Add(new TextBlock
        {
            Text = "本机状态恢复", FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
        });
        heading.Children.Add(new TextBlock
        {
            Text = "当前仅提供恢复入口，未创建海报和托盘。原状态库和备份不会被静默删除或自动替换。",
            TextWrapping = TextWrapping.Wrap
        });
        _notice.Severity = InfoBarSeverity.Warning;
        _notice.Title = "当前为离线模式";
        _notice.Message = reason.Length > 4000 ? reason[..4000] + "…" : reason;
        heading.Children.Add(_notice);
        heading.Children.Add(_pending);
        heading.Children.Add(_progress);
        _root.Children.Add(heading);

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock
        {
            Text = "当前配置的本地备份",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        body.Children.Add(new TextBlock
        {
            Text = "按更新时间排列，检查最近 100 份备份；默认只选中最近通过校验的备份，不会自动恢复。无效备份保留并列出原因。",
            TextWrapping = TextWrapping.Wrap
        });
        _backups.MaxHeight = 270;
        body.Children.Add(_backups);
        body.Children.Add(_details);
        body.Children.Add(_archive);
        var scroll = new ScrollViewer
        {
            Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        _root.Children.Add(scroll);

        var actions = new StackPanel { Spacing = 10 };
        var operations = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        operations.Children.Add(_refresh);
        operations.Children.Add(_restore);
        operations.Children.Add(_resolve);
        actions.Children.Add(operations);
        var lifecycle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        lifecycle.Children.Add(_restart);
        lifecycle.Children.Add(_exitButton);
        actions.Children.Add(lifecycle);
        actions.Children.Add(new TextBlock
        {
            Text = "未执行恢复也可尝试正常启动；这不表示原库已修好。检查或恢复期间请勿结束进程，不会自动进入主界面。",
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(actions, 2);
        _root.Children.Add(actions);
        Content = _root;

        AutomationProperties.SetAutomationId(_backups, "RecoveryBackupList");
        AutomationProperties.SetAutomationId(_restore, "RecoveryRestoreSelected");
        AutomationProperties.SetAutomationId(_resolve, "RecoveryResolveInterrupted");
        AutomationProperties.SetAutomationId(_restart, "RecoveryNormalRestart");
        AutomationProperties.SetAutomationId(_exitButton, "RecoveryExit");

        _backups.SelectionChanged += (_, _) => { ShowSelection(); UpdateControls(); };
        _refresh.Click += async (_, _) => await RefreshAsync();
        _restore.Click += async (_, _) => await RestoreSelectedAsync();
        _resolve.Click += async (_, _) => await ResolveInterruptedAsync();
        _restart.Click += (_, _) =>
        {
            if (IsBusy || !_restartReady || _recovery.HasPendingRecovery || _closed) return;
            try { _requestNormalRestart(); }
            catch (Exception exception) { ShowFailure("重新启动请求未完成", exception.Message); }
        };
        _exitButton.Click += (_, _) => CloseForExit();
        _root.Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await RefreshAsync();
        };
        AppWindow.Closing += (_, args) =>
        {
            // Closing during dialog/worker IO would release the single-instance lease too early.
            if (IsBusy) args.Cancel = true;
        };
        Closed += (_, _) =>
        {
            if (_closed) return;
            _closed = true;
            _dialog?.Hide();
            _exit();
        };
        UpdateControls();
    }

    public bool IsBusy { get; private set; }


    public bool CloseForExit()
    {
        if (IsBusy) return false;
        if (!_closed) Close();
        return true;
    }

    private async Task RefreshAsync()
    {
        if (IsBusy || _closed) return;
        SetBusy(true);
        try
        {
            string? selected = Selected?.FileName;
            IReadOnlyList<StateBackupCandidate> candidates = await Task.Run(_recovery.ListCandidates);
            _backups.Items.Clear();
            foreach (StateBackupCandidate candidate in candidates)
            {
                var text = new StackPanel { Spacing = 3 };
                text.Children.Add(new TextBlock
                {
                    Text = candidate.FileName,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });
                text.Children.Add(new TextBlock
                {
                    Text = candidate.IsValid
                        ? $"可恢复 · {candidate.LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · schema {candidate.SchemaVersion} · revision {candidate.Revision}"
                        : $"不可恢复 · {candidate.LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                    TextWrapping = TextWrapping.Wrap
                });
                _backups.Items.Add(new ListViewItem { Content = text, Tag = candidate });
            }
            _backups.SelectedItem = _backups.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => ((StateBackupCandidate)item.Tag).FileName == selected)
                ?? _backups.Items.Cast<ListViewItem>().FirstOrDefault(item => ((StateBackupCandidate)item.Tag).IsValid);
            if (candidates.Count == 0)
                _details.Text = "未找到本配置的备份。原库仍保留；请退出并保留日志和状态目录，不会创建空库覆盖它。";
            else if (Selected is null)
                _details.Text = "没有通过校验的候选。可选择条目查看原因；原备份仍保留，不会尝试强行恢复。";
            else ShowSelection();
        }
        catch (Exception exception) { ShowFailure("备份检查失败", exception.Message); }
        finally { SetBusy(false); }
    }

    private async Task RestoreSelectedAsync()
    {
        if (IsBusy || _closed || _recovery.HasPendingRecovery || Selected is not { IsValid: true } candidate) return;
        SetBusy(true);
        try
        {
            if (!await ConfirmAsync("恢复这份备份？",
                $"备份：{candidate.FileName}\n备份 revision：{candidate.Revision}\n\n" +
                "当前状态库及存在的 WAL/SHM 将完整归档，再安装经校验的备份副本。此操作会退回备份中的全部配置，备份之后的修改不会保留在当前库中。\n\n" +
                "原库和原备份都会保留；不会移动或删除桌面文件。恢复成功后仍由你明确选择尝试正常启动。",
                "确认恢复")) return;
            _restartReady = false;
            StateBackupRecoveryResult result = await Task.Run(() => _recovery.Restore(candidate.FileName));
            ShowResult(result, "备份已恢复", "已安装校验后的状态库。检查归档路径后，可选择“尝试正常启动”；最终以主界面启动结果为准。");
        }
        catch (Exception exception) { ShowFailure("恢复未完成", exception.Message); }
        finally { SetBusy(false); }
    }

    private async Task ResolveInterruptedAsync()
    {
        if (IsBusy || _closed || !_recovery.HasPendingRecovery) return;
        SetBusy(true);
        try
        {
            if (!await ConfirmAsync("处理上次中断？",
                "此操作只根据恢复日志收尾：若数据库替换尚未正式完成，将从归档逐字节还原原库及 WAL/SHM；若已确认完成，只结束日志收尾。\n\n" +
                "不会自动选择或导入其他备份。原库可能仍有启动错误，处理成功不等于原库已修好；之后可重新选择有效备份。",
                "确认处理")) return;
            _restartReady = false;
            StateBackupRecoveryResult result = await Task.Run(_recovery.RecoverInterruptedRestore);
            ShowResult(result, "上次恢复日志已处理", "现在可选择备份重新恢复，或明确尝试正常启动。原始证据和恢复归档均保留；日志收尾不表示原库已修好。");
        }
        catch (Exception exception) { ShowFailure("处理中断未完成", exception.Message); }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirm)
    {
        if (_closed || _root.XamlRoot is null || _dialog is not null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = _root.XamlRoot, Title = title, PrimaryButtonText = confirm,
            CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
            }
        };
        _dialog = dialog;
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        finally { if (ReferenceEquals(_dialog, dialog)) _dialog = null; }
    }

    private StateBackupCandidate? Selected => (_backups.SelectedItem as ListViewItem)?.Tag as StateBackupCandidate;

    private void ShowSelection()
    {
        if (Selected is not { } candidate) return;
        _details.Text = candidate.IsValid
            ? $"已选：{candidate.FileName}\n校验通过仅表示这份备份可用于恢复；还未更改当前状态。"
            : $"已选：{candidate.FileName}\n不可恢复：{candidate.Error ?? "校验失败"}";
    }

    private void ShowResult(StateBackupRecoveryResult result, string successTitle, string successMessage)
    {
        _archive.Text = result.ArchivePath is { } path
            ? "本次恢复材料目录（原始文件不会静默清理）：\n" + path
            : "";
        if (result.Succeeded)
        {
            _restartReady = !_recovery.HasPendingRecovery;
            _notice.Severity = InfoBarSeverity.Success;
            _notice.Title = successTitle;
            _notice.Message = successMessage;
        }
        else ShowFailure("恢复未完成", result.Error ?? "未知错误。请保留状态和恢复材料。");
    }

    private void ShowFailure(string title, string message)
    {
        // Without a pending journal the service has not left a partially installed file set.
        // Retrying normal startup is an explicit user action, not a claim that this database is healthy.
        _restartReady = !_recovery.HasPendingRecovery;
        _notice.IsOpen = true;
        _notice.Severity = InfoBarSeverity.Error;
        _notice.Title = title;
        _notice.Message = message;
        UpdateControls();
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateControls();
    }

    private void UpdateControls()
    {
        bool pending = _recovery.HasPendingRecovery;
        _pending.IsOpen = pending;
        _refresh.IsEnabled = !IsBusy && !_closed;
        _backups.IsEnabled = !IsBusy && !_closed;
        _restore.IsEnabled = !IsBusy && !_closed && !pending && Selected?.IsValid == true;
        _resolve.IsEnabled = !IsBusy && !_closed && pending;
        _restart.IsEnabled = !IsBusy && !_closed && !pending && _restartReady;
        _exitButton.IsEnabled = !IsBusy && !_closed;
    }
}

