using DesktopUp.Application;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;

namespace DesktopUp;

// Explicit composition, not a runtime service locator. Lazy roots preserve maintenance/offline
// startup. Access is confined to the owning UI thread: neither the state store nor the ledger nor desktop adapters are created until needed.
internal sealed class ApplicationComposition : IDisposable
{
    internal IUserDataPaths Paths { get; } = new UserDataPaths(
        Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") ?? DistributionBuild.DefaultDataRoot);
    internal IStructuredLog Log => _log ??= new JsonLineLog(Paths);
    internal IResourceLedger Ledger => _ledger ??= new ResourceLedger(Paths);
    // Concrete store access is confined to the other Bootstrap composition files.
    internal SqliteStateStore Store => _store ??= new SqliteStateStore(Paths, Log);
    internal IStateStore StateStore => Store;
    internal ISettingsRepository Settings => _settings ??= new SqliteSettingsRepository(Store, Log);
    internal IStateCommandRepository Repository => _repository ??= new SqliteStateCommandRepository(Store);
    internal StateProjectionCoordinator Projection => _projection ??= new StateProjectionCoordinator(Repository, Log);
    internal IDesktopShellLocator Shell => _shell ??= new ExplorerDesktopLocator();
    internal IDisplayTopologyService Displays => _displays ??= new DisplayTopologyService();
    internal IDesktopLayerAttachment Attachment => _attachment ??= new DesktopLayerAttachment();

    internal ILocalContentWorker? Worker => _worker;
    internal ILocalContentWorker StartWorker() => _worker ??= new LocalContentWorkerClient(
        ContentWorkerLocation.InApplication(AppContext.BaseDirectory), Ledger);
    internal Task StopWorkerAsync() => _worker?.StopAsync() ?? Task.CompletedTask;
    internal IContentActivityStore ActivityStore => _activityStore ??= new SqliteContentActivityStore(Store);
    internal ContentActivityConsumer Activity => _activity ??= new(ActivityStore,
        error => Log.Write("warning", "content.activity_projection_failed", new { error.Message }));
    internal Task StopActivityAsync() => _activity?.StopAsync() ?? Task.CompletedTask;

    private LocalContentWorkerClient? _worker;
    private IContentActivityStore? _activityStore;
    private ContentActivityConsumer? _activity;
    private IStructuredLog? _log;
    private IResourceLedger? _ledger;
    private SqliteStateStore? _store;
    private ISettingsRepository? _settings;
    private IStateCommandRepository? _repository;
    private StateProjectionCoordinator? _projection;
    private IDesktopShellLocator? _shell;
    private IDisplayTopologyService? _displays;
    private IDesktopLayerAttachment? _attachment;

    // App drains operations, sessions and Worker before closing this sole database owner.
    // Repeated early-failure/exit disposal is safe, and never creates an unopened store.
    public void Dispose()
    {
        _activity?.StopAsync().GetAwaiter().GetResult();
        _worker?.Dispose();
        _store?.Dispose();
    }
}
