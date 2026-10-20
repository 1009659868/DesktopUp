using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public partial class App
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_isPrimary)
        {
            if (IsUninstallMode) Environment.ExitCode = 31;
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        if (!IsUninstallMode)
        {
            try { _profileLifecycleLease = ProfileLifecycleGate.Acquire(new UserDataPaths(Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") ?? DistributionBuild.DefaultDataRoot).Root); }
            catch (IOException) { Environment.ExitCode = 31; _singleInstance.Dispose(); Exit(); return; }
            catch (UnauthorizedAccessException) { Environment.ExitCode = 31; _singleInstance.Dispose(); Exit(); return; }
        }
        _composition = new ApplicationComposition();
        _log = _composition.Log;
        if (IsUninstallMode) { RunUninstallPreparation(); return; }
        _ledger = _composition.Ledger;
        if (TryEnterOfflineRecovery()) return;
        PosterAnimationOptions animationOptions;
        try
        {
            var flags = _startupIntent.Arguments.Where(a => a.StartsWith("--animation-config=", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (flags.Length > 1) throw new ArgumentException("Specify --animation-config only once.");
            string userPath = Path.Combine(_composition.Paths.Root, "poster-animation.json");
            string configPath = flags.Length == 1 ? Path.GetFullPath(flags[0]["--animation-config=".Length..]) :
                File.Exists(userPath) ? userPath : Path.Combine(AppContext.BaseDirectory, "config", "poster-animation.json");
            animationOptions = PosterAnimationConfiguration.Read(configPath, required: flags.Length == 1);
            _log.Write("info", "configuration.posture_animation", new { path = configPath, options = animationOptions, startupOnly = true });
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _log.Write("critical", "configuration.posture_animation_invalid", new { exception.Message });
            ExitApplication(); return;
        }
        StateStoreInitializationResult stateInitialization =
            _composition.StateStore.Initialize();
        if (!stateInitialization.Succeeded)
        {
            _log.Write("critical", "application.state_store_unavailable", new
            {
                stateInitialization.PreviousSchemaVersion,
                stateInitialization.Error
            });
            _composition.Dispose();
            OpenOfflineRecovery(stateInitialization.Error ?? "状态库无法打开。");
            return;
        }
        _storageInitialized = true;
        _transferDiscovery=StartContentDiscovery();
        try
        {
            _stateCoordinator = _composition.Projection;
            _stateCoordinator.EnsureInitialized();
            SettingsStateSnapshot settings =
                _composition.Settings.Read();
            _log.Write("info", "state_repositories.ready", new
            {
                settings.Revision,
                settings.SettingsRevision,
                settings.Settings.DynamicEffectsPaused,
                performanceMode = settings.Settings.PerformanceMode.ToString()
            });
        }
        catch (Exception exception)
        {
            _log.Write("critical", "application.state_repository_unavailable", new
            {
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message
            });
            ExitApplication();
            return;
        }
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.XamlResourceReferenceFailed += OnXamlResourceReferenceFailed;
        _stateCommands = _composition.Projection;
        ConfigurePortableProfiles();
        _stateCoordinator.ProjectionChanged += OnStateProjectionChanged;
        _controlCenter = new ControlCenterService(
            _ledger,
            _log,
            ExecuteControlCenterPosterCommand,
            () => ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter),
            _stateCoordinator, _stateCommands, _inputRouter,
            target => _spatialRuntime?.GetEffectiveLayout(target), () => _spatialRuntime, _portableController,
            _transferLocationNotices,_storageLocation, () => _composition.Worker, ContentReturns,
            () => RequestReplacement(true), CreateContentIconSource(), CreateReferenceRepair(_stateCommands),
            new StartupRegistrationService(new WindowsStartupRegistration(AppContext.BaseDirectory,
                DistributionBuild.IsInstallable, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT")))));
        _tray = new TrayService(_ledger, _log);
        _tray.Create(QueueTrayAction);
        _log.Write("info", "application.started", new
        {
            version = typeof(App).Assembly.GetName().Version?.ToString(),
            distribution = DistributionBuild.ApplicationId,
            creationDefaults = DistributionBuild.CreationDefaults,
            runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            dataRoot = _composition.Paths.Root,
            isElevated = IsProcessElevated(),
            requestedExecutionLevel = "asInvoker"
        });
        if (DistributionBuild.CreationDefaultsError is { } defaultsError)
            _log.Write("warning", "configuration.creation_defaults_ignored", new { error = defaultsError });
        _spatialRuntime = new PosterSpatialRuntime(_stateCoordinator, _stateCommands, _log, animationOptions,
            () => _posterSessions?.GloballyHidden == true, id => _controlCenter?.HasPosterDraft(id) == true,
            (id, entity, document) => _controlCenter?.OwnsContentFieldDraft(id, entity, document) == true);
        _spatialRuntime.EffectiveLayoutChanged += RefreshControlCenterRuntime;
        ConfigureRealContentServices(_spatialRuntime);
        _spatialRuntime.ConfigureSceneMotionLedger(_ledger);
        _spatialRuntime.ConfigureTransfers(_transferDiscovery,
            async()=>
            {
                if(_storageLocation is null)return null;
                var location=await Task.Run(_storageLocation.Read);
                if(!location.NeedsSelection)return await Task.Run(()=>_storageLocation.EnsureReady());
                _controlCenter.Show();await Task.Yield();
                return _controlCenter.CurrentWindow is {} center?await center.EnsureTransferStorageAsync():null;
            },
            ()=>_controlCenter.IsSettingsInteractionActive||_controlCenter.IsRemovalInteractionActive,
            message=>{_controlCenter.Show();_controlCenter.CurrentWindow?.ShowTransferFailure(message);});
        StartAutoPosture();
        _spatialRuntime.RecoveryRequired += () => QueueDesktopRecovery(
            DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
            "spatial-physical-recovery", invalidateShell: true);
        _posterSessions = new PosterSessionRegistry(new WinUiPosterSessionFactory(
            _composition.Attachment, _stateCommands, _ledger, _log,
            ShowControlCenter, OnPosterEditingChanged, args => OnDesktopEnvironmentChanged(this, args), _spatialRuntime,
            _inputRouter, request => _controlCenter?.RequestRemoval(request)), _log);
        StartDesktopEnvironmentMonitoring();
        QueueDesktopRecovery(DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
            "startup", invalidateShell: false);

        DispatchIntent(_startupIntent);
        while (_pendingIntents.TryDequeue(out StartupIntent? pending))
        {
            DispatchIntent(pending);
        }

    }
}
