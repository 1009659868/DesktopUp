using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    private Task _transferDiscovery=Task.CompletedTask;
    private async Task DiscoverStartupTransfers(SqliteStateStore store)
    {
        try
        {
            var counts=await Task.Run(()=>
            {
                var journal=new SqliteTransferJournal(store);
                var finished=new AutomaticTransferCompletion(journal).Settle();
                return new {pending=finished.Count(x=>!x.Item.IsFinished),preserved=finished.Count(x=>x.Kind==TransferFinishKind.Preserved)};
            });
            _stateCoordinator?.Refresh();
            _log?.Write("info","transfer.startup_discovered",new {counts.pending,counts.preserved,automaticFileReplay=false});
        }
        catch(Exception e){_log?.Write("warning","transfer.discovery_unavailable",new {e.Message,automaticFileReplay=false});}
    }
}
