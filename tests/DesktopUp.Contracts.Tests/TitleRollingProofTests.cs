using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleRollingProofTests
{
    [Theory]
    [InlineData(1, "expanding")] [InlineData(3, "expanding")] [InlineData(6, "expanding")]
    [InlineData(1, "collapsing")] [InlineData(3, "collapsing")] [InlineData(6, "collapsing")]
    [InlineData(1, "expanded")] [InlineData(3, "expanded")] [InlineData(6, "expanded")]
    public void ContinuousDragUsesBoundedProofMemoryAndOneFinalRevision(int count, string stage)
    {
        using var f = new Fixture(count, stage);
        int peak = 0, events = 0;
        f.Coordinator.ProjectionChanged += (_, _) => events++;
        long initialRevision = f.Coordinator.Refresh().Revision;
        for (int i = 0; i < 2048; i++)
        {
            f.Move(500 + i % 2 * 80);
            peak = Math.Max(peak, f.Session.SampleCount);
            if (f.Session.NeedsCheckpoint) Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
            Assert.False(f.Session.Cancelled);
        }
        Assert.InRange(peak, 1, 128); Assert.True(f.Session.SealedSegments >= 16);
        Assert.Equal(initialRevision, f.Coordinator.Refresh().Revision); Assert.Equal(0, events);
        var command = f.Release(580);
        Assert.NotNull(command.Trace.Checkpoint);
        Assert.True(command.Trace.Samples.Count <= 129);
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command).Status);
        var result = f.Coordinator.ExecuteTitle(command, f.Lease);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(initialRevision + 1, result.CurrentRevision); Assert.Equal(1, events);
        Assert.Equal(1, result.Projection!.PostersById["C"].LayoutGeneration);
        Assert.Equal(stage != "expanded", result.Projection.PostersById["C"].Poster.IsCollapsed);
        Assert.Equal(360, result.Projection.PostersById["C"].Poster.ExpandedHeightDip);
        Assert.Equal(580, result.Projection.PostersById["C"].Poster.FallbackXDip);
        Assert.Equal(result.CurrentRevision, result.Projection.Tray.Revision);
        Assert.Equal(result.CurrentRevision, result.Projection.ControlCenter.Revision);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.ExecuteTitle(command, f.Lease).Status);
    }

    [Fact]
    public void CheckpointDoesNotBecomeAReplacementPressOrLoseNeighborHomes()
    {
        using var f = new Fixture(3, "expanding");
        var press = f.Session.Intent.Press; var capture = f.Session.Intent.Token;
        for (int i = 0; i < 300; i++)
        {
            f.Move(500 + i % 2 * 80);
            if (f.Session.NeedsCheckpoint) Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        }
        Assert.Equal(press, f.Session.Intent.Press); Assert.Equal(capture, f.Session.Intent.Token);
        var command = f.Release(580); var result = f.Coordinator.ExecuteTitle(command, f.Lease);
        Assert.True(result.Succeeded, result.Error);
        foreach (var peer in f.Initial.Posters.Where(p => p.PosterId != "C"))
        {
            var actual = f.Session.Presented.Posters.Single(p => p.PosterId == peer.PosterId);
            Assert.Equal(peer.Bounds.Y, actual.Bounds.Y);
        }
        Assert.NotNull(f.Session.Intent.Rearm);
    }

    [Theory] [InlineData("closed")] [InlineData("stale-runtime")] [InlineData("entity")]
    [InlineData("foreign-session")] [InlineData("foreign-issuer")]
    public void InvalidCheckpointCannotEraseUnverifiedTailOrWrite(string invalid)
    {
        using var f = new Fixture(3, "expanding");
        f.Move(500); int count = f.Session.SampleCount;
        var session = f.Session; var coordinator = f.Coordinator;
        if (invalid == "closed") f.Lease.Dispose();
        if (invalid == "stale-runtime") f.Host.Current = false;
        if (invalid == "entity")
        {
            var p = f.Coordinator.Refresh().Posters.Last();
            Assert.True(f.Coordinator.Execute(new PosterStateCommand("change", StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetLocked, p.Revision, p.Poster.PosterId, p.Poster with { IsLocked = true })
                { ReadSet = StateReadSet.ForPoster(p.Poster.PosterId, p.EntityVersion) }).Succeeded);
        }
        if (invalid == "foreign-session")
        {
            var foreign = f.Session.Start with { Press = f.Session.Start.Press with { OperationId = "foreign" } };
            session = new(foreign, f.Metrics);
            TitleSpatialTestData.Accept(session, TitleSpatialTestData.Input(session, TitlePointerEventKind.Move, 500));
        }
        if (invalid == "foreign-issuer") coordinator = new(f.Repository);
        Assert.False(coordinator.CheckpointTitle(session, f.Lease));
        Assert.Equal(count, f.Session.SampleCount); Assert.Equal(0, f.Session.SealedSegments);
    }

    [Fact]
    public void PendingPartialPresentationCannotBeSealed()
    {
        using var f = new Fixture(1, "expanding");
        f.Move(500);
        var proposal = f.Session.Propose(TitleSpatialTestData.Input(f.Session, TitlePointerEventKind.Release, 500));
        Assert.NotNull(proposal);
        Assert.Throws<InvalidOperationException>(() => f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        Assert.Equal(0, f.Session.SealedSegments);
    }

    [Fact]
    public void FinalWitnessCannotBeRemovedOrReplacedAfterCheckpoint()
    {
        using var f = new Fixture(1, "expanded");
        f.Move(500); Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        var command = f.Release(500);
        command = command with { Trace = command.Trace with { Checkpoint = null } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.ExecuteTitle(command, f.Lease).Status);
    }

    [Fact]
    public void RepeatedOutAndBackStillCountsAsOneExplicitAdjustment()
    {
        using var f = new Fixture(1, "expanded");
        for (int i = 0; i < 1024; i++)
        {
            f.Move(i % 2 == 0 ? 50 : 0);
            if (f.Session.NeedsCheckpoint) Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        }
        var result = f.Coordinator.ExecuteTitle(f.Release(0), f.Lease);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, result.Projection!.PostersById["C"].LayoutGeneration);
        Assert.Equal(0, result.Projection.PostersById["C"].Poster.FallbackXDip);
    }

    [Theory]
    [InlineData("hide-source")] [InlineData("remove-neighbor")] [InlineData("policy-source")]
    [InlineData("create")] [InlineData("runtime")] [InlineData("disposed")]
    public void CheckpointDoesNotSurviveNewEntityOrRuntimeLifetime(string invalid)
    {
        using var f = new Fixture(3, "expanded");
        for (int i = 0; i < 260; i++)
        {
            f.Move(500 + i % 2 * 80);
            if (f.Session.NeedsCheckpoint) Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        }
        var command = f.Release(580);
        Assert.NotNull(command.Trace.Checkpoint);
        var before = f.Coordinator.Refresh(); var source = before.PostersById["C"];
        if (invalid == "hide-source")
            Assert.True(f.Coordinator.Execute(new PosterStateCommand("hide", StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetVisible, before.Revision, "C", source.Poster with { IsVisible = false })
                { ReadSet = StateReadSet.ForPoster("C", source.EntityVersion) }).Succeeded);
        if (invalid == "policy-source")
            Assert.True(f.Coordinator.Execute((PosterAutoPostureDraft.Open(source) with { Enabled = false })
                .ToCommand("disable")).Succeeded);
        if (invalid == "remove-neighbor")
        {
            var preview = f.Coordinator.PrepareRemoval(before.Posters.Last().Poster.PosterId)!;
            Assert.True(f.Coordinator.Execute(new RemovePosterCommand("remove", StateCommandSource.ControlCenter,
                preview.Revision, preview.ReadSet, preview.PosterId, true)).Succeeded);
        }
        if (invalid == "create")
            Assert.True(f.Coordinator.Execute(new CreatePosterCommand("create-more", StateCommandSource.ControlCenter,
                before.Revision, StateReadSet.ForCatalog(before.CatalogVersion),
                source.Poster with { Title = "new entity", IsVisible = false })).Succeeded);
        if (invalid == "runtime") f.Host.Current = false;
        if (invalid == "disposed") f.Lease.Dispose();
        var latest = f.Coordinator.Refresh();
        int publishes = 0; f.Coordinator.ProjectionChanged += (_, _) => publishes++;
        Assert.False(f.Coordinator.ExecuteTitle(command, f.Lease).Succeeded);
        var after = f.Coordinator.Refresh();
        Assert.Equal(latest.Revision, after.Revision); Assert.Equal(0, publishes);
        Assert.Equal(latest.Posters.Select(p => p.Poster), after.Posters.Select(p => p.Poster));
        Assert.False(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
    }

    [Fact]
    public void CheckpointFromAnotherLiveLeaseCannotReplaceFinalProof()
    {
        using var f = new Fixture(1, "expanded");
        using var other = new Fixture(1, "expanded");
        f.Move(500); other.Move(500);
        Assert.True(f.Coordinator.CheckpointTitle(f.Session, f.Lease));
        Assert.True(other.Coordinator.CheckpointTitle(other.Session, other.Lease));
        var command = f.Release(500); var foreign = other.Release(500);
        Assert.NotEqual(command.Trace.Checkpoint!.Id, foreign.Trace.Checkpoint!.Id);
        long revision = f.Coordinator.Refresh().Revision;
        Assert.False(f.Coordinator.ExecuteTitle(command with { Trace = command.Trace with
            { Checkpoint = foreign.Trace.Checkpoint } }, f.Lease).Succeeded);
        Assert.Equal(revision, f.Coordinator.Refresh().Revision);
    }

    internal sealed class Host(TitleSpatialStart start) : ITitleRuntimeReadback
    {
        public bool Current = true;
        public bool ValidateTitleStart(TitleSpatialStart candidate, StateReadSet reads) => Current && candidate == start;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.TitleRolling.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        private long _sequence = 1;
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public TitleSpatialSession Session { get; }
        public TitlePresentationDriver Driver { get; }
        public TitleRuntimeLease Lease { get; }
        public Host Host { get; }
        public SpatialSnapshot Initial { get; }
        public PosterContentMetrics Metrics { get; }
        public StateReadSet Reads { get; }
        private readonly long _revision;
        public Fixture(int count, string stage)
        {
            var paths = new UserDataPaths(_root); paths.EnsureCreated(); _store = new(paths);
            Assert.True(_store.Initialize().Succeeded); Repository = new(_store); Coordinator = new(Repository);
            bool compact = stage == "expanding";
            var seed = new PosterDefinition("C", "C", "", "test", 0, 0, 240, 360, 0, 0, .2, .36,
                false, compact, true, "grid", "{}", "basic-box");
            var projection = Coordinator.EnsureInitialized(seed);
            for (int i = 1; i < count; i++)
            {
                int y = (compact ? 60 : 360) + (i - 1) * 60;
                var p = seed with { Title = "peer-" + i, IsCollapsed = true, FallbackYDip = y, VerticalAnchor = y / 940d };
                var result = Coordinator.Execute(new CreatePosterCommand("create-" + i, StateCommandSource.ControlCenter,
                    projection.Revision, StateReadSet.ForCatalog(projection.CatalogVersion), p));
                Assert.True(result.Succeeded); projection = result.Projection!;
            }
            Initial = new(TitleSpatialTestData.Env, projection.Posters.Select(e =>
                new SpatialPoster(e.Poster.PosterId, new(0, (int)e.Poster.FallbackYDip, 240, e.Poster.IsCollapsed ? 60 : 360),
                    360, true, false, e.Poster.IsCollapsed, e.LayoutGeneration, e.LastLayoutOperationId)));
            Metrics = projection.PostersById["C"].Presentation.Metrics;
            var start = TitleSpatialTestData.Start(Initial, paused: stage == "expanding" ? false : stage == "collapsing" ? true : null, metrics: Metrics);
            Session = new(start, Metrics); Driver = new(Session); Host = new(start); Coordinator.BindTitleRuntime(Host);
            Reads = new(projection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: projection.CatalogVersion);
            Lease = Assert.IsType<TitleRuntimeLease>(Coordinator.OpenTitle(start, Reads)); _revision = projection.Revision;
        }
        public void Move(int dx)
        {
            Driver.Queue(TitleSpatialTestData.Input(Session, TitlePointerEventKind.Move, dx).Pointer! with { Sequence = ++_sequence });
            Driver.Tick(() => true, _ => true);
            while (Driver.HasPendingPresentation) Driver.Tick(() => true, _ => true);
        }
        public TitleSpatialCommand Release(int dx)
        {
            Driver.Queue(TitleSpatialTestData.Input(Session, TitlePointerEventKind.Release, dx).Pointer! with { Sequence = ++_sequence });
            int ticks = 0;
            while (!Session.Complete && !Session.Cancelled && ticks++ < 1000) Driver.Tick(() => true, _ => true);
            Assert.True(Session.Complete);
            return new(Session.Start.Press.OperationId, StateCommandSource.Poster, _revision, Reads, Session.Freeze());
        }
        public void Dispose() { Lease.Dispose(); _store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
