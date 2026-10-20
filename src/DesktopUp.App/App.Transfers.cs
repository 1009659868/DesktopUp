using DesktopUp.Application;

namespace DesktopUp;

public partial class App
{
    private Task _transferDiscovery=Task.CompletedTask;
    private IContentMonitor? _managedContentMonitor;
    private IDisposable? _managedContentMonitorLease;
    private string? _lastManagedContentNotice;
    private long _observedContentVersion = -1;

    private void InvalidateObservationTargets(StateProjectionChangedEventArgs change)
    {
        // Binding/provisioning/relocation changes publish through the existing projection boundary.
        // Conservatively rediscover on item changes, but unchanged routes do NOT schedule all content.
        // In particular, publishing the monitor's own commit must not fan out to every directory again.
        if (change.CatalogChanged || _observedContentVersion != change.Projection.ContentVersion)
            _managedContentMonitor?.RefreshTargets();
        _observedContentVersion = change.Projection.ContentVersion;
    }

    private async Task DiscoverStartupTransfers(ContentStartupUseCase startup,
        Func<Action<ManagedContentSyncResult>, IContentMonitor> startMonitor)
    {
        try
        {
            var counts = await Task.Run(startup.Run);
            var returnNotices = counts.ReturnNotices;
            _stateCoordinator?.Refresh();
            if(returnNotices.Count > 0) _dispatcher?.TryEnqueue(() =>
            {
                if(Volatile.Read(ref _exiting) != 0)return;
                _controlCenter?.Show(); _controlCenter?.CurrentWindow?.ShowTransferFailure(string.Join("\n",returnNotices));
            });
            _log?.Write("info","transfer.startup_discovered",new {pending=counts.Pending,preserved=counts.Preserved,automaticFileReplay=false});
        }
        catch(Exception e){_log?.Write("warning","transfer.discovery_unavailable",new {e.Message,automaticFileReplay=false});}
        if (Volatile.Read(ref _exiting) == 0)
        {
            _managedContentMonitorLease = _ledger?.Acquire("watcher.managed-content", "coalesced-owned-collections");
            _managedContentMonitor = startMonitor(result => _dispatcher?.TryEnqueue(() =>
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
