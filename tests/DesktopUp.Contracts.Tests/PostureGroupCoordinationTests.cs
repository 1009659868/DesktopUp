using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
namespace DesktopUp.Contracts.Tests;

public sealed class PostureGroupCoordinationTests
{
    [Fact]
    public void QueuedIntentCanFollowPostureGeometryButNotUnrelatedUserEdits()
    {
        var p = new PosterDefinition("A", "A", "test", "test", 0, 0, 240, 360, 0, 0, .24, .225,
            false, true, false, "adaptive-grid", "{}", "basic-box");
        Assert.True(PosturePendingPolicy.CanAdopt(p, p with { IsCollapsed = false, FallbackYDip = 60, VerticalAnchor = .1, NormalizedWidth = .2 }));
        Assert.False(PosturePendingPolicy.CanAdopt(p, p with { IsLocked = true }));
        Assert.False(PosturePendingPolicy.CanAdopt(p, p with { WidthDip = 300 }));
        Assert.False(PosturePendingPolicy.CanAdopt(p, p with { LayoutParametersJson = "changed" }));
        Assert.Equal(64, PosturePendingPolicy.Capacity); Assert.Equal(5000, PosturePendingPolicy.LifetimeMilliseconds);
    }
    [Fact]
    public void ReservationsCoverWholeSweepsAndParticipantIdentityNotJustEndpoints()
    {
        var env = SpatialContinuityTests.Environment;
        PostureGroupReservation Sweep(string id, int x, int end, string workspace = "test") =>
            PostureGroupReservation.Create(new(env with { WorkspaceId = workspace }, []),
                [new([new(id, new(x, 0, 100, 60), new(end, 0, 100, 60))])]);
        Assert.True(Sweep("a", 0, 600).Conflicts(Sweep("b", 300, 300)));
        Assert.False(Sweep("a", 0, 100).Conflicts(Sweep("b", 200, 500)));
        Assert.False(Sweep("a", 0, 600).Conflicts(Sweep("b", 0, 600, "other")));
        Assert.True(Sweep("a", 0, 100).Conflicts(Sweep("a", 900, 950)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void IndependentGroupsKeepFullCasAndEachCommitOneRevisionInEitherOrder(bool reverse)
    {
        using var f = new Fixture();
        var initial = f.Scene(); var state = f.Coordinator.Refresh();
        var ids = state.Posters.Select(p => p.Poster.PosterId).ToArray(); if (reverse) Array.Reverse(ids);
        var first = f.Command(ids[0], initial); var second = f.Command(ids[1], initial);
        var expected = SpatialContinuitySolver.Plan(initial, ids[1], false, 60, second.RequestId,
            state.PostersById[ids[1]].Presentation.Metrics, null).Group.Plan.Changes;
        var result = f.Coordinator.Execute(first); Assert.True(result.Succeeded, result.Error);
        Assert.Equal(state.Revision + 1, result.CurrentRevision);
        // Old complete CAS still rejects. Rebase uses a NEW request id after this diagnostic rejection.
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(second).Status);
        second = second with { RequestId = "rebase-" + second.RequestId };
        var rebased = PostureGroupRebase.TryRebase(second, f.Scene(), result.Projection!, expected);
        Assert.NotNull(rebased); Assert.Equal(2, rebased.ReadSet!.Posters.Count);
        var finished = f.Coordinator.Execute(rebased); Assert.True(finished.Succeeded, finished.Error);
        Assert.Equal(state.Revision + 2, finished.CurrentRevision);
        Assert.All(finished.Projection!.Posters, p => Assert.False(p.Poster.IsCollapsed));
        Assert.Empty(PosterSpatialSolver.ValidateLayout(f.Scene()));
    }

    [Fact]
    public void RebaseRejectsDifferentAnimatedEndpoints()
    {
        using var f = new Fixture(); var initial = f.Scene(); var command = f.Command("A", initial);
        Assert.Null(PostureGroupRebase.TryRebase(command, initial, f.Coordinator.Refresh(), []));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.RR.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Coordinator { get; }
        private readonly SpatialEnvironment _environment = SpatialContinuityTests.Environment;
        public Fixture()
        {
            _store = new(new UserDataPaths(_root)); Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            var seed = new PosterDefinition("A", "A", "test", "test", 0, 0, 240, 360, 0, 0, .24, .225,
                false, true, false, "adaptive-grid", "{}", "basic-box");
            var current = Coordinator.EnsureInitialized(seed);
            var create = Coordinator.Execute(new CreatePosterCommand("create-b", StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), seed with { FallbackXDip = 400, HorizontalAnchor = 400d / 760 }));
            Assert.True(create.Succeeded);
        }
        public SpatialSnapshot Scene() => new(_environment, Coordinator.Refresh().Posters.Select(p => new SpatialPoster(p.Poster.PosterId,
            new((int)p.Poster.FallbackXDip, (int)p.Poster.FallbackYDip, 240, p.Poster.IsCollapsed ? 60 : 360), 360,
            true, p.Poster.IsLocked, p.Poster.IsCollapsed, p.LayoutGeneration, p.LastLayoutOperationId)));
        public PosterStateCommand Command(string id, SpatialSnapshot initial)
        {
            var p = Coordinator.Refresh(); var target = p.PostersById[id]; string request = "expand-" + id;
            var plan = SpatialContinuitySolver.Plan(initial, id, false, 60, request, target.Presentation.Metrics, p.SpatialContinuities.FirstOrDefault());
            return new(request, StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, p.Revision, id,
                SpatialGestureCommitPolicy.Capture(target.Poster, plan.Group.Plan.Changes.Single().After, _environment, 360) with { IsCollapsed = false })
            { ReadSet = new(p.Posters.ToDictionary(t => t.Poster.PosterId, t => t.EntityVersion), CatalogVersion: p.CatalogVersion),
                SpatialPosture = new(initial, false, 60, null) { AllowPush = true, UseContinuity = true, ContentMetrics = target.Presentation.Metrics } };
        }
        public void Dispose() { _store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
