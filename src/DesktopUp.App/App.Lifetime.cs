
namespace DesktopUp;

public partial class App
{
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
        if (_spatialRuntime is not null) await _spatialRuntime.StopTransfersAsync();
        await _transferDiscovery;
        if (_managedContentMonitor is not null) await _managedContentMonitor.DisposeAsync();
        _managedContentMonitor = null;
        _managedContentMonitorLease?.Dispose(); _managedContentMonitorLease = null;
        _inputRouter.Suspend();
        _controlCenter?.CancelRemoval("application-exit");
        DebugSettings.XamlResourceReferenceFailed -= OnXamlResourceReferenceFailed;
        StopDesktopEnvironmentMonitoring();
        _posterSessions?.Dispose();
        _posterSessions = null;
        if (_composition is not null) await _composition.StopWorkerAsync();
        if (_spatialRuntime is not null) await _spatialRuntime.WaitForContentOperationsAsync();
        if (_composition is not null) await _composition.StopActivityAsync();
        // Cancellation completes on the UI dispatcher before composition/logging and the final ledger close.
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
        _composition?.Dispose();
        _composition = null;
        _profileLifecycleLease?.Dispose(); _profileLifecycleLease = null;
        _storageInitialized = false;
        StartRequestedReplacement();
        Exit();
    }
}
