using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class RealContentUsageTests
{
    private static ContentOpenRequest Request(Fixture f, string? poster = null)
    {
        string owner=poster??f.Main;
        var relation=f.Current.ContentCatalog.PosterItems.First(p=>p.PosterId==owner);
        var target=new ContentActivationTarget(owner,relation.LayoutItemId,1,1,"fixture");
        return ContentOpenContract.Prepare(f.Current.ContentCatalog,new(target,Guid.NewGuid(),ContentOpenOrigin.Enter),target).Request!;
    }
    private static ContentDetachCommand Detach(Fixture f, params string[] ids)=>new(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,
        f.Current.Revision,new(new Dictionary<string,long>{{f.Main,f.Current.PostersById[f.Main].EntityVersion}},ContentVersion:f.Current.ContentVersion),f.Main,ids,true);

    [Fact] public void DetachOnlyRemovesPosterRelationOneRevisionAndReplay()
    {
        using var f=new Fixture();var entry=Entry();f.Commit(f.AddCommand(f.Main,entry));string b=f.NewPoster();
        f.Commit(f.AddCommand(b,Entry("other.txt")));
        string id=f.Current.ContentCatalog.PosterItems.Single(p=>p.PosterId==f.Main).LayoutItemId;
        string? other=f.Current.PostersById[b].Poster.ContentLayoutJson;long rev=f.Current.Revision;
        var command=Detach(f,id);var result=f.Coordinator.Execute(command);
        Assert.True(result.Succeeded,result.Error);Assert.Equal(rev+1,f.Current.Revision);
        Assert.Equal(2,f.Current.ContentCatalog.Items.Count);Assert.Equal(2,f.Current.ContentCatalog.Memberships.Count);
        Assert.Single(f.Current.ContentCatalog.PosterItems);Assert.Equal(b,f.Current.ContentCatalog.PosterItems.Single().PosterId);
        Assert.Equal(other,f.Current.PostersById[b].Poster.ContentLayoutJson);
        Assert.Equal(StateCommandStatus.Replayed,f.Coordinator.Execute(command).Status);
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_poster_items"));
    }
    [Fact] public void DetachRequiresConfirmationAndCompleteCurrentVersions()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));
        var command=Detach(f,f.Current.ContentCatalog.PosterItems.Single().LayoutItemId);
        Assert.Equal(StateCommandStatus.ConfirmationRequired,f.Coordinator.Execute(command with {Confirmed=false}).Status);
        Assert.False(f.Coordinator.Execute(command with {ReadSet=command.ReadSet with {ContentVersion=null}}).Succeeded);
        f.Commit(f.AddCommand(f.Main,Entry("second.txt")));
        Assert.Equal(StateCommandStatus.Conflict,f.Coordinator.Execute(command).Status);
        Assert.Equal(2,f.Current.ContentCatalog.PosterItems.Count);
    }
    [Fact] public void DetachSqlFaultRollsBackLayoutRelationsAndReceipt()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));
        var before=f.Current;var command=Detach(f,before.ContentCatalog.PosterItems.Single().LayoutItemId);
        f.Sql("CREATE TRIGGER reject_detach BEFORE DELETE ON content_poster_items BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before.Revision,f.Coordinator.Refresh().Revision);Assert.Single(f.Current.ContentCatalog.PosterItems);
        f.Sql("DROP TRIGGER reject_detach;");Assert.True(f.Coordinator.Execute(command).Succeeded);
    }
    [Fact] public void TailPinsStayLegalWhenCardinalityFallsAndRestOfCompositionStays()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry("a"),Entry("b"),Entry("c")));
        var p=f.Current.PostersById[f.Main];ContentLayoutPolicy.TryRead(p.Poster.ContentLayoutJson,f.Main,out var layout,out _);
        layout=layout! with {Placements=layout!.Placements.Select((v,i)=>v with {PinnedIndex=i==0?null:i+1}).ToArray()};
        f.SetLayout(ContentLayoutPolicy.Serialize(layout));
        Assert.True(f.Coordinator.Execute(Detach(f,layout.Placements[0].ItemId)).Succeeded);
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson,f.Main,out var after,out _);
        Assert.Equal(new int?[]{1,2},after!.Placements.Select(p=>p.PinnedIndex));
        Assert.Equal(layout.Placements.Skip(1).Select(p=>(p.ColumnSpan,p.RowSpan)),after.Placements.Select(p=>(p.ColumnSpan,p.RowSpan)));
    }
    [Fact] public void DurableOpeningClaimBlocksReplayConflictAndConcurrentTarget()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var journal=new SqliteContentOpenJournal(f.Store);var request=Request(f);
        Assert.Equal(ContentOpenRequestAdmission.Ready,journal.TryStart(request));
        Assert.Equal(ContentOpenRequestAdmission.Duplicate,journal.TryStart(request));
        Assert.Equal(ContentOpenRequestAdmission.Conflict,journal.TryStart(request with {Origin=ContentOpenOrigin.DoubleClick}));
        Assert.Equal(ContentOpenRequestAdmission.Busy,journal.TryStart(Request(f)));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_open_facts"));
    }
    [Fact] public void SuccessIsRecordedOnceEvenAfterRelationIsDetached()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var journal=new SqliteContentOpenJournal(f.Store);var request=Request(f);
        Assert.Equal(ContentOpenRequestAdmission.Ready,journal.TryStart(request));
        Assert.True(f.Coordinator.Execute(Detach(f,f.Current.ContentCatalog.PosterItems.Single().LayoutItemId)).Succeeded);
        var completion=new ContentOpenCompletion(request.ActivationId,request.Source.ProviderId,ContentOpenOutcome.Succeeded,ContentOpenEvidence.ShellAccepted,DateTimeOffset.UtcNow);
        Assert.NotNull(journal.Complete(request,completion));Assert.Null(journal.Complete(request,completion));
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_open_facts"));Assert.Empty(f.Coordinator.Refresh().ContentCatalog.PosterItems);
        Assert.Equal(ContentOpenRequestAdmission.Duplicate,new SqliteContentOpenJournal(f.Store).TryStart(request));
    }
    [Theory]
    [InlineData(ContentOpenOutcome.Cancelled)] [InlineData(ContentOpenOutcome.Failed)]
    [InlineData(ContentOpenOutcome.TimedOut)] [InlineData(ContentOpenOutcome.Unavailable)]
    [InlineData(ContentOpenOutcome.Handled)]
    public void NonSuccessNeverProducesAnOpeningFact(ContentOpenOutcome outcome)
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var journal=new SqliteContentOpenJournal(f.Store);var request=Request(f);
        journal.TryStart(request);
        Assert.Null(journal.Complete(request,new(request.ActivationId,request.Source.ProviderId,outcome,ContentOpenEvidence.None,DateTimeOffset.UtcNow)));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(ContentOpenRequestAdmission.Duplicate,journal.TryStart(request));
    }
    [Fact] public void InterruptedOpeningIsNeverAutomaticallyRetried()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var request=Request(f);
        new SqliteContentOpenJournal(f.Store).TryStart(request);
        var restarted=new SqliteContentOpenJournal(f.Store);restarted.RetireInterruptedRequests();
        Assert.Equal(ContentOpenRequestAdmission.Duplicate,restarted.TryStart(request));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(ContentOpenRequestAdmission.Ready,restarted.TryStart(Request(f)));
    }
    [Fact] public void StaleRepairAndForeignCompletionCannotCreateSuccess()
    {
        using var f=new Fixture();var original=Entry();f.Commit(f.AddCommand(f.Main,original));var request=Request(f);
        f.Commit(f.AddCommand(f.Main,Entry("new")) with {RepairContentId=original.Item.Id});
        var journal=new SqliteContentOpenJournal(f.Store);Assert.Equal(ContentOpenRequestAdmission.Conflict,journal.TryStart(request));
        var current=Request(f);journal.TryStart(current);
        Assert.Null(journal.Complete(current,new(Guid.NewGuid(),current.Source.ProviderId,ContentOpenOutcome.Succeeded,ContentOpenEvidence.ShellAccepted,DateTimeOffset.UtcNow)));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_open_facts"));
    }
    [Fact] public void AtomicCompletionFailureDoesNotLeaveFactOrCompletedReceipt()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var request=Request(f);var journal=new SqliteContentOpenJournal(f.Store);journal.TryStart(request);
        f.Sql("CREATE TRIGGER reject_fact BEFORE INSERT ON content_open_facts BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        var completion=new ContentOpenCompletion(request.ActivationId,request.Source.ProviderId,ContentOpenOutcome.Succeeded,ContentOpenEvidence.ShellAccepted,DateTimeOffset.UtcNow);
        Assert.ThrowsAny<Exception>(()=>journal.Complete(request,completion));
        Assert.Equal(0L,f.Sql("SELECT completed FROM content_open_requests"));Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_open_facts"));
        f.Sql("DROP TRIGGER reject_fact;");Assert.NotNull(journal.Complete(request,completion));
    }
    [Fact] public async Task RealViewLoadsBoundedRasterWithoutMutatingCatalog()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var catalog=f.Current.ContentCatalog;
        var worker=new Worker((r,c)=>ValueTask.FromResult(new LocalContentResponse(r.RequestId,ContentAvailability.Available,"",
            "sample.txt","Text document",new byte[16],2,2)));
        var source=new ReferenceContentRuntime(catalog,f.Main,1,worker);
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson,f.Main,out var layout,out _);
        var view=source.Read(layout!).Single();
        var key=new ContentAssetKey(f.Main,view.ItemId,1,1,1,1,view.AssetKey!,32,32,96,96,1);
        var bytes=await source.LoadAsync(key,CancellationToken.None);
        Assert.Equal("image/bgra32",bytes!.MediaType);Assert.Equal(2,bytes.PixelWidth);
        Assert.Equal(2,source.Version);Assert.Equal("Text document",source.Read(layout!).Single().TypeName);
        Assert.Equal(ContentCatalogCodec.Write(catalog),ContentCatalogCodec.Write(f.Current.ContentCatalog));
    }
    [Fact] public async Task MissingViewRetainsIdentityAndCanRecoverByRefresh()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));bool available=false;
        var worker=new Worker((r,c)=>ValueTask.FromResult(new LocalContentResponse(r.RequestId,available?ContentAvailability.Available:ContentAvailability.Missing,"")));
        var source=new ReferenceContentRuntime(f.Current.ContentCatalog,f.Main,1,worker);
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson,f.Main,out var layout,out _);
        var view=source.Read(layout!).Single();var key=new ContentAssetKey(f.Main,view.ItemId,1,1,1,1,view.AssetKey!,32,32,96,96,1);
        await source.LoadAsync(key,CancellationToken.None);Assert.False(source.Read(layout!).Single().IsAvailable);
        available=true;source.Refresh();await source.LoadAsync(source.RasterKey(key),CancellationToken.None);
        Assert.True(source.Read(layout!).Single().IsAvailable);Assert.Equal(view.ItemId,source.Read(layout!).Single().ItemId);
    }
    [Theory] [InlineData(-1,1,4)] [InlineData(1,1,8)] [InlineData(1024,1024,4)]
    public void UntrustedRasterShapeIsRejected(int width,int height,int bytes) => Assert.False(LocalContentProtocol.ValidPixels(new byte[bytes],width,height));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealOpeningAndRevealHaveDifferentFactSemantics(bool useViewAdapter)
    {
        using var f=new Fixture();var entry=Entry();entry=entry with {Item=entry.Item with {Capabilities=entry.Item.Capabilities|ContentCapability.Reveal}};
        f.Commit(f.AddCommand(f.Main,entry));int dispatches=0;
        var worker=new Worker((r,c)=>{dispatches++;return ValueTask.FromResult(new LocalContentResponse(r.RequestId,ContentAvailability.Available,"accepted",Evidence:ContentOpenEvidence.ShellAccepted));});
        var service=new ContentActionService(()=>f.Current.ContentCatalog,worker,new SqliteContentOpenJournal(f.Store));
        var actions=useViewAdapter ? new ReferenceContentActions(service,
            new ReferenceContentRuntime(f.Current.ContentCatalog,f.Main,1,worker)) : null;
        var relation=f.Current.ContentCatalog.PosterItems.Single();var target=new ContentActivationTarget(f.Main,relation.LayoutItemId,1,1,"fixture");
        var intent=new ContentOpenIntent(target,Guid.NewGuid(),ContentOpenOrigin.Enter);
        async ValueTask<ContentOperationFeedback> Open() => useViewAdapter
            ? await actions!.OpenAsync(intent,CancellationToken.None)
            : (await service.OpenAsync(intent,CancellationToken.None)).Feedback;
        Assert.True((await Open()).Succeeded);
        Assert.False((await Open()).Succeeded);Assert.Equal(1,dispatches);
        var revealed=useViewAdapter ? await actions!.RevealAsync(relation.LayoutItemId,CancellationToken.None)
            : (await service.RevealAsync(relation.LayoutItemId,CancellationToken.None)).Feedback;
        Assert.True(revealed.Succeeded);
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_open_facts"));Assert.Equal(2,dispatches);
    }
    private sealed class Worker(Func<LocalContentRequest,CancellationToken,ValueTask<LocalContentResponse>> run):ILocalContentWorker
    { public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request,CancellationToken cancellation)=>run(request,cancellation); }
}
