using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

public partial class App
{
    private void QueueTrayAction(TrayAction action)
    {
        // Return from the native menu/notification callback before touching XAML or disposing windows.
        _ = _dispatcher?.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _exiting) != 0) return;
            try
            {
                _tray?.ClearFeedback();
                switch (action)
                {
                    case TrayAction.Create: ShowTrayResult(CreateTrayPoster()); break;
                    case TrayAction.OpenCenter: _controlCenter?.Show(); break;
                    case TrayAction.Visibility: SetTrayGlobalHidden(_posterSessions?.GloballyHidden != true); break;
                    case TrayAction.LockAll: TogglePosterLocked(StateCommandSource.Tray); break;
                    case TrayAction.Motion: ToggleDynamicEffectsPaused(StateCommandSource.Tray); break;
                    case TrayAction.Exit: ExitApplication(); return;
                }
                _log?.Write("info", "tray.action_dispatched", new { action = action.ToString() });
            }
            catch (Exception ex)
            {
                _log?.Write("error", "tray.action_failed", new { action = action.ToString(), ex.Message, ex.HResult });
                _tray?.Notify("操作未完成，请检查当前状态后重试。");
            }
            finally { if (Volatile.Read(ref _exiting) == 0) RefreshControlCenterRuntime(); }
        });
    }

    private StateCommandResult CreateTrayPoster()
    {
        if (_stateCoordinator?.TryGetCurrent(out var current) != true || current is null ||
            _spatialRuntime is null || _controlCenterEnvironmentPending || _systemLifecycle.IsSuspended)
            return new(StateCommandStatus.NotReady, "tray-create-unavailable", 0, 0, null, "桌面尚未就绪，请稍后新增。");
        return _spatialRuntime.ApplyControlCenterCatalog(TrayManagement.Create(current, $"tray-create-{Guid.NewGuid():N}"));
    }

    private void SetTrayGlobalHidden(bool hidden)
    {
        if (Volatile.Read(ref _exiting) != 0 || _posterSessions is null || _posterSessions.GloballyHidden == hidden) return;
        // Cancel the old environment/gestures before releasing sessions. Restore cannot start on a stale environment.
        QueueDesktopRecovery(DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
            hidden ? "tray-hide-all" : "tray-restore-all", invalidateShell: false);
        _posterSessions.SetGloballyHidden(hidden);
        if (hidden && _posterSessions.Sessions.Count != 0)
            _tray?.Notify("部分海报释放失败，已停止交互；请查看控制中心运行状态。");
        RefreshControlCenterRuntime();
        ObserveAutoPosture();
        _log?.Write("info", "tray.visibility_changed", new { hidden, sessions = _posterSessions.Sessions.Count });
    }

    private void ShowTrayResult(StateCommandResult result)
    {
        _controlCenter?.ShowCommandResult(result); // Does not open the center or select another poster.
        _log?.Write(result.Succeeded ? "info" : "warning", "tray.command_completed", new
        { result.RequestId, status = result.Status.ToString(), result.CurrentRevision, result.ResultPosterId, result.Error });
        if (!result.Succeeded) _tray?.Notify(result.Error ?? "状态已变化，操作未应用，请重试。");
        else if (result.ResultPosterId is not null && _posterSessions?.GloballyHidden == true)
            _tray?.Notify("海报已新增；当前全局隐藏，恢复显示后可见。", error: false);
    }
}
