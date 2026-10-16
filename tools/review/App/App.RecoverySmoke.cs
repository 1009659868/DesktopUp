using DesktopUp.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Read-only gate: confirms the real offline window opens without starting any live state consumers.
    // Restore/interrupt file effects are covered by infrastructure tests; native confirmations remain manual.
    private void StartOfflineRecoverySmokeIfRequested()
    {
        if (!_startupIntent.Arguments.Contains("--recovery-smoke", StringComparer.Ordinal)) return;
        string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
        string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
        string actual = Path.GetFullPath(_services!.GetRequiredService<IUserDataPaths>().Root);
        if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
            actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        { _log!.Write("error", "contract.recovery_smoke_refused", new { reason = "isolated-profile-required" }); return; }
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.recovery", "read-only-offline-window");
        long deadline = _autoClock.ElapsedMilliseconds + 65000;
        bool stopped = false;
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (_recoveryWindow is not { } window) return;
            var snapshot = window.SnapshotForSmoke();
            if ((!snapshot.Loaded || snapshot.Busy) && _autoClock.ElapsedMilliseconds < deadline) return;
            bool passed = snapshot.Loaded && !snapshot.Busy && !_storageInitialized && _tray is null &&
                _posterSessions is null && _controlCenter is null && _stateCoordinator is null &&
                _inputRouter.ReadyCount == 0 && (!snapshot.Pending || !snapshot.CanRestart && !snapshot.CanRestore);
            Stop();
            _log!.Write(passed ? "info" : "error", "contract.recovery_smoke", new
            { passed, snapshot, restoreExecuted = false, nativeConfirmation = false });
            ExitApplication();
        }
        timer.Tick += Tick; timer.Start();
    }
}
