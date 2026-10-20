using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class LocalContentCompatibilityTests
{
    [Theory]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData(@"\\?\C:\sample.txt")]
    [InlineData(@"shell:AppsFolder\vendor!App")]
    [InlineData(@"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("steam://rungameid/1")]
    [InlineData(@"C:\sample.txt:extra")]
    [InlineData(@"C:\NUL.txt")]
    [InlineData(@"C:\folder.\file.txt")]
    [InlineData(@"C:relative.txt")]
    public void UnsupportedPayloadIsRefusedBeforeHoverOrFilesystemAccess(string path)
    {
        Assert.False(LocalContentCompatibility.TryNormalize(path, false, out _, out _));
        Assert.Null(LocalContentReferenceProbe.Inspect(path).Item);
        Assert.Throws<IOException>(() => LocalTransferFiles.Normalize(path));
        var session = new IncomingDropSession();
        Assert.Null(session.Enter([path], IncomingDropSessionTests.Surface(), new(-900, 0), 0, 2));
        Assert.False(session.Active);
    }

    [Fact]
    public void RootReferenceIsNotPermissionToCollectWholeDrive()
    {
        Assert.True(LocalContentCompatibility.TryNormalize(@"C:\", true, out _, out _));
        Assert.False(LocalContentCompatibility.TryNormalize(@"C:\", false, out _, out _));
        Assert.Null(new IncomingDropSession().Enter([@"C:\"], IncomingDropSessionTests.Surface(), new(-900, 0), 0, 2));
    }

    [Fact]
    public void AliasedDuplicatePayloadCannotOfferTwoCopiesOfOneObject()
    {
        var session = new IncomingDropSession();
        Assert.Null(session.Enter([@"C:\owned\file.txt", @"c:\owned\sub\..\file.txt"],
            IncomingDropSessionTests.Surface(), new(-900, 0), 0, 2));
    }

    [Theory]
    [InlineData(@"C:\owned\启动.LNK", ContentKind.Shortcut)]
    [InlineData(@"C:\owned\游戏.URL", ContentKind.Shortcut)]
    [InlineData(@"C:\owned\application.exe", ContentKind.File)]
    [InlineData(@"C:\owned\no-extension", ContentKind.File)]
    public void FilesKeepTheirOwnIdentityInsteadOfTheTargetIdentity(string path, ContentKind kind)
    {
        Assert.True(LocalContentCompatibility.TryNormalize(path, false, out _, out _));
        Assert.Equal(kind, LocalContentCompatibility.Kind(path, FileAttributes.Normal));
        Assert.Equal(ContentKind.Folder, LocalContentCompatibility.Kind(path, FileAttributes.Directory));
    }

    [Theory]
    [InlineData(0x400, false)] [InlineData(0x1000, false)] [InlineData(0x400000, false)]
    [InlineData(0x80000, true)] [InlineData(0x100000, true)] [InlineData(0x40000, true)]
    public void RemoteDataFlagsAreNotConfusedWithPinningIntentOrExtendedAttributes(int flags, bool allowed) =>
        Assert.Equal(allowed, LocalContentCompatibility.IsMaterialized((FileAttributes)flags));

    [Theory]
    [InlineData(".lnk")] [InlineData(".url")]
    public void CollectingPhysicalShortcutPreservesBytesTargetAndRestartIdentity(string extension)
    {
        using var f = new F();
        string target = f.File("target.txt");
        string source = Path.Combine(f.Source, "入口" + extension);
        byte[] original = extension == ".lnk" ? ShellShortcutFile.Encode(target) :
            System.Text.Encoding.UTF8.GetBytes("[InternetShortcut]\r\nURL=" + new Uri(target).AbsoluteUri + "\r\n");
        File.WriteAllBytes(source, original);
        string targetIdentity = LocalTransferFiles.Identity(target);
        var plan = f.Stage(source);
        Assert.Equal(DurableTransferPhase.IndexCommitted, Assert.Single(f.Executor.Execute(plan.Plan.Request.OperationId)).Phase);
        Assert.False(File.Exists(source));
        Assert.Equal(original, File.ReadAllBytes(plan.Files[0].Destination));
        Assert.Equal(targetIdentity, LocalTransferFiles.Identity(target));
        Assert.Equal("owned-content", File.ReadAllText(target));
        var item = Assert.Single(f.State.ContentCatalog.Items);
        Assert.Equal(ContentKind.Shortcut, item.Kind);
        Assert.Equal(plan.Files[0].Destination, item.Target.Locator);
        if (extension == ".lnk") Assert.Equal(target, ShellShortcutFile.ReadStoredTarget(item.Target.Locator));
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(item.Id, Assert.Single(new SqliteStateCommandRepository(reopened).Read().ContentCatalog.Items).Id);
    }

    [Fact]
    public async Task InspectInternetShortcutDoesNotLaunchOrRewriteItsProtocol()
    {
        using var f = new F();
        string path = f.File("registered-game.url");
        const string content = "[InternetShortcut]\r\nURL=steam://rungameid/123\r\n";
        File.WriteAllText(path, content);
        var item = LocalContentReferenceProbe.Inspect(path).Item!;
        using var worker = new LocalContentWorkerClient(Path.Combine(AppContext.BaseDirectory,
            "content-test-helpers", "DesktopUp.ContentWorker.exe"));
        var response = await worker.RunAsync(new(Guid.NewGuid(), LocalContentAction.Inspect, item), CancellationToken.None);
        Assert.Equal(ContentAvailability.Available, response.Availability);
        Assert.Equal(ContentOpenEvidence.None, response.Evidence);
        Assert.Equal(content, File.ReadAllText(path));
        await worker.StopAsync();
    }
}
