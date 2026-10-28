using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class TitlePresentationDriverTests
{
    private static TitleSpatialSession Session(bool? paused = false, int y = 0, bool locked = false)
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 100, y) with { IsLocked = locked }]);
        return new(TitleSpatialTestData.Start(scene, paused: paused), TitleSpatialTestData.Metrics);
    }
    private static TitlePointerInput Input(TitleSpatialSession s, long sequence, TitlePointerEventKind kind, int dx = 0) =>
        TitleSpatialTestData.Input(s, kind, dx).Pointer! with { Sequence = sequence };
    private static void Drain(TitlePresentationDriver driver, Func<SpatialBatchFrame, bool>? present = null)
    {
        int ticks = 0;
        while (!driver.Session.Complete && !driver.Session.Cancelled && ticks++ < 1000)
            driver.Tick(() => true, present ?? (_ => true));
        Assert.True(ticks < 1000, "adapter did not terminate");
    }

    [Theory] [InlineData(0)] [InlineData(600)] [InlineData(940)]
    public void FastClickCommitsOnePreparedGroupNotTheAnimationProof(int y)
    {
        var s = Session(y: y); var d = new TitlePresentationDriver(s); int submits = 0;
        d.Queue(Input(s, 2, TitlePointerEventKind.Release));
        Assert.False(s.Complete);
        Drain(d, frame => { submits++; Assert.NotEmpty(frame.Changes); return true; });
        Assert.Equal(1, submits); Assert.True(s.Complete);
        Assert.True(d.WasFastExpand);
        Assert.Equal(TitleSpatialTestData.Geometry(s.Presented),
            TitleSpatialTestData.Geometry(TitleSpatialSession.Replay(s.Freeze(), TitleSpatialTestData.Metrics).Presented));
    }

    [Theory] [InlineData(0)] [InlineData(600)] [InlineData(940)]
    public void MoveAndReleaseFollowAcceptedFrameAndKeepOwnershipThroughRemainingRetraction(int y)
    {
        var s = Session(y: y); var d = new TitlePresentationDriver(s); int originalY = s.Presented.Posters.Single().Bounds.Y;
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        Assert.Equal(originalY, s.Presented.Posters.Single().Bounds.Y);
        Assert.Equal(130, s.Presented.Posters.Single().Bounds.X);
        d.Queue(Input(s, 3, TitlePointerEventKind.Release, 30));
        d.Tick(() => true, _ => true);
        Assert.True(d.Released); Assert.False(s.Complete); Assert.True(d.HasPendingPresentation);
        Assert.Throws<InvalidOperationException>(() => s.Freeze());
        Drain(d);
        Assert.True(s.Complete); Assert.True(s.Presented.Posters.Single().IsCollapsed);
        Assert.Equal(originalY, s.Presented.Posters.Single().Bounds.Y);
        Assert.NotNull(s.Intent.Rearm);
        Assert.Equal(s.Presented.Posters.Single().Bounds, d.Physical["C"]);
    }

    [Fact]
    public void CoalescingPreservesCrossingAndReturnBeforeButtonUp()
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 1));
        d.Queue(Input(s, 3, TitlePointerEventKind.Move, 30));
        d.Queue(Input(s, 4, TitlePointerEventKind.Move, 20));
        d.Queue(Input(s, 5, TitlePointerEventKind.Move, 0));
        d.Queue(Input(s, 6, TitlePointerEventKind.Release));
        Drain(d);
        Assert.True(s.HasUserMoved);
        Assert.True(s.Presented.Posters.Single().IsCollapsed);
        Assert.Equal(100, s.Presented.Posters.Single().Bounds.X);
    }

    [Fact]
    public void PendingHoldHasNoTimerDrivenGrowthOrCollapse()
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        var before = TitleSpatialTestData.Geometry(s.Presented);
        Assert.False(d.HasPendingWork);
        for (int i = 0; i < 10000; i++) d.Tick(() => true, _ => throw new Exception("must not draw"));
        Assert.Equal(before, TitleSpatialTestData.Geometry(s.Presented));
        Assert.Equal(0, s.SampleCount); Assert.Equal(TitleInteractionPhase.Pending, s.Intent.Phase);
    }

    [Fact]
    public void StationaryCompactDragRetractsButNeverResumesGrowth()
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30));
        int frames = 0;
        while (d.HasPendingWork && frames++ < 100) d.Tick(() => true, _ => true);
        Assert.InRange(frames, 1, 99);
        Assert.False(d.HasPendingWork);
        Assert.True(s.Presented.Posters.Single().IsCollapsed); Assert.False(s.Complete);
        int samples = s.SampleCount;
        for (int i = 0; i < 20; i++) d.Tick(() => true, _ => throw new Exception("no growth"));
        Assert.Equal(samples, s.SampleCount);
        d.Queue(Input(s, 3, TitlePointerEventKind.Release, 30)); Drain(d);
        Assert.True(s.Complete); Assert.True(s.Presented.Posters.Single().IsCollapsed);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void PartialNativeFailureNeverAttestsEndpointAndEndsTheLease(int successfulFrames)
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        var accepted = TitleSpatialTestData.Geometry(s.Presented);
        d.Queue(Input(s, 3, TitlePointerEventKind.Release, 30));
        int calls = 0;
        Drain(d, _ => calls++ < successfulFrames);
        Assert.True(s.Cancelled); Assert.False(s.Complete);
        Assert.Equal(accepted, TitleSpatialTestData.Geometry(s.Presented)); // opaque proposal not accepted
        Assert.Throws<InvalidOperationException>(() => s.Freeze());
        d.Tick(() => true, _ => throw new Exception("closed callback must not present"));
        Assert.False(d.Queue(Input(s, 99, TitlePointerEventKind.Move, 99)));
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void GenerationCheckRunsBeforePrepareBeforeSubmitAndBeforeAccept(int staleCheck)
    {
        var s = Session(paused: null); var d = new TitlePresentationDriver(s); int checks = 0;
        d.Queue(Input(s, 2, TitlePointerEventKind.Release));
        d.Tick(() => ++checks != staleCheck, _ => true);
        Assert.True(s.Cancelled); Assert.False(s.Complete);
    }

    [Theory] [InlineData("capture")] [InlineData("session")] [InlineData("epoch")] [InlineData("operation")] [InlineData("sequence")]
    public void ForeignAndLateInputCannotTakeOver(string stale)
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        var input = Input(s, 2, TitlePointerEventKind.Move, 30);
        input = stale switch {
            "capture" => input with { Token = input.Token with { CaptureId = Guid.NewGuid() } },
            "session" => input with { Token = input.Token with { Session = new("C", 2) } },
            "epoch" => input with { Token = input.Token with { EnvironmentEpoch = 2 } },
            "operation" => input with { Token = input.Token with { OperationId = "old" } },
            _ => input with { Sequence = 1 } };
        Assert.False(d.Queue(input));
        d.Tick(() => true, _ => throw new Exception("foreign input must not draw"));
        Assert.Equal(TitleInteractionPhase.Pending, s.Intent.Phase);
        d.Cancel(); Assert.False(d.Queue(Input(s, 3, TitlePointerEventKind.Release)));
    }

    [Fact]
    public void LockedThresholdCannotBecomeClickAndCancellationConsumesTheGesture()
    {
        var s = Session(locked: true); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 40)); d.Tick(() => true, _ => true);
        Assert.Equal(TitleInteractionPhase.BlockedDrag, s.Intent.Phase);
        d.Queue(Input(s, 3, TitlePointerEventKind.Invalidate, 40));
        Assert.True(s.Cancelled);
        Assert.False(d.Queue(Input(s, 4, TitlePointerEventKind.Release)));
    }

    [Fact]
    public void RearmUsesReleaseEvidenceAndDoesNotClearAnOlderBarrierOnNoResult()
    {
        var s = Session(); var d = new TitlePresentationDriver(s); var evidence = new AutoPostureEvidence();
        evidence.CaptureTitleSuppression(new(s.Intent.Token.Session, 1, 3));
        d.Queue(Input(s, 2, TitlePointerEventKind.Move, 30)); d.Tick(() => true, _ => true);
        d.Queue(Input(s, 3, TitlePointerEventKind.Release, 30) with { PointerEvidence = new(AutoPosturePresence.Outside, 5, 4) });
        Drain(d);
        evidence.CaptureTitleSuppression(s.Intent.Rearm!);
        Assert.Equal(4, evidence.ManualCollapseExit(s.Intent.Token.Session, new(AutoPosturePresence.Inside, 6, 4)));
        Assert.Null(evidence.ManualCollapseExit(s.Intent.Token.Session, new(AutoPosturePresence.Outside, 6, 5)));
    }

    [Fact]
    public void PresenterExceptionClosesModelBeforeHostRecovery()
    {
        var s = Session(); var d = new TitlePresentationDriver(s);
        d.Queue(Input(s, 2, TitlePointerEventKind.Release));
        Assert.Throws<InvalidOperationException>(() => d.Tick(() => true, _ => throw new InvalidOperationException("native")));
        Assert.True(s.Cancelled); Assert.False(s.Complete);
    }

    [Fact]
    public void MotionBudgetFailsClosedInsteadOfKeepingSpatialOwnerBusyForever()
    {
        var s = Session(paused: null); var d = new TitlePresentationDriver(s);
        for (int i = 0; i < s.MotionBudget + 2; i++)
        {
            d.Queue(Input(s, i + 2, TitlePointerEventKind.Move, i % 2 == 0 ? 30 : 60));
            d.Tick(() => true, _ => true);
        }
        Assert.True(s.Cancelled); Assert.False(s.Complete);
        Assert.Equal("motion-budget-exhausted", d.FailureReason);
    }
}
