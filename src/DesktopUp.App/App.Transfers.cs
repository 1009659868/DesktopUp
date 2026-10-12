using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    private Task _transferDiscovery=Task.CompletedTask;
    private ManagedStorageService? _storageLocation;
    private async Task DiscoverStartupTransfers(SqliteStateStore store)
    {
        bool isolated=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT"));
        _storageLocation=new ManagedStorageService(store,isolated?StorageProfileKind.Isolated:
            DistributionBuild.ApplicationId.Contains("Preview",StringComparison.Ordinal)?StorageProfileKind.Preview:StorageProfileKind.Daily,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            isolated?Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_STORAGE_BASE"):null);
        try
        {
            var counts=await Task.Run(()=>
            {
                var settled=_storageLocation.Reconcile();
                var journal=new SqliteTransferJournal(store);
                var finished=new AutomaticTransferCompletion(journal).Settle();
                return new {pending=finished.Count(x=>!x.Item.IsFinished),preserved=finished.Count(x=>x.Kind==TransferFinishKind.Preserved),relocations=settled.Count};
            });
            _stateCoordinator?.Refresh();
            _log?.Write("info","transfer.startup_discovered",new {counts.pending,counts.preserved,automaticFileReplay=false});
        }
        catch(Exception e){_log?.Write("warning","transfer.discovery_unavailable",new {e.Message,automaticFileReplay=false});}
    }
}
