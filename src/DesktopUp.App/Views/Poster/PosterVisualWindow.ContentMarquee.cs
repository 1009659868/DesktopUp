using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private sealed record MarqueeGesture(PosterInputCapture Capture, ContentSelection Before,
        DesktopPixelRect Frame, long EntityVersion, double ScaleX, double ScaleY,
        double X, double Y, ContentLayoutTile[] Tiles, string[] Order, bool Control, bool Shift, IDisposable Lease)
    {
        public ContentRect Rectangle { get; set; }
    }
    private MarqueeGesture? _contentMarquee;

    private bool HandleContentMarquee(PosterInputEventArgs args)
    {
        if (args.EventKind == PosterInputEventKind.Pressed)
        {
            _contentActivation.Reset();
            if (!IsRealContent || HasContentGesture || _disposed || _contentSession.IsSuspended ||
                _monitor is null || _contentLayout.Viewport is null || _contentLayout.Snapshot is null || args.Capture is null ||
                _transitionLayout is not null || spatial.IsPosterSpatialBusy(PosterId!) || spatial.HasCenterDraft(PosterId!) ||
                args.ContentSnapshotGeneration != _contentLayout.Snapshot.ViewportGeneration) return true;
            // Snapshot visible geometry: no scroll/reflow mid-box, no duplicate loop identities.
            _contentMarquee = new(args.Capture, _contentSelection, _bounds, _posterEntityVersion,
                _monitor.ScaleX, _monitor.ScaleY,
                (args.LatestPoint.X-_bounds.X)/_monitor.ScaleX-ContentLeftDip,
                (args.LatestPoint.Y-_bounds.Y)/_monitor.ScaleY-ContentTopDip,
                ContentLayoutFlow.ProjectTiles(_contentLayout.Snapshot,_contentLayout.Viewport,_contentLayout.BrowseMode).ToArray(),
                ContentSelectionOrder(), NativeInputKeys.ControlDown, NativeInputKeys.ShiftDown,
                ledger.Acquire("content.marquee",PosterId!));
            _contentSession.SetInteraction(PosterContentInteraction.Marquee, true);
            args.Handled = true;
            spatial.NotifyContentEditorChanged(PosterId);
            return true;
        }
        if (_contentMarquee is not { } gesture || gesture.Capture != args.Capture) return true;
        if (args.EventKind == PosterInputEventKind.GestureCancelled || _disposed || _contentSession.IsSuspended ||
            _bounds != gesture.Frame || _posterEntityVersion != gesture.EntityVersion || _monitor is null ||
            _monitor.ScaleX != gesture.ScaleX || _monitor.ScaleY != gesture.ScaleY || _transitionLayout is not null)
        { CancelContentMarquee(); return true; }
        if (args.EventKind is PosterInputEventKind.GesturePreview or PosterInputEventKind.GestureCompleted)
        {
            double x = Math.Clamp((args.LatestPoint.X-_bounds.X)/gesture.ScaleX-ContentLeftDip,0,_contentLayout.Viewport!.WidthDip);
            double y = Math.Clamp((args.LatestPoint.Y-_bounds.Y)/gesture.ScaleY-ContentTopDip,0,_contentLayout.Viewport.HeightDip);
            gesture.Rectangle = new(Math.Min(x,gesture.X),Math.Min(y,gesture.Y),Math.Abs(x-gesture.X),Math.Abs(y-gesture.Y));
            var hits = gesture.Tiles.Where(t=>!t.Clip.Intersect(gesture.Rectangle).IsEmpty).Select(t=>t.ItemId);
            _contentSelection = gesture.Before.Marquee(gesture.Order,hits,gesture.Control,gesture.Shift);
        }
        else if (args.EventKind == PosterInputEventKind.Invoked)
            _contentSelection = gesture.Control || gesture.Shift ? gesture.Before : ContentSelection.Empty;
        if (args.EventKind is PosterInputEventKind.GestureCompleted or PosterInputEventKind.Invoked)
        { _contentMarquee=null; _contentSession.SetInteraction(PosterContentInteraction.Marquee, false);gesture.Lease.Dispose();QueueContentProposalDrain(); }
        if (args.EventKind == PosterInputEventKind.Invoked) CanvasBlankClick(args.LatestPoint);
        _selectedContentItem=_contentSelection.Focus;
        RefreshContentSurface();spatial.NotifyContentEditorChanged(PosterId);
        return true;
    }
    private void CancelContentMarquee()
    {
        if (_contentMarquee is not { } gesture) return;
        _contentMarquee=null; _contentSession.SetInteraction(PosterContentInteraction.Marquee, false);gesture.Lease.Dispose();
        _contentSelection=gesture.Before.Prune(ContentSelectionOrder());
        _selectedContentItem=_contentSelection.Focus;
        _inputIsland?.CancelPointerGesture();
        RefreshContentSurface();QueueContentProposalDrain();spatial.NotifyContentEditorChanged(PosterId);
    }
    private void DrawContentMarquee()
    {
        if (_contentMarquee is not { Rectangle.IsEmpty:false } gesture || Presenter.Surface is null) return;
        var r=gesture.Rectangle;
        var box=new Border {Width=r.Width,Height=r.Height,BorderThickness=new(1),
            BorderBrush=Brush(230,100,190,245),Background=Brush(40,100,190,245),IsHitTestVisible=false};
        Canvas.SetLeft(box,r.X);Canvas.SetTop(box,r.Y);Canvas.SetZIndex(box,3);Presenter.Surface.Children.Add(box);
    }
}
