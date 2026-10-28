using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

// R7 combined discovery keeps the old read guards; timing belongs in SessionFixture, not assertions.
public sealed class ReadObservationCostTests
{
    [Fact]
    public void UnchangedRefreshRetainsProjectionAndExternalCommitPublishesOneCoherentSnapshot()
    {
        using var f = new F();
        var coordinator = new StateProjectionCoordinator(f.Repository);
        var before = coordinator.Refresh();
        int notifications = 0; coordinator.ProjectionChanged += (_, _) => notifications++;
        Assert.Same(before, coordinator.Refresh()); Assert.Equal(0, notifications);
        var state = f.State;
        var written = f.Repository.Execute(new("external", "external", state.Revision,
            StateReadSet.ForPoster(f.Main, state.Catalog.Posters.Single().EntityVersion)),
            _ => new() { Updates = [state.Catalog.Posters.Single().Poster with { Title = "external title" }] });
        Assert.Equal(StateCommandStatus.Committed, written.Status);
        var snapshot = f.Repository.Read();
        Assert.Equal(snapshot.Revision, snapshot.Settings.Revision);
        Assert.All(snapshot.Catalog.Posters, p => Assert.Equal(snapshot.Revision, p.Revision));
        var after = coordinator.Refresh();
        Assert.Equal("external title", after.PostersById[f.Main].Poster.Title);
        Assert.Equal(before.Revision + 1, after.Revision); Assert.Equal(1, notifications);
        Assert.Same(after, coordinator.Refresh()); Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData("UPDATE schema_migrations SET checksum_sha256='corrupt' WHERE version=25")]
    [InlineData("UPDATE content_collections SET body_json=json_set(body_json,'$.unexpected',1)")]
    [InlineData("PRAGMA application_id=0")]
    [InlineData("INSERT INTO spatial_continuity SELECT 'broken',1,'{}',revision FROM state_head")]
    public void SameRevisionDoesNotBypassValidationOrReplaceLastGoodProjection(string corruption)
    {
        using var f = new F();
        var coordinator = new StateProjectionCoordinator(f.Repository);
        var before = coordinator.Refresh();
        f.Sql(corruption); // Only this fixture DB; head intentionally unchanged.
        Assert.ThrowsAny<Exception>(() => coordinator.Refresh());
        Assert.ThrowsAny<Exception>(() => new SqliteContentWatchTargetSource(f.Store).Read());
        Assert.ThrowsAny<Exception>(() => new SqliteMappedDirectoryStorage(f.Store).ReadObservation());
        Assert.True(coordinator.TryGetCurrent(out var retained)); Assert.Same(before, retained);
        Assert.Equal(before.Revision, f.Sql("SELECT revision FROM state_head"));
    }

    [Fact]
    public void DiscoveryMatchesManagedAndBoundMappingsAndRetainsMissingTargetsForRetry()
    {
        using var f = new F();
        f.File("mapped.txt");
        var state = f.State;
        var mapped = ContentTestServices.Mapping(f.Store).Create(new("mapping", StateCommandSource.ControlCenter,
            state.Revision, StateReadSet.ForCatalog(state.Catalog.CatalogVersion) with { ContentVersion = state.ContentVersion },
            PosterConfigurationFactory.NewBasicBox("mapped", "DISPLAY1")), f.Source);
        Assert.Equal(StateCommandStatus.Committed, mapped.Status);
        var source = new SqliteContentWatchTargetSource(f.Store);
        Assert.Equal(new RegisteredManagedStorage(f.Root, f.Collection), Assert.Single(f.Journal.StorageLocations()));
        var expected = new[] { new ContentWatchTarget(f.Collection.StorageLocator!, true), new ContentWatchTarget(f.Source, false) };
        Assert.Equal(expected.OrderBy(t => t.Path), source.Read().OrderBy(t => t.Path));
        string parked = f.Source + "-parked"; Directory.Move(f.Source, parked);
        Assert.Equal(expected.OrderBy(t => t.Path), source.Read().OrderBy(t => t.Path));
        Directory.Move(parked, f.Source);
        state = f.State;
        var removed = f.Repository.Execute(new("remove-map", "remove-map", state.Revision,
            StateReadSet.ForPoster(mapped.ResultPosterId!, state.Catalog.Posters.Single(p => p.Poster.PosterId == mapped.ResultPosterId).EntityVersion)
                with { CatalogVersion = state.Catalog.CatalogVersion }),
            _ => new() { Removes = [mapped.ResultPosterId!] });
        Assert.Equal(StateCommandStatus.Committed, removed.Status);
        Assert.Equal(expected[0], Assert.Single(source.Read())); // Unbound managed contents must still be watched.
        Assert.True(File.Exists(Path.Combine(f.Source, "mapped.txt")));
    }

    [Fact]
    public void CombinedDiscoveryStillDecodesManagedRootRecords()
    {
        using var f = new F();
        f.Sql("UPDATE managed_storage_roots SET body_json=json_set(body_json,'$.Unexpected',1)");
        Assert.ThrowsAny<Exception>(() => f.Journal.StorageLocations());
        Assert.ThrowsAny<Exception>(() => new SqliteContentWatchTargetSource(f.Store).Read());
    }
}
