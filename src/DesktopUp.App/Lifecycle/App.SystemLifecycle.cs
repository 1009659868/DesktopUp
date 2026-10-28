using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

public partial class App
{
    private readonly SystemLifecycleGate _systemLifecycle = new();
    private void OnSystemLifecycleChanged(SystemLifecycleSignal signal)
    {
        if (Volatile.Read(ref _exiting) != 0) return;
        var action = _systemLifecycle.Observe(signal);
        _log?.Write("info", "system_lifecycle.observed", new { signal = signal.ToString(), action = action.ToString() });
        if (action == SystemLifecycleAction.Suspend)
        {
            long request = _desktopRecoveryVersionGate.Signal();
            SuspendAutoPosture();
            _inputRouter.Suspend();
            _controlCenter?.CancelRemoval("系统会话已暂停，删除确认已取消。");
            _spatialRuntime?.Suspend();
            _posterSessions?.SuspendEnvironment(request);
            _desktopRecoveryTimer?.Stop();
            _controlCenterEnvironmentPending = true;
            RefreshControlCenterRuntime();
        }
        else if (action == SystemLifecycleAction.Recover)
        {
            _managedContentMonitor?.Invalidate();
            QueueDesktopRecovery(DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
                "lifecycle-" + signal, invalidateShell: true);
        }
    }
}
