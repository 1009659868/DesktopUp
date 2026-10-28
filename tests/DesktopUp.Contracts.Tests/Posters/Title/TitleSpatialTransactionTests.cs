using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleSpatialTransactionTests
{
    [Fact]
    public void NestedReturnAndManualMoveCommitOnceAndPublishOneSharedRevision()
    {
        using var f = new Fixture();
        f.Posture("C", false); f.Posture("B", false);
        var before = f.Coordinator.Refresh();
        var (s, command) = f.Drag(paused: true, dx: 600);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision); // presentation is not persistence
        var events = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, e) => events.Add(e);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
        Assert.Single(events);
        var projection = result.Projection!;
        Assert.Equal(result.CurrentRevision, projection.ControlCenter.Revision);
        Assert.Equal(result.CurrentRevision, projection.Tray.Revision);
        foreach (var p in projection.Posters)
        {
            Assert.Equal(before.PostersById[p.Poster.PosterId].EntityVersion + 1, p.EntityVersion);
            Assert.Equal(before.PostersById[p.Poster.PosterId].LayoutGeneration + 1, p.LayoutGeneration);
            Assert.Equal(result.CurrentRevision, p.Revision);
            var node = projection.SpatialContinuities.Single().Nodes.Single(n => n.Expected.PosterId == p.Poster.PosterId);
            Assert.Equal(p.LayoutGeneration, node.Expected.LayoutGeneration);
            Assert.Equal(command.RequestId, node.Expected.LastLayoutOperationId);
        }
        Assert.Equal(600, f.Find("C").Poster.FallbackXDip); Assert.True(f.Find("C").Poster.IsCollapsed);
        Assert.Equal(60, f.Find("B").Poster.FallbackYDip); Assert.False(f.Find("B").Poster.IsCollapsed);
        Assert.Equal(420, f.Find("A").Poster.FallbackYDip);
        Assert.Equal(before.Posters.Select(p => p.Poster.ExpandedHeightDip), projection.Posters.Select(p => p.Poster.ExpandedHeightDip));
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status); Assert.Single(events);
        f.Posture("B", true); Assert.Equal(120, f.Find("A").Poster.FallbackYDip);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var disk = new SqliteStateCommandRepository(reopened).Read();
        Assert.Equal(f.Coordinator.Refresh().Revision, disk.Revision);
        Assert.Single(disk.SpatialContinuities);
    }

    [Theory] [InlineData("neighbor")] [InlineData("source")] [InlineData("catalog")]
    [InlineData("off-workspace")] [InlineData("hidden")] [InlineData("delete")]
    public void EveryReadEntityAndMembershipIsRecheckedInsideTheTransaction(string mutation)
    {
        using var f = new Fixture();
        if (mutation == "off-workspace") f.Create("other-monitor", 700, false, "other");
        if (mutation == "hidden") f.Create("hidden", 700, true);
        var (_, command) = f.Drag();
        if (mutation == "catalog") f.Create("new", 700);
        else if (mutation == "delete")
        {
            var b = f.Find("B"); var state = f.Coordinator.Refresh();
            Assert.True(f.Coordinator.Execute(new RemovePosterCommand("delete", StateCommandSource.ControlCenter,
                state.Revision, new(new Dictionary<string,long>{{b.Poster.PosterId,b.EntityVersion}}, CatalogVersion: state.CatalogVersion),
                b.Poster.PosterId, true)).Succeeded);
        }
        else
        {
            var target = f.Find(mutation switch { "source" => "C", "off-workspace" => "other-monitor", "hidden" => "hidden", _ => "B" });
            Assert.True(f.Coordinator.Execute(new PosterStateCommand("change-read", StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetLocked, f.Coordinator.Refresh().Revision, target.Poster.PosterId,
                target.Poster with { IsLocked = true }) { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) }).Succeeded);
        }
        var before = JsonSerializer.Serialize(f.Repository.Read());
        var result = f.Coordinator.Execute(command);
        Assert.Equal(mutation == "delete" ? StateCommandStatus.NotFound : StateCommandStatus.Conflict, result.Status);
        Assert.Equal(before, JsonSerializer.Serialize(f.Repository.Read()));
    }

    [Fact]
    public void UnrelatedSettingsRevisionDoesNotCauseFalseConflict()
    {
        using var f = new Fixture(); var (_, command) = f.Drag();
        var p = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new SettingsStateCommand("settings", StateCommandSource.ControlCenter,
            SettingsStateCommandKind.SetLaunchAtStartupRequested, p.Revision,
            p.ControlCenter.Settings.Settings with { LaunchAtStartupRequested = true })
            { ReadSet = StateReadSet.ForSettings(p.ControlCenter.Settings.EntityVersion) }).Succeeded);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(p.Revision + 2, result.CurrentRevision);
        Assert.True(result.Projection!.ControlCenter.Settings.Settings.LaunchAtStartupRequested);
    }

    [Theory]
    [InlineData("reads")] [InlineData("occupancy")] [InlineData("source-frame")] [InlineData("neighbor-frame")]
    [InlineData("generation")] [InlineData("preferred")] [InlineData("phase")] [InlineData("prefix")]
    [InlineData("request")] [InlineData("system")] [InlineData("incomplete")] [InlineData("foreign-input")]
    [InlineData("presented-endpoint")] [InlineData("presented-home")] [InlineData("budget")]
    public void ForgedOrIncompleteProofDoesNotAuthorizeAnyWrites(string attack)
    {
        using var f = new Fixture(); var (_, command) = f.Drag();
        var trace = command.Trace; var start = trace.Start;
        var initial = start.Initial; string source = start.Press.Capture.Press.Session.PosterId;
        switch (attack)
        {
            case "reads":
                command = command with { ReadSet = StateReadSet.ForPoster(source, command.ReadSet.Posters[source]) with { CatalogVersion = command.ReadSet.CatalogVersion } }; break;
            case "occupancy":
                start = start with { Initial = new(initial.Environment, initial.Posters.Where(p => p.PosterId == source)) }; break;
            case "source-frame": case "neighbor-frame":
                start = start with { Initial = new(initial.Environment, initial.Posters.Select(p =>
                    (attack == "source-frame" ? p.PosterId == source : p.PosterId != source)
                        ? p with { Bounds = p.Bounds with { X = 400 } } : p)) }; break;
            case "generation":
                start = start with { Initial = new(initial.Environment, initial.Posters.Select(p => p with { LayoutGeneration = p.LayoutGeneration + 1 })) }; break;
            case "preferred":
                start = start with { Initial = new(initial.Environment, initial.Posters.Select(p => p with { ExpandedHeight = 600 })) }; break;
            case "phase": start = start with { Press = start.Press with { Posture = TitlePresentedPosture.Expanded } }; break;
            case "prefix": start = start with { Paused = start.Paused! with { AcceptedFrames = int.MaxValue } }; break;
            case "request": command = command with { RequestId = "another-owner" }; break;
            case "system": command = command with { Source = StateCommandSource.System }; break;
            case "incomplete": trace = trace with { Samples = trace.Samples.Take(1).ToArray() }; break;
            case "presented-endpoint":
                trace = trace with { Presented = new(trace.Presented.Environment, trace.Presented.Posters.Select(p =>
                    p.PosterId == source ? p with { Bounds = p.Bounds with { X = 500 } } : p)) }; break;
            case "presented-home":
                trace = trace with { Continuity = trace.Continuity with { Nodes = trace.Continuity.Nodes.Select(n =>
                    n.Expected.PosterId != source ? n with { HomeY = n.HomeY + 100 } : n).ToArray() } }; break;
            case "budget":
                trace = trace with { Samples = Enumerable.Repeat(trace.Samples[0], 1000).ToArray() }; break;
            case "foreign-input":
                trace = trace with { Samples = trace.Samples.Select(s => s.Pointer is { } p
                    ? s with { Pointer = p with { Token = p.Token with { EnvironmentEpoch = 99 } } } : s).ToArray() }; break;
        }
        command = command with { Trace = trace with { Start = start } };
        var before = JsonSerializer.Serialize(f.Repository.Read());
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before, JsonSerializer.Serialize(f.Repository.Read()));
    }

    [Fact]
    public void SqlFailureRollsBackSourcePeersContinuityHeadAndReceipt()
    {
        using var f = new Fixture(); f.Posture("C", false); f.Posture("B", false);
        var (_, command) = f.Drag(paused: true, dx: 600);
        string before = JsonSerializer.Serialize(f.Repository.Read());
        f.Sql("CREATE TRIGGER fail_title BEFORE INSERT ON spatial_continuity BEGIN SELECT RAISE(ABORT, 'title-injected'); END;");
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before, JsonSerializer.Serialize(f.Repository.Read()));
        f.Sql("DROP TRIGGER fail_title;");
        // No success receipt survived rollback; the exact same logical request can commit once.
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(command).Status);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
    }

    [Fact]
    public void RoundTripMovementTouchesSourceGenerationButDoesNotResetPeerHomes()
    {
        using var f = new Fixture(); f.Posture("C", false);
        var before = f.Coordinator.Refresh();
        var scene = f.Scene(); var c = f.Find("C"); var continuity = f.Repository.Read().SpatialContinuities.Single();
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene, c.Poster.PosterId,
            continuity: continuity, metrics: c.Presentation.Metrics), c.Presentation.Metrics, continuity);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 100));
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 0));
        TitleSpatialTestData.Release(s);
        var result = f.Coordinator.Execute(new TitleSpatialCommand(s.Start.Press.OperationId, StateCommandSource.Poster,
            before.Revision, SpatialCommandContract.CaptureReadSet(f.Repository.Read()), s.Freeze()));
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
        Assert.Equal(c.LayoutGeneration + 1, f.Find("C").LayoutGeneration);
        Assert.Equal(60, result.Projection!.SpatialContinuities.Single().Nodes.Single(n => n.Expected.PosterId == f.Find("B").Poster.PosterId).HomeY);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptedBentPrefixUsesSingleRevisionAndCompleteCas(bool conflict)
    {
        using var f = new Fixture();
        var before = f.Coordinator.Refresh(); var c = f.Find("C"); var saved = f.Repository.Read();
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(f.Scene(), c.Poster.PosterId), c.Presentation.Metrics);
        var d = new TitlePresentationDriver(s);
        d.Queue(TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 600, 600).Pointer!);
        d.Tick(() => true, _ => true); Assert.True(d.HasPendingPresentation);
        var stop = d.Physical[c.Poster.PosterId];
        d.Queue(TitleSpatialTestData.Input(s, TitlePointerEventKind.Interrupt).Pointer! with { Sequence = 3 });
        for (int i = 0; i < 100 && !s.Complete && !s.Cancelled; i++) d.Tick(() => true, _ => true);
        Assert.True(s.Complete); Assert.Equal(stop, d.Physical[c.Poster.PosterId]);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        if (conflict)
        {
            var b = f.Find("B");
            Assert.True(f.Coordinator.Execute(new PosterStateCommand("lock-during-stop", StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetLocked, before.Revision, b.Poster.PosterId, b.Poster with { IsLocked = true })
                { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) }).Succeeded);
        }
        var command = new TitleSpatialCommand(s.Start.Press.OperationId, StateCommandSource.Poster, before.Revision,
            SpatialCommandContract.CaptureReadSet(saved), s.Freeze());
        var snapshot = JsonSerializer.Serialize(f.Repository.Read()); int publications = 0;
        f.Coordinator.ProjectionChanged += (_, _) => publications++;
        var result = f.Coordinator.Execute(command);
        if (conflict)
        {
            Assert.Equal(StateCommandStatus.Conflict, result.Status); Assert.Equal(0, publications);
            Assert.Equal(snapshot, JsonSerializer.Serialize(f.Repository.Read()));
        }
        else
        {
            Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
            Assert.Equal(1, publications); Assert.Equal(stop.X, f.Find("C").Poster.FallbackXDip);
            Assert.Equal(stop.Y, f.Find("C").Poster.FallbackYDip);
            Assert.Equal(before.PostersById[f.Find("B").Poster.PosterId].EntityVersion, f.Find("B").EntityVersion);
            Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status); Assert.Equal(1, publications);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.TitleSpatial.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public UserDataPaths Paths { get; }
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            Paths = new(_root); Paths.EnsureCreated(); _store = new(Paths); Assert.True(_store.Initialize().Succeeded);
            Repository = new(_store); Coordinator = new(Repository);
            Coordinator.EnsureInitialized(new("C", "C", "", "test", 0, 0, 240, 360, 0, 0, .2, .36,
                false, true, true, "grid", "{}", "basic-box"));
            Create("B", 60); Create("A", 120);
        }
        public PosterSurfaceProjection Find(string title) => Coordinator.Refresh().Posters.Single(p => p.Poster.Title == title);
        public void Create(string title, int y, bool hidden = false, string monitor = "test")
        {
            var current = Coordinator.Refresh(); var p = Find("C").Poster;
            var desired = SpatialGestureCommitPolicy.Capture(p, new(0, y, 240, 60), TitleSpatialTestData.Env, 360)
                with { Title = title, IsVisible = !hidden, MonitorDeviceName = monitor, IsCollapsed = true };
            Assert.True(Coordinator.Execute(new CreatePosterCommand("create-" + title, StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), desired)).Succeeded);
        }
        public SpatialSnapshot Scene()
        {
            var state = Repository.Read(); var env = TitleSpatialTestData.Env;
            return new(env, state.Catalog.Posters.Where(p => p.Poster.IsVisible && p.Poster.MonitorDeviceName == env.WorkspaceId).Select(e =>
            {
                var p = e.Poster;
                var saved = state.SpatialContinuities.FirstOrDefault(c => c.Environment == env)?.Nodes.FirstOrDefault(n =>
                    n.Expected.PosterId == p.PosterId && n.Expected.LayoutGeneration == e.LayoutGeneration)?.Expected;
                return saved ?? new SpatialPoster(p.PosterId, new((int)p.FallbackXDip, (int)p.FallbackYDip, (int)p.WidthDip,
                    p.IsCollapsed ? 60 : (int)p.ExpandedHeightDip), (int)p.ExpandedHeightDip, p.IsVisible,
                    p.IsLocked, p.IsCollapsed, e.LayoutGeneration, e.LastLayoutOperationId);
            }));
        }
        public (TitleSpatialSession, TitleSpatialCommand) Drag(bool paused = false, int dx = 40)
        {
            var before = Coordinator.Refresh(); var c = Find("C"); var state = Repository.Read();
            var continuity = state.SpatialContinuities.FirstOrDefault(p => p.Environment == TitleSpatialTestData.Env);
            var start = TitleSpatialTestData.Start(Scene(), c.Poster.PosterId, paused, .5, continuity, c.Presentation.Metrics);
            var s = new TitleSpatialSession(start, c.Presentation.Metrics, continuity);
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, dx));
            TitleSpatialTestData.Release(s, dx);
            return (s, new(start.Press.OperationId, StateCommandSource.Poster, before.Revision,
                SpatialCommandContract.CaptureReadSet(state), s.Freeze()));
        }
        public void Posture(string title, bool collapsed)
        {
            var p = Find(title); var state = Repository.Read(); var scene = Scene(); string op = Guid.NewGuid().ToString("N");
            var plan = SpatialContinuitySolver.Plan(scene, p.Poster.PosterId, collapsed, 60, op, p.Presentation.Metrics,
                state.SpatialContinuities.FirstOrDefault(c => c.Environment == scene.Environment));
            Assert.True(plan.Group.Plan.Succeeded);
            var rect = plan.Group.Plan.Changes.Single(c => c.PosterId == p.Poster.PosterId).After;
            var command = new PosterStateCommand(op, StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed,
                state.Revision, p.Poster.PosterId, SpatialGestureCommitPolicy.Capture(p.Poster, rect, scene.Environment, p.Poster.ExpandedHeightDip)
                    with { IsCollapsed = collapsed }) {
                ReadSet = SpatialCommandContract.CaptureReadSet(state),
                SpatialPosture = new(scene, collapsed, 60, null) { AllowPush = true, UseContinuity = true, ContentMetrics = p.Presentation.Metrics } };
            var result = Coordinator.Execute(command); Assert.True(result.Succeeded, result.Error);
        }
        public void Sql(string sql)
        {
            using var c = new SqliteConnection($"Data Source={Path.Combine(Paths.State, "desktopup.sqlite3")};Pooling=False");
            c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery();
        }
        public void Dispose() { _store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
