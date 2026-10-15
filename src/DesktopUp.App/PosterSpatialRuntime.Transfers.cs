using DesktopUp.Infrastructure;
using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private SqliteStateStore? _transferStore;
    private Func<Task<RegisteredManagedStorage?>>? _chooseTransferStorage;
    private Func<string,Task<bool>>? _confirmTransfer;
    private Func<bool>? _transferDialogsBusy;
    private Action<string>? _showTransferFailure;
    private Task _transferStartup=Task.CompletedTask;
    private readonly CancellationTokenSource _fileTransferStopping=new();
    private Task _fileTransferTask=Task.CompletedTask;
    private bool _fileTransferBusy;
    private string? _fileTransferPoster;
    internal bool IsFileTransferActive(string id)=>_fileTransferBusy&&_fileTransferPoster==id;
    internal bool FileServiceReady => _transferStore is not null && _transferStartup.IsCompletedSuccessfully &&
        !_fileTransferStopping.IsCancellationRequested && !_fileTransferBusy && !_suspended;
    internal bool TransfersAvailable => FileServiceReady && _transferDialogsBusy?.Invoke()!=true;
    internal void ConfigureTransfers(SqliteStateStore store,Task startup,Func<Task<RegisteredManagedStorage?>> choose,
        Func<string,Task<bool>> confirm,Func<bool> dialogsBusy,Action<string> failure)
    { _transferStore=store;_transferStartup=startup;_chooseTransferStorage=choose;_confirmTransfer=confirm;_transferDialogsBusy=dialogsBusy;_showTransferFailure=failure; }
    internal Task StopTransfersAsync()
    { _fileTransferStopping.Cancel();return _fileTransferTask; }

}
