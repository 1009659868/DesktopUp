using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialPostureIntegrationTests
{
    [Fact]
    public void EveryIntermediatePostureHeightClipsScrollRailAndPreservesInputOrder()
    {
        for (int height = 60; height <= 600; height++)
        {
            var regions = PosterInputRegionClipPolicy.Clip(300, height, new[]
            {
                new PosterInputRegion(PosterInputTargetKind.CollapseButton, -1, new(250, 6, 28, 28)),
                new PosterInputRegion(PosterInputTargetKind.ItemScroll, -1, new(288, 186, 12, Math.Max(1, height - 228))),
                new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, new(0, 0, 300, 46))
            });
            Assert.Equal(PosterInputTargetKind.CollapseButton, regions[0].Kind);
            Assert.All(regions, r => { Assert.True(r.Bounds.IsUsable); Assert.InRange(r.Bounds.Bottom, 1, height); });
            if (height <= 186) Assert.DoesNotContain(regions, r => r.Kind == PosterInputTargetKind.ItemScroll);
        }
    }
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(650, 400, 2)]
    [InlineData(940, 400, 1)]
    public void ExpandFramesAndInverseCollapsePreserveSizeAndCompactAnchor(int y, int finalY, int phases)
    {
        var compact = Poster("A", 20, y, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 };
        var scene = Scene(compact);
        var expanded = SpatialPostureSolver.Plan(scene, "A", false, 60);
        Assert.True(expanded.Succeeded);
        Assert.Equal(phases, expanded.Path.Count);
        var frames = SpatialPostureSolver.Frames(expanded);
        Assert.Empty(PosterSpatialSolver.CheckPath(scene, "A", frames));
        var final = expanded.Changes.Single().After;
        Assert.Equal(new SpatialRect(20, finalY, 240, 600), final);
        Assert.Equal(final, frames.Last().To);
        Assert.All(frames, f => Assert.InRange(f.To.Height - f.From.Height, 1, 32));
        var expandedScene = Scene(compact with { Bounds = final, IsCollapsed = false });
        var collapsed = SpatialPostureSolver.Plan(expandedScene, "A", true, 60, compact.Bounds);
        Assert.True(collapsed.Succeeded);
        Assert.Equal(compact.Bounds, collapsed.Changes.Single().After);
        Assert.Empty(PosterSpatialSolver.CheckPath(expandedScene, "A", SpatialPostureSolver.Frames(collapsed)));
    }

    [Theory]
    [InlineData(96u)]
    [InlineData(120u)]
    [InlineData(144u)]
    [InlineData(192u)]
    public void NegativeOriginAndDpiPreserveLogicalHeight(uint dpi)
    {
        double scale = dpi / 96d;
        var work = new SpatialRect(-2000, -700, (int)(1000 * scale), (int)(1000 * scale));
        var compact = Poster("A", -1900, (int)work.Bottom - (int)(60 * scale), (int)(240 * scale), (int)(60 * scale))
            with { IsCollapsed = true, ExpandedHeight = (int)(600 * scale) };
        var env = TestEnvironment with { WorkArea = work, DpiX = dpi, DpiY = dpi };
        var plan = SpatialPostureSolver.Plan(new(env, [compact]), "A", false, compact.Bounds.Height);
        Assert.True(plan.Succeeded);
        Assert.Equal(600, plan.Changes.Single().After.Height / scale);
        Assert.Equal(work.Bottom, plan.Changes.Single().After.Bottom);
    }

    [Fact]
    public void LockedSourceCanExpandButObstacleAndOversizeFailWithoutAnyChanges()
    {
        var source = Poster("A", 0, 600, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600, IsLocked = true };
        Assert.True(SpatialPostureSolver.Plan(Scene(source), "A", false, 60).Succeeded);
        var blocked = SpatialPostureSolver.Plan(Scene(source, Poster("B", 0, 700)), "A", false, 60);
        Assert.False(blocked.Succeeded); Assert.Empty(blocked.Changes); Assert.Empty(blocked.Path);
        var oversize = SpatialPostureSolver.Plan(Scene(source with { ExpandedHeight = 1100 }), "A", false, 60);
        Assert.Contains(oversize.Issues, i => i.Reason == SpatialFailure.NoSpace);
        Assert.Empty(oversize.Changes);
    }

    [Fact]
    public void StoreCommitsFullHeightWithUpwardTitlePositionAndNoContentMutation()
    {
        using var f = new Fixture();
        var initial = f.Coordinator.Refresh();
        var command = f.Expand();
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error);
        var poster = result.Projection!.PostersById["A"].Poster;
        Assert.Equal(600, poster.ExpandedHeightDip);
        Assert.Equal(400, poster.FallbackYDip);
        Assert.False(poster.IsCollapsed);
        Assert.Equal(initial.PostersById["A"].Poster.LayoutParametersJson, poster.LayoutParametersJson);
        Assert.Equal(initial.PostersById["A"].Poster.TemplateKey, poster.TemplateKey);
        var physical = Scene(Poster("A", 20, 400, 240, 600));
        var collapse = SpatialPostureSolver.Plan(physical, "A", true, 60, new(20, 650, 240, 60));
        var desired = SpatialGestureCommitPolicy.Capture(poster, collapse.Changes.Single().After, TestEnvironment, 600) with { IsCollapsed = true };
        var reverse = f.Coordinator.Execute(new PosterStateCommand("collapse", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetCollapsed, result.CurrentRevision, "A", desired)
        {
            ReadSet = new(result.Projection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: result.Projection.CatalogVersion),
            SpatialPosture = new(physical, true, 60, new(20, 650, 240, 60))
        });
        Assert.True(reverse.Succeeded, reverse.Error);
        Assert.Equal(650, reverse.Projection!.PostersById["A"].Poster.FallbackYDip);
        Assert.Equal(600, reverse.Projection.PostersById["A"].Poster.ExpandedHeightDip);
    }

    [Fact]
    public void CatalogInsertionDuringAnimationRejectsTheOldWholeReadSet()
    {
        using var f = new Fixture();
        var command = f.Expand();
        var current = f.Coordinator.Refresh();
        var added = f.Coordinator.Execute(new CreatePosterCommand("create", StateCommandSource.Tray, current.Revision,
            StateReadSet.ForCatalog(current.CatalogVersion), current.PostersById["A"].Poster with { FallbackXDip = 600 }));
        Assert.True(added.Succeeded, added.Error);
        var rejected = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Conflict, rejected.Status);
        Assert.True(rejected.Projection!.PostersById["A"].Poster.IsCollapsed);
        Assert.Equal(added.CurrentRevision, rejected.CurrentRevision);
    }

    [Fact]
    public void AlteringHeightOrParametersCannotSneakThroughPostureCommand()
    {
        using var f = new Fixture();
        var command = f.Expand();
        Assert.False(f.Coordinator.Execute(command with { DesiredState = command.DesiredState with { ExpandedHeightDip = 400 } }).Succeeded);
        Assert.False(f.Coordinator.Execute(command with { DesiredState = command.DesiredState with { LayoutParametersJson = "{\"size\":3}" } }).Succeeded);
        Assert.True(f.Coordinator.Refresh().PostersById["A"].Poster.IsCollapsed);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.SpatialPosture.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            _store = new(new UserDataPaths(_root)); Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            Coordinator.EnsureInitialized(new("A", "A", "test", "display-1", 20, 650, 240, 600,
                20 / 760d, 650 / 940d, .24, .6, false, true, false, "grid", "{\"itemSize\":82}", "box"));
        }
        public PosterStateCommand Expand()
        {
            var state = Coordinator.Refresh();
            var physical = Scene(Poster("A", 20, 650, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 });
            var plan = SpatialPostureSolver.Plan(physical, "A", false, 60);
            return new("expand", StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, state.Revision, "A",
                SpatialGestureCommitPolicy.Capture(state.PostersById["A"].Poster, plan.Changes.Single().After, TestEnvironment, 600) with { IsCollapsed = false })
            {
                ReadSet = new(state.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: state.CatalogVersion),
                SpatialPosture = new(physical, false, 60, null)
            };
        }
        public void Dispose() { _store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
