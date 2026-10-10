using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferPersistenceTests
{
    [Fact] public void SameVolumeCollectAndTakeBackKeepsIdsAndOneRevisionPerItem()
    {
        using var f=new Fixture();string source=f.File("source.txt");var p=f.Stage(source);
        long revision=f.State.Revision;
        var first=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId));
        Assert.True(first.Phase==DurableTransferPhase.IndexCommitted,first.Error);
        Assert.False(File.Exists(source));Assert.Equal("owned-content",File.ReadAllText(p.Files[0].Destination));
        Assert.Equal(revision+1,f.State.Revision);var item=Assert.Single(f.State.ContentCatalog.Items);
        Assert.Equal(p.Plan.Entries[0].Entry.Item.Id,item.Id);
        Assert.Equal(2,f.State.ContentCatalog.Memberships.Count);
        string destination=Path.Combine(f.Source,"returned.txt");
        var card=Assert.Single(f.State.ContentCatalog.PosterItems);
        var outgoing=LocalTransferPreview.Prepare(f.State,f.Main,ContentDropRoute.ToDesktop,[(item,destination,card.Id)],isolatedDestination:true);
        f.Journal.Begin(outgoing.Plan,outgoing.Files,outgoing.Plan.Fingerprint);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(outgoing.Plan.Request.OperationId)).Phase);
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal(item.Id,Assert.Single(f.State.ContentCatalog.Items).Id);
        Assert.Single(f.State.ContentCatalog.Memberships);
        Assert.Equal("owned-content",File.ReadAllText(destination));
        f.Executor.Recover();Assert.Equal(revision+2,f.State.Revision);
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint,f.Collection.Id);
        Assert.Equal(revision+2,f.State.Revision);
    }
    [Theory]
    [InlineData(TransferExecutionCheckpoint.Claimed,false)]
    [InlineData(TransferExecutionCheckpoint.BeforeMove,false)]
    [InlineData(TransferExecutionCheckpoint.AfterMove,true)]
    [InlineData(TransferExecutionCheckpoint.FileRecorded,true)]
    [InlineData(TransferExecutionCheckpoint.BeforeIndex,true)]
    [InlineData(TransferExecutionCheckpoint.IndexCommitted,true)]
    public void AbruptBoundaryLossRecoversFromDurableEvidence(TransferExecutionCheckpoint point,bool moved)
    {
        using var f=new Fixture();var p=f.Stage(f.File("fault.txt"));long revision=f.State.Revision;
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==point)throw new TransferInterruptionException("lost executor");}));
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        var result=new LocalTransferExecutor(new(reopened)).Recover();
        Assert.Equal(moved?DurableTransferPhase.IndexCommitted:DurableTransferPhase.Review,Assert.Single(result).Phase);
        Assert.Equal(moved?revision+1:revision,f.State.Revision);
        Assert.Equal(!moved,File.Exists(p.Files[0].Source));Assert.Equal(moved,File.Exists(p.Files[0].Destination));
    }
    [Fact] public void ChangedSourceBeforeExecutionDoesNotMoveOrRemoveReference()
    {
        using var f=new Fixture();var p=f.Stage(f.File("changed.txt"));File.WriteAllText(p.Files[0].Source,"new bytes");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.Equal("new bytes",File.ReadAllText(p.Files[0].Source));Assert.False(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void NewDestinationIsNeverOverwritten()
    {
        using var f=new Fixture();var p=f.Stage(f.File("collision.txt"));File.WriteAllText(p.Files[0].Destination,"other owner");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.Equal("other owner",File.ReadAllText(p.Files[0].Destination));Assert.True(File.Exists(p.Files[0].Source));
    }
    [Fact] public void CancellationBeforeExecutionAndRepeatedExecuteDoNotTouchDisk()
    {
        using var f=new Fixture();var p=f.Stage(f.File("cancel.txt"));using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Equal(DurableTransferPhase.Cancelled,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId,cancel.Token)).Phase);
        f.Executor.Execute(p.Plan.Request.OperationId);f.Executor.Recover();Assert.True(File.Exists(p.Files[0].Source));
    }
    [Fact] public void PartialBatchKeepsSuccessAndRetainsFailedItem()
    {
        using var f=new Fixture();var p=f.Stage(f.File("one.txt"),f.File("two.txt"));
        File.WriteAllText(p.Files[1].Destination,"collision");
        var rows=f.Executor.Execute(p.Plan.Request.OperationId).OrderBy(i=>i.Index).ToArray();
        Assert.Equal(DurableTransferPhase.IndexCommitted,rows[0].Phase);Assert.Equal(DurableTransferPhase.Review,rows[1].Phase);
        Assert.Single(f.State.ContentCatalog.PosterItems);Assert.True(File.Exists(p.Files[1].Source));
    }
    [Fact] public void OverlappingPendingOperationsAreRejected()
    {
        using var f=new Fixture();string path=f.File("owned.txt");f.Stage(path);
        Assert.Throws<IOException>(()=>f.Stage(path));Assert.True(File.Exists(path));
    }
    [Fact] public void IndexFaultNeverRollsFileBackAndCanRecover()
    {
        using var f=new Fixture();var p=f.Stage(f.File("dbfault.txt"));f.Sql("CREATE TRIGGER transfer_fail BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.False(File.Exists(p.Files[0].Source));Assert.True(File.Exists(p.Files[0].Destination));Assert.Empty(f.State.ContentCatalog.Items);
        f.Sql("DROP TRIGGER transfer_fail;");
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Recover()).Phase);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void AmbiguousRecoveryPreservesReplacementOrBothCopies(bool replaceDestination)
    {
        using var f=new Fixture();var p=f.Stage(f.File("ambiguous.txt"));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("loss");}));
        if(replaceDestination){File.Move(p.Files[0].Destination,p.Files[0].Destination+".kept");File.WriteAllText(p.Files[0].Destination,"different");}
        else File.WriteAllText(p.Files[0].Source,"new source");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Recover()).Phase);
        Assert.Empty(f.State.ContentCatalog.Items);Assert.True(File.Exists(p.Files[0].Destination));
        if(!replaceDestination)Assert.Equal("new source",File.ReadAllText(p.Files[0].Source));
    }
    [Fact] public void FolderMoveUpdatesKnownDescendantReferenceWithoutChangingCardIdentity()
    {
        using var f=new Fixture();string folder=Path.Combine(f.Source,"folder");Directory.CreateDirectory(folder);
        string child=Path.Combine(folder,"child.txt");File.WriteAllText(child,"child");
        var childItem=LocalContentReferenceProbe.Inspect(child).Item!;f.AddReference(childItem);
        var before=f.State.ContentCatalog.PosterItems.Single();string? layout=f.State.Catalog.Posters.Single().Poster.ContentLayoutJson;
        var p=f.Stage(folder);Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        var relocated=f.State.ContentCatalog.Items.Single(i=>i.Id==childItem.Id);
        Assert.Equal(Path.Combine(p.Files[0].Destination,"child.txt"),relocated.Target.Locator);
        Assert.Contains(before,f.State.ContentCatalog.PosterItems);
        Assert.Equal("child",File.ReadAllText(relocated.Target.Locator));Assert.NotNull(layout);
    }
    [Fact] public void LockedFileFailsClosedAndRetainsOriginal()
    {
        using var f=new Fixture();var p=f.Stage(f.File("locked.txt"));
        using var locked=new FileStream(p.Files[0].Source,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.True(File.Exists(p.Files[0].Source));Assert.False(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void RegistryIsUnboundAndRepeatedRegistrationDoesNotRebindReferences()
    {
        using var f=new Fixture();var state=f.State;
        f.Journal.Register(f.Root,f.Collection,new(new Dictionary<string,long>(),ContentVersion:state.ContentVersion));
        Assert.Single(f.State.ContentCatalog.Collections);Assert.Empty(f.State.ContentCatalog.Bindings);
        Assert.Equal(state.Revision,f.State.Revision);Assert.Equal(18L,f.Sql("PRAGMA user_version"));
    }
    [Fact] public void StaleStateBeforeExecutionRequiresNewPreview()
    {
        using var f=new Fixture();var p=f.Stage(f.File("stale.txt"));
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("other.txt")).Item!);
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.True(File.Exists(p.Files[0].Source));
    }
    [Fact] public void BackupRewindIsBlockedAfterAnIntentExists()
    {
        using var f=new Fixture();var backup=f.Store.CreateBackup();Assert.True(backup.Succeeded,backup.Error);
        f.Stage(f.File("backup.txt"));f.Store.Dispose();
        var result=new StateBackupRecovery(f.Paths,()=>true).Restore(backup.FileName!);
        Assert.False(result.Succeeded);Assert.Contains("cannot be rewound",result.Error);
    }
    [Fact] public void NewMigrationFailureLeavesV15AndCreatesBackup()
    {
        using var f=new Fixture();
        f.Sql("DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DROP TABLE content_transfer_items; DROP TABLE content_transfers; DROP TABLE managed_storage_collections; DROP TABLE managed_storage_roots; DELETE FROM schema_migrations WHERE version>=16; UPDATE state_head SET schema_version=15; PRAGMA user_version=15; CREATE TABLE content_transfers(collision TEXT);");
        Assert.False(f.Store.Initialize().Succeeded);Assert.Equal(15L,f.Sql("PRAGMA user_version"));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM schema_migrations WHERE version=16"));
        Assert.NotEmpty(Directory.GetFiles(f.Paths.Backups,"*.sqlite3"));
    }
    [Theory] [InlineData(@"C:\")] [InlineData(@"\\server\share\x")] [InlineData(@"C:\x:y")] [InlineData(@"C:\bad?.txt")]
    public void AmbiguousPathsRejectBeforeAnyMutation(string path) => Assert.Throws<IOException>(()=>LocalTransferFiles.Normalize(path));

    [Theory]
    [InlineData(TransferExecutionCheckpoint.AfterMove)]
    [InlineData(TransferExecutionCheckpoint.FileRecorded)]
    [InlineData(TransferExecutionCheckpoint.IndexCommitted)]
    public void IndependentProcessExitUsesOnlyPersistedRecoveryEvidence(TransferExecutionCheckpoint point)
    {
        using var f=new Fixture();var p=f.Stage(f.File("process.txt"));
        string helper=Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.TransferFixture.exe");
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true};
        foreach(string argument in new[]{"--transfer-crash",f.DirectoryRoot,p.Plan.Request.OperationId.ToString(),point.ToString()})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!;
        Assert.True(process.WaitForExit(15000),"Owned process did not exit at the selected boundary.");Assert.Equal(73,process.ExitCode);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Recover()).Phase);
        Assert.Single(f.State.ContentCatalog.PosterItems);Assert.False(System.IO.File.Exists(p.Files[0].Source));
    }
    [Theory]
    [InlineData(null)]
    [InlineData(TransferExecutionCheckpoint.AfterCopy)]
    [InlineData(TransferExecutionCheckpoint.CopyRecorded)]
    [InlineData(TransferExecutionCheckpoint.AfterMove)]
    [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeFileCopiesVerifyBeforeSourceDisposal(TransferExecutionCheckpoint? point)
    {
        using var f=new Fixture();var external=Directory.CreateTempSubdirectory("DesktopUp.Transfer.Owned.");
        try
        {
            string source=Path.Combine(external.FullName,"cross.txt");System.IO.File.WriteAllText(source,"owned-content");
            var p=f.Stage(source);Assert.False(p.Files[0].SameVolume,"This integration case requires two real local volumes.");
            if(point is not null)
                Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
                {if(at==point)throw new TransferInterruptionException("lost copy executor");}));
            else
            {var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId));Assert.True(row.Phase==DurableTransferPhase.IndexCommitted,row.Error);}
            bool completed=point is null or TransferExecutionCheckpoint.AfterMove;
            Assert.Equal(completed?DurableTransferPhase.IndexCommitted:DurableTransferPhase.Review,Assert.Single(f.Executor.Recover()).Phase);
            Assert.Equal(!completed,System.IO.File.Exists(source));Assert.Equal("owned-content",System.IO.File.ReadAllText(p.Files[0].Destination));
        }
        finally {external.Delete(true);}
    }
    [Fact] [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeDirectoryIsExplicitlyUnsupportedBeforeMovingAnything()
    {
        using var f=new Fixture();var external=Directory.CreateTempSubdirectory("DesktopUp.Transfer.Owned.");
        try {Assert.Throws<NotSupportedException>(()=>f.Stage(external.FullName));Assert.True(external.Exists);Assert.Empty(f.Journal.Read());}
        finally {external.Delete(true);}
    }
    [Fact] public void SourceFileAndDestinationParentReplacementAreDetected()
    {
        using var f=new Fixture();var p=f.Stage(f.File("replace.txt"));
        System.IO.File.Move(p.Files[0].Source,p.Files[0].Source+".original");System.IO.File.WriteAllText(p.Files[0].Source,"owned-content");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.True(System.IO.File.Exists(p.Files[0].Source+".original"));
    }
    [Fact] public void ReadOnlySourceIsRejectedWithoutClearingAttributes()
    {
        using var f=new Fixture();var p=f.Stage(f.File("readonly.txt"));
        System.IO.File.SetAttributes(p.Files[0].Source,FileAttributes.ReadOnly);
        try {Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);Assert.True(System.IO.File.Exists(p.Files[0].Source));}
        finally {if(System.IO.File.Exists(p.Files[0].Source))System.IO.File.SetAttributes(p.Files[0].Source,FileAttributes.Normal);else System.IO.File.SetAttributes(p.Files[0].Destination,FileAttributes.Normal);}
    }
    [Fact] public void UnknownJournalFormatFailsClosedWithoutRewritingRecord()
    {
        using var f=new Fixture();var p=f.Stage(f.File("unknown.txt"));
        f.Sql("UPDATE content_transfers SET body_json=json_set(body_json,'$.FormatVersion',99)");
        Assert.Throws<InvalidDataException>(()=>f.Executor.Recover());Assert.True(System.IO.File.Exists(p.Files[0].Source));
        Assert.Equal(99L,f.Sql("SELECT json_extract(body_json,'$.FormatVersion') FROM content_transfers"));
    }
    [Fact] public void DestinationParentReplacementDoesNotRedirectTheMove()
    {
        using var f=new Fixture();var p=f.Stage(f.File("parent.txt"));
        Directory.Move(f.Collection.StorageLocator!,f.Collection.StorageLocator+".original");Directory.CreateDirectory(f.Collection.StorageLocator!);
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.True(System.IO.File.Exists(p.Files[0].Source));Assert.Empty(Directory.GetFileSystemEntries(f.Collection.StorageLocator!));
    }
    [Fact] public void CancellationAtLastPreMoveBoundaryKeepsTheOriginal()
    {
        using var f=new Fixture();var p=f.Stage(f.File("cancel-at-boundary.txt"));using var cancel=new CancellationTokenSource();
        f.Executor.Execute(p.Plan.Request.OperationId,cancel.Token,(at,_)=>{if(at==TransferExecutionCheckpoint.BeforeMove)cancel.Cancel();});
        Assert.True(System.IO.File.Exists(p.Files[0].Source));Assert.False(System.IO.File.Exists(p.Files[0].Destination));
    }
    [Fact] public void ExecutionBlocksConflictingCommandsAndSecondExecutor()
    {
        using var f=new Fixture();var p=f.Stage(f.File("owned-state.txt"));bool visited=false;
        f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {
            if(at!=TransferExecutionCheckpoint.Claimed)return;visited=true;
            Assert.Throws<IOException>(()=>new LocalTransferExecutor(new(f.Store)).Recover());
            var s=f.State;
            var result=f.Repository.Execute(new("concurrent-edit","same",s.Revision,StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion)),
                _=>new(){Updates=[s.Catalog.Posters.Single().Poster with {Title="conflict"}]});
            Assert.Equal(StateCommandStatus.Conflict,result.Status);
        });
        Assert.True(visited);Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Journal.Read()).Phase);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void PosterRemovalDoesNotEraseUnfinishedFileEvidence(bool afterMove)
    {
        using var f=new Fixture();var p=f.Stage(f.File("orphan.txt"));
        if(afterMove)
        { f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>{if(at==TransferExecutionCheckpoint.AfterMove)throw new IOException("injected index interruption");}); }
        var s=f.State;
        var removed=f.Repository.Execute(new("remove-for-test","test",s.Revision,new(new Dictionary<string,long>{{f.Main,s.Catalog.Posters.Single().EntityVersion}},CatalogVersion:s.Catalog.CatalogVersion)),_=>new(){Removes=[f.Main]});
        Assert.Equal(StateCommandStatus.Committed,removed.Status);
        var row=Assert.Single(f.Executor.Recover());Assert.Equal(DurableTransferPhase.Review,row.Phase);
        Assert.True(System.IO.File.Exists(afterMove?row.File.Destination:row.File.Source));Assert.Single(f.State.ContentCatalog.Collections);
    }
    [Fact] public void ACompleteV15MigrationPreservesReferencesAndOldChecksums()
    {
        using var f=new Fixture();f.AddReference(LocalContentReferenceProbe.Inspect(f.File("migration.txt")).Item!);
        string before=ContentCatalogCodec.Write(f.State.ContentCatalog);
        f.Sql("DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DROP TABLE content_transfer_items; DROP TABLE content_transfers; DROP TABLE managed_storage_collections; DROP TABLE managed_storage_roots; DELETE FROM schema_migrations WHERE version>=16; UPDATE state_head SET schema_version=15; PRAGMA user_version=15;");
        var old=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=15 ORDER BY version");
        var init=f.Store.Initialize();Assert.True(init.Succeeded,init.Error);Assert.Equal(15,init.PreviousSchemaVersion);
        Assert.Equal(old,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=15 ORDER BY version"));
        Assert.Equal(before,ContentCatalogCodec.Write(f.State.ContentCatalog));Assert.Empty(f.Journal.Read());
    }
    [Fact] [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeAlternateStreamsAreNotSilentlyLost()
    {
        using var f=new Fixture();var dir=Directory.CreateTempSubdirectory("DesktopUp.Transfer.Owned.");
        try
        {
            string source=Path.Combine(dir.FullName,"streams.txt");System.IO.File.WriteAllText(source,"main");System.IO.File.WriteAllText(source+":test", "alternate");
            Assert.Throws<NotSupportedException>(()=>f.Stage(source));Assert.Equal("alternate",System.IO.File.ReadAllText(source+":test"));Assert.Empty(f.Journal.Read());
        }
        finally {dir.Delete(true);}
    }
    [Theory] [InlineData(true)] [InlineData(false)] [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeFailedDisposalOrCancelledCopyKeepsBoth(bool failDisposal)
    {
        using var f=new Fixture();var dir=Directory.CreateTempSubdirectory("DesktopUp.Transfer.Owned.");
        string source=Path.Combine(dir.FullName,"retained.txt");System.IO.File.WriteAllText(source,"owned-content");
        try
        {
            var p=f.Stage(source);using var cancel=new CancellationTokenSource();
            var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId,cancel.Token,(at,_)=>
            {
                if(at!=TransferExecutionCheckpoint.CopyRecorded)return;
                if(failDisposal)System.IO.File.SetAttributes(source,FileAttributes.ReadOnly);else cancel.Cancel();
            }));
            Assert.Equal(DurableTransferPhase.Review,row.Phase);Assert.True(System.IO.File.Exists(source));Assert.True(System.IO.File.Exists(row.File.Destination));Assert.Empty(f.State.ContentCatalog.Items);
        }
        finally {if(System.IO.File.Exists(source))System.IO.File.SetAttributes(source,FileAttributes.Normal);dir.Delete(true);}
    }
    [Fact] public void SharedMovePreservesOtherCardIconLayoutAndStableEntity()
    {
        using var f=new Fixture();string source=f.File("shared.txt");var item=LocalContentReferenceProbe.Inspect(source).Item!;f.AddReference(item);
        var s=f.State;var poster=PosterConfigurationFactory.NewBasicBox("other","DISPLAY1") with {PosterId=Guid.NewGuid().ToString(),ThemeInstanceId=Guid.NewGuid().ToString()};
        var created=f.Repository.Execute(new("create-b","create-b",s.Revision,StateReadSet.ForCatalog(s.Catalog.CatalogVersion)),_=>new(){Creates=[poster]});
        Assert.Equal(StateCommandStatus.Committed,created.Status);s=f.State;
        var reads=StateReadSet.ForPoster(poster.PosterId,s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster.PosterId).EntityVersion) with {ContentVersion=s.ContentVersion};
        var add=new ContentReferenceCommand("share-b",StateCommandSource.ControlCenter,s.Revision,reads,poster.PosterId,[new(item,ContentReferenceChoice.Reuse,item.Id)],true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new(add.RequestId,add.RequestId,s.Revision,reads),before=>ContentReferencePolicy.Prepare(add,before)).Status);
        s=f.State;var b=s.ContentCatalog.PosterItems.Single(p=>p.PosterId==poster.PosterId);
        reads=reads with {ContentVersion=s.ContentVersion,Posters=new Dictionary<string,long>{{poster.PosterId,s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster.PosterId).EntityVersion}}};
        var icon=new ContentIconCommand("icon-b",StateCommandSource.ControlCenter,s.Revision,reads,ContentRelationPolicy.Resolve(s.ContentCatalog,poster.PosterId,b.LayoutItemId)!.Witness,ContentIconPersistenceTests.Icon(),true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new(icon.RequestId,icon.RequestId,s.Revision,reads),before=>ContentIconPolicy.Prepare(icon,before)).Status);
        s=f.State;b=s.ContentCatalog.PosterItems.Single(p=>p.PosterId==poster.PosterId);var bLayout=s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster.PosterId).Poster.ContentLayoutJson;
        var p=f.Stage(source);Assert.Equal(2,p.Plan.Entries[0].AffectedPosterIds.Count);
        f.Executor.Execute(p.Plan.Request.OperationId);s=f.State;
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Journal.Read()).Phase);Assert.Contains(b,s.ContentCatalog.PosterItems);
        Assert.Equal(bLayout,s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster.PosterId).Poster.ContentLayoutJson);Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        var a=s.ContentCatalog.PosterItems.Single(p=>p.PosterId==f.Main);item=Assert.Single(s.ContentCatalog.Items);
        var outgoing=LocalTransferPreview.Prepare(s,f.Main,ContentDropRoute.ToDesktop,[(item,Path.Combine(f.Source,"back.txt"),a.Id)],isolatedDestination:true);
        f.Journal.Begin(outgoing.Plan,outgoing.Files,outgoing.Plan.Fingerprint);f.Executor.Execute(outgoing.Plan.Request.OperationId);
        Assert.Equal(b,Assert.Single(f.State.ContentCatalog.PosterItems));Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        Assert.Equal(bLayout,f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==poster.PosterId).Poster.ContentLayoutJson);
    }
    [Fact] public void MalformedOrFutureStateCannotAuthorizeAFileAction()
    {
        using var f=new Fixture();f.Sql("UPDATE state_head SET schema_version=19; PRAGMA user_version=19;");
        var init=f.Store.Initialize();Assert.False(init.Succeeded);Assert.Equal(19L,f.Sql("PRAGMA user_version"));
    }
    [Fact] public void StorageRootCannotLiveInsideTemporaryOrApplicationStateData()
    {
        using var f=new Fixture();string collection=Path.Combine(f.Paths.Temp,"storage");Directory.CreateDirectory(collection);
        var root=new ManagedStorageRoot(Guid.NewGuid(),f.Paths.Temp,LocalTransferFiles.Identity(f.Paths.Temp),1,ManagedRootLocation.UserPersistent,true,true);
        Assert.Throws<IOException>(()=>f.Journal.Register(root,new(Guid.NewGuid(),"bad",ContentCollectionKind.Managed,collection,ContentStorageOwnership.Managed),new(new Dictionary<string,long>(),ContentVersion:f.State.ContentVersion)));
    }
    [Fact] public void JunctionAncestorsAndDescendantsAreRejected()
    {
        using var f=new Fixture();string link=Path.Combine(f.DirectoryRoot,"junction");
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("/c");start.ArgumentList.Add("mklink");start.ArgumentList.Add("/J");start.ArgumentList.Add(link);start.ArgumentList.Add(f.Source);
        using var child=Process.Start(start)!;Assert.True(child.WaitForExit(10000));Assert.Equal(0,child.ExitCode);
        try
        {
            string source=f.File("junction.txt");
            Assert.Throws<IOException>(()=>LocalTransferFiles.Inspect(Path.Combine(link,"junction.txt"),Path.Combine(f.Collection.StorageLocator!,"new.txt")));
            Assert.Throws<IOException>(()=>LocalTransferFiles.Inspect(f.Source,Path.Combine(link,"nested")));
            Assert.Equal("owned-content",System.IO.File.ReadAllText(source));
        }
        finally {Directory.Delete(link);} // Remove this owned junction before deleting its owned target.
    }
    [Fact] public void SamePathNoOpNeverDetachesOrCopiesContent()
    {
        using var f=new Fixture();var p=f.Stage(f.File("same.txt"));f.Executor.Execute(p.Plan.Request.OperationId);
        var state=f.State;var same=f.Stage(p.Files[0].Destination);
        Assert.Equal(DurableTransferPhase.NoOp,Assert.Single(f.Executor.Execute(same.Plan.Request.OperationId)).Phase);
        Assert.Equal(state.Revision,f.State.Revision);Assert.Equal(state.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
    }
    internal sealed class Fixture:IDisposable
    {
        public string DirectoryRoot {get;}
        public string Source {get;}
        public UserDataPaths Paths {get;}
        public SqliteStateStore Store {get;}
        public SqliteStateCommandRepository Repository {get;}
        public SqliteTransferJournal Journal {get;}
        public LocalTransferExecutor Executor {get;}
        public ManagedStorageRoot Root {get;}
        public Collection Collection {get;}
        public string Main=>BuiltInStateIdentities.PrimaryPosterId;
        public DesktopStateSnapshot State=>Repository.Read();
        public Fixture()
        {
            string repo=AppContext.BaseDirectory;
            while(!System.IO.File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture root missing.");
            DirectoryRoot=Path.Combine(repo,"out","transfer-journal","fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryRoot);
            System.IO.File.WriteAllText(Path.Combine(DirectoryRoot,".transfer-fixture-owner"),"owned");
            Source=Path.Combine(DirectoryRoot,"source");Directory.CreateDirectory(Source);
            string storage=Path.Combine(DirectoryRoot,"storage");Directory.CreateDirectory(storage);
            Paths=new(Path.Combine(DirectoryRoot,"profile"));Store=new(Paths);var init=Store.Initialize();Assert.True(init.Succeeded,init.Error);
            Repository=new(Store);Repository.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("transfer","DISPLAY1") with {PosterId=Main});
            Journal=new(Store);Executor=new(Journal);
            var provisioner=new ManagedStorageProvisioner(Store);var preview=provisioner.Preview(storage);
            var registered=provisioner.Confirm(preview,preview.Fingerprint);Root=registered.Root;Collection=registered.Collection;
        }
        public string File(string name){string p=Path.Combine(Source,name);System.IO.File.WriteAllText(p,"owned-content");return p;}
        public LocalTransferPreview Stage(params string[] paths)
        {
            var state=State;
            var inputs=paths.Select(p=>
            {
                var normalized=LocalTransferFiles.Normalize(p);
                var item=state.ContentCatalog.Items.FirstOrDefault(i=>i.Target.Locator==normalized)??LocalContentReferenceProbe.Inspect(p).Item!;
                return(item,Path.Combine(Collection.StorageLocator!,Path.GetFileName(p)),(Guid?)null);
            }).ToArray();
            var preview=LocalTransferPreview.Prepare(state,Main,ContentDropRoute.IntoManagedStorage,inputs,Root);
            Journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint,Collection.Id);return preview;
        }
        public void AddReference(ContentItem item)
        {
            var before=State;
            var request=new ContentReferenceCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,before.Revision,
                StateReadSet.ForPoster(Main,before.Catalog.Posters.Single().EntityVersion) with {ContentVersion=before.ContentVersion},
                Main,[new(item,ContentReferenceChoice.CreateIndependent)],true);
            var result=Repository.Execute(new(request.RequestId,"fixture-"+request.RequestId,before.Revision,request.ReadSet),s=>ContentReferencePolicy.Prepare(request,s));
            Assert.Equal(StateCommandStatus.Committed,result.Status);
        }
        public object? Sql(string sql)
        {
            using var c=new SqliteConnection($"Data Source={Path.Combine(Paths.State,"desktopup.sqlite3")};Pooling=False");c.Open();
            using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();
        }
        public void Dispose()
        {
            Store.Dispose();SqliteConnection.ClearAllPools();
            // Only the unique directory created by this fixture, never source/desktop input.
            if(System.IO.File.ReadAllText(Path.Combine(DirectoryRoot,".transfer-fixture-owner"))=="owned")Directory.Delete(DirectoryRoot,true);
        }
    }
}
