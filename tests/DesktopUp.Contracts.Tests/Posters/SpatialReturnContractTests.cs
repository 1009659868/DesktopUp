using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialReturnContractTests
{
    private static SpatialReturnProof Proof()
    {
        var source = Poster("A", 0, 0, 240, 600) with { LayoutGeneration = 3, LastLayoutOperationId = "expand-a" };
        var before = Poster("B", 300, 0, 80, 80) with { LayoutGeneration = 4 };
        var after = before with { Bounds = new(800, 0, 80, 80), LayoutGeneration = 5, LastLayoutOperationId = "expand-a" };
        return new("expand-a", TestEnvironment, source, before, after);
    }

    [Fact]
    public void UnchangedPushedPeerHasEligibilityButAnObstructedReturnPathDoesNot()
    {
        var proof = Proof();
        var current = Scene(proof.SourceAfter, proof.TargetAfter);
        Assert.Null(SpatialReturnPolicy.CheckEligibility(current, proof));
        var path = new[] { new SpatialPathLeg("B", proof.TargetAfter.Bounds, proof.TargetBefore.Bounds) };
        Assert.Empty(PosterSpatialSolver.CheckPath(current, "B", path));
        var blocked = Scene(proof.SourceAfter, proof.TargetAfter, Poster("C", 600, 0, 80, 80) with { IsLocked = true });
        Assert.Null(SpatialReturnPolicy.CheckEligibility(blocked, proof));
        Assert.Contains(PosterSpatialSolver.CheckPath(blocked, "B", path), i => i.Reason == SpatialFailure.PathBlocked && i.ObstacleId == "C");
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("position")]
    [InlineData("size")]
    [InlineData("lock")]
    [InlineData("hidden")]
    [InlineData("collapsed")]
    [InlineData("takeover")]
    public void UserAdjustmentOrStateChangeInvalidatesReturnEvenIfCoordinatesMatch(string change)
    {
        var proof = Proof();
        SpatialPoster target = change switch
        {
            "generation" => proof.TargetAfter with { LayoutGeneration = 7 }, // moved away and back
            "position" => proof.TargetAfter with { Bounds = new(810, 0, 80, 80) },
            "size" => proof.TargetAfter with { Bounds = new(800, 0, 90, 80) },
            "lock" => proof.TargetAfter with { IsLocked = true },
            "hidden" => proof.TargetAfter with { IsVisible = false },
            "collapsed" => proof.TargetAfter with { IsCollapsed = true },
            _ => proof.TargetAfter with { LastLayoutOperationId = "another-push" }
        };
        Assert.NotNull(SpatialReturnPolicy.CheckEligibility(Scene(proof.SourceAfter, target), proof));
    }

    [Fact]
    public void MissingConsumedDeletedSourceOrChangedSourceDoesNotMoveAnything()
    {
        var proof = Proof();
        var current = Scene(proof.SourceAfter, proof.TargetAfter);
        Assert.Equal(SpatialFailure.MissingProof, SpatialReturnPolicy.CheckEligibility(current, null)!.Reason);
        Assert.Equal(SpatialFailure.ConsumedProof, SpatialReturnPolicy.CheckEligibility(current, proof with { Consumed = true })!.Reason);
        Assert.Equal(SpatialFailure.SourceChanged, SpatialReturnPolicy.CheckEligibility(Scene(proof.TargetAfter), proof)!.Reason);
        Assert.Equal(SpatialFailure.SourceChanged, SpatialReturnPolicy.CheckEligibility(
            Scene(proof.SourceAfter with { LayoutGeneration = 9 }, proof.TargetAfter), proof)!.Reason);
        Assert.Equal(SpatialFailure.TargetChanged, SpatialReturnPolicy.CheckEligibility(Scene(proof.SourceAfter), proof)!.Reason);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("shell")]
    [InlineData("display")]
    [InlineData("dpi")]
    [InlineData("workspace")]
    public void EnvironmentOrSessionChangeInvalidatesCausalProof(string change)
    {
        var proof = Proof();
        var environment = change switch
        {
            "restart" => TestEnvironment with { RuntimeSessionId = Guid.NewGuid() },
            "shell" => TestEnvironment with { ShellGeneration = 2 },
            "display" => TestEnvironment with { DisplayGeneration = 2 },
            "dpi" => TestEnvironment with { DpiX = 120 },
            _ => TestEnvironment with { WorkArea = new(0, 0, 1000, 900) }
        };
        Assert.Equal(SpatialFailure.StaleEnvironment, SpatialReturnPolicy.CheckEligibility(
            new(environment, [proof.SourceAfter, proof.TargetAfter]), proof)!.Reason);
    }

    [Fact]
    public void MalformedProofCannotClaimOwnershipOrSkipLayoutGenerations()
    {
        var proof = Proof();
        var current = Scene(proof.SourceAfter, proof.TargetAfter);
        Assert.Equal(SpatialFailure.MissingProof, SpatialReturnPolicy.CheckEligibility(current,
            proof with { OperationId = "fabricated" })!.Reason);
        Assert.Equal(SpatialFailure.MissingProof, SpatialReturnPolicy.CheckEligibility(current,
            proof with { TargetBefore = proof.TargetBefore with { LayoutGeneration = 5 } })!.Reason);
    }

    [Fact]
    public void DiscontinuousOrStalePathCannotSkipAnObstacle()
    {
        var source = Poster("A", 0, 0, 80, 80);
        var path = new[] { new SpatialPathLeg("A", new(500, 0, 80, 80), new(800, 0, 80, 80)) };
        Assert.Contains(PosterSpatialSolver.CheckPath(Scene(source), "A", path), i => i.Reason == SpatialFailure.InvalidInput);
    }
}
