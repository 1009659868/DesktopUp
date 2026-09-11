using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSchedulerTests
{
    private static AutoPostureObservation Node(string id = "A", bool collapsed = false, int delay = 1) =>
        new(new(id, 1), 1, 1, new(true, delay), collapsed, true, true, new(AutoPosturePresence.Outside));

    private sealed class Rig : IDisposable
    {
        public long Now;
        public AutoPostureSample Sample = new([Node()], false);
        public readonly Queue<Action> Queue = new();
        public readonly List<AutoPostureRequest> Requests = [];
        public readonly List<AutoPostureRequest> Cancellations = [];
        public readonly Dictionary<long, Action<AutoPostureOutcome>> Done = [];
        public int? Timer;
        public AutoPostureScheduler Scheduler;
        public Rig() => Scheduler = new(() => Now, () => Sample, a => { Queue.Enqueue(a); return true; },
            ms => Timer = ms, (r, done) => { Requests.Add(r); Done[r.Id] = done; }, r => Cancellations.Add(r));
        public void Drain()
        {
            int limit = 100;
            while (Queue.TryDequeue(out var action)) { Assert.True(--limit > 0, "Unbounded dispatcher feedback"); action(); }
        }
        public void Tick(long now) { Now = now; Scheduler.Observe(); Drain(); }
        public void Change(Func<AutoPostureObservation, AutoPostureObservation> change) =>
            Sample = Sample with { Posters = Sample.Posters.Select(change).ToArray() };
        public void Dispose() => Scheduler.Dispose();
    }

    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(300)]
    public void Deadline_is_monotonic_shared_and_revalidated_before_effect(int seconds)
    {
        using var f = new Rig(); f.Change(n => n with { Policy = new(true, seconds) });
        f.Tick(0); Assert.Equal(120, f.Timer);
        f.Tick(seconds * 1000 - 1); Assert.Empty(f.Requests); Assert.Equal(1, f.Timer);
        f.Now++; f.Scheduler.Observe(); Assert.Empty(f.Requests);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Inside, 1) });
        f.Drain(); Assert.Empty(f.Requests);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Outside, 1, 1) });
        f.Tick(f.Now); f.Tick(f.Now + seconds * 1000);
        Assert.True(Assert.Single(f.Requests).Collapse);
    }

    [Fact]
    public void Native_callbacks_capture_exit_entry_without_running_effects_or_queuing_histories()
    {
        using var f = new Rig(); f.Change(n => n with { IsCollapsed = true }); f.Tick(0);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) });
        f.Scheduler.Observe(); Assert.Empty(f.Requests); Assert.Single(f.Queue);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Outside, 1, 1) }); f.Scheduler.Observe();
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 2, 1) }); f.Scheduler.Observe();
        Assert.Single(f.Queue); f.Drain(); Assert.False(Assert.Single(f.Requests).Collapse);
    }

    [Fact]
    public void Busy_defers_fresh_candidates_and_orders_equal_deadlines_by_identity()
    {
        using var f = new Rig(); f.Sample = new([Node("C"), Node("B"), Node("A")], true);
        f.Tick(0); f.Tick(1000); Assert.Empty(f.Requests);
        f.Sample = f.Sample with { SpatialBusy = false }; f.Tick(2000);
        Assert.Equal("A", Assert.Single(f.Requests).Session.PosterId);
        f.Change(n => n.Session.PosterId == "A" ? n with { IsCollapsed = true, EntityRevision = 2 } : n);
        f.Done[1](AutoPostureOutcome.Committed); Assert.Single(f.Requests); f.Drain();
        Assert.Equal("B", f.Requests[1].Session.PosterId); Assert.Equal(2, f.Requests.Count);
    }

    [Fact]
    public void Busy_candidate_reentry_discards_deadline()
    {
        using var f = new Rig(); f.Sample = f.Sample with { SpatialBusy = true };
        f.Tick(0); f.Tick(1000); f.Change(n => n with { Pointer = new(AutoPosturePresence.Inside, 1) });
        f.Sample = f.Sample with { SpatialBusy = false }; f.Tick(2000); Assert.Empty(f.Requests);
    }

    [Theory]
    [InlineData(AutoPostureProtection.Menu)] [InlineData(AutoPostureProtection.Confirmation)]
    [InlineData(AutoPostureProtection.Editing)] [InlineData(AutoPostureProtection.DragDrop)]
    [InlineData(AutoPostureProtection.Gesture)]
    [InlineData(AutoPostureProtection.Recovery)]
    public void Semantic_protection_cancels_once_and_keeps_single_flight_until_restore_ack(AutoPostureProtection protection)
    {
        using var f = new Rig(); f.Tick(0); f.Tick(1000);
        f.Sample = f.Sample with { Protection = protection }; f.Tick(1100);
        Assert.Single(f.Cancellations); f.Tick(1500); Assert.Single(f.Cancellations);
        f.Sample = f.Sample with { Protection = AutoPostureProtection.None }; f.Tick(2000); f.Tick(4000);
        Assert.Single(f.Requests);
        f.Done[1](AutoPostureOutcome.Cancelled); f.Drain(); Assert.Null(f.Scheduler.State.Outstanding);
        f.Tick(10000); Assert.Single(f.Requests);
    }

    [Fact]
    public void No_space_does_not_resolve_or_write_again_without_new_independent_evidence()
    {
        using var f = new Rig(); f.Change(n => n with { IsCollapsed = true }); f.Tick(0);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) }); f.Tick(1);
        f.Done[1](AutoPostureOutcome.InsufficientSpace); f.Drain();
        for (int i = 2; i < 1002; i++) f.Tick(i * 120);
        Assert.Single(f.Requests);
        f.Change(n => n with { SpaceVersion = 1 }); f.Tick(130000);
        Assert.Equal(2, f.Requests.Count);
    }

    [Theory] [InlineData("disabled")] [InlineData("hidden")] [InlineData("unready")] [InlineData("removed")]
    public void Zero_demand_stops_timer_and_stale_completion_cannot_restart_it(string kind)
    {
        using var f = new Rig(); f.Tick(0); f.Tick(1000);
        f.Change(n => kind switch {
            "disabled" => n with { Policy = new(false, 1) }, "hidden" => n with { IsVisible = false },
            "unready" => n with { IsReady = false }, _ => n });
        if (kind == "removed") f.Sample = f.Sample with { Posters = [] };
        f.Tick(1100); Assert.Null(f.Timer); Assert.Single(f.Cancellations);
        f.Done[1](AutoPostureOutcome.Cancelled); f.Drain(); Assert.Null(f.Timer);
        f.Done[1](AutoPostureOutcome.Committed); f.Drain(); Assert.Null(f.Timer); Assert.Single(f.Requests);
    }

    [Fact]
    public void Entity_and_policy_are_read_at_dispatch_not_captured_by_native_wakeup()
    {
        using var f = new Rig(); f.Tick(0); f.Now = 1000; f.Scheduler.Observe();
        f.Change(n => n with { EntityRevision = 7 }); f.Drain();
        Assert.Equal(7, Assert.Single(f.Requests).ExpectedEntityRevision);
    }

    [Fact]
    public void Resume_restarts_delay_and_does_not_replay_initial_hover()
    {
        using var f = new Rig(); f.Tick(0); f.Change(n => n with { IsReady = false, EnvironmentEpoch = 2 }); f.Tick(900);
        f.Change(n => n with { IsReady = true }); f.Tick(10000); f.Tick(10999); Assert.Empty(f.Requests);
        f.Tick(11000); Assert.Single(f.Requests);
    }

    [Fact]
    public void Manual_collapse_inside_needs_actual_exit_then_entry()
    {
        using var f = new Rig(); f.Change(n => n with { Pointer = new(AutoPosturePresence.Inside) }); f.Tick(0);
        f.Change(n => n with { IsCollapsed = true, EntityRevision = 2, ManualIntentVersion = 1,
            Pointer = new(AutoPosturePresence.ExpansionZone) }); f.Tick(1); f.Tick(10000); Assert.Empty(f.Requests);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Outside) }); f.Tick(10001);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) }); f.Tick(10002); Assert.Empty(f.Requests);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Outside, 1, 1) }); f.Tick(10003);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 2, 1) }); f.Tick(10004);
        Assert.Single(f.Requests);
    }

    [Fact]
    public void Disposing_invalidates_queued_effect_and_cancels_live_effect_once()
    {
        using var f = new Rig(); f.Tick(0); f.Now = 1000; f.Scheduler.Observe();
        f.Dispose(); f.Drain(); Assert.Empty(f.Requests); Assert.Null(f.Timer);
        using var g = new Rig(); g.Tick(0); g.Tick(1000); g.Dispose(); g.Dispose();
        Assert.Single(g.Cancellations); g.Done[1](AutoPostureOutcome.Committed); g.Drain(); Assert.Null(g.Timer);
    }

    [Fact]
    public void Manual_collapse_that_moves_title_keeps_intent_time_exit_latch()
    {
        using var f = new Rig(); var evidence = new AutoPostureEvidence();
        f.Change(n => n with { Pointer = new(AutoPosturePresence.Inside) }); f.Tick(0);
        var session = f.Sample.Posters[0].Session;
        evidence.CaptureManualCollapse(session, f.Sample.Posters[0].Pointer, true);
        var outside = new AutoPosturePointer(AutoPosturePresence.Outside);
        f.Change(n => n with { IsCollapsed = true, EntityRevision = 2, ManualIntentVersion = 1, Pointer = outside,
            ManualCollapseExitVersion = evidence.ManualCollapseExit(session, outside) }); f.Tick(1);
        // Geometry moved the title away; a mere return to that title did not prove an exit.
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) }); f.Tick(2); Assert.Empty(f.Requests);
        var exit = new AutoPosturePointer(AutoPosturePresence.Outside, 1, 1);
        f.Change(n => n with { Pointer = exit, ManualCollapseExitVersion = evidence.ManualCollapseExit(session, exit) }); f.Tick(3);
        f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 2, 1) }); f.Tick(4); Assert.Single(f.Requests);
    }

    [Fact]
    public void Manual_latch_does_not_leak_across_replaced_sessions_recovery_or_explicit_expansion()
    {
        var evidence = new AutoPostureEvidence(); var session = new PosterInputSessionKey("A", 1);
        var inside = new AutoPosturePointer(AutoPosturePresence.Inside, 2, 3);
        evidence.CaptureManualCollapse(session, inside, true); Assert.Equal(3, evidence.ManualCollapseExit(session, inside));
        evidence.RetainSessions([new("A", 2)]); Assert.Null(evidence.ManualCollapseExit(session, inside));
        evidence.CaptureManualCollapse(session, inside, true); evidence.ResetPointerBaseline();
        Assert.Null(evidence.ManualCollapseExit(session, inside));
        evidence.CaptureManualCollapse(session, inside, true); evidence.CaptureManualCollapse(session, inside, false);
        Assert.Null(evidence.ManualCollapseExit(session, inside));
    }

    [Fact]
    public void Rejected_dispatch_stops_scheduler_without_native_effect()
    {
        int? timer = 1;
        using var scheduler = new AutoPostureScheduler(() => 0, () => new([Node()], false), _ => false,
            ms => timer = ms, (_, _) => Assert.Fail("Unexpected effect"), _ => Assert.Fail("Unexpected cancellation"));
        scheduler.Observe(); Assert.Null(timer); Assert.Empty(scheduler.State.Posters);
    }

    [Fact]
    public void Synchronous_no_space_and_failed_logging_do_not_reenter_or_lose_completion()
    {
        var queue = new Queue<Action>(); long now = 0; int requests = 0;
        using var scheduler = new AutoPostureScheduler(() => now, () => new([Node()], false),
            a => { queue.Enqueue(a); return true; }, _ => { },
            (_, done) => { requests++; done(AutoPostureOutcome.InsufficientSpace); }, _ => { },
            (_, _) => throw new IOException("log unavailable"));
        scheduler.Observe(); while (queue.TryDequeue(out var first)) first();
        now = 1000; scheduler.Observe();
        int remaining = 10; while (queue.TryDequeue(out var next)) { Assert.True(--remaining > 0); next(); }
        Assert.Equal(1, requests); Assert.Null(scheduler.State.Outstanding);
        Assert.Equal(AutoPosturePhase.Attempted, scheduler.State.Posters["A"].Phase);
    }
}
