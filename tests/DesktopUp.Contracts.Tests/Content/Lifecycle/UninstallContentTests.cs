using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class UninstallContentTests
{
    private sealed class ReportProfile : IUninstallProfile
    {
        public string? Outcome;
        public string[] Returned = [], Retained = [], Errors = [];
        public bool OpenExisting() => true;
        public void SaveReport(string outcome, IEnumerable<string> returned, IEnumerable<string> retained, IEnumerable<string> errors)
        { Outcome = outcome; Returned = returned.ToArray(); Retained = retained.ToArray(); Errors = errors.ToArray(); }
    }

    private static UninstallPreparation Prepare(F f, IShellDirectoryTransfer shell, ReportProfile profile)
    {
        var storage = new SqliteContentReturnStorage(f.Store);
        var returns = new ContentReturnUseCase(storage, new LocalContentReturnFiles(f.Store, Desktop(f)), shell);
        return new(profile, returns, new UninstallContentUseCase(storage, returns));
    }

    [Fact]
    public async Task PreparationKeepDoesNotInspectOfflineContentOrReconcilePendingMoves()
    {
        using var f = new F();
        Collect(f, "keep.txt");
        f.Stage(f.File("pending.txt"));
        string path = f.Collection.StorageLocator!;
        Directory.Move(path, path + "-offline");
        var profile = new ReportProfile();
        var shell = new Shell(Move);
        var preparation = Prepare(f, shell, profile);

        await preparation.Execute(keep: true, silent: true, 0, CancellationToken.None);

        Assert.Equal("保留原位", profile.Outcome);
        Assert.Contains(path, profile.Retained);
        Assert.Empty(profile.Returned);
        Assert.Equal(0, shell.Calls);
        Assert.Contains(f.Journal.Read(), x => !x.IsFinished);
    }

    [Fact]
    public async Task PreparationSilentReturnRefusesFilesBeforeShellAndReportsRetainedLocations()
    {
        using var f = new F();
        Collect(f, "kept.txt");
        var profile = new ReportProfile();
        var shell = new Shell(Move);
        var preparation = Prepare(f, shell, profile);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            preparation.Execute(keep: false, silent: true, 0, CancellationToken.None));
        preparation.SaveFailureReport(error.Message);

        Assert.Contains("静默卸载", error.Message);
        Assert.Equal(0, shell.Calls);
        Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Contains(f.Collection.StorageLocator!, profile.Retained);
        Assert.Empty(profile.Returned);
        Assert.Single(profile.Errors);
    }

    [Fact]
    public async Task PreparationFailureReportKeepsCompletedItemsAndRetryOnlyMovesRemainder()
    {
        using var f = new F();
        Collect(f, "a.txt");
        Collect(f, "b.txt");
        int calls = 0;
        var shell = new Shell((source, destination) => ++calls == 1 ? Move(source, destination) : new(null, true, 0));
        var profile = new ReportProfile();
        var preparation = Prepare(f, shell, profile);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            preparation.Execute(keep: false, silent: false, 0, CancellationToken.None));
        preparation.SaveFailureReport(error.Message);
        Assert.Single(profile.Returned);
        Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Equal(1, preparation.ReturnedCount);

        var retryShell = new Shell(Move);
        var retryProfile = new ReportProfile();
        await Prepare(f, retryShell, retryProfile).Execute(keep: false, silent: false, 0, CancellationToken.None);
        Assert.Equal(1, retryShell.Calls);
        Assert.Equal("归还完成", retryProfile.Outcome);
        Assert.Empty(f.State.ContentCatalog.PosterItems);
    }

    private sealed class Shell(Func<string,string,ShellDirectoryResult> run):IShellDirectoryTransfer
    {
        public int Calls;
        public Task<ShellDirectoryResult> Run(string source,string destination,bool copy,nint owner,Func<bool> current,CancellationToken token)
        {Assert.False(copy);Assert.True(current());Calls++;return Task.FromResult(run(source,destination));}
    }
    private static ShellDirectoryResult Move(string source,string directory)
    {string target=Path.Combine(directory,Path.GetFileName(source));if(Directory.Exists(source))Directory.Move(source,target);else File.Move(source,target);return new(target,false,0);}
    private static string Desktop(F f){var p=Path.Combine(f.DirectoryRoot,"desktop");Directory.CreateDirectory(p);return p;}
    private static string Collect(F f,string name){string source=f.File(name);var p=f.Stage(source);f.Executor.Execute(p.Plan.Request.OperationId);return source;}
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ReturnsAllAndKeepsPosterDefinitions(bool realShell)
    {
        using var f=new F();string origin=Collect(f,"origin.txt");var fake=new Shell(Move);
        var service=new ContentReturnFixture(f.Store,realShell?null:fake,Desktop(f));var preview=service.InspectUninstall();
        var result=await service.ReturnForUninstall(preview,0);
        Assert.True(result.Completed,string.Join(";",result.Errors));Assert.True(File.Exists(origin));Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Single(f.State.Catalog.Posters);Assert.Empty(service.InspectUninstall().Files);
        Assert.True((await service.ReturnForUninstall(service.InspectUninstall(),0)).Completed);
        if(!realShell)Assert.Equal(1,fake.Calls);
    }
    [Fact] public async Task RegisteredEntityWithoutCardRetainsOriginalDestination()
    {
        using var f=new F();string origin=Collect(f,"orphan.txt");var state=f.State;
        var plan=ContentRelationRemoval.Prepare(state,state.ContentCatalog.PosterItems.Select(p=>p.Id).ToArray());
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("detach","detach",state.Revision,StateReadSet.ForPoster(f.Main,state.Catalog.Posters.Single().EntityVersion) with{ContentVersion=state.ContentVersion}),_=>plan).Status);
        var service=new ContentReturnFixture(f.Store,new Shell(Move),Desktop(f));
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Single(service.InspectUninstall().Files);
        var result=await service.ReturnForUninstall(service.InspectUninstall(),0);
        Assert.True(result.Completed,string.Join(";",result.Errors));Assert.True(File.Exists(origin));
    }
    [Fact] public async Task UnindexedFileAndDirectoryGoToDesktopWithoutFabricatedCards()
    {
        using var f=new F();string storage=f.Collection.StorageLocator!,desktop=Desktop(f);
        File.WriteAllText(Path.Combine(storage,"unindexed.txt"),"owned");Directory.CreateDirectory(Path.Combine(storage,"orphan-folder"));File.WriteAllText(Path.Combine(storage,"orphan-folder","child.txt"),"child");
        var service=new ContentReturnFixture(f.Store,new Shell(Move),desktop);var result=await service.ReturnForUninstall(service.InspectUninstall(),0);
        Assert.True(result.Completed,string.Join(";",result.Errors));Assert.Equal("owned",File.ReadAllText(Path.Combine(desktop,"unindexed.txt")));
        Assert.Equal("child",File.ReadAllText(Path.Combine(desktop,"orphan-folder","child.txt")));Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task MissingOriginUsesDesktopAndMappedOrExternalSourcesStay()
    {
        using var f=new F();Collect(f,"gone-origin.txt");Directory.Delete(f.Source);
        string map=Path.Combine(f.DirectoryRoot,"mapped");Directory.CreateDirectory(map);File.WriteAllText(Path.Combine(map,"borrowed.txt"),"borrowed");
        CrossPosterTransferTests.AddPoster(f,"map",map);
        string desktop=Desktop(f);var service=new ContentReturnFixture(f.Store,new Shell(Move),desktop);
        Assert.True((await service.ReturnForUninstall(service.InspectUninstall(),0)).Completed);
        Assert.True(File.Exists(Path.Combine(desktop,"gone-origin.txt")));Assert.True(File.Exists(Path.Combine(map,"borrowed.txt")));
    }
    [Fact] public async Task PartialCancellationKeepsRemainderAndRetryDoesNotReplaySuccess()
    {
        using var f=new F();Collect(f,"a.txt");Collect(f,"b.txt");int calls=0;
        var service=new ContentReturnFixture(f.Store,new Shell((s,d)=>++calls==1?Move(s,d):new(null,true,0)),Desktop(f));
        var first=await service.ReturnForUninstall(service.InspectUninstall(),0);
        Assert.False(first.Completed);Assert.Single(first.Returned);Assert.Single(f.State.ContentCatalog.PosterItems);
        var retry=new ContentReturnFixture(f.Store,new Shell(Move),Desktop(f));Assert.True((await retry.ReturnForUninstall(retry.InspectUninstall(),0)).Completed);
        Assert.True(File.Exists(Path.Combine(f.Source,"a.txt")));Assert.True(File.Exists(Path.Combine(f.Source,"b.txt")));
    }
    [Fact] public async Task ChangedInventoryCannotMoveFiles()
    {
        using var f=new F();Collect(f,"a.txt");var shell=new Shell(Move);var service=new ContentReturnFixture(f.Store,shell,Desktop(f));var preview=service.InspectUninstall();
        File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"new.txt"),"new");
        Assert.False((await service.ReturnForUninstall(preview,0)).Completed);Assert.Equal(0,shell.Calls);
    }
    [Fact] public async Task CancelBeforeStartDoesNotMoveFiles()
    {
        using var f=new F();Collect(f,"a.txt");var shell=new Shell(Move);var service=new ContentReturnFixture(f.Store,shell,Desktop(f));
        Assert.False((await service.ReturnForUninstall(service.InspectUninstall(),0,new CancellationToken(true))).Completed);Assert.Equal(0,shell.Calls);
    }
    [Fact] public async Task RecordedResultReconcilesWithoutSecondMoveOrPosterDeletion()
    {
        using var f=new F();string origin=Collect(f,"a.txt");var shell=new Shell(Move);var service=new ContentReturnFixture(f.Store,shell,Desktop(f))
        {Checkpoint=phase=>{if(phase=="ResultRecorded")throw new IOException("simulated interruption");}};
        Assert.False((await service.ReturnForUninstall(service.InspectUninstall(),0)).Completed);
        Assert.True(File.Exists(origin));Assert.Single(f.State.ContentCatalog.PosterItems);
        var restart=new ContentReturnFixture(f.Store,shell,Desktop(f));Assert.Empty(restart.Reconcile());Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Empty(restart.InspectUninstall().Files);Assert.Single(f.State.Catalog.Posters);Assert.Equal(1,shell.Calls);
    }
    [Fact] public async Task CollisionNeverOverwritesWithoutShellDecision()
    {
        using var f=new F();string original=Collect(f,"collision.txt");File.WriteAllText(original,"sentinel");
        var service=new ContentReturnFixture(f.Store,new Shell((_,_)=>new(null,true,0)),Desktop(f));
        Assert.False((await service.ReturnForUninstall(service.InspectUninstall(),0)).Completed);
        Assert.Equal("sentinel",File.ReadAllText(original));Assert.Single(service.InspectUninstall().Files);
    }
    [Fact] public void InspectionIsNotAnImplicitMoveAndPendingTransfersBlockReturn()
    {
        using var f=new F();Collect(f,"kept.txt");var state=f.State;var shell=new Shell(Move);var service=new ContentReturnFixture(f.Store,shell,Desktop(f));
        Assert.Single(service.InspectUninstall().Files);Assert.Equal(state.Revision,f.State.Revision);Assert.Equal(0,shell.Calls);
        f.Stage(f.File("pending.txt"));Assert.Throws<IOException>(()=>service.InspectUninstall());
    }
    [Fact] public void ProfileLeaseExcludesInstallerAndAnotherOrdinaryApp()
    {
        using var f=new F();using(var normal=ProfileLifecycleGate.Acquire(f.Paths.Root))
        {
            Assert.Throws<IOException>(()=>new FileStream(Path.Combine(f.Paths.Root,ProfileLifecycleGate.FileName),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));
            Assert.Throws<IOException>(()=>ProfileLifecycleGate.Acquire(f.Paths.Root));
        }
        using var installer=new FileStream(Path.Combine(f.Paths.Root,ProfileLifecycleGate.FileName),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>ProfileLifecycleGate.Acquire(f.Paths.Root));
    }
    [Fact] public void ExplicitPreserveRecordsOfflineLocationsWithoutOpeningOrMovingThem()
    {
        using var f=new F();Collect(f,"keep.txt");var shell=new Shell(Move);var service=new ContentReturnFixture(f.Store,shell,Desktop(f));
        var path=f.Collection.StorageLocator!;Directory.Move(path,path+"-external");
        Assert.Contains(path,service.RetainedLocations());Assert.Equal(0,shell.Calls);Assert.Throws<IOException>(()=>service.InspectUninstall());
    }
    [Fact] public void MaintenanceDoesNotForwardDestructiveArgumentsToRunningApp()
    {
        string id=Guid.NewGuid().ToString("N");var names=new SingleInstanceNames("Local\\DesktopUp.UninstallTest."+id,"DesktopUp.UninstallTest."+id);
        using var primary=new SingleInstanceCoordinator(names);Assert.True(primary.TryAcquireOrForward(StartupIntent.Background([])));
        using var secondary=new SingleInstanceCoordinator(names);Assert.False(secondary.TryAcquireOrForward(StartupIntent.Background(["--prepare-uninstall=return"]),forwardExisting:false));
    }
}
