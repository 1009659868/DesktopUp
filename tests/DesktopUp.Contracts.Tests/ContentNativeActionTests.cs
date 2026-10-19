using DesktopUp.Windows;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentNativeActionTests
{
    [Theory][InlineData("文档.txt", false, 2)][InlineData("archive.tar.gz", false, 11)]
    [InlineData("folder.name", true, 11)][InlineData(".gitignore", false, 10)][InlineData("README", false, 6)]
    public void InlineRenameSelectsStemWithoutChangingTheExtension(string name, bool folder, int length) =>
        Assert.Equal(length, LocalFileNamePolicy.RenameSelectionLength(name, folder));

    [Fact]public void ShellAndIconWorkersUseTheCompleteBundledDirectory() =>
        Assert.Equal(Path.Combine(Path.GetFullPath("C:/DesktopUp"), "content-worker", "DesktopUp.ContentWorker.exe"),
            ContentWorkerLocation.InApplication("C:/DesktopUp"));
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("..")] [InlineData("a/b")][InlineData("CON.txt")]
    [InlineData("Lpt1.log")][InlineData("COM²")][InlineData("x.")][InlineData("x ")][InlineData("x:stream")]
    public void InvalidNamesCannotEscapeOrBecomeDevices(string name)=>Assert.NotNull(LocalFileNamePolicy.Error(name));
    [Theory][InlineData("新的文件.txt")][InlineData("LPT0.txt")][InlineData("my.file")]
    public void NormalNamesAccepted(string name)=>Assert.Null(LocalFileNamePolicy.Error(name));

    private static string Collect(F f,string name="before.txt")
    {var staged=f.Stage(f.File(name));Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(staged.Plan.Request.OperationId)).Phase);return staged.Files[0].Destination;}
    private static void Rename(F f,string name)
    {
        var card=Assert.Single(f.State.ContentCatalog.PosterItems);var relation=ContentRelationPolicy.Resolve(f.State.ContentCatalog,f.Main,card.LayoutItemId)!;
        new ContentRenameService(f.Store).Rename(f.Main,card.LayoutItemId,relation.Item.Id,relation.Item.Version,name);
    }
    [Fact]public void RenamePreservesCardLayoutAndBytesAndSurvivesRestart()
    {
        using var f=new F();string old=Collect(f);var before=f.State;Rename(f,"after.txt");var after=f.State;
        Assert.Equal(before.ContentCatalog.PosterItems,after.ContentCatalog.PosterItems);
        Assert.Equal(before.Catalog.Posters.Single().Poster.ContentLayoutJson,after.Catalog.Posters.Single().Poster.ContentLayoutJson);
        Assert.Equal("after.txt",Assert.Single(after.ContentCatalog.Items).DisplayName);
        Assert.Equal("owned-content",File.ReadAllText(Assert.Single(after.ContentCatalog.Items).Target.Locator));Assert.False(File.Exists(old));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_renames WHERE closed=0"));
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);Assert.Equal(0,new ContentRenameService(reopened).Reconcile());
    }
    [Fact]public void CollisionDoesNotOverwriteEitherFile()
    {
        using var f=new F();string old=Collect(f);string occupied=Path.Combine(f.Collection.StorageLocator!,"occupied.txt");File.WriteAllText(occupied,"other");
        Assert.Throws<IOException>(()=>Rename(f,"occupied.txt"));Assert.Equal("other",File.ReadAllText(occupied));Assert.Equal("owned-content",File.ReadAllText(old));
        Assert.Equal(old,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
    }
    [Fact]public void DatabaseFailureRollsBackExactFileAndDoesNotChangeRelations()
    {
        using var f=new F();string old=Collect(f);var before=f.State;
        f.Sql("CREATE TRIGGER rename_fault BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'injected'); END;");
        Assert.ThrowsAny<Exception>(()=>Rename(f,"after.txt"));Assert.True(File.Exists(old));
        Assert.False(File.Exists(Path.Combine(f.Collection.StorageLocator!,"after.txt")));Assert.Equal(before.Revision,f.State.Revision);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void InterruptedRenameObservesButNeverReplays(bool moved)
    {
        using var f=new F();string old=Collect(f),next=Path.Combine(f.Collection.StorageLocator!,"after.txt");var original=Assert.Single(f.State.ContentCatalog.Items);
        var intent=new ContentRenameIntent(Guid.NewGuid(),old,next,LocalTransferFiles.Identity(old),LocalTransferFiles.Identity(Path.GetDirectoryName(old)!),
            [new(original.Id,original.Version,old,next)]);
        string json=JsonSerializer.Serialize(intent).Replace("'","''");
        f.Sql($"INSERT INTO content_renames(operation_id,body_json) VALUES('{intent.Id}','{json}');");
        if(moved)File.Move(old,next);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(moved?1:0,new ContentRenameService(reopened).Reconcile());
        Assert.Equal(moved?next:old,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
        Assert.Equal(moved,File.Exists(next));Assert.Equal(!moved,File.Exists(old));
    }
    [Fact]public void CaseOnlyRenameUsesActualSpelling()
    {using var f=new F();Collect(f);Rename(f,"BEFORE.txt");Assert.Equal("BEFORE.txt",Assert.Single(f.State.ContentCatalog.Items).DisplayName);}
    [Fact]public void LegacyReferenceRenameUpdatesPathWithoutCollectingFile()
    {
        using var f=new F();string old=f.File("legacy.txt");f.AddReference(LocalContentReferenceProbe.Inspect(old).Item!);
        Rename(f,"renamed.txt");Assert.Equal(Path.Combine(f.Source,"renamed.txt"),Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
        Assert.Empty(Directory.EnumerateFileSystemEntries(f.Collection.StorageLocator!).Where(p=>Path.GetFileName(p)=="renamed.txt"));
    }
    [Fact]public void ShellProtocolRejectsOtherSchemesAndBadResultIdentity()
    {
        foreach(string path in new[]{"shell:AppsFolder","https://example.org","C:\\x:stream","\\\\host\\share\\x"})
            Assert.False(ShellUiProtocol.Valid(new(Guid.NewGuid(),ShellUiAction.ItemMenu,path,0,0)));
        var request=new ShellUiRequest(Guid.NewGuid(),ShellUiAction.Properties,"C:\\data\\x.txt",0,0);
        Assert.True(ShellUiProtocol.Valid(request));Assert.False(ShellUiProtocol.Valid(request,new(Guid.NewGuid(),true,false)));
        Assert.False(ShellUiProtocol.Valid(request,new(request.Id,true,true)));
    }
    [Fact]public async Task HelperRejectsSubstitutedIdentityAndReleasesLeaseWithoutShowingUi()
    {
        using var f=new F();string path=f.File("helper.txt");var ledger=new ResourceLedger(f.Paths);
        var client=new ShellUiClient(Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.ContentWorker.exe"),ledger);
        var result=await client.RunAsync(new(Guid.NewGuid(),ShellUiAction.Properties,path,0,0) {Identity=new string('0',48)},CancellationToken.None);
        Assert.False(result.Invoked);Assert.NotNull(result.Error);Assert.Empty(ledger.ActiveResources);Assert.Equal("owned-content",File.ReadAllText(path));
    }
    [Theory][InlineData(ShellUiAction.Properties)][InlineData(ShellUiAction.ItemMenu)][InlineData(ShellUiAction.DirectoryMenu)]
    public async Task RealShellPreparationAcceptsCurrentIdentityAndBuildsMenuWithoutInvokingCommand(ShellUiAction action)
    {
        using var f=new F();string path=action==ShellUiAction.DirectoryMenu?f.Source:f.File("真实对象.txt");
        var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.ContentWorker.exe"),"--shell-ui-prepare-v1")
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using var process=System.Diagnostics.Process.Start(start)!;
        var request=new ShellUiRequest(Guid.NewGuid(),action,path,0,0){Identity=LocalTransferFiles.Identity(path)};
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));process.StandardInput.Close();
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            string output=await process.StandardOutput.ReadToEndAsync(stop.Token),error=await process.StandardError.ReadToEndAsync(stop.Token);
            await process.WaitForExitAsync(stop.Token);
            Assert.True(process.ExitCode==0,$"exit={process.ExitCode}; output={output}; error={error}");Assert.StartsWith("ready",output);
        }
        finally{if(!process.HasExited){process.Kill(false);await process.WaitForExitAsync();}}
    }
    [Fact]public void ReplacementWhileNameDialogOpenIsNotRenamed()
    {
        using var f=new F();string path=Collect(f),identity=LocalTransferFiles.Identity(path);var before=f.State;
        var card=Assert.Single(before.ContentCatalog.PosterItems);var item=Assert.Single(before.ContentCatalog.Items);
        File.Move(path,Path.Combine(f.Source,"original.txt"));File.WriteAllText(path,"replacement");
        Assert.Throws<IOException>(()=>new ContentRenameService(f.Store).Rename(f.Main,card.LayoutItemId,item.Id,item.Version,"after.txt",expectedIdentity:identity));
        Assert.Equal("replacement",File.ReadAllText(path));Assert.Equal(before.Revision,f.State.Revision);
    }
    [Fact]public void FolderRenameUpdatesKnownDescendantsWithoutAddingNewCards()
    {
        using var f=new F();string folder=Path.Combine(f.Source,"folder");Directory.CreateDirectory(folder);
        string child=Path.Combine(folder,"child.txt");File.WriteAllText(child,"child");
        f.AddReference(LocalContentReferenceProbe.Inspect(folder).Item!);f.AddReference(LocalContentReferenceProbe.Inspect(child).Item!);
        var before=f.State;var root=before.ContentCatalog.Items.Single(i=>i.Target.Locator==folder);
        var card=before.ContentCatalog.PosterItems.Single(p=>ContentRelationPolicy.Resolve(before.ContentCatalog,f.Main,p.LayoutItemId)!.Item.Id==root.Id);
        new ContentRenameService(f.Store).Rename(f.Main,card.LayoutItemId,root.Id,root.Version,"renamed-folder");
        Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.All(f.State.ContentCatalog.Items,i=>Assert.Contains("renamed-folder",i.Target.Locator));
        Assert.Equal("child",File.ReadAllText(Path.Combine(f.Source,"renamed-folder","child.txt")));
    }
    [Fact]public void SchemaTwentyOneUpgradesWithoutChangingExistingContent()
    {
        using var f=new F();Collect(f);var before=f.State;
        f.Sql(StorageSchemaFixture.Drop22+"UPDATE state_head SET schema_version=21; PRAGMA user_version=21;");
        var result=f.Store.Initialize();Assert.True(result.Succeeded,result.Error);Assert.Equal(21,result.PreviousSchemaVersion);
        Assert.Equal(25,result.Head!.SchemaVersion);Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.Equal(before.ContentCatalog.Items,f.State.ContentCatalog.Items);
    }
}
