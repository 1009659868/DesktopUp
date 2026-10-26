using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Managed native-window ingress only: published border -> OnInput -> ordinary frame owner.
    // No OS mouse injection and no new resize solver. The isolated fixture is restored formally.
    private object RunContentFrameMinimumForProbe()
    {
        var original = ContentDocument; var native = inputRouter.Capture;
        bool vertical = original.FlowAxis == ContentFlowAxis.Vertical;
        int checks = 0;
        void Require(bool ok,string message) { checks++; if(!ok) throw new InvalidOperationException("frame-minimum: " + message); }
        var observations = new List<object>();
        try
        {
            Require(!IsEditing && !HasContentGesture && _spatialGesture is null && native is null,"Idle fixture required.");
            var view = _contentLayout.Viewport!; var snapshot = _contentLayout.Snapshot!;
            int span = Math.Min(12,(int)Math.Floor(((vertical ? view.WidthDip : view.HeightDip) + snapshot.GapDip) /
                (snapshot.CellDip + snapshot.GapDip)));
            Require(span >= 8,"Fixture cannot exercise an item-derived minimum beyond the shell baseline.");
            var first = original.Placements[0];
            Require(EditContent(new(ContentLayoutEditKind.Resize,first.ItemId) {
                ColumnSpan = vertical ? span : first.ColumnSpan, RowSpan = vertical ? first.RowSpan : span }) is null,"Large-item fixture edit failed.");
            Require(SetEditing(true),"Editing border not published.");
            Require(TryReadContentFrameMinimum(out int minWidth,out int minHeight),"No trusted minimum.");
            var initial = _bounds; var before = ReadBrowseForProbe();
            var sizes = _contentLayout.Snapshot!.Items.ToDictionary(i=>i.ItemId,i=>(i.Bounds.Width,i.Bounds.Height));
            var kind = vertical ? PosterInputTargetKind.ResizeRight : PosterInputTargetKind.ResizeBottom;
            void Send(PosterInputEventKind action,int delta)
            {
                var surface = inputRouter.ReadSurface(inputSession)!;
                var region = surface.Layout.Regions.First(r=>r.Kind==kind);
                OnInput(_inputIsland,new(action,kind,region.Index,initial,vertical?delta:0,vertical?0:delta,surface.Layout.Revision) {Session=inputSession});
                if(action==PosterInputEventKind.GesturePreview)
                { Require(_pendingGestureFrame is not null,"Resize input was not scheduled."); spatial.CancelDragFrame(this); PresentPendingGestureFrame(); }
            }
            foreach(int delta in new[]{-10000,-20,-10000})
            {
                Send(PosterInputEventKind.GesturePreview,delta);
                Require(_spatialGesture is not null,"First outer resize owner lost.");
                int expected = delta == -10000 ? (vertical ? minWidth : minHeight) :
                    Math.Max(vertical ? minWidth : minHeight,(vertical ? initial.Width : initial.Height)-20);
                Require((vertical ? _bounds.Width : _bounds.Height)==expected,"Actual native frame missed the content minimum/reverse sample.");
                Require(_bounds.X==initial.X && _bounds.Y==initial.Y &&
                    (vertical ? _bounds.Height==initial.Height : _bounds.Width==initial.Width),"Resize changed anchored/main geometry.");
                var read = ReadBrowseForProbe();
                Require(read.CanvasMatches && read.InputMatches && read.Clipped,"Actual XAML/HRGN/input disagree.");
                Require(_contentLayout.Snapshot!.SpaceStatus==ContentLayoutSpaceStatus.Normal &&
                    _contentLayout.Snapshot.Items.All(i=>sizes[i.ItemId]==(i.Bounds.Width,i.Bounds.Height)),"Resize scaled or clipped cross-axis items.");
                Require(_stateRevision==before.Revision && _authoritativePoster!.ContentLayoutJson==before.StoredRaw,"Preview persisted state.");
                observations.Add(new{delta,_bounds,minWidth,minHeight,read});
            }
            Send(PosterInputEventKind.GestureCancelled,0);
            Require(_spatialGesture is null && _pendingGestureFrame is null && _bounds==initial,
                "Cancel did not restore the committed physical frame.");
            Require(_stateRevision==before.Revision && _authoritativePoster!.ContentLayoutJson==before.StoredRaw && inputRouter.Capture==native,
                "Cancel saved state or touched native capture.");
            return new{passed=true,checks,observations,nativeMouse=false,minimumFromActualItem=true};
        }
        finally
        {
            if(_spatialGesture is not null) CancelSpatialGesture("frame-probe-cleanup");
            Require(SetEditing(false),"Could not restore daily mode.");
            var now=ContentDocument;
            Require(CommitContentDocument(now,RebaseContentIntent(now,original),_posterEntityVersion,_stateRevision) is null,
                "Could not formally restore content fixture.");
        }
    }
}
