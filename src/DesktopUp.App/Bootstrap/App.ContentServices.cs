using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    // Called at the original startup point after projection initialization.
    // App retains worker ownership and its existing StopAsync / operation-drain order.
    private void ConfigureRealContentServices(PosterSpatialRuntime runtime)
    {
        var services = _composition!;
        var store = services.Store;
        var coordinator = services.Projection;
        var journal = new SqliteContentOpenJournal(store);
        journal.RetireInterruptedRequests();
        runtime.ConfigureActivity(new ContentActivityProducer(services.ActivityStore, services.Activity, ContentModelRegistry.BuiltIns()));
        services.Activity.StatisticsChanged += ids =>
            _dispatcher!.TryEnqueue(() => runtime.NotifyActivityStatisticsChanged(ids));
        services.Activity.RequestRefresh();
        var worker = _composition!.StartWorker();
        var actions = new ContentActionService(
            () => coordinator.TryGetCurrent(out var current) ? current!.ContentCatalog : ContentCatalogSnapshot.Empty,
            worker, journal, services.Activity);
        var files = new LocalContentActionFiles();
        var shellFiles = new ShellDirectoryTransfer();
        runtime.ConfigureContentReturns(ContentReturns);
        runtime.ConfigureMappingUseCase(new MappedDirectoryUseCase(
            new SqliteMappedDirectoryStorage(store), new LocalContentObservationFiles()));
        runtime.ConfigureTransferUseCases(
            new ManagedIngressUseCase(new SqliteManagedIngressStorage(store), files),
            new MappedIngressUseCase(new MappedDirectoryIngress(store), shellFiles),
            new CrossPosterTransferUseCase(new CrossPosterTransfer(store), shellFiles),
            new NativeShellExitUseCase(new NativeShellExitJournal(store)));
        runtime.ConfigureFileActions(new ContentFileActionService(
            new SqliteContentFileActionContext(store), files,
            new ContentCreationUseCase(new SqliteContentCreationStorage(store), files,
                new MappedContentCreationUseCase(new SqliteMappedDirectoryStorage(store),
                    new LocalContentObservationFiles(), new LocalOwnedContentCreationFiles())),
            new ContentRenameUseCase(new SqliteContentRenameJournal(store), files),
            new ContentDeletionUseCase(new SqliteContentDeletionStorage(store), files, new ShellDirectoryTransfer()),
            new ShellUiClient(ContentWorkerLocation.InApplication(AppContext.BaseDirectory), _ledger!)));
        runtime.ConfigureRealContent(worker, actions, new SqliteContentIconStore(store));
    }
}
