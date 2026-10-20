using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

// P1's new boundary only. Existing mapping tests retain normal file/identity/scan coverage.
public sealed class MappedObservationReadTests
{
    [Fact]
    public void ScopedPassOnlyPreparesAndObservesFreshlyRegisteredRequestedRoots()
    {
        using var f = new F();
        string first = Map(f, f.Source), otherPath = NewDirectory(f), second = Map(f, otherPath);
        f.File("first-only.txt"); File.WriteAllText(Path.Combine(otherPath, "later.txt"), "owned");
        var scope = new HashSet<string>([f.Source, Path.Combine(f.DirectoryRoot, "not-registered")], StringComparer.OrdinalIgnoreCase);
        var storage = new SqliteMappedDirectoryStorage(f.Store);
        Assert.Equal(Collection(f.State, first), Assert.Single(storage.ReadObservation(directories: scope).Directories).Key);
        var clock = new Clock(); var service = ContentTestServices.Mapping(f.Store, clock);
        Assert.Equal(f.Source, Assert.Single(service.Reconcile(directories: scope).Retries).Path);
        clock.Advance(); Assert.True(service.Reconcile(directories: scope).Changed);
        Assert.Single(f.State.ContentCatalog.PosterItems.Where(p => p.PosterId == first));
        Assert.Empty(f.State.ContentCatalog.PosterItems.Where(p => p.PosterId == second));
        service.Reconcile(); clock.Advance(); Assert.True(service.Reconcile().Changed);
        Assert.Single(f.State.ContentCatalog.PosterItems.Where(p => p.PosterId == second));
    }

    [Fact]
    public void ReadIsDetachedAndEachPassReadsSameRevisionEvidenceAgain()
    {
        using var f = new F();
        string id = Map(f, f.Source);
        var storage = new SqliteMappedDirectoryStorage(f.Store);
        var first = storage.ReadObservation();
        Guid collection = Collection(first.State, id);
        var evidence = first.Directories[collection].Evidence!;
        Assert.Equal(storage.Evidence(collection).Identity, evidence.Identity);
        Assert.Equal(storage.Evidence(collection).Items, evidence.Items);
        f.Sql($"UPDATE mapped_directory_observations SET root_identity='external-change' WHERE collection_id='{collection}'");
        var next = storage.ReadObservation();
        Assert.Equal(first.State.Revision, next.State.Revision);
        Assert.Equal("external-change", next.Directories[collection].Evidence!.Identity);
        Assert.NotEqual("external-change", evidence.Identity);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => storage.ReadObservation(cancelled.Token));
    }

    [Theory]
    [InlineData("DELETE FROM mapped_directory_observations WHERE collection_id=")]
    [InlineData("UPDATE mapped_directory_observations SET observations_json='{}' WHERE collection_id=")]
    [InlineData("UPDATE mapped_directory_observations SET observations_json='[null]' WHERE collection_id=")]
    [InlineData("UPDATE mapped_directory_observations SET observations_json=printf('%600000s','')||'[]' WHERE collection_id=")]
    public void BadDirectoryEvidenceKeepsItsCardsWithoutBlockingHealthyDirectory(string sql)
    {
        using var f = new F();
        f.File("keep.txt");
        string bad = Map(f, f.Source), goodPath = NewDirectory(f), good = Map(f, goodPath);
        var before = f.State;
        Guid collection = Collection(before, bad);
        var original = before.ContentCatalog.PosterItems.Single(p => p.PosterId == bad);
        f.Sql(sql + $"'{collection}'");
        File.WriteAllText(Path.Combine(goodPath, "added.txt"), "owned");
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock);
        Assert.True(service.Reconcile().Pending);
        clock.Advance();
        var result = service.Reconcile();
        Assert.True(result.Changed);
        Assert.Single(result.Notices);
        Assert.Equal(original, f.State.ContentCatalog.PosterItems.Single(p => p.PosterId == bad));
        Assert.Single(f.State.ContentCatalog.PosterItems.Where(p => p.PosterId == good));
        Assert.True(File.Exists(Path.Combine(f.Source, "keep.txt")));
    }

    [Fact]
    public void CommitReadsNewEvidenceEvenWhenRevisionDidNotChange()
    {
        using var f = new F();
        string id = Map(f, f.Source);
        Guid collection = Collection(f.State, id);
        f.File("incoming.txt");
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock, () =>
            f.Sql($"UPDATE mapped_directory_observations SET root_identity='replaced' WHERE collection_id='{collection}'"));
        var before = f.State;
        service.Reconcile(); clock.Advance();
        var result = service.Reconcile();
        Assert.False(result.Changed); Assert.NotEmpty(result.Notices);
        Assert.Equal(before.Revision, f.State.Revision);
        Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.True(File.Exists(Path.Combine(f.Source, "incoming.txt")));
    }

    [Fact]
    public void CommitRevalidatesManagedRootOverlap()
    {
        using var f = new F();
        Map(f, f.Source);
        f.File("incoming.txt");
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock, () =>
            f.Sql($"UPDATE managed_storage_roots SET body_json=json_set(body_json,'$.Locator','{f.Source.Replace("'", "''")}')"));
        service.Reconcile(); clock.Advance();
        var result = service.Reconcile();
        Assert.False(result.Changed); Assert.NotEmpty(result.Notices);
        Assert.Empty(f.State.ContentCatalog.PosterItems);
    }

    [Fact]
    public void ConcurrentPosterVersionChangeCannotBeOverwrittenByPreparedRead()
    {
        using var f = new F();
        string id = Map(f, f.Source);
        f.File("incoming.txt");
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock, () =>
        {
            var state = f.State;
            var poster = state.Catalog.Posters.Single(p => p.Poster.PosterId == id);
            var changed = f.Repository.Execute(new(Guid.NewGuid().ToString(), "external", state.Revision,
                StateReadSet.ForPoster(id, poster.EntityVersion)),
                _ => new() { Updates = [poster.Poster with { Title = "external title" }] });
            Assert.Equal(StateCommandStatus.Committed, changed.Status);
        });
        service.Reconcile(); clock.Advance();
        var result = service.Reconcile();
        Assert.False(result.Changed); Assert.NotEmpty(result.Notices);
        Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal("external title", f.State.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster.Title);
    }

    [Fact]
    public void OwnCommitRefreshesStateAndLaterPassStillObservesNewChanges()
    {
        using var f = new F();
        Map(f, f.Source);
        string second = NewDirectory(f); Map(f, second);
        f.File("first.txt"); File.WriteAllText(Path.Combine(second, "second.txt"), "owned");
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock);
        service.Reconcile(); clock.Advance();
        var result = service.Reconcile();
        Assert.True(result.Changed); Assert.True(result.Pending); Assert.Empty(result.Notices);
        Assert.Single(f.State.ContentCatalog.PosterItems);
        clock.Advance();
        Assert.Empty(service.Reconcile().Notices);
        Assert.Equal(2, f.State.ContentCatalog.PosterItems.Count);
        Assert.False(service.Reconcile().Changed);
        File.Move(Path.Combine(second, "second.txt"), Path.Combine(second, "renamed.txt"));
        service.Reconcile(); clock.Advance();
        Assert.True(service.Reconcile().Changed);
        Assert.Contains(f.State.ContentCatalog.Items, item => item.DisplayName == "renamed.txt");
    }

    [Fact]
    public void HeldExecutionLockStopsCommitWithoutDroppingFileOrCard()
    {
        using var f = new F();
        Map(f, f.Source); string file = f.File("incoming.txt");
        var before = f.State;
        var clock = new Clock();
        var service = ContentTestServices.Mapping(f.Store, clock);
        service.Reconcile(); clock.Advance();
        using var held = new FileStream(Path.Combine(f.Paths.State, "transfer-executor.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var result = service.Reconcile();
        Assert.False(result.Changed); Assert.True(result.Pending); Assert.NotEmpty(result.Notices);
        Assert.Equal(before.Revision, f.State.Revision);
        Assert.True(File.Exists(file)); Assert.Empty(f.State.ContentCatalog.PosterItems);
    }

    private static Guid Collection(DesktopStateSnapshot state, string poster) =>
        state.ContentCatalog.Bindings.Single(b => b.PosterId == poster).CollectionId;
    private static string NewDirectory(F f)
    {
        string path = Path.Combine(f.DirectoryRoot, "second");
        Directory.CreateDirectory(path);
        return path;
    }
    private static string Map(F f, string path)
    {
        var state = f.State;
        var result = ContentTestServices.Mapping(f.Store).Create(new(Guid.NewGuid().ToString(),
            StateCommandSource.ControlCenter, state.Revision,
            StateReadSet.ForCatalog(state.Catalog.CatalogVersion) with { ContentVersion = state.ContentVersion },
            PosterConfigurationFactory.NewBasicBox("P1 map", "DISPLAY1")), path);
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        return result.ResultPosterId!;
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance() => now += TimeSpan.FromSeconds(3);
    }
}
