using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Isolated managed ingress, NOT OS mouse certification. Uses actual published tile regions,
    // the production press/flush/end path and live XAML/HRGN layout readback.
    private object RunLoopDragForProbe()
    {
        var original = ContentDocument;
        var nativeCapture = inputRouter.Capture;
        var observations = new List<object>();
        int checks = 0;
        string stage = "loop-preconditions";
        DirectContentProtocolPress? owned = null;
        void Require(bool ok, string message)
        { checks++; if (!ok) throw new InvalidOperationException(stage + ": " + message); }
        ContentBrowseProbeRead Read()
        {
            var read = ReadBrowseForProbe();
            Require(read.CanvasMatches && read.InputMatches && read.Clipped, "Live tiles/XAML/published regions differ.");
            Require(inputRouter.Capture == nativeCapture, "Managed probe changed native capture.");
            return read;
        }
        void Send(DirectContentProtocolPress press, PosterInputEventKind kind, PointerScreenPoint point,
            TitleNativeEndReason reason = TitleNativeEndReason.None)
        {
            var surface = inputRouter.ReadSurface(inputSession)!;
            var args = PosterContentInputPolicy.Event(press.Capture, kind, press.Point, point,
                surface.Layout.Revision, ++press.Sequence, reason);
            Require(HandleContentInput(args), "Event did not enter the production content handler.");
        }
        DirectContentProtocolPress Begin(bool resize = false)
        {
            if (resize) Require(SetEditing(true), "Cannot publish the existing span-handle entry.");
            ScrollContentForSmoke(0, 0); ScrollItems(120);
            var read = Read();
            var tile = read.Tiles.First(t => t.LoopIndex == 1 && t.Clip.Width > 20 && t.Clip.Height > 20);
            if (resize)
            {
                Require(SelectContentItem(tile.ItemId) is null, "Cannot select the seam copy for its span handle.");
            }
            var surface = inputRouter.ReadSurface(inputSession)!;
            var region = surface.Layout.Regions.Single(r =>
                r.Kind == (resize ? PosterInputTargetKind.ContentSpanHandle : PosterInputTargetKind.ContentItem) &&
                r.ContentItemId == tile.ItemId && (resize
                    ? r.Bounds.X >= tile.ExpectedInput.X && r.Bounds.Y >= tile.ExpectedInput.Y &&
                      r.Bounds.Right <= tile.ExpectedInput.Right && r.Bounds.Bottom <= tile.ExpectedInput.Bottom
                    : r.Bounds == tile.ExpectedInput));
            var point = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + Math.Min(10, region.Bounds.Width / 2),
                surface.Frame.Y + region.Bounds.Y + Math.Min(10, region.Bounds.Height / 2));
            var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
            var press = new DirectContentProtocolPress(capture, point); owned = press;
            var before = Read();
            Send(press, PosterInputEventKind.Pressed, point);
            Require(_contentGesture?.Capture == capture && _contentGesture.CanEdit && _contentGesture.Tile.LoopIndex == 1,
                "First seam press did not acquire the actual visible copy.");
            var after = Read();
            Require(before.ScrollX == after.ScrollX && before.ScrollY == after.ScrollY && before.StoredRaw == after.StoredRaw,
                "Press moved the browsing phase or persisted content.");
            return press;
        }
        void Preview(DirectContentProtocolPress press, PointerScreenPoint point, bool edge = false)
        {
            Send(press, PosterInputEventKind.GesturePreview, point);
            FlushContentMotion(edge);
            Require(_contentGesture?.Capture == press.Capture, "Motion lost its first owner.");
            var gesture = _contentGesture!;
            Require(Math.Abs(gesture.GhostX - (gesture.Tile.Bounds.X + (point.X - press.Point.X) / gesture.ScaleX)) < 1e-7 &&
                Math.Abs(gesture.GhostY - (gesture.Tile.Bounds.Y + (point.Y - press.Point.Y) / gesture.ScaleY)) < 1e-7,
                "Ghost lost the press internal grab point.");
            Read();
        }
        void Released(ContentBrowseAnchor expectedAnchor)
        {
            Require(!HasContentGesture && _contentMotionTimer is null && _contentMotionLease is null &&
                _contentSession.Draft is null && _contentSession.DraftProjection is null, "Gesture resources were not released.");
            var read = Read();
            var expected = ContentLayoutFlow.RestoreAnchor(_contentLayout.Snapshot!, _contentLayout.Viewport!, expectedAnchor);
            Require(Math.Abs(read.ScrollX - expected.ScrollXDip) < 1e-6 && Math.Abs(read.ScrollY - expected.ScrollYDip) < 1e-6,
                "End did not restore the grabbed-item anchor.");
        }
        PointerScreenPoint Point(double x, double y) => new(
            _bounds.X + (int)Math.Round((ContentLeftDip + x) * _monitor!.ScaleX),
            _bounds.Y + (int)Math.Round((ContentTopDip + y) * _monitor!.ScaleY));
        try
        {
            Require(original.LoopScroll && !IsEditing && !HasContentGesture && nativeCapture is null,
                "Use an idle daily-mode looping fixture.");
            foreach (var mode in new[] { ContentLayoutMode.Automatic, ContentLayoutMode.Manual })
            {
                stage = mode + "-mode";
                Require(EditContent(new(ContentLayoutEditKind.Mode) { Mode = mode }) is null, "Mode command failed.");
                stage = mode + "-navigation-cancel";
                var press = Begin(); var before = Read(); var gesture = _contentGesture!;
                bool vertical = gesture.Snapshot.FlowAxis == ContentFlowAxis.Vertical;
                double x = gesture.Navigation.PressAnchor.ViewportXDip, y = gesture.Navigation.PressAnchor.ViewportYDip;
                var forward = Point(vertical ? x : gesture.Viewport.WidthDip - 10, vertical ? gesture.Viewport.HeightDip - 10 : y);
                Preview(press, forward, true);
                for (int i = 1; i < 12; i++) { FlushContentMotion(true); Read(); }
                Require(gesture.Navigation.TravelDip == 96, "Forward edge motion did not accumulate across the seam.");
                var backward = Point(vertical ? x : 10, vertical ? 10 : y);
                Preview(press, backward, true);
                for (int i = 1; i < 12; i++) { FlushContentMotion(true); Read(); }
                Require(gesture.Navigation.TravelDip == 0, "Reverse motion failed to return unwrapped navigation to zero.");
                var anchor = gesture.Navigation.EndAnchor(gesture.PointerDeltaX, gesture.PointerDeltaY);
                Send(press, PosterInputEventKind.GestureCancelled, backward);
                Released(anchor);
                Require(_stateRevision == before.Revision && _authoritativePoster?.ContentLayoutJson == before.StoredRaw,
                    "Cancellation persisted a preview.");
                observations.Add(new { stage, before, after = Read(), travel = gesture.Navigation.TravelDip });

                stage = mode + "-commit";
                press = Begin(); before = Read(); gesture = _contentGesture!;
                PointerScreenPoint? destination = null;
                // Find a legal, changing target among actual visible cells. No proposal is injected.
                double pitch = gesture.Snapshot.CellDip + gesture.Snapshot.GapDip;
                for (double yy = 10; yy < gesture.Viewport.HeightDip && destination is null; yy += pitch)
                    for (double xx = 10; xx < gesture.Viewport.WidthDip && destination is null; xx += pitch)
                    {
                        var point = Point(xx, yy);
                        Preview(press, point);
                        if (_contentSession.Draft is { } proposal && !ContentLayoutEditing.SameState(gesture.Document, proposal)) destination = point;
                    }
                Require(destination.HasValue, "Fixture had no legal changed drop.");
                anchor = gesture.Navigation.EndAnchor(gesture.PointerDeltaX, gesture.PointerDeltaY);
                var intended = _contentSession.Draft!;
                Send(press, PosterInputEventKind.GestureCompleted, destination!.Value);
                Released(anchor);
                var afterCommit = Read();
                Require(afterCommit.Revision == before.Revision + 1 && afterCommit.DocumentVersion == before.DocumentVersion + 1,
                    "Drag did not use one committed revision/document version.");
                Require(ContentLayoutEditing.SameState(ContentDocument, intended) && ContentDocument.LoopScroll &&
                    afterCommit.Members == before.Members, "Saved layout differs or duplicated a member/changed loop preference.");
                observations.Add(new { stage, before, after = afterCommit });
                Require(UndoContent(false) is null, "Committed drag could not be undone.");

                stage = mode + "-no-op-invalid-release";
                press = Begin(); before = Read(); gesture = _contentGesture!;
                Preview(press, press.Point);
                anchor = gesture.Navigation.EndAnchor(0, 0);
                Send(press, PosterInputEventKind.GestureCompleted, press.Point); Released(anchor);
                Require(_stateRevision == before.Revision && _authoritativePoster?.ContentLayoutJson == before.StoredRaw,
                    "Zero motion authored a layout change.");
                press = Begin(); before = Read(); gesture = _contentGesture!;
                var outside = Point(-20, -20); Preview(press, outside);
                anchor = gesture.Navigation.EndAnchor(gesture.PointerDeltaX, gesture.PointerDeltaY);
                Send(press, PosterInputEventKind.GestureCompleted, outside); Released(anchor);
                Require(_stateRevision == before.Revision && _authoritativePoster?.ContentLayoutJson == before.StoredRaw,
                    "Invalid release persisted content.");
                observations.Add(new { stage, before, after = Read() });

                stage = mode + "-seam-resize-cancel-commit-history";
                // Same actual loop copy and production handle path. Rapid reverse samples are
                // evaluated against the frozen press; they never accumulate rounded deltas.
                foreach (bool commit in new[] { false, true })
                {
                    press = Begin(true); before = Read(); gesture = _contentGesture!;
                    pitch = gesture.Snapshot.CellDip + gesture.Snapshot.GapDip;
                    destination = null;
                    foreach (var delta in new[] { (X: -1, Y: 0), (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1) })
                    {
                        var point = new PointerScreenPoint(press.Point.X + (int)Math.Round(delta.X * pitch * gesture.ScaleX),
                            press.Point.Y + (int)Math.Round(delta.Y * pitch * gesture.ScaleY));
                        Preview(press, point);
                        if (_contentSession.Draft is { } candidate && !ContentLayoutEditing.SameState(gesture.Document, candidate))
                        { destination = point; break; }
                    }
                    Require(destination.HasValue, "No legal changing resize for the actual seam handle.");
                    intended = _contentSession.Draft!;
                    Preview(press, press.Point);
                    Preview(press, destination!.Value);
                    Require(ContentLayoutEditing.SameState(intended, _contentSession.Draft!), "Reverse samples drifted the span proposal.");
                    anchor = gesture.Navigation.EndAnchor(gesture.PointerDeltaX, gesture.PointerDeltaY);
                    Send(press, commit ? PosterInputEventKind.GestureCompleted : PosterInputEventKind.GestureCancelled, destination.Value);
                    Released(anchor);
                    var result = Read();
                    Require(result.Revision == before.Revision + (commit ? 1 : 0) &&
                        result.DocumentVersion == before.DocumentVersion + (commit ? 1 : 0) && result.Members == before.Members &&
                        result.LoopPreference, "Resize changed membership/loop or used multiple commits.");
                    Require(commit ? ContentLayoutEditing.SameState(ContentDocument, intended) :
                        _authoritativePoster?.ContentLayoutJson == before.StoredRaw, "Resize result differed from accepted intent.");
                    if (commit)
                    {
                        Require(UndoContent(false) is null && ContentLayoutEditing.SameState(ContentDocument, gesture.Document, true) &&
                            _stateRevision == result.Revision + 1,
                            "Resize undo failed to restore the original author composition.");
                        Require(UndoContent(true) is null && ContentLayoutEditing.SameState(ContentDocument, intended, true) &&
                            _stateRevision == result.Revision + 2, "Resize redo drifted.");
                        Require(UndoContent(false) is null, "Cannot return to the initial composition.");
                    }
                    observations.Add(new { stage, commit, before, after = result });
                    Require(SetEditing(false), "Cannot return from span editing.");
                }
            }
            stage = "restore-fixture";
            var now = ContentDocument;
            Require(CommitContentDocument(now, RebaseContentIntent(now, original), _posterEntityVersion, _stateRevision) is null,
                "Could not restore the isolated fixture through the formal command.");
            return new { passed = true, checks, observations, nativeMouse = false };
        }
        finally
        {
            if (owned is not null && _contentGesture?.Capture == owned.Capture) CancelContentGesture("loop-probe-cleanup");
            if (IsEditing) SetEditing(false);
        }
    }
}
