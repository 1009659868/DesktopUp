using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    private Task _transferDiscovery=Task.CompletedTask;
    private ManagedStorageService? _storageLocation;
    private ManagedContentMonitor? _managedContentMonitor;
    private IDisposable? _managedContentMonitorLease;
    private string? _lastManagedContentNotice;
    private async Task DiscoverStartupTransfers(SqliteStateStore store)
    {
        IReadOnlyList<string> returnNotices = [];
        bool isolated=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT")) || DistributionBuild.ReviewStorageBase is not null;
        _storageLocation=new ManagedStorageService(store,isolated?StorageProfileKind.Isolated:
            DistributionBuild.ApplicationId.Contains("Preview",StringComparison.Ordinal)?StorageProfileKind.Preview:StorageProfileKind.Daily,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            isolated?Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_STORAGE_BASE") ?? DistributionBuild.ReviewStorageBase:null);
        try
        {
            var counts=await Task.Run(()=>
            {
                var settled=_storageLocation.Reconcile();
                var journal=new SqliteTransferJournal(store);
                var finished=new AutomaticTransferCompletion(journal).Settle();
                SingleOwnerMigration.Apply(store);
                new ContentRenameService(store).Reconcile();
                returnNotices = new PosterContentReturn(store).Reconcile();
                foreach(string notice in returnNotices)
                    _log?.Write("warning","content.return_preserved",new{notice});
                var crossNotices=new CrossPosterTransfer(store).Reconcile(); // Index only; never replay a Shell move.
                foreach(string notice in crossNotices)_log?.Write("warning","transfer.cross_poster_preserved",new{notice});
                new MappedDirectoryIngress(store).SettleInterrupted();
                // Only previously released, originally live moves are observed. Old missing
                // cards and crash-before-release intents are NEVER auto-removed (DEC-047).
                new NativeShellExitJournal(store).Reconcile();
                return new {pending=finished.Count(x=>!x.Item.IsFinished),preserved=finished.Count(x=>x.Kind==TransferFinishKind.Preserved),relocations=settled.Count};
            });
            _stateCoordinator?.Refresh();
            if(returnNotices.Count > 0) _dispatcher?.TryEnqueue(() =>
            {
                if(Volatile.Read(ref _exiting) != 0)return;
                _controlCenter?.Show(); _controlCenter?.WindowForPortableProbe?.ShowTransferFailure(string.Join("\n",returnNotices));
            });
            _log?.Write("info","transfer.startup_discovered",new {counts.pending,counts.preserved,automaticFileReplay=false});
        }
        catch(Exception e){_log?.Write("warning","transfer.discovery_unavailable",new {e.Message,automaticFileReplay=false});}
        if (Volatile.Read(ref _exiting) == 0)
        {
            _managedContentMonitorLease = _ledger?.Acquire("watcher.managed-content", "coalesced-owned-collections");
            _managedContentMonitor = new ManagedContentMonitor(store, result => _dispatcher?.TryEnqueue(() =>
            {
                if (Volatile.Read(ref _exiting) != 0) return;
                _spatialRuntime?.PublishMappingStates(result.MappingStates);
                if (result.Changed)
                {
                    _stateCoordinator?.Refresh();
                    _log?.Write("info", "content.external_sync", new { result.RemovedCards, result.RenamedItems, fileActions = false });
                }
                string? notice = result.Notices.Count == 0 ? null : string.Join("\n", result.Notices);
                if (notice is not null && notice != _lastManagedContentNotice)
                    _log?.Write("warning", "content.external_sync_deferred", new { notice });
                _lastManagedContentNotice = notice;
            }));
        }
    }
}
