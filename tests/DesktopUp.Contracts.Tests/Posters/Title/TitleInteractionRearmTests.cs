using DesktopUp.Application;
using static DesktopUp.Contracts.Tests.TitleTestData;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleInteractionRearmTests
{
    private static TitleAutoRearmSuppression CompactRelease()
    {
        var s = Move(Start(), 7).State!;
        return Release(s, 7).State!.Rearm!;
    }

    [Fact]
    public void Compact_release_reuses_existing_auto_model_and_requires_real_exit_then_entry()
    {
        var barrier = CompactRelease();
        var observation = new AutoPostureObservation(barrier.Session, 1, 1, new(true), true, true, true,
            new(AutoPosturePresence.ExpansionZone, 2, 1), ManualIntentVersion: 1);
        var state = AutoPostureState.Empty;
        AutoPostureStep Observe(int sequence, AutoPosturePointer pointer, long space = 0)
        {
            observation = observation with { Pointer = pointer, SpaceVersion = space,
                ManualCollapseExitVersion = barrier.ForObservation(observation.Session, observation.EnvironmentEpoch, pointer) };
            var next = AutoPostureModel.Advance(state, new(sequence, sequence * 1000L, [observation]));
            state = next.State; return next;
        }
        Assert.Null(Observe(1, observation.Pointer).Request);
        Assert.Equal(AutoPosturePhase.ManualSuppressed, state.Posters["A"].Phase);
        // Bounds/occlusion/space and unknown readings are not real exit evidence.
        Assert.Null(Observe(2, new(AutoPosturePresence.Outside, 2, 1), 1).Request);
        Assert.Null(Observe(3, new(AutoPosturePresence.Unknown, 2, 1), 2).Request);
        Assert.Null(Observe(4, new(AutoPosturePresence.ExpansionZone, 2, 1), 3).Request);
        Assert.Null(Observe(5, new(AutoPosturePresence.ExpansionZone, 3, 1), 3).Request);
        Assert.Null(Observe(6, new(AutoPosturePresence.Outside, 3, 2), 3).Request);
        var request = Assert.IsType<AutoPostureRequest>(Observe(7, new(AutoPosturePresence.ExpansionZone, 4, 2), 3).Request);
        Assert.False(request.Collapse);
        Assert.Null(Observe(8, new(AutoPosturePresence.ExpansionZone, 4, 2), 3).Request);
    }

    [Fact]
    public void Exit_during_drag_does_not_arm_hover_after_release_inside()
    {
        var s = Move(Start(), 7).State!;
        var ended = TitleInteractionModel.Advance(s, Input(s, TitlePointerEventKind.Release, 9) with
            { PointerEvidence = new(AutoPosturePresence.ExpansionZone, 9, 8) }).State!;
        var barrier = ended.Rearm!;
        Assert.Equal(8, barrier.ForObservation(s.Token.Session, 1, new(AutoPosturePresence.ExpansionZone, 9, 8)));
        Assert.Null(barrier.ForObservation(s.Token.Session, 1, new(AutoPosturePresence.Outside, 9, 9)));
    }

    [Fact]
    public void Barrier_never_leaks_to_another_poster_session_or_environment()
    {
        var barrier = CompactRelease(); var pointer = new AutoPosturePointer(AutoPosturePresence.ExpansionZone, 2, 1);
        Assert.Null(barrier.ForObservation(new("B", 1), 1, pointer));
        Assert.Null(barrier.ForObservation(new("A", 2), 1, pointer));
        Assert.Null(barrier.ForObservation(new("A", 1), 2, pointer));
        Assert.Equal(1, barrier.ForObservation(new("A", 1), 1, pointer));
    }

    [Fact]
    public void Explicit_click_can_fast_expand_while_hover_remains_suppressed()
    {
        var s = Move(Start(), 7).State!; var end = Release(s, 7).State!;
        Assert.NotNull(end.Rearm);
        var p = Press(TitlePresentedPosture.Compact) with { OperationId = "click-after-drag", Sequence = end.Sequence + 1,
            Capture = Press().Capture with { Id = Guid.Parse("00000000-0000-0000-0000-000000000099") } };
        var click = TitleInteractionModel.Begin(end, p);
        Assert.Equal(TitleInteractionAction.PauseGroup, click.Action);
        var release = Release(click.State!);
        Assert.Equal(TitleInteractionAction.FastExpand, release.Action);
        Assert.Null(release.State!.Rearm);
        // The barrier is evidence for the auto scheduler, never a veto on explicit click intent.
    }
}
