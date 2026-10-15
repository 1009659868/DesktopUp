using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private string? BeginContentRename(string itemId)
    {
        if(_disposed || HasFileAction || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!))return "请先完成当前操作。";
        if(spatial.ContentActionRelation(PosterId!,itemId) is null)return "来源已变化，请重新选择。";
        SetFileActionBusy(true); spatial.TrackContentOperation(Observe());return null;
        async Task Observe()
        {
            try
            {
                await RenameContentInline(itemId);
            }
            catch(Exception e) { if(!_disposed)await ShowFileActionError("未完成重命名："+e.Message); }
            finally { SetFileActionBusy(false);if(!_disposed)QueueContentProposalDrain(); }
        }
    }

    private async Task ShowFileActionError(string message)
    {
        log.Write("warning","content.file_action_failed",new{PosterId,message});
        if(_disposed || _contentOpenCancellation.IsCancellationRequested)return;
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog=new Window {Title="DesktopUp · 文件操作"};var close=new Button {Content="关闭",HorizontalAlignment=HorizontalAlignment.Right};
        var panel=new StackPanel {Padding=new(20),Spacing=20};
        panel.Children.Add(new TextBlock {Text=message,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true});panel.Children.Add(close);dialog.Content=panel;
        using var lease=ledger.Acquire("window.file-error",PosterId!);
        close.Click+=(_,_)=>dialog.Close();dialog.Closed+=(_,_)=>done.TrySetResult();
        dialog.AppWindow.Resize(new(500,260));dialog.AppWindow.Move(new(Math.Max(0,_bounds.X),Math.Max(0,_bounds.Y)));
        using var registration=_contentOpenCancellation.Token.Register(()=>dialog.DispatcherQueue.TryEnqueue(()=>{if(!done.Task.IsCompleted)dialog.Close();}));
        dialog.Activate();await done.Task;
    }
}
