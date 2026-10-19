using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferRecoveryTests
{
    [Fact] public void DiscoveryDoesNotExecuteAnIntentOrReadFileContents()
    {
        using var f=new F();var p=f.Stage(f.File("pending.txt"));
        using var locked=new FileStream(p.Files[0].Source,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        var row=Assert.Single(f.Journal.RecoveryEntries());Assert.Equal(DurableTransferPhase.Intent,row.Item.Phase);
        Assert.True(row.PosterExists);Assert.False(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void ExplicitRetryReplacesReservationAtomicallyAndIsNotAnImplicitMove()
    {
        using var f=new F();var p=f.Stage(f.File("retry.txt"));f.Executor.Recover();
        var ticket=f.Journal.PreviewRetry(p.Plan.Request.OperationId,0);
        Assert.NotEqual(p.Plan.Request.OperationId,ticket.Preview.Plan.Request.OperationId);
        long revision=f.State.Revision;Guid next=f.Journal.ConfirmRetry(ticket,ticket.Preview.Plan.Fingerprint);
        Assert.Equal(revision,f.State.Revision);Assert.True(File.Exists(p.Files[0].Source));
        Assert.Equal(DurableTransferPhase.Cancelled,f.Journal.Read().Single(x=>x.OperationId==p.Plan.Request.OperationId).Phase);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(next)).Phase);
        Assert.Equal(revision+1,f.State.Revision);Assert.Throws<IOException>(()=>f.Journal.ConfirmRetry(ticket,ticket.Preview.Plan.Fingerprint));
    }
    [Fact] public void RetryRefreshesContentReadSetButConfirmationCannotSilentlyRebaseAgain()
    {
        using var f=new F();var p=f.Stage(f.File("first.txt"));f.Executor.Recover();
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("extra.txt")).Item!);
        var ticket=f.Journal.PreviewRetry(p.Plan.Request.OperationId,0);
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("later.txt")).Item!);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmRetry(ticket,ticket.Preview.Plan.Fingerprint));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        Assert.True(File.Exists(p.Files[0].Source));Assert.False(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void FailedNewIntentRollsBackOldReservationRelease()
    {
        using var f=new F();var p=f.Stage(f.File("atomic.txt"));f.Executor.Recover();
        var t=f.Journal.PreviewRetry(p.Plan.Request.OperationId,0);
        f.Sql("CREATE TRIGGER fail_retry BEFORE INSERT ON content_transfers BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>f.Journal.ConfirmRetry(t,t.Preview.Plan.Fingerprint));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        Assert.True(File.Exists(p.Files[0].Source));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void ChangedFactsRejectRetryAndKeepOldReservation(bool destination)
    {
        using var f=new F();var p=f.Stage(f.File("changed.txt"));f.Executor.Recover();
        var t=f.Journal.PreviewRetry(p.Plan.Request.OperationId,0);
        File.WriteAllText(destination?p.Files[0].Destination:p.Files[0].Source,"new bytes");
        Assert.Throws<IOException>(()=>f.Journal.ConfirmRetry(t,t.Preview.Plan.Fingerprint));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
    }
    [Fact] public void WrongConfirmationDoesNotReleaseOrWriteNewIntent()
    {
        using var f=new F();var p=f.Stage(f.File("confirm.txt"));f.Executor.Recover();
        var t=f.Journal.PreviewRetry(p.Plan.Request.OperationId,0);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmRetry(t,"other"));Assert.Single(f.Journal.Read());
    }
    [Fact] public void ExplicitEndOnlyReleasesProvenUnmovedItemAndKeepsHistoryAndGuard()
    {
        using var f=new F();var p=f.Stage(f.File("release.txt"));var entry=Assert.Single(f.Journal.RecoveryEntries());
        f.Journal.ReleaseUnexecuted(entry.Item.OperationId,0,entry.Witness);
        Assert.Equal(DurableTransferPhase.Cancelled,Assert.Single(f.Journal.Read()).Phase);
        Assert.True(File.Exists(p.Files[0].Source));Assert.True(File.Exists(Path.Combine(f.Paths.State,"managed-file-history.guard")));
        f.Stage(p.Files[0].Source);Assert.Equal(2,f.Journal.Read().Count);
    }
    [Theory] [InlineData("changed-source")] [InlineData("same-name-replaced")] [InlineData("both")] [InlineData("missing")]
    public void AmbiguousReleaseNeverSuppressesPendingEvidence(string change)
    {
        using var f=new F();var p=f.Stage(f.File("ambiguous.txt"));f.Executor.Recover();
        var entry=Assert.Single(f.Journal.RecoveryEntries());
        if(change=="changed-source")File.WriteAllText(p.Files[0].Source,"different");
        if(change=="same-name-replaced"){File.Move(p.Files[0].Source,p.Files[0].Source+".keep");File.WriteAllText(p.Files[0].Source,"owned-content");}
        if(change=="both")File.Copy(p.Files[0].Source,p.Files[0].Destination);
        if(change=="missing")File.Move(p.Files[0].Source,p.Files[0].Source+".keep");
        Assert.ThrowsAny<IOException>(()=>f.Journal.ReleaseUnexecuted(entry.Item.OperationId,0,entry.Witness));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
    }
    [Fact] public void StaleReviewWitnessCannotEndNewlyChangedTask()
    {
        using var f=new F();var p=f.Stage(f.File("stale.txt"));var row=Assert.Single(f.Journal.RecoveryEntries());
        f.Executor.Recover();Assert.Throws<IOException>(()=>f.Journal.ReleaseUnexecuted(row.Item.OperationId,0,row.Witness));
    }
    [Fact] public void DeletedPosterKeepsPathsAndAllowsEndingUnmovedButCannotRetry()
    {
        using var f=new F();var p=f.Stage(f.File("deleted.txt"));f.Executor.Recover();
        f.Sql("DELETE FROM posters;");
        var row=Assert.Single(f.Journal.RecoveryEntries());Assert.False(row.PosterExists);
        Assert.Equal(p.Files[0].Destination,row.Item.File.Destination);
        Assert.Throws<IOException>(()=>f.Journal.PreviewRetry(row.Item.OperationId,0));
        f.Journal.ReleaseUnexecuted(row.Item.OperationId,0,row.Witness);Assert.Empty(f.State.Catalog.Posters);
    }
    [Fact] public void PerItemReconcileDoesNotConsumeAnotherUnstartedIntent()
    {
        using var f=new F();var p=f.Stage(f.File("one.txt"),f.File("two.txt"));
        f.Executor.Recover(p.Plan.Request.OperationId,0);
        Assert.Equal(DurableTransferPhase.Review,f.Journal.Read()[0].Phase);
        Assert.Equal(DurableTransferPhase.Intent,f.Journal.Read()[1].Phase);
    }
    [Fact] public void ExecutorLeaseBlocksUserResolutionWithoutChangingEvidence()
    {
        using var f=new F();var p=f.Stage(f.File("lease.txt"));
        using var lease=new FileStream(Path.Combine(f.Paths.State,"transfer-executor.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>f.Journal.PreviewRetry(p.Plan.Request.OperationId,0));
        Assert.Equal(DurableTransferPhase.Intent,Assert.Single(f.Journal.Read()).Phase);
    }
    [Fact] public void StoragePreviewIsReadOnlyThenCreatesDedicatedChildWithoutCollectingExistingFiles()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"persistent");Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"user.txt"),"untouched");
        var provisioner=new ManagedStorageProvisioner(f.Store);var p=provisioner.Preview(root);
        Assert.Single(Directory.GetFileSystemEntries(root));Assert.False(Directory.Exists(p.CollectionPath));
        var result=provisioner.Confirm(p,p.Fingerprint);
        Assert.Equal(p.CollectionPath,result.Collection.StorageLocator);Assert.Equal("untouched",File.ReadAllText(Path.Combine(root,"user.txt")));
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Empty(f.State.ContentCatalog.Items);
        long revision=f.State.Revision;var again=provisioner.Preview(root);provisioner.Confirm(again,again.Fingerprint);
        Assert.Equal(revision,f.State.Revision);Assert.Equal(2,f.Journal.StorageLocations().Count);
        Assert.Contains(provisioner.Pending(),x=>x.CollectionId==p.Intent.CollectionId);
    }
    [Theory]
    [InlineData(StorageProvisionCheckpoint.IntentWritten)] [InlineData(StorageProvisionCheckpoint.RootClaimed)]
    [InlineData(StorageProvisionCheckpoint.ChildClaimed)] [InlineData(StorageProvisionCheckpoint.Registered)]
    public void RecordedCreationCanResumeAfterFreshProcessState(StorageProvisionCheckpoint point)
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"creation");Directory.CreateDirectory(root);
        var provisioner=new ManagedStorageProvisioner(f.Store);var p=provisioner.Preview(root);
        Assert.Throws<TransferInterruptionException>(()=>provisioner.Confirm(p,p.Fingerprint,at=>{if(at==point)throw new TransferInterruptionException("loss");}));
        using var store=new SqliteStateStore(f.Paths);Assert.True(store.Initialize().Succeeded);
        var next=new ManagedStorageProvisioner(store);var resume=next.Preview(next.Pending().Single(x=>x.CollectionId==p.Intent.CollectionId).Root.Locator);
        var result=next.Confirm(resume,resume.Fingerprint);Assert.Equal(p.Intent.CollectionId,result.Collection.Id);
        Assert.True(Directory.Exists(result.Collection.StorageLocator));Assert.Equal(2,new SqliteTransferJournal(store).StorageLocations().Count);
    }
    [Fact] public void MissingChildOwnershipProofIsNotGuessedAfterCreationGap()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"gap");Directory.CreateDirectory(root);
        var service=new ManagedStorageProvisioner(f.Store);var p=service.Preview(root);
        Assert.Throws<TransferInterruptionException>(()=>service.Confirm(p,p.Fingerprint,at=>{if(at==StorageProvisionCheckpoint.DirectoryCreated)throw new TransferInterruptionException("gap");}));
        File.WriteAllText(Path.Combine(p.CollectionPath,"keep.txt"),"keep");
        Assert.Throws<IOException>(()=>service.Confirm(service.Preview(root),service.Preview(root).Fingerprint));
        Assert.Equal("keep",File.ReadAllText(Path.Combine(p.CollectionPath,"keep.txt")));Assert.Single(f.Journal.StorageLocations());
    }
    [Fact] public void AnotherProfileCannotClaimOrRegisterExistingOwnedRoot()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"owner");Directory.CreateDirectory(root);
        var service=new ManagedStorageProvisioner(f.Store);var p=service.Preview(root);service.Confirm(p,p.Fingerprint);
        using var other=new SqliteStateStore(new UserDataPaths(Path.Combine(f.DirectoryRoot,"other-profile")));Assert.True(other.Initialize().Succeeded);
        Assert.Throws<IOException>(()=>new ManagedStorageProvisioner(other).Preview(root));Assert.Empty(new SqliteTransferJournal(other).StorageLocations());
    }
    [Fact] public void ReplacedRootCannotConsumeOldConfirmation()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"replace");Directory.CreateDirectory(root);
        var service=new ManagedStorageProvisioner(f.Store);var p=service.Preview(root);
        Directory.Move(root,root+".keep");Directory.CreateDirectory(root);
        Assert.Throws<IOException>(()=>service.Confirm(p,p.Fingerprint));Assert.Empty(Directory.GetFileSystemEntries(root));
    }
    [Fact] public void OwnershipCannotBeBypassedThroughLowLevelRegistration()
    {
        using var f=new F();using var other=new SqliteStateStore(new UserDataPaths(Path.Combine(f.DirectoryRoot,"foreign")));
        Assert.True(other.Initialize().Succeeded);
        Assert.Throws<IOException>(()=>new SqliteTransferJournal(other).Register(f.Root,f.Collection,new(new Dictionary<string,long>(),ContentVersion:0)));
        Assert.Empty(new SqliteTransferJournal(other).StorageLocations());
    }
    [Fact] public void MissingRootClaimAfterConfirmationBlocksExecution()
    {
        using var f=new F();var p=f.Stage(f.File("lost-claim.txt"));
        string marker=Path.Combine(f.Root.Locator,".desktopup-storage-owner.json");File.Move(marker,marker+".kept");
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        Assert.True(File.Exists(p.Files[0].Source));Assert.False(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void UnknownMarkerNeverOverwrittenOrRemoved()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"unknown");Directory.CreateDirectory(root);
        string marker=Path.Combine(root,".desktopup-storage-owner.json");File.WriteAllText(marker,"future-format");
        Assert.ThrowsAny<Exception>(()=>new ManagedStorageProvisioner(f.Store).Preview(root));Assert.Equal("future-format",File.ReadAllText(marker));
    }
    [Fact] public void RegistrationFaultPreservesDirectoryClaimsAndAllowsExplicitRetry()
    {
        using var f=new F();string root=Path.Combine(f.DirectoryRoot,"register-fail");Directory.CreateDirectory(root);
        var service=new ManagedStorageProvisioner(f.Store);var p=service.Preview(root);
        f.Sql("CREATE TRIGGER fail_register BEFORE INSERT ON managed_storage_roots BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>service.Confirm(p,p.Fingerprint));
        Assert.True(Directory.Exists(p.CollectionPath));Assert.Single(f.Journal.StorageLocations());
        f.Sql("DROP TRIGGER fail_register;");var again=service.Preview(root);service.Confirm(again,again.Fingerprint);
        Assert.Equal(2,f.Journal.StorageLocations().Count);
    }
}
