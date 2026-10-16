using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private RejectedContentPreviewBasis? _rejectedContentPreviewBasis;
    private sealed record RejectedContentPreviewBasis(string ItemId, long HeatVersion, long Revision,
        long EntityVersion, long DocumentVersion, long OpenIntents, string? StoredRaw, string AuthorJson);

    internal object PrepareRejectedContentPreviewProbe()
    {
        var author=ContentDocument;
        var item=author.Placements.Single();
        var space=author.ReservedSpaces.Single();
        if(_disposed || _contentPresentationSuspended || _contentReadOnly || IsEditing || HasContentGesture ||
            _geometry.IsLocked || _geometry.IsCollapsed || _transitionLayout is not null || _monitor is null ||
            _inputIsland is null || inputRouter.Capture is not null || spatial.HasCenterDraft(author.PosterId) ||
            _contentHeatSession.ActiveVersion is not null || _contentHeatSession.HasPending ||
            author.SchemaVersion!=2 || author.Mode!=ContentLayoutMode.Automatic || !author.AllowHeatDrivenSize || author.LoopScroll ||
            author.FlowAxis!=ContentFlowAxis.Vertical || item is not { FixedPosition:true, ManualSize:false,
                Column:0, Row:8, ColumnSpan:4, RowSpan:4 } ||
            space is not { Kind:ContentReservedKind.Explicit, Column:4, Row:0, ColumnSpan:4, RowSpan:4 })
            throw new InvalidOperationException("Use the isolated, idle rejected-heat fixture with its original hard position and explicit space.");
        _rejectedContentPreviewBasis=new(item.ItemId,1,_stateRevision,_posterEntityVersion,author.DocumentVersion,
            _contentOpenIntentCount,_authoritativePoster?.ContentLayoutJson,ContentLayoutPolicy.Serialize(author));
        if(!OfferContentHeat(new(author.PosterId,author.ItemSetVersion,1,[new(item.ItemId,0,1.5,"rejected-preview-probe")])))
            throw new InvalidOperationException("Production heat admission rejected the legal starting composition.");
        return new { Name="rejected-heat-prepared", Basis=_rejectedContentPreviewBasis, SourceAuthor=item, ReservedSpace=space,
            HeatPending=_contentHeatSession.HasPending, NativeMouse=false, NativeKeyboard=false };
    }

    internal object RunRejectedContentPreviewProbe()
    {
        var basis=_rejectedContentPreviewBasis ?? throw new InvalidOperationException("Prepare the rejected-heat probe on an earlier dispatcher tick.");
        var assertions=new List<string>();
        var inputs=new List<object>();
        var nativeBasis=inputRouter.Capture;
        PosterInputCapture? ownedCapture=null;
        void Require(bool value,string message)
        {
            if(!value) throw new InvalidOperationException("rejected-heat: "+message);
            assertions.Add(message);
        }
        bool Released()=>_contentGesture is null && _contentDraft is null && _contentDraftProjection is null &&
            _contentMotionTimer is null && _contentMotionLease is null && _contentLatestMotion is null && !_contentMotionPending;
        void Unchanged()
        {
            Require(_stateRevision==basis.Revision && _posterEntityVersion==basis.EntityVersion &&
                ContentDocument.DocumentVersion==basis.DocumentVersion && _contentOpenIntentCount==basis.OpenIntents,
                "State/entity/document revisions and open-intent count remain unchanged.");
            Require(_authoritativePoster?.ContentLayoutJson==basis.StoredRaw &&
                ContentLayoutPolicy.Serialize(ContentDocument)==basis.AuthorJson,"Stored and author JSON remain byte-for-byte unchanged.");
        }
        ContentSurfaceProbe VisibleSource()
        {
            Require(!_contentReadOnly && !_contentProjectionFault && _contentSnapshot is { IsLayoutReadOnly:false } snapshot &&
                snapshot.Items.SingleOrDefault() is { Column:0, Row:8, ColumnSpan:6, RowSpan:6 } slot && !slot.Clip.IsEmpty,
                "The last-good 6x6 source remains visible, normal and editable instead of an empty read-only fallback.");
            var surface=ReadContentSurfaceForSmoke();
            Require(surface.Items==1 && surface.HitItems==1 && surface.CanvasMatches && surface.InputMatches && surface.Clipped,
                "Actual production Canvas and published item input still match the complete single-member composition.");
            return surface;
        }
        try
        {
            Require(nativeBasis is null && Released() && !IsEditing && _monitor is not null &&
                _contentHeatSession.ActiveVersion==basis.HeatVersion && !_contentHeatSession.HasPending,
                "The earlier queued heat reached its safe point before the real-region protocol begins.");
            Unchanged();
            var source=VisibleSource();
            var snapshot=_contentSnapshot!;
            var viewport=_contentViewport!;
            var tile=ContentLayoutFlow.ProjectTiles(snapshot,viewport).Single(t=>t.ItemId==basis.ItemId && !t.Clip.IsEmpty);
            Require(tile.Clip==tile.Bounds,"The actual source tile is fully visible and does not depend on synthetic offscreen coordinates.");
            var published=inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No published input surface.");
            var region=published.Layout.Regions.Single(r=>r.Kind==PosterInputTargetKind.ContentItem && r.ContentItemId==basis.ItemId);
            Require(region.ContentSnapshotGeneration==snapshot.ViewportGeneration,"The press uses the currently published source generation.");
            var capture=new PosterInputCapture(Guid.NewGuid(),new(inputSession,published.Layout.Revision,published.Frame,region));
            ownedCapture=capture;
            var point=new PointerScreenPoint(published.Frame.X+region.Bounds.X+region.Bounds.Width/2,
                published.Frame.Y+region.Bounds.Y+region.Bounds.Height/2);
            long sequence=0;
            PosterInputEventArgs Send(PosterInputEventKind kind,PointerScreenPoint latest)
            {
                var current=inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("Published input disappeared.");
                var args=PosterContentInputPolicy.Event(capture,kind,point,latest,current.Layout.Revision,++sequence);
                Require(HandleContentInput(args),"The captured event enters the production content handler.");
                inputs.Add(new { Event=kind.ToString(), args.Sequence, args.Handled, Press=point, Latest=latest,
                    Capture=capture.Id, Revision=_stateRevision, Notice=_contentNotice });
                return args;
            }
            Require(Send(PosterInputEventKind.Pressed,point).Handled && _contentGesture is { CanEdit:true } captured && captured.Capture==capture,
                "The actual hit region acquires the ordinary editable gesture with organize mode off.");
            double pitch=snapshot.CellDip+snapshot.GapDip;
            var destination=new PointerScreenPoint(point.X,point.Y-(int)Math.Round(8*pitch*_monitor!.ScaleY));
            Send(PosterInputEventKind.GesturePreview,destination);
            Require(_contentMotionPending && _contentMotionTimer is not null && _contentMotionLease is not null,
                "Production retains this motion for its real bounded preview flush.");
            // Invoke the same flush used by release, with edge scrolling off; no Proposal is injected.
            FlushContentMotion(false);
            var gesture=_contentGesture ?? throw new InvalidOperationException("The valid capture was unexpectedly cancelled.");
            Require(gesture.LastMapping is { Accepted:true, NoChange:false } &&
                gesture.LastEdit is { Kind:ContentLayoutEditKind.Move, Column:0, Row:0 },
                "The real pointer displacement maps to the intended hard-position move at (0,0).");
            // This is the existing author editor, not a copied solver/oracle. It distinguishes a
            // legal author move from the subsequent effective-heat collision rejected by the handler.
            var authorMove=ContentLayoutEditing.Apply(gesture.Document,gesture.LastEdit!,gesture.Viewport,gesture.Snapshot);
            Require(authorMove.Accepted && authorMove.Document.Placements.Single() is { Column:0, Row:0, ColumnSpan:4, RowSpan:4 },
                "The existing author editor accepts the 4x4 destination while preserving authored size.");
            Require(gesture.Proposal is null && _contentDraft is null && _contentDraftProjection is null &&
                _contentNotice?.Contains("Authored position conflicts with content, reserved space or region:",StringComparison.Ordinal)==true,
                "Effective heat collision reports its reason and publishes no author/effective draft.");
            Unchanged();
            var rejected=VisibleSource();
            Require(rejected.GeometryFingerprint==source.GeometryFingerprint,"Rejected preview keeps the last-good source geometry.");
            inputs.Add(new { Event="production-flush-rejected", gesture.LastEdit, gesture.LastMapping,
                AuthorMoveAccepted=authorMove.Accepted, AuthorCandidate=authorMove.Document, Notice=_contentNotice, Surface=rejected });
            Send(PosterInputEventKind.GestureCompleted,destination);
            Require(Released() && inputRouter.Capture==nativeBasis,"Release frees every owned gesture/motion resource without altering native capture.");
            Unchanged();
            var released=VisibleSource();
            Require(released.GeometryFingerprint==source.GeometryFingerprint,"Release restores the same valid current presentation and input.");
            var result=new { Name="rejected-heat-preview-release", Passed=true, Basis=basis,
                Source=source, RejectedPreview=rejected, Released=released, Assertions=assertions.ToArray(), Inputs=inputs.ToArray(),
                NativeMouse=false, NativeKeyboard=false, AuthoritativeWrites=0, AllOwnedLeasesReleased=Released() };
            log.Write("info","content.rejected_preview_observed",result);
            return result;
        }
        finally
        {
            if(_contentGesture?.Capture==ownedCapture && ownedCapture is not null) CancelContentGesture("rejected-preview-probe-cleanup");
            _rejectedContentPreviewBasis=null;
        }
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal object PrepareRejectedContentPreviewProbe(string id)
    {
        if(_suspended || IsSpatialBusy || HasCenterDraft(id) || !_windows.TryGetValue(id,out var window))
            throw new InvalidOperationException("The isolated rejected-heat poster is unavailable or busy.");
        return window.PrepareRejectedContentPreviewProbe();
    }
    internal object RunRejectedContentPreviewProbe(string id)
    {
        if(_suspended || IsSpatialBusy || HasCenterDraft(id) || !_windows.TryGetValue(id,out var window))
            throw new InvalidOperationException("The isolated rejected-heat poster is unavailable or busy.");
        return window.RunRejectedContentPreviewProbe();
    }
}
