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
    internal void CancelContentEditingFromCenter() => CancelContentGesture("center-selection-or-close");
    internal ContentLayoutEditorView ReadContentEditor() => new(ContentDocument, _posterEntityVersion, _selectedContentItem,
        IsEditing, !_contentReadOnly && !_geometry.IsLocked && !_disposed && !_contentPresentationSuspended, _contentNotice);

    private bool HandleContentInput(PosterInputEventArgs args)
    {
        if (args.TargetKind is not (PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport)) return false;
        if (args.EventKind == PosterInputEventKind.Scrolled) { ScrollItems(args.WheelDelta); return true; }
        if (args.EventKind == PosterInputEventKind.Pressed)
        {
            args.Handled = true;
            if (_disposed || _contentPresentationSuspended || _monitor is null || _inputIsland is null || _transitionLayout is not null || spatial.IsSpatialBusy ||
                _contentSnapshot is null || args.ContentSnapshotGeneration != _contentSnapshot.ViewportGeneration || args.Capture is null) return true;
            CancelContentGesture("new-press");
            _selectedContentItem = args.ContentItemId;
            spatial.NotifyContentEditorChanged(PosterId);
            if (args.TargetKind == PosterInputTargetKind.ContentViewport) { RefreshContentSurface(); return true; }
            var doc = ContentDocument;
            var slot = _contentSnapshot.Items.FirstOrDefault(p => p.ItemId == args.ContentItemId);
            if (slot is null) return true;
            bool canEdit = IsEditing && !_geometry.IsLocked && !_contentReadOnly &&
                (args.TargetKind == PosterInputTargetKind.ContentSpanHandle || doc.Mode == ContentLayoutMode.Manual || doc.Placements[slot.SourceIndex].FixedPosition);
            _contentGesture = new(args.Capture, doc, _contentSnapshot, slot, _posterEntityVersion, _stateRevision,
                _bounds, args.TargetKind, _monitor.ScaleX, _monitor.ScaleY, canEdit, ledger.Acquire("content.gesture", "pointer-preview"));
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
            else if (IsEditing)
            {
                _contentNotice = "自动排版不会因拖动改变：请在“…”中进入手动排版，或固定所选卡片。";
                if (_modeLabel is not null) _modeLabel.Text="先进入手动排版或固定卡片";
            }
        }
        if (args.EventKind is PosterInputEventKind.Invoked or PosterInputEventKind.GestureCompleted)
        {
            StopContentMotion();
            _contentGesture = null; gesture.Lease.Dispose(); _contentDraft = null;
            if (args.EventKind == PosterInputEventKind.GestureCompleted && gesture.Proposal is { } desired)
                CommitContentDocument(gesture.Document, desired, gesture.EntityVersion, gesture.Revision);
            else
            {
                if (!IsEditing) log.Write("info", "poster.test_item_invoked", new { itemId=args.ContentItemId, demo=true });
                RefreshContentSurface();
            }
            QueueContentProposalDrain();
        }
        return true;
    }
    private void CancelContentGesture(string reason)
    {
        StopContentMotion();
        if (_contentGesture is not { } gesture) { _contentDraft=null; return; }
        _contentGesture=null; _contentDraft=null; gesture.Lease.Dispose();
        // Release the existing native owner after clearing RAM; its synchronous cancel cannot recurse.
        _inputIsland?.CancelPointerGesture();
        log.Write("info","content.gesture_cancelled",new { PosterId, reason, itemId=gesture.Slot.ItemId });
        if (!_disposed && _itemsGrid is not null && _bounds.IsUsable) RefreshContentSurface();
        QueueContentProposalDrain();
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
        if(edgeScroll && _contentSnapshot is { } current && _contentViewport is { } view)
        {
            double x=(motion.LatestPoint.X-_bounds.X)/gesture.ScaleX-ContentLeftDip;
            double y=(motion.LatestPoint.Y-_bounds.Y)/gesture.ScaleY-ContentTopDip;
            double Edge(double point,double extent)=>point < -64 || point > extent+64 ? 0 :
                point<24 ? -8 : point>extent-24 ? 8 : 0;
            double nextX=Math.Clamp(_contentScrollX+Edge(x,view.WidthDip),0,Math.Max(0,current.ExtentWidthDip-view.WidthDip));
            double nextY=Math.Clamp(_itemScrollOffsetDip+Edge(y,view.HeightDip),0,Math.Max(0,current.ExtentHeightDip-view.HeightDip));
            scrolled=nextX!=_contentScrollX || nextY!=_itemScrollOffsetDip;
            _contentScrollX=nextX;_itemScrollOffsetDip=nextY;
        }
        if(!_contentMotionPending && !scrolled) return;
        _contentMotionPending=false;
        double dx=motion.DeltaX/gesture.ScaleX+_contentScrollX-gesture.Snapshot.ScrollXDip;
        double dy=motion.DeltaY/gesture.ScaleY+_itemScrollOffsetDip-gesture.Snapshot.ScrollYDip;
        double pitch=gesture.Snapshot.CellDip+gesture.Snapshot.GapDip;
        int stepX=(int)Math.Round(dx/pitch,MidpointRounding.AwayFromZero),stepY=(int)Math.Round(dy/pitch,MidpointRounding.AwayFromZero);
        var original=gesture.Document.Placements[gesture.Slot.SourceIndex];
        var edit=gesture.Target==PosterInputTargetKind.ContentSpanHandle
            ? new ContentLayoutEdit(ContentLayoutEditKind.Resize,original.ItemId)
              {ColumnSpan=Math.Clamp(original.ColumnSpan+stepX,1,ContentLayoutPolicy.MaximumSpan),RowSpan=Math.Clamp(original.RowSpan+stepY,1,ContentLayoutPolicy.MaximumSpan)}
            : new ContentLayoutEdit(ContentLayoutEditKind.Move,original.ItemId)
              {Column=Math.Clamp(gesture.Slot.Column-gesture.Slot.RegionColumn+stepX,0,ContentLayoutPolicy.MaximumColumns-1),
               Row=Math.Clamp(gesture.Slot.Row-gesture.Slot.RegionRow+stepY,0,ContentLayoutPolicy.MaximumRow)};
        if(gesture.LastEdit==edit && !scrolled) return;
        gesture.LastEdit=edit;
        var result=ContentLayoutEditing.Apply(gesture.Document,edit,ContentViewportFor(_bounds));
        gesture.Proposal=result.Accepted?result.Document:null;
        _contentDraft=result.Accepted?result.Document:gesture.Document;
        _contentNotice=result.Accepted?null:result.Error;
        RefreshContentSurface();
    }
    private void RefreshContentSurface()
    {
        if (_disposed || _contentPresentationSuspended || _monitor is null || !_bounds.IsUsable || _itemsGrid is null) return;
        UpdateItemScrollState(_bounds);
        if (_inputIsland is not null) _inputIsland.ApplyLayout(_bounds,BuildInputLayout(_bounds));
    }
    internal string? EditContent(ContentLayoutEdit edit)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return "内容只读、已锁定或正在交互。";
        var before=ContentDocument;
        var basis=edit.Kind==ContentLayoutEditKind.Mode && edit.Mode==ContentLayoutMode.Manual ? EffectiveContentDocument : before;
        var result=ContentLayoutEditing.Apply(basis,edit,ContentViewportFor(_bounds));
        if(!result.Accepted) return _contentNotice=result.Error;
        var desired=ReferenceEquals(basis,before)?result.Document:RebaseContentIntent(before,result.Document);
        return CommitContentDocument(before,desired,_posterEntityVersion,_stateRevision);
    }
    // Per-poster switches committed through the same single-revision content command. A conflicting pin
    // is reported instead of being relocated, and the switch never reorders or resizes anything.
    internal string? SetContentFlowAxis(ContentFlowAxis axis)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return "内容只读、已锁定或正在交互。";
        var before=ContentDocument;
        var preference=_presentation.Parameters.Preferences ?? new();
        var result=ContentLayoutFlowPolicy.SetFlowAxis(before,axis,preference.Scale,preference.Spacing);
        if(!result.Accepted) return _contentNotice=result.Error;
        return CommitContentDocument(before,result.Document,_posterEntityVersion,_stateRevision);
    }
    internal string? SetContentLoopScroll(bool enabled)
    {
        if (_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return "内容只读、已锁定或正在交互。";
        var before=ContentDocument;
        var preference=_presentation.Parameters.Preferences ?? new();
        var result=ContentLayoutFlowPolicy.SetLoopScroll(before,enabled,preference.Scale,preference.Spacing);
        if(!result.Accepted) return _contentNotice=result.Error;
        return CommitContentDocument(before,result.Document,_posterEntityVersion,_stateRevision);
    }
    private string? CommitContentDocument(ContentLayoutDocument before,ContentLayoutDocument desired,long entityVersion,long revision,bool recordHistory=true)
    {
        if (_disposed || _contentPresentationSuspended) return "内容呈现已暂停，请等待恢复。";
        if (ContentLayoutPolicy.Serialize(before)==ContentLayoutPolicy.Serialize(desired)) { RefreshContentSurface(); return null; }
        var result=stateCommands.Execute(new ContentLayoutCommand("content-"+Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
            revision,before.PosterId,StateReadSet.ForPoster(before.PosterId,entityVersion),before.DocumentVersion,before.ItemSetVersion,desired,
            !before.Placements.Select(p=>p.ItemId).ToHashSet(StringComparer.Ordinal).SetEquals(desired.Placements.Select(p=>p.ItemId))));
        _contentDraft=null;
        if(result.Succeeded && result.Status==StateCommandStatus.Committed)
        {
            if(recordHistory) _contentHistory.Record(before,desired);
            _contentNotice="布局已保存";
        }
        else _contentNotice=result.Succeeded ? null : result.Error ?? "布局未保存，已恢复当前权威状态。";
        if(result.Projection?.PostersById.TryGetValue(before.PosterId,out var current)==true)
        {
            _authoritativePoster=current.Poster; _posterEntityVersion=current.EntityVersion;
            _posterRevision=current.PosterRevision; _stateRevision=result.CurrentRevision;
        }
        _solvedDocument=null; RefreshContentSurface();
        spatial.NotifyContentEditorChanged(PosterId);
        return result.Succeeded ? null : _contentNotice;
    }
    internal string? SelectContentItem(string? id)
    {
        if (_disposed || _contentPresentationSuspended) return "内容呈现已暂停，请等待恢复。";
        CancelContentGesture("explicit-selection");
        if(id is not null && !ContentDocument.Placements.Any(p=>p.ItemId==id)) return "内容已变化，请重新选择。";
        _selectedContentItem=id; RefreshContentSurface(); spatial.NotifyContentEditorChanged(PosterId); return null;
    }
    internal string? NudgeContent(int x,int y,bool resize)
    {
        if (_disposed || _contentPresentationSuspended) return "内容呈现已暂停，请等待恢复。";
        if(_selectedContentItem is not { } id) return "请先选择卡片。";
        var slot=EnsureContentSnapshot(_bounds).Items.FirstOrDefault(p=>p.ItemId==id);
        var placement=ContentDocument.Placements.FirstOrDefault(p=>p.ItemId==id);
        if(slot is null || placement is null) return "所选卡片已变化。";
        return EditContent(resize ? new(ContentLayoutEditKind.Resize,id)
            {ColumnSpan=Math.Clamp(placement.ColumnSpan+x,1,ContentLayoutPolicy.MaximumSpan),RowSpan=Math.Clamp(placement.RowSpan+y,1,ContentLayoutPolicy.MaximumSpan)}
            : new(ContentLayoutEditKind.Move,id)
            {Column=Math.Clamp(slot.Column-slot.RegionColumn+x,0,ContentLayoutPolicy.MaximumColumns-1),Row=Math.Clamp(slot.Row-slot.RegionRow+y,0,ContentLayoutPolicy.MaximumRow)});
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
    private void AddContentMenu(MenuFlyout flyout,Func<string,bool> current)
    {
        void Add(string title,Action action,bool enabled=true)
        {
            var item=StyleMenuItem(new MenuFlyoutItem {Text=title,IsEnabled=enabled});
            item.Click+=(_,_)=>{if(current("content-"+title)){flyout.Hide();action();}};
            flyout.Items.Add(item);
        }
        if(!IsEditing || _geometry.IsCollapsed || _disposed || _contentPresentationSuspended) return;
        var d=ContentDocument;
        Add(d.ShowBaselineGrid?"隐藏基线网格":"显示基线网格",()=>EditContent(new(ContentLayoutEditKind.Guides){ShowBaselineGrid=!d.ShowBaselineGrid}),!_contentReadOnly);
        Add(d.ShowSpanLabels?"隐藏跨度提示":"显示跨度提示",()=>EditContent(new(ContentLayoutEditKind.Guides){ShowSpanLabels=!d.ShowSpanLabels}),!_contentReadOnly);
        if(d.Mode!=ContentLayoutMode.Manual) Add("进入手动排版",()=>EditContent(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual}),!_contentReadOnly);
        else
        {
            var exit=StyleMenuItem(new MenuFlyoutSubItem {Text="退出手动排版…"});
            foreach(var choice in new[]{("保留当前顺序",ManualLayoutExit.KeepOrder),("转为顺序锚",ManualLayoutExit.ConvertToAnchors),("恢复自动排列",ManualLayoutExit.RestoreAutomatic)})
            {
                var item=StyleMenuItem(new MenuFlyoutItem{Text=choice.Item1});
                item.Click+=(_,_)=>{if(current("content-mode")){flyout.Hide();EditContent(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic,ManualExit=choice.Item2});}};
                exit.Items.Add(item);
            }
            flyout.Items.Add(exit);
        }
        if(_selectedContentItem is { } selected && d.Placements.FirstOrDefault(p=>p.ItemId==selected) is { } placement)
            Add(placement.FixedPosition?"解除所选卡片固定":"固定所选卡片位置",()=>EditContent(new(ContentLayoutEditKind.Fixed,selected){Enabled=!placement.FixedPosition}),!_contentReadOnly);
        Add("撤销布局",()=>UndoContent(false),!_contentReadOnly);
        Add("重做布局",()=>UndoContent(true),!_contentReadOnly);
        flyout.Items.Add(new MenuFlyoutSeparator());
    }
    internal string? UndoContent(bool redo)
    {
        if(_contentReadOnly || _geometry.IsLocked || HasContentGesture || _transitionLayout is not null || _disposed || _contentPresentationSuspended) return "当前不能撤销布局。";
        var before=ContentDocument;
        var proposal=redo?_contentHistory.ProposeRedo(before):_contentHistory.ProposeUndo(before);
        if(proposal is null) return "没有可用的布局历史，或当前版本已被外部修改。";
        string? error=CommitContentDocument(before,proposal.Document,_posterEntityVersion,_stateRevision,false);
        if(error is null) _contentHistory.Confirm(proposal,ContentDocument);
        else _contentHistory.CancelProposal();
        return error;
    }
    private sealed class ContentGesture(PosterInputCapture capture,ContentLayoutDocument document,ContentLayoutSnapshot snapshot,
        ContentResolvedPlacement slot,long entityVersion,long revision,DesktopPixelRect frame,PosterInputTargetKind target,double scaleX,double scaleY,bool canEdit,IDisposable lease)
    {
        public PosterInputCapture Capture {get;}=capture;
        public ContentLayoutDocument Document {get;}=document;
        public ContentLayoutSnapshot Snapshot {get;}=snapshot;
        public ContentResolvedPlacement Slot {get;}=slot;
        public long EntityVersion {get;}=entityVersion;
        public long Revision {get;}=revision;
        public DesktopPixelRect Frame {get;}=frame;
        public PosterInputTargetKind Target {get;}=target;
        public double ScaleX {get;}=scaleX;
        public double ScaleY {get;}=scaleY;
        public bool CanEdit {get;}=canEdit;
        public IDisposable Lease {get;}=lease;
        public ContentLayoutDocument? Proposal {get;set;}
        public ContentLayoutEdit? LastEdit {get;set;}
    }
}

internal sealed record ContentLayoutEditorView(ContentLayoutDocument Document,long EntityVersion,string? SelectedItemId,bool IsEditing,bool CanEdit,string? Notice);
