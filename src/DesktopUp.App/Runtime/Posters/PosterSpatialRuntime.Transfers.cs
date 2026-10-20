using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private bool _transfersConfigured;
    private Func<Task<RegisteredManagedStorage?>>? _chooseTransferStorage;
    private Func<bool>? _transferDialogsBusy;
    private Action<string>? _showTransferFailure;
    private Task _transferStartup=Task.CompletedTask;
    private readonly ContentOperationLifetime _contentOperations = new(error =>
        log.Write("warning", "content.operation_failed", new { error.Message, type = error.GetType().Name }));
    internal bool ContentOperationsStopping => _contentOperations.IsStopping;
    internal bool IsFileTransferActive(string id) => _contentOperations.IsActiveFor(id);
    internal bool FileServiceReady => _transfersConfigured && _transferStartup.IsCompletedSuccessfully &&
        !ContentOperationsStopping && !_contentOperations.FileOperationActive && !_suspended;
    internal bool TransfersAvailable => FileServiceReady && _transferDialogsBusy?.Invoke()!=true;
    internal void ConfigureTransfers(Task startup,Func<Task<RegisteredManagedStorage?>> choose,
        Func<bool> dialogsBusy,Action<string> failure)
    { _transfersConfigured=true;_transferStartup=startup;_chooseTransferStorage=choose;_transferDialogsBusy=dialogsBusy;_showTransferFailure=failure; }
    internal Task StopTransfersAsync() => _contentOperations.StopFileOperationsAsync();

    private ContentOperationLifetime.FileOperation BeginFileOperation(string posterId, string? source = null)
    {
        var operation = _contentOperations.BeginFileOperation(posterId, source);
        try { ActivityChanged?.Invoke(); return operation; }
        catch { operation.Dispose(); throw; }
    }

    private void FinishFileOperation(ContentOperationLifetime.FileOperation operation)
    {
        // Publication can fail after a durable change. Release admission regardless;
        // the caller reports that failure and must never replay the file action.
        try { state.Refresh(); }
        finally { operation.Dispose(); ActivityChanged?.Invoke(); }
    }

}
