using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    private IStateBackupRecovery CreateOfflineRecovery(IUserDataPaths paths) =>
        new StateBackupRecovery(paths, () => _isPrimary && !_storageInitialized);

    private static ContentReferenceRepairUseCase CreateReferenceRepair(IStateCommandService commands) =>
        new(new LocalContentReferenceSource(), commands);

    private static IContentIconSource CreateContentIconSource() => new LocalContentIconSource();

    private void ConfigurePortableProfiles()
    {
        var services = _composition ?? throw new InvalidOperationException("Application services are not ready.");
        var coordinator = services.Projection;
        bool Guard(PortableImportPlan plan) => Volatile.Read(ref _exiting) == 0 &&
            _spatialRuntime?.CanApplyPortable(plan) == true;
        coordinator.BindPortableRuntime(Guard);
        _portableController = new(services.Repository, coordinator,
            services.StateStore, new PortableProfileFile(services.Paths),
            () => _spatialRuntime?.PortableTopology(), Guard);
    }


}
