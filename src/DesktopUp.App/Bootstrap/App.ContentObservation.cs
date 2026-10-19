using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    // One application use case for first collection, global settings and startup reconciliation.
    private StorageLocationUseCase? _storageLocation;
    private ITransferLocationNotices? _transferLocationNotices;

    private Task StartContentDiscovery()
    {
        var store = _composition!.Store;
        bool isolated = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT")) ||
            DistributionBuild.ReviewStorageBase is not null;
        _storageLocation = new StorageLocationUseCase(new SqliteStorageLocationStorage(store, isolated ? StorageProfileKind.Isolated :
            DistributionBuild.ApplicationId.Contains("Preview", StringComparison.Ordinal) ? StorageProfileKind.Preview : StorageProfileKind.Daily,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            isolated ? Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_STORAGE_BASE") ?? DistributionBuild.ReviewStorageBase : null),
            new StorageRelocationUseCase(new LocalStorageRelocationExecution(store)));
        _transferLocationNotices = new SqliteTransferLocationNotices(store);
        var startup = new ContentStartupUseCase(new SqliteContentStartupSteps(store, _storageLocation, ContentReturns), _log);
        return DiscoverStartupTransfers(startup, completed =>
        {
            var files = new LocalContentObservationFiles();
            var observation = new ContentObservationService(
                new ManagedContentSyncUseCase(new SqliteManagedContentObservationStorage(store), files),
                new MappedDirectoryUseCase(new SqliteMappedDirectoryStorage(store), files));
            return new ManagedContentMonitor(observation, completed);
        });
    }
}
