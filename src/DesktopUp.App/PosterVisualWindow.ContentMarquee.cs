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
            if (!IsRealContent || HasContentGesture || _disposed || _contentPresentationSuspended ||
                _monitor is null || _contentViewport is null || _contentSnapshot is null || args.Capture is null ||
                _transitionLayout is not null || spatial.IsPosterSpatialBusy(PosterId!) || spatial.HasCenterDraft(PosterId!) ||
                args.ContentSnapshotGeneration != _contentSnapshot.ViewportGeneration) return true;
            // Snapshot visible geometry: no scroll/reflow mid-box, no duplicate loop identities.
            _contentMarquee = new(args.Capture, _contentSelection, _bounds, _posterEntityVersion,
                _monitor.ScaleX, _monitor.ScaleY,
                (args.LatestPoint.X-_bounds.X)/_monitor.ScaleX-ContentLeftDip,
                (args.LatestPoint.Y-_bounds.Y)/_monitor.ScaleY-ContentTopDip,
                ContentLayoutFlow.ProjectTiles(_contentSnapshot,_contentViewport,_contentBrowseMode).ToArray(),
                ContentSelectionOrder(), NativeInputKeys.ControlDown, NativeInputKeys.ShiftDown,
                ledger.Acquire("content.marquee",PosterId!));
            args.Handled = true;
            spatial.NotifyContentEditorChanged(PosterId);
            return true;
        }
        if (_contentMarquee is not { } gesture || gesture.Capture != args.Capture) return true;
        if (args.EventKind == PosterInputEventKind.GestureCancelled || _disposed || _contentPresentationSuspended ||
            _bounds != gesture.Frame || _posterEntityVersion != gesture.EntityVersion || _monitor is null ||
            _monitor.ScaleX != gesture.ScaleX || _monitor.ScaleY != gesture.ScaleY || _transitionLayout is not null)
        { CancelContentMarquee(); return true; }
        if (args.EventKind is PosterInputEventKind.GesturePreview or PosterInputEventKind.GestureCompleted)
        {
            double x = Math.Clamp((args.LatestPoint.X-_bounds.X)/gesture.ScaleX-ContentLeftDip,0,_contentViewport!.WidthDip);
            double y = Math.Clamp((args.LatestPoint.Y-_bounds.Y)/gesture.ScaleY-ContentTopDip,0,_contentViewport.HeightDip);
            gesture.Rectangle = new(Math.Min(x,gesture.X),Math.Min(y,gesture.Y),Math.Abs(x-gesture.X),Math.Abs(y-gesture.Y));
            var hits = gesture.Tiles.Where(t=>!t.Clip.Intersect(gesture.Rectangle).IsEmpty).Select(t=>t.ItemId);
            _contentSelection = gesture.Before.Marquee(gesture.Order,hits,gesture.Control,gesture.Shift);
        }
        else if (args.EventKind == PosterInputEventKind.Invoked)
            _contentSelection = gesture.Control || gesture.Shift ? gesture.Before : ContentSelection.Empty;
        if (args.EventKind is PosterInputEventKind.GestureCompleted or PosterInputEventKind.Invoked)
        { _contentMarquee=null;gesture.Lease.Dispose();QueueContentProposalDrain(); }
        _selectedContentItem=_contentSelection.Focus;
        RefreshContentSurface();spatial.NotifyContentEditorChanged(PosterId);
        return true;
    }
    private void CancelContentMarquee()
    {
        if (_contentMarquee is not { } gesture) return;
        _contentMarquee=null;gesture.Lease.Dispose();
        _contentSelection=gesture.Before.Prune(ContentSelectionOrder());
        _selectedContentItem=_contentSelection.Focus;
        _inputIsland?.CancelPointerGesture();
        RefreshContentSurface();QueueContentProposalDrain();spatial.NotifyContentEditorChanged(PosterId);
    }
    private void DrawContentMarquee()
    {
        if (_contentMarquee is not { Rectangle.IsEmpty:false } gesture || _contentCanvas is null) return;
        var r=gesture.Rectangle;
        var box=new Border {Width=r.Width,Height=r.Height,BorderThickness=new(1),
            BorderBrush=Brush(230,100,190,245),Background=Brush(40,100,190,245),IsHitTestVisible=false};
        Canvas.SetLeft(box,r.X);Canvas.SetTop(box,r.Y);Canvas.SetZIndex(box,3);_contentCanvas.Children.Add(box);
    }
}
