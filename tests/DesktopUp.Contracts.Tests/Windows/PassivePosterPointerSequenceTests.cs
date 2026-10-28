using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    [Fact]
    public void Real_exit_and_entry_count_without_any_routed_event()
    {
        var f = new Fixture(); int wakes = 0; f.Router.PointerActivity += () => wakes++;
        Assert.Equal(new AutoPosturePointer(AutoPosturePresence.ExpansionZone), f.Pointer());
        f.Native.Point = new(-350, 124);
        Assert.Equal(1, f.Pointer().ExitVersion);
        f.Native.Point = new(-250, 124);
        var pointer = f.Pointer();
        Assert.Equal(1, pointer.EnterVersion); Assert.Equal(1, pointer.ExitVersion);
        Assert.Equal(0, wakes); // a passive fallback is required, not just input-region events
    }

    [Fact]
    public void Route_wake_is_versioned_and_never_manufactures_entry()
    {
        var f = new Fixture(); int wakes = 0; Action handler = () => wakes++;
        f.Router.PointerActivity += handler;
        f.Router.NotifyPointerActivity(A, 1);
        f.Router.NotifyPointerActivity(A with { Epoch = 99 }, 1);
        f.Router.NotifyPointerActivity(A, 99);
        Assert.Equal(1, wakes);
        Assert.Equal(0, f.Pointer().EnterVersion);
        f.Router.Suspend(); f.Router.NotifyPointerActivity(A, 1); Assert.Equal(1, wakes);
        f.Router.PointerActivity -= handler;
        f.Router.Resume(); f.Router.NotifyPointerActivity(A, 1); Assert.Equal(1, wakes);
    }

    [Fact]
    public void Observer_wake_failure_cannot_escape_into_native_input_or_steal_capture()
    {
        var f = new Fixture();
        var capture = f.Router.Begin(f.Router.Resolve(-250, 124, [A])!);
        f.Router.PointerActivity += () => throw new InvalidOperationException("injected-observer-fault");
        Assert.IsType<InvalidOperationException>(f.Router.NotifyPointerActivity(A, 1));
        Assert.Same(capture, f.Router.Capture);
        Assert.True(f.Router.IsCurrent(A, 1));
    }

    [Fact]
    public void Stationary_cursor_geometry_crossings_never_create_boundary_evidence()
    {
        var f = new Fixture(); f.Pointer();
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(f.Router.Publish(A, Bounds with { Y = i % 2 == 0 ? 500 : 100 }, Layout(i + 2)));
            var pointer = f.Pointer();
            Assert.Equal(0, pointer.EnterVersion); Assert.Equal(0, pointer.ExitVersion);
        }
    }

    [Fact]
    public void Simultaneous_pointer_and_layout_change_does_not_guess_user_intent()
    {
        var f = new Fixture(); f.Native.Point = new(-350, 124); f.Pointer();
        Assert.True(f.Router.Publish(A, Bounds with { X = -400 }, Layout(2)));
        f.Native.Point = new(-349, 124);
        Assert.Equal(0, f.Pointer().EnterVersion);
    }

    [Fact]
    public void Occlusion_removal_and_unknown_recovery_are_not_entry_or_exit()
    {
        var f = new Fixture(); f.Pointer();
        f.Native.Receiver = 99; Assert.Equal(0, f.Pointer().ExitVersion);
        f.Native.Receiver = 11; Assert.Equal(0, f.Pointer().EnterVersion);
        f.Native.CursorOk = false; f.Pointer();
        f.Native.CursorOk = true; f.Native.Point = new(-249, 125);
        Assert.Equal(0, f.Pointer().EnterVersion);
        Assert.Equal(0, f.Sample(epoch: 2).Posters[0].Pointer.EnterVersion);
    }

    [Fact]
    public void Move_from_actions_into_title_is_real_expansion_zone_entry()
    {
        var f = new Fixture(); f.Native.Point = new(-20, 124); f.Pointer();
        f.Native.Point = new(-250, 124);
        Assert.Equal(1, f.Pointer().EnterVersion);
    }

    [Fact]
    public void Delayed_sample_and_replaced_session_cannot_restore_old_evidence()
    {
        var f = new Fixture(); f.Pointer(); var oldTargets = f.Targets.ToArray();
        f.Native.Point = new(-350, 124); f.Pointer();
        var current = f.Observer.Current;
        Assert.Same(current, f.Observer.Sample(1, 1, oldTargets));
        var newKey = A with { Epoch = 2 };
        f.Router.Register(newKey); Assert.True(f.Router.Publish(newKey, Bounds, Layout()));
        f.Targets[0] = f.Targets[0] with { Session = newKey };
        Assert.Equal(0, f.Pointer().ExitVersion);
        f.Targets.Clear(); Assert.Empty(f.Sample().Posters);
    }

    [Fact]
    public void Tracker_rejects_regressed_environment_and_duplicate_ids_without_partial_application()
    {
        var tracker = new PosterPointerTracker();
        var reading = new PosterPointerReading(A, new(A, Bounds, Layout()), AutoPosturePresence.Inside);
        var first = tracker.Observe(1, 2, new(-250, 124), [reading], AutoPostureProtection.None);
        Assert.Same(first, tracker.Observe(2, 1, null, [], AutoPostureProtection.None));
        Assert.Throws<ArgumentException>(() => tracker.Observe(2, 2, null, [reading, reading], AutoPostureProtection.None));
        Assert.Same(first, tracker.Current);
    }

    [Fact]
    public void Previous_geometry_is_detached_from_mutable_caller_arrays()
    {
        var tracker = new PosterPointerTracker();
        var regions = new[] { new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, new(0, 0, 300, 48)) };
        var layout = Layout() with { Regions = regions };
        var first = new PosterPointerReading(A, new(A, Bounds, layout), AutoPosturePresence.Outside);
        tracker.Observe(1, 1, new(-350, 124), [first], AutoPostureProtection.None);
        regions[0] = regions[0] with { Kind = PosterInputTargetKind.MoreButton };
        var restored = layout with { Regions = new[] { regions[0] with { Kind = PosterInputTargetKind.TitleBar } } };
        var result = tracker.Observe(2, 1, new(-250, 124),
            [new(A, new(A, Bounds, restored), AutoPosturePresence.ExpansionZone)], AutoPostureProtection.None);
        Assert.Equal(1, result.Posters[0].Pointer.EnterVersion);
    }

    [Fact]
    public void Manual_collapse_waits_for_real_leave_and_reentry_through_native_adapter()
    {
        var f = new Fixture(); f.Targets[0] = f.Targets[0] with { Collapsed = false };
        var state = AutoPostureState.Empty;
        AutoPostureStep Feed(bool collapsed, long manual)
        {
            f.Targets[0] = f.Targets[0] with { Collapsed = collapsed };
            var snapshot = f.Sample(); var o = snapshot.Posters[0];
            var step = AutoPostureModel.Advance(state, new(snapshot.Sequence, snapshot.Sequence * 10,
                [new(A, 1, 1, new(true), collapsed, true, true, o.Pointer, manual, Protection: o.Protection)],
                SharedProtection: snapshot.SharedProtection));
            state = step.State; return step;
        }
        Assert.Null(Feed(false, 0).Request);
        Assert.Null(Feed(true, 1).Request);
        Assert.Equal(AutoPosturePhase.ManualSuppressed, state.Posters["A"].Phase);
        Assert.Null(Feed(true, 1).Request);
        // External covering/uncovering is not a genuine exit.
        f.Native.Receiver = 99; Assert.Null(Feed(true, 1).Request);
        f.Native.Receiver = 11; Assert.Null(Feed(true, 1).Request);
        f.Native.Point = new(-350, 124); Assert.Null(Feed(true, 1).Request);
        f.Native.Point = new(-250, 124);
        Assert.False(Feed(true, 1).Request!.Collapse);
    }

    [Fact]
    public void Shared_protection_resets_deadline_and_does_not_replay_hover()
    {
        var f = new Fixture(); f.Native.Point = new(-350, 124);
        var state = AutoPostureState.Empty;
        AutoPostureStep Feed(long ms, AutoPostureProtection protection)
        {
            var snapshot = f.Sample(protection); var o = snapshot.Posters[0];
            var step = AutoPostureModel.Advance(state, new(snapshot.Sequence, ms,
                [new(A, 1, 1, new(true, 1), false, true, true, o.Pointer)], SharedProtection: snapshot.SharedProtection));
            state = step.State; return step;
        }
        Feed(0, AutoPostureProtection.None);
        Assert.Null(Feed(999, AutoPostureProtection.Confirmation).Request);
        Assert.Null(Feed(2000, AutoPostureProtection.None).Request);
        Assert.Equal(3000, state.Posters["A"].DeadlineMs);
        Assert.True(Feed(3000, AutoPostureProtection.None).Request!.Collapse);
    }
}
