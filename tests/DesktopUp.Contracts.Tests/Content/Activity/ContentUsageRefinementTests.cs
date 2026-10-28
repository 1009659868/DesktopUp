using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentUsageRefinementTests
{
    private static void Detach(Fixture f, string poster)
    {
        var current=f.Current;
        Assert.True(f.Coordinator.Execute(new ContentDetachCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,
            current.Revision,StateReadSet.ForPoster(poster,current.PostersById[poster].EntityVersion) with {ContentVersion=current.ContentVersion},
            poster,current.ContentCatalog.PosterItems.Where(p=>p.PosterId==poster).Select(p=>p.LayoutItemId).ToArray(),true)).Succeeded);
    }
    [Fact] public void LastDetachedRecordIsPreservedButNextPreviewDefaultsToIndependent()
    {
        using var f=new Fixture();var original=Entry();f.Commit(f.AddCommand(f.Main,original));Detach(f,f.Main);
        Assert.Single(f.Current.ContentCatalog.Items);Assert.Single(f.Current.ContentCatalog.Memberships);
        var incoming=Entry();var preview=ContentReferencePreviewPolicy.ForItem(f.Current.ContentCatalog,incoming.Item,[]);
        Assert.Empty(preview.Reusable);Assert.Equal(ContentReferenceChoice.CreateIndependent,preview.DefaultChoice);
        f.Commit(f.AddCommand(f.Main,incoming));Assert.Equal(2,f.Current.ContentCatalog.Items.Count);
        var member=f.Current.ContentCatalog.Memberships.Single(m=>m.Id==f.Current.ContentCatalog.PosterItems.Single().MembershipId);
        Assert.Equal(incoming.Item.Id,member.ContentItemId);Assert.NotEqual(original.Item.Id,member.ContentItemId);
    }
    [Fact] public void StillOwnedByAnotherPosterDefaultsToSkip()
    {
        using var f=new Fixture();var original=Entry();string b=f.NewPoster();f.Commit(f.AddCommand(b,original));
        var preview=ContentReferencePreviewPolicy.ForItem(f.Current.ContentCatalog,Entry().Item,[]);
        Assert.Equal(original.Item.Id,Assert.Single(preview.Reusable).Item.Id);Assert.Equal(new[]{b},preview.Reusable[0].PosterIds);
        Assert.Equal(ContentReferenceChoice.Skip,preview.DefaultChoice);
        Detach(f,b);Assert.Empty(ContentReferencePreviewPolicy.ForItem(f.Current.ContentCatalog,Entry().Item,[]).Reusable);
    }
    [Fact] public void BatchDuplicateStaysExplicitAndDoesNotReviveHistoricalRecord()
    {
        using var f=new Fixture();var historical=Entry();f.Commit(f.AddCommand(f.Main,historical));Detach(f,f.Main);
        var first=Entry().Item;var next=Entry().Item;
        var preview=ContentReferencePreviewPolicy.ForItem(f.Current.ContentCatalog,next,[first]);
        var option=Assert.Single(preview.Reusable);Assert.Equal(first.Id,option.Item.Id);Assert.True(option.FromThisBatch);Assert.Empty(option.PosterIds);
        Assert.Equal(ContentReferenceChoice.Skip,preview.DefaultChoice);
    }
    [Fact] public void DifferentSourceHasNoReuseAndPreviewDoesNotMutateCatalog()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var before=ContentCatalogCodec.Write(f.Current.ContentCatalog);
        Assert.Empty(ContentReferencePreviewPolicy.ForItem(f.Current.ContentCatalog,Entry("different.txt").Item,[]).Reusable);
        Assert.Equal(before,ContentCatalogCodec.Write(f.Current.ContentCatalog));
    }
    [Theory] [InlineData(120,110,20,116,86)] [InlineData(210,80,24,206,52)] [InlineData(40,240,20,36,216)]
    public void RealIconUsesTheCardImageAreaNotTheOldFixedIconCap(double w,double h,double label,double expectedW,double expectedH)
    { var area=ContentCardPresentationPolicy.ImageArea(w,h,label);Assert.Equal(expectedW,area.Width);Assert.Equal(expectedH,area.Height); }
    [Fact] public void SingleControlShiftAndClearedSelectionHaveConsistentHighlight()
    {
        string[] order=["a","b","c"];
        var s=ContentSelection.Empty.Select(order,"a");Assert.True(ContentCardPresentationPolicy.IsSelected(s.Ids,"a"));
        s=s.Select(order,"c",true);Assert.True(ContentCardPresentationPolicy.IsSelected(s.Ids,"a"));Assert.True(ContentCardPresentationPolicy.IsSelected(s.Ids,"c"));
        s=s.Select(order,"a",true);Assert.Single(s.Ids);Assert.True(ContentCardPresentationPolicy.IsSelected(s.Ids,"c"));
        Assert.False(ContentCardPresentationPolicy.IsSelected(s.Select(order,null).Ids,"c"));
    }
}
