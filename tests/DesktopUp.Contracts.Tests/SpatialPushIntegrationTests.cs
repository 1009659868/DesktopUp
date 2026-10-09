using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialPushIntegrationTests
{
    private static readonly SpatialEnvironment Environment = TestEnvironment with { WorkArea = new(0, 0, 1200, 1400) };
    private static SpatialSnapshot Chain() => new(Environment, [
        Poster("A", 0, 0, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 },
        Poster("B", 0, 70), Poster("C", 0, 400), Poster("D", 700, 200)]);

    [Fact]
    public void ExpansionPushesOnlyTheMinimumChainAndEverySimultaneousFrameIsSafe()
    {
        var scene = Chain();
        var group = SpatialPushSolver.Plan(scene, "A", false, 60, "expand");
        Assert.True(group.Plan.Succeeded);
        Assert.Equal(new[] { "A", "B", "C" }, group.Plan.Changes.Select(c => c.PosterId));
        Assert.Equal(600, group.Plan.Changes.Single(c => c.PosterId == "B").After.Y);
        Assert.Equal(840, group.Plan.Changes.Single(c => c.PosterId == "C").After.Y);
        AssertFrames(scene, group);
        Assert.Equal(2, group.NewProof!.Participants.Count);
        var reversed = SpatialPushSolver.Plan(new(scene.Environment, scene.Posters.Reverse()), "A", false, 60, "expand");
        Assert.Equal(group.Plan.Changes, reversed.Plan.Changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FixedObstacleOrFullWorkspaceRejectsTheWholeGroup(bool locked)
    {
        var initial = Chain();
        var scene = locked ? new SpatialSnapshot(initial.Environment, initial.Posters.Select(p => p.PosterId == "C" ? p with { IsLocked = true } : p))
            : new SpatialSnapshot(initial.Environment with { WorkArea = new(0, 0, 1200, 800) }, initial.Posters);
        var group = SpatialPushSolver.Plan(scene, "A", false, 60, "expand");
        Assert.False(group.Plan.Succeeded); Assert.Empty(group.Plan.Changes); Assert.Empty(group.Frames); Assert.Null(group.NewProof);
    }

    [Fact]
    public void UpwardGrowthPushesAboveNeighborsWithoutMovingLockedSourceSideways()
    {
        var source = Poster("A", 0, 1340, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600, IsLocked = true };
        var scene = new SpatialSnapshot(Environment, [source, Poster("B", 0, 1090), Poster("C", 0, 700)]);
        var group = SpatialPushSolver.Plan(scene, "A", false, 60, "up");
        Assert.True(group.Plan.Succeeded);
        Assert.Equal(800, group.Plan.Changes.Single(p => p.PosterId == "A").After.Y);
        Assert.All(group.Plan.Changes, p => Assert.Equal(0, p.After.X));
        AssertFrames(scene, group);
    }

    [Fact]
    public void UntouchedChainReturnsAndConsumesProofOnlyOnce()
    {
        var initial = Chain(); var expanded = SpatialPushSolver.Plan(initial, "A", false, 60, "expand");
        var current = After(initial, expanded, "expand");
        var collapsed = SpatialPushSolver.Plan(current, "A", true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded); Assert.Equal("expand", collapsed.ConsumeOperationId);
        AssertFrames(current, collapsed);
        Assert.All(collapsed.Plan.Changes, c => Assert.Equal(initial.Posters.Single(p => p.PosterId == c.PosterId).Bounds, c.After));
        var noProof = SpatialPushSolver.Plan(current, "A", true, 60, "again", expanded.NewProof! with { Status = "consumed" });
        Assert.Single(noProof.Plan.Changes); Assert.Null(noProof.ConsumeOperationId);
    }

    [Fact]
    public void MovedPeerStaysWhileAnotherUntouchedPeerCanReturn()
    {
        var initial = Chain(); var expanded = SpatialPushSolver.Plan(initial, "A", false, 60, "expand");
        var current = After(initial, expanded, "expand");
        current = new(current.Environment, current.Posters.Select(p => p.PosterId == "B" ? p with
            { Bounds = p.Bounds with { X = 400 }, LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = "manual" } : p));
        var collapsed = SpatialPushSolver.Plan(current, "A", true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded);
        Assert.DoesNotContain(collapsed.Plan.Changes, c => c.PosterId == "B");
        Assert.Equal(400, collapsed.Plan.Changes.Single(c => c.PosterId == "C").After.Y);
        AssertFrames(current, collapsed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NewSessionOrSourceGenerationChangeDisablesOldAutomaticReturn(bool newSession)
    {
        var initial = Chain(); var expanded = SpatialPushSolver.Plan(initial, "A", false, 60, "expand");
        var current = After(initial, expanded, "expand");
        current = new(newSession ? current.Environment with { RuntimeSessionId = Guid.NewGuid() } : current.Environment,
            current.Posters.Select(p => !newSession && p.PosterId == "A" ? p with { LayoutGeneration = p.LayoutGeneration + 1 } : p));
        var collapsed = SpatialPushSolver.Plan(current, "A", true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded); Assert.Single(collapsed.Plan.Changes); Assert.Null(collapsed.ConsumeOperationId);
    }

    [Fact]
    public void ActualSqliteBatchSharesOneRevisionAndReplaysWithoutMovingAgain()
    {
        using var f = new Fixture(); var before = f.Coordinator.Refresh(); var command = f.Command(false);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
        Assert.All(result.Projection!.Posters, p => { Assert.Equal(result.CurrentRevision, p.PosterRevision); Assert.Equal(1, p.LayoutGeneration); });
        Assert.Single(result.Projection.SpatialOperations);
        Assert.Equal(2, result.Projection.SpatialOperations[0].Participants.Count);
        var replay = f.Coordinator.Execute(command); Assert.Equal(StateCommandStatus.Replayed, replay.Status);
        Assert.Equal(result.CurrentRevision, replay.CurrentRevision);
        var collapse = f.Coordinator.Execute(f.Command(true)); Assert.True(collapse.Succeeded, collapse.Error);
        Assert.Empty(collapse.Projection!.SpatialOperations);
        Assert.Equal(70, f.Find("B").Poster.FallbackYDip); Assert.Equal(400, f.Find("C").Poster.FallbackYDip);
        Assert.Equal(1L, f.Scalar("SELECT COUNT(*) FROM spatial_operations WHERE status='consumed';"));
    }

    [Fact]
    public void ReturningToOriginalCoordinatesStillInvalidatesAUserAdjustedPeer()
    {
        using var f = new Fixture(); Assert.True(f.Coordinator.Execute(f.Command(false)).Succeeded);
        var b = f.Find("B"); var snapshot = f.Scene(); var projection = f.Coordinator.Refresh();
        var session = new SpatialGestureSession(b.Poster.PosterId, snapshot, projection, PosterGestureKind.Move, PosterOperatingMode.Using, 240, 240);
        var original = session.Presented;
        Assert.True(session.Accept(session.Preview(original with { X = 700 })));
        Assert.True(session.Accept(session.Preview(original)));
        var move = f.Coordinator.Execute(new PosterStateCommand(session.OperationId, StateCommandSource.Poster,
            PosterStateCommandKind.Move, projection.Revision, b.Poster.PosterId, b.Poster)
            { ReadSet = session.Reads, SpatialGesture = session.Freeze() });
        Assert.True(move.Succeeded, move.Error);
        Assert.Equal(b.LayoutGeneration + 1, f.Find("B").LayoutGeneration);
        var collapse = f.Coordinator.Execute(f.Command(true)); Assert.True(collapse.Succeeded, collapse.Error);
        Assert.Equal(600, f.Find("B").Poster.FallbackYDip);
    }

    [Fact]
    public void UnrelatedRenameDoesNotDestroyReturnEligibility()
    {
        using var f = new Fixture(); Assert.True(f.Coordinator.Execute(f.Command(false)).Succeeded);
        var a = f.Find("A");
        var rename = f.Coordinator.Execute(new PosterStateCommand("rename", StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
            a.Revision, a.Poster.PosterId, a.Poster with { Title = "renamed" }) { ReadSet = StateReadSet.ForPoster(a.Poster.PosterId, a.EntityVersion) });
        Assert.True(rename.Succeeded); Assert.Equal(a.LayoutGeneration, rename.Projection!.PostersById["A"].LayoutGeneration);
        Assert.Single(rename.Projection.SpatialOperations);
        Assert.True(f.Coordinator.Execute(f.Command(true)).Succeeded);
        Assert.Equal(70, f.Find("B").Poster.FallbackYDip);
    }

    [Fact]
    public void SqliteFailureInMiddleRollsBackAllGeometryGenerationsAndProofs()
    {
        using var f = new Fixture(); var before = f.Coordinator.Refresh();
        f.Execute("CREATE TRIGGER reject_second BEFORE UPDATE ON posters WHEN old.title='B' BEGIN SELECT RAISE(ABORT, 'injected spatial failure'); END;");
        var result = f.Coordinator.Execute(f.Command(false));
        Assert.False(result.Succeeded);
        var after = f.Coordinator.Refresh(); Assert.Equal(before.Revision, after.Revision);
        Assert.All(after.Posters, p => Assert.Equal(0, p.LayoutGeneration));
        Assert.True(after.PostersById["A"].Poster.IsCollapsed); Assert.Empty(after.SpatialOperations);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM spatial_participants;"));
    }

    [Fact]
    public void NewObstacleInvalidatesWholePostureBeforeAnyBatchWrite()
    {
        using var f = new Fixture(); var command = f.Command(false); var before = f.Coordinator.Refresh();
        var added = f.Coordinator.Execute(new CreatePosterCommand("new", StateCommandSource.Tray, before.Revision,
            StateReadSet.ForCatalog(before.CatalogVersion), before.PostersById["A"].Poster with { FallbackXDip = 600 }));
        Assert.True(added.Succeeded);
        var result = f.Coordinator.Execute(command); Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal(added.CurrentRevision, result.CurrentRevision); Assert.Empty(result.Projection!.SpatialOperations);
    }

    [Fact]
    public void V4MigratesWithBackupAndNoInventedCausality()
    {
        using var f = new MigrationFixture(); LegacyStateDatabase.Create(f.Path, 4);
        using var store = new SqliteStateStore(f.Paths); var result = store.Initialize();
        Assert.True(result.Succeeded, result.Error); Assert.Equal(4, result.PreviousSchemaVersion); Assert.Equal(14, result.Head!.SchemaVersion);
        Assert.Equal(4, store.ValidateBackup(System.IO.Path.Combine(f.Paths.Backups, result.PreMigrationBackupFileName!)).SchemaVersion);
        Assert.Empty(new SqliteStateCommandRepository(store).Read().SpatialOperations);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM spatial_participants;"));
    }

    [Fact]
    public void FailedV5MigrationRollsBackToV4AndMissingCurrentTableFailsClosed()
    {
        using var f = new MigrationFixture(); LegacyStateDatabase.Create(f.Path, 4);
        f.Execute("CREATE TABLE spatial_participants(broken TEXT);");
        using var store = new SqliteStateStore(f.Paths); Assert.False(store.Initialize().Succeeded);
        Assert.Equal(4L, f.Scalar("PRAGMA user_version;"));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM pragma_table_info('posters') WHERE name='layout_generation';"));
        f.Execute("DROP TABLE spatial_participants;"); Assert.True(store.Initialize().Succeeded);
        f.Execute("DROP TABLE spatial_participants;"); Assert.False(store.Initialize().Succeeded);
    }

    private static SpatialSnapshot After(SpatialSnapshot before, SpatialGroupPlan group, string id) => new(before.Environment,
        before.Posters.Select(p => group.Plan.Changes.FirstOrDefault(c => c.PosterId == p.PosterId) is { } c
            ? p with { Bounds = c.After, IsCollapsed = c.IsCollapsed, LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = id } : p));
    internal static void AssertFrames(SpatialSnapshot initial, SpatialGroupPlan group)
    {
        var current = initial.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
        foreach (var frame in group.Frames)
        {
            var before = new Dictionary<string, SpatialRect>(current);
            foreach (var change in frame.Changes) { Assert.Equal(before[change.PosterId], change.From); current[change.PosterId] = change.To; }
            var ids = current.Keys.ToArray();
            foreach (var rect in current.Values) Assert.True(initial.Environment.WorkArea.Contains(rect));
            for (int i = 0; i < ids.Length; i++) for (int j = i + 1; j < ids.Length; j++)
                Assert.False(SpatialPathPolicy.CrossesMoving(before[ids[i]], current[ids[i]], before[ids[j]], current[ids[j]]));
        }
    }

    private class MigrationFixture : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopUp.SpatialPush.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public string Path => System.IO.Path.Combine(Paths.State, "desktopup.sqlite3");
        public MigrationFixture() { Paths = new(_root); Paths.EnsureCreated(); }
        public object? Scalar(string sql) { using var c = new SqliteConnection($"Data Source={Path};Pooling=False"); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
        public void Execute(string sql) { using var c = new SqliteConnection($"Data Source={Path};Pooling=False"); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        public virtual void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
    private sealed class Fixture : MigrationFixture
    {
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            _store = new(Paths); Assert.True(_store.Initialize().Succeeded); Coordinator = new(new SqliteStateCommandRepository(_store));
            var a = new PosterDefinition("A", "A", "test", "display-1", 0, 0, 240, 600, 0, 0, .2, 600 / 1400d, false, true, false, "grid", "{}", "box");
            var current = Coordinator.EnsureInitialized(a);
            foreach (var entry in new[] { ("B", 70), ("C", 400) })
            {
                var result = Coordinator.Execute(new CreatePosterCommand("create-" + entry.Item1, StateCommandSource.ControlCenter,
                    current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), a with { Title = entry.Item1, FallbackYDip = entry.Item2,
                        ExpandedHeightDip = 240, IsCollapsed = false, VerticalAnchor = entry.Item2 / 1160d, NormalizedExpandedHeight = 240 / 1400d }));
                Assert.True(result.Succeeded, result.Error); current = result.Projection!;
            }
        }
        public PosterSurfaceProjection Find(string title) => Coordinator.Refresh().Posters.Single(p => p.Poster.Title == title);
        public SpatialSnapshot Scene() => new(Environment, Coordinator.Refresh().Posters.Select(p => new SpatialPoster(p.Poster.PosterId,
            new((int)p.Poster.FallbackXDip, (int)p.Poster.FallbackYDip, (int)p.Poster.WidthDip, p.Poster.IsCollapsed ? 60 : (int)p.Poster.ExpandedHeightDip),
            (int)p.Poster.ExpandedHeightDip, p.Poster.IsVisible, p.Poster.IsLocked, p.Poster.IsCollapsed, p.LayoutGeneration, p.LastLayoutOperationId)));
        public PosterStateCommand Command(bool collapsed)
        {
            var state = Coordinator.Refresh(); var scene = Scene(); var id = Guid.NewGuid().ToString("N");
            var proof = collapsed ? state.SpatialOperations.FirstOrDefault(p => p.ExpandedSource.PosterId == "A") : null;
            var group = SpatialPushSolver.Plan(scene, "A", collapsed, 60, id, proof); Assert.True(group.Plan.Succeeded);
            return new(id, StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, state.Revision, "A",
                SpatialGestureCommitPolicy.Capture(state.PostersById["A"].Poster, group.Plan.Changes.Single(c => c.PosterId == "A").After, Environment, 600)
                    with { IsCollapsed = collapsed })
            {
                ReadSet = new(state.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: state.CatalogVersion),
                SpatialPosture = new(scene, collapsed, 60, null) { AllowPush = true, ReturnOperationId = proof?.OperationId }
            };
        }
        public override void Dispose() { _store.Dispose(); base.Dispose(); }
    }
}
