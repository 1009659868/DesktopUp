using DesktopUp.Application;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureModelTests
{
    private static AutoPostureObservation Poster(string id = "A", bool collapsed = false, int delay = 3) =>
        new(new(id, 1), 1, 1, new(true, delay), collapsed, true, true, new(AutoPosturePresence.Outside));

    private sealed class Clock(params AutoPostureObservation[] posters)
    {
        public AutoPostureState State = AutoPostureState.Empty;
        public readonly Dictionary<string, AutoPostureObservation> Posters = posters.ToDictionary(p => p.Session.PosterId);
        private long _sequence;
        public AutoPostureObservation A { get => Posters["A"]; set => Posters["A"] = value; }
        public AutoPostureStep Tick(long now, bool busy = false, AutoPostureProtection shared = AutoPostureProtection.None,
            AutoPostureCompletion? completion = null)
        {
            var step = AutoPostureModel.Advance(State, new(++_sequence, now, Posters.Values.ToArray(), busy, shared, completion));
            Assert.False(step.Ignored);
            State = step.State;
            return step;
        }
        public void Enter(string id = "A")
        {
            var p = Posters[id];
            Posters[id] = p with { Pointer = p.Pointer with { Presence = AutoPosturePresence.ExpansionZone, EnterVersion = p.Pointer.EnterVersion + 1 } };
        }
        public void Exit(string id = "A")
        {
            var p = Posters[id];
            Posters[id] = p with { Pointer = p.Pointer with { Presence = AutoPosturePresence.Outside, ExitVersion = p.Pointer.ExitVersion + 1 } };
        }
        public AutoPostureStep Commit(long now, AutoPostureRequest request)
        {
            var p = Posters[request.Session.PosterId];
            Posters[request.Session.PosterId] = p with { IsCollapsed = request.Collapse, EntityRevision = p.EntityRevision + 1 };
            return Tick(now, completion: new(request, AutoPostureOutcome.Committed));
        }
    }

    [Fact]
    public void Default_is_off_and_pointer_motion_cannot_request_a_change()
    {
        Assert.False(new AutoPosturePolicy().Enabled);
        Assert.Equal(3, new AutoPosturePolicy().LeaveDelaySeconds);
        var c = new Clock(Poster() with { Policy = new() });
        c.Tick(0); c.Enter(); Assert.Null(c.Tick(1).Request); c.Exit();
        Assert.Null(c.Tick(500_000).Request);
        Assert.Equal(AutoPosturePhase.Disabled, c.State.Posters["A"].Phase);
        Assert.Null(c.State.Posters["A"].DeadlineMs);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(301)]
    public void Invalid_delay_rejected_even_while_disabled(int delay) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutoPosturePolicy(false, delay).Validate());

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(300)]
    public void Delay_boundary_emits_exactly_once_and_never_changes_authoritative_posture(int delay)
    {
        var c = new Clock(Poster(delay: delay));
        Assert.Null(c.Tick(100).Request);
        Assert.Null(c.Tick(100 + delay * 1000 - 1).Request);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(100 + delay * 1000).Request);
        Assert.True(r.Collapse);
        Assert.False(c.State.Posters["A"].Last.IsCollapsed);
        Assert.Null(c.Tick(200 + delay * 1000).Request);
        Assert.Equal(r, c.State.Outstanding);
    }

    [Fact]
    public void Initial_presence_and_button_area_are_not_new_hover_intent()
    {
        var c = new Clock(Poster(collapsed: true) with { Pointer = new(AutoPosturePresence.ExpansionZone, 9) });
        Assert.Null(c.Tick(0).Request);
        Assert.Null(c.Tick(1000).Request);
        c.A = c.A with { Pointer = c.A.Pointer with { Presence = AutoPosturePresence.Inside } };
        Assert.Null(c.Tick(2000).Request);
        c.Enter();
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(2001).Request);
        Assert.False(r.Collapse);
        Assert.Null(c.Tick(2002).Request);
    }

    [Fact]
    public void Reentry_cancels_old_deadline_and_next_exit_starts_full_delay()
    {
        var c = new Clock(Poster()); c.Tick(0); c.Enter();
        Assert.Null(c.Tick(2999).Request);
        Assert.Null(c.State.Posters["A"].DeadlineMs);
        c.Exit(); Assert.Null(c.Tick(3000).Request);
        Assert.Null(c.Tick(5999).Request);
        Assert.NotNull(c.Tick(6000).Request);
    }

    [Fact]
    public void Boundary_pair_between_samples_also_restarts_full_delay()
    {
        var c = new Clock(Poster()); c.Tick(0);
        c.Enter(); c.Exit(); Assert.Null(c.Tick(2900).Request);
        Assert.Equal(5900, c.State.Posters["A"].DeadlineMs);
        Assert.Null(c.Tick(3000).Request);
        Assert.NotNull(c.Tick(5900).Request);
    }

    [Fact]
    public void Manual_collapse_guard_requires_genuine_exit_then_entry()
    {
        var c = new Clock(Poster() with { Pointer = new(AutoPosturePresence.ExpansionZone) }); c.Tick(0);
        c.A = c.A with { IsCollapsed = true, EntityRevision = 2, ManualIntentVersion = 1 };
        Assert.Null(c.Tick(1).Request);
        Assert.Equal(AutoPosturePhase.ManualSuppressed, c.State.Posters["A"].Phase);
        c.Enter(); Assert.Null(c.Tick(2).Request);
        // A layout-induced Outside sample is not sufficient exit evidence.
        c.A = c.A with { Pointer = c.A.Pointer with { Presence = AutoPosturePresence.Outside } };
        c.Tick(3); c.Enter(); Assert.Null(c.Tick(4).Request);
        c.Exit(); c.Enter(); // both genuine boundaries observed between model frames
        Assert.NotNull(c.Tick(5).Request);
    }

    [Fact]
    public void Manual_expansion_outside_restarts_delay_without_replaying_old_deadline()
    {
        var c = new Clock(Poster()); c.Tick(0);
        c.A = c.A with { ManualIntentVersion = 1, EntityRevision = 2 };
        Assert.Null(c.Tick(5000).Request);
        Assert.Equal(8000, c.State.Posters["A"].DeadlineMs);
        Assert.NotNull(c.Tick(8000).Request);
    }

    public static TheoryData<AutoPostureProtection> Protections => new()
    {
        AutoPostureProtection.Editing, AutoPostureProtection.Menu, AutoPostureProtection.Confirmation,
        AutoPostureProtection.PointerButton, AutoPostureProtection.PointerCapture, AutoPostureProtection.DragDrop, AutoPostureProtection.Gesture,
        AutoPostureProtection.Recovery
    };

    [Theory]
    [MemberData(nameof(Protections))]
    public void Local_protection_clears_deadline_and_restarts_on_release(AutoPostureProtection protection)
    {
        var c = new Clock(Poster()); c.Tick(0);
        c.A = c.A with { Protection = protection };
        Assert.Null(c.Tick(2999).Request); Assert.Null(c.State.Posters["A"].DeadlineMs);
        Assert.Null(c.Tick(4000).Request);
        c.A = c.A with { Protection = AutoPostureProtection.None };
        Assert.Null(c.Tick(5000).Request); Assert.Null(c.Tick(7999).Request);
        Assert.NotNull(c.Tick(8000).Request);
    }

    [Theory]
    [MemberData(nameof(Protections))]
    public void Shared_protection_blocks_all_and_does_not_replay_hover(AutoPostureProtection protection)
    {
        var c = new Clock(Poster(collapsed: true), Poster("B")); c.Tick(0);
        c.Enter(); Assert.Null(c.Tick(3000, shared: protection).Request);
        Assert.All(c.State.Posters.Values, n => Assert.Equal(AutoPosturePhase.Protected, n.Phase));
        Assert.Null(c.Tick(5000).Request);
        Assert.Equal(8000, c.State.Posters["B"].DeadlineMs);
        Assert.Null(c.Tick(5001).Request);
        c.Exit(); c.Tick(5002); c.Enter();
        Assert.Equal("A", c.Tick(5003).Request?.Session.PosterId);
    }

    [Fact]
    public void Busy_wait_uses_fresh_candidate_and_entity_revision()
    {
        var c = new Clock(Poster(), Poster("B")); c.Tick(0);
        Assert.Null(c.Tick(3000, busy: true).Request);
        c.Enter(); c.Posters["B"] = c.Posters["B"] with { EntityRevision = 44 };
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3001).Request);
        Assert.Equal("B", r.Session.PosterId); Assert.Equal(44, r.ExpectedEntityRevision);
        Assert.Null(c.State.Posters["A"].DeadlineMs);
    }

    [Fact]
    public void Busy_hover_candidate_is_withdrawn_when_pointer_leaves()
    {
        var c = new Clock(Poster(collapsed: true)); c.Tick(0); c.Enter();
        Assert.Null(c.Tick(1, busy: true).Request);
        c.Exit(); Assert.Null(c.Tick(2).Request);
        Assert.Null(c.Tick(1_000_000).Request);
    }

    [Fact]
    public void Candidate_order_is_deadline_then_ordinal_id_and_independent_of_catalog_order()
    {
        foreach (bool reverse in new[] { false, true })
        {
            var ps = new[] { Poster("C", delay: 1), Poster("B"), Poster("A") };
            var c = new Clock(reverse ? ps.Reverse().ToArray() : ps); c.Tick(0);
            var r1 = Assert.IsType<AutoPostureRequest>(c.Tick(3000).Request);
            Assert.Equal("C", r1.Session.PosterId);
            var r2 = Assert.IsType<AutoPostureRequest>(c.Commit(3001, r1).Request);
            Assert.Equal("A", r2.Session.PosterId);
            var r3 = Assert.IsType<AutoPostureRequest>(c.Commit(3002, r2).Request);
            Assert.Equal("B", r3.Session.PosterId);
            Assert.Null(c.Commit(3003, r3).Request);
        }
    }
}
