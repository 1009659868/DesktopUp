using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private bool _shellUiBusy;
    private PointerScreenPoint _contentMenuPoint;
    internal bool HasFileAction => _shellUiBusy;
    private void SetFileActionBusy(bool busy)
    { _shellUiBusy=busy;spatial.NotifyFileActionState(PosterId); }
    private string? BeginShellUi(string? itemId, ShellUiAction action)
    {
        if (_shellUiBusy || _disposed || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!)) return "请先完成当前操作。";
        var target = itemId is null ? null : spatial.ContentActionRelation(PosterId!,itemId);
        if (itemId is not null && target is null) return "当前项目没有可用的本机路径。";
        var point = _contentMenuPoint;
        SetFileActionBusy(true);
        spatial.TrackContentOperation(Observe());
        return null;
        async Task Observe()
        {
            try
            {
                var client = new ShellUiClient(ContentWorkerLocation.InApplication(AppContext.BaseDirectory), ledger);
                string? path = target?.Item.Target.Locator ?? await spatial.ResolveActionDirectory(PosterId!);
                if (path is null || _disposed || _contentOpenCancellation.IsCancellationRequested) return;
                await spatial.ValidateMappedAction(PosterId!, itemId);
                if (target is not null && spatial.ContentActionRelation(PosterId!,itemId!)?.Witness != target.Witness)
                    throw new IOException("所选对象已变化，请重新打开菜单。");
                string identity = await Task.Run(()=>LocalTransferFiles.Identity(path));
                var result = await client.RunAsync(new(Guid.NewGuid(), action, path, point.X, point.Y) { Identity=identity }, _contentOpenCancellation.Token);
                if (!_disposed && result.Error is { } error) await ShowFileActionError(error);
                // The managed observer handles actual movement. Refresh only the selected
                // item's pixels/metadata (e.g. a shortcut's changed icon), never every card.
                if (!_disposed && result.Invoked && itemId is not null && _contentPorts.Views is ReferenceContentRuntime views)
                    views.RefreshItem(itemId);
                log.Write(result.Error is null ? "info" : "warning", "content.shell_ui_finished", new { PosterId, action, result.Invoked, result.Cancelled, result.Error });
            }
            catch (Exception e) { if (!_disposed) await ShowFileActionError("系统操作未完成：" + e.Message); }
            finally { SetFileActionBusy(false); if (!_disposed) { spatial.RefreshFileActionState(); QueueContentProposalDrain(); } }
        }
    }
    private void AddContentBackgroundMenu(MenuFlyout flyout, Func<string, bool> current)
    {
        if (spatial.ContentActionPath(PosterId!, null) is { } mapped)
        {
            var directory = StyleMenuItem(new MenuFlyoutItem { Text = "映射目录：" + Path.GetFileName(mapped), IsEnabled = false });
            ToolTipService.SetToolTip(directory, mapped);
            flyout.Items.Add(directory);
            var state=spatial.MappingState(PosterId!);
            string detail=spatial.MappingMessage(PosterId!)??state?.Message??"目录连接正常，正在自动同步直接子项。";
            var status=StyleMenuItem(new MenuFlyoutItem {Text=state?.Available==false?"目录暂不可用 · 内容已保留…":"映射状态…"});
            status.Click+=(_,_)=>{flyout.Hide();spatial.TrackContentOperation(ShowFileActionError(mapped+"\n\n"+detail));};
            flyout.Items.Add(status);
        }
        var mapping = StyleMenuItem(new MenuFlyoutItem { Text = "目录映射…", IsEnabled = !_shellUiBusy && spatial.TransfersAvailable });
        mapping.Click += (_, _) =>
        {
            if (!current("map-directory")) return;
            flyout.Hide();
            if (BeginDirectoryMapping() is { } error) ReportContentFailure(error);
        };
        flyout.Items.Add(mapping);
        var create = new MenuFlyoutSubItem { Text = "新建", IsEnabled = !_shellUiBusy && spatial.TransfersAvailable && spatial.MappingAvailable(PosterId!) };
        foreach (var (label, kind) in new[] { ("文件夹", NewContentKind.Folder), ("文本文档", NewContentKind.TextFile), ("快捷方式…", NewContentKind.Shortcut) })
        {
            var entry = StyleMenuItem(new MenuFlyoutItem { Text = label });
            entry.Click += (_, _) =>
            {
                if (!current("new-" + kind)) return;
                flyout.Hide();
                if (BeginContentCreate(kind) is { } error) ReportContentFailure(error);
            };
            create.Items.Add(entry);
        }
        flyout.Items.Add(create);
        flyout.Items.Add(new MenuFlyoutSeparator());
        void Add(string text, string command, ShellUiAction action)
        {
            var item = StyleMenuItem(new MenuFlyoutItem { Text = text, IsEnabled = !_shellUiBusy });
            item.Click += (_, _) =>
            {
                if (!current(command)) return;
                flyout.Hide();
                if (BeginShellUi(null, action) is { } error) ReportContentFailure(error);
            };
            flyout.Items.Add(item);
        }
        Add("目录属性", "directory-properties", ShellUiAction.Properties);
        Add("Windows 目录菜单…", "directory-menu", ShellUiAction.DirectoryMenu);
    }
}
