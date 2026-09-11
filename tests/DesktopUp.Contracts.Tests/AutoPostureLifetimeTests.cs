using DesktopUp.Application;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureModelTests
{
    [Theory]
    [InlineData(AutoPostureOutcome.InsufficientSpace)] [InlineData(AutoPostureOutcome.Conflict)]
    [InlineData(AutoPostureOutcome.Cancelled)] [InlineData(AutoPostureOutcome.HostUnavailable)]
    public void Failure_consumes_trigger_not_every_revision_or_timer_tick(AutoPostureOutcome outcome)
    {
        var c = new Clock(Poster(collapsed: true)); c.Tick(0); c.Enter();
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(1).Request);
        Assert.Null(c.Tick(2, completion: new(r, outcome)).Request);
        for (int i = 3; i < 103; i++)
        {
            c.A = c.A with { EntityRevision = i };
            Assert.Null(c.Tick(i).Request);
        }
        c.A = c.A with { SpaceVersion = 1 };
        var retry = Assert.IsType<AutoPostureRequest>(c.Tick(103).Request);
        Assert.NotEqual(r.Id, retry.Id);
        c.Tick(104, completion: new(retry, outcome));
        c.Exit(); c.Tick(105); c.Enter();
        Assert.NotNull(c.Tick(106).Request);
    }

    [Fact]
    public void Failed_collapse_does_not_loop_and_new_exit_gets_full_delay()
    {
        var c = new Clock(Poster()); c.Tick(0);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3000).Request);
        Assert.Null(c.Tick(3001, completion: new(r, AutoPostureOutcome.Conflict)).Request);
        Assert.Null(c.Tick(9000).Request);
        c.Enter(); c.Exit(); Assert.Null(c.Tick(9001).Request);
        Assert.NotNull(c.Tick(12001).Request);
    }

    [Fact]
    public void Claimed_commit_without_matching_authoritative_posture_is_not_fabricated()
    {
        var c = new Clock(Poster()); c.Tick(0);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3000).Request);
        Assert.Null(c.Tick(3001, completion: new(r, AutoPostureOutcome.Committed)).Request);
        Assert.False(c.State.Posters["A"].Last.IsCollapsed);
        Assert.Equal(AutoPosturePhase.Attempted, c.State.Posters["A"].Phase);
        Assert.Null(c.Tick(9000).Request);
    }

    [Theory]
    [InlineData("hidden")] [InlineData("not-ready")] [InlineData("unknown")]
    [InlineData("environment")] [InlineData("session")] [InlineData("policy")]
    [InlineData("manual")] [InlineData("protection")] [InlineData("deleted")]
    public void Invalidated_flight_cancels_once_but_holds_slot_until_exact_ack(string change)
    {
        var c = new Clock(Poster(), Poster("B")); c.Tick(0);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3000).Request);
        switch (change)
        {
            case "hidden": c.A = c.A with { IsVisible = false }; break;
            case "not-ready": c.A = c.A with { IsReady = false }; break;
            case "unknown": c.A = c.A with { Pointer = new(AutoPosturePresence.Unknown) }; break;
            case "environment": c.A = c.A with { EnvironmentEpoch = 2 }; break;
            case "session": c.A = c.A with { Session = new("A", 2) }; break;
            case "policy": c.A = c.A with { Policy = new(false) }; break;
            case "manual": c.A = c.A with { ManualIntentVersion = 1 }; break;
            case "protection": c.A = c.A with { Protection = AutoPostureProtection.Menu }; break;
            case "deleted": c.Posters.Remove("A"); break;
        }
        var cancel = c.Tick(3001);
        Assert.Equal(r, cancel.Cancel); Assert.Null(cancel.Request);
        Assert.Equal(r, c.State.Outstanding);
        Assert.Null(c.Tick(3002).Cancel);
        var wrong = r with { Id = r.Id + 100 };
        Assert.Null(c.Tick(3003, completion: new(wrong, AutoPostureOutcome.Cancelled)).Request);
        Assert.Equal(r, c.State.Outstanding);
        var next = c.Tick(3004, completion: new(r, AutoPostureOutcome.Cancelled));
        Assert.Equal("B", next.Request?.Session.PosterId);
        Assert.Null(next.Cancel);
    }

    [Fact]
    public void Old_completion_does_not_poison_new_environment_and_old_ack_cannot_clear_new_flight()
    {
        var c = new Clock(Poster(collapsed: true)); c.Tick(0); c.Enter();
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(1).Request);
        c.A = c.A with { EnvironmentEpoch = 2 };
        Assert.Equal(r, c.Tick(2).Cancel);
        Assert.Null(c.Tick(3, completion: new(r, AutoPostureOutcome.InsufficientSpace)).Request);
        Assert.False(c.State.Posters["A"].AttemptConsumed);
        c.Exit(); c.Tick(4); c.Enter();
        var next = Assert.IsType<AutoPostureRequest>(c.Tick(5).Request);
        Assert.Null(c.Tick(6, completion: new(r, AutoPostureOutcome.Committed)).Request);
        Assert.Equal(next, c.State.Outstanding);
    }

    [Fact]
    public void Removal_prunes_nodes_and_ack_releases_last_ephemeral_resource()
    {
        var c = new Clock(Poster()); c.Tick(0);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3000).Request);
        c.Posters.Clear(); Assert.Equal(r, c.Tick(3001).Cancel);
        Assert.Empty(c.State.Posters); Assert.NotNull(c.State.Outstanding);
        c.Tick(3002, completion: new(r, AutoPostureOutcome.Cancelled));
        Assert.Empty(c.State.Posters); Assert.Null(c.State.Outstanding);
        Assert.Null(c.Tick(1_000_000).Request);
    }

    [Theory]
    [InlineData("hidden")] [InlineData("not-ready")] [InlineData("unknown")]
    [InlineData("environment")] [InlineData("session")] [InlineData("policy")]
    public void Recovery_or_policy_change_never_catches_up_old_deadline(string change)
    {
        var c = new Clock(Poster()); c.Tick(0);
        switch (change)
        {
            case "hidden": c.A = c.A with { IsVisible = false }; break;
            case "not-ready": c.A = c.A with { IsReady = false }; break;
            case "unknown": c.A = c.A with { Pointer = new(AutoPosturePresence.Unknown) }; break;
            case "environment": c.A = c.A with { EnvironmentEpoch = 2 }; break;
            case "session": c.A = c.A with { Session = new("A", 2) }; break;
            case "policy": c.A = c.A with { Policy = new(true, 1) }; break;
        }
        Assert.Null(c.Tick(5000).Request);
        if (change is "hidden" or "not-ready" or "unknown")
        {
            c.A = c.A with { IsVisible = true, IsReady = true, Pointer = new(AutoPosturePresence.Outside) };
            Assert.Null(c.Tick(6000).Request);
            Assert.Equal(9000, c.State.Posters["A"].DeadlineMs);
        }
        else Assert.Equal(5000 + c.A.Policy.LeaveDelaySeconds * 1000, c.State.Posters["A"].DeadlineMs);
    }

    [Fact]
    public void Restart_starts_full_delay_and_does_not_reuse_pending_request()
    {
        var c = new Clock(Poster()); c.Tick(0); Assert.NotNull(c.Tick(3000).Request);
        var restarted = new Clock(c.A);
        Assert.Null(restarted.Tick(1_000_000).Request);
        Assert.Equal(1_003_000, restarted.State.Posters["A"].DeadlineMs);
    }

    [Theory]
    [InlineData("sequence")] [InlineData("time")] [InlineData("entity")]
    [InlineData("epoch")] [InlineData("environment")] [InlineData("entry")]
    [InlineData("exit")] [InlineData("manual")] [InlineData("space")]
    public void Regressed_frame_is_ignored_atomically_including_its_completion(string counter)
    {
        var p = Poster() with { Session = new("A", 2), EntityRevision = 2, EnvironmentEpoch = 2,
            Pointer = new(AutoPosturePresence.Outside, 2, 2), ManualIntentVersion = 2, SpaceVersion = 2 };
        var c = new Clock(p); c.Tick(10);
        var r = Assert.IsType<AutoPostureRequest>(c.Tick(3010).Request);
        long seq = 3, now = 3011;
        switch (counter)
        {
            case "sequence": seq = 2; break;
            case "time": now = 3009; break;
            case "entity": p = p with { EntityRevision = 1 }; break;
            case "epoch": p = p with { Session = new("A", 1) }; break;
            case "environment": p = p with { EnvironmentEpoch = 1 }; break;
            case "entry": p = p with { Pointer = p.Pointer with { EnterVersion = 1 } }; break;
            case "exit": p = p with { Pointer = p.Pointer with { ExitVersion = 1 } }; break;
            case "manual": p = p with { ManualIntentVersion = 1 }; break;
            case "space": p = p with { SpaceVersion = 1 }; break;
        }
        var step = AutoPostureModel.Advance(c.State, new(seq, now, [p], Completion: new(r, AutoPostureOutcome.Cancelled)));
        Assert.True(step.Ignored); Assert.Same(c.State, step.State);
        Assert.Null(step.Request); Assert.Null(step.Cancel); Assert.Equal(r, step.State.Outstanding);
    }

    [Fact]
    public void Malformed_catalog_is_rejected_before_any_partial_update()
    {
        var p = Poster(); var c = new Clock(p); c.Tick(0);
        Assert.Throws<ArgumentException>(() => AutoPostureModel.Advance(c.State, new(2, 3000, [p, p])));
        Assert.Throws<ArgumentException>(() => AutoPostureModel.Advance(c.State, new(2, 3000, [p with { Pointer = new((AutoPosturePresence)99) }])));
        Assert.Throws<ArgumentException>(() => AutoPostureModel.Advance(c.State, new(2, 3000, [p], SharedProtection: (AutoPostureProtection)256)));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoPostureModel.Advance(c.State, new(2, long.MaxValue, [p])));
        Assert.Null(c.State.Outstanding); Assert.Equal(1, c.State.Sequence);
    }

    [Fact]
    public void State_owns_immutable_snapshot_and_cannot_be_mutated_through_input_or_map()
    {
        var input = new[] { Poster() };
        var step = AutoPostureModel.Advance(AutoPostureState.Empty, new(1, 0, input));
        input[0] = input[0] with { IsCollapsed = true };
        Assert.False(step.State.Posters["A"].Last.IsCollapsed);
        var map = Assert.IsAssignableFrom<IDictionary<string, AutoPostureNode>>(step.State.Posters);
        Assert.Throws<NotSupportedException>(() => map.Clear());
        string before = JsonSerializer.Serialize(step.State);
        AutoPostureModel.Advance(step.State, new(2, 3000, [Poster()]));
        Assert.Equal(before, JsonSerializer.Serialize(step.State));
    }
}
