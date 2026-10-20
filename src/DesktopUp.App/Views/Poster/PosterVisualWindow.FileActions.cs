using DesktopUp.Application;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private PointerScreenPoint _contentMenuPoint;
    internal bool HasFileAction => _contentSession.HasFileAction;
    private void SetFileActionBusy(bool busy)
    {
        _contentSession.SetInteraction(PosterContentInteraction.FileAction, busy);
        try { spatial.NotifyFileActionState(PosterId); }
        catch
        {
            if (busy) _contentSession.SetInteraction(PosterContentInteraction.FileAction, false);
            throw;
        }
    }
    private string? BeginContentDelete()
    {
        if (spatial.ContentOperationsStopping || _disposed || HasFileAction || HasContentGesture || !spatial.TransfersAvailable || spatial.HasCenterDraft(PosterId!))
            return "请先完成当前操作。";
        string[] ids = SelectedContentIds();
        if (ids.Length == 0) return "请先选择文件。";
        SetFileActionBusy(true); spatial.TrackContentOperation(Delete()); return null;
        async Task Delete()
        {
            try
            {
                var result = await spatial.DeleteContent(PosterId!, ids, _windowHandle, _contentOpenCancellation.Token);
                if (!_disposed && result.Errors.Count > 0) await ShowFileActionError(string.Join("\n", result.Errors));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!_disposed) await ShowFileActionError("删除未完成：" + e.Message); }
            finally { SetFileActionBusy(false); if (!_disposed) QueueContentProposalDrain(); }
        }
    }
    private string? BeginShellUi(string? itemId, ShellUiAction action)
    {
        if (spatial.ContentOperationsStopping || _contentSession.HasFileAction || _disposed || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!)) return "请先完成当前操作。";
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
                NewContentLocation? location = action == ShellUiAction.DirectoryMenu ? await spatial.PrepareNewContent(PosterId!) : null;
                if (action == ShellUiAction.DirectoryMenu && location is null) return;
                string? path = location?.Directory ?? target?.Item.Target.Locator ?? await spatial.ResolveActionDirectory(PosterId!);
                if (path is null || _disposed || _contentOpenCancellation.IsCancellationRequested) return;
                var outcome = await spatial.InvokeShellAction(PosterId!, itemId, target, location, path, action,
                    point.X, point.Y, _contentOpenCancellation.Token);
                var result = outcome.Result;
                if (outcome.PublicationError is { } publicationError && !_disposed)
                    await ShowFileActionError(publicationError);
                if (outcome.Registered && result.CreatedItem is { EditName: true } created && !_disposed)
                {
                    try
                    {
                        if (spatial.ContentItemAtPath(PosterId!, created.Path) is { } newId)
                            await RenameContentInline(newId);
                    }
                    catch (Exception e)
                    { if (!_disposed) await ShowFileActionError("文件已由 Windows 创建并保留，未删除或重试。\n" + created.Path + "\n" + e.Message); }
                }
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
        var create = new MenuFlyoutSubItem { Text = "新建", IsEnabled = !_contentSession.HasFileAction && spatial.TransfersAvailable && spatial.MappingAvailable(PosterId!) };
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
            var item = StyleMenuItem(new MenuFlyoutItem { Text = text, IsEnabled = !_contentSession.HasFileAction });
            item.Click += (_, _) =>
            {
                if (!current(command)) return;
                flyout.Hide();
                if (BeginShellUi(null, action) is { } error) ReportContentFailure(error);
            };
            flyout.Items.Add(item);
        }
        Add("目录属性", "directory-properties", ShellUiAction.Properties);
        Add(ShellUiProtocol.MenuLabel, "directory-menu", ShellUiAction.DirectoryMenu);
    }
}
