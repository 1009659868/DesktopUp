using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentGesture? _contentGesture;
    private readonly ContentLayoutHistory _contentHistory = new();
    private string? _contentNotice;
    private DispatcherQueueTimer? _contentMotionTimer;
    private IDisposable? _contentMotionLease;
    private PosterInputEventArgs? _contentLatestMotion;
    private bool _contentMotionPending;
    public bool HasContentGesture => _contentGesture is not null;
    internal ContentLayoutEditorView ReadContentEditor() => new(ContentDocument, _posterEntityVersion, _selectedContentItem,
        IsEditing, !_contentReadOnly && !_geometry.IsLocked && !_disposed && !_contentPresentationSuspended, _contentNotice)
        { SelectedSlot = _contentSnapshot?.Items.FirstOrDefault(p => p.ItemId == _selectedContentItem),
          CanUndo = _contentHistory.CanUndo(ContentDocument), CanRedo = _contentHistory.CanRedo(ContentDocument),
          SpaceEpoch = _contentSpaceEpoch, SpaceSession = _contentSurfaceSession };

    private bool HandleContentInput(PosterInputEventArgs args)
    {
        if (args.TargetKind is not (PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport)) return false;
        if (args.EventKind == PosterInputEventKind.Scrolled) { _contentActivation.Reset(); ScrollItems(args.WheelDelta, DesktopUp.Windows.NativeInputKeys.ShiftDown); return true; }
        if (args.EventKind == PosterInputEventKind.Pressed)
        {
            // Any intervening handle/background press ends a card double-click pair, even if
            // that press never crosses the drag threshold or is rejected by another owner.
            if (args.TargetKind != PosterInputTargetKind.ContentItem) _contentActivation.Reset();
            // A stale/forged handle press cannot resize after editing adornments are hidden.
            // Ordinary Item presses keep their direct-drag behavior in both operating modes.
            if (args.TargetKind == PosterInputTargetKind.ContentSpanHandle && !IsEditing) return true;
            if (PosterId is { } targetId && spatial.HasCenterDraft(targetId))
            {
                PublishContentFeedback("控制中心有未完成的修改，请先完成或取消；当前操作未开始。");
                return true;
            }
            if (_disposed || _contentPresentationSuspended || _monitor is null || _inputIsland is null || _transitionLayout is not null || spatial.IsPosterSpatialBusy(PosterId!) ||
                _contentSnapshot is null || args.ContentSnapshotGeneration != _contentSnapshot.ViewportGeneration || args.Capture is null) return true;
            PublishContentFeedback(null);
            _selectedContentItem = args.ContentItemId;
            spatial.NotifyContentEditorChanged(PosterId);
            if (args.TargetKind == PosterInputTargetKind.ContentViewport)
            { _contentActivation.Reset(); RefreshContentSurface(); return true; }
            var doc = ContentDocument;
            var slot = _contentSnapshot.Items.FirstOrDefault(p => p.ItemId == args.ContentItemId);
            var viewport = _contentViewport;
            if (slot is null || viewport is null) return true;
            double x = (args.LatestPoint.X - _bounds.X) / _monitor.ScaleX - ContentLeftDip;
            double y = (args.LatestPoint.Y - _bounds.Y) / _monitor.ScaleY - ContentTopDip;
            var tile = ContentLayoutFlow.ProjectTiles(_contentSnapshot, viewport, _contentBrowseMode)
                .FirstOrDefault(t => t.ItemId == slot.ItemId && t.Clip.Contains(x, y));
            if (tile is null) return true;
            bool canEdit = !_geometry.IsLocked && !_contentReadOnly;
            _contentGesture = new(args.Capture, doc, _contentSnapshot, viewport, tile, slot,
                ContentActivationTargetFor(slot.ItemId), _posterEntityVersion, _stateRevision,
                _bounds, args.TargetKind, _monitor.ScaleX, _monitor.ScaleY, canEdit,
                ContentDragNavigation.Begin(_contentSnapshot, viewport, tile, x, y, _contentBrowseMode), ledger.Acquire("content.gesture", "pointer-preview"));
            args.Handled = true;
            spatial.NotifyContentEditorChanged(PosterId);
            RefreshContentSurface();
            return true;
        }
        if (_contentGesture is not { } gesture || args.Capture != gesture.Capture) return true;
        if (args.EventKind == PosterInputEventKind.GestureCancelled)
        { CancelContentGesture("capture-" + args.EndReason); return true; }
        if (_posterEntityVersion != gesture.EntityVersion || _bounds != gesture.Frame || _monitor is null ||
            _monitor.ScaleX != gesture.ScaleX || _monitor.ScaleY != gesture.ScaleY || _disposed || _contentPresentationSuspended || _transitionLayout is not null)
        { CancelContentGesture("basis-expired"); return true; }
        if (args.EventKind is PosterInputEventKind.GesturePreview or PosterInputEventKind.GestureCompleted)
        {
            _contentActivation.Reset();
            gesture.IsDragging = true;
            if (gesture.CanEdit)
            {
                _contentLatestMotion=args; _contentMotionPending=true;
                if(args.EventKind==PosterInputEventKind.GestureCompleted)
                {
                    FlushContentMotion(false);
                    if(!ReferenceEquals(_contentGesture,gesture)) return true;
                }
                else StartContentMotion();
            }
            else
            {
                PublishContentFeedback(_geometry.IsLocked ? "海报已锁定，请先解除锁定。" : "当前内容只读，原配置已保留。");
            }
        }
        if (args.EventKind is PosterInputEventKind.Invoked or PosterInputEventKind.GestureCompleted)
        {
            StopContentMotion();
            QueueContentEndAnchor(gesture);
            _contentGesture = null; gesture.Lease.Dispose(); _contentDraft = _contentDraftProjection = null;
            if (args.EventKind == PosterInputEventKind.GestureCompleted && gesture.Proposal is { } desired)
            {
                // Derive feedback before the write, from the same accepted proposal. A renderer
                // diagnostic must never turn a completed write into a thrown failure afterwards.
                var feedback = gesture.LastEdit is { } edit && gesture.EditResult is { } editResult
                    ? ContentInteractionFeedbackPolicy.Describe(gesture.Document, gesture.Snapshot,
                        edit, editResult, ContentFeedbackPhase.Committed) : null;
                string? error = CommitContentDocument(gesture.Document, desired, gesture.EntityVersion, gesture.Revision);
                if (error is null && feedback is not null) PublishContentImpact(feedback);
            }
            else
            {
                if (args.EventKind == PosterInputEventKind.Invoked && gesture.Target == PosterInputTargetKind.ContentItem)
                    ClickContent(gesture, args);
                else if (args.EventKind == PosterInputEventKind.GestureCompleted)
                    PublishContentFeedback(_contentNotice?.Contains("在此松手", StringComparison.Ordinal) == true
                        ? "本次位置无效，未保存；请在海报内容区内调整。"
                        : _contentNotice ?? "位置未改变，本次没有保存。", transient: gesture.Proposal is not null);
                RefreshContentSurface();
            }
            QueueContentProposalDrain();
            spatial.NotifyContentEditorChanged(PosterId);
        }
        return true;
    }
    private void CancelContentGesture(string reason)
    {
        _contentActivation.Reset();
        StopContentMotion();
        if (_contentGesture is not { } gesture) { _contentDraft=_contentDraftProjection=null; return; }
        QueueContentEndAnchor(gesture);
        _contentGesture=null; _contentDraft=_contentDraftProjection=null; gesture.Lease.Dispose();
        // Release the existing native owner after clearing RAM; its synchronous cancel cannot recurse.
        _inputIsland?.CancelPointerGesture();
        log.Write("info","content.gesture_cancelled",new { PosterId, reason, itemId=gesture.Slot.ItemId });
        PublishContentFeedback("已取消本次调整，原布局未改。", transient: true);
        if (!_disposed && _itemsGrid is not null && _bounds.IsUsable) RefreshContentSurface();
        QueueContentProposalDrain();
        spatial.NotifyContentEditorChanged(PosterId);
    }
    private void QueueContentEndAnchor(ContentGesture gesture)
    {
        // Preserve the actual grabbed identity after cancel/no-op/commit, not whichever peer
        // happens to be the first visible candidate. Expired frame/scale never reuses old pixels.
        if (gesture.IsDragging && !_disposed && !_contentPresentationSuspended && _posterEntityVersion == gesture.EntityVersion &&
            _bounds == gesture.Frame && _monitor is not null &&
            _monitor.ScaleX == gesture.ScaleX && _monitor.ScaleY == gesture.ScaleY)
            _contentEndAnchor = gesture.Navigation.EndAnchor(gesture.PointerDeltaX, gesture.PointerDeltaY);
    }
    private void StartContentMotion()
    {
        if(_contentMotionTimer is not null || _window is null) return;
        _contentMotionLease=ledger.Acquire("timer.content-motion",PosterId!);
        _contentMotionTimer=_window.DispatcherQueue.CreateTimer();
        _contentMotionTimer.Interval=TimeSpan.FromMilliseconds(16);
        _contentMotionTimer.Tick+=ContentMotionTick; _contentMotionTimer.Start();
    }
    private void ContentMotionTick(DispatcherQueueTimer timer,object args)=>FlushContentMotion(true);
    private void StopContentMotion()
    {
        if(_contentMotionTimer is not null) {_contentMotionTimer.Stop();_contentMotionTimer.Tick-=ContentMotionTick;_contentMotionTimer=null;}
        _contentMotionLease?.Dispose();_contentMotionLease=null;_contentLatestMotion=null;_contentMotionPending=false;
    }
    private void FlushContentMotion(bool edgeScroll)
    {
        if(_contentGesture is not {CanEdit:true} gesture || _contentLatestMotion is not { } motion) return;
        if(_disposed || _contentPresentationSuspended || _monitor is null || _bounds!=gesture.Frame || _posterEntityVersion!=gesture.EntityVersion ||
            _monitor.ScaleX!=gesture.ScaleX || _monitor.ScaleY!=gesture.ScaleY || _transitionLayout is not null)
        {CancelContentGesture("motion-basis-expired");return;}
        bool scrolled=false;
        if(edgeScroll)
        {
            double x=(motion.LatestPoint.X-_bounds.X)/gesture.ScaleX-ContentLeftDip;
            double y=(motion.LatestPoint.Y-_bounds.Y)/gesture.ScaleY-ContentTopDip;
            double Edge(double point,double extent)=>point < -64 || point > extent+64 ? 0 :
                point<24 ? -8 : point>extent-24 ? 8 : 0;
            bool vertical=gesture.Snapshot.FlowAxis==ContentFlowAxis.Vertical;
            try
            {
                var next = gesture.Navigation.Advance(vertical ? Edge(y,gesture.Viewport.HeightDip) : Edge(x,gesture.Viewport.WidthDip));
                scrolled = next.TravelDip != gesture.Navigation.TravelDip;
                gesture.Navigation = next;
                var offsets = next.ProjectOffsets(gesture.Snapshot, gesture.Viewport);
                _contentScrollX=offsets.ScrollXDip; _itemScrollOffsetDip=offsets.ScrollYDip;
            }
            catch (ArgumentException)
            { CancelContentGesture("navigation-budget"); return; }
        }
        if(!_contentMotionPending && !scrolled) return;
        _contentMotionPending=false;
        double dx=motion.DeltaX/gesture.ScaleX, dy=motion.DeltaY/gesture.ScaleY;
        ContentRect ghost;
        try { ghost = gesture.Navigation.Ghost(dx,dy); }
        catch (ArgumentException) { CancelContentGesture("pointer-budget"); return; }
        gesture.PointerDeltaX=dx; gesture.PointerDeltaY=dy;
        gesture.GhostX=ghost.X; gesture.GhostY=ghost.Y;
        double pointerX=(motion.LatestPoint.X-_bounds.X)/gesture.ScaleX-ContentLeftDip;
        double pointerY=(motion.LatestPoint.Y-_bounds.Y)/gesture.ScaleY-ContentTopDip;
        var mapped=ContentDragPolicy.Map(gesture.Document,gesture.Snapshot,gesture.Viewport,gesture.Tile,
            gesture.Target==PosterInputTargetKind.ContentSpanHandle?ContentDragKind.Resize:ContentDragKind.Move,
            dx,dy,gesture.Navigation.ScrollDeltaXDip,gesture.Navigation.ScrollDeltaYDip);
        if(!new ContentRect(0,0,gesture.Viewport.WidthDip,gesture.Viewport.HeightDip).Contains(pointerX,pointerY))
            mapped=new(null,"请移回海报内容区；在此松手不会保存。");
        if (gesture.LastMapping == mapped && !scrolled) { RefreshContentSurface(); return; }
        gesture.LastMapping=mapped;
        gesture.LastEdit=mapped.Edit;
        var result=!mapped.Accepted ? new ContentLayoutEditResult(false,gesture.Document,mapped.Error)
            : mapped.NoChange ? new ContentLayoutEditResult(true,gesture.Document)
            : ContentLayoutEditing.Apply(gesture.Document,mapped.Edit!,gesture.Viewport,gesture.Snapshot);
        _contentNotice=result.Error;
        gesture.EditResult = result;
        ContentLayoutDocument? projected=null;
        if(result.Accepted)
        {
            try
            {
                projected=ProjectContentEdit(result.Document);
                RequireWritableContentProjection(projected,ContentViewportFor(_bounds));
                if (mapped.NoChange) _contentNotice = "位置未改变，松手不保存。";
                else SetContentImpact(ContentInteractionFeedbackPolicy.Describe(gesture.Document, gesture.Snapshot, mapped.Edit!, result));
            }
            catch(ArgumentException error) { projected=null; _contentNotice=error.Message; }
        }
        gesture.Proposal=projected is not null?result.Document:null;
        _contentDraft=gesture.Proposal;
        _contentDraftProjection=projected;
        RefreshContentSurface();
    }
    private void RefreshContentSurface()
    {
        if (_disposed || _contentPresentationSuspended || _monitor is null || !_bounds.IsUsable || _itemsGrid is null) return;
        UpdateItemScrollState(_bounds);
        if (_inputIsland?.ApplyLayout(_bounds,BuildInputLayout(_bounds)) == true) PublishContentSpace(_contentSpaceCandidate);
    }
    internal string? EditContent(ContentLayoutEdit edit, long? centerEntityVersion = null, long? centerDocumentVersion = null)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return ReportContentFailure("内容只读、已锁定或正在交互。");
        var before=ContentDocument;
        bool centerOwner = centerEntityVersion.HasValue && centerDocumentVersion.HasValue &&
            spatial.OwnsCenterContentDraft(before.PosterId, centerEntityVersion.Value, centerDocumentVersion.Value);
        if (centerEntityVersion.HasValue && (!centerOwner || centerEntityVersion != _posterEntityVersion ||
            centerDocumentVersion != before.DocumentVersion)) return ReportContentFailure("海报或内容版本已变化，草稿保留；请取消后重新调整。");
        var presented = _contentSnapshot ?? EnsureContentSnapshot(_bounds);
        var result=ContentLayoutEditing.Apply(before,edit,ContentViewportFor(_bounds),presented);
        if(!result.Accepted) return ReportContentFailure(result.Error ?? "这次调整未能完成，原布局已保留。");
        var feedback = ContentInteractionFeedbackPolicy.Describe(before, presented, edit, result, ContentFeedbackPhase.Committed);
        string? error = CommitContentDocument(before,result.Document,_posterEntityVersion,_stateRevision, centerOwner: centerOwner);
        if (error is null) PublishContentImpact(feedback);
        return error;
    }
    internal ContentInteractionFeedback? PreviewContentEdit(ContentLayoutEdit edit)
    {
        if (_disposed || _contentPresentationSuspended || _contentReadOnly || _geometry.IsLocked ||
            HasContentGesture || _transitionLayout is not null || _contentSnapshot is not { } presented) return null;
        var before = ContentDocument;
        var result = ContentLayoutEditing.Apply(before, edit, ContentViewportFor(_bounds), presented);
        if (result.Accepted)
        {
            try { RequireWritableContentProjection(ProjectContentEdit(result.Document), ContentViewportFor(_bounds)); }
            catch (ArgumentException error) { return ContentInteractionFeedbackPolicy.Rejected(error.Message); }
        }
        // Pure existing editor result: no draft projection, generation, notice, history or DB mutation.
        return ContentInteractionFeedbackPolicy.Describe(before, presented, edit, result);
    }
    // Per-poster switches committed through the same single-revision content command. A conflicting pin
    // is reported instead of being relocated, and the switch never reorders or resizes anything.
    internal string? SetContentFlowAxis(ContentFlowAxis axis)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return ReportContentFailure("内容只读、已锁定或正在交互。");
        var before=ContentDocument;
        var preference=_presentation.Parameters.Preferences ?? new();
        var result=ContentLayoutFlowPolicy.SetFlowAxis(before,axis,preference.Scale,preference.Spacing);
        if(!result.Accepted) return ReportContentFailure(result.Error ?? "浏览方向未改变，原布局已保留。");
        return CommitContentDocument(before,result.Document,_posterEntityVersion,_stateRevision);
    }
    internal string? SetContentLoopScroll(bool enabled)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return ReportContentFailure("内容只读、已锁定或正在交互。");
        var before=ContentDocument;
        var preference=_presentation.Parameters.Preferences ?? new();
        var result=ContentLayoutFlowPolicy.SetLoopScroll(before,enabled,preference.Scale,preference.Spacing);
        if(!result.Accepted) return ReportContentFailure(result.Error ?? "循环设置未改变，原布局已保留。");
        return CommitContentDocument(before,result.Document,_posterEntityVersion,_stateRevision);
    }
    private string? CommitContentDocument(ContentLayoutDocument before,ContentLayoutDocument desired,long entityVersion,long revision,bool recordHistory=true,bool centerOwner=false)
    {
        if (spatial.HasCenterDraft(before.PosterId) && !(centerOwner && spatial.OwnsCenterContentDraft(before.PosterId, entityVersion, before.DocumentVersion)))
            return RefuseContentCommit("控制中心有未完成的修改，请先完成或取消。");
        if (_disposed || _contentPresentationSuspended) return ReportContentFailure("内容呈现已暂停，请等待恢复。");
        try { RequireWritableContentProjection(ProjectContentEdit(desired), ContentViewportFor(_bounds)); }
        catch (ArgumentException error) { return RefuseContentCommit(error.Message); }
        if (ContentLayoutPolicy.Serialize(before)==ContentLayoutPolicy.Serialize(desired))
        { PublishContentFeedback("内容未改变，本次没有保存。", transient: true); RefreshContentSurface(); return null; }
        var result=stateCommands.Execute(new ContentLayoutCommand("content-"+Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
            revision,before.PosterId,StateReadSet.ForPoster(before.PosterId,entityVersion),before.DocumentVersion,before.ItemSetVersion,desired,
            !before.Placements.Select(p=>p.ItemId).ToHashSet(StringComparer.Ordinal).SetEquals(desired.Placements.Select(p=>p.ItemId))));
        _contentDraft=_contentDraftProjection=null;
        if(result.Succeeded && result.Status==StateCommandStatus.Committed)
        {
            if(recordHistory) _contentHistory.Record(before,desired);
            PublishContentFeedback("布局已保存，可撤销。", transient: true);
        }
        else PublishContentFeedback(result.Succeeded ? null : result.Error ?? "布局未保存，已恢复当前状态。");
        if(result.Projection?.PostersById.TryGetValue(before.PosterId,out var current)==true)
        {
            _authoritativePoster=current.Poster; _posterEntityVersion=current.EntityVersion;
            _posterRevision=current.PosterRevision; _stateRevision=result.CurrentRevision;
        }
        _solvedDocument=null; RefreshContentSurface();
        spatial.NotifyContentEditorChanged(PosterId);
        return result.Succeeded ? null : _contentNotice;
    }
    private string RefuseContentCommit(string message)
    {
        PublishContentFeedback(message);
        _contentDraft=_contentDraftProjection=null;
        _solvedDocument=null;
        RefreshContentSurface();
        spatial.NotifyContentEditorChanged(PosterId);
        return message;
    }
    internal string? SelectContentItem(string? id)
    {
        if (_disposed || _contentPresentationSuspended) return "内容呈现已暂停，请等待恢复。";
        if (HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!) || PosterId is { } posterId && spatial.HasCenterDraft(posterId))
            return "请先完成或取消当前修改，再切换项目。";
        if(id is not null && !ContentDocument.Placements.Any(p=>p.ItemId==id)) return "内容已变化，请重新选择。";
        _contentActivation.Reset();
        _selectedContentItem=id; RefreshContentSurface(); spatial.NotifyContentEditorChanged(PosterId); return null;
    }
    internal string? NudgeContent(int x,int y,bool resize)
    {
        if (_disposed || _contentPresentationSuspended) return "内容呈现已暂停，请等待恢复。";
        if(_selectedContentItem is not { } id) return "请先选择卡片。";
        var slot=EnsureContentSnapshot(_bounds).Items.FirstOrDefault(p=>p.ItemId==id);
        var placement=ContentDocument.Placements.FirstOrDefault(p=>p.ItemId==id);
        if(slot is null || placement is null) return "所选卡片已变化。";
        if (resize) return EditContent(new(ContentLayoutEditKind.Resize,id)
            {ColumnSpan=slot.ColumnSpan+x,RowSpan=slot.RowSpan+y});
        if (ContentDocument.Mode != ContentLayoutMode.Manual && !placement.FixedPosition)
        {
            int delta=ContentDocument.FlowAxis==ContentFlowAxis.Vertical ? (y!=0?y:x) : (x!=0?x:y);
            return EditContent(new(ContentLayoutEditKind.Move,id) {TargetReadOrder=slot.ReadOrder+delta});
        }
        return EditContent(new(ContentLayoutEditKind.Move,id)
            {Column=slot.Column-slot.RegionColumn+x,Row=slot.Row-slot.RegionRow+y});
    }
    internal string? UseContentSample(string sample)
    {
        if(_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return "当前不能替换 DEMO 布局。";
        var before=ContentDocument;
        var next=sample switch { "illustration"=>ContentLayoutSamples.Illustration(before.PosterId), "covers"=>ContentLayoutSamples.Covers(before.PosterId),
            "landscape"=>ContentLayoutSamples.Landscape(before.PosterId), _=>ContentLayoutSamples.Equal(before.PosterId) };
        var desired=RebaseContentIntent(before,next);
        return CommitContentDocument(before,desired,_posterEntityVersion,_stateRevision);
    }
    private static ContentLayoutDocument RebaseContentIntent(ContentLayoutDocument current, ContentLayoutDocument intent)
    {
        var old=current.Placements.ToDictionary(p=>p.ItemId,StringComparer.Ordinal);
        bool changedMembership=!old.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(intent.Placements.Select(p=>p.ItemId));
        return ContentLayoutPolicy.Freeze(intent with { PosterId=current.PosterId,DocumentVersion=checked(current.DocumentVersion+1),
            ItemSetVersion=checked(current.ItemSetVersion+(changedMembership?1:0)),
            Placements=intent.Placements.Select(p=>p with { Version=old.TryGetValue(p.ItemId,out var previous)
                ? previous.Version + (p with { Version=previous.Version } != previous ? 1 : 0) : 1 }).ToArray() });
    }
    internal string? UndoContent(bool redo)
    {
        if(_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return ReportContentFailure("当前不能撤销布局。请先结束正在进行的操作或解除锁定。");
        var before=ContentDocument;
        var proposal=redo?_contentHistory.ProposeRedo(before):_contentHistory.ProposeUndo(before);
        if(proposal is null) return ReportContentFailure("没有可用的布局历史，或当前版本已被外部修改。");
        string? error=CommitContentDocument(before,proposal.Document,_posterEntityVersion,_stateRevision,false);
        if(error is null)
        {
            _contentHistory.Confirm(proposal,ContentDocument);
            PublishContentFeedback(redo ? "已重做本次操作。" : "已撤销本次操作，海报大小保持不变。", transient: true);
        }
        else _contentHistory.CancelProposal();
        return error;
    }
    private sealed class ContentGesture(PosterInputCapture capture,ContentLayoutDocument document,ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport, ContentLayoutTile tile, ContentResolvedPlacement slot, ContentActivationTarget? activationTarget,
        long entityVersion,long revision,DesktopPixelRect frame,PosterInputTargetKind target,double scaleX,double scaleY,bool canEdit,
        ContentDragNavigation navigation,IDisposable lease)
    {
        public PosterInputCapture Capture {get;}=capture;
        public ContentLayoutDocument Document {get;}=document;
        public ContentLayoutSnapshot Snapshot {get;}=snapshot;
        public ContentLayoutViewport Viewport {get;}=viewport;
        public ContentLayoutTile Tile {get;}=tile;
        public ContentActivationTarget? ActivationTarget {get;}=activationTarget;
        public ContentResolvedPlacement Slot {get;}=slot;
        public long EntityVersion {get;}=entityVersion;
        public long Revision {get;}=revision;
        public DesktopPixelRect Frame {get;}=frame;
        public PosterInputTargetKind Target {get;}=target;
        public double ScaleX {get;}=scaleX;
        public double ScaleY {get;}=scaleY;
        public bool CanEdit {get;}=canEdit;
        public ContentDragNavigation Navigation {get;set;}=navigation;
        public double PointerDeltaX {get;set;}
        public double PointerDeltaY {get;set;}
        public IDisposable Lease {get;}=lease;
        public ContentLayoutDocument? Proposal {get;set;}
        public ContentLayoutEdit? LastEdit {get;set;}
        public ContentLayoutEditResult? EditResult {get;set;}
        public ContentDragResult? LastMapping {get;set;}
        public bool IsDragging {get;set;}
        public double GhostX {get;set;}=tile.Bounds.X;
        public double GhostY {get;set;}=tile.Bounds.Y;
    }
}

internal sealed record ContentLayoutEditorView(ContentLayoutDocument Document,long EntityVersion,string? SelectedItemId,bool IsEditing,bool CanEdit,string? Notice)
{
    public long SpaceEpoch { get; init; }
    public Guid SpaceSession { get; init; }
    public ContentResolvedPlacement? SelectedSlot { get; init; }
    public bool CanUndo { get; init; }
    public bool CanRedo { get; init; }
}
