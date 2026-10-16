using DesktopUp.Application;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

// Composition only. Runtime use-case extraction is a separate migration.
public partial class App
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        // Explicit child-process test override; the opt-in preview build has its own profile.
        services.AddSingleton<IUserDataPaths>(new UserDataPaths(
            Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") ?? DistributionBuild.DefaultDataRoot));
        services.AddSingleton<IStructuredLog, JsonLineLog>();
        services.AddSingleton<IResourceLedger, ResourceLedger>();
        services.AddSingleton<SqliteStateStore>();
        services.AddSingleton<IStateStore>(static provider =>
            provider.GetRequiredService<SqliteStateStore>());
        services.AddSingleton<IPosterRepository, SqlitePosterRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<
            IStateProjectionRepository,
            SqliteStateProjectionRepository>();
        services.AddSingleton<StateProjectionCoordinator>();
        services.AddSingleton<IStateCommandRepository, SqliteStateCommandRepository>();
        services.AddSingleton<IStateProjectionCoordinator>(static provider =>
            provider.GetRequiredService<StateProjectionCoordinator>());
        services.AddSingleton<IStateCommandService>(static provider =>
            provider.GetRequiredService<StateProjectionCoordinator>());
        services.AddSingleton<IDesktopShellLocator, ExplorerDesktopLocator>();
        services.AddSingleton<IDesktopLayerAttachment, DesktopLayerAttachment>();
        services.AddSingleton<IDisplayTopologyService, DisplayTopologyService>();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
