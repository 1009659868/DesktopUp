using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private SqliteStateStore? _transferStore;
    private Action? _refreshTransfers;
    private ManagedStorageService? _storageLocation;
    private CancellationTokenSource? _storageCancellation;
    internal void CancelStorageChange()=>_storageCancellation?.Cancel();
    private readonly CancellationTokenSource _transferStopping=new();
    internal void ConfigureTransfers(SqliteStateStore store,Action refresh,ManagedStorageService? storageLocation=null)
    {
        _transferStore=store;_refreshTransfers=refresh;_storageLocation=storageLocation;
        ((FrameworkElement)Content).Loaded+=async (_,_)=>await DiscoverTransferPending();
        Closed+=(_,_)=>_transferStopping.Dispose();
    }
    private async Task DiscoverTransferPending()
    {
        if(_transferStore is null)return;
        try
        {
            var rows=await Task.Run(()=>new SqliteTransferJournal(_transferStore).Read());
            if(_editingClosed||_portableStopping)return;
            // Exception-only locations, not an action/retry/task manager.
            TransferRecoveryMenu.Visibility=rows.Any(x=>x.Phase==DurableTransferPhase.Review &&
                x.Error?.StartsWith(SqliteTransferJournalNotice.UnmovedPrefix,StringComparison.Ordinal)!=true)
                ?Visibility.Visible:Visibility.Collapsed;
        }
        catch(Exception e){_portableLog?.Write("warning","transfer.locations_unavailable",new{e.Message});}
    }
    private static TextBlock TransferText(string text)=>new(){Text=text,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
    private async void ManagedStorage_Click(object sender,RoutedEventArgs e)
    {
        UiCGlobalSettings.Flyout?.Hide();
        await PortableAction(ShowStorageLocation);
    }

    internal async Task<RegisteredManagedStorage?> EnsureTransferStorageAsync(bool forceChoose=false)
    {
        if(_transferStore is null)return null;
        RegisteredManagedStorage? result=null;
        await PortableAction(async()=>
        {
            if(_storageLocation is null)return;
            var view=await Task.Run(_storageLocation.Read);
            if(_editingClosed||_portableStopping)return;
            if(view.NeedsSelection)
            {
                var panel=new StackPanel{Spacing=12};
                var roots=view.Choices.DistinctBy(x=>x.Root.Id).ToArray();
                var choice=new ComboBox{Header="已有收纳位置",MinWidth=390,
                    ItemsSource=roots.Select(x=>x.Root.Locator).ToArray(),SelectedIndex=0};
                panel.Children.Add(choice);
                panel.Children.Add(TransferText("仅选择新增内容的位置，不合并或搬动旧目录。整库更改请使用“更改并迁移”。"));
                if(!await ReferenceDialog("选择已有位置",panel,"使用此位置"))return;
                int selected=choice.SelectedIndex;
                await Task.Run(()=>_storageLocation.SelectExisting(roots[selected].Root.Id));
            }
            result=await Task.Run(()=>_storageLocation.EnsureReady(_transferStopping.Token));
            _refreshTransfers?.Invoke();
        });
        return result;
    }

    private async Task ShowStorageLocation()
    {
        if(_storageLocation is null)return;
        var view=await Task.Run(_storageLocation.Read);
        if(_editingClosed||_portableStopping)return;
        var panel=new StackPanel{Spacing=12,MinWidth=420,MaxWidth=560};
        var line=new Grid{ColumnSpacing=10};line.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        line.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        line.Children.Add(TransferText(view.Path));
        var open=new Button{Content="打开",IsEnabled=view.Exists,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(open,1);line.Children.Add(open);
        open.Click+=async(_,_)=>
        {
            try
            {
                var current=await Task.Run(_storageLocation.Read);
                if(!current.Exists)return;
                var folder=await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(current.Path);
                await global::Windows.System.Launcher.LaunchFolderAsync(folder);
            }
            catch(Exception error){StateInfoBar.IsOpen=true;StateInfoBar.Message=error.Message;}
        };
        panel.Children.Add(line);
        if(!view.Exists&&!view.NeedsSelection)panel.Children.Add(TransferText("首次收纳时自动创建。文件与安装、配置及缓存分开保存。"));
        var roots=view.Choices.DistinctBy(x=>x.Root.Id).ToArray();ComboBox? choice=null;
        if(view.NeedsSelection)
        {choice=new(){Header="选择用于新增收纳的旧位置（不搬文件）",ItemsSource=roots.Select(x=>x.Root.Locator).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(choice);}
        if(view.Notice is not null)panel.Children.Add(TransferText(view.Notice));
        var dialog=new ContentDialog{XamlRoot=Content.XamlRoot,Title="收纳位置 · 全局",Content=panel,
            PrimaryButtonText="更改并迁移…",SecondaryButtonText=view.NeedsSelection?"使用选中旧位置":"",CloseButtonText="关闭",DefaultButton=ContentDialogButton.Close};
        ApplyPaperDialogStyle(dialog);_portableDialog=dialog;
        ContentDialogResult answer;
        try{answer=await dialog.ShowAsync();}finally{if(ReferenceEquals(_portableDialog,dialog))_portableDialog=null;}
        if(_editingClosed||_portableStopping)return;
        if(answer==ContentDialogResult.Secondary)
        {int selected=choice!.SelectedIndex;await Task.Run(()=>_storageLocation.SelectExisting(roots[selected].Root.Id));_refreshTransfers?.Invoke();return;}
        if(answer!=ContentDialogResult.Primary)return;
        string? path=await PickPortableFile(false,purpose:NativePickerPurpose.ManagedStorageFolder);
        if(path is null||_editingClosed||_portableStopping)return;
        var preview=await Task.Run(()=>_storageLocation.PreviewChange(path));
        string affected=preview.Posters.Count==0?"无海报内容受影响":string.Join("、",preview.Posters);
        if(!await PortableDialog("确认更改收纳位置",$"目标：{preview.Destination}\n\n{preview.Files} 个文件，{preview.Directories} 个文件夹，{preview.Bytes:N0} 字节（不含程序归属记录）。\n影响海报：{affected}\n\n迁移全部受管集合；非收纳引用的原文件不动。准备完成后一次切换，无法核实的副本保留。", "更改并迁移"))return;
        await RunStorageChange(preview);
    }
    private async Task RunStorageChange(StorageChangePreview preview)
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(_transferStopping.Token);
        _storageCancellation=cancel;
        var progress=new ContentDialog{XamlRoot=Content.XamlRoot,Title="正在更改收纳位置",CloseButtonText="取消",
            Content=new StackPanel{Spacing=14,Children={new ProgressBar{IsIndeterminate=true},TransferText("正在准备并核实文件。提交前取消保留原位置；提交后取消保留新位置和剩余旧副本。")}}};
        ApplyPaperDialogStyle(progress);_portableDialog=progress;
        progress.CloseButtonClick+=(_,_)=>CancelStorageChange();
        var shown=progress.ShowAsync();StorageChangeResult? result=null;
        try{result=await Task.Run(()=>_storageLocation!.Change(preview,preview.Fingerprint,cancel.Token));}
        finally{_storageCancellation=null;progress.Hide();await shown;if(ReferenceEquals(_portableDialog,progress))_portableDialog=null;_refreshTransfers?.Invoke();}
        _portableLog?.Write("info","storage.location_changed",new{result.Committed,result.Cleaned,retained=result.RetainedPaths.Count});
        if(!_editingClosed&&!_portableStopping)await PortableDialog("收纳位置",result.Message+
            (result.RetainedPaths.Count==0?"":"\n\n"+string.Join("\n",result.RetainedPaths)));
    }
    internal async Task<bool> ConfirmDirectTransferAsync(string message)
    {
        bool confirmed=false;
        await PortableAction(async()=>confirmed=await PortableDialog("确认移动",message,"移动"));
        return confirmed;
    }
    internal void ShowTransferFailure(string message)
    {
        if(_editingClosed||_portableStopping)return;
        StateInfoBar.IsOpen=true;StateInfoBar.Severity=InfoBarSeverity.Warning;
        StateInfoBar.Title="本次收纳未能全部完成";StateInfoBar.Message=message;
        _=DiscoverTransferPending();
    }
    private async void TransferRecovery_Click(object sender,RoutedEventArgs e)=>await PortableAction(async()=>
    {
        if(_transferStore is null)return;
        var rows=await Task.Run(()=>new SqliteTransferJournal(_transferStore).Read());
        var exceptions=rows.Where(x=>x.Phase==DurableTransferPhase.Review &&
            x.Error?.StartsWith(SqliteTransferJournalNotice.UnmovedPrefix,StringComparison.Ordinal)!=true).TakeLast(30);
        await PortableDialog("异常文件位置", "这些操作未能确认完成，程序未删除或覆盖文件。可复制位置到资源管理器查看，再重新拖动。\n\n"+
            string.Join("\n\n",exceptions.Select(x=>$"{Path.GetFileName(x.File.Source)}\n原位置：{x.File.Source}\n目标位置：{x.File.Destination}")));
    });
}
// User presentation does not expose the journal's phase/closure controls.
internal static class SqliteTransferJournalNotice { internal const string UnmovedPrefix="自动收尾：原文件未移动；"; }
