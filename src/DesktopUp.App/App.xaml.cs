using System.Collections.Concurrent;
using System.Security.Principal;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
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
    private ApplicationComposition? _composition;
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
        _isPrimary = _singleInstance.TryAcquireOrForward(_startupIntent.ForExistingInstance(),
            forwardExisting: !IsUninstallMode && _startupIntent.Kind != StartupIntentKind.AutoStart);
    }

    // Optional development hooks have no implementation in a product build.
    partial void RunReviewTools();
    partial void StopReviewTools();

    private void OnExternalIntent(StartupIntent intent)
    {
        if (IsUninstallMode) return; // Maintenance is not an interactive primary instance.
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
            case StartupIntentKind.AutoStart:
                break; // Includes zero posters: login must not open/focus the center.
            case StartupIntentKind.StartBackground:
            case StartupIntentKind.ShowControlCenter:
                bool hasPosters = _stateCoordinator is not null && _stateCoordinator.TryGetCurrent(out var current) &&
                    current is not null && current.Posters.Count > 0;
                if (intent.ShouldShowControlCenter(hasPosters)) ShowControlCenter();
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
        InvalidateObservationTargets(args);
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

    private static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
