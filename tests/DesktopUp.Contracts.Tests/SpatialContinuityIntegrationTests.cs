using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed partial class SpatialContinuityIntegrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedCommandsShareRevisionAndRebaseFutureCollapse(bool innerFirst)
    {
        using var f = new Fixture();
        foreach (var step in new[] { ("C", false), ("B", false), (innerFirst ? "B" : "C", true), (innerFirst ? "C" : "B", true) })
        {
            var before = f.Coordinator.Refresh(); var command = f.Command(step.Item1, step.Item2);
            var result = f.Coordinator.Execute(command); Assert.True(result.Succeeded, result.Error);
            Assert.Equal(before.Revision + 1, result.CurrentRevision);
            var continuity = Assert.Single(result.Projection!.SpatialContinuities);
            foreach (var n in continuity.Nodes)
            {
                var entity = result.Projection.PostersById[n.Expected.PosterId];
                Assert.Equal(entity.LayoutGeneration, n.Expected.LayoutGeneration);
                Assert.Equal(entity.LastLayoutOperationId, n.Expected.LastLayoutOperationId);
                Assert.Equal(entity.Poster.IsCollapsed, n.Expected.IsCollapsed);
            }
            Assert.Equal(result.CurrentRevision, f.Scalar("SELECT committed_revision FROM spatial_continuity;"));
            string persisted = JsonSerializer.Serialize(continuity);
            Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
            Assert.Equal(persisted, JsonSerializer.Serialize(f.Coordinator.Refresh().SpatialContinuities.Single()));
        }
        Assert.Equal(0, f.Find("C").Poster.FallbackYDip);
        Assert.Equal(60, f.Find("B").Poster.FallbackYDip);
        Assert.Equal(120, f.Find("A").Poster.FallbackYDip);
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        Assert.Single(new SqliteStateCommandRepository(reopened).Read().SpatialContinuities);
    }

    [Fact]
    public void ConflictAndSqlFailureDoNotPartiallyAdvanceEntitiesOrContinuity()
    {
        using var f = new Fixture(); var command = f.Command("C", false);
        var b = f.Find("B"); var current = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("lock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, current.Revision, b.Poster.PosterId, b.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) }).Succeeded);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(command).Status);
        Assert.Empty(f.Coordinator.Refresh().SpatialContinuities);
        b = f.Find("B"); current = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("unlock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, current.Revision, b.Poster.PosterId, b.Poster with { IsLocked = false })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) }).Succeeded);
        command = f.Command("C", false);
        var before = f.Coordinator.Refresh();
        f.Execute("CREATE TRIGGER reject_continuity BEFORE INSERT ON spatial_continuity BEGIN SELECT RAISE(ABORT, 'injected'); END;");
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        var after = f.Coordinator.Refresh(); Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Posters.Select(p => p.Poster), after.Posters.Select(p => p.Poster));
        Assert.Empty(after.SpatialContinuities);
    }

    [Fact]
    public void NewSessionKeepsSavedPositionsButDoesNotReplayOldAnchors()
    {
        using var f = new Fixture(); Assert.True(f.Coordinator.Execute(f.Command("C", false)).Succeeded);
        double bY = f.Find("B").Poster.FallbackYDip;
        f.Environment = f.Environment with { RuntimeSessionId = Guid.NewGuid() };
        Assert.True(f.Coordinator.Execute(f.Command("C", true)).Succeeded);
        Assert.Equal(bY, f.Find("B").Poster.FallbackYDip);
    }

    [Fact]
    public void MetadataCommandDoesNotInvalidatePhysicalContinuity()
    {
        using var f = new Fixture(); Assert.True(f.Coordinator.Execute(f.Command("C", false)).Succeeded);
        var b = f.Find("B"); var current = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("rename", StateCommandSource.ControlCenter,
            PosterStateCommandKind.Rename, current.Revision, b.Poster.PosterId, b.Poster with { Title = "B renamed" })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) }).Succeeded);
        Assert.True(f.Coordinator.Execute(f.Command("C", true)).Succeeded);
        Assert.Equal(60, f.Coordinator.Refresh().PostersById[b.Poster.PosterId].Poster.FallbackYDip);
    }

    [Fact]
    public void V5MigrationKeepsFrozenSqlCreatesBackupAndAddsNoGuessedRelations()
    {
        using var f = new Fixture(false); LegacyStateDatabase.Create(f.Path, 5);
        using var store = new SqliteStateStore(f.Paths); var result = store.Initialize();
        Assert.True(result.Succeeded, result.Error); Assert.Equal(5, result.PreviousSchemaVersion); Assert.Equal(24, result.Head!.SchemaVersion);
        Assert.Equal(5, store.ValidateBackup(System.IO.Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!)).SchemaVersion);
        Assert.Empty(new SqliteStateCommandRepository(store).Read().SpatialContinuities);
    }

    [Fact]
    public void FailedV6DdlRollsBackAndMissingCurrentTableFailsClosed()
    {
        using var f = new Fixture(false); LegacyStateDatabase.Create(f.Path, 5);
        f.Execute("CREATE TABLE spatial_continuity(broken TEXT);");
        using var store = new SqliteStateStore(f.Paths); Assert.False(store.Initialize().Succeeded);
        Assert.Equal(5L, f.Scalar("PRAGMA user_version;"));
        Assert.Equal(5L, f.Scalar("SELECT COUNT(*) FROM schema_migrations;"));
        f.Execute("DROP TABLE spatial_continuity;"); Assert.True(store.Initialize().Succeeded);
        f.Execute("DROP TABLE spatial_continuity;"); Assert.False(store.Initialize().Succeeded);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopUp.Continuity.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public string Path => System.IO.Path.Combine(Paths.State, "desktopup.sqlite3");
        private SqliteStateStore? _store;
        public StateProjectionCoordinator Coordinator { get; } = null!;
        public SpatialEnvironment Environment = SpatialContinuityTests.Environment;
        public Fixture(bool initialize = true, bool automatic = false)
        {
            Paths = new(_root); Paths.EnsureCreated(); if (!initialize) return;
            _store = new(Paths); Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            var c = new PosterDefinition("C", "C", "test", "test", 0, 0, 240, 360, 0, 0, .24, .225,
                false, true, automatic, "grid", "{}", "night-ops");
            var state = Coordinator.EnsureInitialized(c);
            foreach (var entry in new[] { ("B", 60), ("A", 120) })
            {
                var r = Coordinator.Execute(new CreatePosterCommand("create-" + entry.Item1, StateCommandSource.ControlCenter,
                    state.Revision, StateReadSet.ForCatalog(state.CatalogVersion), c with { Title = entry.Item1, FallbackYDip = entry.Item2,
                        VerticalAnchor = entry.Item2 / 1240d }));
                Assert.True(r.Succeeded, r.Error); state = r.Projection!;
            }
        }
        public PosterSurfaceProjection Find(string title) => Coordinator.Refresh().Posters.Single(p => p.Poster.Title == title);
        public PosterStateCommand Command(string title, bool collapsed)
        {
            var state = Coordinator.Refresh(); var target = state.Posters.Single(p => p.Poster.Title == title);
            var scene = new SpatialSnapshot(Environment, state.Posters.Select(p => new SpatialPoster(p.Poster.PosterId,
                new((int)p.Poster.FallbackXDip, (int)p.Poster.FallbackYDip, (int)p.Poster.WidthDip,
                    p.Poster.IsCollapsed ? 60 : (int)p.Poster.ExpandedHeightDip), (int)p.Poster.ExpandedHeightDip,
                p.Poster.IsVisible, p.Poster.IsLocked, p.Poster.IsCollapsed, p.LayoutGeneration, p.LastLayoutOperationId)));
            string op = Guid.NewGuid().ToString("N"); var metrics = target.Presentation.Metrics;
            var result = SpatialContinuitySolver.Plan(scene, target.Poster.PosterId, collapsed, 60, op, metrics,
                state.SpatialContinuities.FirstOrDefault(s => s.Environment.WorkspaceId == Environment.WorkspaceId));
            Assert.True(result.Group.Plan.Succeeded, string.Join(',', result.Group.Plan.Issues));
            var final = result.Group.Plan.Changes.Single(p => p.PosterId == target.Poster.PosterId).After;
            return new(op, StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, state.Revision, target.Poster.PosterId,
                SpatialGestureCommitPolicy.Capture(target.Poster, final, Environment, target.Poster.ExpandedHeightDip) with { IsCollapsed = collapsed })
            {
                ReadSet = new(state.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: state.CatalogVersion),
                SpatialPosture = new(scene, collapsed, 60, null) { AllowPush = true, UseContinuity = true, ContentMetrics = metrics }
            };
        }
        public object? Scalar(string sql) { using var c = new SqliteConnection($"Data Source={Path};Pooling=False"); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
        public void Execute(string sql) { using var c = new SqliteConnection($"Data Source={Path};Pooling=False"); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        public void Dispose() { _store?.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
