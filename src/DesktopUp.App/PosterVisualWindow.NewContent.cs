using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private string? BeginContentCreate(NewContentKind kind)
    {
        if (_disposed || HasFileAction || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!)) return "请先完成当前操作。";
        SetFileActionBusy(true); spatial.TrackContentOperation(Observe()); return null;
        async Task Observe()
        {
            try
            {
                var location = await spatial.PrepareNewContent(PosterId!);
                if (location is null || _disposed) return;
                var input = await RequestNewContent(kind, location.Directory);
                if (input is null || _disposed) return;
                string path = await spatial.CreateContent(PosterId!, location, kind, input.Value.Name, input.Value.Target, _contentOpenCancellation.Token);
                // Publication has completed. Resolve the real relation, not an optimistic
                // placeholder; the same inline editor is used for existing and new objects.
                if (!_disposed && spatial.ContentItemAtPath(PosterId!, path) is { } itemId)
                    await RenameContentInline(itemId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!_disposed) await ShowFileActionError("未完成新建：" + e.Message); }
            finally { SetFileActionBusy(false); if (!_disposed) QueueContentProposalDrain(); }
        }
    }

    private async Task<(string Name, string? Target)?> RequestNewContent(NewContentKind kind, string directory)
    {
        if (_disposed || _contentOpenCancellation.IsCancellationRequested) return null;
        string title = kind switch { NewContentKind.Folder => "新建文件夹", NewContentKind.TextFile => "新建文本文档", _ => "新建快捷方式" };
        string initial = await spatial.SuggestContentName(directory, kind);
        if (_disposed || _contentOpenCancellation.IsCancellationRequested) return null;
        if (kind != NewContentKind.Shortcut) return (initial, null);
        var done = new TaskCompletionSource<(string, string?)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new Window { Title = "DesktopUp · " + title };
        var target = new TextBox { Header = "目标文件或文件夹", PlaceholderText = "本机绝对路径", MaxLength = 8192 };
        var file = new Button { Content = "选择文件…" }; var folder = new Button { Content = "选择文件夹…" };
        var pickers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; pickers.Children.Add(file); pickers.Children.Add(folder);
        var notice = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "使用此目标" }; var cancel = new Button { Content = "取消" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var panel = new StackPanel { Padding = new(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "选择快捷方式目标", FontSize = 20 }); panel.Children.Add(target); panel.Children.Add(pickers);
        panel.Children.Add(new TextBlock { Text = "只新建快捷方式，不移动或执行目标。\n位置：" + directory, TextWrapping = TextWrapping.Wrap, MaxHeight = 65 });
        panel.Children.Add(notice); panel.Children.Add(buttons); dialog.Content = panel;
        using var lease = ledger.Acquire("window.new-shortcut", PosterId!);
        using var closed = CancellationTokenSource.CreateLinkedTokenSource(_contentOpenCancellation.Token);
        bool picking = false; Task pickerTask = Task.CompletedTask;
        async Task Pick(NativePickerPurpose purpose)
        {
            if (picking || done.Task.IsCompleted) return;
            picking = true; file.IsEnabled = folder.IsEnabled = ok.IsEnabled = false;
            try
            {
                var picker = new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(dialog), false, ledger, log,
                    initiallyCancelled: closed.IsCancellationRequested, purpose: purpose);
                using var stop = closed.Token.Register(picker.Cancel);
                string? value = await picker.Completion;
                if (!done.Task.IsCompleted && value is not null)
                {
                    done.TrySetResult((initial, value)); dialog.Close();
                }
            }
            catch (Exception e) { if (!done.Task.IsCompleted) notice.Text = e.Message; }
            finally { picking = false; if (!done.Task.IsCompleted) file.IsEnabled = folder.IsEnabled = ok.IsEnabled = true; }
        }
        void Save()
        {
            if (picking) return;
            if (!Path.IsPathFullyQualified(target.Text)) { notice.Text = "请选择本机文件或文件夹的绝对路径。"; return; }
            done.TrySetResult((initial, target.Text)); dialog.Close();
        }
        file.Click += (_, _) => { pickerTask = Pick(NativePickerPurpose.ReferenceFile); };
        folder.Click += (_, _) => { pickerTask = Pick(NativePickerPurpose.ReferenceFolder); };
        ok.Click += (_, _) => Save(); cancel.Click += (_, _) => dialog.Close();
        panel.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Escape) { e.Handled = true; dialog.Close(); }
            else if (e.Key == global::Windows.System.VirtualKey.Enter) { e.Handled = true; Save(); }
        };
        dialog.Closed += (_, _) => { closed.Cancel(); done.TrySetResult(null); };
        dialog.AppWindow.Resize(new(540, 370)); dialog.AppWindow.Move(new(Math.Max(0, _bounds.X), Math.Max(0, _bounds.Y)));
        using var cancellation = closed.Token.Register(() => dialog.DispatcherQueue.TryEnqueue(() => { if (!done.Task.IsCompleted) dialog.Close(); }));
        dialog.Activate(); target.Focus(FocusState.Programmatic);
        var result = await done.Task;
        await pickerTask; // Exit waits until the owned native selector has released its lease.
        return result;
    }
}
