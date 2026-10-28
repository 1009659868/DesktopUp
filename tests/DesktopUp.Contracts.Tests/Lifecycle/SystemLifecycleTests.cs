using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class SystemLifecycleTests
{
    [Theory]
    [InlineData(0x218, 4, SystemLifecycleSignal.PowerSuspend)]
    [InlineData(0x218, 6, SystemLifecycleSignal.PowerResume)]
    [InlineData(0x218, 7, SystemLifecycleSignal.PowerResume)]
    [InlineData(0x218, 18, SystemLifecycleSignal.PowerResume)]
    [InlineData(0x2B1, 7, SystemLifecycleSignal.SessionLock)]
    [InlineData(0x2B1, 8, SystemLifecycleSignal.SessionUnlock)]
    [InlineData(0x2B1, 15, SystemLifecycleSignal.SessionUnlock)]
    [InlineData(0x2B1, 1, SystemLifecycleSignal.SessionConnect)]
    [InlineData(0x2B1, 3, SystemLifecycleSignal.SessionConnect)]
    [InlineData(0x2B1, 5, SystemLifecycleSignal.SessionConnect)]
    [InlineData(0x2B1, 2, SystemLifecycleSignal.SessionDisconnect)]
    [InlineData(0x2B1, 4, SystemLifecycleSignal.SessionDisconnect)]
    [InlineData(0x2B1, 6, SystemLifecycleSignal.SessionDisconnect)]
    public void Documented_native_signals_map_without_reinterpreting_remote_control(int message, int value, SystemLifecycleSignal expected) =>
        Assert.Equal(expected, DesktopLifecycleSignals.Classify((uint)message, (nuint)value, 12, 12));

    [Theory] [InlineData(0x2B1, 9, 12)] [InlineData(0x2B1, 10, 12)]
    [InlineData(0x2B1, 7, 13)] [InlineData(0x218, 10, 12)] [InlineData(0x123, 7, 12)]
    public void Other_sessions_and_unrelated_signals_do_not_pause_this_application(int message, int value, int session) =>
        Assert.Null(DesktopLifecycleSignals.Classify((uint)message, (nuint)value, session, 12));

    [Fact]
    public void Power_resume_does_not_unlock_or_reconnect_session()
    {
        var gate = new SystemLifecycleGate();
        Assert.Equal(SystemLifecycleAction.Suspend, gate.Observe(SystemLifecycleSignal.SessionLock));
        Assert.Equal(SystemLifecycleAction.None, gate.Observe(SystemLifecycleSignal.SessionLock));
        gate.Observe(SystemLifecycleSignal.SessionDisconnect); gate.Observe(SystemLifecycleSignal.PowerSuspend);
        Assert.Equal(SystemLifecycleAction.None, gate.Observe(SystemLifecycleSignal.PowerResume));
        Assert.True(gate.IsSuspended);
        Assert.Equal(SystemLifecycleAction.None, gate.Observe(SystemLifecycleSignal.SessionUnlock));
        Assert.True(gate.IsSuspended);
        Assert.Equal(SystemLifecycleAction.Recover, gate.Observe(SystemLifecycleSignal.SessionConnect));
        Assert.False(gate.IsSuspended);
    }
    [Theory] [InlineData(SystemLifecycleSignal.PowerResume)] [InlineData(SystemLifecycleSignal.SessionConnect)]
    [InlineData(SystemLifecycleSignal.SessionUnlock)]
    public void Resume_without_observed_suspend_still_requires_fresh_recovery(SystemLifecycleSignal resume)
    {
        var gate = new SystemLifecycleGate();
        Assert.Equal(SystemLifecycleAction.Recover, gate.Observe(resume)); Assert.False(gate.IsSuspended);
    }
    [Fact]
    public void Invalid_signal_cannot_mutate_pause_reasons()
    {
        var gate = new SystemLifecycleGate(); gate.Observe(SystemLifecycleSignal.SessionLock);
        Assert.Throws<ArgumentOutOfRangeException>(() => gate.Observe((SystemLifecycleSignal)99)); Assert.True(gate.IsSuspended);
    }
}

public sealed partial class AutoPostureSchedulerTests
{
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(6)]
    public void Lifetime_churn_uses_one_timer_and_prunes_all_deleted_targets(int count)
    {
        using var f = new Rig();
        f.Sample = new(Enumerable.Range(0, count).Select(i => Node("P" + i, delay: 300)).ToArray(), false);
        f.Tick(0); Assert.Equal(count == 0 ? null : (int?)120, f.Timer);
        for (int round = 1; round <= 40; round++)
        {
            f.Change(n => n with { IsVisible = false }); f.Tick(round * 1000); Assert.Null(f.Timer);
            f.Change(n => n with { IsVisible = true, Session = n.Session with { Epoch = n.Session.Epoch + 1 } });
            f.Tick(round * 1000 + 1); Assert.Empty(f.Requests);
            Assert.Equal(count, f.Scheduler.State.Posters.Count); Assert.Empty(f.Queue);
        }
        f.Sample = new([], false); f.Tick(100000); Assert.Empty(f.Scheduler.State.Posters); Assert.Null(f.Timer);
        f.Dispose(); f.Drain(); Assert.Empty(f.Requests); Assert.Null(f.Timer);
    }
    [Theory] [InlineData("hide")] [InlineData("delete")] [InlineData("pause")]
    public void Queued_pre_lifetime_effect_reads_current_targets_at_dispatch(string change)
    {
        using var f = new Rig(); f.Tick(0); f.Now = 1000; f.Scheduler.Observe();
        if (change == "delete") f.Sample = new([], false);
        else f.Change(n => change == "hide" ? n with { IsVisible = false } : n with { IsReady = false, EnvironmentEpoch = 2 });
        f.Drain(); Assert.Empty(f.Requests); Assert.Null(f.Timer);
    }
    [Fact]
    public void Nested_system_pause_clears_deadline_until_last_reason_recovers()
    {
        using var f = new Rig(); var gate = new SystemLifecycleGate(); f.Tick(0);
        gate.Observe(SystemLifecycleSignal.SessionLock); gate.Observe(SystemLifecycleSignal.PowerSuspend);
        f.Change(n => n with { IsReady = !gate.IsSuspended, EnvironmentEpoch = 2 }); f.Tick(900);
        gate.Observe(SystemLifecycleSignal.PowerResume);
        f.Change(n => n with { IsReady = !gate.IsSuspended }); f.Tick(100000); Assert.Null(f.Timer); Assert.Empty(f.Requests);
        gate.Observe(SystemLifecycleSignal.SessionUnlock);
        f.Change(n => n with { IsReady = !gate.IsSuspended, EnvironmentEpoch = 3 }); f.Tick(100001);
        f.Tick(101000); Assert.Empty(f.Requests); f.Tick(101001); Assert.Single(f.Requests);
    }
    [Fact]
    public void Old_session_ack_cannot_complete_new_incarnation_flight()
    {
        using var f = new Rig(); f.Tick(0); f.Tick(1000); var old = f.Requests[0];
        f.Change(n => n with { Session = new("A", 2) }); f.Tick(1001);
        Assert.Single(f.Cancellations); f.Done[old.Id](AutoPostureOutcome.Cancelled); f.Drain();
        f.Tick(2001); var next = f.Requests[1];
        f.Done[old.Id](AutoPostureOutcome.Committed); f.Drain(); Assert.Equal(next, f.Scheduler.State.Outstanding);
        f.Dispose(); f.Done[next.Id](AutoPostureOutcome.Committed); f.Drain(); Assert.Null(f.Timer); Assert.Empty(f.Scheduler.State.Posters);
    }
}
