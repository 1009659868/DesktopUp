using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class MappedDirectoryIngressTests
{
    private static string Map(F f)
    {
        var s=f.State;
        var command=new CreatePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,s.Revision,
            new(s.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:s.Catalog.CatalogVersion,ContentVersion:s.ContentVersion),
            PosterConfigurationFactory.NewBasicBox("映射","DISPLAY1"));
        var result=ContentTestServices.Mapping(f.Store).Create(command,f.Source);
        Assert.Equal(StateCommandStatus.Committed,result.Status);return result.ResultPosterId!;
    }
    private static string Outside(F f,string name="incoming.txt")
    {string path=Path.Combine(f.DirectoryRoot,name);File.WriteAllText(path,"isolated-data");return path;}
    private sealed class Fake(Func<string,string,bool,ShellDirectoryResult> run):IShellDirectoryTransfer
    {
        public int Calls;
        public Task<ShellDirectoryResult> Run(string source,string destination,bool copy,nint owner,Func<bool> current,CancellationToken token)
        {Calls++;Assert.True(current());return Task.FromResult(run(source,destination,copy));}
    }
    private static ShellDirectoryResult Move(string source,string target,bool copy)
    {var path=Path.Combine(target,Path.GetFileName(source));if(copy)File.Copy(source,path);else File.Move(source,path);return new(path,false,0);}
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MoveOrCopyWritesIntentFirstThenMapsActualResultAtLanding(bool copy)
    {
        using var f=new F();f.File("neighbor.txt");string id=Map(f),source=Outside(f);
        var neighbor=Assert.Single(f.State.ContentCatalog.PosterItems);
        var fake=new Fake((s,d,c)=>{Assert.True(MappedDirectoryIngress.Pending(f.Store));Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_transfers"));return Move(s,d,c);});
        var result=await ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[source],copy,new(new(360,480),0,0,0),0,()=>true);
        Assert.Empty(result.Errors);Assert.Equal(1,result.Completed);Assert.Equal(copy,File.Exists(source));
        Assert.Equal("isolated-data",File.ReadAllText(Path.Combine(f.Source,"incoming.txt")));
        Assert.Contains(neighbor,f.State.ContentCatalog.PosterItems);Assert.Equal(2,f.State.ContentCatalog.PosterItems.Count);
        Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==id).Poster.ContentLayoutJson,id,out var layout,out _));
        var added=f.State.ContentCatalog.PosterItems.Single(p=>p.Id!=neighbor.Id);
        Assert.Equal(1,layout!.Placements.Single(p=>p.ItemId==added.LayoutItemId).PinnedIndex);
        Assert.False(MappedDirectoryIngress.Pending(f.Store));Assert.False(File.Exists(Path.Combine(f.Collection.StorageLocator!,"incoming.txt")));
    }
    [Fact] public async Task WindowsCancelDoesNotMoveSourceOrInventCardAndCanRetry()
    {
        using var f=new F();string id=Map(f),source=Outside(f);
        var cancel=new Fake((_,_,_)=>new(null,true,0));
        var result=await ContentTestServices.MappedIngress(f.Store,cancel).Execute(id,[source],false,null,0,()=>true);
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.True(File.Exists(source));Assert.Equal(0,result.Completed);
        var retry=await ContentTestServices.MappedIngress(f.Store,new Fake(Move)).Execute(id,[source],false,null,0,()=>true);
        Assert.Equal(1,retry.Completed);Assert.Empty(retry.Errors);
    }
    [Fact] public async Task PartialBatchKeepsCompletedResultAndUnfinishedSource()
    {
        using var f=new F();string id=Map(f),first=Outside(f,"one.txt"),second=Outside(f,"two.txt");int calls=0;
        var fake=new Fake((s,d,c)=>++calls==1?Move(s,d,c):new(null,true,0));
        var result=await ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[first,second],false,null,0,()=>true);
        Assert.Equal(1,result.Completed);Assert.Single(f.State.ContentCatalog.PosterItems);Assert.True(File.Exists(second));Assert.False(File.Exists(first));
    }
    [Fact] public async Task KnownSourceCardMovesToSingleMappedOwnerWithoutDeletingOtherItems()
    {
        using var f=new F();string source=Outside(f);f.AddReference(LocalContentReferenceProbe.Inspect(source).Item!);string id=Map(f);
        var result=await ContentTestServices.MappedIngress(f.Store,new Fake(Move)).Execute(id,[source],false,null,0,()=>true);
        Assert.Empty(result.Errors);Assert.Equal(id,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
        Assert.Null(ContentSingleOwnerPolicy.Validate(f.State.ContentCatalog));Assert.Empty(new NativeShellExitJournal(f.Store).Pending());
    }
    [Fact] public async Task DatabaseIntentFailureNeverStartsFileOperation()
    {
        using var f=new F();string id=Map(f),source=Outside(f);var fake=new Fake(Move);
        f.Sql("CREATE TRIGGER ingress_fault BEFORE INSERT ON mapped_directory_ingress BEGIN SELECT RAISE(ABORT,'injected'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[source],false,null,0,()=>true));
        Assert.Equal(0,fake.Calls);Assert.True(File.Exists(source));Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task ChangedSourceAfterPreparationIsNotPassedToShell()
    {
        using var f=new F();string id=Map(f),source=Outside(f);var fake=new Fake(Move);
        var ingress=ContentTestServices.MappedIngress(f.Store,fake,_=>{File.Move(source,source+".original");File.WriteAllText(source,"replacement");});
        var result=await ingress.Execute(id,[source],false,null,0,()=>true);
        Assert.NotEmpty(result.Errors);Assert.Equal(0,fake.Calls);Assert.Equal("replacement",File.ReadAllText(source));
    }
    [Fact] public async Task AlreadyDirectChildIsNoOpAndUnavailableRootNeverMovesSource()
    {
        using var f=new F();string existing=f.File("existing.txt"),id=Map(f);var fake=new Fake(Move);
        var result=await ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[existing],false,null,0,()=>true);
        Assert.Equal(1,result.Skipped);Assert.Equal(0,fake.Calls);
        string source=Outside(f);Directory.Move(f.Source,f.Source+"-parked");
        await Assert.ThrowsAnyAsync<IOException>(()=>ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[source],false,null,0,()=>true));
        Assert.Equal(0,fake.Calls);Assert.True(File.Exists(source));Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void Schema23MigrationAndRestartClosureDoNotReplayFiles()
    {
        using var f=new F();f.File("keep.txt");Map(f);var before=f.State;
        var checksum=f.Sql("SELECT checksum_sha256 FROM schema_migrations WHERE version=23");
        f.Sql(StorageSchemaFixture.Drop24+"UPDATE state_head SET schema_version=23;PRAGMA user_version=23;");
        var result=f.Store.Initialize();Assert.True(result.Succeeded,result.Error);Assert.Equal(23,result.PreviousSchemaVersion);Assert.Equal(25,result.Head!.SchemaVersion);
        Assert.NotNull(result.PreMigrationBackupFileName);Assert.Equal(checksum,f.Sql("SELECT checksum_sha256 FROM schema_migrations WHERE version=23"));
        f.Sql("INSERT INTO mapped_directory_ingress(operation_id,body_json) VALUES('interrupted','{}');");
        Assert.Equal(1,new MappedDirectoryIngress(f.Store).SettleInterrupted());Assert.False(MappedDirectoryIngress.Pending(f.Store));
        Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);Assert.True(File.Exists(Path.Combine(f.Source,"keep.txt")));
    }
    [Fact] public async Task StaleReleasedPosterAndPreCancelledOperationNeverStartShell()
    {
        using var f=new F();string id=Map(f),source=Outside(f);var fake=new Fake(Move);
        await Assert.ThrowsAsync<IOException>(()=>ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[source],false,null,0,()=>true,expectedPosterVersion:999));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ContentTestServices.MappedIngress(f.Store,fake).Execute(id,[source],false,null,0,()=>true,cancellation.Token));
        Assert.Equal(0,fake.Calls);Assert.True(File.Exists(source));Assert.False(MappedDirectoryIngress.Pending(f.Store));
    }
    [Fact] public void LossAndOriginalRootRecoveryReportStatusWithoutClearingCards()
    {
        using var f=new F();f.File("keep.txt");string id=Map(f);var before=f.State.ContentCatalog.PosterItems;
        var service=ContentTestServices.Mapping(f.Store);Assert.True(Assert.Single(service.Reconcile().States).Available);
        Directory.Move(f.Source,f.Source+"-parked");var failed=service.Reconcile();
        Assert.True(failed.Pending);Assert.False(Assert.Single(failed.States).Available);Assert.Equal(id,failed.States[0].PosterId);Assert.NotNull(failed.States[0].Message);
        Assert.Equal(before,f.State.ContentCatalog.PosterItems);
        Directory.Move(f.Source+"-parked",f.Source);Assert.True(Assert.Single(service.Reconcile().States).Available);Assert.Equal(before,f.State.ContentCatalog.PosterItems);
    }
    [Theory][InlineData(false)][InlineData(true)]
    [Trait("Category","LocalMultiVolume")]
    public async Task RealWindowsCrossVolumeMoveKeepsCompleteFileOrFolder(bool folder)
    {
        using var f=new F();string id=Map(f);
        var external=Directory.CreateTempSubdirectory("DesktopUp.MappedIngress.Owned.");
        try
        {
            Assert.NotEqual(Path.GetPathRoot(f.Source),Path.GetPathRoot(external.FullName));
            string source=Path.Combine(external.FullName,folder?"cross-folder":"cross.txt");
            if(folder){Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"child.txt"),"cross-volume-owned");}else File.WriteAllText(source,"cross-volume-owned");
            using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result=await ContentTestServices.MappedIngress(f.Store).Execute(id,[source],false,null,0,()=>true,cancel.Token).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Empty(result.Errors);Assert.Equal(1,result.Completed);Assert.False(LocalTransferFiles.Exists(source));
            string destination=Path.Combine(f.Source,Path.GetFileName(source));
            Assert.Equal("cross-volume-owned",File.ReadAllText(folder?Path.Combine(destination,"child.txt"):destination));
            Assert.Single(f.State.ContentCatalog.PosterItems);
        }
        finally{external.Delete(true);} // Only the unique temp directory made above, never supplied data.
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public async Task RealWindowsShellReportsRootResultForFileAndFolder(bool copy,bool folder)
    {
        using var f=new F();string id=Map(f),source=Path.Combine(f.DirectoryRoot,folder?"incoming-folder":"incoming.txt");
        if(folder){Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"child.txt"),"isolated-data");}else File.WriteAllText(source,"isolated-data");
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result=await ContentTestServices.MappedIngress(f.Store).Execute(id,[source],copy,null,0,()=>true,cancel.Token).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Empty(result.Errors);Assert.Equal(1,result.Completed);Assert.Equal(copy,LocalTransferFiles.Exists(source));
        string destination=Path.Combine(f.Source,Path.GetFileName(source));Assert.Equal("isolated-data",File.ReadAllText(folder?Path.Combine(destination,"child.txt"):destination));
        Assert.Equal(destination,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);Assert.Single(f.State.ContentCatalog.PosterItems);
    }
}
