using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;
internal sealed partial class PosterVisualWindow
{
    // Real window/production event handler, no synthetic OS mouse and no file release.
    internal async Task CombinedSelectionProbe(string root)
    {
        string? original=_authoritativePoster!.ContentLayoutJson;
        RefreshContentSurface();
        var surface=inputRouter.ReadSurface(inputSession)!;
        var blank=surface.Layout.Regions.Single(r=>r.Kind==PosterInputTargetKind.ContentViewport);
        var start=new PointerScreenPoint(_bounds.X+blank.Bounds.Right-2,_bounds.Y+blank.Bounds.Bottom-2);
        var end=new PointerScreenPoint(_bounds.X+blank.Bounds.X+2,_bounds.Y+blank.Bounds.Y+2);
        var capture=new PosterInputCapture(Guid.NewGuid(),new(inputSession,surface.Layout.Revision,_bounds,blank));
        long sequence=0;
        void Send(PosterInputEventKind kind,PointerScreenPoint point)=>HandleContentInput(
            PosterContentInputPolicy.Event(capture,kind,start,point,_layoutRevision,++sequence));
        Send(PosterInputEventKind.Pressed,start);Send(PosterInputEventKind.GesturePreview,end);
        if(_contentMarquee is null || !_contentSelection.ShowHighlight || _contentSelection.Ids.Count==0)
            throw new IOException("Marquee did not preview explicit selection.");
        await CapturePosterXamlForProbeAsync(Path.Combine(root,"marquee-preview.png"));
        Send(PosterInputEventKind.GestureCancelled,end);
        if(HasContentGesture || _contentSelection.Ids.Count!=0 || _authoritativePoster.ContentLayoutJson!=original)
            throw new IOException("Marquee cancellation did not restore selection/layout.");
        surface=inputRouter.ReadSurface(inputSession)!;
        blank=surface.Layout.Regions.Single(r=>r.Kind==PosterInputTargetKind.ContentViewport);
        capture=new(Guid.NewGuid(),new(inputSession,surface.Layout.Revision,_bounds,blank));
        Send(PosterInputEventKind.Pressed,start);Send(PosterInputEventKind.GestureCompleted,end);
        if(HasContentGesture || !_contentSelection.ShowHighlight || _contentSelection.Ids.Count==0 || _authoritativePoster.ContentLayoutJson!=original)
            throw new IOException("Marquee completion modified layout or lost selection.");
        surface=inputRouter.ReadSurface(inputSession)!;
        var item=surface.Layout.Regions.First(r=>r.Kind==PosterInputTargetKind.ContentItem);
        start=new(_bounds.X+item.Bounds.X+item.Bounds.Width/2,_bounds.Y+item.Bounds.Y+item.Bounds.Height/2);
        capture=new(Guid.NewGuid(),new(inputSession,surface.Layout.Revision,_bounds,item));
        Send(PosterInputEventKind.Pressed,start);
        // A synthetic capture cannot hand off actual mouse ownership to OLE. Exercise the
        // internal gesture only; native cross-window release belongs to the D4-N group.
        Send(PosterInputEventKind.GesturePreview,start with {X=start.X+8});
        if(_contentDragImage is null || _contentGesture is null)throw new IOException("Native drag sprite was not created inside frame.");
        Send(PosterInputEventKind.GesturePreview,start with {X=start.X+6});
        if(_contentDragImage is null)throw new IOException("Native drag sprite lost on return.");
        Send(PosterInputEventKind.GestureCancelled,start);
        if(HasContentGesture || _contentDragImage is not null || _contentDragImageLease is not null || _authoritativePoster.ContentLayoutJson!=original)
            throw new IOException("Drag cancellation leaked a sprite or changed files/layout.");
        log.Write("info","contract.d5_selection_passed",new{nativeMouse=false,nativeSprite=true,marqueeCancel=true,marqueeCommit=true,nativeEgressVerified=false});
    }
}
