using DesktopUp.Application;
using static DesktopUp.Contracts.Tests.TitleTestData;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleInteractionModelTests
{
    [Theory]
    [InlineData(TitlePresentedPosture.Compact)]
    [InlineData(TitlePresentedPosture.Expanding)]
    [InlineData(TitlePresentedPosture.Collapsing)]
    [InlineData(TitlePresentedPosture.Expanded)]
    public void Down_pauses_immediately_but_never_expands_or_changes_presented_geometry(TitlePresentedPosture posture)
    {
        var press = Press(posture);
        var step = TitleInteractionModel.Begin(null, press);
        Assert.Equal(TitleInteractionAction.PauseGroup, step.Action);
        var state = Assert.IsType<TitleInteractionState>(step.State);
        Assert.True(state.HasPointerCapture); Assert.True(step.Consumed);
        Assert.Equal(TitleInteractionPhase.Pending, state.Phase);
        Assert.Equal(press, state.Press); Assert.Equal(new TitlePointerDelta(20, 18), state.GrabOffset);
    }

    [Theory]
    [InlineData(TitlePresentedPosture.Compact, TitleInteractionAction.FastExpand)]
    [InlineData(TitlePresentedPosture.Expanding, TitleInteractionAction.FastExpand)]
    [InlineData(TitlePresentedPosture.Collapsing, TitleInteractionAction.FastExpand)]
    [InlineData(TitlePresentedPosture.Expanded, TitleInteractionAction.ResumeGroup)]
    public void Click_on_release_expands_only_and_expanded_click_does_not_submit_again(
        TitlePresentedPosture posture, TitleInteractionAction expected)
    {
        var result = Release(Start(Press(posture)), 2, 1);
        Assert.Equal(expected, result.Action);
        Assert.Equal(TitleInteractionPhase.Released, result.State!.Phase);
        Assert.False(result.State.HasPointerCapture); Assert.Null(result.State.Rearm);
    }

    [Theory]
    [InlineData(TitlePresentedPosture.Compact, TitleInteractionPhase.CompactDrag, TitleInteractionAction.BeginCompactDrag)]
    [InlineData(TitlePresentedPosture.Expanding, TitleInteractionPhase.CompactDrag, TitleInteractionAction.BeginCompactDrag)]
    [InlineData(TitlePresentedPosture.Collapsing, TitleInteractionPhase.CompactDrag, TitleInteractionAction.BeginCompactDrag)]
    [InlineData(TitlePresentedPosture.Expanded, TitleInteractionPhase.ExpandedDrag, TitleInteractionAction.BeginExpandedDrag)]
    public void Threshold_immediately_selects_drag_from_press_phase(
        TitlePresentedPosture posture, TitleInteractionPhase phase, TitleInteractionAction action)
    {
        var initial = Start(Press(posture));
        var step = Move(initial, 3, 4);
        Assert.Equal(action, step.Action); Assert.Equal(phase, step.State!.Phase);
        Assert.Equal(new TitlePointerDelta(3, 4), step.State.Delta);
        Assert.Equal(initial.GrabOffset, step.State.GrabOffset);
        Assert.Equal(initial.Press, step.State.Press); Assert.True(step.State.HasPointerCapture);
    }

    [Theory]
    [InlineData(0, 0, false)] [InlineData(4, 0, false)] [InlineData(3, 3, false)]
    [InlineData(3, 4, true)] [InlineData(0, -5, true)] [InlineData(-5, 0, true)]
    public void Threshold_matches_five_physical_pixel_euclidean_distance(int x, int y, bool drag)
    {
        var step = Move(Start(), x, y);
        Assert.Equal(drag ? TitleInteractionPhase.CompactDrag : TitleInteractionPhase.Pending, step.State!.Phase);
        Assert.Equal(drag ? TitleInteractionAction.BeginCompactDrag : TitleInteractionAction.None, step.Action);
    }

    [Fact]
    public void Large_pointer_delta_does_not_overflow_or_wrap_into_click()
    {
        var state = Start();
        var result = TitleInteractionModel.Advance(state, Input(state, TitlePointerEventKind.Move) with
            { Position = new(int.MinValue, int.MaxValue) });
        Assert.Equal(TitleInteractionAction.BeginCompactDrag, result.Action);
        Assert.Equal((long)int.MinValue - 120, result.State!.Delta.X);
    }

    [Theory]
    [InlineData(TitlePresentedPosture.Compact)]
    [InlineData(TitlePresentedPosture.Expanding)]
    [InlineData(TitlePresentedPosture.Expanded)]
    public void Locked_click_still_expands_but_crossing_threshold_never_becomes_click(TitlePresentedPosture posture)
    {
        var press = Press(posture) with { IsLocked = true };
        var clicked = Release(Start(press));
        Assert.Equal(posture == TitlePresentedPosture.Expanded ? TitleInteractionAction.ResumeGroup :
            TitleInteractionAction.FastExpand, clicked.Action);
        var moved = Move(Start(press), 6).State!;
        Assert.Equal(TitleInteractionPhase.BlockedDrag, moved.Phase);
        moved = Move(moved, 0).State!;
        var released = Release(moved);
        Assert.Equal(TitleInteractionAction.ResumeGroup, released.Action);
        Assert.False(released.State!.HasPointerCapture);
    }

    [Theory]
    [InlineData(PosterInputTargetKind.MoreButton)]
    [InlineData(PosterInputTargetKind.CollapseButton)]
    [InlineData(PosterInputTargetKind.Item)]
    [InlineData(PosterInputTargetKind.ItemScroll)]
    [InlineData(PosterInputTargetKind.ResizeLeft)]
    [InlineData(PosterInputTargetKind.EditSurface)]
    public void Other_regions_are_not_consumed_by_title_arbitration(PosterInputTargetKind kind)
    {
        var p = Press(); p = p with { Capture = p.Capture with
            { Press = p.Capture.Press with { Region = p.Capture.Press.Region with { Kind = kind } } } };
        var result = TitleInteractionModel.Begin(null, p);
        Assert.Null(result.State); Assert.False(result.Consumed); Assert.True(result.Ignored);
    }

    [Theory]
    [InlineData("off")] [InlineData("editing")] [InlineData("unready")]
    public void Ineligible_press_leaves_existing_manual_path_untouched(string mode)
    {
        var p = Press() with { AutomaticEnabled = mode != "off", IsEditing = mode == "editing", IsReady = mode != "unready" };
        var result = TitleInteractionModel.Begin(null, p);
        Assert.False(result.Consumed); Assert.Null(result.State); Assert.Equal(TitleInteractionAction.None, result.Action);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 250)] [InlineData(true, 6)]
    public void Invalid_release_or_missing_move_event_cannot_emit_click(bool canInvoke, int dx)
    {
        var result = Release(Start(), dx, invoke: canInvoke);
        Assert.Equal(TitleInteractionAction.CancelGroup, result.Action);
        Assert.False(result.State!.HasPointerCapture); Assert.True(result.Consumed);
    }

    [Fact]
    public void Compact_release_carries_final_position_and_release_time_exit_barrier()
    {
        var state = Move(Start(), 8).State!;
        var result = TitleInteractionModel.Advance(state, Input(state, TitlePointerEventKind.Release, 19) with
            { PointerEvidence = new(AutoPosturePresence.Outside, 8, 7), CanInvoke = false });
        Assert.Equal(TitleInteractionAction.FinishCompactDrag, result.Action);
        Assert.Equal(new TitlePointerDelta(19, 0), result.State!.Delta);
        Assert.Equal(7, result.State.Rearm!.ExitVersion);
        Assert.False(result.State.HasPointerCapture);
        Assert.Equal(TitlePresentedPosture.Expanding, result.State.Press.Posture);
    }

    [Fact]
    public void Expanded_release_finishes_drag_without_collapsing_or_suppression()
    {
        var state = Move(Start(Press(TitlePresentedPosture.Expanded)), 9).State!;
        var result = Release(state, 10, invoke: false);
        Assert.Equal(TitleInteractionAction.FinishExpandedDrag, result.Action);
        Assert.Null(result.State!.Rearm); Assert.False(result.State.HasPointerCapture);
    }

    [Theory]
    [InlineData("missing")] [InlineData("negative")] [InlineData("stale")]
    public void Missing_or_stale_release_evidence_cancels_instead_of_leaving_capture_stuck(string kind)
    {
        var s = Move(Start(), 9).State!;
        AutoPosturePointer? pointer = kind switch
        {
            "missing" => null, "negative" => new(AutoPosturePresence.Unknown, -1, 1),
            _ => new(AutoPosturePresence.ExpansionZone, 1, 0)
        };
        var result = TitleInteractionModel.Advance(s, Input(s, TitlePointerEventKind.Release, 10) with { PointerEvidence = pointer });
        Assert.Equal(TitleInteractionAction.CancelGroup, result.Action);
        Assert.False(result.State!.HasPointerCapture); Assert.Null(result.State.Rearm);
    }

    [Fact]
    public void Unknown_presence_still_retains_valid_real_exit_counter()
    {
        var s = Move(Start(), 9).State!;
        var result = TitleInteractionModel.Advance(s, Input(s, TitlePointerEventKind.Release, 10) with
            { PointerEvidence = new(AutoPosturePresence.Unknown, 2, 1) });
        Assert.Equal(TitleInteractionAction.FinishCompactDrag, result.Action);
        Assert.Equal(1, result.State!.Rearm!.ExitVersion);
    }

    [Theory]
    [InlineData("operation")] [InlineData("session")] [InlineData("environment")] [InlineData("capture")]
    public void Foreign_events_do_not_take_ownership_or_advance_sequence(string field)
    {
        var s = Start();
        var token = field switch
        {
            "operation" => s.Token with { OperationId = "other" },
            "session" => s.Token with { Session = new("A", 2) },
            "environment" => s.Token with { EnvironmentEpoch = 2 },
            _ => s.Token with { CaptureId = Guid.Empty }
        };
        var result = TitleInteractionModel.Advance(s, Input(s, TitlePointerEventKind.Release) with { Token = token });
        Assert.Same(s, result.State); Assert.True(result.Ignored); Assert.False(result.Consumed);
    }

    [Theory]
    [InlineData("sequence")] [InlineData("epoch")] [InlineData("capture")] [InlineData("phase")]
    [InlineData("outside")] [InlineData("region")]
    public void Invalid_press_fails_before_pause_or_ownership(string field)
    {
        var p = Press();
        p = field switch
        {
            "sequence" => p with { Sequence = 0 }, "epoch" => p with { EnvironmentEpoch = 0 },
            "capture" => p with { Capture = p.Capture with { Id = Guid.Empty } },
            "phase" => p with { Posture = (TitlePresentedPosture)55 },
            "outside" => p with { Position = new(0, 0) },
            _ => p with { Capture = p.Capture with { Press = p.Capture.Press with
                { Region = p.Capture.Press.Region with { Bounds = new(0, 0, 1000, 20) } } } }
        };
        Assert.Throws<ArgumentException>(() => TitleInteractionModel.Begin(null, p));
    }
}
