using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private SqliteStateStore? _transferStore;
    private Action? _refreshTransfers;
    private readonly CancellationTokenSource _transferStopping=new();
    internal bool TransferGlobalEntryForProbe=>_transferStore is not null && MoreActionsButton.IsEnabled;
    internal void ConfigureTransfers(SqliteStateStore store,Action refresh)
    {
        _transferStore=store;_refreshTransfers=refresh;
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
    private async void ManagedStorage_Click(object sender,RoutedEventArgs e)=>await EnsureTransferStorageAsync(forceChoose:true);

    internal async Task<RegisteredManagedStorage?> EnsureTransferStorageAsync(bool forceChoose=false)
    {
        if(_transferStore is null)return null;
        RegisteredManagedStorage? result=null;
        await PortableAction(async()=>
        {
            var locations=await Task.Run(()=>new SqliteTransferJournal(_transferStore).StorageLocations());
            if(!forceChoose && locations.Count==1){result=locations[0];return;}
            if(locations.Count>0)
            {
                var panel=new StackPanel{Spacing=12};
                var choice=new ComboBox{Header="收纳文件保存位置",MinWidth=390,
                    ItemsSource=locations.Select(x=>x.Collection.StorageLocator).Append("选择新的存储目录…").ToArray(),SelectedIndex=0};
                panel.Children.Add(choice);
                panel.Children.Add(TransferText("实际文件保存在这里，与安装和缓存分离。不会自动迁移已有文件。"));
                if(!await ReferenceDialog("收纳存储",panel,"使用此位置"))return;
                if(choice.SelectedIndex<locations.Count){result=locations[choice.SelectedIndex];return;}
            }
            var picker=new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(this),false,_portableLedger!,_portableLog!,purpose:NativePickerPurpose.ManagedStorageFolder);
            _portablePicker=picker;
            string? path;
            try{path=await picker.Completion;}finally{if(ReferenceEquals(_portablePicker,picker))_portablePicker=null;}
            if(path is null||_editingClosed||_portableStopping)return;
            var provisioner=new ManagedStorageProvisioner(_transferStore);
            var preview=await Task.Run(()=>provisioner.Preview(path));
            if(!await PortableDialog("设置收纳位置",preview.CollectionPath+"\n\n将在这里保存拖入的实际文件。已有文件不移动；请选与来源同盘的持久目录。","使用此位置"))return;
            var registered=await Task.Run(()=>{_transferStopping.Token.ThrowIfCancellationRequested();return provisioner.Confirm(preview,preview.Fingerprint);});
            result=new(registered.Root,registered.Collection);
            _refreshTransfers?.Invoke();
        });
        return result;
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
