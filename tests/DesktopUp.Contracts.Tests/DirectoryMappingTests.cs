using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class DirectoryMappingTests
{
    private sealed class Clock : TimeProvider
    { private DateTimeOffset now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => now; public void Advance() => now += TimeSpan.FromSeconds(3); }
    private static string Map(F f, string? directory = null)
    {
        var s = f.State;
        var command = new CreatePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, s.Revision,
            new(s.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: s.Catalog.CatalogVersion, ContentVersion: s.ContentVersion),
            PosterConfigurationFactory.NewBasicBox("映射", "DISPLAY1"));
        var result = new MappedDirectoryService(f.Store).Create(command, directory ?? f.Source);
        Assert.True(result.Status == StateCommandStatus.Committed, result.Error); return result.ResultPosterId!;
    }
    private static void Settle(MappedDirectoryService service, Clock clock)
    { service.Reconcile(); clock.Advance(); var result = service.Reconcile(); Assert.Empty(result.Notices); }
    [Fact] public void MappingCreatesIndependentBorrowedPosterWithoutWritingOrMovingFiles()
    {
        using var f = new F(); string path = f.File("keep.txt"); var before = f.State.Catalog.Posters.Single();
        string id = Map(f); var state = f.State;
        var after = state.Catalog.Posters.Single(p => p.Poster.PosterId == f.Main);
        Assert.Equal(before.Poster, after.Poster); Assert.Equal(before.EntityVersion, after.EntityVersion);
        var binding = Assert.Single(state.ContentCatalog.Bindings); Assert.Equal(id, binding.PosterId);
        Assert.Equal(ContentStorageOwnership.Borrowed, state.ContentCatalog.Collections.Single(c => c.Id == binding.CollectionId).Ownership);
        Assert.Single(Directory.EnumerateFileSystemEntries(f.Source)); Assert.Equal("owned-content", File.ReadAllText(path));
        Assert.Empty(f.Journal.Read()); Assert.Single(state.ContentCatalog.PosterItems);
    }
    [Fact] public void EmptyDirectoryCanBeMappedAndLaterAddsDirectChildrenOnly()
    {
        using var f = new F(); string id = Map(f); Assert.Empty(f.State.ContentCatalog.PosterItems);
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock);
        string sub = Path.Combine(f.Source, "folder"); Directory.CreateDirectory(sub); File.WriteAllText(Path.Combine(sub, "nested.txt"), "child");
        f.File("top.txt"); Settle(service, clock);
        Assert.Equal(2, f.State.ContentCatalog.PosterItems.Count); Assert.DoesNotContain(f.State.ContentCatalog.Items, i => i.DisplayName == "nested.txt");
        Assert.All(f.State.ContentCatalog.PosterItems, p => Assert.Equal(id, p.PosterId));
    }
    [Fact] public void RenamePreservesCardAndLayoutAndContentEditOnlyChangesThatItem()
    {
        using var f = new F(); string path = f.File("before.txt"); f.File("neighbor.txt"); string id = Map(f);
        var before = f.State; var original = before.ContentCatalog.Items.Single(i => i.Target.Locator == path);
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock);
        File.Move(path, Path.Combine(f.Source, "after.txt")); Settle(service, clock);
        Assert.Equal(before.ContentCatalog.PosterItems, f.State.ContentCatalog.PosterItems);
        Assert.Equal(before.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster.ContentLayoutJson, f.State.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster.ContentLayoutJson);
        Assert.Equal("after.txt", f.State.ContentCatalog.Items.Single(i => i.Id == original.Id).DisplayName);
        var neighbor = f.State.ContentCatalog.Items.Single(i => i.DisplayName == "neighbor.txt");
        File.AppendAllText(Path.Combine(f.Source, "after.txt"), "changed"); Settle(service, clock);
        Assert.Equal(neighbor, f.State.ContentCatalog.Items.Single(i => i.Id == neighbor.Id));
    }
    [Fact] public void DeleteUsesCommonRelationRemovalAndKeepsOtherCards()
    {
        using var f = new F(); string path = f.File("remove.txt"); f.File("keep.txt"); Map(f);
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock);
        File.Delete(path); Settle(service, clock);
        Assert.Single(f.State.ContentCatalog.PosterItems); Assert.Single(f.State.ContentCatalog.Memberships.Where(m => m.Origin == ContentMembershipOrigin.Source));
        Assert.True(File.Exists(Path.Combine(f.Source, "keep.txt")));
    }
    [Fact] public void MissingOrReplacedRootNeverClearsCardsOrClaimsNewDirectory()
    {
        using var f = new F(); f.File("old.txt"); Map(f); var before = f.State;
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock);
        string parked = f.Source + "-parked"; Directory.Move(f.Source, parked);
        Assert.NotEmpty(service.Reconcile().Notices); clock.Advance(); Assert.NotEmpty(service.Reconcile().Notices);
        Directory.CreateDirectory(f.Source); f.File("different.txt"); Assert.NotEmpty(service.Reconcile().Notices);
        Assert.Equal(before.ContentCatalog.PosterItems, f.State.ContentCatalog.PosterItems); Assert.Equal(before.ContentVersion, f.State.ContentVersion);
    }
    [Fact] public void RestartRetainsRootAndChildIdentityThenObservesRename()
    {
        using var f = new F(); string path = f.File("before.txt"); Map(f); var card = Assert.Single(f.State.ContentCatalog.PosterItems);
        using var restart = new SqliteStateStore(f.Paths); Assert.True(restart.Initialize().Succeeded);
        File.Move(path, Path.Combine(f.Source, "after.txt")); var clock = new Clock(); var service = new MappedDirectoryService(restart, clock); Settle(service, clock);
        Assert.Equal(card, Assert.Single(f.State.ContentCatalog.PosterItems));
    }
    [Fact] public void SameNameReplacementGetsNewCardIdentity()
    {
        using var f = new F(); string path = f.File("same.txt"); Map(f); var old = Assert.Single(f.State.ContentCatalog.PosterItems);
        File.Move(path, Path.Combine(f.DirectoryRoot, "parked.txt")); File.WriteAllText(path, "replacement");
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock); Settle(service, clock);
        Assert.NotEqual(old.Id, Assert.Single(f.State.ContentCatalog.PosterItems).Id);
    }
    [Fact] public void ExistingOwnerAndOverlappingRootsAreRejectedWithoutChangingOriginal()
    {
        using var f = new F(); string path = f.File("owned.txt"); f.AddReference(LocalContentReferenceProbe.Inspect(path).Item!); var before = f.State;
        Assert.Throws<ArgumentException>(() => Map(f)); Assert.Equal(before.Revision, f.State.Revision);
        Assert.Throws<IOException>(() => Map(f, f.Root.Locator));
    }
    [Fact] public void DuplicateMappingAndNestedMappingAreRejected()
    {
        using var f = new F(); string sub = Path.Combine(f.Source, "sub"); Directory.CreateDirectory(sub); Map(f);
        Assert.Throws<IOException>(() => Map(f)); Assert.Throws<IOException>(() => Map(f, sub));
    }
    [Theory][InlineData(NewContentKind.Folder, "folder")][InlineData(NewContentKind.TextFile, "new.txt")][InlineData(NewContentKind.Shortcut, "link.lnk")]
    public void NewContentUsesMappedDirectoryNotManagedStorage(NewContentKind kind, string name)
    {
        using var f = new F(); string id = Map(f); var service = new MappedDirectoryService(f.Store);
        var location = new NewContentLocation(null, f.State.ContentVersion, f.State.Catalog.Posters.Single(p => p.Poster.PosterId == id).EntityVersion) { Mapping = service.ResolveNewContent(id) };
        string path = new ContentCreationService(f.Store).Create(id, location, kind, name, f.Source);
        Assert.Equal(Path.Combine(f.Source, name), path); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.False(LocalTransferFiles.Exists(Path.Combine(f.Collection.StorageLocator!, name))); Assert.Empty(f.Journal.Read());
        if (kind == NewContentKind.Shortcut) Assert.Equal(f.Source, ShellShortcutFile.ReadStoredTarget(path));
    }
    [Fact] public void PosterRemovalDoesNotDeleteDirectoryOrRecreatePoster()
    {
        using var f = new F(); string path = f.File("keep.txt"); string id = Map(f); var s = f.State;
        var command = new RemovePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, s.Revision,
            new(new Dictionary<string,long> { [id] = s.Catalog.Posters.Single(p => p.Poster.PosterId == id).EntityVersion }, CatalogVersion: s.Catalog.CatalogVersion), id, true);
        var result = f.Repository.Execute(new(command.RequestId, "remove", s.Revision, command.ReadSet), _ => new() { Removes = [id] });
        Assert.Equal(StateCommandStatus.Committed, result.Status); new MappedDirectoryService(f.Store).Reconcile();
        Assert.True(File.Exists(path)); Assert.Single(f.State.Catalog.Posters); Assert.Empty(new MappedDirectoryService(f.Store).ActivePaths());
    }
    [Fact] public void ManagedIncomingRejectsMappedPosterBeforeAnyFileMove()
    {
        using var f = new F(); string id = Map(f); string file = Path.Combine(f.DirectoryRoot, "outside.txt"); File.WriteAllText(file, "remain");
        Assert.Throws<IOException>(() => DirectTransferPreparation.Incoming(f.State, id, [file], new(f.Root, f.Collection)));
        Assert.Equal("remain", File.ReadAllText(file)); Assert.Empty(f.Journal.Read());
    }
    [Fact] public void SchemaTwentyTwoUpgradesWithoutRewritingPriorLedgerOrContent()
    {
        using var f = new F(); f.AddReference(LocalContentReferenceProbe.Inspect(f.File("keep.txt")).Item!);
        var before = f.State;
        var checksum = f.Sql("SELECT checksum_sha256 FROM schema_migrations WHERE version=22");
        f.Sql(StorageSchemaFixture.Drop23 + "UPDATE state_head SET schema_version=22; PRAGMA user_version=22;");
        var result = f.Store.Initialize(); Assert.True(result.Succeeded, result.Error);
        Assert.Equal(22, result.PreviousSchemaVersion); Assert.Equal(24, result.Head!.SchemaVersion);
        Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(checksum, f.Sql("SELECT checksum_sha256 FROM schema_migrations WHERE version=22"));
        Assert.Equal(before.ContentCatalog.PosterItems, f.State.ContentCatalog.PosterItems);
        Assert.Empty(new MappedDirectoryService(f.Store).ActivePaths());
    }
    [Fact] public void FailedCreationRollsBackPosterAndEvidenceTogether()
    {
        using var f = new F(); f.File("keep.txt"); var before = f.State;
        f.Sql("CREATE TRIGGER mapping_fault BEFORE INSERT ON mapped_directory_observations BEGIN SELECT RAISE(ABORT,'injected'); END;");
        Assert.ThrowsAny<Exception>(() => Map(f));
        Assert.Equal(before.Revision, f.State.Revision); Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal("owned-content", File.ReadAllText(Path.Combine(f.Source, "keep.txt")));
    }
    [Fact] public void OversizedOrCancelledScanNeverPublishesPartialDirectory()
    {
        using var f = new F(); f.File("keep.txt"); Map(f); var before = f.State;
        for (int i = 0; i < 1000; i++) File.WriteAllText(Path.Combine(f.Source, "extra-" + i), "");
        var service = new MappedDirectoryService(f.Store);
        Assert.NotEmpty(service.Reconcile().Notices); Assert.Equal(before.Revision, f.State.Revision);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => MappedDirectoryService.Scan(f.Source, cancelled.Token));
    }
    [Fact] public void ChangedDirectoryDuringCommitIsNotPublished()
    {
        using var f = new F(); f.File("old.txt"); Map(f); f.File("new.txt"); var before = f.State;
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock) { BeforeCommit = () => f.File("late.txt") };
        service.Reconcile(); clock.Advance(); Assert.NotEmpty(service.Reconcile().Notices);
        Assert.Equal(before.Revision, f.State.Revision);
    }
    [Fact] public void NativeMoveThenObserverUsesCommonRemovalWithoutRecreatingCard()
    {
        using var f = new F(); string path = f.File("exit.txt"); string id = Map(f);
        var card = Assert.Single(f.State.ContentCatalog.PosterItems); var journal = new NativeShellExitJournal(f.Store);
        var intent = journal.Begin(id, [card.LayoutItemId]); string destination = Path.Combine(f.DirectoryRoot, "exited.txt");
        File.Move(path, destination); journal.End(intent, true);
        Assert.Equal(1, journal.ObserveReleasedMove(intent)); journal.Close(intent);
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock); Settle(service, clock);
        Assert.Empty(f.State.ContentCatalog.PosterItems); Assert.Empty(f.State.ContentCatalog.Memberships);
        Assert.Equal("owned-content", File.ReadAllText(destination));
    }
    [Fact] public void SameNameReplacementRejectsNativeActionsButMissingSourceKeepsStaleDragProtocol()
    {
        using var f = new F(); string path = f.File("old.txt"); string id = Map(f);
        var card = Assert.Single(f.State.ContentCatalog.PosterItems); var journal = new NativeShellExitJournal(f.Store);
        File.Move(path, Path.Combine(f.DirectoryRoot, "old.txt"));
        var stale = journal.Begin(id, [card.LayoutItemId]); Assert.True(stale.Items[0].Missing); journal.End(stale, false);
        File.WriteAllText(path, "replacement");
        Assert.Throws<IOException>(() => journal.Begin(id, [card.LayoutItemId]));
        Assert.Throws<IOException>(() => new MappedDirectoryService(f.Store).ValidateAction(id, card.LayoutItemId));
        Assert.Equal("replacement", File.ReadAllText(path));
    }
    [Fact] public void MappedNewContentFailureCleansOnlyOwnUnchangedResult()
    {
        using var f = new F(); string id = Map(f); var service = new MappedDirectoryService(f.Store);
        var location = new NewContentLocation(null, f.State.ContentVersion, f.State.Catalog.Posters.Single(p => p.Poster.PosterId == id).EntityVersion) { Mapping = service.ResolveNewContent(id) };
        service.BeforeCommit = () => throw new IOException("injected");
        Assert.Throws<IOException>(() => service.CreateNew(id, location, NewContentKind.TextFile, "new.txt", [], default));
        Assert.False(File.Exists(Path.Combine(f.Source, "new.txt"))); Assert.Empty(f.State.ContentCatalog.PosterItems);
        service.BeforeCommit = () => File.WriteAllText(Path.Combine(f.Source, "new.txt"), "external");
        Assert.Throws<IOException>(() => service.CreateNew(id, location, NewContentKind.TextFile, "new.txt", [], default));
        Assert.Equal("external", File.ReadAllText(Path.Combine(f.Source, "new.txt"))); Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void MappedRenameThenObserverKeepsCardIdentityAndDoesNotRecommitUnchangedSnapshot()
    {
        using var f = new F(); f.File("old.txt"); string id = Map(f);
        var card = Assert.Single(f.State.ContentCatalog.PosterItems); var item = Assert.Single(f.State.ContentCatalog.Items);
        new ContentRenameService(f.Store).Rename(id, card.LayoutItemId, item.Id, item.Version, "new.txt");
        var clock = new Clock(); var service = new MappedDirectoryService(f.Store, clock); Settle(service, clock);
        Assert.Equal(card, Assert.Single(f.State.ContentCatalog.PosterItems)); long revision = f.State.Revision;
        Assert.False(service.Reconcile().Changed); Assert.Equal(revision, f.State.Revision);
    }
}
