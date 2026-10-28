using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentLayoutSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutEditingTests
{
    private static ContentLayoutDocument Edit(ContentLayoutDocument d,ContentLayoutEdit edit)
    { var r=ContentLayoutEditing.Apply(d,edit,View()); Assert.True(r.Accepted,r.Error); Safe(ContentLayoutSolver.Resolve(r.Document,View())); return r.Document; }
    private static ContentLayoutDocument Manual(int count=3) => Edit(Doc(count),new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});

    [Fact]
    public void Automatic_drag_pins_only_the_final_read_position_without_changing_mode_or_hard_geometry()
    {
        // DEC-018/019: the selected card receives an order pin, while other automatic authors remain intact.
        var d=Doc(); var r=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Move,d.Placements[0].ItemId){TargetReadOrder=1},View());
        Assert.True(r.Accepted,r.Error);
        var moved=r.Document.Placements.Single(p=>p.ItemId==d.Placements[0].ItemId);
        Assert.Equal(2,moved.PinnedIndex); Assert.False(moved.FixedPosition); Assert.Null(moved.Column); Assert.Null(moved.Row);
        Assert.Equal(ContentLayoutMode.Automatic,r.Document.Mode);
        Assert.Equal(d.Placements.Skip(1),r.Document.Placements.Skip(1));
        Assert.Equal(moved.ItemId,ContentLayoutSolver.Resolve(r.Document,View()).Items[1].ItemId);
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
    public void Compatible_legacy_hard_obstacle_rejects_manual_collision_and_survives_independent_automatic_size_restore()
    {
        var automatic=Doc(3) with {SchemaVersion=2,Mode=ContentLayoutMode.Anchored};
        automatic=automatic with {Placements=automatic.Placements.Select((p,i)=>i==1
            ?p with {Column=4,Row=0,FixedPosition=true}:p).ToArray()};
        var d=Edit(automatic,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});
        var rejected=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Move,d.Placements[0].ItemId){Column=4,Row=0},View());
        Assert.False(rejected.Accepted); Assert.Same(d,rejected.Document);
        var resized=Edit(automatic,new(ContentLayoutEditKind.Resize,automatic.Placements[1].ItemId){ColumnSpan=6,RowSpan=4});
        var restored=Edit(resized,new(ContentLayoutEditKind.SizeOverride,resized.Placements[1].ItemId){Enabled=false});
        Assert.True(restored.Placements[1].FixedPosition); Assert.False(restored.Placements[1].ManualSize);
        Assert.Equal(4,restored.Placements[1].Column); Assert.Equal(0,restored.Placements[1].Row);
        Assert.Equal(automatic.Placements[1].ColumnSpan,restored.Placements[1].ColumnSpan);
    }

    [Fact]
    public void New_hard_position_creation_and_legacy_hard_position_release_require_an_explicit_conversion_contract()
    {
        var automatic=Doc(1);
        var creation=ContentLayoutEditing.Apply(automatic,new(ContentLayoutEditKind.Fixed,automatic.Placements[0].ItemId){Enabled=true},View());
        Assert.False(creation.Accepted); Assert.Same(automatic,creation.Document);
        var legacy=automatic with {SchemaVersion=2,Mode=ContentLayoutMode.Anchored,
            Placements=[automatic.Placements[0] with {Column=0,Row=0,FixedPosition=true}]};
        var release=ContentLayoutEditing.Apply(legacy,new(ContentLayoutEditKind.Fixed,legacy.Placements[0].ItemId){Enabled=false},View());
        Assert.False(release.Accepted); Assert.Same(legacy,release.Document);
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
    public void Legacy_manual_without_saved_automatic_intent_cannot_invent_exit_A()
    {
        var d=Manual();
        d=d with {SchemaVersion=2,SavedAutomaticMode=null,Placements=d.Placements.Select(p=>p with {SavedAutomaticState=null}).ToArray()};
        var r=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic},View());
        Assert.False(r.Accepted); Assert.Same(d,r.Document);
    }

    [Theory]
    [InlineData(ManualLayoutExit.KeepOrder)]
    [InlineData(ManualLayoutExit.ConvertToAnchors)]
    [InlineData(ManualLayoutExit.RestoreAutomatic)]
    public void Legacy_exit_strategies_are_rejected_without_erasing_manual_or_return_intent(ManualLayoutExit exit)
    {
        var d=Manual(); d=Edit(d,new(ContentLayoutEditKind.Resize,d.Placements[0].ItemId){ColumnSpan=6,RowSpan=6});
        var after=ContentLayoutEditing.Apply(d,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic,ManualExit=exit},View());
        Assert.False(after.Accepted); Assert.Same(d,after.Document);
        Assert.Equal(ContentLayoutMode.Manual,after.Document.Mode);
        Assert.Equal(6,after.Document.Placements[0].ColumnSpan);
        Assert.All(after.Document.Placements,p=>Assert.NotNull(p.SavedAutomaticState));
    }

    [Fact]
    public void Exit_A_restores_saved_automatic_pin_and_size_instead_of_carrying_manual_changes()
    {
        var automatic=Edit(Doc(3),new(ContentLayoutEditKind.Pinned,Doc(3).Placements[0].ItemId){Enabled=true,PinnedIndex=1});
        automatic=Edit(automatic,new(ContentLayoutEditKind.Resize,automatic.Placements[0].ItemId){ColumnSpan=5,RowSpan=4});
        var manual=Edit(automatic,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});
        manual=Edit(manual,new(ContentLayoutEditKind.Resize,manual.Placements[0].ItemId){ColumnSpan=6,RowSpan=6});
        var after=Edit(manual,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic});
        Assert.Equal(ContentLayoutMode.Automatic,after.Mode); Assert.Null(after.SavedAutomaticMode);
        Assert.Equal(5,after.Placements[0].ColumnSpan); Assert.Equal(4,after.Placements[0].RowSpan);
        Assert.Equal(1,after.Placements[0].PinnedIndex); Assert.True(after.Placements[0].ManualSize);
        Assert.Equal(automatic.Placements[0].AutomaticSizeBasis,after.Placements[0].AutomaticSizeBasis);
        Assert.All(after.Placements,p=>{Assert.Null(p.Column);Assert.Null(p.Row);Assert.False(p.FixedPosition);Assert.Null(p.SavedAutomaticState);});
    }

    [Fact]
    public void Releasing_size_override_restores_original_author_basis_then_heat_can_resize()
    {
        var d=Doc(1) with {AllowHeatDrivenSize=true}; d=d with {Placements=[d.Placements[0] with {ColumnSpan=3,RowSpan=2}]};
        d=Edit(d,new(ContentLayoutEditKind.Resize,d.Placements[0].ItemId){ColumnSpan=8,RowSpan=6});
        d=Edit(d,new(ContentLayoutEditKind.SizeOverride,d.Placements[0].ItemId){Enabled=false});
        Assert.Equal(3,d.Placements[0].ColumnSpan); Assert.Equal(2,d.Placements[0].RowSpan); Assert.False(d.Placements[0].ManualSize);
        Assert.Null(d.Placements[0].AutomaticSizeBasis);
        var p=new ContentHeatProposal("A",1,1,[new(d.Placements[0].ItemId,0,1.5)]);
        Assert.Equal(5,ContentLayoutProposals.ResolveHeat(d,p).Document.Placements[0].ColumnSpan);
    }

    [Theory]
    [InlineData("foreign")] [InlineData("span")] [InlineData("row")] [InlineData("no-pin-slot")] [InlineData("missing-value")]
    public void Invalid_edits_never_partially_change_document(string kind)
    {
        var d=kind=="no-pin-slot"?Doc(3):Manual(); string id=d.Placements[0].ItemId;
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
        var before=Doc();var after=Edit(before,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=!before.ShowBaselineGrid}); var h=new ContentLayoutHistory();h.Record(before,after);
        var p=h.ProposeUndo(after)!;Assert.Equal(1,h.UndoCount);Assert.Equal(0,h.RedoCount);
        Assert.False(h.Confirm(p,after));h.CancelProposal();Assert.False(h.Confirm(p,p.Document));Assert.Equal(1,h.UndoCount);
        Assert.Null(h.ProposeUndo(after with { ShowSpanLabels=!after.ShowSpanLabels }));
        p=h.ProposeUndo(after)!;Assert.True(h.Confirm(p,p.Document));Assert.Equal(0,h.UndoCount);Assert.Equal(1,h.RedoCount);
        var changed=Edit(p.Document,new(ContentLayoutEditKind.Guides){ShowSpanLabels=!p.Document.ShowSpanLabels});h.Record(p.Document,changed);Assert.Equal(0,h.RedoCount);
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
