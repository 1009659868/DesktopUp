using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentLayoutSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutProposalTests
{
    private static ContentHeatProposal Heat(ContentLayoutDocument d,double scale=1,long version=1)=>new(d.PosterId,d.ItemSetVersion,version,
        d.Placements.Select((p,i)=>new ContentHeatItem(p.ItemId,d.Placements.Count-i-1,scale)).ToArray());

    [Theory]
    [InlineData(.5,2)] [InlineData(.75,3)] [InlineData(1,4)] [InlineData(1.5,6)] [InlineData(3,12)]
    public void Heat_is_one_length_multiplier_from_author_intent_not_cumulative(double scale,int span)
    {
        var d=Doc(2); var p=Heat(d,scale);var a=ContentLayoutProposals.ResolveHeat(d,p);var b=ContentLayoutProposals.ResolveHeat(d,p);
        Assert.All(a.Document.Placements,i=>{Assert.Equal(span,i.ColumnSpan);Assert.Equal(span,i.RowSpan);});
        Assert.True(ContentLayoutEditing.SameState(a.Document,b.Document));Assert.All(d.Placements,i=>Assert.Equal(4,i.ColumnSpan));
        Assert.Equal(d.DocumentVersion,a.Document.DocumentVersion);Assert.Equal(d.ItemSetVersion,a.Document.ItemSetVersion);
    }

    [Theory]
    [InlineData(.89,1,1)] [InlineData(.94,1,2)] [InlineData(1.28,2,2)] [InlineData(1.31,2,3)]
    [InlineData(1.22,3,3)] [InlineData(1.19,3,2)] [InlineData(2.28,3,3)] [InlineData(2.31,3,4)]
    public void Version_one_quantizer_has_five_tiers_and_hysteresis(double scale,int previous,int expected)=>Assert.Equal(expected,ContentLayoutProposals.Quantize(scale,previous));

    [Fact]
    public void Manual_layout_ignores_heat_and_size_override_and_pin_are_independent()
    {
        var d=Doc(3);var manual=d with {Mode=ContentLayoutMode.Manual};Assert.True(ContentLayoutEditing.SameState(manual,ContentLayoutProposals.ResolveHeat(manual,Heat(manual,3)).Document));
        d=d with {Mode=ContentLayoutMode.Anchored,Placements=d.Placements.Select((p,i)=>p with {ManualSize=i==0,PinnedIndex=i==0?1:null}).ToArray()};
        var effective=ContentLayoutProposals.ResolveHeat(d,Heat(d,1.5)).Document;
        Assert.Equal(4,effective.Placements[0].ColumnSpan);Assert.Equal(6,effective.Placements[1].ColumnSpan);
        Assert.Equal(d.Placements[0].ItemId,ContentLayoutSolver.Resolve(effective,View()).Items[0].ItemId);
    }

    [Theory]
    [InlineData("owner")] [InlineData("itemset")] [InlineData("unknown-item")] [InlineData("mapping")] [InlineData("scale")] [InlineData("duplicate")]
    public void Invalid_or_stale_proposal_cannot_resolve(string kind)
    {
        var d=Doc(2);var p=Heat(d);p=kind switch{
            "owner"=>p with{PosterId="B"},"itemset"=>p with{ItemSetVersion=2},"unknown-item"=>p with{Items=[new("A:removed",0,1)]},
            "mapping"=>p with{MappingVersion=2},"scale"=>p with{Items=[new(d.Placements[0].ItemId,0,double.NaN)]},
            _=>p with{Items=[p.Items[0],p.Items[0]]}};
        Assert.Throws<ContentLayoutException>(()=>ContentLayoutProposals.ResolveHeat(d,p));
    }

    [Fact]
    public void Protected_interaction_keeps_only_latest_immutable_heat_and_never_replays_consumed_version()
    {
        var d=Doc(1);var b=new ContentHeatProposalBuffer("A",1);var mutable=Heat(d).Items.ToArray();Assert.True(b.Offer(Heat(d) with {Items=mutable}));
        mutable[0]=mutable[0] with {RelativeScale=3};Assert.Null(b.Take(true));Assert.Equal(1,b.Take(false)!.Items[0].RelativeScale);
        Assert.False(b.Offer(Heat(d)));Assert.True(b.Offer(Heat(d,.5,2)));Assert.True(b.Offer(Heat(d,3,3)));
        Assert.Null(b.Take(true));Assert.Equal(3,b.Take(false)!.Version);Assert.Null(b.Take(false));
        Assert.False(b.Offer(Heat(d) with {ItemSetVersion=2,Version=4}));b.Invalidate();Assert.False(b.Offer(Heat(d,1,5)));
    }

    [Fact]
    public void Theme_paint_is_geometry_neutral_metrics_wait_and_invalid_keeps_last_good()
    {
        var original=PosterPresentationPolicy.Defaults();var paint=original with {Theme=original.Theme with{Accent="#AABBCC"}};
        var metric=paint with{Content=paint.Content with{GapDip=20}};var latest=metric with{Content=metric.Content with{GapDip=25}};
        Assert.Equal(ContentThemeChange.None,ContentLayoutProposals.ClassifyThemeChange(original,original));
        Assert.Equal(ContentThemeChange.PaintOnly,ContentLayoutProposals.ClassifyThemeChange(original,paint));
        Assert.Equal(ContentThemeChange.Metrics,ContentLayoutProposals.ClassifyThemeChange(paint,metric));
        var b=new ContentThemeProposalBuffer(original);Assert.True(b.Offer(new(1,paint),true));Assert.Equal(paint,b.Current);
        Assert.True(b.Offer(new(2,metric),true));Assert.True(b.Offer(new(3,latest),true));Assert.False(b.ApplyPending(true));Assert.Equal(paint,b.Current);
        Assert.False(b.Offer(new(4,latest with{Content=latest.Content with{GapDip=double.NaN}}),false));Assert.Equal(paint,b.Current);
        Assert.True(b.ApplyPending(false));Assert.Equal(latest,b.Current);Assert.False(b.ApplyPending(false));Assert.False(b.Offer(new(2,metric),false));
    }
}
