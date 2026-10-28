using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleSafeStopTests
{
    private static TitlePointerInput Input(TitleSpatialSession s, long seq, TitlePointerEventKind kind, int dx = 0, int dy = 0) =>
        TitleSpatialTestData.Input(s, kind, dx, dy).Pointer! with { Sequence = seq };
    private static void Drain(TitlePresentationDriver d)
    {
        for (int i = 0; i < 200 && !d.Session.Complete && !d.Session.Cancelled; i++) d.Tick(() => true, _ => true);
        Assert.True(d.Session.Complete);
        Assert.Equal(TitleSpatialTestData.Geometry(d.Session.Presented),
            TitleSpatialTestData.Geometry(TitleSpatialSession.Replay(d.Session.Freeze(), TitleSpatialTestData.Metrics).Presented));
    }
    private static TitleSpatialSession Bent()
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 20, 20, 100, 100, 100),
             TitleSpatialTestData.P("O", 200, 200, 200, 200, 200) with { IsLocked = true }]);
        return new(TitleSpatialTestData.Start(scene), TitleSpatialTestData.Metrics);
    }

    [Theory]
    [InlineData(TitlePresentedPosture.Compact)]
    [InlineData(TitlePresentedPosture.Expanding)]
    [InlineData(TitlePresentedPosture.Expanded)]
    [InlineData(TitlePresentedPosture.Collapsing)]
    public void CaptureInterruptionDropsUnpresentedMotionAndKeepsVerifiedAnchor(TitlePresentedPosture stage)
    {
        int height = stage is TitlePresentedPosture.Expanded or TitlePresentedPosture.Collapsing ? 360 : 60;
        bool? paused = stage == TitlePresentedPosture.Expanding ? false : stage == TitlePresentedPosture.Collapsing ? true : null;
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 200, height)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene, paused: paused), TitleSpatialTestData.Metrics);
        var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        var anchor = d.Physical["C"];
        d.Queue(Input(s, 3, TitlePointerEventKind.Move, 600));
        d.Queue(Input(s, 4, TitlePointerEventKind.Interrupt, 900));
        Drain(d);
        Assert.Equal(anchor.X, d.Physical["C"].X); Assert.Equal(anchor.Y, d.Physical["C"].Y);
        Assert.Equal(stage == TitlePresentedPosture.Expanded ? 360 : 60, d.Physical["C"].Height);
        Assert.False(d.Released); Assert.True(d.Interrupted); Assert.True(d.InputEnded);
        Assert.Equal(TitleInteractionPhase.Interrupted, s.Intent.Phase);
        Assert.False(d.Queue(Input(s, 5, TitlePointerEventKind.Release)));
        Assert.Equal(stage != TitlePresentedPosture.Expanded, s.Intent.Rearm is not null);
    }

    [Fact]
    public void InterruptionInBentPathAttestsActualWholeFrameNotOldOriginOrUnpresentedEndpoint()
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 500, 500)); d.Tick(() => true, _ => true);
        Assert.True(d.HasPendingPresentation);
        var stop = d.Physical["C"];
        Assert.NotEqual(s.Presented.Posters.Single(p => p.PosterId == "C").Bounds, stop);
        d.Queue(Input(s, 3, TitlePointerEventKind.Interrupt, 900, 900)); Drain(d);
        Assert.Equal(stop, d.Physical["C"]);
        Assert.Equal(1, s.Freeze().Samples[0].PresentedFrames);
        Assert.True(s.HasUserMoved);
        Assert.Empty(PosterSpatialSolver.ValidateLayout(s.Presented));
    }

    [Fact]
    public void NewDirectionPreemptsBentTailOnlyAtVerifiedGroupBoundary()
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        var physical = s.Presented;
        bool Present(SpatialBatchFrame frame) { physical = TitleSpatialTestData.Apply(physical, [frame]); return true; }
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 500, 500)); d.Tick(() => true, Present);
        Assert.True(d.HasPendingPresentation);
        d.Queue(Input(s, 3, TitlePointerEventKind.Move, 450, 450));
        d.Queue(Input(s, 4, TitlePointerEventKind.Move, -10, 0)); d.Tick(() => true, Present);
        Assert.Equal(10, d.Physical["C"].X); Assert.Equal(20, d.Physical["C"].Y);
        d.Queue(Input(s, 5, TitlePointerEventKind.Release, -10)); Drain(d);
        Assert.Equal(1, s.Freeze().Samples[0].PresentedFrames);
    }

    [Theory]
    [InlineData(TitleInvalidation.EnvironmentChanged)]
    [InlineData(TitleInvalidation.Hidden)]
    [InlineData(TitleInvalidation.Removed)]
    [InlineData(TitleInvalidation.Conflict)]
    [InlineData(TitleInvalidation.PolicyDisabled)]
    public void HardFailureCannotBeDowngradedToSafeStop(TitleInvalidation reason)
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        d.Queue(Input(s, 3, TitlePointerEventKind.Interrupt) with { Reason = reason });
        d.Tick(() => true, _ => true);
        Assert.True(s.Cancelled); Assert.Throws<InvalidOperationException>(() => s.Freeze());
    }

    [Fact]
    public void FailedNativeBatchNeverBecomesAnAcceptedPrefixOrSafeStop()
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 500, 500)); d.Tick(() => true, _ => false);
        Assert.True(s.Cancelled); Assert.Equal("native-batch-rejected", d.FailureReason);
        Assert.False(d.Queue(Input(s, 3, TitlePointerEventKind.Interrupt)));
        Assert.Equal(0, s.SampleCount);
    }

    [Fact]
    public void ExpiredRuntimeBeforeSoftFinishStillCancelsWithoutCommitProof()
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        d.Queue(Input(s, 3, TitlePointerEventKind.Interrupt)); d.Tick(() => false, _ => throw new Exception("stale frame"));
        Assert.True(s.Cancelled); Assert.Throws<InvalidOperationException>(() => s.Freeze());
    }

    [Fact]
    public void PendingInterruptionCannotBecomeClickOrMove()
    {
        var s = Bent(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Interrupt)); d.Tick(() => true, _ => throw new Exception("pending must not draw"));
        Assert.True(s.Cancelled); Assert.False(s.HasUserMoved);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1000)]
    public void InvalidPrefixCannotAuthorizeHiddenMotion(int count)
    {
        var s = Bent(); var proposal = s.Propose(new(Input(s, 2, TitlePointerEventKind.Move, 500, 500)))!;
        Assert.False(s.AcceptPresentedPrefix(proposal, count)); Assert.Equal(0, s.SampleCount);
        Assert.Throws<ArgumentException>(() => s.Propose(new(Input(s, 2, TitlePointerEventKind.Move, 500, 500), count)));
    }

    [Fact]
    public void InterruptedTransferredExpansionPreservesPeerHomesAndConditionalReturn()
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 0), TitleSpatialTestData.P("B", 0, 60), TitleSpatialTestData.P("A", 0, 120)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial, paused: false), TitleSpatialTestData.Metrics);
        var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 500)); d.Tick(() => true, _ => true);
        var anchor = d.Physical["C"];
        d.Queue(Input(s, 3, TitlePointerEventKind.Interrupt, 900)); Drain(d);
        Assert.Equal(anchor.X, d.Physical["C"].X); Assert.Equal(anchor.Y, d.Physical["C"].Y);
        Assert.Equal(60, d.Physical["B"].Y); Assert.Equal(120, d.Physical["A"].Y);
        Assert.Equal(60, s.Continuity.Nodes.Single(n => n.Expected.PosterId == "B").HomeY);
        Assert.Equal(120, s.Continuity.Nodes.Single(n => n.Expected.PosterId == "A").HomeY);
    }

    [Fact]
    public void AcceptedPrefixYieldsAtCheckpointBoundaryBeforeStartingAnotherProposal()
    {
        var s = Bent();
        for (int i = 0; i < s.MotionBudget / 2 - 1; i++)
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, i % 2 == 0 ? 10 : 0));
        var d = new TitlePresentationDriver(s); long seq = s.Intent.Sequence;
        d.Queue(Input(s, ++seq, TitlePointerEventKind.Move, 500, 500)); d.Tick(() => true, _ => true);
        Assert.True(d.HasPendingPresentation); var stop = d.Physical["C"];
        d.Queue(Input(s, ++seq, TitlePointerEventKind.Move, -10)); d.Tick(() => true, _ => true);
        Assert.True(s.NeedsCheckpoint); Assert.False(d.HasPendingPresentation);
        Assert.Equal(stop, d.Physical["C"]); Assert.Equal(s.MotionBudget / 2, s.SampleCount);
        // The real runtime seals here through its bound coordinator. Even a terminal that
        // arrives before the next frame must remain replayable without a hidden tail.
        d.Queue(Input(s, ++seq, TitlePointerEventKind.Interrupt)); Drain(d);
        Assert.Equal(stop, d.Physical["C"]);
    }

    [Fact]
    public void RepeatedBurstsAndReversalsAreLatestBoundedAndReplayable()
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 100)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene, paused: false), TitleSpatialTestData.Metrics);
        var d = new TitlePresentationDriver(s); long seq = 2;
        d.Queue(Input(s, seq++, TitlePointerEventKind.Move, 10)); d.Tick(() => true, _ => true);
        for (int i = 0; i < 100; i++)
        {
            d.Queue(Input(s, seq++, TitlePointerEventKind.Move, 500));
            int latest = i % 2 == 0 ? 120 : -60;
            d.Queue(Input(s, seq++, TitlePointerEventKind.Move, latest)); d.Tick(() => true, _ => true);
            Assert.Equal(100 + latest, d.Physical["C"].X);
        }
        d.Queue(Input(s, seq, TitlePointerEventKind.Interrupt)); Drain(d);
        Assert.Equal(40, d.Physical["C"].X); Assert.True(s.SampleCount <= 103);
    }
}
