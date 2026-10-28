using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentInteractionFeedbackTests
{
    private static ContentLayoutViewport View(double width=384,double height=384,double scrollY=0) =>
        new(width,height,ScrollYDip:scrollY,Generation:7);
    private static ContentLayoutDocument Automatic(int count=3) => new("feedback",Enumerable.Range(0,count)
        .Select(i=>new ContentPlacement("feedback:"+i,i,2,2)).ToArray())
        { BaseCellDip=48,BaseGapDip=0 };
    private static ContentLayoutDocument Adaptable() => Automatic(4) with
    {
        Mode=ContentLayoutMode.Manual,
        Placements=
        [
            new("feedback:0",0,4,2) {Column=0,Row=0,ManualSize=true},
            new("feedback:1",1,2,2) {Column=6,Row=0,ManualSize=true},
            new("feedback:2",2,2,1) {Column=0,Row=4,ManualSize=true},
            new("feedback:3",3,2,2) {Column=6,Row=8,ManualSize=true}
        ]
    };

    [Fact]
    public void Narrow_edit_reports_full_adoption_including_an_unchanged_screen_position_and_offscreen_restore_position()
    {
        var author=Adaptable();
        var viewport=View(192,144);
        var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=0,Row=5};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error); Assert.True(result.AdoptsProjection);
        string original=ContentLayoutPolicy.Serialize(author), candidate=ContentLayoutPolicy.Serialize(result.Document);
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result);

        Assert.Equal(ContentFeedbackState.Preview,feedback.State);
        Assert.Equal("feedback:0",feedback.Impact.EditedItemId);
        Assert.Equal(new[]{"feedback:1","feedback:3"},feedback.Impact.AdoptedItemIds);
        Assert.DoesNotContain("feedback:3",feedback.Impact.DisplacedItemIds);
        Assert.Contains("feedback:3",feedback.Impact.OffscreenAffectedItemIds);
        Assert.Equal(presented.Items.Single(p=>p.ItemId=="feedback:3").Bounds,
            result.PreviewSnapshot!.Items.Single(p=>p.ItemId=="feedback:3").Bounds);
        var restoredWide=ContentLayoutSolver.Resolve(author,View());
        var adoptedWide=ContentLayoutSolver.Resolve(result.Document,View());
        Assert.NotEqual(restoredWide.Items.Single(p=>p.ItemId=="feedback:3").Column,
            adoptedWide.Items.Single(p=>p.ItemId=="feedback:3").Column);
        Assert.Contains("整张",feedback.Message); Assert.Contains("屏外",feedback.Message); Assert.Contains("可一次撤销",feedback.Message);
        Assert.Equal(original,ContentLayoutPolicy.Serialize(author)); Assert.Equal(candidate,ContentLayoutPolicy.Serialize(result.Document));
    }

    [Fact]
    public void Peer_displacement_is_compared_with_presented_geometry_not_author_geometry()
    {
        var author=Adaptable(); var viewport=View(192,144);
        var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=0,Row=2};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        var impact=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result).Impact;

        Assert.Equal(new[]{"feedback:1","feedback:3"},impact.AdoptedItemIds);
        Assert.Equal(new[]{"feedback:1","feedback:2"},impact.DisplacedItemIds);
        Assert.Equal(new[]{"feedback:2","feedback:3"},impact.OffscreenAffectedItemIds);
        Assert.DoesNotContain(impact.EditedItemId,impact.DisplacedItemIds);
        // feedback:1 is both adopted and pushed; feedback:3 is adopted without being pushed.
        Assert.Contains("feedback:1",impact.AdoptedItemIds.Intersect(impact.DisplacedItemIds));
    }

    [Fact]
    public void Offscreen_uses_current_scroll_clip_and_counts_a_partial_clip_as_visible()
    {
        var author=Adaptable(); var viewport=View(192,96,192);
        var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:2") {Column=0,Row=6};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        var impact=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result).Impact;
        Assert.Equal(new[]{"feedback:1","feedback:3"},impact.OffscreenAffectedItemIds);
        Assert.DoesNotContain("feedback:2",impact.OffscreenAffectedItemIds);

        var partialView=View(192,144,48);
        var partial=ContentLayoutSolver.Resolve(author,partialView);
        var sameEdit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=0,Row=5};
        var partialResult=ContentLayoutEditing.Apply(author,sameEdit,partialView,partial);
        Assert.True(partialResult.Accepted,partialResult.Error);
        Assert.True(partial.Items.Single(p=>p.ItemId=="feedback:0").Clip.Height<partial.Items[0].Bounds.Height);
        Assert.DoesNotContain("feedback:0",ContentInteractionFeedbackPolicy.Describe(author,partial,sameEdit,partialResult).Impact.OffscreenAffectedItemIds);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Local_push_feedback_is_axis_symmetric_without_claiming_adoption(ContentFlowAxis axis)
    {
        bool vertical=axis==ContentFlowAxis.Vertical;
        var author=Automatic() with {Mode=ContentLayoutMode.Manual,FlowAxis=axis};
        author=author with {Placements=author.Placements.Select((p,i)=>p with
            {Column=vertical?i*2:0,Row=vertical?0:i*2,ManualSize=true}).ToArray()};
        var viewport=View(); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=vertical?2:0,Row=vertical?0:2};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result,ContentFeedbackPhase.Committed);
        Assert.Equal(ContentFeedbackState.Committed,feedback.State);
        Assert.False(feedback.Impact.AdoptsProjection); Assert.Empty(feedback.Impact.AdoptedItemIds);
        Assert.Equal(new[]{"feedback:1"},feedback.Impact.DisplacedItemIds);
        Assert.Empty(feedback.Impact.OffscreenAffectedItemIds); Assert.DoesNotContain("整张",feedback.Message);
    }

    [Theory]
    [InlineData(ContentLayoutEditKind.Move)]
    [InlineData(ContentLayoutEditKind.Resize)]
    public void Returning_to_presented_origin_or_size_does_not_report_adoption_or_commit(ContentLayoutEditKind kind)
    {
        var author=Adaptable(); var viewport=View(192,144); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var slot=presented.Items.Single(p=>p.ItemId=="feedback:1");
        var edit=kind==ContentLayoutEditKind.Move
            ? new ContentLayoutEdit(kind,slot.ItemId) {Column=slot.Column,Row=slot.Row}
            : new ContentLayoutEdit(kind,slot.ItemId) {ColumnSpan=slot.ColumnSpan,RowSpan=slot.RowSpan};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error); Assert.Same(author,result.Document);
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result,ContentFeedbackPhase.Committed);
        Assert.Equal(ContentFeedbackState.NoChange,feedback.State); Assert.False(feedback.Impact.AdoptsProjection);
        Assert.Empty(feedback.Impact.AdoptedItemIds); Assert.Empty(feedback.Impact.DisplacedItemIds);
        Assert.Contains("未保存",feedback.Message); Assert.DoesNotContain("可一次撤销",feedback.Message);
    }

    [Fact]
    public void Version_only_changes_are_not_presented_as_a_new_operation()
    {
        var author=Automatic(); var viewport=View(); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Guides);
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        result=result with {Document=result.Document with {DocumentVersion=8,ItemSetVersion=9,
            Placements=result.Document.Placements.Select(p=>p with {Version=12}).ToArray()}};
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result,ContentFeedbackPhase.Committed);
        Assert.Equal(ContentFeedbackState.NoChange,feedback.State); Assert.Null(feedback.Impact.EditedItemId);
    }

    [Fact]
    public void Rejected_manual_drop_retains_its_reason_and_does_not_report_the_adapted_picture_as_saved()
    {
        var author=Adaptable(); var viewport=View(192,144); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=-1,Row=2};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.False(result.Accepted);
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result,ContentFeedbackPhase.Committed);
        Assert.Equal(ContentFeedbackState.Rejected,feedback.State); Assert.Equal(result.Error,feedback.Reason);
        Assert.Contains(result.Error!,feedback.Message); Assert.Contains("未保存",feedback.Message);
        Assert.Empty(feedback.Impact.AdoptedItemIds); Assert.Empty(feedback.Impact.DisplacedItemIds);
        var cancelled=ContentInteractionFeedbackPolicy.Cancelled("capture-lost");
        Assert.Equal(ContentFeedbackState.Cancelled,cancelled.State); Assert.Equal("capture-lost",cancelled.Reason);
        Assert.Contains("未保存",cancelled.Message); Assert.DoesNotContain("可一次撤销",cancelled.Message);
    }

    [Fact]
    public void Automatic_drop_reports_the_final_available_pin_and_preserves_other_pins()
    {
        var author=Automatic(4); author=author with {Placements=author.Placements.Select((p,i)=>i==0?p with {PinnedIndex=1}:p).ToArray()};
        var viewport=View(); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:3") {TargetReadOrder=0};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error); Assert.Equal(2,result.Document.Placements[3].PinnedIndex);
        Assert.Equal(author.Placements[0],result.Document.Placements[0]);
        var feedback=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result);
        Assert.Equal(2,feedback.Impact.FinalPinnedPosition); Assert.Contains("第 2 位",feedback.Message);
        Assert.Contains("其他图钉保留",feedback.Message); Assert.Contains("未固定项仍自动排序",feedback.Message);
        Assert.Empty(feedback.Impact.DisplacedItemIds); Assert.Empty(feedback.Impact.AdoptedItemIds);
    }

    [Fact]
    public void Effective_heat_geometry_keeps_author_versions_and_does_not_invent_manual_adoption()
    {
        var author=Automatic() with {AllowHeatDrivenSize=true};
        var heat=new ContentHeatProposal(author.PosterId,author.ItemSetVersion,1,
            author.Placements.Select((p,i)=>new ContentHeatItem(p.ItemId,i,1.5)).ToArray());
        var effective=ContentLayoutProposals.ResolveHeat(author,heat).Document;
        var viewport=View(); var presented=ContentLayoutSolver.Resolve(effective,viewport);
        Assert.Equal(author.DocumentVersion,presented.DocumentVersion);
        Assert.Equal(author.ItemSetVersion,presented.ItemSetVersion);
        Assert.Equal(3,presented.Items.Single(p=>p.ItemId=="feedback:0").ColumnSpan);
        Assert.Equal(2,author.Placements[0].ColumnSpan);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Resize,"feedback:0") {ColumnSpan=3,RowSpan=4};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        Assert.Equal(result.Document.DocumentVersion,result.PreviewSnapshot!.DocumentVersion);
        Assert.Equal(author.ItemSetVersion,result.PreviewSnapshot.ItemSetVersion);
        var preview=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result);
        Assert.Equal(ContentFeedbackState.Preview,preview.State);
        Assert.False(preview.Impact.AdoptsProjection); Assert.Empty(preview.Impact.AdoptedItemIds);
        Assert.Empty(preview.Impact.DisplacedItemIds);
        Assert.Equal(new ContentAutomaticSizeBasis(2,2),result.Document.Placements[0].AutomaticSizeBasis);
        var committed=ContentInteractionFeedbackPolicy.Describe(author,presented,edit,result,ContentFeedbackPhase.Committed);
        Assert.Equal(ContentFeedbackState.Committed,committed.State);
        Assert.StartsWith("松手",preview.Message); Assert.StartsWith("已",committed.Message);
    }

    [Fact]
    public void Mode_roundtrip_explains_and_counts_restored_automatic_overrides_not_manual_sizes()
    {
        var author=Automatic() with {Mode=ContentLayoutMode.Anchored};
        author=author with {Placements=author.Placements.Select((p,i)=>i==0?p with {PinnedIndex=1,ManualSize=true,ColumnSpan=3}:p).ToArray()};
        var viewport=View(); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var enter=new ContentLayoutEdit(ContentLayoutEditKind.Mode) {Mode=ContentLayoutMode.Manual};
        var entered=ContentLayoutEditing.Apply(author,enter,viewport,presented);
        Assert.True(entered.Accepted,entered.Error);
        var entryFeedback=ContentInteractionFeedbackPolicy.Describe(author,presented,enter,entered,ContentFeedbackPhase.Committed);
        Assert.True(entryFeedback.Impact.EnteredManual); Assert.Contains("整张",entryFeedback.Message); Assert.Contains("原自动图钉",entryFeedback.Message);
        var changed=ContentLayoutEditing.Apply(entered.Document,new(ContentLayoutEditKind.Resize,"feedback:0") {ColumnSpan=4,RowSpan=3},viewport);
        Assert.True(changed.Accepted,changed.Error);
        var manualPicture=ContentLayoutSolver.Resolve(changed.Document,viewport);
        var exit=new ContentLayoutEdit(ContentLayoutEditKind.Mode) {Mode=ContentLayoutMode.Automatic};
        var exited=ContentLayoutEditing.Apply(changed.Document,exit,viewport,manualPicture);
        Assert.True(exited.Accepted,exited.Error); Assert.Equal(ContentLayoutMode.Anchored,exited.Document.Mode);
        Assert.Equal(3,exited.Document.Placements[0].ColumnSpan); Assert.Equal(2,exited.Document.Placements[0].RowSpan);
        var feedback=ContentInteractionFeedbackPolicy.Describe(changed.Document,manualPicture,exit,exited,ContentFeedbackPhase.Committed);
        Assert.True(feedback.Impact.RestoredAutomatic); Assert.False(feedback.Impact.EnteredManual);
        Assert.Equal(new[]{"feedback:0"},feedback.Impact.RestoredPinnedItemIds);
        Assert.Equal(new[]{"feedback:0"},feedback.Impact.RestoredSizeOverrideItemIds);
        Assert.Contains("手动位置和大小不带回",feedback.Message); Assert.Contains("可一次撤销",feedback.Message);
    }

    [Fact]
    public void Incomplete_presented_member_sets_cannot_silently_underreport_impact()
    {
        var author=Adaptable(); var viewport=View(192,144); var presented=ContentLayoutSolver.Resolve(author,viewport);
        var edit=new ContentLayoutEdit(ContentLayoutEditKind.Move,"feedback:0") {Column=0,Row=5};
        var result=ContentLayoutEditing.Apply(author,edit,viewport,presented);
        Assert.True(result.Accepted,result.Error);
        var visibleOnly=presented with {Items=presented.Items.Where(p=>!p.Clip.IsEmpty).ToArray()};
        Assert.Throws<ArgumentException>(()=>ContentInteractionFeedbackPolicy.Describe(author,visibleOnly,edit,result));
    }
}
