using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal async Task<PosterContentReturnResult> ReturnAndRemovePoster(RemovePosterCommand command, nint owner)
    {
        // The deletion confirmation itself owns the dialog lease; do not reject our own lease.
        if (!FileServiceReady || HasCenterDraft(command.PosterId))
            return new(false, 0, [], ["文件操作或编辑尚未结束，请稍后删除海报。"]);
        _fileTransferBusy = true; _fileTransferPoster = command.PosterId; ActivityChanged?.Invoke();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fileTransferTask = done.Task;
        try
        {
            var result = await Task.Run(() => new PosterContentReturn(_transferStore!).Execute(command, owner,
                () => !_fileTransferStopping.IsCancellationRequested && !_suspended, _fileTransferStopping.Token));
            log.Write(result.Removed ? "info" : "warning", "content.return_completed", new { command.PosterId, result.Removed, result.Returned, result.Destinations, result.Errors });
            return result;
        }
        finally
        {
            try { state.Refresh(); }
            finally { _fileTransferBusy = false; _fileTransferPoster = null; done.TrySetResult(); ActivityChanged?.Invoke(); }
        }
    }
}
