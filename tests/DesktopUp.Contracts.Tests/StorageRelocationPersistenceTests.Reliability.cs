using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed partial class StorageRelocationPersistenceTests
{
    [Fact] public void PendingMigrationBlocksLegacyProvisionBeforeAnyDirectoryDeclaration()
    {
        using var f=new F();string other=Path.Combine(f.DirectoryRoot,"not-claimed");Directory.CreateDirectory(other);
        var provisioner=new ManagedStorageProvisioner(f.Store);var preview=provisioner.Preview(other);
        var p=Preview(f);Journal(f).Begin(p,p.Fingerprint);
        Assert.Throws<IOException>(()=>provisioner.Confirm(preview,preview.Fingerprint));
        Assert.Empty(Directory.EnumerateFileSystemEntries(other));Assert.Single(f.Journal.StorageLocations());
        Journal(f).Cancel(p.Request.OperationId);provisioner.Confirm(preview,preview.Fingerprint);
        Assert.Equal(2,f.Journal.StorageLocations().Count);
    }
    [Fact] public void BundleOwnerCannotBeOfferedToOrOverwrittenByLegacyAdoption()
    {
        using var f=new F();var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);j.RecordTargets(p.Request.OperationId,v);j.Commit(p.Request.OperationId,v);
        var service=new ManagedStorageAdoption(f.Store);Assert.Empty(service.Candidates());
        Assert.Throws<IOException>(()=>service.Preview(f.Collection.Id));
        Assert.False(File.Exists(Path.Combine(p.Request.Destination.Locator,ManagedStorageAdoption.RootMarker)));
        var location=Assert.Single(f.Journal.StorageLocations());ManagedStorageProvisioner.ValidateRegistration(f.Store,location.Root,location.Collection);
    }
    [Fact] public void RegisteredRootWithoutCollectionCannotBeGuessedAsAnOwnedEmptyRoot()
    {
        using var f=new F();f.Sql("DELETE FROM managed_storage_collections");
        Assert.ThrowsAny<Exception>(()=>Preview(f));Assert.Empty(Journal(f).Read());Assert.True(Directory.Exists(f.Collection.StorageLocator));
    }
    [Fact] public void AdoptedLegacyOwnerIsPreservedWhileBundleTakesOverItsBinding()
    {
        using var f=new F();
        File.Move(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"),Path.Combine(f.DirectoryRoot,"old-root.keep"));
        File.Move(Path.Combine(f.Collection.StorageLocator!,".desktopup-collection-owner.json"),Path.Combine(f.DirectoryRoot,"old-child.keep"));
        var service=new ManagedStorageAdoption(f.Store);var adopt=service.Preview(f.Collection.Id);service.Confirm(adopt,adopt.Fingerprint);
        var p=Preview(f);Journal(f).Begin(p,p.Fingerprint);
        Assert.Throws<IOException>(()=>service.Confirm(adopt,adopt.Fingerprint));
        var v=new StubVerifier(StorageBundleOwnership.CreatePrepared(f.Store,p),p.Fingerprint);
        Journal(f).RecordTargets(p.Request.OperationId,v);Journal(f).Commit(p.Request.OperationId,v);
        Assert.True(File.Exists(Path.Combine(f.Root.Locator,ManagedStorageAdoption.RootMarker)));
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));
        var location=Assert.Single(f.Journal.StorageLocations());ManagedStorageProvisioner.ValidateRegistration(f.Store,location.Root,location.Collection);
    }
    [Fact] public void Schema18UpgradePreservesHistoryReferencesAndMarkersWithoutChoosingARoot()
    {
        using var f=new F();AddOwned(f);string catalog=ContentCatalogCodec.Write(f.State.ContentCatalog);long revision=f.State.Revision;
        byte[] marker=File.ReadAllBytes(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"));
        var sums=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=18 ORDER BY version");
        f.Sql(StorageSchemaFixture.Drop19+"DELETE FROM schema_migrations WHERE version=19;UPDATE state_head SET schema_version=18;PRAGMA user_version=18;");
        var result=f.Store.Initialize();Assert.True(result.Succeeded,result.Error);Assert.Equal(18,result.PreviousSchemaVersion);Assert.Equal(27,result.Head!.SchemaVersion);Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(sums,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=18 ORDER BY version"));
        Assert.Equal(catalog,ContentCatalogCodec.Write(f.State.ContentCatalog));Assert.Equal(revision,f.State.Revision);
        Assert.Equal(marker,File.ReadAllBytes(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json")));
        Assert.Null(Journal(f).ReadLocation().Selection.ActiveRootId);Assert.Empty(Journal(f).Read());
    }
    [Fact] public void FailedSchema19LeavesOriginalVersionAndNoPartiallyCreatedSelection()
    {
        using var f=new F();
        f.Sql(StorageSchemaFixture.Drop19+"DELETE FROM schema_migrations WHERE version=19;UPDATE state_head SET schema_version=18;PRAGMA user_version=18;CREATE TABLE storage_relocation_commits(conflict TEXT);");
        var result=f.Store.Initialize();Assert.False(result.Succeeded);Assert.Equal(18L,f.Sql("PRAGMA user_version"));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM sqlite_master WHERE name='storage_location_state'"));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM schema_migrations WHERE version=19"));
    }
    [Theory] [InlineData("IntentWritten")] [InlineData("TargetsRecorded")] [InlineData("IndexWritten")] [InlineData("ReceiptWritten")] [InlineData("Committed")]
    public void IndependentProcessExitPreservesAtomicIndexAndIndependentReceipt(string boundary)
    {
        using var f=new F();long revision=f.State.Revision;
        var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.TransferFixture.exe"))
        {UseShellExecute=false,CreateNoWindow=true};
        foreach(string arg in new[]{"--storage-relocation-crash",f.DirectoryRoot,boundary})start.ArgumentList.Add(arg);
        using var child=System.Diagnostics.Process.Start(start)!;Assert.True(child.WaitForExit(15000));Assert.Equal(75,child.ExitCode);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);var journal=new SqliteStorageRelocationJournal(reopened);var row=Assert.Single(journal.Read());
        bool committed=boundary=="Committed";Assert.Equal(committed,row.Commit is not null);
        Assert.Equal(revision+(committed?1:0),new SqliteStateCommandRepository(reopened).Read().Revision);
        Assert.Equal(committed?row.Plan.Request.Destination.Id:f.Root.Id,Assert.Single(new SqliteTransferJournal(reopened).StorageLocations()).Root.Id);
        Assert.True(Directory.Exists(f.Collection.StorageLocator));
        if(!committed){journal.Cancel(row.Plan.Request.OperationId);Assert.NotNull(journal.Get(row.Plan.Request.OperationId).Closure);}
    }
    [Fact] public void DurableReceiptWinsEvenIfPhaseFieldWasNotAdvanced()
    {
        using var f=new F();var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);j.RecordTargets(p.Request.OperationId,v);var receipt=j.Commit(p.Request.OperationId,v);
        f.Sql("UPDATE storage_relocations SET phase=3");Assert.Equal(receipt,j.Get(p.Request.OperationId).Commit);
        j.Cancel(p.Request.OperationId);Assert.Null(j.Get(p.Request.OperationId).Closure);
        Assert.Equal(receipt,j.Commit(p.Request.OperationId,new StubVerifier(null!,"unused"){Fail=true}));
    }
    [Theory] [InlineData("fingerprint")] [InlineData("root")] [InlineData("revision")]
    public void ForgedCommitReceiptIsNotTrusted(string change)
    {
        using var f=new F();var p=Preview(f);var v=Prepared(f,p);var j=Journal(f);j.RecordTargets(p.Request.OperationId,v);j.Commit(p.Request.OperationId,v);
        f.Sql(change switch {"fingerprint"=>"UPDATE storage_relocation_commits SET fingerprint='forged'","root"=>"UPDATE storage_relocation_commits SET active_root_id='00000000-0000-0000-0000-000000000000'",_=>"UPDATE storage_relocation_commits SET revision=revision+100"});
        Assert.ThrowsAny<Exception>(()=>j.Get(p.Request.OperationId));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void OpeningAndMigrationHaveExplicitMutualAdmission(bool openingFirst)
    {
        using var f=new F();AddOwned(f);var card=f.State.ContentCatalog.PosterItems.Single();var resolved=ContentRelationPolicy.Resolve(f.State.ContentCatalog,f.Main,card.LayoutItemId)!;
        var request=new ContentOpenRequest(Guid.NewGuid(),resolved.Witness,resolved.Item.Source,resolved.Item.Target,ContentOpenOrigin.DoubleClick);
        var opens=new SqliteContentOpenJournal(f.Store);var plan=Preview(f);
        if(openingFirst)
        {
            Assert.Equal(ContentOpenRequestAdmission.Ready,opens.TryStart(request));Assert.Throws<IOException>(()=>Journal(f).Begin(plan,plan.Fingerprint));
            opens.RetireInterruptedRequests();Journal(f).Begin(plan,plan.Fingerprint);
        }
        else
        {
            Journal(f).Begin(plan,plan.Fingerprint);Assert.Equal(ContentOpenRequestAdmission.Busy,opens.TryStart(request));
            Journal(f).Cancel(plan.Request.OperationId);Assert.Equal(ContentOpenRequestAdmission.Ready,opens.TryStart(request));
        }
    }
    [Fact] public void MultiPosterUniqueItemsAndCustomIconSurviveOneRevisionSwitch()
    {
        using var f=new F();AddOwned(f);var state=f.State;var item=Assert.Single(state.ContentCatalog.Items);
        var other=PosterConfigurationFactory.NewBasicBox("shared","DISPLAY1") with {PosterId=Guid.NewGuid().ToString(),ThemeInstanceId=Guid.NewGuid().ToString()};
        f.Repository.Execute(new("share-create","create",state.Revision,StateReadSet.ForCatalog(state.Catalog.CatalogVersion)),_=>new(){Creates=[other]});state=f.State;
        var reads=StateReadSet.ForPoster(other.PosterId,state.Catalog.Posters.Single(x=>x.Poster.PosterId==other.PosterId).EntityVersion) with {ContentVersion=state.ContentVersion};
        var secondSource=f.File("second-owned.txt");
        var add=new ContentReferenceCommand("share-add",StateCommandSource.ControlCenter,state.Revision,reads,other.PosterId,
            [new(LocalContentReferenceProbe.Inspect(secondSource).Item!,ContentReferenceChoice.CreateIndependent)],true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new(add.RequestId,"share",state.Revision,reads),s=>ContentReferencePolicy.Prepare(add,s)).Status);state=f.State;
        var ingress=DirectTransferPreparation.Incoming(state,other.PosterId,[secondSource],new(f.Root,f.Collection));
        f.Journal.Begin(ingress.Plan,ingress.Files,ingress.Plan.Fingerprint,f.Collection.Id);f.Executor.Execute(ingress.Plan.Request.OperationId);state=f.State;
        var card=state.ContentCatalog.PosterItems.Single(x=>x.PosterId==other.PosterId);
        reads=StateReadSet.ForPoster(other.PosterId,state.Catalog.Posters.Single(x=>x.Poster.PosterId==other.PosterId).EntityVersion) with {ContentVersion=state.ContentVersion};
        var icon=new ContentIconCommand("icon-shared",StateCommandSource.ControlCenter,state.Revision,reads,ContentRelationPolicy.Resolve(state.ContentCatalog,other.PosterId,card.LayoutItemId)!.Witness,ContentIconPersistenceTests.Icon(),true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new(icon.RequestId,"icon",state.Revision,reads),s=>ContentIconPolicy.Prepare(icon,s)).Status);
        var before=f.State;var p=Preview(f);Assert.Equal(2,p.AffectedPosterIds.Count);var v=Prepared(f,p);Journal(f).RecordTargets(p.Request.OperationId,v);Journal(f).Commit(p.Request.OperationId,v);
        Assert.Equal(before.Revision+1,f.State.Revision);Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.Equal(before.ContentCatalog.Memberships,f.State.ContentCatalog.Memberships);Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        foreach(var poster in before.Catalog.Posters)Assert.Equal(poster.Poster,f.State.Catalog.Posters.Single(x=>x.Poster.PosterId==poster.Poster.PosterId).Poster);
    }
    [Fact] public void UnknownSelectionAndMissingVersionTriggerFailClosed()
    {
        using var f=new F();f.Sql("UPDATE storage_location_state SET format_version=77");Assert.False(f.Store.Initialize().Succeeded);
        f.Sql("UPDATE storage_location_state SET format_version=1;DROP TRIGGER storage_roots_update");Assert.False(f.Store.Initialize().Succeeded);
    }
    [Fact] public void BundleDeclarationCannotBePreparedWithoutDurableIntentOrOverForeignFiles()
    {
        using var f=new F();var p=Preview(f);Assert.ThrowsAny<Exception>(()=>StorageBundleOwnership.CreatePrepared(f.Store,p));
        Journal(f).Begin(p,p.Fingerprint);string foreign=Path.Combine(p.Request.Destination.Locator,"keep.txt");File.WriteAllText(foreign,"keep");
        Assert.Throws<IOException>(()=>StorageBundleOwnership.CreatePrepared(f.Store,p));Assert.Equal("keep",File.ReadAllText(foreign));
        Assert.False(File.Exists(Path.Combine(p.Request.Destination.Locator,StorageBundleOwnership.RootMarker)));
    }
}
