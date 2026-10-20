using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Only reached after RunIncomingProbe validates its owned, isolated data root.
    // Tests the actual production Popup and command callbacks, not native keyboard delivery.
    internal async Task VerifyInlineNamingForProbe()
    {
        await Task.Delay(300);
        async Task Edit(Func<string?> begin, string? value, bool cancel, bool collision = false, bool dismiss = false)
        {
            var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _inlineRenameReadyForProbe = async (name, popup, save, abandon) =>
            {
                try
                {
                    await Task.Delay(100);
                    if (!popup.IsOpen || name.FocusState == FocusState.Unfocused || _inlineRenameItem is null)
                        throw new IOException("Inline popup is not visible/focused.");
                    if (name.SelectionStart != 0 || name.SelectionLength == 0)
                        throw new IOException("Inline editor did not select a filename.");
                    if (collision)
                    {
                        name.Text = "first.txt"; save(); await Task.Delay(300);
                        if (!popup.IsOpen || name.IsReadOnly) throw new IOException("Collision did not retain editable name.");
                    }
                    if (value is not null) name.Text = value;
                    if (cancel) abandon(); else if (dismiss) popup.IsOpen = false; else save();
                    observed.TrySetResult();
                }
                catch (Exception e) { abandon(); observed.TrySetException(e); }
            };
            try
            {
                if (begin() is { } error) throw new IOException(error);
                await observed.Task.WaitAsync(TimeSpan.FromSeconds(12));
                var wait = System.Diagnostics.Stopwatch.StartNew();
                while (HasFileAction)
                { if (wait.Elapsed.TotalSeconds > 12) throw new IOException("Inline operation did not finish."); await Task.Delay(50); }
                if (_inlineRenameItem is not null) throw new IOException("Inline session not released.");
            }
            finally { _inlineRenameReadyForProbe = null; _cancelInlineRename?.Invoke(); }
        }
        await Edit(() => BeginContentCreate(NewContentKind.Folder), "行内新建文件夹", false);
        string directory = (await spatial.PrepareNewContent(PosterId!))!.Directory;
        if (!Directory.Exists(Path.Combine(directory, "行内新建文件夹"))) throw new IOException("New folder was not renamed.");
        await Edit(() => BeginContentCreate(NewContentKind.TextFile), "不应保存.txt", true);
        string defaultPath = Path.Combine(directory, "新建文本文档.txt");
        if (!File.Exists(defaultPath) || File.Exists(Path.Combine(directory, "不应保存.txt"))) throw new IOException("Esc did not retain the default file.");
        string item = spatial.ContentItemAtPath(PosterId!, defaultPath) ?? throw new IOException("No created item.");
        await Edit(() => BeginContentRename(item), "行内重命名.txt", false, collision: true);
        if (!File.Exists(Path.Combine(directory, "行内重命名.txt")) || File.Exists(defaultPath)) throw new IOException("Existing inline rename failed.");
        await Edit(() => BeginContentRename(item), "离开编辑提交.txt", false, dismiss: true);
        if (!File.Exists(Path.Combine(directory, "离开编辑提交.txt"))) throw new IOException("Light dismissal did not commit rename.");
    }
}
