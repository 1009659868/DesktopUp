using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;
using static DesktopUp.Contracts.Tests.SpatialPushIntegrationTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialExpansionPolicyTests
{
    private static readonly PosterContentMetrics Metrics = new("fixture", 150, 100, 50);
    private static SpatialSnapshot Scene(int ceiling) => new(TestEnvironment with { WorkArea = new(0, 0, 1000, 1000) },
        new[] { Poster("A", 0, 600, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 },
            Poster("B", 0, 800, 240, 200) with { IsLocked = true } }
        .Concat(ceiling == 0 ? [] : new[] { Poster("U", 0, 0, 240, ceiling) with { IsLocked = true } }));

    [Theory]
    [InlineData(0, SpatialExpansionOutcome.Full, 200, 600)]
    [InlineData(200, SpatialExpansionOutcome.Full, 200, 600)]
    [InlineData(350, SpatialExpansionOutcome.Constrained, 350, 450)]
    [InlineData(499, SpatialExpansionOutcome.Constrained, 499, 301)]
    [InlineData(500, SpatialExpansionOutcome.Constrained, 500, 300)]
    [InlineData(501, SpatialExpansionOutcome.InsufficientSpace, 0, 299)]
    [InlineData(550, SpatialExpansionOutcome.InsufficientSpace, 0, 250)]
    public void FrozenCasesAndExactPixelThreshold(int ceiling, SpatialExpansionOutcome outcome, int top, int height)
    {
        var scene = Scene(ceiling);
        var result = SpatialExpansionPolicy.Plan(scene, "A", 60, "expand", Metrics);
        Assert.Equal(outcome, result.Outcome); Assert.Equal(height, result.EffectiveHeight);
        Assert.Equal(600, scene.Posters.Single(p => p.PosterId == "A").ExpandedHeight);
        if (outcome == SpatialExpansionOutcome.InsufficientSpace)
        { Assert.Empty(result.Group.Frames); Assert.Empty(result.Group.Plan.Changes); Assert.Null(result.Group.NewProof); return; }
        Assert.Equal(new(0, top, 240, height), result.Group.Plan.Changes.Single(p => p.PosterId == "A").After);
        AssertFrames(scene, result.Group);
        var source = result.Group.NewProof!.ExpandedSource;
        Assert.Equal(600, source.ExpandedHeight);
        var expanded = new SpatialSnapshot(scene.Environment, scene.Posters.Select(p => p.PosterId == "A" ? source : p));
        Assert.Empty(PosterSpatialSolver.ValidateLayout(expanded));
        var collapse = SpatialPushSolver.Plan(expanded, "A", true, 60, "collapse", result.Group.NewProof);
        Assert.True(collapse.Plan.Succeeded); Assert.Equal(scene.Posters.Single(p => p.PosterId == "A").Bounds,
            collapse.Plan.Changes.Single(p => p.PosterId == "A").After);
        AssertFrames(expanded, collapse);
    }

    [Fact]
    public void FullCandidatesTakePriorityOverAFeasibleShortDownwardExpansion()
    {
        var scene = Scene(200);
        var a = SpatialExpansionPolicy.Plan(scene, "A", 60, "op", Metrics);
        var b = SpatialExpansionPolicy.Plan(new(scene.Environment, scene.Posters.Reverse()), "A", 60, "op", Metrics);
        Assert.Equal(SpatialExpansionOutcome.Full, a.Outcome);
        Assert.Equal(a.Group.Plan.Changes, b.Group.Plan.Changes);
    }

    [Theory]
    [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(192)]
    public void MetricsReserveOneCompleteTemplateRowRegardlessOfItemCount(uint dpi)
    {
        var m = PosterContentMetrics.Current;
        Assert.Equal((int)(Math.Ceiling(186 * dpi / 96d) + Math.Ceiling(118 * dpi / 96d) + Math.Ceiling(42 * dpi / 96d)), m.MinimumPixels(dpi));
        Assert.True((m with { CompleteItemRowDip = 150, Version = "larger-label" }).MinimumPixels(dpi) > m.MinimumPixels(dpi));
    }

    [Fact]
    public void BudgetAndIncompatibleIntentAreNotReportedAsNoSpace()
    {
        var budget = SpatialExpansionPolicy.Plan(Scene(350), "A", 60, "op", Metrics, 1);
        Assert.Equal(SpatialFailure.BudgetExceeded, budget.Group.Plan.Issues.Single().Reason);
        var incompatible = SpatialExpansionPolicy.Plan(Scene(350), "A", 60, "op", Metrics with { CompleteItemRowDip = 900 });
        Assert.Equal(SpatialFailure.IncompatibleMetrics, incompatible.Group.Plan.Issues.Single().Reason);
        Assert.All(new[] { budget, incompatible }, r => { Assert.Equal(SpatialExpansionOutcome.Rejected, r.Outcome); Assert.Empty(r.Group.Frames); });
    }

    [Fact]
    public void SpaceReleasedBeforeTheNextExplicitExpansionRestoresPreferredHeight()
    {
        var constrained = SpatialExpansionPolicy.Plan(Scene(350), "A", 60, "one", Metrics);
        Assert.Equal(450, constrained.EffectiveHeight);
        var free = SpatialExpansionPolicy.Plan(Scene(0), "A", 60, "two", Metrics);
        Assert.Equal(600, free.EffectiveHeight);
    }

    [Fact]
    public void SmallRandomScenesMatchExhaustiveMaximumAndHaveSafePaths()
    {
        var random = new Random(2407);
        for (int trial = 0; trial < 80; trial++)
        {
            var environment = TestEnvironment with { WorkArea = new(-20, -10, 50, 40) };
            var a = new SpatialPoster("A", new(0, 6, 8, 3), 22, true, false, true, 0);
            var posters = new List<SpatialPoster> { a };
            for (int i = 0; i < 7; i++)
            {
                var p = new SpatialPoster("p" + i, new(random.Next(-10, 12), random.Next(-10, 25), random.Next(4, 12), 4),
                    4, true, random.Next(3) == 0, false, 0);
                if (!posters.Any(o => o.Bounds.Overlaps(p.Bounds))) posters.Add(p);
            }
            var scene = new SpatialSnapshot(environment, posters);
            int maximum = 3, maximumDownAtBestHeight = 0;
            for (int up = 0; up <= 16; up++) for (int down = 0; down <= 19 - up; down++)
            {
                var endpoint = new SpatialRect(0, 6 - up, 8, 3 + up + down);
                if (!environment.WorkArea.Contains(endpoint)) continue;
                var candidate = SpatialPushSolver.Plan(scene, "A", false, 3, "exhaustive", expansionBounds: endpoint);
                if (candidate.Plan.Succeeded && (endpoint.Height > maximum ||
                    (endpoint.Height == maximum && down > maximumDownAtBestHeight)))
                { maximum = endpoint.Height; maximumDownAtBestHeight = down; }
            }
            var actual = SpatialExpansionPolicy.Plan(scene, "A", 3, "selected", new("small", 1, 1, 1));
            Assert.Equal(maximum, actual.EffectiveHeight);
            Assert.Equal(a.Bounds.Bottom + maximumDownAtBestHeight,
                actual.Group.Plan.Changes.Single(c => c.PosterId == "A").After.Bottom);
            AssertFrames(scene, actual.Group);
        }
    }
}
