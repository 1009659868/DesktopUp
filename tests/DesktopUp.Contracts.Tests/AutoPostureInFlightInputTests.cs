using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSchedulerTests
{
    public static TheoryData<bool, bool, bool, AutoPostureProtection> RawClicks
    {
        get
        {
            var data = new TheoryData<bool, bool, bool, AutoPostureProtection>();
            foreach (bool collapse in new[] { true, false })
            foreach (bool inside in new[] { true, false })
            foreach (bool local in new[] { true, false })
            foreach (var protection in new[] { AutoPostureProtection.PointerButton, AutoPostureProtection.PointerCapture,
                AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture })
                data.Add(collapse, inside, local, protection);
            return data;
        }
    }

    [Theory] [MemberData(nameof(RawClicks))]
    public void Raw_click_during_animation_does_not_cancel_or_reverse(bool collapse, bool inside,
        bool local, AutoPostureProtection protection)
    {
        using var f = new Rig();
        f.Change(n => n with { IsCollapsed = !collapse }); f.Tick(0);
        if (!collapse) f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) });
        f.Tick(1000);
        var request = Assert.Single(f.Requests); Assert.Equal(collapse, request.Collapse);
        f.Sample = f.Sample with { SpatialBusy = true };
        f.Change(n => n with { Pointer = new(inside ? AutoPosturePresence.Inside : AutoPosturePresence.Outside, 1, 1) });
        // Repeated down/up cycles, not just an endpoint. No cancellation or extra requests.
        for (int i = 0; i < 6; i++)
        {
            var pressed = i % 2 == 0 ? protection : AutoPostureProtection.None;
            if (local) f.Change(n => n with { Protection = pressed });
            else f.Sample = f.Sample with { Protection = pressed };
            f.Tick(1010 + i * 10);
            Assert.Empty(f.Cancellations); Assert.Single(f.Requests);
            Assert.Equal(request, f.Scheduler.State.Outstanding);
            Assert.Equal(AutoPosturePhase.InFlight, f.Scheduler.State.Posters["A"].Phase);
        }
        f.Change(n => n with { IsCollapsed = collapse, EntityRevision = 2 });
        f.Sample = f.Sample with { SpatialBusy = false };
        f.Done[request.Id](AutoPostureOutcome.Committed); f.Drain();
        Assert.Null(f.Scheduler.State.Outstanding); Assert.Single(f.Requests);
        Assert.Equal(collapse, f.Scheduler.State.Posters["A"].Last.IsCollapsed);
        // Expansion completed outside must wait a complete NEW leave delay.
        long nextDeadline = f.Now + 1000;
        f.Tick(nextDeadline - 1); Assert.Single(f.Requests);
        if (!collapse && !inside) { f.Tick(nextDeadline); Assert.Equal(2, f.Requests.Count); Assert.True(f.Requests[1].Collapse); }
    }

    [Theory]
    [InlineData(AutoPostureProtection.PointerButton)]
    [InlineData(AutoPostureProtection.PointerCapture)]
    [InlineData(AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture)]
    public void Raw_input_between_deadline_and_dispatch_still_blocks_new_animation(AutoPostureProtection protection)
    {
        using var f = new Rig(); f.Tick(0); f.Now = 1000; f.Scheduler.Observe();
        f.Sample = f.Sample with { Protection = protection }; f.Drain();
        Assert.Empty(f.Requests); Assert.Empty(f.Cancellations);
        Assert.Equal(AutoPosturePhase.Protected, f.Scheduler.State.Posters["A"].Phase);
        f.Sample = f.Sample with { Protection = AutoPostureProtection.None }; f.Tick(1100);
        f.Tick(2099); Assert.Empty(f.Requests); f.Tick(2100); Assert.Single(f.Requests);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void Finishing_while_button_held_does_not_start_waiting_neighbor_or_replay_hover(bool collapse)
    {
        using var f = new Rig(); f.Sample = new([Node("A", !collapse), Node("B")], false); f.Tick(0);
        if (!collapse) f.Change(n => n.Session.PosterId == "A" ? n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) } : n);
        f.Tick(1000); var request = Assert.Single(f.Requests); Assert.Equal("A", request.Session.PosterId);
        f.Sample = f.Sample with { SpatialBusy = true, Protection = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture };
        f.Tick(1100); Assert.Empty(f.Cancellations);
        f.Change(n => n.Session.PosterId == "A" ? n with { IsCollapsed = collapse, EntityRevision = 2,
            Pointer = new(collapse ? AutoPosturePresence.ExpansionZone : AutoPosturePresence.Inside, 2) } : n);
        f.Sample = f.Sample with { SpatialBusy = false };
        f.Done[request.Id](AutoPostureOutcome.Committed); f.Drain();
        f.Tick(10000); Assert.Single(f.Requests); Assert.Null(f.Scheduler.State.Outstanding);
        f.Sample = f.Sample with { Protection = AutoPostureProtection.None }; f.Tick(10001);
        f.Tick(11000); Assert.Single(f.Requests);
        f.Tick(11001); Assert.Equal("B", f.Requests[1].Session.PosterId); Assert.Equal(2, f.Requests.Count);
    }

    [Theory]
    [InlineData(true, "manual")] [InlineData(false, "manual")]
    [InlineData(true, "policy")] [InlineData(false, "policy")]
    [InlineData(true, "environment")] [InlineData(false, "environment")]
    [InlineData(true, "unknown")] [InlineData(false, "unknown")]
    [InlineData(true, "gesture")] [InlineData(false, "gesture")]
    [InlineData(true, "menu")] [InlineData(false, "menu")]
    public void Raw_input_never_masks_real_invalidation(bool collapse, string reason)
    {
        using var f = new Rig(); f.Change(n => n with { IsCollapsed = !collapse }); f.Tick(0);
        if (!collapse) f.Change(n => n with { Pointer = new(AutoPosturePresence.ExpansionZone, 1) });
        f.Tick(1000); var request = Assert.Single(f.Requests);
        f.Sample = f.Sample with { SpatialBusy = true, Protection = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture };
        f.Tick(1100); Assert.Empty(f.Cancellations);
        f.Change(n => reason switch
        {
            "manual" => n with { ManualIntentVersion = 1 },
            "policy" => n with { Policy = new(false, 1) },
            "environment" => n with { EnvironmentEpoch = 2 },
            "unknown" => n with { Pointer = n.Pointer with { Presence = AutoPosturePresence.Unknown } },
            "gesture" => n with { Protection = AutoPostureProtection.Gesture },
            _ => n with { Protection = AutoPostureProtection.Menu }
        });
        f.Tick(1110); Assert.Equal(request, Assert.Single(f.Cancellations));
        f.Tick(1120); Assert.Single(f.Cancellations); Assert.Equal(request, f.Scheduler.State.Outstanding);
        f.Done[request.Id](AutoPostureOutcome.Cancelled); f.Drain();
        Assert.Null(f.Scheduler.State.Outstanding); Assert.Single(f.Requests);
    }
}
