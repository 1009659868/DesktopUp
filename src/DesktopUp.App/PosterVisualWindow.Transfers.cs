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
    private IncomingDropPreview? _incomingPreview;
    private IncomingLayoutPreview? _incomingLayout;
    private (ContentLayoutDocument Document, IncomingContentPlacement Landing, int Count, long Entity, DesktopPixelRect Frame)? _incomingBasis;
    internal long TransferEntityVersion=>_posterEntityVersion;
    internal nint TransferOwner=>_windowHandle;
    internal bool CanReceiveFiles => CanTransferFiles && spatial.MappingAvailable(PosterId!);
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
            _fileDrop=_inputIsland.InstallDropReceiver(()=>CanReceiveFiles&&spatial.CanReceiveInto(PosterId!)?IncomingSurface():null,
                ticket=>
                {
                    var placement=IncomingContentPlacement.From(EnsureContentSnapshot(_bounds),_contentViewport!,ticket.Preview.Location);
                    spatial.PrepareMappedDropHandoff(PosterId!);
                    _window?.DispatcherQueue.TryEnqueue(()=>
                    {if(CanReceiveFiles && ticket.Preview.Surface==IncomingSurface())spatial.ReceiveFiles(this,ticket,placement);});
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
            ContentLayoutFlow.IsLooping(snapshot,viewport)?ContentLayoutFlow.LoopPeriodDip(snapshot,viewport):0)
            { SupportsCopy=spatial.ContentActionPath(PosterId!,null) is not null };
    }
    internal bool IncomingGeometryCurrent(IncomingDropSurface start) => !_disposed&&!_contentPresentationSuspended &&
        !_geometry.IsLocked && !HasContentGesture && _transitionLayout is null &&
        start.RunId==_incomingRun && start.Frame==_bounds && start.ScaleX==(_monitor?.ScaleX??1) && start.ScaleY==(_monitor?.ScaleY??1);
    private void ShowIncomingPreview(IncomingDropPreview? preview)
    {
        bool hadPreview = _incomingLayout is not null;
        _incomingPreview = preview;
        if (preview?.Location.Area != IncomingDropArea.Content || !CanTransferFiles)
        {
            _incomingLayout = null; _incomingBasis = null;
            if (!_disposed && !_contentPresentationSuspended && hadPreview) RedrawIncomingPreview();
            if (preview is null && !spatial.IsFileTransferActive(PosterId!)) PublishContentFeedback(null);
            if (preview is null && !_disposed) QueueContentProposalDrain();
            return;
        }
        var snapshot = EnsureContentSnapshot(_bounds);
        var landing = IncomingContentPlacement.From(snapshot, _contentViewport!, preview.Location)!;
        // A move within one cell/order does not run the solver or rebuild any visual.
        var basis = (ContentDocument, landing, preview.Count, _posterEntityVersion, _bounds);
        if (_incomingBasis == basis) return;
        _incomingBasis = basis;
        try { _incomingLayout = IncomingLayoutPreview.Create(ContentDocument, landing, preview.Count); }
        catch (Exception error) when (error is ArgumentException or IOException or OverflowException)
        { _incomingLayout = null; } // No invented layout; Drop still revalidates through the normal transaction.
        RedrawIncomingPreview();
    }

    private void RedrawIncomingPreview()
    {
        if (!_disposed && !_contentPresentationSuspended && _contentSnapshot is { } snapshot && _contentViewport is { } viewport)
            DrawContentSnapshot(snapshot, viewport); // Paint only: never change native hit/surface witnesses.
    }

    private IncomingLayoutPreview? IncomingPaintPreview(ContentLayoutViewport viewport)
    {
        if (_incomingLayout is null) return null;
        if (_incomingBasis is { } basis && ReferenceEquals(basis.Document, ContentDocument) &&
            basis.Landing.Viewport == viewport && basis.Entity == _posterEntityVersion && basis.Frame == _bounds && CanTransferFiles)
            return _incomingLayout;
        _incomingLayout = null; _incomingBasis = null;
        return null;
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
