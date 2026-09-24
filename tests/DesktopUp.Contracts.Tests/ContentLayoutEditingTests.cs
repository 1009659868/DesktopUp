using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentLayoutSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutEditingTests
{
    private static ContentLayoutDocument Edit(ContentLayoutDocument d,ContentLayoutEdit edit)
    { var r=ContentLayoutEditing.Apply(d,edit,View()); Assert.True(r.Accepted,r.Error); Safe(ContentLayoutSolver.Resolve(r.Document,View())); return r.Document; }
    private static ContentLayoutDocument Manual(int count=3) => Edit(Doc(count),new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});

    [Fact]
    public void Dragging_pins_the_card_instead_of_requiring_manual_mode()
    {
        // Interaction revision: the drag gesture is itself the intent to pin. One card receives a
        // position override and the poster moves to anchored+automatic; every other card keeps its
        // automatic/heat ordering because full manual takeover is never a prerequisite.
        var d=Doc(); var r=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Move,d.Placements[0].ItemId){Column=0,Row=4},View());
        Assert.True(r.Accepted,r.Error);
        var moved=r.Document.Placements.Single(p=>p.ItemId==d.Placements[0].ItemId);
        Assert.True(moved.FixedPosition); Assert.Equal(4,moved.Row);
        Assert.Equal(ContentLayoutMode.Anchored,r.Document.Mode);
        Assert.False(r.Document.Placements[1].FixedPosition);
        Safe(ContentLayoutSolver.Resolve(r.Document,View()));
    }

    [Fact]
    public void Enter_manual_captures_geometry_and_versions_once()
    {
        var d=Doc(); var before=ContentLayoutSolver.Resolve(d,View()); var manual=Edit(d,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});
        Assert.Equal(before.Items.Select(p=>p.Bounds),ContentLayoutSolver.Resolve(manual,View()).Items.Select(p=>p.Bounds));
        Assert.Equal(2,manual.DocumentVersion); Assert.Equal(d.ItemSetVersion,manual.ItemSetVersion); Assert.All(manual.Placements,p=>Assert.Equal(2,p.Version));
        Assert.Same(manual,Edit(manual,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual}));
    }

    [Fact]
    public void Manual_move_pushes_nonfixed_peers_forward_without_mutating_business_identity()
    {
        var d=Manual(); var id=d.Placements[0].ItemId; var moved=Edit(d,new(ContentLayoutEditKind.Move,id){Column=4,Row=0});
        Assert.Equal(4,moved.Placements[0].Column); Assert.Equal(4,moved.Placements[1].Row); Assert.Equal(0,moved.Placements[2].Row);
        Assert.Equal(d.Placements.Select(p=>p.ItemId),moved.Placements.Select(p=>p.ItemId)); Assert.Equal(d.DocumentVersion+1,moved.DocumentVersion);
        Assert.Equal(d.Placements[2].Version,moved.Placements[2].Version);
    }

    [Fact]
    public void Fixed_obstacle_rejects_whole_edit_and_is_independent_of_size_override()
    {
        var d=Manual(); d=Edit(d,new(ContentLayoutEditKind.Fixed,d.Placements[1].ItemId){Enabled=true});
        var rejected=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Move,d.Placements[0].ItemId){Column=4,Row=0},View());
        Assert.False(rejected.Accepted); Assert.Same(d,rejected.Document);
        d=Edit(d,new(ContentLayoutEditKind.SizeOverride,d.Placements[1].ItemId){Enabled=false});
        Assert.True(d.Placements[1].FixedPosition); Assert.False(d.Placements[1].ManualSize);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void Guide_toggles_are_independent_and_do_not_change_geometry(bool grid,bool spans)
    {
        var d=Doc(); var a=ContentLayoutSolver.Resolve(d,View()); var after=Edit(d,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=grid,ShowSpanLabels=spans});
        Assert.Equal(grid,after.ShowBaselineGrid); Assert.Equal(spans,after.ShowSpanLabels);
        Assert.Equal(a.Items,ContentLayoutSolver.Resolve(after,View()).Items); Assert.All(after.Placements,p=>Assert.Equal(1,p.Version));
    }

    [Fact]
    public void Pin_conflict_walks_forward_without_overwriting_existing_global_anchor()
    {
        var d=Doc(3); d=Edit(d,new(ContentLayoutEditKind.Pinned,d.Placements[2].ItemId){Enabled=true,PinnedIndex=1});
        d=Edit(d,new(ContentLayoutEditKind.Pinned,d.Placements[0].ItemId){Enabled=true,PinnedIndex=1});
        Assert.Equal(1,d.Placements[2].PinnedIndex); Assert.Equal(2,d.Placements[0].PinnedIndex);
        Assert.Equal(new[]{2,0,1},ContentLayoutSolver.Resolve(d,View()).Items.Select(p=>p.SourceIndex));
    }

    [Fact]
    public void Manual_exit_without_choice_is_rejected()
    {
        var d=Manual(); var r=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic},View());
        Assert.False(r.Accepted); Assert.Same(d,r.Document);
    }

    [Theory]
    [InlineData(ManualLayoutExit.KeepOrder,ContentLayoutMode.Automatic,true)]
    [InlineData(ManualLayoutExit.ConvertToAnchors,ContentLayoutMode.Anchored,true)]
    [InlineData(ManualLayoutExit.RestoreAutomatic,ContentLayoutMode.Automatic,false)]
    public void Three_exit_choices_are_distinct_and_all_leave_manual(ManualLayoutExit exit,ContentLayoutMode mode,bool size)
    {
        var d=Manual(); d=Edit(d,new(ContentLayoutEditKind.Resize,d.Placements[0].ItemId){ColumnSpan=6,RowSpan=6});
        var after=Edit(d,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic,ManualExit=exit});
        Assert.Equal(mode,after.Mode); Assert.All(after.Placements,p=>{Assert.Null(p.Column);Assert.Null(p.Row);Assert.False(p.FixedPosition);Assert.Equal(size,p.ManualSize);});
        Assert.Equal(size?6:4,after.Placements[0].ColumnSpan);
        Assert.Equal(exit==ManualLayoutExit.ConvertToAnchors?1:null,after.Placements[0].PinnedIndex);
    }

    [Fact]
    public void Releasing_size_override_restores_neutral_span_then_heat_can_resize()
    {
        var d=Doc(1); d=Edit(d,new(ContentLayoutEditKind.Resize,d.Placements[0].ItemId){ColumnSpan=8,RowSpan=6});
        d=Edit(d,new(ContentLayoutEditKind.SizeOverride,d.Placements[0].ItemId){Enabled=false});
        Assert.Equal(4,d.Placements[0].ColumnSpan); Assert.Equal(4,d.Placements[0].RowSpan); Assert.False(d.Placements[0].ManualSize);
        var p=new ContentHeatProposal("A",1,1,[new(d.Placements[0].ItemId,0,1.5)]);
        Assert.Equal(6,ContentLayoutProposals.ResolveHeat(d,p).Document.Placements[0].ColumnSpan);
    }

    [Theory]
    [InlineData("foreign")] [InlineData("span")] [InlineData("row")] [InlineData("no-pin-slot")] [InlineData("missing-value")]
    public void Invalid_edits_never_partially_change_document(string kind)
    {
        var d=Manual(); string id=d.Placements[0].ItemId;
        var edit=kind switch {
            "foreign"=>new ContentLayoutEdit(ContentLayoutEditKind.Resize,"B:x"){ColumnSpan=2,RowSpan=2},
            "span"=>new(ContentLayoutEditKind.Resize,id){ColumnSpan=33,RowSpan=2},
            "row"=>new(ContentLayoutEditKind.Move,id){Column=0,Row=65535},
            "no-pin-slot"=>new(ContentLayoutEditKind.Pinned,id){Enabled=true,PinnedIndex=4},
            _=>new ContentLayoutEdit(ContentLayoutEditKind.Fixed,id)};
        var result=ContentLayoutEditing.Apply(d,edit,View()); Assert.False(result.Accepted); Assert.Same(d,result.Document);
    }

    [Fact]
    public void Continuous_reverse_moves_resize_and_undo_redo_have_no_drift()
    {
        var original=Manual(); var current=original; var history=new ContentLayoutHistory();
        for(int i=0;i<24;i++)
        {
            var next=Edit(current,new(ContentLayoutEditKind.Move,current.Placements[0].ItemId){Column=i%2==0?4:0,Row=i%3==0?8:0});
            history.Record(current,next); current=next;
        }
        string final=ContentLayoutPolicy.Serialize(current);
        int count=history.UndoCount;
        for(int i=0;i<count;i++) {var p=history.ProposeUndo(current);Assert.NotNull(p);Assert.True(history.Confirm(p,p.Document));current=p.Document;Safe(ContentLayoutSolver.Resolve(current,View()));}
        Assert.True(ContentLayoutEditing.SameState(original,current,true));
        for(int i=0;i<count;i++) {var p=history.ProposeRedo(current);Assert.NotNull(p);Assert.True(history.Confirm(p,p.Document));current=p.Document;Safe(ContentLayoutSolver.Resolve(current,View()));}
        Assert.True(ContentLayoutPolicy.TryRead(final,"A",out var expected,out _)); Assert.True(ContentLayoutEditing.SameState(expected!,current,true));
    }

    [Fact]
    public void History_waits_for_successful_cas_and_stale_confirmation_cannot_consume_entry()
    {
        var before=Doc();var after=Edit(before,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=false}); var h=new ContentLayoutHistory();h.Record(before,after);
        var p=h.ProposeUndo(after)!;Assert.Equal(1,h.UndoCount);Assert.Equal(0,h.RedoCount);
        Assert.False(h.Confirm(p,after));h.CancelProposal();Assert.False(h.Confirm(p,p.Document));Assert.Equal(1,h.UndoCount);
        Assert.Null(h.ProposeUndo(after with { ShowSpanLabels=false }));
        p=h.ProposeUndo(after)!;Assert.True(h.Confirm(p,p.Document));Assert.Equal(0,h.UndoCount);Assert.Equal(1,h.RedoCount);
        var changed=Edit(p.Document,new(ContentLayoutEditKind.Guides){ShowSpanLabels=false});h.Record(p.Document,changed);Assert.Equal(0,h.RedoCount);
    }

    [Fact]
    public void History_is_bounded_and_cannot_cross_poster()
    {
        var d=Doc();var h=new ContentLayoutHistory();
        for(int i=0;i<80;i++){var next=Edit(d,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=!d.ShowBaselineGrid});h.Record(d,next);d=next;}
        Assert.Equal(64,h.UndoCount); Assert.Null(h.ProposeUndo(ContentLayoutPolicy.Remap(d,"B")));
        Assert.Throws<ContentLayoutException>(()=>h.Record(ContentLayoutSamples.Equal("B"),ContentLayoutSamples.Equal("B") with {DocumentVersion=2,ShowBaselineGrid=false}));
    }
}
