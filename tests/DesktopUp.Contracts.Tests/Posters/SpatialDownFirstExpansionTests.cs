using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;
using static DesktopUp.Contracts.Tests.SpatialPushIntegrationTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialDownFirstExpansionTests
{
    private static readonly PosterContentMetrics Metrics = new("fixture", 150, 100, 50);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(140)] [InlineData(300)]
    public void UsesEveryAvailableDownwardPixelEvenWhenPureUpWouldFit(int gap)
    {
        var scene = new SpatialSnapshot(TestEnvironment with { WorkArea = new(0, 0, 1000, 1000) },
            [Poster("A", 0, 600, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 },
             Poster("B", 0, 660 + gap, 240, 340 - gap) with { IsLocked = true }]);
        var result = SpatialExpansionPolicy.Plan(scene, "A", 60, "down-first", Metrics);
        Assert.Equal(SpatialExpansionOutcome.Full, result.Outcome);
        AssertDownThenUp(scene, result.Group, new(0, 60 + gap, 240, 600));
        Assert.Single(result.Group.Plan.Changes);
        Assert.Empty(result.Group.NewProof!.Participants);
    }

    [Theory]
    [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(192)]
    public void DownFirstIsIndependentOfDpiAndNegativeWorkspaceOrigin(uint dpi)
    {
        int Px(int dip) => (int)(dip * dpi / 96);
        const int x = -2200, y = -1400;
        var scene = new SpatialSnapshot(TestEnvironment with
            { WorkArea = new(x, y, Px(1000), Px(1000)), DpiX = dpi, DpiY = dpi },
            [Poster("A", x, y + Px(600), Px(240), Px(60)) with { IsCollapsed = true, ExpandedHeight = Px(600) },
             Poster("B", x, y + Px(800), Px(240), Px(200)) with { IsLocked = true }]);
        var result = SpatialExpansionPolicy.Plan(scene, "A", Px(60), "scaled", Metrics);
        Assert.Equal(SpatialExpansionOutcome.Full, result.Outcome);
        AssertDownThenUp(scene, result.Group, new(x, y + Px(200), Px(240), Px(600)));
        Assert.Equal(Px(600), result.Group.NewProof!.ExpandedSource.ExpandedHeight);
    }

    [Fact]
    public void IncludesLegalChainTravelInDownwardCapacityAndCollapsesToOriginalAnchor()
    {
        var scene = new SpatialSnapshot(TestEnvironment with { WorkArea = new(0, 0, 1000, 1000) },
            [Poster("A", 0, 600, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 },
             Poster("B", 0, 700, 240, 100),
             Poster("C", 0, 900, 240, 100) with { IsLocked = true }]);
        var result = SpatialExpansionPolicy.Plan(scene, "A", 60, "push-down", Metrics);
        Assert.Equal(SpatialExpansionOutcome.Full, result.Outcome);
        AssertDownThenUp(scene, result.Group, new(0, 200, 240, 600));
        Assert.Equal(new(0, 800, 240, 100), result.Group.Plan.Changes.Single(c => c.PosterId == "B").After);
        var proof = result.Group.NewProof!;
        var expanded = new SpatialSnapshot(scene.Environment, scene.Posters.Select(p => p.PosterId == "A"
            ? proof.ExpandedSource : proof.Participants.SingleOrDefault(t => t.TargetBefore.PosterId == p.PosterId)?.TargetAfter ?? p));
        var collapse = SpatialPushSolver.Plan(expanded, "A", true, 60, "return", proof);
        Assert.True(collapse.Plan.Succeeded);
        AssertFrames(expanded, collapse);
        foreach (var change in collapse.Plan.Changes)
            Assert.Equal(scene.Posters.Single(p => p.PosterId == change.PosterId).Bounds, change.After);
        // No peer-only tail after the source finishes collapsing.
        Assert.All(collapse.Frames, f => Assert.Contains(f.Changes, c => c.PosterId == "A"));
    }

    private static void AssertDownThenUp(SpatialSnapshot scene, SpatialGroupPlan group, SpatialRect endpoint)
    {
        Assert.True(group.Plan.Succeeded);
        Assert.Equal(endpoint, group.Plan.Changes.Single(c => c.PosterId == "A").After);
        var start = scene.Posters.Single(p => p.PosterId == "A").Bounds;
        var legs = group.Frames.SelectMany(f => f.Changes).Where(c => c.PosterId == "A").ToArray();
        bool goingUp = false;
        foreach (var leg in legs)
        {
            Assert.Equal(start.X, leg.To.X); Assert.Equal(start.Width, leg.To.Width);
            Assert.True(leg.To.Height > leg.From.Height);
            if (leg.To.Y < leg.From.Y)
            {
                goingUp = true;
                Assert.Equal(endpoint.Bottom, leg.From.Bottom);
                Assert.Equal(endpoint.Bottom, leg.To.Bottom);
            }
            else
            {
                Assert.False(goingUp);
                Assert.Equal(start.Y, leg.To.Y);
                Assert.True(leg.To.Bottom > leg.From.Bottom);
            }
        }
        if (endpoint.Bottom > start.Bottom) Assert.Equal(start.Y, legs[0].To.Y);
        AssertFrames(scene, group);
    }
}
