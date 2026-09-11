using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ExpansionDragModelTests
{
    private static readonly SpatialEnvironment Environment = new("test", new(-1000, -200, 2000, 1400),
        96, 96, 1, 1, Guid.Parse("dd6664df-fc4e-4669-a62b-855536e463d8"));
    private static readonly Guid Capture = Guid.Parse("778944c5-da0f-441e-8109-9bd234975bad");
    private static SpatialPoster P(string id = "A", int x = 0, int y = 0, bool locked = false) =>
        new(id, new(x, y, 240, 60), 360, true, locked, true, 1);
    private static ExpansionDragState Start(params SpatialPoster[] posters)
    {
        var scene = new SpatialSnapshot(Environment, posters.Length == 0 ? [P()] : posters);
        return ExpansionDragModel.Start(new("expand", new("A", 1), Environment), scene, 140);
    }
    private static ExpansionDragStep Send(ExpansionDragState s, ExpansionDragEventKind kind,
        Guid? capture = null, bool threshold = false) => ExpansionDragModel.Advance(s,
        new(s.Identity, s.Sequence + 1, kind, capture, threshold));
    private static ExpansionDragState Drag(ExpansionDragState s) =>
        Send(s, ExpansionDragEventKind.BeginDrag, Capture, true).State;
    private static SpatialRect Bounds(ExpansionDragState s, string id = "A") => s.Presented.Posters.Single(p => p.PosterId == id).Bounds;
    private static ExpansionDragState Grow(ExpansionDragState s, int pixels, bool upward = false)
    {
        var beforeRect = Bounds(s); var to = beforeRect with { Height = beforeRect.Height + pixels, Y = beforeRect.Y - (upward ? pixels : 0) };
        return ExpansionDragModel.AcceptPresented(s, ExpansionDragMotion.Grow, new([new("A", beforeRect, to)]));
    }
    private static ExpansionDragState Move(ExpansionDragState s, int dx, int dy)
    {
        var beforeRect = Bounds(s);
        var plan = SpatialCollisionSolver.Resolve(s.Presented, new("A", beforeRect with { X = beforeRect.X + dx, Y = beforeRect.Y + dy },
            PosterGestureKind.Move, PosterOperatingMode.Using, 1, 1));
        Assert.True(plan.Succeeded, string.Join(',', plan.Issues));
        foreach (var leg in plan.Path)
            s = ExpansionDragModel.AcceptPresented(s, ExpansionDragMotion.Move, new([leg]));
        return s;
    }

    [Fact]
    public void OrdinaryClicksAndSubthresholdPressNeverCancelOrTakeOver()
    {
        var s = Grow(Start(), 30);
        for (int i = 0; i < 30; i++)
        {
            s = Send(s, ExpansionDragEventKind.PointerActivity).State;
            s = Send(s, ExpansionDragEventKind.BeginDrag, Capture, false).State;
            s = Send(s, ExpansionDragEventKind.ReleaseDrag, Capture).State;
        }
        Assert.Equal(ExpansionDragPhase.Expanding, s.Phase); Assert.Null(s.Capture); Assert.Equal(90, Bounds(s).Height);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReleaseAndFinishEitherOrderReachExactlyOneCommit(bool releaseFirst)
    {
        var s = Grow(Start(), 50); s = Drag(s); s = Move(s, 80, 30); s = Grow(s, 250);
        int commits = 0;
        var first = releaseFirst ? ExpansionDragEventKind.ReleaseDrag : ExpansionDragEventKind.ExpansionFinished;
        var second = releaseFirst ? ExpansionDragEventKind.ExpansionFinished : ExpansionDragEventKind.ReleaseDrag;
        var step = Send(s, first, Capture); s = step.State; commits += step.RequestCommit ? 1 : 0;
        Assert.Equal(releaseFirst ? ExpansionDragPhase.Expanding : ExpansionDragPhase.Dragging, s.Phase);
        Assert.Equal(releaseFirst ? null : Capture, s.Capture);
        step = Send(s, second, Capture); s = step.State; commits += step.RequestCommit ? 1 : 0;
        Assert.Equal(ExpansionDragPhase.AwaitingCommit, s.Phase); Assert.Equal(1, commits);
        Assert.Equal(new SpatialRect(80, 30, 240, 360), Bounds(s));
        Assert.False(Send(s, second, Capture).RequestCommit);
        Assert.Equal(ExpansionDragPhase.Completed, Send(s, ExpansionDragEventKind.CommitSucceeded).State.Phase);
    }

    [Fact]
    public void AnimationEndsFirstAndDragContinuesUntilRelease()
    {
        var s = Drag(Grow(Start(), 300)); s = Send(s, ExpansionDragEventKind.ExpansionFinished).State;
        s = Move(s, 70, 40); s = Move(s, -20, 10);
        Assert.Equal(ExpansionDragPhase.Dragging, s.Phase); Assert.Equal(Capture, s.Capture);
        Assert.Equal(new SpatialRect(50, 50, 240, 360), Bounds(s));
        Assert.True(Send(s, ExpansionDragEventKind.ReleaseDrag, Capture).RequestCommit);
    }

    [Fact]
    public void ReleaseFirstKeepsPositionAndGrowthProgressWithoutRollback()
    {
        var s = Drag(Grow(Start(), 64)); s = Move(s, 90, 40);
        var step = Send(s, ExpansionDragEventKind.ReleaseDrag, Capture); s = step.State;
        Assert.False(step.RequestCommit); Assert.Equal(124, Bounds(s).Height);
        s = Grow(s, 100); s = Grow(s, 136);
        Assert.Equal(new SpatialRect(90, 40, 240, 360), Bounds(s));
        Assert.True(Send(s, ExpansionDragEventKind.ExpansionFinished).RequestCommit);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EveryActivePhaseCanInvalidateAndLateEventsCannotResurrect(int phase)
    {
        var s = Grow(Start(), 300);
        if (phase is 1 or 2) s = Drag(s);
        if (phase is 2 or 3) s = Send(s, ExpansionDragEventKind.ExpansionFinished).State;
        s = Send(s, ExpansionDragEventKind.Invalidate).State;
        Assert.Equal(ExpansionDragPhase.Cancelled, s.Phase); Assert.Null(s.Capture);
        foreach (var kind in Enum.GetValues<ExpansionDragEventKind>())
            Assert.Same(s, Send(s, kind, Capture, true).State);
    }

    [Fact]
    public void IdentitySequenceAndCaptureRejectStaleOrForeignEvents()
    {
        var s = Drag(Start());
        foreach (var identity in new[] { s.Identity with { OperationId = "other" },
            s.Identity with { Session = new("B", 1) }, s.Identity with { Session = new("A", 2) },
            s.Identity with { Environment = Environment with { DpiY = 120 } } })
            Assert.Same(s, ExpansionDragModel.Advance(s, new(identity, s.Sequence + 1, ExpansionDragEventKind.Invalidate)).State);
        Assert.Same(s, ExpansionDragModel.Advance(s, new(s.Identity, s.Sequence, ExpansionDragEventKind.ReleaseDrag, Capture)).State);
        Assert.Equal(Capture, Send(s, ExpansionDragEventKind.ReleaseDrag, Guid.NewGuid()).State.Capture);
    }

    [Fact]
    public void LockedSourceMayExpandButCannotBecomeADrag()
    {
        var s = Grow(Start(P(locked: true)), 100); s = Drag(s);
        Assert.Equal(ExpansionDragPhase.Expanding, s.Phase); Assert.Null(s.Capture);
    }

    [Fact]
    public void HalfExpandedBelowMinimumCannotFinishOrCommit()
    {
        var s = Grow(Start(), 79);
        Assert.Equal(ExpansionDragPhase.Expanding, Send(s, ExpansionDragEventKind.ExpansionFinished).State.Phase);
        Assert.Equal(ExpansionDragPhase.Expanding, Send(s, ExpansionDragEventKind.CommitSucceeded).State.Phase);
        s = Grow(s, 1);
        Assert.True(Send(s, ExpansionDragEventKind.ExpansionFinished).RequestCommit);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TakeoverUsesPresentedHeightIncludingUpwardPrefix(bool twoLeg)
    {
        var s = Start(P(y: 900));
        if (twoLeg) s = Grow(s, 20);
        s = Grow(s, 80, true); var before = Bounds(s); s = Drag(s);
        Assert.Equal(before, Bounds(s)); s = Move(s, -40, -30);
        Assert.Equal(before with { X = before.X - 40, Y = before.Y - 30 }, Bounds(s));
        Assert.True(s.UserAdjusted); Assert.Equal(60, Bounds(Start()).Height);
    }

    [Fact]
    public void PrepareDoesNotAdvanceStateAndReplayingPresentedBatchIsRejected()
    {
        var s = Drag(Start()); var beforeRect = Bounds(s); var frame = new SpatialBatchFrame([new("A", beforeRect, beforeRect with { X = 40 })]);
        Assert.Null(ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Move, frame)); Assert.Equal(beforeRect, Bounds(s));
        var next = ExpansionDragModel.AcceptPresented(s, ExpansionDragMotion.Move, frame);
        Assert.Equal("stale-or-outside-frame", ExpansionDragModel.CheckFrame(next, ExpansionDragMotion.Move, frame));
        Assert.Equal(beforeRect, Bounds(s)); // immutable pre-submit rollback snapshot
    }

    [Theory]
    [InlineData("shrink")] [InlineData("width")] [InlineData("outside")] [InlineData("no-growth")]
    public void UnsafeGrowthCannotBeAccepted(string kind)
    {
        var s = Drag(Grow(Start(), 120)); var beforeRect = Bounds(s);
        var to = kind switch { "shrink" => beforeRect with { Height = 60 }, "width" => beforeRect with { Width = 241, Height = 200 },
            "outside" => beforeRect with { Y = -300, Height = 360 }, _ => beforeRect with { Y = -20 } };
        var frame = new SpatialBatchFrame([new("A", beforeRect, to)]);
        Assert.NotNull(ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Grow, frame));
        Assert.Throws<ArgumentException>(() => ExpansionDragModel.AcceptPresented(s, ExpansionDragMotion.Grow, frame));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeBatchDetectsEndpointOverlapAndSweptCrossing(bool cross)
    {
        var s = Drag(Start(P(), P("B", 400)));
        var beforeRect = Bounds(s); var frame = new SpatialBatchFrame([new("A", beforeRect, beforeRect with { X = cross ? 700 : 300 })]);
        Assert.Equal("swept-collision", ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Move, frame));
        var legal = Move(s, 700, 0); Assert.Equal(160, Bounds(legal).X);
        Assert.Equal(Bounds(s, "B"), Bounds(legal, "B")); Assert.Equal(Capture, legal.Capture);
    }

    [Fact]
    public void MoveCannotResizeOrBringNeighborsAlong()
    {
        var s = Drag(Start(P(), P("B", 400)));
        var a = Bounds(s); var b = Bounds(s, "B");
        Assert.Equal("move-cannot-resize-or-push", ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Move,
            new([new("A", a, a with { X = 20 }), new("B", b, b with { X = 420 })])));
        Assert.Equal("move-cannot-resize-or-push", ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Move,
            new([new("A", a, a with { Height = 100 })])));
    }

    [Fact]
    public void ExistingPushSolverFramesRemainSafeDuringCapturedExpansion()
    {
        var s = Drag(Start(P(), P("B", 0, 60), P("C", 0, 120)));
        var plan = SpatialExpansionPolicy.Plan(s.Presented, "A", 60, "probe", PosterContentMetrics.Current);
        Assert.True(plan.Group.Plan.Succeeded);
        foreach (var frame in plan.Group.Frames) s = ExpansionDragModel.AcceptPresented(s, ExpansionDragMotion.Grow, frame);
        Assert.Equal(360, Bounds(s).Height); Assert.Equal(360, Bounds(s, "B").Y); Assert.Equal(420, Bounds(s, "C").Y);
        Assert.Equal(ExpansionDragPhase.ExpandingAndDragging, s.Phase);
    }

    [Fact]
    public void StructuralSafetyIsNotProofThatNeighborMotionIsAuthorized()
    {
        var s = Start(P(), P("B", 600, 0)); var a = Bounds(s); var b = Bounds(s, "B");
        // This is geometrically safe but B was never contacted. A production commit MUST
        // additionally replay the spatial strategy/continuity proof and reject this proposal.
        Assert.Null(ExpansionDragModel.CheckFrame(s, ExpansionDragMotion.Grow,
            new([new("A", a, a with { Height = 80 }), new("B", b, b with { Y = 20 })])));
    }

    [Theory]
    [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(192)]
    public void ThousandsOfReversedMovesHaveNoAccumulatedDrift(int dpi)
    {
        var env = Environment with { DpiX = (uint)dpi, DpiY = (uint)dpi };
        var s = ExpansionDragModel.Start(new("expand", new("A", 1), env), new(env, [P(x: -500)]), 140);
        s = Drag(Grow(s, 100)); var start = Bounds(s); var copy = s;
        var random = new Random(dpi);
        for (int i = 0; i < 1000; i++)
        {
            int dx = random.Next(-40, 41), dy = random.Next(-40, 41);
            s = Move(Move(s, dx, dy), -dx, -dy); copy = Move(Move(copy, dx, dy), -dx, -dy);
            Assert.Equal(start, Bounds(s)); Assert.Equal(Bounds(s), Bounds(copy));
            Assert.Empty(PosterSpatialSolver.ValidateLayout(s.Presented));
        }
        Assert.True(s.UserAdjusted); Assert.Equal(Capture, s.Capture);
    }
}
