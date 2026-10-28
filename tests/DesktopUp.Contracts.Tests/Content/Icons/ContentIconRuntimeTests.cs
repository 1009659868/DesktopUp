using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;
using static DesktopUp.Contracts.Tests.ContentIconPersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentIconRuntimeTests
{
    private sealed class Worker : ILocalContentWorker
    {
        public int Calls;
        public List<LocalContentAction> Actions = [];
        public ContentAvailability Availability = ContentAvailability.Available;
        public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest r,CancellationToken c)
        {
            Calls++; Actions.Add(r.Action);
            if (r.Action == LocalContentAction.Inspect)
                return ValueTask.FromResult(new LocalContentResponse(r.RequestId,Availability,""));
            return ValueTask.FromResult(new LocalContentResponse(r.RequestId,ContentAvailability.Available,"",
                Pixels:[0,0,0,255],Width:1,Height:1));
        }
    }
    private sealed class DelayedStore : IContentIconStore
    {
        public TaskCompletionSource<ContentIconAsset?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<ContentIconAsset?> ReadAsync(string id,CancellationToken c)=>new(Completion.Task);
    }
    private static (ReferenceContentRuntime Runtime,ContentAssetKey Key) Source(Fixture f,Worker worker,IContentIconStore? store)
    {
        var runtime = new ReferenceContentRuntime(f.Current.ContentCatalog,f.Main,f.Current.ContentVersion,worker,store);
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson,f.Main,out var layout,out _);
        var view=runtime.Read(layout!).Single();
        return (runtime,runtime.RasterKey(new(f.Main,view.ItemId,1,1,1,1,view.AssetKey!,128,128,96,96,1)));
    }
    [Fact] public async Task OverrideLoadsFromOwnedStoreAndRestoreUsesOriginalProviderAgain()
    {
        using var f=new Fixture();var entry=Entry();f.Commit(f.AddCommand(f.Main,entry));var worker=new Worker();
        Assert.True(f.Coordinator.Execute(Command(f,f.Main,Icon())).Succeeded);
        var custom=Source(f,worker,new SqliteContentIconStore(f.Store));
        var payload=await custom.Runtime.LoadAsync(custom.Key,CancellationToken.None);
        Assert.Equal(Icon().Pixels,payload!.Bytes.ToArray());Assert.Equal([LocalContentAction.Inspect],worker.Actions);
        f.Commit(f.AddCommand(f.Main,Entry("repaired.txt")) with {RepairContentId=entry.Item.Id});
        Assert.Equal(Icon().Id,f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
        Assert.True(f.Coordinator.Execute(Command(f,f.Main,null)).Succeeded);
        var restored=Source(f,worker,new SqliteContentIconStore(f.Store));
        Assert.Null(await restored.Runtime.LoadAsync(custom.Key,CancellationToken.None));
        Assert.NotNull(await restored.Runtime.LoadAsync(restored.Key,CancellationToken.None));
        Assert.Equal([LocalContentAction.Inspect,LocalContentAction.Icon],worker.Actions);
    }
    [Fact] public async Task CustomPixelsSurviveMissingAndRecoveredSourceWithoutFreezingAvailability()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));f.Coordinator.Execute(Command(f,f.Main,Icon()));
        var worker=new Worker { Availability=ContentAvailability.Missing };
        var s=Source(f,worker,new SqliteContentIconStore(f.Store));
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson,f.Main,out var layout,out _);
        Assert.Equal(Icon().Pixels,(await s.Runtime.LoadAsync(s.Runtime.RasterKey(s.Key),CancellationToken.None))!.Bytes.ToArray());
        Assert.False(s.Runtime.Read(layout!).Single().IsAvailable);
        worker.Availability=ContentAvailability.Available;s.Runtime.Refresh();
        Assert.Equal(Icon().Pixels,(await s.Runtime.LoadAsync(s.Runtime.RasterKey(s.Key),CancellationToken.None))!.Bytes.ToArray());
        Assert.True(s.Runtime.Read(layout!).Single().IsAvailable);
        Assert.All(worker.Actions,a=>Assert.Equal(LocalContentAction.Inspect,a));
        Assert.Equal(Icon().Id,f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
    }
    [Fact] public async Task MissingCustomStoreNeverSilentlyFallsBackToShell()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));f.Coordinator.Execute(Command(f,f.Main,Icon()));
        var worker=new Worker();var s=Source(f,worker,null);
        Assert.Null(await s.Runtime.LoadAsync(s.Key,CancellationToken.None));Assert.Equal(0,worker.Calls);
    }
    [Fact] public async Task LateCustomCompletionCannotOverwriteNewIconOrReenterAfterExit()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));f.Coordinator.Execute(Command(f,f.Main,Icon(1)));
        var store=new DelayedStore();var worker=new Worker();var old=Source(f,worker,store);
        using var cache=new ContentAssetCache(old.Runtime);cache.SetVisible([old.Key]);
        var pending=cache.RequestAsync(old.Key);
        f.Coordinator.Execute(Command(f,f.Main,Icon(2)));var next=Source(f,worker,new SqliteContentIconStore(f.Store));
        Assert.NotEqual(old.Key.SourceKey,next.Key.SourceKey);cache.SetVisible([next.Key]);
        Assert.Equal(ContentAssetStatus.Stale,(await pending).Status);
        store.Completion.SetResult(Icon(1));
        for(int i=0;i<100&&cache.RunningLoads!=0;i++)await Task.Delay(10);
        Assert.Equal(0,cache.RunningLoads);Assert.False(cache.TryGet(old.Key,out _));Assert.False(cache.TryGet(next.Key,out _));
        Assert.Null(await next.Runtime.LoadAsync(old.Key,CancellationToken.None));Assert.Equal(0,worker.Calls);
        cache.Dispose();Assert.Equal(0,cache.Count);
    }
    [Fact] public async Task CancellationAfterStoreReadStillPreventsDelivery()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));f.Coordinator.Execute(Command(f,f.Main,Icon()));
        var store=new DelayedStore();var s=Source(f,new Worker(),store);using var c=new CancellationTokenSource();
        var pending=s.Runtime.LoadAsync(s.Key,c.Token).AsTask();c.Cancel();store.Completion.SetResult(Icon());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
    }
    [Theory] [InlineData("target")] [InlineData("layout")] [InlineData("readset")] [InlineData("membership")]
    public void TransactionBarrierRejectsChangesRidingAlongWithIconEdit(string kind)
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));
        var state=new SqliteStateCommandRepository(f.Store).Read();var command=Command(f,f.Main,Icon());
        var plan=ContentIconPolicy.Prepare(command,state);var reads=command.ReadSet;
        if(kind=="target")plan=plan with {ContentCatalog=plan.ContentCatalog! with {Items=plan.ContentCatalog!.Items.Select(x=>x with {DisplayName="changed"}).ToArray()}};
        if(kind=="layout")plan=plan with {Updates=[plan.Updates[0] with {Title="changed"}]};
        if(kind=="readset")reads=reads with {ContentVersion=null};
        if(kind=="membership")plan=plan with {ContentCatalog=plan.ContentCatalog! with {Memberships=[]}};
        Assert.False(ContentIconPolicy.ValidMutation(state,reads,plan));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void OverBudgetStoreFailsClosedWithoutDeletingEvidence(bool bytes)
    {
        using var f=new Fixture();int count=bytes?257:513;int size=bytes?256:1;
        // Owned corruption fixture verifies admission limits before any BLOB allocation/hash walk.
        f.Sql($"WITH RECURSIVE n(v) AS (VALUES(1) UNION ALL SELECT v+1 FROM n WHERE v<{count}) " +
            $"INSERT INTO content_icon_assets SELECT 'icon-v1-' || printf('%064x',v),1,{size},{size},zeroblob({size*size*4}) FROM n");
        var result=f.Store.Initialize();Assert.False(result.Succeeded);
        Assert.Contains("budget",result.Error!,StringComparison.OrdinalIgnoreCase);
        Assert.Equal((long)count,f.Sql("SELECT count(*) FROM content_icon_assets"));
    }
}
