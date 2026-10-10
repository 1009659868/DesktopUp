using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    private PortableProfileController? _portableController;
    private StateBackupRecovery? _offlineRecovery;
    private StateRecoveryWindow? _recoveryWindow;
    private IDisposable? _recoveryWindowLease;
    private bool _storageInitialized;
    private bool _restartRequested, _restartForRecovery;

    private bool TryEnterOfflineRecovery()
    {
        var paths = _services!.GetRequiredService<IUserDataPaths>();
        _offlineRecovery = new(paths, () => _isPrimary && !_storageInitialized);
        if (_offlineRecovery.HasPendingRecovery)
        {
            OpenOfflineRecovery("检测到上次未收尾的恢复记录。已阻止打开状态库；请明确处理该记录。");
            return true;
        }
        if (_startupIntent.Arguments.Contains("--recover-state", StringComparer.OrdinalIgnoreCase))
        { OpenOfflineRecovery("本机数据库离线恢复；尚未选择或应用任何备份。"); return true; }
        return false;
    }

    private void OpenOfflineRecovery(string reason)
    {
        if (_storageInitialized) throw new InvalidOperationException("Cannot open recovery while state is live.");
        _recoveryWindow = new StateRecoveryWindow(_offlineRecovery!, reason,
            () => RequestReplacement(false), ExitApplication);
        _recoveryWindowLease = _ledger!.Acquire("window.state-recovery", "offline-only");
        _recoveryWindow.Closed += (_, _) => { _recoveryWindowLease?.Dispose(); _recoveryWindowLease = null; };
        _recoveryWindow.Activate();
        _log?.Write("info", "state_recovery.opened", new { reason, pending = _offlineRecovery!.HasPendingRecovery });
    }

    private void ConfigurePortableProfiles()
    {
        var services = _services ?? throw new InvalidOperationException("Application services are not ready.");
        var coordinator = services.GetRequiredService<StateProjectionCoordinator>();
        bool Guard(PortableImportPlan plan) => Volatile.Read(ref _exiting) == 0 &&
            _spatialRuntime?.CanApplyPortable(plan) == true;
        coordinator.BindPortableRuntime(Guard);
        _portableController = new(services.GetRequiredService<IStateCommandRepository>(), coordinator,
            services.GetRequiredService<IStateStore>(), services.GetRequiredService<IUserDataPaths>(),
            () => _spatialRuntime?.PortableTopology(), Guard, () => RequestReplacement(true));
    }

    private void RequestReplacement(bool recovery)
    {
        if (_recoveryWindow?.IsBusy == true || Volatile.Read(ref _exiting) != 0) return;
        if (!recovery && _offlineRecovery?.HasPendingRecovery == true) return;
        _restartRequested = true; _restartForRecovery = recovery;
        ExitApplication();
    }

    private void StartRequestedReplacement()
    {
        if (!_restartRequested) return;
        try
        {
            // All windows, state connections and the single-instance owner have been released.
            // Never replay diagnostics or smoke flags into a replacement process.
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add(_restartForRecovery ? "--recover-state" : "--show-control-center");
            Process.Start(start)?.Dispose();
        }
        catch (Exception error)
        {
            // Logging was deliberately closed before launching. Retain a narrow restart diagnostic.
            System.Diagnostics.Trace.WriteLine("DesktopUp replacement could not start: " + error.Message);
        }
    }
}
