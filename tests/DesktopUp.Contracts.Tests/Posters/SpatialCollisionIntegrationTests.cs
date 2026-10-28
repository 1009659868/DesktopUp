using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialCollisionIntegrationTests
{
    [Fact]
    public void HighSpeedDragStopsAtContactEvenIfEndpointIsBeyondObstacle()
    {
        var scene = Scene(Poster("A", 0, 0), Poster("B", 400, 0));
        var plan = Move(scene, new(760, 0, 240, 240));
        Assert.True(plan.Succeeded);
        Assert.Equal(new SpatialRect(160, 0, 240, 240), plan.Changes.Single().After);
        Assert.Empty(PosterSpatialSolver.CheckPath(scene, "A", plan.Path));
    }

    [Fact]
    public void ContactSlidesWithoutPushingLockedNeighbor()
    {
        var obstacle = Poster("B", 400, 0) with { IsLocked = true };
        var scene = Scene(Poster("A", 0, 0), obstacle);
        var plan = Move(scene, new(500, 120, 240, 240));
        Assert.Equal(new SpatialRect(160, 120, 240, 240), plan.Changes.Single().After);
        Assert.Single(plan.Changes);
        Assert.Equal(obstacle, scene.Posters[1]);
    }

    [Theory]
    [InlineData(PosterGestureKind.ResizeLeft, 0, 300, 540, 240, 0, 300)]
    [InlineData(PosterGestureKind.ResizeRight, 300, 300, 600, 240, 700, 300)]
    [InlineData(PosterGestureKind.ResizeTop, 300, 0, 240, 540, 300, 0)]
    [InlineData(PosterGestureKind.ResizeBottom, 300, 300, 240, 600, 300, 700)]
    [InlineData(PosterGestureKind.ResizeTopLeft, 0, 0, 540, 540, 0, 0)]
    [InlineData(PosterGestureKind.ResizeTopRight, 300, 0, 600, 540, 700, 0)]
    [InlineData(PosterGestureKind.ResizeBottomLeft, 0, 300, 540, 600, 0, 700)]
    [InlineData(PosterGestureKind.ResizeBottomRight, 300, 300, 600, 600, 700, 700)]
    public void AllResizeDirectionsPreserveOppositeEdgesAndSafeTrace(PosterGestureKind kind,
        int x, int y, int w, int h, int ox, int oy)
    {
        var scene = Scene(Poster("A", 300, 300), Poster("B", ox, oy));
        var result = SpatialCollisionSolver.Resolve(scene, new("A", new(x, y, w, h), kind, PosterOperatingMode.Editing, 240, 240));
        Assert.True(result.Succeeded);
        Assert.Empty(PosterSpatialSolver.CheckPath(scene, "A", result.Path));
        var direct = PosterSpatialSolver.PlanFrameChange(scene, new("A", result.Changes.Single().After, kind,
            PosterOperatingMode.Editing, 240, 240));
        Assert.True(direct.Succeeded);
        Assert.False(result.Changes.Single().After.Overlaps(scene.Posters[1].Bounds));
    }

    [Fact]
    public void ReplacedTimerSamplesDoNotAdvancePresentedGeometry()
    {
        using var fixture = new Fixture();
        var session = fixture.Session();
        var unpresented = session.Preview(new(100, 50, 240, 240));
        Assert.Equal(new SpatialRect(0, 0, 240, 240), session.Presented);
        var newest = session.Preview(new(20, 80, 240, 240));
        Assert.True(session.Accept(newest));
        Assert.Equal(new SpatialRect(20, 80, 240, 240), session.Presented);
        Assert.DoesNotContain(session.Freeze().Path, p => p.To == unpresented.Changes.Single().After);
    }

    [Fact]
    public void RepeatedContactThenSlideRetainsOneSessionAndRecordsUserAdjustment()
    {
        using var fixture = new Fixture();
        var session = fixture.Session();
        for (int i = 0; i < 20; i++) Assert.True(session.Accept(session.Preview(new(760, i * 4, 240, 240))));
        Assert.False(session.IsCancelled);
        Assert.Equal(new SpatialRect(160, 76, 240, 240), session.Presented);
        Assert.True(session.Accept(session.Preview(new(0, 0, 240, 240))));
        Assert.Equal(session.Initial.Posters[0].Bounds, session.Presented);
        Assert.True(session.HasUserAdjusted); // M04-05 consumes this even if the endpoint returns to start.
    }

    [Fact]
    public void EnvironmentAndObstacleChangesInvalidateButSettingsDoNot()
    {
        using var f = new Fixture();
        var session = f.Session();
        var current = f.Coordinator.Refresh();
        Assert.False(session.IsCurrent(current, TestEnvironment with { DpiX = 120 }));
        Assert.False(session.IsCurrent(current, TestEnvironment with { RuntimeSessionId = Guid.NewGuid() }));
        Assert.False(session.IsCurrent(current, TestEnvironment with { ShellGeneration = 2 }));
        var settings = f.Coordinator.Execute(new SettingsStateCommand("settings", StateCommandSource.ControlCenter,
            SettingsStateCommandKind.SetDynamicEffectsPaused, current.Revision,
            current.ControlCenter.Settings.Settings with { DynamicEffectsPaused = true })
            { ReadSet = new(new Dictionary<string, long>(), SettingsVersion: current.ControlCenter.Settings.EntityVersion) });
        Assert.True(settings.Succeeded, settings.Error);
        Assert.True(session.IsCurrent(settings.Projection!, TestEnvironment));
        var b = settings.Projection!.Posters.Single(p => p.Poster.PosterId != "A");
        var locked = f.Coordinator.Execute(new PosterStateCommand("lock-b", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, settings.CurrentRevision, b.Poster.PosterId, b.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) });
        Assert.True(locked.Succeeded, locked.Error);
        Assert.False(session.IsCurrent(locked.Projection!, TestEnvironment));
    }

    [Fact]
    public void GesturePersistsOnceWithCompleteCasAndReplay()
    {
        using var f = new Fixture();
        var session = f.Session();
        Assert.True(session.Accept(session.Preview(new(100, 30, 240, 240))));
        var command = f.Command(session);
        var first = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Committed, first.Status);
        Assert.Equal(session.ObservedRevision + 1, first.CurrentRevision);
        var replay = f.Coordinator.Execute(command);
        Assert.True(replay.Succeeded, replay.Error);
        Assert.Equal(first.CurrentRevision, replay.CurrentRevision);
        Assert.Equal(100, replay.Projection!.PostersById["A"].Poster.FallbackXDip);
    }

    [Fact]
    public void ChangedObstacleCausesNoPartialWrite()
    {
        using var f = new Fixture();
        var s = f.Session();
        Assert.True(s.Accept(s.Preview(new(100, 30, 240, 240))));
        var command = f.Command(s);
        var current = f.Coordinator.Refresh();
        var b = current.Posters.Single(p => p.Poster.PosterId != "A");
        var change = f.Coordinator.Execute(new PosterStateCommand("lock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, current.Revision, b.Poster.PosterId, b.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) });
        var rejected = f.Coordinator.Execute(command);
        Assert.False(rejected.Succeeded);
        Assert.Equal(change.CurrentRevision, rejected.CurrentRevision);
        Assert.Equal(0, rejected.Projection!.PostersById["A"].Poster.FallbackXDip);
    }

    [Fact]
    public void TraceCannotSmuggleUnrelatedFieldsOrAnUnsafeEndpoint()
    {
        using var f = new Fixture();
        var s = f.Session();
        Assert.True(s.Accept(s.Preview(new(100, 30, 240, 240))));
        var command = f.Command(s);
        Assert.False(f.Coordinator.Execute(command with { DesiredState = command.DesiredState with { Title = "changed" } }).Succeeded);
        Assert.False(f.Coordinator.Execute(command with { SpatialGesture = command.SpatialGesture! with { Final = new(450, 0, 240, 240) } }).Succeeded);
        Assert.Equal(s.ObservedRevision, f.Coordinator.Refresh().Revision);
    }

    [Fact]
    public void SeededRandomMovesNeverTunnelOrResizeAndRemainDeterministic()
    {
        var random = new Random(340403);
        for (int i = 0; i < 400; i++)
        {
            var scene = Scene(Poster("A", 0, 0), Poster("B", 380, 250), Poster("C", 650, 650));
            var desired = new SpatialRect(random.Next(761), random.Next(761), 240, 240);
            var a = Move(scene, desired);
            var b = Move(new(scene.Environment, scene.Posters.Reverse()), desired);
            Assert.True(a.Succeeded);
            Assert.Equal(a.Changes, b.Changes);
            Assert.Empty(PosterSpatialSolver.CheckPath(scene, "A", a.Path));
            Assert.All(a.Path, leg => { Assert.Equal(240, leg.To.Width); Assert.Equal(240, leg.To.Height); });
        }
    }

    private static SpatialPlan Move(SpatialSnapshot scene, SpatialRect desired) => SpatialCollisionSolver.Resolve(scene,
        new("A", desired, PosterGestureKind.Move, PosterOperatingMode.Using, 240, 240));

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.SpatialGesture.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            _store = new(new UserDataPaths(_root));
            Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            var initial = Coordinator.EnsureInitialized(Definition("A", 0, 0));
            var created = Coordinator.Execute(new CreatePosterCommand("create-b", StateCommandSource.ControlCenter,
                initial.Revision, new(new Dictionary<string, long>(), CatalogVersion: initial.CatalogVersion), Definition("B", 400, 0)));
            Assert.True(created.Succeeded, created.Error);
        }
        public SpatialGestureSession Session()
        {
            var current = Coordinator.Refresh();
            var scene = new SpatialSnapshot(TestEnvironment, current.Posters.Select(p =>
                Poster(p.Poster.PosterId, (int)p.Poster.FallbackXDip, (int)p.Poster.FallbackYDip)));
            return new("A", scene, current, PosterGestureKind.Move, PosterOperatingMode.Using, 240, 240);
        }
        public PosterStateCommand Command(SpatialGestureSession session)
        {
            var poster = Coordinator.Refresh().PostersById["A"].Poster;
            return new(session.OperationId, StateCommandSource.Poster, PosterStateCommandKind.Move, session.ObservedRevision,
                "A", SpatialGestureCommitPolicy.Capture(poster, session.Presented, TestEnvironment, poster.ExpandedHeightDip))
                { ReadSet = session.Reads, SpatialGesture = session.Freeze() };
        }
        private static PosterDefinition Definition(string id, int x, int y) => new(id, id, "test", "display-1",
            x, y, 240, 240, x / 760d, y / 760d, 0.24, 0.24, false, false, false, "grid", "{}", "box");
        public void Dispose()
        {
            _store.Dispose(); SqliteConnection.ClearAllPools();
            Directory.Delete(_root, true);
        }
    }
}
