using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public partial class App
{
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
            _composition is null ||
            _posterSessions is null)
        {
            return;
        }

        try
        {
            IDesktopShellLocator locator =
                _composition.Shell;
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
                _composition.Displays;
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
            _composition is null ||
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
            _composition.Shell.Invalidate();
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
            _composition is null ||
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
        if (_composition is null || _posterSessions is null)
        {
            return;
        }

        IDesktopShellLocator locator =
            _composition.Shell;
        IDisplayTopologyService topologyService =
            _composition.Displays;
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
                current = _stateCoordinator.EnsureInitialized();
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
}
