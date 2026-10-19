using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentDeletionTests
{
    private sealed class Shell(Func<string, Func<bool>, ShellDeleteResult> run) : IShellFileDeletion
    {
        public int Calls;
        public Task<ShellDeleteResult> Recycle(string path, nint owner, Func<bool> current, CancellationToken token)
        { Calls++; return Task.FromResult(run(path, current)); }
    }
    private static string Collect(F f, string name = "delete.txt")
    {
        var staged = f.Stage(f.File(name));
        Assert.Equal(DurableTransferPhase.IndexCommitted, Assert.Single(f.Executor.Execute(staged.Plan.Request.OperationId)).Phase);
        return staged.Files[0].Destination;
    }
    private static string[] Cards(F f) => f.State.ContentCatalog.PosterItems.Select(p => p.LayoutItemId).ToArray();
    // Fake Shell parks only fixture-owned objects: proves app observation/cleanup without
    // putting test files into the user's Recycle Bin or deleting any user content.
    private static ShellDeleteResult ParkFile(F f, string path, Func<bool> current)
    {
        Assert.True(current());
        Assert.True(Assert.Single(new NativeShellExitJournal(f.Store).Pending()).ExplicitDelete);
        string target = Path.Combine(f.DirectoryRoot, "recycled-" + Path.GetFileName(path));
        if (Directory.Exists(path)) Directory.Move(path, target); else File.Move(path, target);
        return new(false, 0);
    }
    private static Shell Park(F f) => new((path, current) => ParkFile(f, path, current));
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RealAbsenceRemovesCardButDoesNotReexecuteOnRestart(bool legacy)
    {
        using var f = new F(); string path;
        if (legacy) { path = f.File("legacy.txt"); f.AddReference(LocalContentReferenceProbe.Inspect(path).Item!); }
        else path = Collect(f);
        // A legacy source is already in Source; park under a different filename.
        var shell = Park(f);
        var result = await new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0);
        Assert.Equal(1, result.Removed); Assert.Empty(result.Errors); Assert.False(File.Exists(path));
        Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal("owned-content", File.ReadAllText(Path.Combine(f.DirectoryRoot, "recycled-" + Path.GetFileName(path))));
        using var restart = new SqliteStateStore(f.Paths); Assert.True(restart.Initialize().Succeeded);
        new NativeShellExitJournal(restart).Reconcile(); Assert.Equal(1, shell.Calls);
    }
    [Theory][InlineData(true, 0)][InlineData(false, -2147467259)][InlineData(false, 0)]
    public async Task CancelFailureOrFalseSuccessNeverDetachesAnExistingFile(bool cancel, int hr)
    {
        using var f = new F(); string path = Collect(f); var before = f.State;
        var result = await new ContentDeletionService(f.Store, new Shell((_, _) => new(cancel, hr))).Delete(f.Main, Cards(f), 0);
        Assert.Equal(0, result.Removed); Assert.True(File.Exists(path));
        Assert.Equal(before.ContentCatalog.PosterItems, f.State.ContentCatalog.PosterItems);
        Assert.Equal(before.ContentVersion, f.State.ContentVersion);
    }
    [Fact] public async Task PartialBatchOnlyRemovesActuallyDeletedObjects()
    {
        using var f = new F(); Collect(f, "one.txt"); Collect(f, "two.txt"); int calls = 0;
        var shell = new Shell((path, current) => ++calls == 1 ? ParkFile(f, path, current) : new(true, 0));
        var result = await new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0);
        Assert.Equal(1, result.Removed); Assert.True(result.Cancelled); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task ReplacementIsNotDeletedOrDetached()
    {
        using var f = new F(); string original = Collect(f);
        var shell = new Shell((path, current) =>
        {
            File.Move(path, Path.Combine(f.Source, "original-kept.txt")); File.WriteAllText(path, "replacement");
            Assert.False(current()); return new(false, unchecked((int)0x80004004));
        });
        var result = await new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0);
        Assert.Equal(0, result.Removed); Assert.Single(f.State.ContentCatalog.PosterItems); Assert.Equal("replacement", File.ReadAllText(original));
    }
    [Fact] public async Task MissingFileNeverFallsBackToReferenceOnlyDelete()
    {
        using var f = new F(); string path = Collect(f); File.Move(path, Path.Combine(f.Source, "moved.txt"));
        var shell = Park(f);
        await Assert.ThrowsAsync<IOException>(() => new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0));
        Assert.Equal(0, shell.Calls); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public async Task IndexFailureRetainsReceiptAndRestartOnlyObserves()
    {
        using var f = new F(); Collect(f);
        f.Sql("CREATE TRIGGER delete_fault BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'injected'); END;");
        var shell = Park(f); var result = await new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0);
        Assert.NotEmpty(result.Errors); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Single(new NativeShellExitJournal(f.Store).Pending());
        f.Sql("DROP TRIGGER delete_fault;");
        using var restart = new SqliteStateStore(f.Paths); Assert.True(restart.Initialize().Succeeded);
        Assert.Equal(1, new NativeShellExitJournal(restart).Reconcile()); Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal(1, shell.Calls);
    }
    [Fact] public void OldDragReceiptSerializationDoesNotAcquireNewFields()
    {
        var intent = new ShellExitIntent(Guid.NewGuid(), "poster", DateTimeOffset.UtcNow, []);
        Assert.DoesNotContain("ExplicitDelete", System.Text.Json.JsonSerializer.Serialize(intent));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FolderAndMappedFolderUseActualRemovalWithoutDeletingTheMappingRoot(bool mapped)
    {
        using var f = new F(); string folder = Path.Combine(f.Source, "folder"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "keep child bytes");
        string posterId = f.Main;
        if (mapped)
        {
            var state = f.State;
            var command = new CreatePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, state.Revision,
                new(state.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: state.Catalog.CatalogVersion, ContentVersion: state.ContentVersion),
                PosterConfigurationFactory.NewBasicBox("mapped", "DISPLAY1"));
            var creation = new MappedDirectoryService(f.Store).Create(command, f.Source);
            Assert.Equal(StateCommandStatus.Committed, creation.Status); posterId = creation.ResultPosterId!;
        }
        else f.AddReference(LocalContentReferenceProbe.Inspect(folder).Item!);
        var result = await new ContentDeletionService(f.Store, Park(f)).Delete(posterId, Cards(f), 0);
        Assert.Equal(1, result.Removed); Assert.Empty(result.Errors); Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.True(Directory.Exists(f.Source)); Assert.False(Directory.Exists(folder));
        Assert.Equal("keep child bytes", File.ReadAllText(Path.Combine(f.DirectoryRoot, "recycled-folder", "child.txt")));
        if (mapped) Assert.Contains(f.State.Catalog.Posters, p => p.Poster.PosterId == posterId);
    }
    [Fact] public async Task CancellationBeforeStartDoesNotWriteIntentOrCallShell()
    {
        using var f = new F(); string path = Collect(f); var shell = Park(f);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        var result = await new ContentDeletionService(f.Store, shell).Delete(f.Main, Cards(f), 0, stop.Token);
        Assert.True(result.Cancelled); Assert.Equal(0, shell.Calls); Assert.True(File.Exists(path));
        Assert.Empty(new NativeShellExitJournal(f.Store).Pending());
    }
    [Fact] public void RecycleFlagsPreserveSystemWarningsAndUndo()
    {
        uint flags = ShellDirectoryTransfer.RecycleFlags;
        Assert.NotEqual(0u, flags & 0x80000); Assert.NotEqual(0u, flags & 0x4000);
        Assert.NotEqual(0u, flags & 0x20000000); Assert.Equal(0u, flags & (0x10 | 0x400));
    }
}
