using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using DesktopUp.Domain;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageServiceTests
{
    private static StorageLocationUseCase Service(F f,SqliteStateStore? store=null,string? storage=null,Action<string>? checkpoint=null)=>
        Create(store??f.Store,storage??Path.Combine(f.DirectoryRoot,"defaults"),checkpoint);
    private static StorageLocationUseCase Create(SqliteStateStore store,string? storage,Action<string>? checkpoint=null)=>
        new(new SqliteStorageLocationStorage(store,StorageProfileKind.Isolated,"C:/unused-local",storage,checkpoint),
            new StorageRelocationUseCase(new LocalStorageRelocationExecution(store)),checkpoint);
    private static SqliteStateStore Empty(F f,string suffix="empty")
    {var store=new SqliteStateStore(new UserDataPaths(Path.Combine(f.DirectoryRoot,suffix)));Assert.True(store.Initialize().Succeeded);
     new SqliteStateCommandRepository(store).EnsureInitialized(PosterConfigurationFactory.NewBasicBox("settings","DISPLAY1"));return store;}
    private static string Destination(F f){string path=Path.Combine(f.DirectoryRoot,"destination");Directory.CreateDirectory(path);return path;}
    [Fact] public void ReadingEmptySettingsIsLazyAndFirstUseIsDurableAndIdempotent()
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store);var before=new SqliteStateCommandRepository(store).Read();
        var view=service.Read();Assert.False(view.Exists);Assert.False(Directory.Exists(view.Path));
        Assert.Equal(before.Revision,new SqliteStateCommandRepository(store).Read().Revision);
        var ready=service.EnsureReady();Assert.Equal(view.Path,ready.Root.Locator);Assert.True(service.Read().Exists);
        var after=new SqliteStateCommandRepository(store).Read();Assert.Equal(ready,service.EnsureReady());Assert.Equal(after.Revision,new SqliteStateCommandRepository(store).Read().Revision);
        Assert.Equal(ready.Root.Id,new SqliteStorageRelocationJournal(store).ReadLocation().Selection.ActiveRootId);
    }
    [Fact] public void PhysicalProfilesGetDistinctDefaultsAndOldRootIsNeverMovedOnUse()
    {
        using var f=new F();using var one=Empty(f,"one");using var two=Empty(f,"two");
        Assert.NotEqual(Service(f,one).EnsureReady().Root.Locator,Service(f,two).EnsureReady().Root.Locator);
        var source=f.File("original.txt");var old=Service(f).EnsureReady();Assert.Equal(f.Root,old.Root);Assert.True(File.Exists(source));
        Assert.False(Directory.Exists(Service(f).ProposedDefaultPath));
    }
    [Fact] public void MissingIsolationNeverFallsBackToDailyStorage()
    {
        using var f=new F();using var store=Empty(f);var service=Create(store,null);
        Assert.Throws<ArgumentException>(()=>service.Read());Assert.Throws<ArgumentException>(()=>service.EnsureReady());
        Assert.Empty(new SqliteTransferJournal(store).StorageLocations());
    }
    [Theory] [InlineData("profile")] [InlineData("temp")]
    public void DefaultAndChosenLocationsRejectProtectedPaths(string path)
    {
        using var f=new F();using var store=Empty(f);string target=path=="profile"?Path.Combine(f.DirectoryRoot,"empty"):Path.GetTempPath();
        Assert.ThrowsAny<Exception>(()=>Service(f,store,target).EnsureReady());
        Assert.ThrowsAny<Exception>(()=>Service(f,store).PreviewChange(target));
    }
    [Fact] public void UnclaimedDefaultAndReplacedOwnedRootNeverAdoptOrFallback()
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store);string path=service.ProposedDefaultPath;
        Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"foreign.txt"),"untouched");
        Assert.Throws<IOException>(()=>service.EnsureReady());Assert.Equal("untouched",File.ReadAllText(Path.Combine(path,"foreign.txt")));
        Service(f).EnsureReady();Directory.Move(f.Root.Locator,f.Root.Locator+"-old");Directory.CreateDirectory(f.Root.Locator);
        Assert.Throws<IOException>(()=>Service(f).Read());Assert.Throws<IOException>(()=>Service(f).EnsureReady());
    }
    [Theory] [InlineData("IntentWritten")] [InlineData("ReceiptWritten")] [InlineData("Registered")]
    public void InterruptedDefaultSetupCanContinueOnlyFromDurableIdentity(string checkpoint)
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store,checkpoint:point=>{if(point==checkpoint)throw new IOException("injected setup loss");});
        Assert.Throws<IOException>(()=>service.EnsureReady());
        var fresh=Service(f,store);var ready=fresh.EnsureReady();Assert.True(fresh.Read().Exists);Assert.Equal(ready,fresh.EnsureReady());
    }
    [Fact] public void UnreceiptedDirectoryCreationIsPreservedNotGuessed()
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store,checkpoint:point=>{if(point=="DirectoryCreated")throw new IOException("loss before receipt");});
        Assert.Throws<IOException>(()=>service.EnsureReady());Assert.True(Directory.Exists(service.ProposedDefaultPath));
        Assert.Throws<IOException>(()=>Service(f,store).EnsureReady());Assert.Empty(new SqliteTransferJournal(store).StorageLocations());
    }
    [Fact] public void FreshExplicitLocationNeedsConfirmationAndRegistersNoForeignContent()
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store);string target=Destination(f);var preview=service.PreviewChange(target);
        Assert.Empty(Directory.GetFileSystemEntries(target));Assert.Equal(0,preview.Files);
        Assert.Throws<IOException>(()=>service.Change(preview,"wrong"));
        Assert.True(service.Change(preview,preview.Fingerprint).Committed);Assert.Equal(target,service.Read().Path);
        Assert.False(Directory.Exists(service.ProposedDefaultPath));
    }
    [Fact] public void ExistingMultipleRootsRequireSelectionWithoutMerging()
    {
        using var f=new F();string target=Destination(f);var provision=new ManagedStorageProvisioner(f.Store);var p=provision.Preview(target);var second=provision.Confirm(p,p.Fingerprint);
        var service=Service(f);Assert.True(service.Read().NeedsSelection);Assert.Throws<IOException>(()=>service.EnsureReady());
        service.SelectExisting(second.Root.Id);Assert.Equal(second,service.EnsureReady());Assert.True(Directory.Exists(f.Collection.StorageLocator));
    }
    [Fact] public void ProductPreviewCountsPayloadNotMarkersAndMigratesWithoutChangingRelationships()
    {
        using var f=new F();var staged=f.Stage(f.File("managed.txt"));f.Executor.Execute(staged.Plan.Request.OperationId);
        Directory.CreateDirectory(Path.Combine(f.Collection.StorageLocator!,"empty-folder"));var before=f.State;var service=Service(f);var p=service.PreviewChange(Destination(f));
        Assert.Equal(1,p.Files);Assert.Equal(1,p.Directories);Assert.Equal("owned-content"u8.Length,p.Bytes);Assert.Single(p.Posters);
        var result=service.Change(p,p.Fingerprint);Assert.True(result.Committed);Assert.True(result.Cleaned,result.Message);
        Assert.Equal(before.Revision+1,f.State.Revision);Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.Equal(p.Destination,service.Read().Path);Assert.Equal("owned-content",File.ReadAllText(f.State.ContentCatalog.Items.Single().Target.Locator));
    }
    [Fact] public void EmptyCollectionChangesThroughRealProtocolAndCancellationPreservesRoot()
    {
        using var f=new F();var service=Service(f);string target=Destination(f);var p=service.PreviewChange(target);using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>service.Change(p,p.Fingerprint,cancel.Token));Assert.Equal(f.Root.Locator,service.Read().Path);Assert.Empty(Directory.GetFileSystemEntries(target));
        Assert.True(service.Change(p,p.Fingerprint).Committed);Assert.Equal(target,service.Read().Path);
    }
    [Fact] public void StaleOrOccupiedPreviewCannotOverwriteFiles()
    {
        using var f=new F();var service=Service(f);string target=Destination(f);var p=service.PreviewChange(target);File.WriteAllText(Path.Combine(target,"external.txt"),"foreign");
        Assert.Throws<IOException>(()=>service.Change(p,p.Fingerprint));Assert.Equal(f.Root.Locator,service.Read().Path);
        Assert.Equal("foreign",File.ReadAllText(Path.Combine(target,"external.txt")));Assert.Throws<IOException>(()=>service.PreviewChange(target));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void StartupSettlesOnlyExistingWorkAndNeverReplaysCopy(bool committed)
    {
        using var f=new F();var staged=f.Stage(f.File("restart.txt"));f.Executor.Execute(staged.Plan.Request.OperationId);string old=f.State.ContentCatalog.Items.Single().Target.Locator;
        var journal=new SqliteStorageRelocationJournal(f.Store);var plan=journal.Preview(Destination(f));var files=new StorageRelocationFiles(f.Store);files.Prepare(plan,plan.Fingerprint);
        if(committed){journal.RecordTargets(plan.Request.OperationId,files);journal.Commit(plan.Request.OperationId,files);}
        var service=Service(f);var result=Assert.Single(service.Reconcile());Assert.Equal(committed,result.Committed);Assert.True(result.Cleaned);
        Assert.Equal(!committed,File.Exists(old));Assert.Empty(service.Reconcile());
        Assert.Equal("owned-content",File.ReadAllText(f.State.ContentCatalog.Items.Single().Target.Locator));
    }
    [Fact] public void InitialDestinationChangedAfterConfirmationIsNotClaimed()
    {
        using var f=new F();using var store=Empty(f);var service=Service(f,store);string target=Destination(f);var p=service.PreviewChange(target);
        File.WriteAllText(Path.Combine(target,"foreign"),"untouched");Assert.Throws<IOException>(()=>service.Change(p,p.Fingerprint));
        Assert.Empty(new SqliteTransferJournal(store).StorageLocations());Assert.Single(Directory.GetFileSystemEntries(target));
    }
    [Fact] public void ReopeningProfileRetainsDefaultOwnershipAndDisposingDoesNotDeleteContents()
    {
        using var f=new F();string profile=Path.Combine(f.DirectoryRoot,"restart-profile"),root;
        using(var first=Empty(f,"restart-profile")){var ready=Service(f,first).EnsureReady();root=ready.Root.Locator;File.WriteAllText(Path.Combine(ready.Collection.StorageLocator!,"keep.txt"),"keep");}
        using var second=new SqliteStateStore(new UserDataPaths(profile));Assert.True(second.Initialize().Succeeded);
        Assert.Equal(root,Service(f,second).EnsureReady().Root.Locator);Assert.Single(Directory.GetFiles(root,"keep.txt",SearchOption.AllDirectories));
    }
    [Fact] public void ReferenceFilesOutsideStorageAreNotMoved()
    {
        using var f=new F();string reference=f.File("reference-only.txt");f.AddReference(LocalContentReferenceProbe.Inspect(reference).Item!);
        var service=Service(f);var p=service.PreviewChange(Destination(f));Assert.Equal(0,p.Files);Assert.Empty(p.Posters);
        Assert.True(service.Change(p,p.Fingerprint).Committed);Assert.True(File.Exists(reference));Assert.Equal(reference,f.State.ContentCatalog.Items.Single().Target.Locator);
    }
    [Fact] public void UnboundLegacyRootCannotBeReplacedByANewDefault()
    {
        using var f=new F();f.Sql("DELETE FROM managed_storage_collections");
        Assert.Throws<IOException>(()=>Service(f).EnsureReady());Assert.False(Directory.Exists(Service(f).ProposedDefaultPath));
    }
    [Fact] public void InstallerUninstallRemainsManifestBoundAndNeverRecursivelyDeletesStorage()
    {
        using var f=new F();string repo=f.DirectoryRoot;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)!;
        string installer=File.ReadAllText(Path.Combine(repo,"packaging/preview/installer.nsi"));
        string evidence=File.ReadAllText(Path.Combine(repo,"packaging/preview/uninstall-evidence.nsh"));
        string cleanupSource=installer+"\n"+evidence;
        Assert.DoesNotContain("RMDir /r",cleanupSource,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("${UNINSTALL_FILES}",installer);
        Assert.Contains("DeleteFileW(w \"$INSTDIR\\$UninstallFailureFile\") i.r2 ?e",evidence);
        Assert.Single(evidence.Split('\n').Where(line=>line.Contains("::DeleteFileW(")));
        Assert.DoesNotContain("/REBOOTOK",cleanupSource,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("::RmShutdown",evidence,StringComparison.OrdinalIgnoreCase);
        // The directory page may explain where user data lives; only executable cleanup
        // statements are forbidden from targeting it.
        var cleanup = string.Join("\n", cleanupSource.Split('\n').Where(line =>
            System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*(Delete|RMDir)\s", System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
        Assert.DoesNotContain("DesktopUp\\Storage",cleanup,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$LOCALAPPDATA",cleanup,StringComparison.OrdinalIgnoreCase);
        Assert.Throws<IOException>(()=>Service(f).PreviewChange(f.Paths.Temp));
        Assert.True(Directory.Exists(f.Collection.StorageLocator));
    }
}
