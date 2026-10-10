using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private NativeFileDropTarget? _fileDrop;
    private bool _fileTransferFeedback;
    internal long TransferEntityVersion=>_posterEntityVersion;
    internal bool CanTransferFiles=>!_disposed&&!_contentPresentationSuspended&&!_geometry.IsLocked&&!_contentReadOnly&&
        _transitionLayout is null&&!HasContentGesture&&!spatial.IsPosterSpatialBusy(PosterId!)&&!spatial.HasCenterDraft(PosterId!);
    internal void TransferFeedback(string message)
    {
        if(!_disposed){_fileTransferFeedback=true;PublishContentFeedback(message,transient:true);}
    }
    private void AttachFileDrop()
    {
        if(_inputIsland is null)return;
        try
        {
            _fileDrop=new(_inputIsland.WindowHandle,()=>CanTransferFiles&&spatial.TransfersAvailable,
                paths=>
                {
                    long version=_posterEntityVersion;
                    _window?.DispatcherQueue.TryEnqueue(()=>
                    {if(version==_posterEntityVersion&&!_disposed)spatial.TransferFiles(this,paths,null);});
                },ledger,log);
            log.Write("info","transfer.native_target_ready",new{PosterId});
        }
        catch(Exception e){log.Write("warning","transfer.native_target_unavailable",new{PosterId,e.Message});}
    }
    private bool HandleDesktopTransfer(ContentGesture gesture,PosterInputEventArgs args)
    {
        if(!IsRealContent||gesture.Target!=PosterInputTargetKind.ContentItem||!gesture.CanEdit||
            args.EventKind is not (PosterInputEventKind.GesturePreview or PosterInputEventKind.GestureCompleted))return false;
        var point=args.LatestPoint;
        bool outside=point.X<_bounds.X||point.Y<_bounds.Y||point.X>=_bounds.X+_bounds.Width||point.Y>=_bounds.Y+_bounds.Height;
        if(!outside)return false; // Returning inside uses the original capture and layout model.
        StopContentMotion();_contentDraft=_contentDraftProjection=null;gesture.Proposal=null;gesture.LastMapping=null;
        gesture.IsDragging=true;_contentActivation.Reset();
        bool desktop=NativeDesktopDropSite.IsDesktop(point.X,point.Y);
        var decision=DirectDesktopDropPolicy.Resolve(args.EventKind,point,_bounds,true,gesture.CanEdit,desktop,
            NativeDesktopDropSite.MoveModifier,spatial.TransfersAvailable);
        bool allowed=decision is DirectDesktopDropDecision.Preview or DirectDesktopDropDecision.Commit;
        PublishContentFeedback(allowed?"松手取回桌面；Esc取消。":"此处不能取出；移回海报可继续整理，Esc取消。");
        if(args.EventKind==PosterInputEventKind.GestureCompleted)
        {
            _contentGesture=null;gesture.Lease.Dispose();
            RefreshContentSurface();QueueContentProposalDrain();spatial.NotifyContentEditorChanged(PosterId);
            if(allowed)spatial.TransferFiles(this,null,gesture.TransferSelection);
        }
        else RefreshContentSurface();
        return true;
    }
}
