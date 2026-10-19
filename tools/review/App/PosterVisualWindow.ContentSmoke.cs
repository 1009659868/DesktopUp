using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

// Isolated diagnostic readers. They inspect the already-created production Canvas and
// published input table; they do not solve another layout or allocate a native surface.
internal sealed record ContentSurfaceProbe(string PosterId, long Handle, int Items, int Controls,
    int VisualChildren, int HitItems, bool CanvasMatches, bool InputMatches, bool Clipped,
    bool PartialClip, bool LastItemReachable, double ScrollX, double ScrollY,
    double ExtentWidth, double ExtentHeight, double ViewWidth, double ViewHeight,
    string GeometryFingerprint, string HitFingerprint, bool Transition, bool TransitionStructureReady, bool TransitionValid);
internal sealed record ContentProposalProbe(long? ActiveHeatVersion, bool HeatPending,
    ContentLayoutDocument AuthorIntent, ContentLayoutDocument EffectiveIntent);
internal sealed record ContentThemeSmokeProbe(PosterPresentationParameters Parameters, bool Pending);
internal sealed record ContentSuspensionSmokeProbe(bool InputReady, bool HeatPending, long? ActiveHeatVersion,
    DesktopPixelRect Frame, string GeometryFingerprint);
internal sealed record ContentInputSmokeResult(string Scenario, long BeforeRevision, long AfterRevision,
    int Previews, bool LatestPending, bool MotionReleased, int? ExpectedRow, int? ActualRow,
    bool NativeMouse = false, bool NativeKeyboard = false);
internal sealed record ContentAdornmentProbe(string? SelectedItemId, bool IsEditing,
    bool SelectedCardPresent, double? SelectedBorderThickness, int VisualResizeHandles,
    int PublishedResizeHandles, int PublishedItems, bool FeedbackDiagnosticsEnabled,
    bool FeedbackVisualAllocated, bool FeedbackVisible, bool FooterVisible, ContentSurfaceProbe Surface);

internal sealed partial class PosterVisualWindow
{
    internal ContentAdornmentProbe ReadContentAdornmentsForProbe()
    {
        (_window?.Content as FrameworkElement)?.UpdateLayout();
        var selected = _selectedContentItem is { } id && Presenter.Cards.TryGetValue(id, out var item)
            ? item as Grid : null;
        var card = selected?.Children.OfType<Border>().FirstOrDefault();
        int visualHandles = Presenter.Cards.Values.OfType<Grid>().Sum(cell => cell.Children.OfType<Border>().Count(child =>
            child.Width == 14 && child.Height == 14 && Grid.GetRowSpan(child) == 2 && !child.IsHitTestVisible));
        var regions = inputRouter.ReadSurface(inputSession)?.Layout.Regions
            ?? throw new InvalidOperationException("The actual poster has no published input regions.");
        return new(_selectedContentItem, IsEditing, selected is not null && Presenter.Surface?.Children.Contains(selected) == true,
            card?.BorderThickness.Left, visualHandles, regions.Count(r => r.Kind == PosterInputTargetKind.ContentSpanHandle),
            regions.Count(r => r.Kind == PosterInputTargetKind.ContentItem), TestContentFeedbackEnabled,
            _contentFeedbackCallout is not null || _contentFeedbackText is not null,
            _contentFeedbackCallout?.Visibility == Visibility.Visible, _footer?.Visibility == Visibility.Visible,
            ReadContentSurfaceForSmoke());
    }
    internal ContentProposalProbe ReadContentProposalForSmoke() => new(_contentSession.ActiveHeatVersion,
        _contentSession.HasPendingHeat, ContentDocument, EffectiveContentDocument);
    internal ContentThemeSmokeProbe ReadContentThemeForSmoke() => new(_presentation.Parameters,_contentSession.HasBufferedTheme);
    internal ContentSuspensionSmokeProbe ReadContentSuspensionForSmoke() => new(
        inputRouter.ReadSurface(inputSession) is not null,_contentSession.HasPendingHeat,_contentSession.ActiveHeatVersion,
        _bounds,StateRequestFingerprint.Compute((_contentLayout.Snapshot??throw new InvalidOperationException("No presented content snapshot."))
            .Items.Select(p=>new{p.ItemId,p.Bounds,p.Clip}).ToArray()));
    internal bool RecoverContentForSmoke(DisplayTopologySnapshot topology) =>
        Recover(_desktopHost??throw new InvalidOperationException("No actual desktop host."),topology,999);

    // No SetCapture / hook / mouse synthesis. The actual published region and session are
    // copied into the existing event protocol and fed through the production content handler.
    // One synchronous batch prevents the real dispatcher timer from interleaving the previews.
    internal ContentInputSmokeResult RunContentInputForSmoke(string scenario)
    {
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(scenario+": "+message);}
        Check(scenario is "latest-release" or "reverse-origin" or "cancel-draft" or "edge-release","Unknown synthetic content case.");
        Check(IsEditing && !HasContentGesture && _monitor is not null && _transitionLayout is null,"Content input basis not ready.");
        ScrollContentForSmoke(0,0);
        var id=_contentLayout.Snapshot!.Items.First(p=>!p.Clip.IsEmpty).ItemId;
        Check(SelectContentItem(id) is null,"Unable to select a real visible item.");
        var adornments = ReadContentAdornmentsForProbe();
        Check(adornments.IsEditing && adornments.SelectedBorderThickness == 2 && adornments.VisualResizeHandles == 1 &&
            adornments.PublishedResizeHandles == 1, "Editing selection lacks its actual visible and published resize handle.");
        var surface=inputRouter.ReadSurface(inputSession)!;
        var targetKind=scenario=="edge-release"?PosterInputTargetKind.ContentItem:PosterInputTargetKind.ContentSpanHandle;
        var region=surface.Layout.Regions.Single(p=>p.Kind==targetKind && p.ContentItemId==id);
        Check(region.ContentSnapshotGeneration==_contentLayout.Snapshot!.ViewportGeneration,"Stale source generation.");
        var capture=new PosterInputCapture(Guid.NewGuid(),new(inputSession,surface.Layout.Revision,surface.Frame,region));
        var nativeBasis=inputRouter.Capture;
        var press=new PointerScreenPoint(surface.Frame.X+region.Bounds.X+region.Bounds.Width/2,
            surface.Frame.Y+region.Bounds.Y+region.Bounds.Height/2);
        long sequence=0,before=_stateRevision;
        string raw=ContentLayoutPolicy.Serialize(ContentDocument);
        int previews=0;bool latestPending=false;int? expectedRow=null,actualRow=null;
        PosterInputEventArgs Send(PosterInputEventKind kind,PointerScreenPoint latest,TitleNativeEndReason end=TitleNativeEndReason.None)
        {
            var args=PosterContentInputPolicy.Event(capture,kind,press,latest,
                inputRouter.ReadSurface(inputSession)!.Layout.Revision,++sequence,end);
            Check(HandleContentInput(args),"Event escaped the content branch.");
            if(kind==PosterInputEventKind.GesturePreview)previews++;
            return args;
        }
        try
        {
            var pressed=Send(PosterInputEventKind.Pressed,press);
            Check(pressed.Handled && _contentGesture?.Capture==capture,"Content handler did not acquire its RAM gesture.");
            double pitch=_contentGesture!.Snapshot.CellDip+_contentGesture.Snapshot.GapDip;
            int one=(int)Math.Round(pitch*_monitor!.ScaleX);
            PointerScreenPoint Offset(int x,int y=0)=>new(press.X+x,press.Y+y);
            if(scenario=="edge-release")
            {
                var view=_contentLayout.Viewport!;
                Check(_contentLayout.Snapshot!.ExtentHeightDip>view.HeightDip+32,"Edge scroll fixture needs vertical overflow.");
                var latest=new PointerScreenPoint(press.X,_bounds.Y+(int)Math.Round((ContentTopDip+view.HeightDip-2)*_monitor.ScaleY));
                Send(PosterInputEventKind.GesturePreview,latest);
                latestPending=_contentMotionPending && _contentLatestMotion?.LatestPoint==latest && _contentMotionTimer is not null;
                double scrollBefore=_itemScrollOffsetDip;
                for(int i=0;i<6;i++)FlushContentMotion(true);
                Check(_itemScrollOffsetDip>scrollBefore,"Production edge ticks did not scroll.");
                var gesture=_contentGesture!;
                double dy=(latest.Y-press.Y)/gesture.ScaleY+_itemScrollOffsetDip-gesture.Snapshot.ScrollYDip;
                expectedRow=Math.Clamp(gesture.Slot.Row-gesture.Slot.RegionRow+(int)Math.Round(dy/pitch,MidpointRounding.AwayFromZero),0,ContentLayoutPolicy.MaximumRow);
                Send(PosterInputEventKind.GestureCompleted,latest);
                actualRow=ContentDocument.Placements.Single(p=>p.ItemId==id).Row;
                Check(actualRow==expectedRow,"Release lost the scroll-adjusted grab delta.");
                Check(_stateRevision==before+1,"Edge release must commit exactly once.");
            }
            else
            {
                Send(PosterInputEventKind.GesturePreview,Offset(one*3));
                Send(PosterInputEventKind.GesturePreview,Offset(-one));
                var latest=scenario=="reverse-origin"?press:Offset(one);
                var last=Send(PosterInputEventKind.GesturePreview,latest);
                latestPending=_contentMotionPending && ReferenceEquals(_contentLatestMotion,last) &&
                    _contentMotionTimer is not null && _contentSession.Draft is null && _contentSession.Draft is null;
                Check(latestPending && _stateRevision==before,"Preview batch was flushed early or lost the latest event.");
                if(scenario=="cancel-draft")
                {
                    FlushContentMotion(false);
                    Check(_contentSession.Draft is not null && _contentSession.Draft is not null,"Cancellation did not exercise an actual draft.");
                    Send(PosterInputEventKind.GestureCancelled,latest,TitleNativeEndReason.CancelMode);
                    Check(_stateRevision==before && ContentLayoutPolicy.Serialize(ContentDocument)==raw,"Cancellation persisted the preview.");
                }
                else
                {
                    var original=ContentDocument.Placements.Single(p=>p.ItemId==id);
                    Send(PosterInputEventKind.GestureCompleted,latest);
                    var committed=ContentDocument.Placements.Single(p=>p.ItemId==id);
                    Check(_stateRevision==before+(scenario=="latest-release"?1:0),"Release committed an unexpected number of revisions.");
                    Check(committed.ColumnSpan==original.ColumnSpan+(scenario=="latest-release"?1:0),"Release did not flush the final/latest delta.");
                    if(scenario=="reverse-origin")Check(ContentLayoutPolicy.Serialize(ContentDocument)==raw,"Return-to-origin created an authored change.");
                }
            }
            bool released=_contentMotionTimer is null && _contentMotionLease is null && !_contentMotionPending &&
                _contentLatestMotion is null && _contentGesture is null && _contentSession.Draft is null;
            Check(released,"Content timer/gesture/draft outlived completion or cancellation.");
            Check(inputRouter.Capture==nativeBasis,"Synthetic content gate changed the native router owner.");
            return new(scenario,before,_stateRevision,previews,latestPending,released,expectedRow,actualRow);
        }
        finally{if(HasContentGesture)CancelContentGesture("synthetic-gate-cleanup");}
    }
    internal ContentSurfaceProbe ReadContentSurfaceForSmoke()
    {
        var snapshot = _contentLayout.Snapshot ?? throw new InvalidOperationException("Content snapshot is not ready.");
        var viewport = _contentLayout.Viewport ?? throw new InvalidOperationException("Content viewport is not ready.");
        var published = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("Input surface is not published.");
        var hits = published.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem).ToArray();
        bool canvasMatches = Presenter.Surface is not null && Presenter.Cards.All(pair =>
        {
            var slot = snapshot.Items.Single(p => p.ItemId == pair.Key);
            return Presenter.Surface.Children.Contains(pair.Value) &&
                Math.Abs(Canvas.GetLeft(pair.Value) - (slot.Bounds.X - snapshot.ScrollXDip)) < .01 &&
                Math.Abs(Canvas.GetTop(pair.Value) - (slot.Bounds.Y - snapshot.ScrollYDip)) < .01 &&
                Math.Abs(pair.Value.Width - slot.Bounds.Width) < .01 && Math.Abs(pair.Value.Height - slot.Bounds.Height) < .01;
        });
        canvasMatches &= snapshot.Items.Where(p => !p.Clip.IsEmpty).All(p => Presenter.Cards.ContainsKey(p.ItemId));
        bool inputMatches = _monitor is not null && hits.All(hit =>
        {
            var slot = snapshot.Items.FirstOrDefault(p => p.ItemId == hit.ContentItemId);
            if (slot is null || slot.Clip.IsEmpty || hit.ContentSnapshotGeneration != snapshot.ViewportGeneration) return false;
            int x = (int)Math.Round((ContentLeftDip + slot.Clip.X) * _monitor!.ScaleX);
            int y = (int)Math.Round((ContentTopDip + slot.Clip.Y) * _monitor.ScaleY);
            int right = (int)Math.Round((ContentLeftDip + slot.Clip.Right) * _monitor.ScaleX);
            int bottom = (int)Math.Round((ContentTopDip + slot.Clip.Bottom) * _monitor.ScaleY);
            return hit.Bounds == new DesktopPixelRect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
        });
        if (!_geometry.IsCollapsed && _transitionLayout is null)
            inputMatches &= hits.Length == snapshot.Items.Count(p => !p.Clip.IsEmpty);
        var clip = (Presenter.Surface?.Clip as RectangleGeometry)?.Rect;
        bool clipped = clip.HasValue && Math.Abs(clip.Value.Width - viewport.WidthDip) < .01 &&
            Math.Abs(clip.Value.Height - viewport.HeightDip) < .01;
        if (_transitionLayout is { } transition && _monitor is not null && _surface is not null)
        {
            // R4 deliberately keeps the CONTENT canvas at expanded capacity. The outer
            // lease owns the current visible clip; shrinking the content canvas would
            // defeat the pre-layout guarantee this gate is meant to preserve.
            var outer = (transition.Canvas.Clip as RectangleGeometry)?.Rect;
            var body = (transition.Structure.Clip as RectangleGeometry)?.Rect;
            double width = _bounds.Width / _monitor.ScaleX - _surface.BorderThickness.Left - _surface.BorderThickness.Right;
            double height = _bounds.Height / _monitor.ScaleY - _surface.BorderThickness.Top - _surface.BorderThickness.Bottom;
            clipped = clip.HasValue && outer.HasValue && body.HasValue &&
                Math.Abs(outer.Value.Width-width)<.01 && Math.Abs(outer.Value.Height-height)<.01 &&
                body.Value.Height>=0 && body.Value.Height<=height+.01;
        }
        var last = snapshot.Items.OrderBy(p => p.ReadOrder).LastOrDefault();
        return new(PosterId!, _windowHandle.ToInt64(), snapshot.Items.Count, Presenter.Cards.Count,
            Presenter.Surface?.Children.Count ?? 0, hits.Length, canvasMatches, inputMatches, clipped,
            snapshot.Items.Any(p => !p.Clip.IsEmpty && (p.Clip.Width < p.Bounds.Width - .01 || p.Clip.Height < p.Bounds.Height - .01)),
            last is not null && !last.Clip.IsEmpty, snapshot.ScrollXDip, snapshot.ScrollYDip,
            snapshot.ExtentWidthDip, snapshot.ExtentHeightDip, viewport.WidthDip, viewport.HeightDip,
            StateRequestFingerprint.Compute(snapshot.Items.Select(p => new { p.ItemId, p.Bounds, p.Clip }).ToArray()),
            StateRequestFingerprint.Compute(hits.Select(p => new { p.ContentItemId, p.Bounds }).ToArray()),
            _transitionLayout is not null, _transitionLayout is { } lease && lease.RequestId==_contentSession.ActiveCommand &&
                Math.Abs(lease.Structure.ActualHeight-lease.StableHeight)<.5 &&
                Math.Abs(lease.Structure.ActualWidth-lease.StableWidth)<.5,
            VerifyTransitionLayout(_transitionLayout is not null));
    }

    // Deliberately non-native scroll driver: same fields and refresh used by ScrollItems.
    // Shift-key sensing and real wheel routing still require manual validation.
    internal void ScrollContentForSmoke(double x, double y)
    {
        if (_transitionLayout is not null || HasContentGesture || _geometry.IsCollapsed)
            throw new InvalidOperationException("Cannot probe scroll during a production gesture/transition.");
        var snapshot = _contentLayout.Snapshot ?? throw new InvalidOperationException("No scroll basis.");
        var viewport = _contentLayout.Viewport ?? throw new InvalidOperationException("No scroll viewport.");
        _contentScrollX = Math.Clamp(x, 0, Math.Max(0, snapshot.ExtentWidthDip - viewport.WidthDip));
        _itemScrollOffsetDip = Math.Clamp(y, 0, Math.Max(0, snapshot.ExtentHeightDip - viewport.HeightDip));
        RefreshContentSurface();
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal ContentAdornmentProbe ReadContentAdornmentsForProbe(string id) => _windows[id].ReadContentAdornmentsForProbe();
    internal ContentSurfaceProbe ReadContentSurfaceForSmoke(string id) => _windows[id].ReadContentSurfaceForSmoke();
    internal void ScrollContentForSmoke(string id, double x, double y) => _windows[id].ScrollContentForSmoke(x, y);
    internal ContentProposalProbe ReadContentProposalForSmoke(string id) => _windows[id].ReadContentProposalForSmoke();
    internal bool OfferContentHeatForSmoke(string id, ContentHeatProposal proposal) => _windows[id].OfferContentHeat(proposal);
    internal ContentThemeSmokeProbe ReadContentThemeForSmoke(string id) => _windows[id].ReadContentThemeForSmoke();
    internal bool OfferContentThemeForSmoke(string id,ContentThemeProposal proposal) => _windows[id].OfferContentTheme(proposal);
    internal ContentSuspensionSmokeProbe ReadContentSuspensionForSmoke(string id) => _windows[id].ReadContentSuspensionForSmoke();
    internal void SuspendContentInputForSmoke(string id) => _windows[id].SuspendInput();
    internal bool RecoverContentForSmoke(string id) => _windows[id].RecoverContentForSmoke(
        _effectiveTopology??throw new InvalidOperationException("No actual display topology."));
    internal ContentInputSmokeResult RunContentInputForSmoke(string id,string scenario) => _windows[id].RunContentInputForSmoke(scenario);
}
