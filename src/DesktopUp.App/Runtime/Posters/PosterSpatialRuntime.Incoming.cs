using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private CancellationTokenSource? _incomingCancellation;
    internal void ReceiveFiles(PosterVisualWindow window,IncomingDropTicket ticket,IncomingContentPlacement? landing,ShellExitIntent? origin=null)
    {
        if(origin is not null && _nativeExitOperation is { } source)
        {
            TrackContentOperation(AfterSource());return;
            async Task AfterSource(){await source.Completion;ReceiveFiles(window,ticket,landing,origin);}
        }
        if(!TransfersAvailable || !window.CanReceiveFiles || !window.IncomingGeometryCurrent(ticket.Preview.Surface))return;
        var operation = BeginFileOperation(window.PosterId!, origin?.PosterId);
        TrackContentOperation(RunIncoming(window, ticket, landing, operation, origin));
    }
    private async Task RunIncoming(PosterVisualWindow window,IncomingDropTicket ticket,IncomingContentPlacement? landing,ContentOperationLifetime.FileOperation operation,ShellExitIntent? origin=null)
    {
        using var active = operation;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_contentOperations.Stopping);
        _incomingCancellation = lifetime;
        string id=window.PosterId!;long entity=window.TransferEntityVersion;
        bool Current()=>!lifetime.IsCancellationRequested && !_suspended && _windows.GetValueOrDefault(id)==window &&
            window.IncomingGeometryCurrent(ticket.Preview.Surface) && window.TransferEntityVersion==entity && !HasCenterDraft(id);
        try
        {
            if(origin is not null && ticket.Preview.Effect==2)
            {
                var targetStorage=ContentActionPath(id,null) is null?await _chooseTransferStorage!():null;
                if(!Current())return;
                nint owner=window.TransferOwner;
                var result=await Task.Run(()=>_crossPosterTransfers!.Execute(origin,id,ticket.Paths,targetStorage,
                    landing,owner,()=>!lifetime.IsCancellationRequested,lifetime.Token,entity));
                state.Refresh();
                if(result.Errors.Count>0)_showTransferFailure?.Invoke(string.Join("\n",result.Errors));
                log.Write("info","transfer.cross_poster_finished",new{source=origin.PosterId,target=id,result.Completed,result.Skipped,result.Errors});
                return;
            }
            if(ContentActionPath(id,null) is not null)
            {
                // File/COM work stays off the UI thread. Read-set validation in the service
                // rejects late layout changes before applying the drop position.
                nint owner=window.TransferOwner;
                var result=await Task.Run(()=>_mappedIngress!.Execute(id,ticket.Paths,ticket.Preview.Effect==1,
                    landing,owner,()=>!lifetime.IsCancellationRequested,lifetime.Token,entity));
                state.Refresh();
                SetMappingOperationMessage(id,result.Errors.Count>0?string.Join("；",result.Errors):null);
                log.Write("info","transfer.mapped_ingress_finished",new{posterId=id,result.Completed,result.Skipped,result.Errors});
                return;
            }
            var storage=await _chooseTransferStorage!();
            if(storage is null || !Current())return;
            var managedResult = await _managedIngress!.Execute(id, ticket.Paths, storage, landing,
                new IncomingPresentation(Current,
                    (path, completed) => window.TransferFeedback($"正在收纳 {Path.GetFileName(path)}…（已完成 {completed} 项）"),
                    () => { state.Refresh(); entity = window.TransferEntityVersion; }), lifetime.Token);
            window.TransferFeedback(managedResult.LastError is null ? $"已收纳 {managedResult.Completed} 项。" : $"已收纳 {managedResult.Completed} 项；{managedResult.LastError}");
            log.Write("info","transfer.ingress_finished",new{posterId=id,completed=managedResult.Completed,failed=managedResult.Failed,total=managedResult.Total,lastError=managedResult.LastError});
        }
        catch(Exception e)
        {
            if(ContentActionPath(id,null) is not null)SetMappingOperationMessage(id,"本次未能完成："+e.Message);
            window.TransferFeedback("本次未能完成，可重新拖动："+e.Message);
            log.Write("warning","transfer.ingress_failed",new{posterId=id,e.Message});
        }
        finally
        {
            _incomingCancellation = null;
            FinishFileOperation(active);
        }
    }
}
