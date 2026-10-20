using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class CrossPosterTransferTests
{
    private sealed class Fake(Func<string,string,ShellDirectoryResult> run) : IShellDirectoryTransfer
    {
        public int Calls;
        public Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner, Func<bool> current, CancellationToken token)
        { Assert.False(copy); Assert.True(current()); Calls++; return Task.FromResult(run(source,destination)); }
    }
    private static ShellDirectoryResult Move(string source,string directory)
    {
        string target=Path.Combine(directory,Path.GetFileName(source));
        if(Directory.Exists(source))Directory.Move(source,target);else File.Move(source,target);
        return new(target,false,0);
    }
    internal static string AddPoster(F f,string name,string? mapping=null)
    {
        var state=f.State;
        var command=new CreatePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,state.Revision,
            new(state.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:state.Catalog.CatalogVersion,ContentVersion:state.ContentVersion),
            PosterConfigurationFactory.NewBasicBox(name,"DISPLAY1"));
        var poster=command.Configuration with {PosterId=Guid.NewGuid().ToString(),ThemeInstanceId=Guid.NewGuid().ToString()};
        var result=mapping is null?f.Repository.Execute(new(command.RequestId,command.RequestId,state.Revision,command.ReadSet),_=>new(){Creates=[poster],ResultPosterId=poster.PosterId}):
            ContentTestServices.Mapping(f.Store).Create(command,mapping);
        Assert.Equal(StateCommandStatus.Committed,result.Status);return result.ResultPosterId!;
    }
    private static void Icon(F f,string poster)
    {
        var s=f.State;var card=s.ContentCatalog.PosterItems.First(p=>p.PosterId==poster);
        var reads=StateReadSet.ForPoster(poster,s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster).EntityVersion) with {ContentVersion=s.ContentVersion};
        var command=new ContentIconCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,s.Revision,reads,
            ContentRelationPolicy.Resolve(s.ContentCatalog,poster,card.LayoutItemId)!.Witness,ContentIconPersistenceTests.Icon(),true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new(command.RequestId,command.RequestId,s.Revision,reads),x=>ContentIconPolicy.Prepare(command,x)).Status);
    }
    private static ShellExitIntent Capture(F f,string source)
    {
        var exit=new NativeShellExitJournal(f.Store);
        var intent=exit.Begin(source,f.State.ContentCatalog.PosterItems.Where(p=>p.PosterId==source).Select(p=>p.LayoutItemId).ToArray());
        exit.End(intent,false);return intent; // OLE consumed by another poster, not by Explorer.
    }
    private static (string Source,string Target,RegisteredManagedStorage? Storage) Setup(F f,bool sourceMapped,bool targetMapped,bool folder=false)
    {
        string path=Path.Combine(f.Source,folder?"folder":"sample.txt");
        if(folder){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"child.txt"),"owned-content");}else File.WriteAllText(path,"owned-content");
        string source;
        if(sourceMapped)source=AddPoster(f,"source mapping",f.Source);
        else{var plan=f.Stage(path);f.Executor.Execute(plan.Plan.Request.OperationId);source=f.Main;}
        Icon(f,source);
        string? directory=null;
        if(targetMapped){directory=Path.Combine(f.DirectoryRoot,"mapped-target");Directory.CreateDirectory(directory);}
        string target=AddPoster(f,"target",directory);
        return(source,target,targetMapped?null:new(f.Root,f.Collection));
    }
    private static Task<MappedIngressResult> Run(F f,(string Source,string Target,RegisteredManagedStorage? Storage) fixture,IShellDirectoryTransfer? shell=null,
        IncomingContentPlacement? landing=null, CrossPosterTransferUseCase? service=null)
    {
        var intent=Capture(f,fixture.Source);
        return(service??ContentTestServices.CrossPoster(f.Store,shell)).Execute(intent,fixture.Target,intent.Items.Select(i=>i.Path).ToArray(),fixture.Storage,landing,0,()=>true);
    }

    [Theory]
    [InlineData(false,false,false)][InlineData(false,true,false)][InlineData(true,false,false)][InlineData(true,true,false)]
    [InlineData(false,false,true)][InlineData(false,true,true)][InlineData(true,false,true)][InlineData(true,true,true)]
    public async Task FourRoutesPreserveSoleOwnerIconAndActualLanding(bool sourceMapped,bool targetMapped,bool folder)
    {
        using var f=new F();var setup=Setup(f,sourceMapped,targetMapped,folder);
        var original=f.State.ContentCatalog.Items.Single();var fake=new Fake(Move);
        var result=await Run(f,setup,fake,new(new(360,480),0,0,0));
        Assert.Empty(result.Errors);Assert.Equal(1,result.Completed);Assert.Equal(sourceMapped||targetMapped?1:0,fake.Calls);
        var card=Assert.Single(f.State.ContentCatalog.PosterItems);Assert.Equal(setup.Target,card.PosterId);
        Assert.Equal(ContentIconPersistenceTests.Icon().Id,card.IconAssetId);Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        var item=Assert.Single(f.State.ContentCatalog.Items);Assert.Equal(original.Id,item.Id);Assert.Null(ContentSingleOwnerPolicy.Validate(f.State.ContentCatalog));
        Assert.Equal("owned-content",File.ReadAllText(folder?Path.Combine(item.Target.Locator,"child.txt"):item.Target.Locator));
        Assert.Equal(!(sourceMapped||targetMapped),LocalTransferFiles.Exists(original.Target.Locator));
        Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target).Poster.ContentLayoutJson,setup.Target,out var layout,out _));
        Assert.Equal(1,Assert.Single(layout!.Placements).PinnedIndex);
        Assert.False(MappedDirectoryIngress.Pending(f.Store));
        var scan=ContentTestServices.Mapping(f.Store).Reconcile();Assert.Empty(scan.Notices);
        Assert.True(f.Store.Initialize().Succeeded); // Stored relations/assets survive a schema verification/reopen boundary.
    }
    [Fact] public async Task CancellationDoesNotInventCardsAndRetryWorks()
    {
        using var f=new F();var setup=Setup(f,false,true);var before=f.State;
        var cancel=new Fake((_,_)=>new(null,true,0));
        var result=await Run(f,setup,cancel);Assert.Equal(0,result.Completed);Assert.Equal(before.Revision,f.State.Revision);
        Assert.Single(f.State.ContentCatalog.PosterItems);Assert.True(File.Exists(before.ContentCatalog.Items.Single().Target.Locator));
        var retry=await Run(f,setup,new Fake(Move));Assert.Equal(1,retry.Completed);Assert.Empty(retry.Errors);
    }
    [Fact] public async Task PartialBatchOnlyTransfersCompletedItems()
    {
        using var f=new F();var setup=Setup(f,false,true);
        var plan=f.Stage(f.File("second.txt"));f.Executor.Execute(plan.Plan.Request.OperationId);
        int count=0;var fake=new Fake((s,d)=>++count==1?Move(s,d):new(null,true,0));
        var result=await Run(f,setup,fake);Assert.Equal(1,result.Completed);Assert.Equal(1,result.Skipped);
        Assert.Single(f.State.ContentCatalog.PosterItems.Where(p=>p.PosterId==setup.Source));Assert.Single(f.State.ContentCatalog.PosterItems.Where(p=>p.PosterId==setup.Target));
        Assert.All(f.State.ContentCatalog.Items,i=>Assert.True(File.Exists(i.Target.Locator)));Assert.False(MappedDirectoryIngress.Pending(f.Store));
    }
    [Fact] public async Task RecordedFileResultRecoversIndexWithoutReplayingShell()
    {
        using var f=new F();var setup=Setup(f,false,true);var fake=new Fake(Move);
        var service=ContentTestServices.CrossPoster(f.Store,fake,at=>{if(at=="FileRecorded")throw new IOException("simulated exit after result");});
        var result=await Run(f,setup,service:service);Assert.Equal(0,result.Completed);Assert.NotEmpty(result.Errors);
        Assert.True(MappedDirectoryIngress.Pending(f.Store));Assert.Equal(setup.Source,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
        Assert.Empty(new CrossPosterTransfer(f.Store).Reconcile());Assert.Equal(1,fake.Calls);
        Assert.Equal(setup.Target,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);Assert.Equal(ContentIconPersistenceTests.Icon().Id,f.State.ContentCatalog.PosterItems.Single().IconAssetId);
        Assert.Empty(new CrossPosterTransfer(f.Store).Reconcile());Assert.False(MappedDirectoryIngress.Pending(f.Store));
    }
    [Fact] public async Task IntentFailureNeverMovesFiles()
    {
        using var f=new F();var setup=Setup(f,true,false);var fake=new Fake(Move);
        f.Sql("CREATE TRIGGER cross_fault BEFORE INSERT ON mapped_directory_ingress BEGIN SELECT RAISE(ABORT,'injected'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>Run(f,setup,fake));Assert.Equal(0,fake.Calls);Assert.True(File.Exists(f.State.ContentCatalog.Items.Single().Target.Locator));
    }
    [Fact] public async Task ReplacedSourceAndStaleTargetRefuseBeforeExecution()
    {
        using var f=new F();var setup=Setup(f,false,true);var fake=new Fake(Move);var intent=Capture(f,setup.Source);
        await Assert.ThrowsAsync<IOException>(()=>ContentTestServices.CrossPoster(f.Store,fake).Execute(intent,setup.Target,intent.Items.Select(i=>i.Path).ToArray(),null,null,0,()=>true,expectedTargetVersion:999));
        string source=intent.Items.Single().Path;File.Move(source,source+".old");File.WriteAllText(source,"replacement");
        await Assert.ThrowsAsync<IOException>(()=>ContentTestServices.CrossPoster(f.Store,fake).Execute(intent,setup.Target,[source],null,null,0,()=>true));
        Assert.Equal(0,fake.Calls);Assert.Equal("replacement",File.ReadAllText(source));Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task ActualShellConflictRenameUsesReturnedNameNotGuessedName()
    {
        using var f=new F();var setup=Setup(f,false,true);
        var fake=new Fake((s,d)=>{string destination=Path.Combine(d,"Windows-selected-name.txt");File.Move(s,destination);return new(destination,false,0);});
        var result=await Run(f,setup,fake);Assert.Empty(result.Errors);Assert.EndsWith("Windows-selected-name.txt",f.State.ContentCatalog.Items.Single().Target.Locator);
        Assert.Equal("Windows-selected-name.txt",f.State.ContentCatalog.Items.Single().DisplayName);
    }
    [Fact] public async Task PhysicalReplaceRemovesFormerTargetCardAtomically()
    {
        using var f=new F();var setup=Setup(f,false,true);
        string directory=f.State.ContentCatalog.Collections.Single(c=>c.Kind==ContentCollectionKind.Mapped).StorageLocator!;
        File.WriteAllText(Path.Combine(directory,"sample.txt"),"old-target");
        var clock=new ManualTimeProvider();var mapping=ContentTestServices.Mapping(f.Store,clock);mapping.Reconcile();clock.Advance(TimeSpan.FromSeconds(3));mapping.Reconcile();
        var fake=new Fake((s,d)=>{string target=Path.Combine(d,Path.GetFileName(s));File.Move(s,target,true);return new(target,false,0);});
        var result=await Run(f,setup,fake);Assert.Empty(result.Errors);Assert.Equal(setup.Target,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
        Assert.Null(ContentSingleOwnerPolicy.Validate(f.State.ContentCatalog));Assert.Equal("owned-content",File.ReadAllText(Path.Combine(directory,"sample.txt")));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RealWindowsExecutorMovesFileAndDirectory(bool folder)
    {
        using var f=new F();var setup=Setup(f,true,false,folder);
        var result=await Run(f,setup);Assert.Empty(result.Errors);Assert.Equal(1,result.Completed);Assert.Equal(setup.Target,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
    }
    [Fact] public async Task ManualTargetUsesItsOwnGridAndKeepsNeighborAndSourceFile()
    {
        using var f=new F();var setup=Setup(f,false,false);
        var before=f.State;
        new ContentCreationFixture(f.Store).Create(setup.Target,new(setup.Storage,before.ContentVersion,before.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target).EntityVersion),NewContentKind.TextFile,"neighbor.txt");
        var state=f.State;var poster=state.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target);
        var neighbor=state.ContentCatalog.PosterItems.Single(p=>p.PosterId==setup.Target);
        Assert.True(ContentLayoutPolicy.TryRead(poster.Poster.ContentLayoutJson,setup.Target,out var layout,out _));
        var manual=ContentLayoutEditing.Apply(layout!,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual},new(360,480));Assert.True(manual.Accepted,manual.Error);
        var reads=StateReadSet.ForPoster(setup.Target,poster.EntityVersion);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("manual-target","manual-target",state.Revision,reads),_=>new(){Updates=[poster.Poster with {ContentLayoutJson=ContentLayoutPolicy.Serialize(manual.Document)}]}).Status);
        var result=await Run(f,setup,new Fake(Move),new(new(360,480),0,8,12));Assert.Empty(result.Errors);
        Assert.Contains(neighbor,f.State.ContentCatalog.PosterItems);Assert.Equal(2,f.State.ContentCatalog.PosterItems.Count);
        ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target).Poster.ContentLayoutJson,setup.Target,out var after,out _);
        var added=f.State.ContentCatalog.PosterItems.Single(p=>p.Id!=neighbor.Id);var placed=ContentLayoutSolver.Resolve(after!,new(360,480)).Items.Single(p=>p.ItemId==added.LayoutItemId);
        Assert.Equal(8,placed.Column);Assert.Equal(12,placed.Row);Assert.All(f.State.ContentCatalog.Items,i=>Assert.True(File.Exists(i.Target.Locator)));
    }
    [Fact] public async Task ReplacementAfterRecordedResultIsNotClaimedOrDeletedOnRecovery()
    {
        using var f=new F();var setup=Setup(f,false,true);var fake=new Fake(Move);
        var service=ContentTestServices.CrossPoster(f.Store,fake,at=>{if(at=="FileRecorded")throw new IOException("interrupted");});
        await Run(f,setup,service:service);
        string path=Path.Combine(f.State.ContentCatalog.Collections.Single(c=>c.Kind==ContentCollectionKind.Mapped).StorageLocator!,"sample.txt");
        File.Move(path,path+".original");File.WriteAllText(path,"unrelated replacement");
        Assert.NotEmpty(new CrossPosterTransfer(f.Store).Reconcile());Assert.Equal("unrelated replacement",File.ReadAllText(path));
        Assert.Equal("owned-content",File.ReadAllText(path+".original"));Assert.Equal(1,fake.Calls);Assert.False(MappedDirectoryIngress.Pending(f.Store));
    }
    [Fact] public async Task LateTargetEditKeepsFileResultButDoesNotApplyOldHoverPosition()
    {
        using var f=new F();var setup=Setup(f,false,true);
        var service=ContentTestServices.CrossPoster(f.Store,new Fake(Move),at=>
        {
            if(at!="FileRecorded")return;
            var s=f.State;var p=s.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target);
            var result=f.Repository.Execute(new("late-target","late-target",s.Revision,StateReadSet.ForPoster(setup.Target,p.EntityVersion)),
                _=>new(){Updates=[p.Poster with {Title="Edited during Shell operation"}]});Assert.Equal(StateCommandStatus.Committed,result.Status);
        });
        var result=await Run(f,setup,landing:new(new(360,480),0,0,0),service:service);Assert.Empty(result.Errors);
        var target=f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==setup.Target).Poster;
        Assert.Equal("Edited during Shell operation",target.Title);ContentLayoutPolicy.TryRead(target.ContentLayoutJson,setup.Target,out var layout,out _);
        Assert.Null(Assert.Single(layout!.Placements).PinnedIndex);Assert.Equal(setup.Target,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
    }
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>now;
        public void Advance(TimeSpan by)=>now+=by;
    }
    [Fact] public async Task UnknownShellResultPreservesFilesAndReportsBothLocations()
    {
        using var f=new F();var setup=Setup(f,false,true);
        var fake=new Fake((s,d)=>{Move(s,d);return new(null,false,0);});
        var result=await Run(f,setup,fake);Assert.Equal(0,result.Completed);Assert.Single(result.Errors);
        Assert.Contains("目标目录",result.Errors[0]);Assert.Equal(1,fake.Calls);
        string target=f.State.ContentCatalog.Collections.Single(c=>c.Kind==ContentCollectionKind.Mapped).StorageLocator!;
        Assert.Equal("owned-content",File.ReadAllText(Path.Combine(target,"sample.txt")));Assert.False(MappedDirectoryIngress.Pending(f.Store));
    }
}
