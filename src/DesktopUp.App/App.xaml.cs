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
    private PosterVisualWindow? _poster;
    private IStateProjectionCoordinator? _stateCoordinator;
    private IStateCommandService? _stateCommands;
    private IStructuredLog? _log;
    private IResourceLedger? _ledger;
    private CancellationTokenSource? _desktopDiscoveryCancellation;
    private Task? _desktopDiscoveryTask;
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
    private int _exiting;

    public App()
    {
        InitializeComponent();
        var parser = new StartupIntentParser();
        _startupIntent = parser.Parse(Environment.GetCommandLineArgs().Skip(1));
        _singleInstance = new SingleInstanceCoordinator(SingleInstanceNames.ForCurrentUser());
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
        StateStoreInitializationResult stateInitialization =
            _services.GetRequiredService<IStateStore>().Initialize();
        if (!stateInitialization.Succeeded)
        {
            _log.Write("critical", "application.state_store_unavailable", new
            {
                stateInitialization.PreviousSchemaVersion,
                stateInitialization.Error
            });
            ExitApplication();
            return;
        }
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
        _stateCommands = _services.GetRequiredService<IStateCommandService>();
        _stateCoordinator.ProjectionChanged += OnStateProjectionChanged;
        _controlCenter = new ControlCenterService(
            _ledger,
            _log,
            () => TogglePosterLocked(StateCommandSource.ControlCenter),
            () => TogglePosterCollapsed(StateCommandSource.ControlCenter),
            () => ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter));
        _tray = new TrayService(_ledger, _log);
        _tray.Create(
            _controlCenter.Show,
            TogglePosterEditing,
            () => TogglePosterLocked(StateCommandSource.Tray),
            () => ToggleDynamicEffectsPaused(StateCommandSource.Tray),
            ExitApplication);
        _log.Write("info", "application.started", new
        {
            version = typeof(App).Assembly.GetName().Version?.ToString(),
            dataRoot = _services.GetRequiredService<IUserDataPaths>().Root,
            isElevated = IsProcessElevated(),
            requestedExecutionLevel = "asInvoker"
        });
        StartDesktopDiscovery();

        DispatchIntent(_startupIntent);
        while (_pendingIntents.TryDequeue(out StartupIntent? pending))
        {
            DispatchIntent(pending);
        }

        ScheduleSmokeExitIfRequested(_startupIntent.Arguments);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserDataPaths, UserDataPaths>();
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
        services.AddSingleton<IStateProjectionCoordinator>(static provider =>
            provider.GetRequiredService<StateProjectionCoordinator>());
        services.AddSingleton<IStateCommandService>(static provider =>
            provider.GetRequiredService<StateProjectionCoordinator>());
        services.AddSingleton<IDesktopShellLocator, ExplorerDesktopLocator>();
        services.AddSingleton<IDesktopLayerAttachment, DesktopLayerAttachment>();
        services.AddSingleton<IDisplayTopologyService, DisplayTopologyService>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private void StartDesktopDiscovery()
    {
        if (_services is null || _log is null || _ledger is null)
        {
            return;
        }

        _desktopDiscoveryCancellation = new CancellationTokenSource();
        _desktopDiscoveryTask = DiscoverDesktopShellAsync(
            _services.GetRequiredService<IDesktopShellLocator>(),
            _log,
            _ledger,
            _desktopDiscoveryCancellation.Token);
    }

    private async Task DiscoverDesktopShellAsync(
        IDesktopShellLocator locator,
        IStructuredLog log,
        IResourceLedger ledger,
        CancellationToken cancellationToken)
    {
        using IDisposable lease = ledger.Acquire(
            "background.desktop-shell-discovery",
            "bounded-background-task");
        try
        {
            IDisplayTopologyService displayTopology =
                _services!.GetRequiredService<IDisplayTopologyService>();
            Task<DesktopShellHostSnapshot?> shellTask =
                locator.WaitForStableHostAsync(cancellationToken).AsTask();
            Task<DisplayTopologySnapshot?> topologyTask =
                displayTopology.WaitForStableTopologyAsync(cancellationToken).AsTask();
            await Task.WhenAll(shellTask, topologyTask).ConfigureAwait(false);

            DesktopShellHostSnapshot? shellSnapshot = await shellTask.ConfigureAwait(false);
            DisplayTopologySnapshot? topologySnapshot = await topologyTask.ConfigureAwait(false);
            if (shellSnapshot is null || topologySnapshot is null)
            {
                log.Write("warning", "desktop_environment.unavailable", new
                {
                    shellAvailable = shellSnapshot is not null,
                    topologyAvailable = topologySnapshot is not null
                });
                return;
            }

            log.Write("info", "desktop_shell.stable", new
            {
                generation = shellSnapshot.Generation,
                iconViewHandle = $"0x{shellSnapshot.Identity.IconViewHandle:X}",
                containerHandle = $"0x{shellSnapshot.Identity.ContainerHandle:X}",
                explorerProcessId = shellSnapshot.Identity.ProcessId,
                shellSnapshot.StableAtUtc
            });
            log.Write("info", "display_topology.stable", new
            {
                generation = topologySnapshot.Generation,
                topologySnapshot.Signature,
                monitorCount = topologySnapshot.Monitors.Count,
                primary = topologySnapshot.PrimaryMonitor?.DeviceName,
                topologySnapshot.StableAtUtc
            });
            _dispatcher?.TryEnqueue(() => ShowPoster(shellSnapshot, topologySnapshot));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            log.Write("info", "desktop_shell.discovery_cancelled");
        }
    }

    private void ShowPoster(
        DesktopShellHostSnapshot desktopHost,
        DisplayTopologySnapshot displayTopology)
    {
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _ledger is null ||
            _log is null ||
            _controlCenter is null)
        {
            return;
        }

        try
        {
            _poster ??= new PosterVisualWindow(
                _services.GetRequiredService<IDesktopLayerAttachment>(),
                _services.GetRequiredService<IStateProjectionCoordinator>(),
                _services.GetRequiredService<IStateCommandService>(),
                _ledger,
                _log,
                _controlCenter.Show,
                editing => _tray?.SetPosterEditing(editing));
            if (!_poster.Show(desktopHost, displayTopology))
            {
                _log.Write("error", "poster.show_failed", new
                {
                    desktopHost.Generation,
                    displayGeneration = displayTopology.Generation
                });
                return;
            }

            StartDesktopEnvironmentMonitoring();
        }
        catch (Exception exception)
        {
            _log.Write("error", "poster.show_exception", new
            {
                desktopHost.Generation,
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message,
                exception.StackTrace
            });
            _poster?.Dispose();
            _poster = null;
        }
    }

    private void StartDesktopEnvironmentMonitoring()
    {
        if (_poster is null ||
            _poster.WindowHandle == 0 ||
            _dispatcher is null ||
            _ledger is null ||
            _desktopEnvironmentWatcher is not null)
        {
            return;
        }

        _desktopEnvironmentCancellation = new CancellationTokenSource();
        _desktopEnvironmentWatcher = new DesktopEnvironmentWatcher(
            _poster.WindowHandle,
            ReassertPosterDesktopBottom);
        _desktopEnvironmentWatcher.Changed += OnDesktopEnvironmentChanged;
        _desktopEnvironmentWatcher.PointerActivationGuarded +=
            OnPosterPointerActivationGuarded;
        _desktopEnvironmentWatcher.LayerActivationGuarded +=
            OnPosterLayerActivationGuarded;
        _desktopEnvironmentWatcher.ZOrderChangeConstrained +=
            OnPosterZOrderChangeConstrained;
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
            handle = $"0x{_poster.WindowHandle:X}"
        });
    }

    private void OnDesktopEnvironmentChanged(
        object? sender,
        DesktopEnvironmentChangedEventArgs args)
    {
        QueueDesktopRecovery(args.Kind, args.Reason, invalidateShell: true);
    }

    private bool ReassertPosterDesktopBottom()
    {
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _poster is null)
        {
            return false;
        }

        DesktopShellHostSnapshot? shell =
            _services.GetRequiredService<IDesktopShellLocator>().Current;
        return shell is not null && _poster.ReassertDesktopBottom(shell);
    }

    private void OnPosterPointerActivationGuarded(
        object? sender,
        DesktopPointerActivationGuardedEventArgs args)
    {
        _log?.Write(
            args.DesktopBottomRestored ? "info" : "warning",
            "poster.pointer_activation_suppressed",
            new
            {
                args.DesktopBottomRestored,
                args.Error,
                handle = _poster is null ? null : $"0x{_poster.WindowHandle:X}"
            });

        if (!args.DesktopBottomRestored &&
            !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell))
        {
            QueueDesktopRecovery(
                DesktopEnvironmentChangeKind.DesktopShell |
                    DesktopEnvironmentChangeKind.DisplayTopology,
                "pointer-layer-reassert-failed",
                invalidateShell: true);
        }
    }

    private void OnPosterLayerActivationGuarded(
        object? sender,
        DesktopLayerActivationGuardedEventArgs args)
    {
        _log?.Write(
            args.DesktopBottomRestored ? "info" : "warning",
            "poster.layer_activation_reasserted",
            new
            {
                args.Reason,
                args.DesktopBottomRestored,
                args.Error,
                handle = _poster is null ? null : $"0x{_poster.WindowHandle:X}"
            });

        if (!args.DesktopBottomRestored &&
            !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell))
        {
            QueueDesktopRecovery(
                DesktopEnvironmentChangeKind.DesktopShell |
                    DesktopEnvironmentChangeKind.DisplayTopology,
                $"{args.Reason}-layer-reassert-failed",
                invalidateShell: true);
        }
    }

    private void OnPosterZOrderChangeConstrained(
        object? sender,
        DesktopZOrderChangeConstrainedEventArgs args)
    {
        _log?.Write(
            "info",
            "poster.z_order_change_constrained",
            new
            {
                requestedInsertAfter = $"0x{args.RequestedInsertAfter:X}",
                enforcedInsertAfter = "HWND_BOTTOM",
                handle = _poster is null ? null : $"0x{_poster.WindowHandle:X}"
            });
    }

    private void DesktopHealthTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _poster is null)
        {
            return;
        }

        try
        {
            IDesktopShellLocator locator =
                _services.GetRequiredService<IDesktopShellLocator>();
            DesktopShellHostSnapshot? shell = locator.Current;
            if ((shell is null || !_poster.IsAttached(shell)) &&
                !HasPendingOrActiveRecovery(DesktopEnvironmentChangeKind.DesktopShell))
            {
                QueueDesktopRecovery(
                    DesktopEnvironmentChangeKind.DesktopShell |
                        DesktopEnvironmentChangeKind.DisplayTopology,
                    shell is null ? "shell-health-invalid" : "owner-health-mismatch",
                    invalidateShell: shell is not null);
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
        _pendingDesktopRecoveryKinds |= kind;
        _pendingDesktopRecoveryReasons.Add(reason);
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

    private async void DesktopRecoveryTimer_Tick(
        DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        if (Volatile.Read(ref _exiting) != 0 ||
            _services is null ||
            _poster is null ||
            _desktopEnvironmentCancellation is null)
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

        try
        {
            await RecoverDesktopEnvironmentAsync(
                request,
                kinds,
                reasons,
                _desktopEnvironmentCancellation.Token);
        }
        catch (OperationCanceledException)
            when (_desktopEnvironmentCancellation.IsCancellationRequested)
        {
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
        if (_services is null || _poster is null)
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

            bool applied = _poster.Recover(shell, topology, request);
            observedPair = CaptureCurrentGenerationPair(
                locator,
                topologyService,
                topology);
            if (applied &&
                observedPair is not null &&
                _desktopRecoveryVersionGate.CanCommit(
                    request,
                    expectedPair,
                    observedPair.Value) &&
                _poster.CurrentGenerationPair == expectedPair)
            {
                _log?.Write("info", "desktop_environment.recovery_committed", new
                {
                    request,
                    kinds = kinds.ToString(),
                    reasons,
                    attempt,
                    shellGeneration = shell.Generation,
                    topologyGeneration = topology.Generation,
                    handle = $"0x{_poster.WindowHandle:X}"
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
        switch (intent.Kind)
        {
            case StartupIntentKind.ShowControlCenter:
                _controlCenter?.Show();
                break;
            case StartupIntentKind.Exit:
                ExitApplication();
                break;
        }
    }

    private void TogglePosterEditing()
    {
        if (_poster is null)
        {
            _log?.Write("warning", "poster.mode_change_ignored", new
            {
                error = "poster-not-ready"
            });
            return;
        }

        bool target = !_poster.IsEditing;
        _ = _poster.SetEditing(target);
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
        _controlCenter?.ApplyProjection(args.Projection.ControlCenter);
        _tray?.ApplyProjection(args.Projection.Tray);
        _ = _poster?.ApplyProjection(args);
    }

    private void TogglePosterLocked(StateCommandSource source)
    {
        if (!TryGetProjection(source, "lock", out DesktopUpProjection? projection))
        {
            return;
        }
        DesktopUpProjection currentProjection = projection!;
        PosterDefinition desired = currentProjection.Poster.Poster with
        {
            IsLocked = !currentProjection.Poster.Poster.IsLocked
        };
        StateCommandResult result = _stateCommands!.Execute(new PosterStateCommand(
            $"{SourceToken(source)}-lock-{Guid.NewGuid():N}",
            source,
            PosterStateCommandKind.SetLocked,
            currentProjection.Revision,
            desired.PosterId,
            desired));
        LogCommandResult("poster-lock", result);
    }

    private void TogglePosterCollapsed(StateCommandSource source)
    {
        if (!TryGetProjection(source, "collapse", out DesktopUpProjection? projection))
        {
            return;
        }
        DesktopUpProjection currentProjection = projection!;
        PosterDefinition desired = currentProjection.Poster.Poster with
        {
            IsCollapsed = !currentProjection.Poster.Poster.IsCollapsed
        };
        StateCommandResult result = _stateCommands!.Execute(new PosterStateCommand(
            $"{SourceToken(source)}-collapse-{Guid.NewGuid():N}",
            source,
            PosterStateCommandKind.SetCollapsed,
            currentProjection.Revision,
            desired.PosterId,
            desired));
        LogCommandResult("poster-collapse", result);
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
            desired));
        LogCommandResult("dynamic-effects-pause", result);
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

    private void ExitApplication()
    {
        if (Interlocked.Exchange(ref _exiting, 1) != 0)
        {
            return;
        }
        _log?.Write("info", "application.exit_requested");
        StopDesktopEnvironmentMonitoring();
        StopDesktopDiscovery();
        _poster?.Dispose();
        _poster = null;
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
        _singleInstance.Dispose();
        _ledger?.WriteSnapshot();
        _log?.Write("info", "application.stopped", new
        {
            activeResources = _ledger?.ActiveResources.Count ?? 0
        });
        _services?.Dispose();
        _services = null;
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
            _desktopEnvironmentWatcher.PointerActivationGuarded -=
                OnPosterPointerActivationGuarded;
            _desktopEnvironmentWatcher.LayerActivationGuarded -=
                OnPosterLayerActivationGuarded;
            _desktopEnvironmentWatcher.ZOrderChangeConstrained -=
                OnPosterZOrderChangeConstrained;
            _desktopEnvironmentWatcher.Dispose();
            _desktopEnvironmentWatcher = null;
        }
        _desktopEnvironmentWatcherLease?.Dispose();
        _desktopEnvironmentWatcherLease = null;

        _pendingDesktopRecoveryKinds = DesktopEnvironmentChangeKind.None;
        _pendingDesktopRecoveryReasons.Clear();
        _desktopEnvironmentCancellation?.Dispose();
        _desktopEnvironmentCancellation = null;
        _log?.Write("info", "desktop_environment.monitoring_stopped");
    }

    private void StopDesktopDiscovery()
    {
        _desktopDiscoveryCancellation?.Cancel();
        try
        {
            _desktopDiscoveryTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        _desktopDiscoveryTask = null;
        _desktopDiscoveryCancellation?.Dispose();
        _desktopDiscoveryCancellation = null;
    }

    private static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
