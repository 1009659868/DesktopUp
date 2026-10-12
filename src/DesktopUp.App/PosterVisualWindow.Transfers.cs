using DesktopUp.Application;
using DesktopUp.Windows;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private IPosterDropRegistration? _fileDrop;
    private bool _fileTransferFeedback;
    private readonly Guid _incomingRun=Guid.NewGuid();
    private Border? _incomingMarker;
    internal long TransferEntityVersion=>_posterEntityVersion;
    internal bool CanTransferFiles=>!_disposed&&!_contentPresentationSuspended&&!_geometry.IsLocked&&
        (!_contentReadOnly || _authoritativePoster?.IsCollapsed==true&&!_contentParseReadOnly)&&
        _transitionLayout is null&&!HasContentGesture&&!spatial.IsPosterSpatialBusy(PosterId!,includeFileTransfer:false)&&!spatial.HasCenterDraft(PosterId!);
    internal void TransferFeedback(string message)
    {
        if(!_disposed){_fileTransferFeedback=true;PublishContentFeedback(message,transient:true);}
    }
    private void AttachFileDrop()
    {
        if(_inputIsland is null)return;
        try
        {
            _fileDrop=_inputIsland.InstallDropReceiver(()=>CanTransferFiles&&spatial.TransfersAvailable?IncomingSurface():null,
                ticket=>
                {
                    var placement=IncomingContentPlacement.From(EnsureContentSnapshot(_bounds),_contentViewport!,ticket.Preview.Location);
                    _window?.DispatcherQueue.TryEnqueue(()=>
                    {if(CanTransferFiles && ticket.Preview.Surface==IncomingSurface())spatial.ReceiveFiles(this,ticket,placement);});
                },ShowIncomingPreview);
            log.Write("info","transfer.native_target_ready",new{PosterId});
        }
        catch(Exception e){_fileDrop?.Dispose();_fileDrop=null;log.Write("warning","transfer.native_target_unavailable",new{PosterId,e.Message});}
    }
    private IncomingDropSurface IncomingSurface()
    {
        var snapshot=EnsureContentSnapshot(_bounds);var viewport=_contentViewport!;
        bool collapsed=_authoritativePoster?.IsCollapsed==true;
        return new(_incomingRun,PosterId!,_posterEntityVersion,ContentDocument.DocumentVersion,snapshot.ViewportGeneration,
            _bounds,_monitor?.ScaleX??1,_monitor?.ScaleY??1,
            collapsed?new(0,0,0,0):new(ContentLeftDip,ContentTopDip,viewport.WidthDip,viewport.HeightDip),
            snapshot.FlowAxis,snapshot.ScrollXDip,snapshot.ScrollYDip,
            ContentLayoutFlow.IsLooping(snapshot,viewport)?ContentLayoutFlow.LoopPeriodDip(snapshot,viewport):0);
    }
    internal bool IncomingGeometryCurrent(IncomingDropSurface start) => !_disposed&&!_contentPresentationSuspended &&
        !_geometry.IsLocked && !HasContentGesture && _transitionLayout is null &&
        start.RunId==_incomingRun && start.Frame==_bounds && start.ScaleX==(_monitor?.ScaleX??1) && start.ScaleY==(_monitor?.ScaleY??1);
    private void ShowIncomingPreview(IncomingDropPreview? preview)
    {
        if(_incomingMarker is not null)_contentCanvas?.Children.Remove(_incomingMarker);
        _incomingMarker=null;
        if(preview is null){if(!spatial.IsFileTransferActive(PosterId!))PublishContentFeedback(null);return;}
        _fileTransferFeedback=true;
        if(preview.Location.Area==IncomingDropArea.Frame)
        {PublishContentFeedback($"松手收纳 {preview.Count} 项，按当前布局加入；Esc 取消。");return;}
        var s=EnsureContentSnapshot(_bounds);var landing=IncomingContentPlacement.From(s,_contentViewport!,preview.Location)!;
        PublishContentFeedback(ContentDocument.Mode==ContentLayoutMode.Manual
            ? $"松手放在第 {landing.Column+1} 列／{landing.Row+1} 行；其他内容局部让位。"
            : $"松手从第 {landing.ReadOrder+1} 位加入 {preview.Count} 项；成功项依次固定。");
        if(_contentCanvas is null)return;
        _incomingMarker=new Border {Width=18,Height=18,CornerRadius=new(9),BorderThickness=new(2),BorderBrush=Brush(240,110,200,245),IsHitTestVisible=false};
        Canvas.SetLeft(_incomingMarker,(preview.ScreenPoint.X-_bounds.X)/preview.Surface.ScaleX-ContentLeftDip-9);
        Canvas.SetTop(_incomingMarker,(preview.ScreenPoint.Y-_bounds.Y)/preview.Surface.ScaleY-ContentTopDip-9);
        _contentCanvas.Children.Add(_incomingMarker);
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
