using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private bool _transfersConfigured;
    private Func<Task<RegisteredManagedStorage?>>? _chooseTransferStorage;
    private Func<bool>? _transferDialogsBusy;
    private Action<string>? _showTransferFailure;
    private Task _transferStartup=Task.CompletedTask;
    private readonly CancellationTokenSource _fileTransferStopping=new();
    private Task _fileTransferTask=Task.CompletedTask;
    private bool _fileTransferBusy;
    private string? _fileTransferPoster;
    private string? _crossPosterSource;
    internal bool IsFileTransferActive(string id)=>_fileTransferBusy&&(_fileTransferPoster==id || _crossPosterSource==id);
    internal bool FileServiceReady => _transfersConfigured && _transferStartup.IsCompletedSuccessfully &&
        !_fileTransferStopping.IsCancellationRequested && !_fileTransferBusy && !_suspended;
    internal bool TransfersAvailable => FileServiceReady && _transferDialogsBusy?.Invoke()!=true;
    internal void ConfigureTransfers(Task startup,Func<Task<RegisteredManagedStorage?>> choose,
        Func<bool> dialogsBusy,Action<string> failure)
    { _transfersConfigured=true;_transferStartup=startup;_chooseTransferStorage=choose;_transferDialogsBusy=dialogsBusy;_showTransferFailure=failure; }
    internal Task StopTransfersAsync()
    { _fileTransferStopping.Cancel();return _fileTransferTask; }

}
