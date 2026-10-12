using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferClosureTests
{
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)]
    public void StopPreservesBothCopiesAndOriginalEvidenceAcrossRestart(int phase)
    {
        using var f=new F();var p=f.Stage(f.File("both.txt"));File.Copy(p.Files[0].Source,p.Files[0].Destination);
        f.Sql($"UPDATE content_transfer_items SET phase={phase},error='preserved error';");
        var original=Assert.Single(f.Journal.Read());long revision=f.State.Revision;
        var history=f.Sql("SELECT body_json||reads_json FROM content_transfers");
        var stop=f.Journal.PreviewStop(original.OperationId,0);
        // Stop is not a claim that either path is available, intact or even readable.
        using(var locked=new FileStream(original.File.Source,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            f.Journal.ConfirmStop(stop,stop.Fingerprint);
        f.Journal.ConfirmStop(stop,stop.Fingerprint);
        using var store=new SqliteStateStore(f.Paths);Assert.True(store.Initialize().Succeeded);
        var journal=new SqliteTransferJournal(store);var executor=new LocalTransferExecutor(journal);
        executor.Execute(original.OperationId);executor.Recover();
        var row=Assert.Single(journal.Read());Assert.True(row.IsFinished);Assert.NotNull(row.Closure);
        Assert.Equal(StateRequestFingerprint.Compute(original),StateRequestFingerprint.Compute(row with{Closure=null}));Assert.Equal(revision,f.State.Revision);
        Assert.Equal(history,f.Sql("SELECT body_json||reads_json FROM content_transfers"));
        Assert.Equal("owned-content",File.ReadAllText(original.File.Source));Assert.Equal("owned-content",File.ReadAllText(original.File.Destination));
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_transfer_closures"));
        Assert.True(File.Exists(Path.Combine(f.Paths.State,"managed-file-history.guard")));
    }
    [Theory] [InlineData("changed")] [InlineData("missing")] [InlineData("moved")]
    public void StopDoesNotGuessOrRepairPhysicalOutcome(string outcome)
    {
        using var f=new F();var p=f.Stage(f.File("uncertain.txt"));f.Executor.Recover();
        if(outcome=="changed")File.WriteAllText(p.Files[0].Source,"changed-source");
        else File.Move(p.Files[0].Source,outcome=="moved"?p.Files[0].Destination:p.Files[0].Source+".keep");
        var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        f.Executor.Recover();Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        string kept=outcome=="changed"?p.Files[0].Source:outcome=="moved"?p.Files[0].Destination:p.Files[0].Source+".keep";
        Assert.Equal(outcome=="changed"?"changed-source":"owned-content",File.ReadAllText(kept));
    }
    [Fact] public void OldRetryAndCancelCannotReviveStoppedRowButNewPreviewMayProceed()
    {
        using var f=new F();var p=f.Stage(f.File("again.txt"));var entry=Assert.Single(f.Journal.RecoveryEntries());
        var retry=f.Journal.PreviewRetry(entry.Item.OperationId,0);var stop=f.Journal.PreviewStop(entry.Item.OperationId,0);
        f.Journal.ConfirmStop(stop,stop.Fingerprint);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmRetry(retry,retry.Preview.Plan.Fingerprint));
        Assert.Throws<IOException>(()=>f.Journal.ReleaseUnexecuted(entry.Item.OperationId,0,entry.Witness));
        Assert.Throws<IOException>(()=>f.Journal.Claim(entry.Item.OperationId,0));
        Assert.Throws<IOException>(()=>f.Journal.Mark(entry.Item.OperationId,0,DurableTransferPhase.Review));
        var next=f.Stage(p.Files[0].Source);Assert.NotEqual(p.Plan.Request.OperationId,next.Plan.Request.OperationId);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(next.Plan.Request.OperationId)).Phase);
    }
    [Fact] public void OldIndexRecoveryTicketCannotFinishStoppedRow()
    {
        using var f=new F();var p=f.Stage(f.File("index.txt"));
        f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>{if(at==TransferExecutionCheckpoint.AfterMove)throw new IOException("pause");});
        var recovery=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmIndexRecovery(recovery,recovery.Fingerprint));
        Assert.Throws<IOException>(()=>f.Journal.CommitVerified(p.Plan.Request.OperationId,0));
        Assert.True(File.Exists(p.Files[0].Destination));Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Theory] [InlineData("confirmation")] [InlineData("row")] [InlineData("state")]
    public void StaleConfirmationRetainsPendingReservation(string change)
    {
        using var f=new F();var p=f.Stage(f.File("stale.txt"));var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);
        if(change=="row")f.Executor.Recover();
        if(change=="state")f.AddReference(LocalContentReferenceProbe.Inspect(f.File("other.txt")).Item!);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmStop(stop,change=="confirmation"?"wrong":stop.Fingerprint));
        Assert.False(Assert.Single(f.Journal.Read()).IsFinished);
        Assert.Throws<IOException>(()=>f.Stage(p.Files[0].Source));
    }
    [Fact] public void SqlFailureRollsBackClosureWithoutReleasingReservation()
    {
        using var f=new F();var p=f.Stage(f.File("sql.txt"));var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);
        f.Sql("CREATE TRIGGER fail_stop BEFORE INSERT ON content_transfer_closures BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>f.Journal.ConfirmStop(stop,stop.Fingerprint));
        Assert.False(Assert.Single(f.Journal.Read()).IsFinished);Assert.Throws<IOException>(()=>f.Stage(p.Files[0].Source));
    }
    [Fact] public void ExecutorLeaseExcludesStopAndPreview()
    {
        using var f=new F();var p=f.Stage(f.File("lease.txt"));var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);
        using var lease=f.Journal.AcquireExecutor();
        Assert.Throws<IOException>(()=>f.Journal.PreviewStop(p.Plan.Request.OperationId,0));
        Assert.Throws<IOException>(()=>f.Journal.ConfirmStop(stop,stop.Fingerprint));
        Assert.False(Assert.Single(f.Journal.Read()).IsFinished);
    }
    [Fact] public void OneClosureDoesNotReleaseOrRewriteSibling()
    {
        using var f=new F();var p=f.Stage(f.File("a.txt"),f.File("b.txt"));var before=f.Journal.Read()[1];
        var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        Assert.Equal(StateRequestFingerprint.Compute(before),StateRequestFingerprint.Compute(f.Journal.Read()[1]));Assert.Throws<IOException>(()=>f.Stage(p.Files[1].Source));
        Assert.Equal(DurableTransferPhase.Intent,f.Journal.Read()[0].Phase);
    }
    [Fact] public void StopOfInterruptedActivePhaseReleasesStateEditBlockOnlyForThatRow()
    {
        using var f=new F();var p=f.Stage(f.File("active.txt"));f.Journal.Claim(p.Plan.Request.OperationId,0);
        var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("edit.txt")).Item!);
        Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void ClosureNeverRewritesExistingSharedContentOrCards()
    {
        using var f=new F();string path=f.File("reference.txt");f.AddReference(LocalContentReferenceProbe.Inspect(path).Item!);
        var p=f.Stage(path);File.Copy(path,p.Files[0].Destination);
        string before=ContentCatalogCodec.Write(f.State.ContentCatalog);var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);
        f.Journal.ConfirmStop(stop,stop.Fingerprint);Assert.Equal(before,ContentCatalogCodec.Write(f.State.ContentCatalog));
    }
    [Fact] public void CompletedMoveCannotBeReclassifiedAsStopped()
    {
        using var f=new F();var p=f.Stage(f.File("done.txt"));f.Executor.Execute(p.Plan.Request.OperationId);
        Assert.Throws<IOException>(()=>f.Journal.PreviewStop(p.Plan.Request.OperationId,0));Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_transfer_closures"));
    }
    [Fact] public void ClosureDoesNotPermitRestoringAnOldDatabaseToEraseFileHistory()
    {
        using var f=new F();var backup=f.Store.CreateBackup();Assert.True(backup.Succeeded,backup.Error);
        var p=f.Stage(f.File("guard.txt"));var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        f.Store.Dispose();Assert.False(new StateBackupRecovery(f.Paths,()=>true).Restore(backup.FileName!).Succeeded);
        Assert.True(File.Exists(p.Files[0].Source));
    }
    [Theory] [InlineData("format")] [InlineData("evidence")]
    public void UnknownClosureOrMismatchedEvidenceFailClosed(string change)
    {
        using var f=new F();var p=f.Stage(f.File("unknown.txt"));var stop=f.Journal.PreviewStop(p.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        f.Sql(change=="format"?"UPDATE content_transfer_closures SET body_json=json_set(body_json,'$.FormatVersion',999)":"UPDATE content_transfer_items SET error='changed'");
        Assert.Throws<InvalidDataException>(()=>f.Executor.Execute(p.Plan.Request.OperationId));Assert.True(File.Exists(p.Files[0].Source));
    }
    [Fact] public void Schema16UpgradePreservesOldChecksumsAndPendingEvidence()
    {
        using var f=new F();var p=f.Stage(f.File("migration.txt"));var old=Assert.Single(f.Journal.Read());
        f.Sql(StorageSchemaFixture.Drop19+"DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DELETE FROM schema_migrations WHERE version>=17; UPDATE state_head SET schema_version=16; PRAGMA user_version=16;");
        var checksums=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations ORDER BY version");
        var result=f.Store.Initialize();Assert.True(result.Succeeded,result.Error);Assert.Equal(16,result.PreviousSchemaVersion);Assert.Equal(19,result.Head!.SchemaVersion);
        Assert.Equal(checksums,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=16 ORDER BY version"));Assert.Equal(StateRequestFingerprint.Compute(old),StateRequestFingerprint.Compute(Assert.Single(f.Journal.Read())));
        Assert.True(File.Exists(p.Files[0].Source));Assert.NotEmpty(Directory.GetFiles(f.Paths.Backups,"*.sqlite3"));
    }
    [Fact] public void ClosureMigrationFailureLeavesSchema16AndFilesIntact()
    {
        using var f=new F();var p=f.Stage(f.File("migration-fail.txt"));
        f.Sql(StorageSchemaFixture.Drop19+"DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DELETE FROM schema_migrations WHERE version>=17; UPDATE state_head SET schema_version=16; PRAGMA user_version=16; CREATE TABLE storage_provision_closures(conflict TEXT);");
        Assert.False(f.Store.Initialize().Succeeded);Assert.Equal(16L,f.Sql("PRAGMA user_version"));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM sqlite_master WHERE name='content_transfer_closures'"));Assert.True(File.Exists(p.Files[0].Source));
    }
}
