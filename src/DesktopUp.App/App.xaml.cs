using System.Collections.Concurrent;
using System.Security.Principal;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly StartupIntent _startupIntent;
    private readonly SingleInstanceCoordinator _singleInstance;
    private readonly ConcurrentQueue<StartupIntent> _pendingIntents = new();
    private readonly bool _isPrimary;
    private DispatcherQueue? _dispatcher;
    private ServiceProvider? _services;
    private ControlCenterService? _controlCenter;
    private TrayService? _tray;
    private PosterSessionRegistry? _posterSessions;
    private PosterSpatialRuntime? _spatialRuntime;
    private readonly PosterInputRouter _inputRouter = new();
    private bool _controlCenterEnvironmentPending = true;
    private Window? _desktopEnvironmentAnchor;
    private IDisposable? _desktopEnvironmentAnchorLease;
    private IStateProjectionCoordinator? _stateCoordinator;
    private IStateCommandService? _stateCommands;
    private IStructuredLog? _log;
    private IResourceLedger? _ledger;
    private DesktopEnvironmentWatcher? _desktopEnvironmentWatcher;
    private IDisposable? _desktopEnvironmentWatcherLease;
    private DispatcherQueueTimer? _desktopHealthTimer;
    private IDisposable? _desktopHealthTimerLease;
    private DispatcherQueueTimer? _desktopRecoveryTimer;
    private IDisposable? _desktopRecoveryTimerLease;
    private CancellationTokenSource? _desktopEnvironmentCancellation;
    private readonly DesktopRecoveryVersionGate _desktopRecoveryVersionGate = new();
    private readonly HashSet<string> _pendingDesktopRecoveryReasons =
        new(StringComparer.Ordinal);
    private DesktopEnvironmentChangeKind _pendingDesktopRecoveryKinds;
    private int _activeShellRecoveries;
    private int _activeDisplayRecoveries;
    private readonly List<Task> _desktopRecoveryTasks = new();
    private int _exiting;

    public App()
    {
        InitializeComponent();
        var parser = new StartupIntentParser();
        _startupIntent = parser.Parse(Environment.GetCommandLineArgs().Skip(1));
        _singleInstance = new SingleInstanceCoordinator(SingleInstanceNames.ForCurrentUser(DistributionBuild.ApplicationId));
        _singleInstance.IntentReceived += OnExternalIntent;
        _isPrimary = _singleInstance.TryAcquireOrForward(_startupIntent.ForExistingInstance());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_isPrimary)
        {
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _services = BuildServices();
        _log = _services.GetRequiredService<IStructuredLog>();
        _ledger = _services.GetRequiredService<IResourceLedger>();
        if (TryEnterOfflineRecovery()) return;
        PosterAnimationOptions animationOptions;
        try
        {
            var flags = _startupIntent.Arguments.Where(a => a.StartsWith("--animation-config=", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (flags.Length > 1) throw new ArgumentException("Specify --animation-config only once.");
            string userPath = Path.Combine(_services.GetRequiredService<IUserDataPaths>().Root, "poster-animation.json");
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
            _services.GetRequiredService<IStateStore>().Initialize();
        if (!stateInitialization.Succeeded)
        {
            _log.Write("critical", "application.state_store_unavailable", new
            {
                stateInitialization.PreviousSchemaVersion,
                stateInitialization.Error
            });
            _services.GetRequiredService<SqliteStateStore>().Dispose();
            OpenOfflineRecovery(stateInitialization.Error ?? "状态库无法打开。");
            return;
        }
        _storageInitialized = true;
        try
        {
            SettingsStateSnapshot settings =
                _services.GetRequiredService<ISettingsRepository>().Read();
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
        _stateCoordinator = _services.GetRequiredService<IStateProjectionCoordinator>();
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.XamlResourceReferenceFailed += OnXamlResourceReferenceFailed;
        _stateCommands = _services.GetRequiredService<IStateCommandService>();
        ConfigurePortableProfiles();
        _stateCoordinator.ProjectionChanged += OnStateProjectionChanged;
        _controlCenter = new ControlCenterService(
            _ledger,
            _log,
            ExecuteControlCenterPosterCommand,
            () => ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter),
            _stateCoordinator, _stateCommands, _inputRouter,
            target => _spatialRuntime?.GetEffectiveLayout(target), () => _spatialRuntime, _portableController);
        _tray = new TrayService(_ledger, _log);
        _tray.Create(QueueTrayAction);
        _log.Write("info", "application.started", new
        {
            version = typeof(App).Assembly.GetName().Version?.ToString(),
            distribution = DistributionBuild.ApplicationId,
            creationDefaults = DistributionBuild.CreationDefaults,
            runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            dataRoot = _services.GetRequiredService<IUserDataPaths>().Root,
            isElevated = IsProcessElevated(),
            requestedExecutionLevel = "asInvoker"
        });
        if (DistributionBuild.CreationDefaultsError is { } defaultsError)
            _log.Write("warning", "configuration.creation_defaults_ignored", new { error = defaultsError });
        _spatialRuntime = new PosterSpatialRuntime(_stateCoordinator, _stateCommands, _log, animationOptions,
            () => _posterSessions?.GloballyHidden == true, id => _controlCenter?.HasPosterDraft(id) == true,
            (id, entity, document) => _controlCenter?.OwnsContentFieldDraft(id, entity, document) == true);
        _spatialRuntime.EffectiveLayoutChanged += RefreshControlCenterRuntime;
        _spatialRuntime.ConfigureLampMotionLedger(_ledger);
        StartAutoPosture();
        _spatialRuntime.RecoveryRequired += () => QueueDesktopRecovery(
            DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
            "spatial-physical-recovery", invalidateShell: true);
        _posterSessions = new PosterSessionRegistry(new WinUiPosterSessionFactory(
            _services.GetRequiredService<IDesktopLayerAttachment>(), _stateCommands, _ledger, _log,
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

        ScheduleSmokeExitIfRequested(_startupIntent.Arguments);
        string[] smokeFlags = ["--content-browse-probe", "--backdrop-probe", "--lamp-pointer-probe", "--template-recipe-probe", "--template-recipe-restart", "--content-composition-probe", "--content-fields-probe", "--content-rejected-heat-probe", "--content-protocol-probe", "--content-native-probe", "--content-native-mouse-probe", "--content-interaction-restart", "--lamp-parameter-probe", "--ui-c-sample-probe", "--content-layout-smoke", "--content-layout-restart", "--picker-smoke", "--portable-smoke", "--tray-smoke", "--control-center-smoke", "--title-surface-smoke", "--auto-measurement", "--auto-lifetime-smoke", "--auto-settings-smoke", "--auto-posture-smoke", "--continuity-smoke", "--continuity-arrange", "--menu-smoke", "--presentation-smoke", "--spatial-smoke", "--input-confirmation-smoke", "--multi-instance-smoke", "--multi-instance-restart"];
        if (_startupIntent.Arguments.Count(a => smokeFlags.Contains(a, StringComparer.OrdinalIgnoreCase)) > 1)
        {
            _log.Write("error", "contract.smoke_refused", new { reason = "one-smoke-per-process" });
            return;
        }
        if (_startupIntent.Arguments.Any(a => a is "--content-browse-probe" or "--backdrop-probe" or "--lamp-pointer-probe" or "--template-recipe-probe" or "--template-recipe-restart" or "--content-composition-probe" or "--content-fields-probe" or "--content-rejected-heat-probe" or "--content-protocol-probe" or "--content-native-probe" or "--content-native-mouse-probe" or "--content-interaction-restart" or "--lamp-parameter-probe" or "--ui-c-sample-probe" or "--content-layout-smoke" or "--content-layout-restart" or "--picker-smoke" or "--portable-smoke" or "--tray-smoke" or "--control-center-smoke" or "--title-surface-smoke" or "--auto-posture-smoke" or "--auto-settings-smoke" or "--auto-lifetime-smoke" or "--auto-measurement"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "auto_posture.smoke_refused", new { reason = "isolated-profile-required" });
            else if (_startupIntent.Arguments.Contains("--content-browse-probe", StringComparer.Ordinal)) RunContentBrowseProbe(actual);
            else if (_startupIntent.Arguments.Contains("--backdrop-probe", StringComparer.Ordinal)) RunBackdropProbe(actual);
            else if (_startupIntent.Arguments.Contains("--lamp-pointer-probe", StringComparer.Ordinal)) RunLampPointerProbe(actual);
            else if (_startupIntent.Arguments.Contains("--template-recipe-probe", StringComparer.Ordinal)) RunTemplateRecipeProbe(actual, false);
            else if (_startupIntent.Arguments.Contains("--template-recipe-restart", StringComparer.Ordinal)) RunTemplateRecipeProbe(actual, true);
            else if (_startupIntent.Arguments.Contains("--content-composition-probe", StringComparer.Ordinal)) RunContentCompositionProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-fields-probe", StringComparer.Ordinal)) RunContentFieldsProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-protocol-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"protocol");
            else if (_startupIntent.Arguments.Contains("--content-rejected-heat-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"rejected-heat");
            else if (_startupIntent.Arguments.Contains("--content-native-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"native");
            else if (_startupIntent.Arguments.Contains("--content-native-mouse-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"native-mouse");
            else if (_startupIntent.Arguments.Contains("--content-interaction-restart", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"restart");
            else if (_startupIntent.Arguments.Contains("--lamp-parameter-probe", StringComparer.Ordinal)) RunLampParameterProbe(actual);
            else if (_startupIntent.Arguments.Contains("--ui-c-sample-probe", StringComparer.Ordinal)) RunUiCSampleProbe(actual);
            else if (_startupIntent.Arguments.Any(a => a is "--content-layout-smoke" or "--content-layout-restart")) RunContentLayoutSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--picker-smoke", StringComparer.Ordinal)) RunPickerSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--portable-smoke", StringComparer.Ordinal)) RunPortableSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--tray-smoke", StringComparer.Ordinal)) RunTraySmoke(actual);
            else if (_startupIntent.Arguments.Contains("--control-center-smoke", StringComparer.Ordinal)) RunControlCenterSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--title-surface-smoke", StringComparer.Ordinal))
            {
                var lease = _ledger.Acquire("fixture.title-surface", "isolated-r4-component-faults");
                var stop = _spatialRuntime.RunTitleSurfaceSmoke(_dispatcher, () =>
                {
                    lease.Dispose(); _stopAutoSmoke = null; ExitApplication();
                });
                _stopAutoSmoke = () => { try { stop(); } finally { lease.Dispose(); _stopAutoSmoke = null; } };
            }
            else if (_startupIntent.Arguments.Contains("--auto-measurement", StringComparer.Ordinal))
            {
                var modes = _startupIntent.Arguments.Where(a => a.StartsWith("--measurement-mode=", StringComparison.Ordinal)).ToArray();
                string mode = modes.Length == 1 ? modes[0]["--measurement-mode=".Length..] : "";
                if (mode is not ("off-expanded" or "on-expanded" or "off-collapsed" or "on-collapsed" or "off-trigger" or "on-trigger"))
                {
                    _log.Write("error", "auto_measurement.refused", new { reason = "invalid-mode" });
                    ExitApplication();
                }
                else RunAutoMeasurement(mode);
            }
            else if (_startupIntent.Arguments.Contains("--auto-lifetime-smoke", StringComparer.Ordinal))
            {
                var modes = _startupIntent.Arguments.Where(a => a.StartsWith("--lifetime-mode=", StringComparison.Ordinal)).ToArray();
                string mode = modes.Length == 0 ? "exercise" : modes[0]["--lifetime-mode=".Length..];
                if (modes.Length > 1 || mode is not ("exercise" or "enabled-exit" or "restart"))
                {
                    _log.Write("error", "auto_lifetime.smoke_refused", new { reason = "invalid-mode" });
                    ExitApplication();
                }
                else RunAutoLifetimeSmoke(mode);
            }
            else if (_startupIntent.Arguments.Contains("--auto-settings-smoke", StringComparer.Ordinal)) RunAutoSettingsSmoke(actual);
            else RunAutoPostureSmoke();
        }
        if (_startupIntent.Arguments.Any(a => a is "--multi-instance-smoke" or "--multi-instance-restart"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "multi_instance.smoke_refused", new { reason = "isolated-profile-required" });
            else RunMultiInstanceSmoke(_startupIntent.Arguments.Contains("--multi-instance-restart", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Any(a => a is "--continuity-smoke" or "--continuity-arrange"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "continuity.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunContinuitySmoke(_dispatcher, _startupIntent.Arguments.Contains("--continuity-arrange", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Contains("--menu-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "menu.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunMenuSmoke(_dispatcher);
        }
        if (_startupIntent.Arguments.Contains("--presentation-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                _startupIntent.Arguments.Any(a => a is "--spatial-smoke" or "--input-confirmation-smoke"))
                _log.Write("error", "presentation.smoke_refused", new { reason = "exclusive-isolated-profile-required" });
            else _spatialRuntime.RunPresentationSmoke(_dispatcher);
        }
        if (_startupIntent.Arguments.Contains("--spatial-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "spatial.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunIsolatedSmoke(_dispatcher, _startupIntent.Arguments
                .FirstOrDefault(a => a.StartsWith("--expect-expansion=", StringComparison.Ordinal))?.Split('=')[1],
                _startupIntent.Arguments.Contains("--spatial-smoke-leave-expanded", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Contains("--input-confirmation-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_services.GetRequiredService<IUserDataPaths>().Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "input_confirmation.smoke_refused", new { reason = "isolated-profile-required" });
            else _controlCenter.RunIsolatedConfirmationSmoke(_dispatcher,
                _startupIntent.Arguments.Contains("--exit-with-confirmation", StringComparer.OrdinalIgnoreCase));
        }
    }

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

    private void StartDesktopEnvironmentMonitoring()
    {
        if (_dispatcher is null ||
            _ledger is null ||
            _desktopEnvironmentWatcher is not null)
        {
            return;
        }

        _desktopEnvironmentCancellation = new CancellationTokenSource();
        // Hidden top-level listener survives a zero-poster catalog and receives broadcasts.
        _desktopEnvironmentAnchor = new Window { Title = "DesktopUp environment listener" };
        _desktopEnvironmentAnchor.AppWindow.Hide();
        _desktopEnvironmentAnchorLease = _ledger.Acquire("window.desktop-environment-anchor", "hidden-broadcast-listener");
        _desktopEnvironmentWatcher = new DesktopEnvironmentWatcher(
            WinRT.Interop.WindowNative.GetWindowHandle(_desktopEnvironmentAnchor), () => true, guardPosterWindow: false);
        _desktopEnvironmentWatcher.Changed += OnDesktopEnvironmentChanged;
        _desktopEnvironmentWatcher.LifecycleChanged += OnSystemLifecycleChanged;
        _log?.Write(_desktopEnvironmentWatcher.SessionNotificationsRegistered ? "info" : "warning", "system_lifecycle.registration",
            new { registered = _desktopEnvironmentWatcher.SessionNotificationsRegistered, error = _desktopEnvironmentWatcher.SessionNotificationError });
        _desktopEnvironmentWatcherLease = _ledger.Acquire(
            "watcher.desktop-environment",
            "native-window-subclass");

        _desktopRecoveryTimer = _dispatcher.CreateTimer();
        _desktopRecoveryTimer.IsRepeating = false;
        _desktopRecoveryTimer.Tick += DesktopRecoveryTimer_Tick;
        _desktopRecoveryTimerLease = _ledger.Acquire(
            "timer.desktop-recovery",
            "dispatcher-debounce-timer");

        _desktopHealthTimer = _dispatcher.CreateTimer();
        _desktopHealthTimer.Interval = TimeSpan.FromSeconds(2);
        _desktopHealthTimer.IsRepeating = true;
        _desktopHealthTimer.Tick += DesktopHealthTimer_Tick;
        _desktopHealthTimer.Start();
        _desktopHealthTimerLease = _ledger.Acquire(
            "timer.desktop-health",
            "read-only-environment-probe");

        _log?.Write("info", "desktop_environment.monitoring_started", new
        {
            sessions = _posterSessions?.Sessions
        });
    }

    private void OnDesktopEnvironmentChanged(object? sender, DesktopEnvironmentChangedEventArgs args)
    {
        bool sessionGuard = args.Reason.StartsWith("session-guard-", StringComparison.Ordinal);
        if (sessionGuard && HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell)) return;
        QueueDesktopRecovery(args.Kind, args.Reason, invalidateShell: !sessionGuard &&
            !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell));
    }
    private void DesktopHealthTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _desktopEnvironmentWatcher?.TryRegisterSessionNotifications();
        if (_systemLifecycle.IsSuspended) return;
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _posterSessions is null)
        {
            return;
        }

        try
        {
            IDesktopShellLocator locator =
                _services.GetRequiredService<IDesktopShellLocator>();
            DesktopShellHostSnapshot? shell = locator.Current;
            if ((shell is null || _posterSessions.NeedsRecovery(shell)) &&
                !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell))
            {
                QueueDesktopRecovery(
                    DesktopEnvironmentChangeKind.DesktopShell |
                        DesktopEnvironmentChangeKind.DisplayTopology,
                    shell is null ? "shell-health-invalid" : "session-health-retry",
                    invalidateShell: false);
                return;
            }

            IDisplayTopologyService topology =
                _services.GetRequiredService<IDisplayTopologyService>();
            DisplayTopologyProbeResult result = topology.Probe();
            if (result.State is DisplayTopologyProbeState.Candidate or DisplayTopologyProbeState.Stable &&
                !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DisplayTopology))
            {
                QueueDesktopRecovery(
                    DesktopEnvironmentChangeKind.DisplayTopology,
                    "display-topology-poll",
                    invalidateShell: false);
            }
        }
        catch (Exception exception)
        {
            _log?.Write("warning", "desktop_environment.health_probe_failed", new
            {
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message
            });
        }
    }

    private bool HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind kind)
    {
        bool pending = (_pendingDesktopRecoveryKinds & kind) != 0;
        bool active = kind switch
        {
            DesktopEnvironmentChangeKind.DesktopShell => _activeShellRecoveries > 0,
            DesktopEnvironmentChangeKind.DisplayTopology => _activeDisplayRecoveries > 0,
            _ => false
        };
        return pending || active;
    }

    private void QueueDesktopRecovery(
        DesktopEnvironmentChangeKind kind,
        string reason,
        bool invalidateShell)
    {
        if (Volatile.Read(ref _exiting) != 0 ||
            kind == DesktopEnvironmentChangeKind.None ||
            _services is null ||
            _desktopRecoveryTimer is null)
        {
            return;
        }

        if (_dispatcher is { HasThreadAccess: false })
        {
            _dispatcher.TryEnqueue(
                () => QueueDesktopRecovery(kind, reason, invalidateShell));
            return;
        }

        long request = _desktopRecoveryVersionGate.Signal();
        SuspendAutoPosture();
        _inputRouter.Suspend();
        _controlCenter?.CancelRemoval("桌面环境正在恢复，删除确认已取消。");
        _spatialRuntime?.Suspend();
        _posterSessions?.SuspendEnvironment(request);
        _controlCenterEnvironmentPending = true;
        RefreshControlCenterRuntime();
        _pendingDesktopRecoveryKinds |= kind;
        _pendingDesktopRecoveryReasons.Add(reason);
        if (_systemLifecycle.IsSuspended) return; // No display/health event can resume a locked/sleeping session.
        if (invalidateShell &&
            (kind & DesktopEnvironmentChangeKind.DesktopShell) != 0)
        {
            _services.GetRequiredService<IDesktopShellLocator>().Invalidate();
            _log?.Write("info", "desktop_shell.invalidated", new { request, reason });
        }

        _desktopRecoveryTimer.Stop();
        _desktopRecoveryTimer.Interval =
            (_pendingDesktopRecoveryKinds & DesktopEnvironmentChangeKind.DesktopShell) != 0
                ? TimeSpan.FromMilliseconds(420)
                : TimeSpan.FromMilliseconds(180);
        _desktopRecoveryTimer.Start();
        _log?.Write("info", "desktop_environment.change_queued", new
        {
            request,
            kind = kind.ToString(),
            reason
        });
    }

    private void DesktopRecoveryTimer_Tick(
        DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        _desktopRecoveryTasks.RemoveAll(task => task.IsCompleted);
        _desktopRecoveryTasks.Add(RunDesktopRecoveryAsync());
    }

    private async Task RunDesktopRecoveryAsync()
    {
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _posterSessions is null ||
            _desktopEnvironmentCancellation is null || _systemLifecycle.IsSuspended)
        {
            return;
        }

        DesktopEnvironmentChangeKind kinds = _pendingDesktopRecoveryKinds;
        string reasons = string.Join(",", _pendingDesktopRecoveryReasons.Order());
        _pendingDesktopRecoveryKinds = DesktopEnvironmentChangeKind.None;
        _pendingDesktopRecoveryReasons.Clear();
        long request = _desktopRecoveryVersionGate.CurrentRequest;
        if ((kinds & DesktopEnvironmentChangeKind.DesktopShell) != 0)
        {
            _activeShellRecoveries++;
        }
        if ((kinds & DesktopEnvironmentChangeKind.DisplayTopology) != 0)
        {
            _activeDisplayRecoveries++;
        }

        CancellationToken cancellationToken = _desktopEnvironmentCancellation.Token;
        using IDisposable? recoveryLease = _ledger?.Acquire($"background.desktop-recovery/{request}", "shared-recovery-task");
        try
        {
            await RecoverDesktopEnvironmentAsync(
                request,
                kinds,
                reasons,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _log?.Write("info", "desktop_environment.recovery_cancelled", new { request });
        }
        catch (Exception exception)
        {
            _log?.Write("error", "desktop_environment.recovery_failed", new
            {
                request,
                kinds = kinds.ToString(),
                reasons,
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message,
                exception.StackTrace
            });
        }
        finally
        {
            if ((kinds & DesktopEnvironmentChangeKind.DesktopShell) != 0)
            {
                _activeShellRecoveries--;
            }
            if ((kinds & DesktopEnvironmentChangeKind.DisplayTopology) != 0)
            {
                _activeDisplayRecoveries--;
            }
        }
    }

    private async Task RecoverDesktopEnvironmentAsync(
        long request,
        DesktopEnvironmentChangeKind kinds,
        string reasons,
        CancellationToken cancellationToken)
    {
        if (_services is null || _posterSessions is null)
        {
            return;
        }

        IDesktopShellLocator locator =
            _services.GetRequiredService<IDesktopShellLocator>();
        IDisplayTopologyService topologyService =
            _services.GetRequiredService<IDisplayTopologyService>();
        const int maximumApplyAttempts = 8;

        for (int attempt = 1; attempt <= maximumApplyAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_desktopRecoveryVersionGate.IsCurrent(request))
            {
                LogStaleRecovery(request, kinds, reasons, "newer-request-before-snapshot");
                return;
            }

            DesktopShellHostSnapshot? shell =
                await locator.WaitForStableHostAsync(cancellationToken);
            DisplayTopologySnapshot? topology =
                await topologyService.WaitForStableTopologyAsync(cancellationToken);
            if (shell is null || topology is null)
            {
                LogRecoveryRetry(
                    request,
                    attempt,
                    kinds,
                    reasons,
                    shell is null ? "shell-unavailable" : "topology-unavailable");
                await Task.Delay(TimeSpan.FromMilliseconds(180), cancellationToken);
                continue;
            }

            var expectedPair = new DesktopRecoveryGenerationPair(
                shell.Generation,
                topology.Generation);
            DesktopRecoveryGenerationPair? observedPair =
                CaptureCurrentGenerationPair(locator, topologyService, topology);
            if (observedPair is null ||
                !_desktopRecoveryVersionGate.CanCommit(
                    request,
                    expectedPair,
                    observedPair.Value))
            {
                LogStaleRecovery(request, kinds, reasons, "generation-changed-before-apply");
                return;
            }

            if ((kinds & DesktopEnvironmentChangeKind.DesktopShell) != 0)
            {
                _log?.Write("info", "desktop_shell.stable", new
                {
                    request,
                    generation = shell.Generation,
                    iconViewHandle = $"0x{shell.Identity.IconViewHandle:X}",
                    containerHandle = $"0x{shell.Identity.ContainerHandle:X}",
                    explorerProcessId = shell.Identity.ProcessId,
                    shell.StableAtUtc
                });
            }
            if ((kinds & DesktopEnvironmentChangeKind.DisplayTopology) != 0)
            {
                _log?.Write("info", "display_topology.stable", new
                {
                    request,
                    generation = topology.Generation,
                    topology.Signature,
                    monitorCount = topology.Monitors.Count,
                    primary = topology.PrimaryMonitor?.DeviceName,
                    topology.StableAtUtc
                });
            }

            if (topology.PrimaryMonitor is not { } primaryMonitor) return;
            if (!_stateCoordinator!.TryGetCurrent(out DesktopUpProjection? current) || current is null)
                current = _stateCoordinator.EnsureInitialized(PosterVisualWindow.CreateInitialDefault(primaryMonitor));
            ApplyStateProjection(new StateProjectionChangedEventArgs(current, StateCommandSource.System, $"sessions-recovery-{request}"));
            bool IsCurrentEnvironment() => Volatile.Read(ref _exiting) == 0 &&
                !_systemLifecycle.IsSuspended &&
                CaptureCurrentGenerationPair(locator, topologyService, topology) is { } observed &&
                _desktopRecoveryVersionGate.CanCommit(request, expectedPair, observed);
            _spatialRuntime?.SetEffectiveTopology(topology);
            bool applied = _posterSessions.Recover(new(shell, topology), request, IsCurrentEnvironment);
            if (IsCurrentEnvironment())
            {
                if (applied) _spatialRuntime?.Resume();
                // Failed clusters have no published input. Ready peers remain usable.
                if (_posterSessions.GloballyHidden) _inputRouter.Suspend(); else _inputRouter.Resume();
                _autoReady = applied && !_posterSessions.GloballyHidden;
                ObserveAutoPosture();
            }
            if (_desktopRecoveryVersionGate.IsCurrent(request))
            {
                _controlCenterEnvironmentPending = false;
                RefreshControlCenterRuntime();
            }
            observedPair = CaptureCurrentGenerationPair(
                locator,
                topologyService,
                topology);
            if (applied &&
                observedPair is not null &&
                _desktopRecoveryVersionGate.CanCommit(
                    request,
                    expectedPair,
                    observedPair.Value))
            {
                _log?.Write("info", "desktop_environment.recovery_committed", new
                {
                    request,
                    kinds = kinds.ToString(),
                    reasons,
                    attempt,
                    shellGeneration = shell.Generation,
                    topologyGeneration = topology.Generation,
                    sessions = _posterSessions?.Sessions
                });
                return;
            }

            if (!_desktopRecoveryVersionGate.IsCurrent(request))
            {
                LogStaleRecovery(request, kinds, reasons, "newer-request-after-apply");
                return;
            }

            LogRecoveryRetry(
                request,
                attempt,
                kinds,
                reasons,
                "apply-or-readback-failed");
            if (attempt < maximumApplyAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(180), cancellationToken);
            }
        }

        _log?.Write("error", "desktop_environment.recovery_exhausted", new
        {
            request,
            kinds = kinds.ToString(),
            reasons,
            attempts = maximumApplyAttempts
        });
    }

    private static DesktopRecoveryGenerationPair? CaptureCurrentGenerationPair(
        IDesktopShellLocator locator,
        IDisplayTopologyService topologyService,
        DisplayTopologySnapshot expectedTopology)
    {
        DesktopShellHostSnapshot? shell = locator.Current;
        DisplayTopologySnapshot? topology = topologyService.Current;
        if (shell is null ||
            topology is null ||
            !topologyService.IsCurrent(expectedTopology))
        {
            return null;
        }

        return new DesktopRecoveryGenerationPair(
            shell.Generation,
            topology.Generation);
    }

    private void LogStaleRecovery(
        long request,
        DesktopEnvironmentChangeKind kinds,
        string reasons,
        string stage) =>
        _log?.Write("info", "desktop_environment.recovery_stale", new
        {
            request,
            kinds = kinds.ToString(),
            reasons,
            stage,
            currentRequest = _desktopRecoveryVersionGate.CurrentRequest
        });

    private void LogRecoveryRetry(
        long request,
        int attempt,
        DesktopEnvironmentChangeKind kinds,
        string reasons,
        string error) =>
        _log?.Write("warning", "desktop_environment.recovery_retry", new
        {
            request,
            attempt,
            kinds = kinds.ToString(),
            reasons,
            error
        });

    private void OnExternalIntent(StartupIntent intent)
    {
        DispatcherQueue? dispatcher = _dispatcher;
        if (dispatcher is null)
        {
            _pendingIntents.Enqueue(intent);
            return;
        }
        dispatcher.TryEnqueue(() => DispatchIntent(intent));
    }

    private void DispatchIntent(StartupIntent intent)
    {
        _log?.Write("info", "activation.received", new { kind = intent.Kind.ToString() });
        if (_recoveryWindow is not null && intent.Kind != StartupIntentKind.Exit)
        { _recoveryWindow.Activate(); return; }
        switch (intent.Kind)
        {
            case StartupIntentKind.ShowControlCenter:
                ShowControlCenter();
                break;
            case StartupIntentKind.Exit:
                ExitApplication();
                break;
        }
    }

    private void ShowControlCenter()
    {
        // Load XAML on the UI dispatcher after the native notification callback returns.
        _ = _dispatcher?.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _exiting) == 0)
            {
                _controlCenter?.Show();
            }
        });
    }

    private void OnXamlResourceReferenceFailed(
        DebugSettings sender,
        XamlResourceReferenceFailedEventArgs args) =>
        _log?.Write("error", "xaml.resource_reference_failed", new { args.Message });

    private void OnPosterEditingChanged(string posterId, bool editing)
    {
        ObserveAutoPosture();
    }

    private void RefreshControlCenterRuntime()
    {
        bool hidden = _posterSessions?.GloballyHidden == true;
        _controlCenter?.ApplySessions(_posterSessions?.Sessions ?? Array.Empty<PosterSessionSnapshot>(), _controlCenterEnvironmentPending, hidden);
        _tray?.ApplyRuntime(hidden, _controlCenterEnvironmentPending);
        if (_stateCoordinator?.TryGetCurrent(out var current) == true && current is not null)
        {
            int constrained = current.Posters.Count(p => p.IsEffectivelyVisible && _spatialRuntime?.GetEffectiveLayout(p)?.IsConstrained == true);
            _tray?.ApplyProjection(current.Tray with { ToolTipText = current.Tray.ToolTipText +
                (constrained > 0 ? $" · {constrained} 张空间受限" : "") });
        }
    }

    private void ExecuteControlCenterPosterCommand(PosterStateCommand command)
    {
        if (Volatile.Read(ref _exiting) != 0 || _stateCommands is null) return;
        // Do not look up the current UI selection here: the displayed command is already targeted.
        if (command.Kind == PosterStateCommandKind.SetCollapsed && _spatialRuntime is not null)
        {
            _spatialRuntime.SetCollapsed(command, result =>
            {
                _controlCenter?.ShowCommandResult(result);
                LogCommandResult("control-center-posture", result);
            });
            return;
        }
        if (command.Kind is PosterStateCommandKind.Move or PosterStateCommandKind.Resize)
            _spatialRuntime?.BeforeManualIntent(command.PosterId);
        StateCommandResult result = _stateCommands.Execute(command);
        _controlCenter?.ShowCommandResult(result);
        LogCommandResult("control-center-poster", result);
        _log?.Write("info", "control_center.command_completed", new
        {
            command.PosterId, Kind = command.Kind.ToString(), Status = result.Status.ToString(),
            command.RequestId, result.CurrentRevision
        });
    }
    private void OnStateProjectionChanged(
        object? sender,
        StateProjectionChangedEventArgs args)
    {
        if (_dispatcher is not null && !_dispatcher.HasThreadAccess)
        {
            _ = _dispatcher.TryEnqueue(() => ApplyStateProjection(args));
            return;
        }
        ApplyStateProjection(args);
    }

    private void ApplyStateProjection(StateProjectionChangedEventArgs args)
    {
        if (Volatile.Read(ref _exiting) != 0)
        {
            return;
        }
        _spatialRuntime?.OnProjection(args);
        _autoEvidence.Apply(args);
        _controlCenter?.ApplyProjection(args.Projection.ControlCenter);
        _tray?.ApplyProjection(args.Projection.Tray);
        _posterSessions?.ApplyProjection(args);
        RefreshControlCenterRuntime();
        ObserveAutoPosture();
    }

    private void TogglePosterLocked(StateCommandSource source)
    {
        if (!TryGetProjection(source, "lock", out DesktopUpProjection? projection))
        {
            return;
        }
        DesktopUpProjection currentProjection = projection!;
        if (source == StateCommandSource.Tray)
        {
            var reads = new StateReadSet(currentProjection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
                CatalogVersion: currentProjection.CatalogVersion);
            StateCommandResult batch = _stateCommands!.Execute(new BatchPosterCommand(
                $"tray-lock-{Guid.NewGuid():N}", source, currentProjection.Revision, reads,
                currentProjection.Posters.Select(p => p.Poster.PosterId).ToArray(),
                BatchPosterCommandKind.SetLocked, !currentProjection.Tray.AllPostersLocked, AllPosters: true));
            LogCommandResult("poster-lock-all", batch);
            ShowTrayResult(batch);
            return;
        }
        // Per-poster center commands are frozen by ControlCenterView, never by this global tray path.
    }

    private void ToggleDynamicEffectsPaused(StateCommandSource source)
    {
        if (!TryGetProjection(source, "pause", out DesktopUpProjection? projection))
        {
            return;
        }
        DesktopUpProjection currentProjection = projection!;
        DesktopUpSettings current = currentProjection.ControlCenter.Settings.Settings;
        DesktopUpSettings desired = current with
        {
            DynamicEffectsPaused = !current.DynamicEffectsPaused
        };
        StateCommandResult result = _stateCommands!.Execute(new SettingsStateCommand(
            $"{SourceToken(source)}-pause-{Guid.NewGuid():N}",
            source,
            SettingsStateCommandKind.SetDynamicEffectsPaused,
            currentProjection.Revision,
            desired) { ReadSet = StateReadSet.ForSettings(currentProjection.ControlCenter.Settings.EntityVersion) });
        LogCommandResult("dynamic-effects-pause", result);
        if (source == StateCommandSource.Tray) ShowTrayResult(result);
    }

    private bool TryGetProjection(
        StateCommandSource source,
        string action,
        out DesktopUpProjection? projection)
    {
        if (_stateCoordinator?.TryGetCurrent(out projection) == true &&
            projection is not null)
        {
            return true;
        }
        _log?.Write("warning", "state_command.not_ready", new
        {
            source = source.ToString(),
            action
        });
        projection = null;
        return false;
    }

    private void LogCommandResult(string action, StateCommandResult result)
    {
        if (result.Succeeded)
        {
            return;
        }
        _log?.Write("warning", "state_command.rejected", new
        {
            action,
            status = result.Status.ToString(),
            result.RequestId,
            result.ExpectedRevision,
            result.CurrentRevision,
            result.Error
        });
    }

    private static string SourceToken(StateCommandSource source) =>
        source.ToString().ToLowerInvariant();

    private void ScheduleSmokeExitIfRequested(IEnumerable<string> arguments)
    {
        string? value = arguments.FirstOrDefault(static item =>
            item.StartsWith("--exit-after-ms=", StringComparison.OrdinalIgnoreCase));
        if (value is null || !int.TryParse(value[(value.IndexOf('=') + 1)..], out int milliseconds))
        {
            return;
        }
        milliseconds = Math.Clamp(milliseconds, 250, 60_000);
        _ = Task.Delay(milliseconds).ContinueWith(
            _ => _dispatcher?.TryEnqueue(ExitApplication),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async void ExitApplication()
    {
        if (_recoveryWindow?.IsBusy == true) return;
        if (Interlocked.Exchange(ref _exiting, 1) != 0)
        {
            return;
        }
        _log?.Write("info", "application.exit_requested");
        StopAutoPosture();
        if (_controlCenter is not null) await _controlCenter.CancelPortableAndWaitAsync();
        _inputRouter.Suspend();
        _controlCenter?.CancelRemoval("application-exit");
        DebugSettings.XamlResourceReferenceFailed -= OnXamlResourceReferenceFailed;
        StopDesktopEnvironmentMonitoring();
        _posterSessions?.Dispose();
        _posterSessions = null;
        // Cancellation completes on the UI dispatcher before DI/logging and the final ledger close.
        await Task.WhenAll(_desktopRecoveryTasks.ToArray());
        _desktopRecoveryTasks.Clear();
        if (_stateCoordinator is not null)
        {
            _stateCoordinator.ProjectionChanged -= OnStateProjectionChanged;
            _stateCoordinator = null;
        }
        _stateCommands = null;
        _controlCenter?.Dispose();
        _controlCenter = null;
        _tray?.Dispose();
        _tray = null;
        _recoveryWindow?.CloseForExit();
        _recoveryWindow = null;
        _recoveryWindowLease?.Dispose(); _recoveryWindowLease = null;
        _singleInstance.Dispose();
        _ledger?.WriteSnapshot();
        _log?.Write("info", "application.stopped", new
        {
            activeResources = _ledger?.ActiveResources.Count ?? 0
        });
        _services?.Dispose();
        _services = null;
        _storageInitialized = false;
        StartRequestedReplacement();
        Exit();
    }

    private void StopDesktopEnvironmentMonitoring()
    {
        _desktopRecoveryVersionGate.Signal();
        _desktopEnvironmentCancellation?.Cancel();

        if (_desktopHealthTimer is not null)
        {
            _desktopHealthTimer.Stop();
            _desktopHealthTimer.Tick -= DesktopHealthTimer_Tick;
            _desktopHealthTimer = null;
        }
        _desktopHealthTimerLease?.Dispose();
        _desktopHealthTimerLease = null;

        if (_desktopRecoveryTimer is not null)
        {
            _desktopRecoveryTimer.Stop();
            _desktopRecoveryTimer.Tick -= DesktopRecoveryTimer_Tick;
            _desktopRecoveryTimer = null;
        }
        _desktopRecoveryTimerLease?.Dispose();
        _desktopRecoveryTimerLease = null;

        if (_desktopEnvironmentWatcher is not null)
        {
            _desktopEnvironmentWatcher.Changed -= OnDesktopEnvironmentChanged;
            _desktopEnvironmentWatcher.LifecycleChanged -= OnSystemLifecycleChanged;
            _desktopEnvironmentWatcher.Dispose();
            _desktopEnvironmentWatcher = null;
        }
        _desktopEnvironmentWatcherLease?.Dispose();
        _desktopEnvironmentWatcherLease = null;
        _desktopEnvironmentAnchor?.Close();
        _desktopEnvironmentAnchor = null;
        _desktopEnvironmentAnchorLease?.Dispose();
        _desktopEnvironmentAnchorLease = null;

        _pendingDesktopRecoveryKinds = DesktopEnvironmentChangeKind.None;
        _pendingDesktopRecoveryReasons.Clear();
        _desktopEnvironmentCancellation?.Dispose();
        _desktopEnvironmentCancellation = null;
        _log?.Write("info", "desktop_environment.monitoring_stopped");
    }

    private static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
