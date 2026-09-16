using DesktopUp.Application;
using System.Text.Json;
using static DesktopUp.Contracts.Tests.TitleTestData;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleInteractionSequenceTests
{
    [Fact]
    public void Holding_and_micro_jitter_never_create_a_long_press_timer_or_automatic_action()
    {
        var s = Start(); var press = s.Press;
        for (int i = 0; i < 10000; i++)
        {
            var step = TitleInteractionModel.Advance(s, Input(s, TitlePointerEventKind.Move, i % 3, i % 2) with
                { Sequence = s.Sequence + 1000 });
            Assert.Equal(TitleInteractionAction.None, step.Action);
            Assert.Equal(TitleInteractionPhase.Pending, step.State!.Phase);
            Assert.Equal(press, step.State.Press); s = step.State;
        }
        Assert.Equal(TitleInteractionAction.FastExpand, Release(s).Action);
    }

    [Theory]
    [InlineData(TitlePresentedPosture.Expanding, 61)]
    [InlineData(TitlePresentedPosture.Expanding, 180)]
    [InlineData(TitlePresentedPosture.Expanding, 359)]
    [InlineData(TitlePresentedPosture.Collapsing, 61)]
    [InlineData(TitlePresentedPosture.Collapsing, 180)]
    [InlineData(TitlePresentedPosture.Collapsing, 359)]
    public void All_partial_stages_immediately_intend_compact_drag_then_compact_finish(TitlePresentedPosture posture, int height)
    {
        var p = Press(posture); p = p with { Capture = p.Capture with
            { Press = p.Capture.Press with { Frame = p.Capture.Press.Frame with { Height = height } } } };
        var s = Start(p);
        var move = Move(s, 5); Assert.Equal(TitleInteractionAction.BeginCompactDrag, move.Action);
        s = move.State!;
        Assert.Equal(height, s.Press.Capture.Press.Frame.Height); // No pretend geometry shrink.
        var follow = Move(s, 8, 3); Assert.Equal(TitleInteractionAction.MoveDrag, follow.Action);
        var release = Release(follow.State!, 8, 3);
        Assert.Equal(TitleInteractionAction.FinishCompactDrag, release.Action);
        var late = TitleInteractionModel.Advance(release.State!, Input(release.State!, TitlePointerEventKind.LegacyInvoke));
        Assert.True(late.Ignored); Assert.True(late.Consumed); Assert.Equal(TitleInteractionAction.None, late.Action);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Last_presented_frame_before_down_decides_classification_and_later_down_cannot_change_it(bool finishedBeforeDown)
    {
        var p = Press(finishedBeforeDown ? TitlePresentedPosture.Expanded : TitlePresentedPosture.Expanding);
        var s = Start(p);
        var later = TitleInteractionModel.Begin(s, p with { Sequence = 2, Posture = TitlePresentedPosture.Expanded });
        Assert.True(later.Ignored); Assert.True(later.Consumed); Assert.Same(s, later.State);
        var move = Move(s, 5);
        Assert.Equal(finishedBeforeDown ? TitleInteractionPhase.ExpandedDrag : TitleInteractionPhase.CompactDrag, move.State!.Phase);
    }

    [Fact]
    public void Constrained_expanded_endpoint_is_not_misclassified_as_still_expanding()
    {
        var p = Press(TitlePresentedPosture.Expanded); p = p with { Capture = p.Capture with
            { Press = p.Capture.Press with { Frame = p.Capture.Press.Frame with { Height = 220 } } } };
        var step = Move(Start(p), 5);
        Assert.Equal(TitleInteractionAction.BeginExpandedDrag, step.Action);
        Assert.Equal(220, step.State!.Press.Capture.Press.Frame.Height);
    }

    [Fact]
    public void Active_owner_rejects_a_second_gesture_even_on_the_same_poster()
    {
        var s = Start(); var p = Press() with { OperationId = "title-2", Sequence = 2,
            Capture = Press().Capture with { Id = Guid.Parse("00000000-0000-0000-0000-000000000002") } };
        var second = TitleInteractionModel.Begin(s, p);
        Assert.Same(s, second.State); Assert.False(second.Consumed); Assert.Equal(TitleInteractionAction.None, second.Action);
        s = Release(s).State!;
        second = TitleInteractionModel.Begin(s, p with { Sequence = 3 });
        Assert.Equal(TitleInteractionAction.PauseGroup, second.Action);
    }

    [Fact]
    public void Ended_gesture_rejects_old_press_and_owned_duplicate_release_without_fallthrough()
    {
        var s = Start(); var release = Input(s, TitlePointerEventKind.Release);
        var done = TitleInteractionModel.Advance(s, release).State!;
        Assert.True(TitleInteractionModel.Begin(done, s.Press with { Sequence = 100 }).Ignored);
        foreach (var kind in new[] { TitlePointerEventKind.Release, TitlePointerEventKind.LegacyInvoke, TitlePointerEventKind.Move })
        {
            var duplicate = TitleInteractionModel.Advance(done, release with { Sequence = 100, Kind = kind });
            Assert.Same(done, duplicate.State); Assert.True(duplicate.Consumed); Assert.True(duplicate.Ignored);
            Assert.Equal(TitleInteractionAction.None, duplicate.Action);
        }
    }

    [Fact]
    public void Returning_to_press_position_after_drag_never_reclassifies_as_click()
    {
        var s = Move(Start(), 6).State!;
        s = Move(s, 0).State!;
        var step = Release(s);
        Assert.Equal(TitleInteractionAction.FinishCompactDrag, step.Action);
        Assert.Equal(new TitlePointerDelta(0, 0), step.State!.Delta);
    }

    public static TheoryData<TitleInvalidation, TitleInteractionPhase> Invalidations
    {
        get
        {
            var data = new TheoryData<TitleInvalidation, TitleInteractionPhase>();
            foreach (var reason in Enum.GetValues<TitleInvalidation>())
            foreach (var phase in new[] { TitleInteractionPhase.Pending, TitleInteractionPhase.CompactDrag,
                TitleInteractionPhase.ExpandedDrag, TitleInteractionPhase.BlockedDrag }) data.Add(reason, phase);
            return data;
        }
    }

    [Theory] [MemberData(nameof(Invalidations))]
    public void Every_active_phase_cancels_once_and_late_callbacks_never_resume(
        TitleInvalidation reason, TitleInteractionPhase phase)
    {
        var p = Press(phase == TitleInteractionPhase.ExpandedDrag ? TitlePresentedPosture.Expanded : TitlePresentedPosture.Expanding)
            with { IsLocked = phase == TitleInteractionPhase.BlockedDrag };
        var s = Start(p);
        if (phase != TitleInteractionPhase.Pending) s = Move(s, 7).State!;
        Assert.Equal(phase, s.Phase);
        var input = Input(s, TitlePointerEventKind.Invalidate) with { Reason = reason };
        var cancelled = TitleInteractionModel.Advance(s, input);
        Assert.Equal(TitleInteractionAction.CancelGroup, cancelled.Action);
        Assert.False(cancelled.State!.HasPointerCapture); Assert.Null(cancelled.State.Rearm);
        var late = TitleInteractionModel.Advance(cancelled.State, input with { Sequence = input.Sequence + 1, Kind = TitlePointerEventKind.Release });
        Assert.True(late.Ignored); Assert.Equal(TitleInteractionAction.None, late.Action);
    }

    [Theory]
    [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(192)]
    public void Reversed_negative_origin_intent_paths_are_deterministic_and_do_not_accumulate_rounding(int dpi)
    {
        var p = Press(); p = p with { Position = new(-1800, -682), Capture = p.Capture with
            { Press = p.Capture.Press with { Frame = new(-1820, -700, 300 * dpi / 96, 360 * dpi / 96) } } };
        var first = Start(p); var second = Start(p);
        for (int i = 0; i < 1000; i++)
        {
            int dx = (i % 2 == 0 ? 11 : -11) * dpi / 96, dy = i % 5;
            var input = Input(first, TitlePointerEventKind.Move, dx, dy);
            var a = TitleInteractionModel.Advance(first, input); var b = TitleInteractionModel.Advance(second, input);
            Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
            Assert.Equal(new TitlePointerDelta(dx, dy), a.State!.Delta);
            Assert.Equal(new TitlePointerDelta(20, 18), a.State.GrabOffset);
            first = a.State; second = b.State!;
        }
        first = Move(first, 0, 0).State!;
        Assert.Equal(new TitlePointerDelta(0, 0), first.Delta);
        Assert.Equal(p.Capture.Press.Frame, first.Press.Capture.Press.Frame);
        Assert.Equal(TitleInteractionAction.FinishCompactDrag, Release(first).Action);
    }

    [Fact]
    public void Seeded_mixed_sequences_stay_bounded_and_end_once_without_spatial_mutation()
    {
        var random = new Random(90114);
        for (int trial = 0; trial < 200; trial++)
        {
            var p = Press((TitlePresentedPosture)(trial % 4));
            var s = Start(p); var firstFrame = s.Press.Capture.Press.Frame;
            int starts = 0, finishes = 0;
            for (int i = 0; i < 100; i++)
            {
                var step = Move(s, random.Next(-12, 13), random.Next(-12, 13));
                if (step.Action is TitleInteractionAction.BeginCompactDrag or TitleInteractionAction.BeginExpandedDrag) starts++;
                Assert.InRange(starts, 0, 1); Assert.Equal(firstFrame, step.State!.Press.Capture.Press.Frame); s = step.State;
            }
            var input = Input(s, TitlePointerEventKind.Release, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                var step = TitleInteractionModel.Advance(s, input with { Sequence = s.Sequence + 1 });
                if (step.Action is TitleInteractionAction.FinishCompactDrag or TitleInteractionAction.FinishExpandedDrag) finishes++;
                s = step.State!;
            }
            Assert.Equal(1, starts); Assert.Equal(1, finishes); Assert.False(s.HasPointerCapture);
        }
    }

    [Fact]
    public void Title_intents_do_not_grant_a_database_or_spatial_commit_capability()
    {
        Assert.DoesNotContain(typeof(StateProjectionCoordinator).GetMethods(), m => m.Name == "ExecuteExpansionDrag");
        Assert.DoesNotContain(typeof(StateProjectionCoordinator).Assembly.GetExportedTypes(),
            t => t.Name.StartsWith("ExpansionDrag", StringComparison.Ordinal) || t.Name == "IExpansionDragCommitter");
        // The new arbiter has no StartAutomatic/SetCollapsed/commit method or grow event.
        Assert.DoesNotContain(Enum.GetNames<TitleInteractionAction>(), n => n.Contains("Grow", StringComparison.Ordinal));
        Assert.Equal(TitleInteractionPhase.Released, Release(Start()).State!.Phase);
    }
}
