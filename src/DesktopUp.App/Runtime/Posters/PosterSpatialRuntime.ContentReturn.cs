using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private ContentReturnUseCase? _contentReturns;
    internal void ConfigureContentReturns(ContentReturnUseCase returns) => _contentReturns = returns;

    internal async Task<PosterContentReturnResult> ReturnAndRemovePoster(RemovePosterCommand command, nint owner)
    {
        // The deletion confirmation itself owns the dialog lease; do not reject our own lease.
        if (!FileServiceReady || HasCenterDraft(command.PosterId) || _contentReturns is null)
            return new(false, 0, [], ["文件操作或编辑尚未结束，请稍后删除海报。"]);
        using var operation = BeginFileOperation(command.PosterId);
        try
        {
            var result = await Task.Run(() => _contentReturns.Execute(command, owner,
                () => !ContentOperationsStopping && !_suspended, _contentOperations.Stopping));
            log.Write(result.Removed ? "info" : "warning", "content.return_completed", new { command.PosterId, result.Removed, result.Returned, result.Destinations, result.Errors });
            return result;
        }
        finally
        {
            FinishFileOperation(operation);
        }
    }
}
