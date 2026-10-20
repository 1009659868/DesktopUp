using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;
internal sealed partial class PosterVisualWindow
{
    // Shown only AFTER release for truly missing references; never interrupts held drag.
    internal async Task<bool> ConfirmMissingReferenceAsync(string message,CancellationToken stopping)
    {
        if(_disposed||stopping.IsCancellationRequested)return false;
        var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog=new Window {Title="DesktopUp · 移除失效引用"};
        var cancel=new Button {Content="取消",HorizontalAlignment=HorizontalAlignment.Right};
        var move=new Button {Content="移除卡片",HorizontalAlignment=HorizontalAlignment.Right};
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Right};
        buttons.Children.Add(cancel);buttons.Children.Add(move);
        var panel=new StackPanel {Spacing=18,Padding=new(20)};
        panel.Children.Add(new TextBlock {Text="移除失效引用？",FontSize=20});
        panel.Children.Add(new ScrollViewer {MaxHeight=300,Content=new TextBlock {Text=message,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true}});
        panel.Children.Add(buttons);dialog.Content=panel;
        using var lease=ledger.Acquire("window.transfer-confirmation",PosterId!);
        cancel.Click+=(_,_)=>dialog.Close();move.Click+=(_,_)=>{done.TrySetResult(true);dialog.Close();};
        panel.KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape){e.Handled=true;dialog.Close();}};
        dialog.Closed+=(_,_)=>done.TrySetResult(false);
        dialog.AppWindow.Resize(new(500,440));
        dialog.AppWindow.Move(new(Math.Max(0,_bounds.X),Math.Max(0,_bounds.Y)));
        using var registration=stopping.Register(()=>dialog.DispatcherQueue.TryEnqueue(()=>{if(!done.Task.IsCompleted)dialog.Close();}));
        dialog.Activate();cancel.Focus(FocusState.Programmatic);
        return await done.Task;
    }
}
