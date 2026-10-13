using DesktopUp.Application;
using DesktopUp.Windows;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private IPosterDropRegistration? _fileDrop;
    private readonly Guid _incomingRun=Guid.NewGuid();
    private Border? _incomingMarker;
    internal long TransferEntityVersion=>_posterEntityVersion;
    internal bool CanTransferFiles=>!_disposed&&!_contentPresentationSuspended&&!_geometry.IsLocked&&
        (!_contentReadOnly || _authoritativePoster?.IsCollapsed==true&&!_contentParseReadOnly)&&
        _transitionLayout is null&&!HasContentGesture&&!spatial.IsPosterSpatialBusy(PosterId!,includeFileTransfer:false)&&!spatial.HasCenterDraft(PosterId!);
    internal void TransferFeedback(string message)
    {
        if(!_disposed)PublishContentFeedback(message,transient:true);
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
    private bool _nativeContentDrag;
    private NativeShellDragSource? _nativeShellSource;
    private bool HandleDesktopTransfer(ContentGesture gesture,PosterInputEventArgs args)
    {
        if(_nativeContentDrag || !IsRealContent || gesture.Target!=PosterInputTargetKind.ContentItem || !gesture.CanEdit ||
            args.EventKind!=PosterInputEventKind.GesturePreview)return false;
        var point=args.LatestPoint;
        bool outside=point.X<_bounds.X||point.Y<_bounds.Y||point.X>=_bounds.X+_bounds.Width||point.Y>=_bounds.Y+_bounds.Height;
        if(!outside)return false;
        DesktopUp.Infrastructure.ShellExitIntent? intent=null;
        NativeShellDragResult? result=null;
        try
        {
            intent=spatial.BeginNativeExit(this,gesture.TransferSelection);
            if(intent is null){CancelContentGesture("transfer-unavailable");return true;}
            StopContentMotion();_contentDraft=_contentDraftProjection=null;gesture.Proposal=null;gesture.LastMapping=null;
            gesture.IsDragging=true;_contentActivation.Reset();PublishContentFeedback(null);
            var press=new PointerScreenPoint(point.X-args.DeltaX,point.Y-args.DeltaY);
            long sequence=args.Sequence;
            bool Current()=>ReferenceEquals(_contentGesture,gesture)&&!_disposed&&!_contentPresentationSuspended&&
                _posterEntityVersion==gesture.EntityVersion&&_bounds==gesture.Frame&&spatial.NativeExitCurrent;
            PosterInputEventArgs Input(PosterInputEventKind kind,FileDropPoint at)=>PosterContentInputPolicy.Event(
                gesture.Capture,kind,press,new(at.X,at.Y),args.LayoutRevision,++sequence);
            using var source=new NativeShellDragSource(_inputIsland!.WindowHandle,
                intent.Items.Where(i=>!i.Missing).Select(i=>i.Path).ToArray(),Current,
                at=>{if(Current())HandleContentInput(Input(PosterInputEventKind.GesturePreview,at));});
            _nativeShellSource=source;
            using var imageHelper=new ShellDropImages();
            _contentDragImage?.InitializeShell(source.Data,imageHelper);
            ReleaseContentDragImage();
            if(!_inputIsland.HandOffContentCapture(gesture.Capture))throw new IOException("拖动捕获已变化，文件未移动。");
            _nativeContentDrag=true;
            using var lease=ledger.Acquire("ole.file-drag-source",PosterId!);
            result=source.Run(spatial.NativeExitStopping);
            if(result.ReturnedToOrigin&&Current())
                HandleContentInput(Input(PosterInputEventKind.GestureCompleted,result.Point));
            else CancelContentGesture("native-shell-ended",quiet:result.Released);
        }
        catch(Exception e)
        {
            CancelContentGesture("native-shell-failed");
            TransferFeedback("本次拖放未完成，可重新拖动："+e.Message);
            log.Write("warning","transfer.native_shell_failed",new{e.Message});
        }
        finally
        {
            _nativeContentDrag=false;
            _nativeShellSource=null;
            if(intent is not null)spatial.CompleteNativeExit(this,intent,result);
        }
        return true;
    }
}
