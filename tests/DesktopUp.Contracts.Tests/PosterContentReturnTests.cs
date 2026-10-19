using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterContentReturnTests
{
    private sealed class Fake(Func<string,string,ShellDirectoryResult> action) : IShellDirectoryTransfer
    {
        public int Calls;
        public Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner, Func<bool> current, CancellationToken token)
        { Assert.False(copy); Assert.True(current()); Calls++; return Task.FromResult(action(source, destination)); }
    }
    private static ShellDirectoryResult Move(string from, string directory)
    {
        var to = Path.Combine(directory, Path.GetFileName(from));
        if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
        return new(to, false, 0);
    }
    private static RemovePosterCommand Command(F f, string? id = null)
    {
        var state = f.State; id ??= f.Main;
        return new(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, state.Revision,
            StateReadSet.ForPoster(id, state.Catalog.Posters.Single(p => p.Poster.PosterId == id).EntityVersion)
                with { CatalogVersion = state.Catalog.CatalogVersion, ContentVersion = state.ContentVersion }, id, true);
    }
    private static string Collect(F f, string name = "file.txt")
    {
        string source = f.File(name); var p = f.Stage(source);
        Assert.Equal(DurableTransferPhase.IndexCommitted, Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        return source;
    }
    private static string Desktop(F f) { var path = Path.Combine(f.DirectoryRoot, "owned-desktop"); Directory.CreateDirectory(path); return path; }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ReturnToOriginThenRemoveAndRestart(bool realShell)
    {
        using var f = new F(); var original = Collect(f); var id = Assert.Single(f.State.ContentCatalog.Items).Id;
        var service = new PosterContentReturn(f.Store, realShell ? null : new Fake(Move), Desktop(f));
        Assert.Equal(original, service.OriginalPath(id));
        var result = await service.Execute(Command(f), 0, () => true);
        Assert.True(result.Removed, string.Join(";", result.Errors)); Assert.Equal(1, result.Returned);
        Assert.Equal("owned-content", File.ReadAllText(original)); Assert.Empty(f.State.Catalog.Posters); Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Empty(f.State.ContentCatalog.Memberships.Where(m => m.Origin == ContentMembershipOrigin.Source));
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        Assert.Empty(new PosterContentReturn(reopened).Reconcile()); Assert.Empty(new SqliteStateCommandRepository(reopened).Read().Catalog.Posters);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MissingOriginOrUnknownNewFileFallsBackToOwnedDesktop(bool unknown)
    {
        using var f = new F(); Collect(f); var desktop = Desktop(f);
        if (unknown) { f.Sql("DELETE FROM content_return_origins; DELETE FROM content_transfer_items; DELETE FROM content_transfers;"); }
        else Directory.Delete(f.Source);
        var result = await new PosterContentReturn(f.Store, new Fake(Move), desktop).Execute(Command(f), 0, () => true);
        Assert.True(result.Removed, string.Join(";", result.Errors)); Assert.True(File.Exists(Path.Combine(desktop, "file.txt")));
    }
    [Fact] public async Task KnownAccessDeniedFallsBackButCancellationDoesNot()
    {
        using var f = new F(); Collect(f); var desktop = Desktop(f);
        var fake = new Fake((source, dest) => dest == f.Source ? new(null, false, unchecked((int)0x80070005)) : Move(source, dest));
        var result = await new PosterContentReturn(f.Store, fake, desktop).Execute(Command(f), 0, () => true);
        Assert.True(result.Removed, string.Join(";", result.Errors)); Assert.Equal(2, fake.Calls); Assert.True(File.Exists(Path.Combine(desktop, "file.txt")));
    }
    [Fact] public async Task PartialCancelRetainsRemainingPosterAndRetryDoesNotRepeatCompletedMove()
    {
        using var f = new F(); var a = Collect(f, "a.txt"); var b = Collect(f, "b.txt"); int calls = 0;
        var service = new PosterContentReturn(f.Store, new Fake((source,dest) => ++calls == 1 ? Move(source,dest) : new(null,true,0)), Desktop(f));
        var result = await service.Execute(Command(f), 0, () => true);
        Assert.False(result.Removed); Assert.Equal(1, result.Returned); Assert.Single(f.State.ContentCatalog.PosterItems); Assert.Single(f.State.Catalog.Posters);
        var retry = await new PosterContentReturn(f.Store, new Fake(Move), Desktop(f)).Execute(Command(f), 0, () => true);
        Assert.True(retry.Removed, string.Join(";", retry.Errors)); Assert.Equal(1, retry.Returned); Assert.True(File.Exists(a)); Assert.True(File.Exists(b));
    }
    [Fact] public async Task SystemRenamedCollisionResultIsRecordedWithoutOverwritingExistingFile()
    {
        using var f = new F(); var original = Collect(f); File.WriteAllText(original, "unrelated");
        var result = await new PosterContentReturn(f.Store, new Fake((source,dir) =>
        { var to = Path.Combine(dir,"file (2).txt"); File.Move(source,to); return new(to,false,0); }), Desktop(f)).Execute(Command(f),0,()=>true);
        Assert.True(result.Removed, string.Join(";", result.Errors)); Assert.Equal("unrelated",File.ReadAllText(original));
        Assert.Equal("owned-content",File.ReadAllText(Assert.Single(result.Destinations)));
    }
    [Fact] public async Task UnusableDesktopRetainsCardAndNeverMovesFile()
    {
        using var f = new F(); Collect(f); Directory.Delete(f.Source); var fake = new Fake(Move);
        var result = await new PosterContentReturn(f.Store, fake, Path.Combine(f.DirectoryRoot,"absent")).Execute(Command(f),0,()=>true);
        Assert.False(result.Removed); Assert.Equal(0,fake.Calls); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.True(File.Exists(Assert.Single(f.State.ContentCatalog.Items).Target.Locator));
    }
    [Fact] public async Task MappingRemovalLeavesDirectoryAndFilesUntouched()
    {
        using var f = new F(); var path = f.File("mapped.txt"); var id = CrossPosterTransferTests.AddPoster(f,"map",f.Source);
        var fake = new Fake(Move); var result = await new PosterContentReturn(f.Store,fake,Desktop(f)).Execute(Command(f,id),0,()=>true);
        Assert.True(result.Removed,string.Join(";",result.Errors)); Assert.Equal(0,fake.Calls); Assert.True(File.Exists(path)); Assert.True(Directory.Exists(f.Source));
    }
    [Fact] public async Task ExternalReferenceRemovalDoesNotMoveItsOriginal()
    {
        using var f = new F(); var path = f.File("reference.txt"); f.AddReference(LocalContentReferenceProbe.Inspect(path).Item!);
        var fake = new Fake(Move); var result = await new PosterContentReturn(f.Store,fake,Desktop(f)).Execute(Command(f),0,()=>true);
        Assert.True(result.Removed,string.Join(";",result.Errors)); Assert.Equal(0,fake.Calls); Assert.True(File.Exists(path));
    }
    [Fact] public async Task OriginSurvivesCrossPosterAndRename()
    {
        using var f = new F(); var original = Collect(f); var item = Assert.Single(f.State.ContentCatalog.Items);
        string target = CrossPosterTransferTests.AddPoster(f,"target"); var card = Assert.Single(f.State.ContentCatalog.PosterItems);
        var intent = new NativeShellExitJournal(f.Store).Begin(f.Main,[card.LayoutItemId]); new NativeShellExitJournal(f.Store).End(intent,false);
        var moved = await new CrossPosterTransfer(f.Store,new Fake(Move)).Execute(intent,target,[item.Target.Locator],new(f.Root,f.Collection),null,0,()=>true);
        Assert.Empty(moved.Errors); var next = Assert.Single(f.State.ContentCatalog.PosterItems);
        new ContentRenameService(f.Store).Rename(target,next.LayoutItemId,item.Id,item.Version,"renamed.txt");
        var service = new PosterContentReturn(f.Store,new Fake(Move),Desktop(f)); Assert.Equal(original,service.OriginalPath(item.Id));
        var result = await service.Execute(Command(f,target),0,()=>true);
        Assert.True(result.Removed,string.Join(";",result.Errors)); Assert.True(File.Exists(Path.Combine(f.Source,"renamed.txt")));
    }
    [Fact] public async Task HistoricCommittedReceiptBackfillsButNoReceiptCannotInventOrigin()
    {
        using var f = new F(); var original = Collect(f); f.Sql("DELETE FROM content_return_origins");
        var result = await new PosterContentReturn(f.Store,new Fake(Move),Desktop(f)).Execute(Command(f),0,()=>true);
        Assert.True(result.Removed,string.Join(";",result.Errors)); Assert.True(File.Exists(original));
    }
    [Fact] public async Task RecordedResultReconcilesIndexOnlyAndDoesNotAutoDeletePoster()
    {
        using var f = new F(); var original = Collect(f); var fake = new Fake(Move);
        var service = new PosterContentReturn(f.Store,fake,Desktop(f)) { Checkpoint = point => { if(point=="ResultRecorded")throw new IOException("injected index interruption"); } };
        Assert.False((await service.Execute(Command(f),0,()=>true)).Removed); Assert.Single(f.State.ContentCatalog.PosterItems); Assert.True(File.Exists(original));
        Assert.Empty(new PosterContentReturn(f.Store,fake,Desktop(f)).Reconcile()); Assert.Equal(1,fake.Calls);
        Assert.Empty(f.State.ContentCatalog.PosterItems); Assert.Single(f.State.Catalog.Posters);
    }
    [Fact] public async Task StaleConsentOrCancellationCannotMoveAnything()
    {
        using var f = new F(); Collect(f); var fake = new Fake(Move); var service = new PosterContentReturn(f.Store,fake,Desktop(f));
        Assert.False((await service.Execute(Command(f) with { Confirmed=false },0,()=>true)).Removed);
        Assert.False((await service.Execute(Command(f) with { ReadSet=Command(f).ReadSet with {ContentVersion=0}},0,()=>true)).Removed);
        Assert.False((await service.Execute(Command(f),0,()=>false)).Removed); Assert.Equal(0,fake.Calls);
    }
    [Fact] public void DirectConfigurationDeletionCannotOrphanManagedCards()
    {
        using var f = new F(); Collect(f);
        var coordinator = new StateProjectionCoordinator(f.Repository); coordinator.Refresh();
        var result = coordinator.Execute(Command(f));
        Assert.False(result.Succeeded); Assert.Single(f.State.ContentCatalog.PosterItems); Assert.True(File.Exists(Assert.Single(f.State.ContentCatalog.Items).Target.Locator));
    }
    [Fact] public void SchemaTwentyFourMigrationPreservesHistoryAndBackfillsProvenSource()
    {
        using var f = new F(); var original = Collect(f); var item = Assert.Single(f.State.ContentCatalog.Items);
        f.Sql(StorageSchemaFixture.Drop25 + "UPDATE state_head SET schema_version=24; PRAGMA user_version=24;");
        var history=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations ORDER BY version");
        var result=f.Store.Initialize(); Assert.True(result.Succeeded,result.Error); Assert.Equal(24,result.PreviousSchemaVersion);
        Assert.Equal(25,result.Head!.SchemaVersion); Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(history,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=24 ORDER BY version"));
        var service=new PosterContentReturn(f.Store); Assert.Empty(service.Reconcile()); Assert.Equal(original,service.OriginalPath(item.Id));
        Assert.True(File.Exists(item.Target.Locator)); Assert.False(File.Exists(original));
    }
    [Fact] public async Task FolderContentsReturnTogetherWithoutFlattening()
    {
        using var f = new F(); var source=Path.Combine(f.Source,"folder"); Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"child.txt"),"child"); var p=f.Stage(source);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).Phase);
        var result=await new PosterContentReturn(f.Store,new Fake(Move),Desktop(f)).Execute(Command(f),0,()=>true);
        Assert.True(result.Removed,string.Join(";",result.Errors)); Assert.Equal("child",File.ReadAllText(Path.Combine(source,"child.txt")));
    }
    [Fact] public async Task InPosterNewFileHasNoInventedSourceAndReturnsToDesktop()
    {
        using var f=new F(); var state=f.State;
        new ContentCreationService(f.Store).Create(f.Main,new(new(f.Root,f.Collection),state.ContentVersion,state.Catalog.Posters.Single().EntityVersion),NewContentKind.TextFile,"new.txt");
        var desktop=Desktop(f);var service=new PosterContentReturn(f.Store,new Fake(Move),desktop);
        Assert.Null(service.OriginalPath(Assert.Single(f.State.ContentCatalog.Items).Id));
        var result=await service.Execute(Command(f),0,()=>true);Assert.True(result.Removed,string.Join(";",result.Errors));Assert.True(File.Exists(Path.Combine(desktop,"new.txt")));
    }
    [Fact] public async Task SourceReplacementStopsMoveWithoutLosingCard()
    {
        using var f=new F(); Collect(f); string source=Assert.Single(f.State.ContentCatalog.Items).Target.Locator;
        var fake=new Fake(Move); var service=new PosterContentReturn(f.Store,fake,Desktop(f))
        { Checkpoint=point=>{if(point=="Prepared"){File.Move(source,source+".preserved");File.WriteAllText(source,"replacement");}} };
        Assert.False((await service.Execute(Command(f),0,()=>true)).Removed);
        Assert.Equal(0,fake.Calls);Assert.Single(f.State.ContentCatalog.PosterItems);Assert.Equal("replacement",File.ReadAllText(source));
    }
}
