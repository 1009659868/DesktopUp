using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ShellNewMenuTests
{
    private static NewContentLocation Location(F f) => new(new(f.Root,f.Collection),f.State.ContentVersion,
        f.State.Catalog.Posters.Single(p=>p.Poster.PosterId==f.Main).EntityVersion);
    private static ShellCreatedItem Created(string path) => new(path,LocalTransferFiles.Identity(path),true);
    private static async Task<ShellCreatedItem> InvokeNew(string directory,string kind)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.ContentFixture.exe"))
        { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        start.ArgumentList.Add("--shell-new-probe");start.ArgumentList.Add(kind);
        using var process=Process.Start(start)!;
        var request=new ShellUiRequest(Guid.NewGuid(),ShellUiAction.DirectoryMenu,directory,0,0){Identity=LocalTransferFiles.Identity(directory)};
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var output=process.StandardOutput.ReadToEndAsync(stop.Token);
            var errors=process.StandardError.ReadToEndAsync(stop.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));process.StandardInput.Close();
            await process.WaitForExitAsync(stop.Token);
            string text=await output,trace=await errors;
            Assert.True(process.ExitCode==0,$"exit={process.ExitCode}; {text}; {trace}");
            Assert.Contains("verb=NewFolder",trace);Assert.Contains("verb=NewLink",trace);Assert.Contains("verb=.txt",trace);
            string json=text.Split('\n',StringSplitOptions.RemoveEmptyEntries).Last();
            var result=JsonSerializer.Deserialize<ShellUiResult>(json)!;
            Assert.True(ShellUiProtocol.Valid(request,result));Assert.True(result.Invoked);Assert.False(result.Cancelled);Assert.Null(result.Error);
            Assert.NotNull(result.CreatedItem);Assert.True(result.CreatedItem.EditName);
            return result.CreatedItem;
        }
        finally {if(!process.HasExited){process.Kill(false);await process.WaitForExitAsync();}}
    }
    [Theory][InlineData("folder")][InlineData("text")]
    public async Task RealShellNewCreatesRegisteredTypeAndPublishesExactCardThenRenamesAndRestarts(string kind)
    {
        using var f=new F();var location=Location(f);string directory=location.Directory;
        string unrelated=Path.Combine(directory,"untouched.txt");File.WriteAllText(unrelated,"unrelated");
        var created=await InvokeNew(directory,kind);
        Assert.True(kind=="folder"?Directory.Exists(created.Path):File.Exists(created.Path));
        var service=new ShellCreatedContentService(f.Store);string parentId=LocalTransferFiles.Identity(directory);
        service.Register(f.Main,location,parentId,created);service.Register(f.Main,location,parentId,created);
        var state=f.State;var card=Assert.Single(state.ContentCatalog.PosterItems);
        var relation=ContentRelationPolicy.Resolve(state.ContentCatalog,f.Main,card.LayoutItemId)!;
        Assert.Equal(created.Path,relation.Item.Target.Locator);
        Assert.Single(state.ContentCatalog.Memberships.Where(m=>m.CollectionId==f.Collection.Id));
        Assert.Equal(1L,f.Sql("SELECT COUNT(*) FROM managed_content_observations"));
        Assert.Empty(f.Journal.Read());Assert.Equal("unrelated",File.ReadAllText(unrelated));
        string next=kind=="folder"?"renamed":"renamed.txt";
        new ContentRenameService(f.Store).Rename(f.Main,card.LayoutItemId,relation.Item.Id,relation.Item.Version,next,expectedIdentity:created.Identity);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(card.Id,Assert.Single(new SqliteStateCommandRepository(reopened).Read().ContentCatalog.PosterItems).Id);
        Assert.True(LocalTransferFiles.Exists(Path.Combine(directory,next)));
    }
    [Fact] public async Task NativeNewOnMappedDirectoryPublishesWithoutMovingOrDuplicating()
    {
        using var f=new F();var s=f.State;
        var command=new CreatePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,s.Revision,
            new(s.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:s.Catalog.CatalogVersion,ContentVersion:s.ContentVersion),
            PosterConfigurationFactory.NewBasicBox("map","DISPLAY1"));
        var map=new MappedDirectoryService(f.Store);var made=map.Create(command,f.Source);Assert.Equal(StateCommandStatus.Committed,made.Status);
        string poster=made.ResultPosterId!;s=f.State;
        var location=new NewContentLocation(null,s.ContentVersion,s.Catalog.Posters.Single(p=>p.Poster.PosterId==poster).EntityVersion){Mapping=map.ResolveNewContent(poster)};
        var created=await InvokeNew(f.Source,"text");var service=new ShellCreatedContentService(f.Store);
        service.Register(poster,location,LocalTransferFiles.Identity(f.Source),created);
        service.Register(poster,location,LocalTransferFiles.Identity(f.Source),created);
        Assert.Equal(poster,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);Assert.True(File.Exists(created.Path));
        Assert.Empty(f.Journal.Read());Assert.Equal(0L,f.Sql("SELECT COUNT(*) FROM managed_content_observations"));
    }
    [Fact] public void ReplacedObjectIsNotAdoptedAndNeitherFileIsDeleted()
    {
        using var f=new F();var location=Location(f);string path=Path.Combine(location.Directory,"new.txt");File.WriteAllText(path,"original");
        var created=Created(path);string preserved=Path.Combine(f.Source,"original.txt");File.Move(path,preserved);File.WriteAllText(path,"replacement");
        Assert.Throws<IOException>(()=>new ShellCreatedContentService(f.Store).Register(f.Main,location,LocalTransferFiles.Identity(location.Directory),created));
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal("original",File.ReadAllText(preserved));Assert.Equal("replacement",File.ReadAllText(path));
    }
    [Fact] public void IndexFailureLeavesNativeCreatedFileAndNoHalfMembership()
    {
        using var f=new F();var location=Location(f);string path=Path.Combine(location.Directory,"new.txt");File.WriteAllText(path,"preserve");
        f.Sql("CREATE TRIGGER create_fault BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'injected'); END;");
        Assert.ThrowsAny<Exception>(()=>new ShellCreatedContentService(f.Store).Register(f.Main,location,LocalTransferFiles.Identity(location.Directory),Created(path)));
        Assert.Equal("preserve",File.ReadAllText(path));Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Empty(f.State.ContentCatalog.Memberships);
    }
    [Fact] public void CreatedResultRequiresDirectoryActionAndDirectChildIdentity()
    {
        var request=new ShellUiRequest(Guid.NewGuid(),ShellUiAction.DirectoryMenu,"C:\\test",0,0);
        var valid=new ShellUiResult(request.Id,true,false){CreatedItem=new("C:\\test\\new.txt",new string('A',48),true)};
        Assert.True(ShellUiProtocol.Valid(request,valid));
        Assert.False(ShellUiProtocol.Valid(request with{Action=ShellUiAction.ItemMenu},valid));
        Assert.False(ShellUiProtocol.Valid(request,valid with{CreatedItem=valid.CreatedItem with{Path="C:\\else\\new.txt"}}));
        Assert.False(ShellUiProtocol.Valid(request,valid with{CreatedItem=valid.CreatedItem with{Identity=null!}}));
        Assert.Equal("Win菜单",ShellUiProtocol.MenuLabel);
    }
}
