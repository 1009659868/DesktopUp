using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class EffectiveExpansionTests
{
    private static readonly SpatialEnvironment Env = TestEnvironment with { WorkArea = new(0, 0, 1200, 1000) };
    private static readonly PosterDefinition Seed = new("A", "A", "test", "display-1", 0, 600, 240, 600,
        0, 600 / 940d, .2, .6, false, true, false, "adaptive-grid", "{}", "night-ops");

    [Fact]
    public void EffectiveHeightNeverBecomesSavedIntentAcrossCommitRefreshAndLock()
    {
        using var f = new Fixture();
        var (scene, command, group) = f.Expansion();
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error);
        var target = result.Projection!.PostersById["A"];
        var cache = new EffectivePosterLayouts();
        cache.Set(target, group.Plan.Changes.Single(c => c.PosterId == "A").After, Env);
        Assert.Equal(450, cache.Find(target)!.HeightDip);
        Assert.True(cache.Find(target)!.IsConstrained);
        Assert.Equal(600, target.Poster.ExpandedHeightDip); Assert.Equal(.6, target.Poster.NormalizedExpandedHeight);
        var refreshed = f.Coordinator.Refresh().PostersById["A"];
        Assert.Equal(450, cache.Find(refreshed)!.HeightDip);
        var locked = f.Coordinator.Execute(new PosterStateCommand("lock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, refreshed.Revision, "A", refreshed.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster("A", refreshed.EntityVersion) });
        Assert.True(locked.Succeeded);
        Assert.Equal(450, cache.Find(locked.Projection!.PostersById["A"])!.HeightDip);
        Assert.Equal(600, new SqliteStateCommandRepository(f.Store).Read().Catalog.Posters.Single(p => p.Poster.PosterId == "A").Poster.ExpandedHeightDip);
        // New process has no effective cache; old proof is not its effective-height authority.
        Assert.Null(new EffectivePosterLayouts().Find(refreshed));
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
    }

    [Fact]
    public void MoveKeepsTemporaryHeightButExplicitResizeReplacesIntent()
    {
        using var f = new Fixture(); var (_, command, group) = f.Expansion();
        var committed = f.Coordinator.Execute(command).Projection!;
        var source = group.NewProof!.ExpandedSource;
        var scene = f.Physical(committed, source.Bounds);
        var session = new SpatialGestureSession("A", scene, committed, PosterGestureKind.Move, PosterOperatingMode.Using, 240, 346);
        Assert.True(session.Accept(session.Preview(source.Bounds with { X = 400 })));
        var target = committed.PostersById["A"];
        var move = f.Coordinator.Execute(new PosterStateCommand(session.OperationId, StateCommandSource.Poster, PosterStateCommandKind.Move,
            committed.Revision, "A", SpatialGestureCommitPolicy.Capture(target.Poster, session.Presented, Env, 600))
            { ReadSet = session.Reads, SpatialGesture = session.Freeze() });
        Assert.True(move.Succeeded, move.Error); Assert.Equal(600, move.Projection!.PostersById["A"].Poster.ExpandedHeightDip);
        var cache = new EffectivePosterLayouts(); cache.Set(move.Projection.PostersById["A"], session.Presented, Env);
        Assert.Equal(450, cache.Find(move.Projection.PostersById["A"])!.HeightDip);
        Assert.Null(cache.Find(move.Projection.PostersById["A"] with { Poster = move.Projection.PostersById["A"].Poster with { ExpandedHeightDip = 500 } }));
    }

    [Fact]
    public void RebuildPlansFromPreferredRectanglesWithoutLoadingPreviousTemporaryHeight()
    {
        var preferred = new SpatialSnapshot(Env, [
            Poster("A", 0, 350, 240, 600),
            Poster("B", 0, 800, 240, 200) with { IsLocked = true },
            Poster("U", 0, 0, 240, 350) with { IsLocked = true }]);
        var result = EffectivePosterLayouts.Rebuild(preferred, PosterContentMetrics.Current, 60);
        // Locked legacy short boxes are retained; they must not be re-expanded against new minimums.
        Assert.NotNull(result);
        Assert.Empty(PosterSpatialSolver.ValidateLayout(result!));
        Assert.Equal(600, result!.Posters.Single(p => p.PosterId == "A").ExpandedHeight);
        Assert.Equal(450, result.Posters.Single(p => p.PosterId == "A").Bounds.Height);
    }

    [Fact]
    public void StaleMetricsAndSnapshotPreferredHeightAreRejectedBeforeWrites()
    {
        using var f = new Fixture(); var (_, command, _) = f.Expansion(); var before = f.Coordinator.Refresh();
        var invalid = command with { SpatialPosture = command.SpatialPosture! with { ContentMetrics = PosterContentMetrics.Current with { Version = "old" } } };
        Assert.False(f.Coordinator.Execute(invalid).Succeeded);
        var scene = command.SpatialPosture!.Initial;
        invalid = command with { SpatialPosture = command.SpatialPosture with { Initial = new(scene.Environment,
            scene.Posters.Select(p => p.PosterId == "A" ? p with { ExpandedHeight = 450 } : p)) } };
        Assert.False(f.Coordinator.Execute(invalid).Succeeded);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RestartPartialSeedDictionaryDoesNotCreateZeroSizedPeers(bool locked)
    {
        var env = Env with { WorkArea = new(0, 0, 2560, 1392) };
        var preferred = new SpatialSnapshot(env, [
            new("A", new(226, 456, 300, 600), 600, true, locked, false, 1),
            new("B", new(226, 992, 300, 400), 400, true, true, false, 0),
            new("U", new(226, 0, 300, 542), 542, true, true, false, 0)]);
        var restored = EffectivePosterLayouts.Rebuild(preferred, PosterContentMetrics.Current, 60,
            new Dictionary<string, SpatialRect> { ["A"] = new(226, 542, 300, 60) });
        Assert.NotNull(restored);
        Assert.Empty(PosterSpatialSolver.ValidateLayout(restored!));
        Assert.Equal(new(226, 542, 300, 450), restored!.Posters.Single(p => p.PosterId == "A").Bounds);
        Assert.Equal(600, restored.Posters.Single(p => p.PosterId == "A").ExpandedHeight);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void RepeatedFeedbackCoalescesResetsAndHasReducedMotionAlternative(bool animate)
    {
        var feedback = new ExpansionFeedback();
        Assert.True(feedback.TryStart(1000, animate));
        Assert.False(feedback.TryStart(1100, animate));
        for (int t = 0; t < 280; t++)
        {
            var sample = feedback.Sample(1000 + t); Assert.True(sample.Active);
            Assert.InRange(sample.OffsetDip, -2, 2);
            if (!animate) Assert.Equal(0, sample.OffsetDip);
        }
        Assert.Equal((false, 0d), feedback.Sample(1280));
        Assert.True(feedback.TryStart(1281, animate));
        feedback.Reset(); Assert.Equal((false, 0d), feedback.Sample(1282));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.EffectiveExpansion.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            Store = new(new UserDataPaths(_root)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store));
            var state = Coordinator.EnsureInitialized(Seed);
            foreach (var box in new[] { ("B", 800, 240), ("U", 0, 350) })
            {
                // B uses a full 240 DIP durable intent but its fixture effective bounds occupy the bottom 200 pixels.
                var created = Coordinator.Execute(new CreatePosterCommand("create-" + box.Item1, StateCommandSource.ControlCenter,
                    state.Revision, StateReadSet.ForCatalog(state.CatalogVersion), Seed with { Title = box.Item1, FallbackYDip = box.Item2,
                        ExpandedHeightDip = box.Item3, NormalizedExpandedHeight = box.Item3 / 1000d, IsLocked = true, IsCollapsed = false }));
                Assert.True(created.Succeeded); state = created.Projection!;
            }
        }
        public SpatialSnapshot Physical(DesktopUpProjection state, SpatialRect? effectiveA = null) => new(Env, state.Posters.Select(p =>
        {
            var rect = p.Poster.Title switch { "A" => effectiveA ?? new(0, 600, 240, 60), "B" => new(0, 800, 240, 200), _ => new SpatialRect(0, 0, 240, 350) };
            return new SpatialPoster(p.Poster.PosterId, rect, (int)p.Poster.ExpandedHeightDip, true, p.Poster.IsLocked,
                p.Poster.IsCollapsed, p.LayoutGeneration, p.LastLayoutOperationId);
        }));
        public (SpatialSnapshot, PosterStateCommand, SpatialGroupPlan) Expansion()
        {
            var state = Coordinator.Refresh(); var scene = Physical(state);
            var result = SpatialExpansionPolicy.Plan(scene, "A", 60, "expand", PosterContentMetrics.Current);
            Assert.Equal(SpatialExpansionOutcome.Constrained, result.Outcome);
            var command = new PosterStateCommand("expand", StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed,
                state.Revision, "A", SpatialGestureCommitPolicy.Capture(state.PostersById["A"].Poster,
                    result.Group.Plan.Changes.Single(c => c.PosterId == "A").After, Env, 600) with { IsCollapsed = false })
            { ReadSet = new(state.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: state.CatalogVersion),
                SpatialPosture = new(scene, false, 60, null) { AllowPush = true, ContentMetrics = PosterContentMetrics.Current } };
            return (scene, command, result.Group);
        }
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
