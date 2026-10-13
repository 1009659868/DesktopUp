using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedContentSyncTests
{
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; public void Advance() => Now += TimeSpan.FromSeconds(3); }
    private static string Collect(F f, string name = "collected.txt")
    {
        var p = f.Stage(f.File(name)); var row = Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId));
        Assert.True(row.Phase == DurableTransferPhase.IndexCommitted, row.Error); return p.Files[0].Destination;
    }
    private static ManagedContentSyncResult Settle(ManagedContentSynchronizer sync, Clock clock)
    { sync.Reconcile(); clock.Advance(); return sync.Reconcile(); }

    [Fact] public void ExternalMoveRemovesCardsAndManagedMembershipWithoutTouchingMovedFileAndRestartStaysEmpty()
    {
        using var f = new F(); string path = Collect(f); var original = f.State; string outside = Path.Combine(f.Source,"on-desktop.txt");
        File.Move(path,outside); var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock);
        Assert.False(sync.Reconcile().Changed); Assert.Single(f.State.ContentCatalog.PosterItems);
        clock.Advance(); var result = sync.Reconcile(); Assert.Equal(1,result.RemovedCards); Assert.Empty(result.Notices);
        Assert.Empty(f.State.ContentCatalog.PosterItems); Assert.Single(f.State.ContentCatalog.Items);
        Assert.Single(f.State.ContentCatalog.Memberships); Assert.Equal(original.Revision+1,f.State.Revision);
        Assert.Equal("owned-content",File.ReadAllText(outside)); Assert.False(File.Exists(path));
        Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single().Poster.ContentLayoutJson,f.Main,out var layout,out _));
        Assert.Empty(layout!.Placements); Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_content_observations"));
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        Assert.False(Settle(new(reopened,clock),clock).Changed); Assert.Empty(new SqliteStateCommandRepository(reopened).Read().ContentCatalog.PosterItems);
    }
    [Fact] public void RenameWhileClosedRetainsEntityCardLayoutAndUpdatesOpeningPath()
    {
        using var f = new F(); string path = Collect(f); var before = f.State;
        string renamed = Path.Combine(f.Collection.StorageLocator!,"renamed.txt"); File.Move(path,renamed);
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        var clock = new Clock(); var result = Settle(new(reopened,clock),clock);
        Assert.Equal(1,result.RenamedItems); Assert.Equal(0,result.RemovedCards); Assert.Empty(result.Notices);
        Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.Equal(before.Catalog.Posters.Single().Poster.ContentLayoutJson,f.State.Catalog.Posters.Single().Poster.ContentLayoutJson);
        Assert.Equal(renamed,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
        Assert.Equal(before.ContentCatalog.Items.Single().Version+1,f.State.ContentCatalog.Items.Single().Version);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void OfflineOrReplacedRootDoesNotMeanAllFilesWereDeleted(bool replacement)
    {
        using var f = new F(); Collect(f); var before = f.State; string away = f.Root.Locator+"-offline";
        Directory.Move(f.Root.Locator,away);
        if(replacement)Directory.CreateDirectory(f.Root.Locator);
        var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock);
        Assert.False(Settle(sync,clock).Changed); Assert.Equal(before.Revision,f.State.Revision);
        if(replacement)Directory.Delete(f.Root.Locator); Directory.Move(away,f.Root.Locator);
        Assert.False(Settle(sync,clock).Changed); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void TransientMoveBackAndFinalEnumerationRaceDoNotRemoveCard()
    {
        using var f = new F(); string path = Collect(f); string away = Path.Combine(f.Source,"temporary.txt"); File.Move(path,away);
        var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock); Assert.True(sync.Reconcile().Pending);
        sync.BeforeCommit = () => File.Move(away,path); clock.Advance(); Assert.False(sync.Reconcile().Changed);
        Assert.Single(f.State.ContentCatalog.PosterItems); sync.BeforeCommit = null; Assert.False(Settle(sync,clock).Changed);
    }
    [Fact] public void IncompleteScanAndUnrelatedLockedFilePreserveAllRelations()
    {
        using var f = new F(); string path = Collect(f); File.Move(path,Path.Combine(f.Source,"outside.txt"));
        string blocked = Path.Combine(f.Collection.StorageLocator!,"unrelated.txt"); File.WriteAllText(blocked,"unmanaged");
        var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock);
        using(var held = new FileStream(blocked,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        { Assert.False(Settle(sync,clock).Changed); Assert.Single(f.State.ContentCatalog.PosterItems); }
        sync.Enumerating = _ => throw new UnauthorizedAccessException("injected incomplete enumeration");
        Assert.False(Settle(sync,clock).Changed); Assert.Single(f.State.ContentCatalog.PosterItems);
        sync.Enumerating = null; Assert.Equal(1,Settle(sync,clock).RemovedCards); Assert.Equal("unmanaged",File.ReadAllText(blocked));
    }
    [Fact] public void OwnTransferLeaseAndPendingIntentDeferObservation()
    {
        using var f = new F(); string path = Collect(f); File.Move(path,Path.Combine(f.Source,"outside.txt"));
        var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock);
        using(f.Journal.AcquireExecutor())Assert.False(Settle(sync,clock).Changed);
        var pending = f.Stage(f.File("another.txt")); Assert.False(Settle(sync,clock).Changed);
        Assert.Single(f.State.ContentCatalog.PosterItems);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); f.Executor.Execute(pending.Plan.Request.OperationId,cancel.Token);
        Assert.Equal(1,Settle(sync,clock).RemovedCards);
    }
    [Fact] public void ManagedRemovalKeepsDifferentSourceWithSameFilename()
    {
        using var f=new F(); string path=Collect(f);
        var independent=LocalContentReferenceProbe.Inspect(f.File("collected.txt")).Item!;
        f.AddReference(independent);
        var independentCard=f.State.ContentCatalog.PosterItems.Single(p=>ContentRelationPolicy.Resolve(f.State.ContentCatalog,p.PosterId,p.LayoutItemId)!.Item.Id==independent.Id);
        File.Move(path,Path.Combine(f.Source,"returned.txt")); var clock=new Clock();
        Assert.Equal(1,Settle(new(f.Store,clock),clock).RemovedCards);
        Assert.Equal(independentCard,Assert.Single(f.State.ContentCatalog.PosterItems));
    }
    [Fact] public void SamePathReplacementIsNotMistakenForDeletionOrRename()
    {
        using var f = new F(); string path = Collect(f); File.Move(path,Path.Combine(f.Source,"old.txt")); File.WriteAllText(path,"replacement");
        var clock = new Clock(); Assert.False(Settle(new(f.Store,clock),clock).Changed); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Equal("replacement",File.ReadAllText(path));
    }
    [Fact] public void CasAndDatabaseFailureNeverPartiallyPrune()
    {
        using var f = new F(); string path = Collect(f); File.Move(path,Path.Combine(f.Source,"outside.txt"));
        var clock = new Clock(); var sync = new ManagedContentSynchronizer(f.Store,clock); sync.Reconcile(); clock.Advance();
        f.Sql("CREATE TRIGGER sync_fault BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'injected'); END;");
        Assert.Throws<SqliteException>(()=>sync.Reconcile()); Assert.Single(f.State.ContentCatalog.PosterItems);
        f.Sql("DROP TRIGGER sync_fault;"); Assert.Equal(1,sync.Reconcile().RemovedCards);
    }
    [Fact] public void PublicRepositoryCannotForgeExternalRemovalAuthorization()
    {
        using var f = new F(); Collect(f); var s=f.State; var cards=s.ContentCatalog.PosterItems.Select(p=>p.Id).ToArray();
        var plan=ContentRelationRemoval.Prepare(s,cards) with {ExternalSyncReceiptId=Guid.NewGuid()};
        var reads=StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion) with {ContentVersion=s.ContentVersion};
        Assert.Equal(StateCommandStatus.ValidationFailed,f.Repository.Execute(new("forged","forged",s.Revision,reads),_=>plan).Status);
        Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task ForegroundExecutorWaitsForObserverButStillRejectsAnotherFileExecutor()
    {
        using var f=new F();using var acquired=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var observer=Task.Run(()=>{using var lease=f.Journal.AcquireObservationExecutor();acquired.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(5)));});
        Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
        var foreground=Task.Run(()=>{using var lease=f.Journal.AcquireExecutor();Assert.Throws<IOException>(()=>f.Journal.AcquireExecutor());});
        try{await Task.Delay(50);Assert.False(foreground.IsCompleted);}finally{release.Set();}
        await Task.WhenAll(observer,foreground).WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact] public void NewIntentBetweenObservationAndCommitTakesPriority()
    {
        using var f=new F();string path=Collect(f);File.Move(path,Path.Combine(f.Source,"outside.txt"));
        var clock=new Clock();var sync=new ManagedContentSynchronizer(f.Store,clock);
        sync.Reconcile();clock.Advance();sync.BeforeCommit=()=>f.Stage(f.File("priority.txt"));
        Assert.False(sync.Reconcile().Changed);Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void UpgradeNineteenPreservesHistoryAndBootstrapsMissingIdentityFromSuccessfulMove()
    {
        using var f = new F(); string path = Collect(f); string history=(string)f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=19")!;
        f.Sql(StorageSchemaFixture.Drop20+"UPDATE state_head SET schema_version=19; PRAGMA user_version=19;");
        File.Move(path,Path.Combine(f.Source,"outside.txt")); var initialized=f.Store.Initialize(); Assert.True(initialized.Succeeded,initialized.Error);
        Assert.Equal(19,initialized.PreviousSchemaVersion); Assert.NotNull(initialized.PreMigrationBackupFileName);
        Assert.Equal(history,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=19"));
        var clock=new Clock(); Assert.Equal(1,Settle(new(f.Store,clock),clock).RemovedCards);
    }
    [Fact] public void FolderRenameAndMoveTrackKnownDescendantsWithoutImportingUnknownFiles()
    {
        using var f=new F();string folder=Path.Combine(f.Source,"folder");Directory.CreateDirectory(folder);
        string child=Path.Combine(folder,"child.txt");File.WriteAllText(child,"nested");
        f.AddReference(LocalContentReferenceProbe.Inspect(child).Item!);
        var incoming=f.Stage(folder);Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(incoming.Plan.Request.OperationId)).Phase);
        var cards=f.State.ContentCatalog.PosterItems.ToArray();string old=incoming.Files[0].Destination;
        string renamed=Path.Combine(f.Collection.StorageLocator!,"renamed-folder");Directory.Move(old,renamed);
        File.WriteAllText(Path.Combine(renamed,"unmanaged.txt"),"do not import");
        var clock=new Clock();var sync=new ManagedContentSynchronizer(f.Store,clock);
        Assert.Equal(2,Settle(sync,clock).RenamedItems);Assert.Equal(cards,f.State.ContentCatalog.PosterItems);Assert.Equal(2,f.State.ContentCatalog.Items.Count);
        Directory.Move(renamed,Path.Combine(f.Source,"returned-folder"));Assert.Equal(2,Settle(sync,clock).RemovedCards);
        Assert.Equal("nested",File.ReadAllText(Path.Combine(f.Source,"returned-folder","child.txt")));
    }
    [Fact] public void LockedPosterStillReflectsKnownRemoval()
    {
        using var f=new F();string path=Collect(f);var s=f.State;
        // Locking layout must not prevent observing physical reality.
        var reads=StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("lock","lock",s.Revision,reads),_=>new(){Updates=[s.Catalog.Posters.Single().Poster with {IsLocked=true}]}).Status);
        File.Move(path,Path.Combine(f.Source,"gone.txt"));var clock=new Clock();Assert.Equal(1,Settle(new(f.Store,clock),clock).RemovedCards);
        Assert.True(f.State.Catalog.Posters.Single().Poster.IsLocked);
    }
    [Fact] public void MissingLegacyContentWithoutAnyIdentityEvidenceIsNotPruned()
    {
        using var f=new F();string path=Collect(f);File.Move(path,Path.Combine(f.Source,"preserved.txt"));
        // Isolated legacy shape: no observer baseline and no successful file receipt to recover it from.
        f.Sql("DELETE FROM managed_content_observations; DELETE FROM content_transfer_items; DELETE FROM content_transfers;");
        var clock=new Clock();var result=Settle(new(f.Store,clock),clock);
        Assert.False(result.Changed);Assert.NotEmpty(result.Notices);Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void SchemaTwentyFailureRollsBackAndPreservesNineteen()
    {
        using var f=new F();f.Sql(StorageSchemaFixture.Drop20+"UPDATE state_head SET schema_version=19;PRAGMA user_version=19;CREATE TABLE managed_content_observations(collision TEXT);");
        var result=f.Store.Initialize();Assert.False(result.Succeeded);Assert.Equal(19L,f.Sql("PRAGMA user_version"));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM schema_migrations WHERE version=20"));Assert.NotEmpty(Directory.GetFiles(f.Paths.Backups,"*.sqlite3"));
    }
    [Fact] public async Task BackgroundWatcherEventuallyUpdatesWithoutControlCenterAndDisposesCleanly()
    {
        using var f = new F(); string path=Collect(f); var completed=new TaskCompletionSource<ManagedContentSyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using(var monitor=new ManagedContentMonitor(f.Store,r=>{if(r.Changed)completed.TrySetResult(r);}))
        { File.Move(path,Path.Combine(f.Source,"outside.txt")); Assert.Equal(1,(await completed.Task.WaitAsync(TimeSpan.FromSeconds(15))).RemovedCards); }
        Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
}
