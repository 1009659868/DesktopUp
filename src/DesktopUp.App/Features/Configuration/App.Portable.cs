using System.Diagnostics;
using DesktopUp.Application;

namespace DesktopUp;

public partial class App
{
    private PortableProfileUseCase? _portableController;
    private IStateBackupRecovery? _offlineRecovery;
    private StateRecoveryWindow? _recoveryWindow;
    private IDisposable? _recoveryWindowLease;
    private bool _storageInitialized;
    private bool _restartRequested, _restartForRecovery;
    private string? _restartDataRoot;

    private bool TryEnterOfflineRecovery()
    {
        var paths = _composition!.Paths;
        _offlineRecovery = CreateOfflineRecovery(paths);
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
        StartOfflineRecoveryReview();
    }

    partial void StartOfflineRecoveryReview();

    private void RequestReplacement(bool recovery)
    {
        if (_recoveryWindow?.IsBusy == true || Volatile.Read(ref _exiting) != 0) return;
        if (!recovery && _offlineRecovery?.HasPendingRecovery == true) return;
        _restartRequested = true; _restartForRecovery = recovery;
        _restartDataRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
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
            if (!string.IsNullOrWhiteSpace(_restartDataRoot)) start.Environment["DESKTOPUP_V3_TEST_DATA_ROOT"] = _restartDataRoot;
            Process.Start(start)?.Dispose();
        }
        catch (Exception error)
        {
            // Logging was deliberately closed before launching. Retain a narrow restart diagnostic.
            System.Diagnostics.Trace.WriteLine("DesktopUp replacement could not start: " + error.Message);
        }
    }
}
