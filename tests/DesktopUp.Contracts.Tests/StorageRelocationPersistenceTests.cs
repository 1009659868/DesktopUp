using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed partial class StorageRelocationPersistenceTests
{
    // This proves the DB/ownership boundary, NOT copy correctness. A real leased copy verifier
    // must replace this internal adapter in D2-03 before any production caller is admitted.
    private sealed class StubVerifier(StorageBundleClaim claim,string fingerprint):IStorageRelocationVerifier
    {
        public string Witness="isolated-persistence-stub";
        public bool Fail;
        public StorageRelocationProofLease Acquire(StorageRelocationPlan plan)
        {if(Fail)throw new IOException("injected missing file proof");return new Lease(new(fingerprint,Witness,claim));}
        private sealed class Lease(StorageRelocationTargets targets):StorageRelocationProofLease
        {public override StorageRelocationTargets Targets=>targets;
         public override string ObjectIdentity(string path)=>"persistence-stub-"+path;
         public override void Dispose(){} }
    }
    private static SqliteStorageRelocationJournal Journal(F f)=>new(f.Store);
    private static StorageRelocationPlan Preview(F f)
    {string target=Path.Combine(f.DirectoryRoot,"destination-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);return Journal(f).Preview(target);}
    private static StubVerifier Prepared(F f,StorageRelocationPlan p)
    {Journal(f).Begin(p,p.Fingerprint);return new(StorageBundleOwnership.CreatePrepared(f.Store,p),p.Fingerprint);}
    private static void AddOwned(F f)
    {var staged=f.Stage(f.File("collected.txt"));f.Executor.Execute(staged.Plan.Request.OperationId);}

    [Fact] public void SelectionIsExplicitVersionedAndSurvivesColdOpen()
    {
        using var f=new F();var j=Journal(f);var start=j.ReadLocation();long revision=f.State.Revision;
        Assert.Null(start.Selection.ActiveRootId);Assert.Empty(j.Read());
        j.Select(f.Root.Id,start);var saved=j.ReadLocation();Assert.Equal(f.Root.Id,saved.Selection.ActiveRootId);
        Assert.Equal(start.Selection.Version+1,saved.Selection.Version);Assert.Equal(revision+1,f.State.Revision);
        Assert.Throws<IOException>(()=>j.Select(f.Root.Id,start));j.Select(f.Root.Id,saved);Assert.Equal(revision+1,f.State.Revision);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(saved,new SqliteStorageRelocationJournal(reopened).ReadLocation());
    }
    [Fact] public void PreviewDoesNotWriteAndOwnershipIsNative()
    {
        using var f=new F();var before=StateRequestFingerprint.Compute(f.State);var p=Preview(f);
        Assert.Equal(before,StateRequestFingerprint.Compute(f.State));Assert.Empty(Journal(f).Read());Assert.Empty(Directory.EnumerateFileSystemEntries(p.Request.Destination.Locator));
        Assert.Throws<IOException>(()=>Journal(f).Begin(p,"wrong"));Assert.Empty(Journal(f).Read());
        File.AppendAllText(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"),"broken");
        Assert.ThrowsAny<Exception>(()=>Journal(f).Begin(p,p.Fingerprint));Assert.Empty(Journal(f).Read());
    }
    [Theory] [InlineData("file")] [InlineData("root-set")] [InlineData("selection")] [InlineData("content")] [InlineData("occupied-target")]
    public void ChangedPreviewCannotCreateAnIntent(string change)
    {
        using var f=new F();var p=Preview(f);
        if(change=="file")File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"external.txt"),"external");
        if(change=="root-set")f.Sql("UPDATE storage_location_state SET root_set_version=root_set_version+1");
        if(change=="selection")Journal(f).Select(f.Root.Id,Journal(f).ReadLocation());
        if(change=="content")f.AddReference(LocalContentReferenceProbe.Inspect(f.File("reference.txt")).Item!);
        if(change=="occupied-target")File.WriteAllText(Path.Combine(p.Request.Destination.Locator,"foreign.txt"),"do not touch");
        Assert.Throws<IOException>(()=>Journal(f).Begin(p,p.Fingerprint));Assert.Empty(Journal(f).Read());
    }
    [Fact] public void IntentReplayIsIdempotentCancelReleasesEditsAndDoesNotMoveFiles()
    {
        using var f=new F();AddOwned(f);var p=Preview(f);var j=Journal(f);var before=f.State;
        j.Begin(p,p.Fingerprint);j.Begin(p,p.Fingerprint);Assert.Single(j.Read());Assert.Equal(before.Revision,f.State.Revision);
        Assert.Throws<IOException>(()=>j.Begin(Preview(f),"different"));
        Assert.Throws<IOException>(()=>f.Stage(f.File("blocked.txt")));
        Assert.Throws<IOException>(()=>j.Select(f.Root.Id,j.ReadLocation()));
        var s=f.State;var result=f.Repository.Execute(new("owned-edit","owned-edit",s.Revision,StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion)),
            _=>new(){Updates=[s.Catalog.Posters.Single().Poster with {Title="blocked"}]});Assert.Equal(StateCommandStatus.Conflict,result.Status);
        j.Cancel(p.Request.OperationId);j.Cancel(p.Request.OperationId);Assert.NotNull(j.Get(p.Request.OperationId).Closure);
        Assert.Throws<IOException>(()=>j.Begin(p,p.Fingerprint));Assert.True(File.Exists(before.ContentCatalog.Items[0].Target.Locator));
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("after-cancel.txt")).Item!);
    }
    [Fact] public void UnrelatedPosterCanChangeWithoutInvalidatingMigration()
    {
        using var f=new F();AddOwned(f);var s=f.State;var other=PosterConfigurationFactory.NewBasicBox("other","DISPLAY1") with {PosterId=Guid.NewGuid().ToString(),ThemeInstanceId=Guid.NewGuid().ToString()};
        f.Repository.Execute(new("other-create","other",s.Revision,StateReadSet.ForCatalog(s.Catalog.CatalogVersion)),_=>new(){Creates=[other]});
        var p=Preview(f);var v=Prepared(f,p);s=f.State;
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("other-edit","edit",s.Revision,StateReadSet.ForPoster(other.PosterId,s.Catalog.Posters.Single(x=>x.Poster.PosterId==other.PosterId).EntityVersion)),_=>new(){Updates=[other with {Title="still usable"}]}).Status);
        Journal(f).RecordTargets(p.Request.OperationId,v);Journal(f).Commit(p.Request.OperationId,v);
        Assert.Equal("still usable",f.State.Catalog.Posters.Single(x=>x.Poster.PosterId==other.PosterId).Poster.Title);
    }
    [Fact] public void OneCommitPreservesCardLayoutIconAndRelationsAndRetainsSources()
    {
        using var f=new F();AddOwned(f);var before=f.State;var original=Assert.Single(before.ContentCatalog.Items);
        var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);
        Assert.Throws<IOException>(()=>j.Commit(p.Request.OperationId,v));
        Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(f.Store,p.Request.Destination,before.ContentCatalog.Collections.Single(x=>x.Kind==ContentCollectionKind.Managed) with {StorageLocator=v.Acquire(p).Targets.Claim.Collections[0].Locator}));
        j.RecordTargets(p.Request.OperationId,v);var receipt=j.Commit(p.Request.OperationId,v);var after=f.State;
        Assert.Equal(before.Revision+1,after.Revision);Assert.Equal(before.ContentVersion+1,after.ContentVersion);
        Assert.Equal(before.ContentCatalog.Memberships,after.ContentCatalog.Memberships);Assert.Equal(before.ContentCatalog.PosterItems,after.ContentCatalog.PosterItems);
        Assert.Equal(before.ContentCatalog.Bindings,after.ContentCatalog.Bindings);
        Assert.Equal(before.Catalog.Posters[0].Poster.ContentLayoutJson,after.Catalog.Posters[0].Poster.ContentLayoutJson);
        var item=Assert.Single(after.ContentCatalog.Items);Assert.Equal(original.Id,item.Id);Assert.Equal(original.Version+1,item.Version);
        Assert.StartsWith(p.Request.Destination.Locator,item.Target.Locator);Assert.True(File.Exists(original.Target.Locator));
        Assert.Equal(receipt,j.Commit(p.Request.OperationId,new StubVerifier(null!,"bad"){Fail=true}));Assert.Equal(after.Revision,f.State.Revision);
        j.Cancel(p.Request.OperationId);Assert.Null(j.Get(p.Request.OperationId).Closure);
        var location=Assert.Single(f.Journal.StorageLocations());ManagedStorageProvisioner.ValidateRegistration(f.Store,location.Root,location.Collection);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(receipt,new SqliteStorageRelocationJournal(reopened).Get(p.Request.OperationId).Commit);
    }
    [Theory] [InlineData((int)StorageRelocationCheckpoint.IndexWritten)] [InlineData((int)StorageRelocationCheckpoint.ReceiptWritten)]
    public void TransactionFailureRollsBackEveryTableAndRevision(int point)
    {
        using var f=new F();AddOwned(f);var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);j.RecordTargets(p.Request.OperationId,v);
        var before=StateRequestFingerprint.Compute(f.State);var location=j.ReadLocation();
        Assert.Throws<IOException>(()=>j.Commit(p.Request.OperationId,v,at=>{if((int)at==point)throw new IOException("injected SQL failure");}));
        Assert.Equal(before,StateRequestFingerprint.Compute(f.State));Assert.Equal(location,j.ReadLocation());Assert.Null(j.Get(p.Request.OperationId).Commit);
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM storage_relocation_commits"));Assert.Equal(f.Root,Assert.Single(f.Journal.StorageLocations()).Root);
        j.Commit(p.Request.OperationId,v);Assert.NotNull(j.Get(p.Request.OperationId).Commit);
    }
    [Theory] [InlineData("selection")] [InlineData("roots")] [InlineData("content")] [InlineData("poster")] [InlineData("source")] [InlineData("target-proof")]
    public void FullCasAndProofAreRecheckedBeforeSwitch(string change)
    {
        using var f=new F();AddOwned(f);var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);j.RecordTargets(p.Request.OperationId,v);long revision=f.State.Revision;
        if(change=="selection")f.Sql("UPDATE storage_location_state SET version=version+1");
        if(change=="roots")f.Sql("UPDATE managed_storage_roots SET body_json=body_json");
        if(change=="content")f.Sql("UPDATE content_head SET version=version+1");
        if(change=="poster")f.Sql("UPDATE posters SET entity_version=entity_version+1");
        if(change=="source")File.AppendAllText(f.State.ContentCatalog.Items[0].Target.Locator,"changed");
        if(change=="target-proof")v.Witness="new witness";
        Assert.Throws<IOException>(()=>j.Commit(p.Request.OperationId,v));Assert.Null(j.Get(p.Request.OperationId).Commit);Assert.Equal(revision,f.State.Revision);
    }
    [Fact] public void RestartCannotReuseAnOldRuntimeToStartOrCommitButCanCancel()
    {
        using var f=new F();var p=Preview(f);var v=Prepared(f,p);Journal(f).RecordTargets(p.Request.OperationId,v);
        using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);var j=new SqliteStorageRelocationJournal(restarted);
        Assert.Throws<IOException>(()=>j.Commit(p.Request.OperationId,v));j.Cancel(p.Request.OperationId);Assert.NotNull(j.Get(p.Request.OperationId).Closure);
    }
    [Theory] [InlineData("intent")] [InlineData("fingerprint")] [InlineData("targets")] [InlineData("owner")] [InlineData("child")] [InlineData("phase-without-receipt")]
    public void UnknownOrForgedRecordsNeverAuthorizeSwitch(string change)
    {
        using var f=new F();var p=Preview(f);var v=Prepared(f,p);Journal(f).RecordTargets(p.Request.OperationId,v);
        if(change=="intent")f.Sql("UPDATE storage_relocations SET body_json=json_set(body_json,'$.FormatVersion',77)");
        if(change=="fingerprint")f.Sql("UPDATE storage_relocations SET fingerprint='forged'");
        if(change=="targets")f.Sql("UPDATE storage_relocations SET targets_json=json_set(targets_json,'$.PlanFingerprint','forged')");
        if(change=="owner")File.AppendAllText(Path.Combine(p.Request.Destination.Locator,StorageBundleOwnership.RootMarker),"broken");
        if(change=="child")File.AppendAllText(Path.Combine(v.Acquire(p).Targets.Claim.Collections[0].Locator,StorageBundleOwnership.ChildMarker),"broken");
        if(change=="phase-without-receipt")f.Sql("UPDATE storage_relocations SET phase=4");
        Assert.ThrowsAny<Exception>(()=>Journal(f).Commit(p.Request.OperationId,v));Assert.Equal(0L,f.Sql("SELECT count(*) FROM storage_relocation_commits"));
    }
    [Fact] public void CopiedProfileCannotAdoptSelectionOrIntent()
    {
        using var f=new F();var p=Preview(f);Journal(f).Begin(p,p.Fingerprint);
        string copy=Path.Combine(f.DirectoryRoot,"copied-profile");var paths=new UserDataPaths(copy);Directory.CreateDirectory(paths.State);
        f.Sql("PRAGMA wal_checkpoint(TRUNCATE)");File.Copy(Path.Combine(f.Paths.State,"desktopup.sqlite3"),Path.Combine(paths.State,"desktopup.sqlite3"));
        using var store=new SqliteStateStore(paths);Assert.True(store.Initialize().Succeeded);
        Assert.Throws<IOException>(()=>new SqliteStorageRelocationJournal(store).ReadLocation());
        Assert.Throws<IOException>(()=>new SqliteStorageRelocationJournal(store).Cancel(p.Request.OperationId));
    }
    [Fact] public void ExecutorLeaseAndPendingTransfersBlockMigration()
    {
        using var f=new F();var p=Preview(f);using(var held=f.Journal.AcquireExecutor())Assert.Throws<IOException>(()=>Journal(f).Begin(p,p.Fingerprint));
        f.Stage(f.File("pending.txt"));Assert.Throws<IOException>(()=>Journal(f).Begin(p,p.Fingerprint));Assert.Empty(Journal(f).Read());
    }
    [Fact] public void MultiRootAndEmptyCollectionsMoveTogetherWithoutRecreatingIds()
    {
        using var f=new F();string other=Path.Combine(f.DirectoryRoot,"other-root");Directory.CreateDirectory(other);
        var provisioner=new ManagedStorageProvisioner(f.Store);var preview=provisioner.Preview(other);var second=provisioner.Confirm(preview,preview.Fingerprint);
        var p=Preview(f);Assert.Equal(2,p.Roots.Count);var v=Prepared(f,p);Journal(f).RecordTargets(p.Request.OperationId,v);Journal(f).Commit(p.Request.OperationId,v);
        var locations=f.Journal.StorageLocations();Assert.Equal(2,locations.Count);Assert.Single(locations.Select(x=>x.Root.Id).Distinct());
        Assert.Contains(locations,x=>x.Collection.Id==f.Collection.Id);Assert.Contains(locations,x=>x.Collection.Id==second.Collection.Id);
        foreach(var location in locations)ManagedStorageProvisioner.ValidateRegistration(f.Store,location.Root,location.Collection);
        Assert.True(Directory.Exists(second.Collection.StorageLocator));Assert.True(Directory.Exists(f.Collection.StorageLocator));
    }
}
