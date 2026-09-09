using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterSpatialSolverTests
{
    internal static readonly SpatialEnvironment TestEnvironment = new("display-1", new(0, 0, 1000, 1000),
        96, 96, 1, 1, Guid.Parse("245d5c52-b190-4e2e-8e55-1016a3e98e1c"));
    internal static SpatialPoster Poster(string id, int x, int y, int w = 240, int h = 240) =>
        new(id, new(x, y, w, h), h, true, false, false, 0);
    internal static SpatialSnapshot Scene(params SpatialPoster[] posters) => new(TestEnvironment, posters);
    private static SpatialPoster Compact(int y, int height = 600) =>
        Poster("A", 20, y, 240, 60) with { IsCollapsed = true, ExpandedHeight = height };
    private static SpatialFrameRequest Move(SpatialPoster p, int x, int y) =>
        new(p.PosterId, p.Bounds with { X = x, Y = y }, PosterGestureKind.Move, PosterOperatingMode.Using, 240, 240);

    [Theory]
    [InlineData(96u, 240, 60)]
    [InlineData(120u, 300, 75)]
    [InlineData(144u, 360, 90)]
    [InlineData(192u, 480, 120)]
    public void LogicalProjectionUsesExplicitPhysicalOriginAndDpi(uint dpi, int width, int height)
    {
        Assert.True(SpatialPixelProjection.TryProjectLocalDip(0, 0, 240, 60, -1920, -500, dpi, dpi, out var result));
        Assert.Equal(new SpatialRect(-1920, -500, width, height), result);
    }

    [Fact]
    public void OutwardRoundingCanInvalidateLogicalContactAndMustBeRechecked()
    {
        Assert.True(SpatialPixelProjection.TryProjectLocalDip(0, 0, 100.1, 60, 0, 0, 120, 120, out var a));
        Assert.True(SpatialPixelProjection.TryProjectLocalDip(100.1, 0, 100, 60, 0, 0, 120, 120, out var b));
        Assert.True(a.Overlaps(b));
        Assert.Contains(PosterSpatialSolver.ValidateLayout(Scene(
            Poster("A", 0, 0) with { Bounds = a, ExpandedHeight = a.Height },
            Poster("B", 0, 0) with { Bounds = b, ExpandedHeight = b.Height })), i => i.Reason == SpatialFailure.InitialOverlap);
    }

    [Theory]
    [InlineData(double.NaN, 240, 96u)]
    [InlineData(double.PositiveInfinity, 240, 96u)]
    [InlineData(0, double.PositiveInfinity, 96u)]
    [InlineData(0, 0, 96u)]
    [InlineData(0, -1, 96u)]
    [InlineData(0, 240, 0u)]
    [InlineData(0, double.MaxValue, 120u)]
    public void MalformedLogicalGeometryIsRejected(double x, double width, uint dpi) =>
        Assert.False(SpatialPixelProjection.TryProjectLocalDip(x, 0, width, 60, 0, 0, dpi, dpi, out _));

    [Fact]
    public void PixelEdgesDoNotOverflowAndTouchIsNotOverlap()
    {
        Assert.False(new SpatialRect(int.MaxValue - 10, 0, 240, 240).IsValid);
        Assert.False(new SpatialRect(0, 0, -1, 240).IsValid);
        Assert.False(new SpatialRect(0, 0, 240, 240).Overlaps(new(240, 0, 240, 240)));
        Assert.True(new SpatialRect(0, 0, 240, 240).Overlaps(new(239, 0, 240, 240)));
    }

    [Fact]
    public void HiddenPosterDoesNotOccupyButShowingItRequiresValidation()
    {
        var a = Poster("A", 0, 0);
        var hidden = Poster("B", 0, 0) with { IsVisible = false };
        Assert.Empty(PosterSpatialSolver.ValidateLayout(Scene(a, hidden)));
        Assert.Contains(PosterSpatialSolver.ValidateLayout(Scene(a, hidden with { IsVisible = true })),
            i => i.Reason == SpatialFailure.InitialOverlap);
        var collapsed = hidden with { IsVisible = true, Bounds = new(0, 0, 240, 60), IsCollapsed = true };
        Assert.Empty(PosterSpatialSolver.ValidateLayout(Scene(a with { Bounds = new(0, 60, 240, 240) }, collapsed)));
    }

    [Theory]
    [InlineData(100, 100, 700, 1)]
    [InlineData(940, 400, 1000, 1)]
    [InlineData(760, 400, 1000, 2)]
    public void ExpansionHasExactOriginalSizeAndDownThenUpTrajectory(int top, int finalTop, int finalBottom, int legs)
    {
        SpatialPoster source = Compact(top);
        SpatialPlan plan = PosterSpatialSolver.PlanExpansion(Scene(source), "A");
        Assert.Equal(SpatialPlanStatus.Accepted, plan.Status);
        SpatialChange change = Assert.Single(plan.Changes);
        Assert.Equal(new SpatialRect(20, finalTop, 240, 600), change.After);
        Assert.Equal(finalBottom, change.After.Bottom);
        Assert.Equal(legs, plan.Path.Count);
        SpatialRect previous = source.Bounds;
        foreach (var leg in plan.Path)
        {
            Assert.Equal(previous, leg.From);
            int growth = leg.To.Height - leg.From.Height;
            for (int step = 0; step <= growth; step++)
            {
                SpatialRect frame = leg.SampleGrowth(step);
                Assert.True(TestEnvironment.WorkArea.Contains(frame));
                Assert.Equal(240, frame.Width);
                Assert.Equal(leg.From.Height + step, frame.Height);
                if (leg.From.Y == leg.To.Y) Assert.Equal(top, frame.Y);
                else Assert.Equal(1000, frame.Bottom);
            }
            Assert.Equal(leg.To, leg.SampleGrowth(growth));
            previous = leg.To;
        }
        Assert.True(source.IsCollapsed); // immutable source/intent survives planning
        Assert.Equal(600, source.ExpandedHeight);
    }

    [Fact]
    public void NegativeOriginAndTaskbarWorkAreaAreRespected()
    {
        var environment = TestEnvironment with { WorkArea = new(-1600, -900, 1000, 940) };
        var poster = Compact(0) with { Bounds = new(-1500, -20, 240, 60) };
        var plan = PosterSpatialSolver.PlanExpansion(new(environment, [poster]), "A");
        Assert.Equal(new SpatialRect(-1500, -560, 240, 600), Assert.Single(plan.Changes).After);
    }

    [Fact]
    public void OversizeExpansionRejectsWithoutShrinkingOrPartialChanges()
    {
        SpatialPoster source = Compact(700, 1001);
        var plan = PosterSpatialSolver.PlanExpansion(Scene(source), "A");
        AssertRejected(plan, SpatialFailure.NoSpace);
        Assert.Equal(1001, source.ExpandedHeight);
        var oversized = Poster("X", 0, 0, 1001, 240);
        Assert.Contains(PosterSpatialSolver.ValidateLayout(Scene(oversized)), i => i.Reason == SpatialFailure.NoSpace);
    }

    [Fact]
    public void LockedSourceMayExpandButMayNotMoveOrResize()
    {
        var source = Compact(760) with { IsLocked = true };
        Assert.True(PosterSpatialSolver.PlanExpansion(Scene(source), "A").Succeeded);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(source), Move(source, 40, 760)), SpatialFailure.PermissionDenied);
    }

    [Fact]
    public void ExpansionBlockedInSecondPhaseReturnsNoFirstPhasePartialPlan()
    {
        var source = Compact(760);
        var obstacle = Poster("B", 20, 400, 240, 100) with { IsLocked = true };
        var plan = PosterSpatialSolver.PlanExpansion(Scene(source, obstacle), "A");
        AssertRejected(plan, SpatialFailure.PathBlocked);
        Assert.Equal("B", Assert.Single(plan.Issues).ObstacleId);
        Assert.Equal(new SpatialRect(20, 760, 240, 60), source.Bounds);
    }

    [Fact]
    public void FastMoveCannotJumpPastObstaclesEvenWithAClearDestination()
    {
        var a = Poster("A", 0, 0, 80, 80);
        var b = Poster("B", 200, 0, 80, 80);
        var c = Poster("C", 400, 0, 80, 80);
        var plan = PosterSpatialSolver.PlanFrameChange(Scene(c, a, b), Move(a, 700, 0));
        AssertRejected(plan, SpatialFailure.PathBlocked);
        Assert.Equal(new[] { "B", "C" }, plan.Issues.Select(i => i.ObstacleId));
    }

    [Fact]
    public void ContinuousPathAllowsGrazingAndMovingAwayButDetectsThinCrossings()
    {
        var obstacle = new SpatialRect(100, 100, 20, 20);
        Assert.False(SpatialPathPolicy.Crosses(new(0, 80, 20, 20), new(200, 80, 20, 20), obstacle));
        Assert.False(SpatialPathPolicy.Crosses(new(80, 100, 20, 20), new(0, 100, 20, 20), obstacle));
        Assert.True(SpatialPathPolicy.Crosses(new(0, 81, 20, 20), new(200, 81, 20, 20), obstacle));
        Assert.True(SpatialPathPolicy.Crosses(new(-1_000_000_000, 100, 1, 1), new(1_000_000_000, 100, 1, 1), new(137, 100, 1, 1)));
        // Diagonal corner contact at one instant is not positive-area intersection.
        Assert.False(SpatialPathPolicy.Crosses(new(0, 0, 20, 20), new(100, 100, 20, 20), new(60, 0, 20, 40)));
    }

    [Theory]
    [InlineData(PosterGestureKind.ResizeLeft, 180, 200, 320, 300)]
    [InlineData(PosterGestureKind.ResizeRight, 200, 200, 320, 300)]
    [InlineData(PosterGestureKind.ResizeTop, 200, 180, 300, 320)]
    [InlineData(PosterGestureKind.ResizeBottom, 200, 200, 300, 320)]
    [InlineData(PosterGestureKind.ResizeTopLeft, 180, 180, 320, 320)]
    [InlineData(PosterGestureKind.ResizeTopRight, 200, 180, 320, 320)]
    [InlineData(PosterGestureKind.ResizeBottomLeft, 180, 200, 320, 320)]
    [InlineData(PosterGestureKind.ResizeBottomRight, 200, 200, 320, 320)]
    public void AllEightResizeCandidatesKeepInactiveEdges(PosterGestureKind kind, int x, int y, int w, int h)
    {
        var a = Poster("A", 200, 200, 300, 300);
        var b = Poster("B", 700, 700);
        var request = new SpatialFrameRequest("A", new(x, y, w, h), kind, PosterOperatingMode.Editing, 240, 240);
        var plan = PosterSpatialSolver.PlanFrameChange(Scene(a, b), request);
        Assert.True(plan.Succeeded);
        Assert.Equal(request.Desired, Assert.Single(plan.Changes).After);
        Assert.Equal(new SpatialRect(700, 700, 240, 240), b.Bounds);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(a, b), request with { Mode = PosterOperatingMode.Using }), SpatialFailure.PermissionDenied);
    }

    [Fact]
    public void ResizingCannotChangeInactiveEdgesOrCrossWorkArea()
    {
        var a = Poster("A", 200, 200, 300, 300);
        var request = new SpatialFrameRequest("A", new(199, 200, 320, 300), PosterGestureKind.ResizeRight, PosterOperatingMode.Editing, 240, 240);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(a), request), SpatialFailure.InvalidInput);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(a), request with { Desired = new(200, 200, 1001, 300) }), SpatialFailure.NoSpace);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(a), request with { Desired = new(200, 200, 200, 300) }), SpatialFailure.InvalidInput);
        AssertRejected(PosterSpatialSolver.PlanFrameChange(Scene(a), Move(a, 900, 200)), SpatialFailure.OutsideWorkArea);
    }

    [Fact]
    public void InvalidHistoricLayoutsAndMissingHiddenTargetsAreExplicit()
    {
        var a = Poster("A", 0, 0);
        AssertRejected(PosterSpatialSolver.PlanExpansion(Scene(a, a), "A"), SpatialFailure.InvalidInput);
        AssertRejected(PosterSpatialSolver.PlanExpansion(Scene(a, Poster("B", 1, 1)), "A"), SpatialFailure.InitialOverlap);
        AssertRejected(PosterSpatialSolver.PlanExpansion(Scene(), "A"), SpatialFailure.MissingTarget);
        AssertRejected(PosterSpatialSolver.PlanExpansion(Scene(a with { IsVisible = false }), "A"), SpatialFailure.HiddenTarget);
        Assert.Equal(SpatialPlanStatus.NoChange, PosterSpatialSolver.PlanFrameChange(Scene(a), Move(a, 0, 0)).Status);
    }

    [Fact]
    public void SnapshotFreezesInputAndResultsAreIndependentOfTraversalOrder()
    {
        var a = Compact(760);
        var b = Poster("B", 20, 400, 50, 100);
        var c = Poster("C", 150, 400, 50, 100);
        var input = new List<SpatialPoster> { c, a, b };
        var snapshot = new SpatialSnapshot(TestEnvironment, input);
        input.Clear();
        var one = PosterSpatialSolver.PlanExpansion(snapshot, "A");
        var two = PosterSpatialSolver.PlanExpansion(Scene(b, c, a), "A");
        Assert.Equal(3, snapshot.Posters.Count);
        Assert.Equal(one.Issues, two.Issues);
        Assert.Throws<NotSupportedException>(() => ((IList<SpatialPoster>)snapshot.Posters).Clear());
    }

    [Fact]
    public void SeededExpansionPropertiesKeepEverySampleLegalAndOriginalSize()
    {
        var random = new Random(402);
        for (int i = 0; i < 500; i++)
        {
            int h = random.Next(60, 1001), top = random.Next(0, 941);
            var source = Compact(top, h);
            var plan = PosterSpatialSolver.PlanExpansion(Scene(source), "A");
            Assert.True(plan.Succeeded);
            var change = Assert.Single(plan.Changes);
            Assert.Equal(h, change.After.Height);
            Assert.True(TestEnvironment.WorkArea.Contains(change.After));
            foreach (var leg in plan.Path)
            {
                int growth = leg.To.Height - leg.From.Height;
                Assert.True(TestEnvironment.WorkArea.Contains(leg.SampleGrowth(growth / 2)));
                Assert.True(TestEnvironment.WorkArea.Contains(leg.SampleGrowth(growth)));
            }
        }
    }

    [Fact]
    public void SeededContinuousPathAgreesWithIndependentDenseSamples()
    {
        var random = new Random(403);
        for (int i = 0; i < 500; i++)
        {
            var from = new SpatialRect(random.Next(-30, 31), random.Next(-30, 31), 10, 10);
            var to = new SpatialRect(random.Next(-30, 31), random.Next(-30, 31), random.Next(1, 16), random.Next(1, 16));
            var obstacle = new SpatialRect(0, 0, 10, 10);
            bool sampledHit = false;
            for (int step = 0; step <= 200; step++)
            {
                double t = step / 200d;
                double left = from.X + (to.X - from.X) * t, top = from.Y + (to.Y - from.Y) * t;
                double right = from.Right + (to.Right - from.Right) * t, bottom = from.Bottom + (to.Bottom - from.Bottom) * t;
                sampledHit |= left < 10 && right > 0 && top < 10 && bottom > 0;
            }
            if (sampledHit) Assert.True(SpatialPathPolicy.Crosses(from, to, obstacle));
        }
    }

    private static void AssertRejected(SpatialPlan plan, SpatialFailure reason)
    {
        Assert.Equal(SpatialPlanStatus.Rejected, plan.Status);
        Assert.Contains(plan.Issues, i => i.Reason == reason);
        Assert.Empty(plan.Changes);
        Assert.Empty(plan.Path);
    }
}
