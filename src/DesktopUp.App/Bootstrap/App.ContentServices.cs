using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    // Called at the original startup point after projection initialization.
    // App retains worker ownership and its existing StopAsync / operation-drain order.
    private void ConfigureRealContentServices(PosterSpatialRuntime runtime)
    {
        var services = _services!;
        var store = services.GetRequiredService<SqliteStateStore>();
        var coordinator = services.GetRequiredService<IStateProjectionCoordinator>();
        var journal = new SqliteContentOpenJournal(store);
        journal.RetireInterruptedRequests();
        _realContentWorker = new LocalContentWorkerClient(
            ContentWorkerLocation.InApplication(AppContext.BaseDirectory), _ledger);
        var actions = new ContentActionService(
            () => coordinator.TryGetCurrent(out var current) ? current!.ContentCatalog : ContentCatalogSnapshot.Empty,
            _realContentWorker, journal);
        runtime.ConfigureRealContent(_realContentWorker, actions, new SqliteContentIconStore(store));
    }
}
