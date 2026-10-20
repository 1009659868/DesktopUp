using DesktopUp.Infrastructure;
using DesktopUp.Application;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentCreationTests
{
    private static NewContentLocation Location(F f) => new(new(f.Root, f.Collection), f.State.ContentVersion, f.State.Catalog.Posters.Single().EntityVersion);
    private static string Create(F f, NewContentKind kind, string name, string? target = null) =>
        new ContentCreationFixture(f.Store).Create(f.Main, Location(f), kind, name, target);
    private static void NoStage(F f) => Assert.Empty(Directory.EnumerateDirectories(f.Root.Locator, ".desktopup-new-*"));

    [Theory][InlineData(NewContentKind.Folder, "资料")][InlineData(NewContentKind.TextFile, "笔记.txt")]
    public void NewObjectIsManagedIndexedAndSurvivesRestart(NewContentKind kind, string name)
    {
        using var f = new F(); string path = Create(f, kind, name);
        Assert.Equal(Path.Combine(f.Collection.StorageLocator!, name), path);
        Assert.True(kind == NewContentKind.Folder ? Directory.Exists(path) : File.Exists(path));
        Assert.Equal(path, Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
        Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Equal(DurableTransferPhase.IndexCommitted, Assert.Single(f.Journal.Read()).Phase); NoStage(f);
        using var restart = new SqliteStateStore(f.Paths); Assert.True(restart.Initialize().Succeeded);
        new AutomaticTransferCompletion(new(restart)).Settle();
        Assert.Single(new SqliteStateCommandRepository(restart).Read().ContentCatalog.PosterItems);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ShortcutIsRealShellLinkAndDoesNotMoveOrExecuteTarget(bool folder)
    {
        using var f = new F(); string target = folder ? f.Source : f.File("未执行.exe");
        string identity = LocalTransferFiles.Identity(target);
        string path = Create(f, NewContentKind.Shortcut, "入口.lnk", target);
        Assert.Equal(target, ShellShortcutFile.ReadStoredTarget(path));
        Assert.Equal(identity, LocalTransferFiles.Identity(target));
        if (!folder) Assert.Equal("owned-content", File.ReadAllText(target));
        Assert.Equal(path, Assert.Single(f.State.ContentCatalog.Items).Target.Locator); NoStage(f);
    }
    [Fact] public void SubsequentCreationPreservesOtherCardIdentityAndLayoutEntries()
    {
        using var f = new F(); Create(f, NewContentKind.TextFile, "one.txt");
        var card = Assert.Single(f.State.ContentCatalog.PosterItems); var item = Assert.Single(f.State.ContentCatalog.Items);
        Create(f, NewContentKind.Folder, "second");
        Assert.Contains(card, f.State.ContentCatalog.PosterItems); Assert.Contains(item, f.State.ContentCatalog.Items);
        Assert.Equal(2, f.State.ContentCatalog.PosterItems.Count); NoStage(f);
    }
    [Theory][InlineData(NewContentKind.Folder, "existing")][InlineData(NewContentKind.TextFile, "existing.txt")][InlineData(NewContentKind.Shortcut, "existing.lnk")]
    public void NameCollisionNeverOverwrites(NewContentKind kind, string name)
    {
        using var f = new F(); string path = Path.Combine(f.Collection.StorageLocator!, name); File.WriteAllText(path, "leave alone");
        long revision = f.State.Revision;
        Assert.Throws<IOException>(() => Create(f, kind, name, f.Source));
        Assert.Equal("leave alone", File.ReadAllText(path)); Assert.Equal(revision, f.State.Revision); NoStage(f);
    }
    [Theory][InlineData("../outside.txt")][InlineData("CON.txt")][InlineData("name.txt:ads")][InlineData("name.")]
    public void InvalidNamesCreateNothing(string name)
    { using var f = new F(); Assert.Throws<IOException>(() => Create(f, NewContentKind.TextFile, name)); Assert.Empty(f.Journal.Read()); NoStage(f); }
    [Fact] public void StaleDialogDoesNotCreateAnything()
    {
        using var f = new F(); var before = Location(f); Create(f, NewContentKind.TextFile, "first.txt");
        Assert.Throws<IOException>(() => new ContentCreationFixture(f.Store).Create(f.Main, before, NewContentKind.Folder, "stale"));
        Assert.Single(f.State.ContentCatalog.PosterItems); NoStage(f);
    }
    [Fact] public void CancelBeforeCreationLeavesNoStageOrReceipt()
    {
        using var f = new F(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ContentCreationFixture(f.Store).Create(f.Main, Location(f), NewContentKind.Folder, "cancel", token: cancel.Token));
        Assert.Empty(f.Journal.Read()); NoStage(f);
    }
    [Fact] public void FailureBeforeJournalCleansOnlyExactGeneratedObject()
    {
        using var f = new F(); var service = new ContentCreationFixture(f.Store) { Checkpoint = (phase, _) => { if (phase == "Created") throw new IOException("fault"); } };
        Assert.Throws<IOException>(() => service.Create(f.Main, Location(f), NewContentKind.TextFile, "fault.txt"));
        Assert.Empty(f.Journal.Read()); Assert.Empty(f.State.ContentCatalog.PosterItems); NoStage(f);
    }
    [Fact] public void ChangedStagedBytesArePreservedOnFailure()
    {
        using var f = new F(); string? staged = null;
        var service = new ContentCreationFixture(f.Store) { Checkpoint = (_, path) => { staged = path; File.WriteAllText(path, "external content"); throw new IOException("fault"); } };
        Assert.Throws<IOException>(() => service.Create(f.Main, Location(f), NewContentKind.TextFile, "fault.txt"));
        Assert.Equal("external content", File.ReadAllText(staged!)); Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void InterruptedJournalUsesExistingRecoveryWithoutReplayingCreation()
    {
        using var f = new F(); var service = new ContentCreationFixture(f.Store) { Checkpoint = (phase, _) => { if (phase == "Journaled") throw new IOException("interrupted"); } };
        Assert.Throws<IOException>(() => service.Create(f.Main, Location(f), NewContentKind.Folder, "interrupted"));
        var row = Assert.Single(f.Journal.Read()); Assert.True(Directory.Exists(row.File.Source));
        new AutomaticTransferCompletion(f.Journal).Settle();
        Assert.Empty(f.State.ContentCatalog.PosterItems); Assert.False(Directory.Exists(row.File.Destination));
        Assert.True(Assert.Single(f.Journal.Read()).IsFinished); Assert.True(Directory.Exists(row.File.Source));
    }
    [Fact] public void ExternalChangeBetweenCreationAndPreviewCannotBeCollected()
    {
        using var f = new F(); string? staged = null;
        var service = new ContentCreationFixture(f.Store) { Checkpoint = (phase, path) => { if (phase == "Created") { staged = path; File.WriteAllText(path, "external content"); } } };
        Assert.Throws<IOException>(() => service.Create(f.Main, Location(f), NewContentKind.TextFile, "changed.txt"));
        Assert.Equal("external content", File.ReadAllText(staged!)); Assert.Empty(f.Journal.Read());
        Assert.False(File.Exists(Path.Combine(f.Collection.StorageLocator!, "changed.txt"))); Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void CancelAfterJournalClosesAttemptAndCleansGeneratedObject()
    {
        using var f = new F(); using var cancel = new CancellationTokenSource();
        var service = new ContentCreationFixture(f.Store) { Checkpoint = (phase, _) => { if (phase == "Journaled") cancel.Cancel(); } };
        Assert.Throws<IOException>(() => service.Create(f.Main, Location(f), NewContentKind.Folder, "cancel", token: cancel.Token));
        Assert.True(Assert.Single(f.Journal.Read()).IsFinished); Assert.Empty(f.State.ContentCatalog.PosterItems); NoStage(f);
    }
    [Fact] public void MissingShortcutTargetCreatesNothing()
    { using var f = new F(); Assert.ThrowsAny<IOException>(() => Create(f, NewContentKind.Shortcut, "missing.lnk", Path.Combine(f.Source, "missing"))); NoStage(f); }

    [Fact] public void DefaultNameAvoidsExistingFilesAndFolders()
    {
        using var f = new F(); string directory = f.Collection.StorageLocator!;
        Assert.Equal("新建文本文档.txt", ContentCreationFixture.SuggestName(directory, NewContentKind.TextFile));
        Directory.CreateDirectory(Path.Combine(directory, "新建文本文档.txt"));
        File.WriteAllText(Path.Combine(directory, "新建文本文档 (2).txt"), "existing");
        Assert.Equal("新建文本文档 (3).txt", ContentCreationFixture.SuggestName(directory, NewContentKind.TextFile));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(directory, "新建文本文档 (2).txt")));
    }
    [Fact] public void IndexFailurePreservesGeneratedFileWithoutInventingCard()
    {
        using var f = new F();
        f.Sql("CREATE TRIGGER create_fault BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'injected'); END;");
        Assert.Throws<IOException>(() => Create(f, NewContentKind.TextFile, "fault.txt"));
        Assert.Empty(f.State.ContentCatalog.PosterItems);
        var row = Assert.Single(f.Journal.Read());
        Assert.True(File.Exists(row.File.Source) || File.Exists(row.File.Destination));
        Assert.True(row.IsFinished); // Closed attempt; not a task the user must 'resume'.
    }
}
