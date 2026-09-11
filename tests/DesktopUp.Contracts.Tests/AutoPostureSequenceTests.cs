using DesktopUp.Application;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureModelTests
{
    [Fact]
    public void Layout_crossing_stationary_pointer_cannot_generate_hover_oscillation()
    {
        var c = new Clock(Poster(collapsed: true), Poster("B", collapsed: true), Poster("C", collapsed: true));
        c.Tick(0);
        for (int i = 1; i <= 1000; i++)
        {
            foreach (string id in c.Posters.Keys.ToArray())
            {
                var p = c.Posters[id];
                c.Posters[id] = p with { EntityRevision = i + 1, SpaceVersion = i,
                    Pointer = p.Pointer with { Presence = i % 2 == 0 ? AutoPosturePresence.Outside : AutoPosturePresence.ExpansionZone } };
            }
            Assert.Null(c.Tick(i * 10_000L).Request);
            Assert.Null(c.State.Outstanding);
        }
        Assert.Equal(1, c.State.NextRequestId);
    }

    [Fact]
    public void Pure_requests_reuse_existing_space_solver_for_100_chain_cycles_without_geometry_drift()
    {
        var space = new SpatialContinuityTests.Model(SpatialContinuityTests.Chain());
        var initialBounds = space.Scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
        var c = new Clock(space.Scene.Posters.Select(p => Poster(p.PosterId, collapsed: p.IsCollapsed)
            with { Policy = new(p.PosterId == "C") }).ToArray());
        long time = 0; c.Tick(time);
        for (int cycle = 0; cycle < 100; cycle++)
        {
            c.Enter("C");
            var expand = Assert.IsType<AutoPostureRequest>(c.Tick(++time).Request);
            Assert.False(expand.Collapse);
            Assert.True(space.Apply("C", expand.Collapse).Group.Plan.Succeeded);
            Assert.Null(c.Commit(++time, expand).Request);
            c.Exit("C"); Assert.Null(c.Tick(++time).Request);
            time += 3000;
            var collapse = Assert.IsType<AutoPostureRequest>(c.Tick(time).Request);
            Assert.True(collapse.Collapse);
            Assert.True(space.Apply("C", collapse.Collapse).Group.Plan.Succeeded);
            Assert.Null(c.Commit(++time, collapse).Request);
            Assert.All(space.Scene.Posters, p => Assert.Equal(initialBounds[p.PosterId], p.Bounds));
            Assert.All(c.State.Posters.Values, p => Assert.True(p.Last.IsCollapsed));
            Assert.Null(c.State.Outstanding);
        }
        Assert.Equal(201, c.State.NextRequestId);
    }

    [Theory]
    [InlineData(17)] [InlineData(73)] [InlineData(20260911)]
    public void Seeded_long_sequences_are_deterministic_bounded_and_input_immutable(int seed)
    {
        var random = new Random(seed);
        var observations = new[] { Poster("A"), Poster("B", true), Poster("C", true) };
        var left = AutoPostureState.Empty; var right = AutoPostureState.Empty;
        long now = 0; int issued = 0, cancelled = 0;
        for (int sequence = 1; sequence <= 5000; sequence++)
        {
            now += random.Next(1, 4001);
            AutoPostureCompletion? completion = null;
            if (left.Outstanding is { } flight && random.Next(3) == 0)
            {
                var outcome = left.CancellationIssued ? AutoPostureOutcome.Cancelled : (AutoPostureOutcome)random.Next(5);
                completion = new(flight, outcome);
                int target = Array.FindIndex(observations, p => p.Session == flight.Session);
                if (target >= 0 && outcome == AutoPostureOutcome.Committed)
                    observations[target] = observations[target] with { IsCollapsed = flight.Collapse, EntityRevision = observations[target].EntityRevision + 1 };
            }
            int index = random.Next(3); var p = observations[index];
            p = random.Next(12) switch
            {
                0 => p with { Pointer = p.Pointer with { Presence = AutoPosturePresence.ExpansionZone, EnterVersion = p.Pointer.EnterVersion + 1 } },
                1 => p with { Pointer = p.Pointer with { Presence = AutoPosturePresence.Outside, ExitVersion = p.Pointer.ExitVersion + 1 } },
                2 => p with { Pointer = p.Pointer with { Presence = AutoPosturePresence.Inside } },
                3 => p with { IsCollapsed = !p.IsCollapsed, ManualIntentVersion = p.ManualIntentVersion + 1, EntityRevision = p.EntityRevision + 1 },
                4 => p with { Policy = new(!p.Policy.Enabled, random.Next(1, 4)) },
                5 => p with { IsVisible = !p.IsVisible },
                6 => p with { Protection = p.Protection == AutoPostureProtection.None ? AutoPostureProtection.Menu : AutoPostureProtection.None },
                7 => p with { EnvironmentEpoch = p.EnvironmentEpoch + 1 },
                8 => p with { Session = new(p.Session.PosterId, p.Session.Epoch + 1), EntityRevision = 1, ManualIntentVersion = 0, SpaceVersion = 0, Pointer = new(AutoPosturePresence.Outside) },
                9 => p with { SpaceVersion = p.SpaceVersion + 1 },
                _ => p
            };
            observations[index] = p;
            bool busy = random.Next(5) == 0;
            var shared = random.Next(12) == 0 ? AutoPostureProtection.Confirmation : AutoPostureProtection.None;
            string inputBefore = JsonSerializer.Serialize(observations);
            string stateBefore = JsonSerializer.Serialize(left);
            var a = AutoPostureModel.Advance(left, new(sequence, now, observations, busy, shared, completion));
            var b = AutoPostureModel.Advance(right, new(sequence, now, observations.Reverse().ToArray(), busy, shared, completion));
            Assert.Equal(inputBefore, JsonSerializer.Serialize(observations));
            Assert.Equal(stateBefore, JsonSerializer.Serialize(left));
            Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
            Assert.False(a.Ignored); Assert.Equal(3, a.State.Posters.Count);
            if (a.Request is { } request)
            {
                issued++;
                Assert.False(busy); Assert.Equal(AutoPostureProtection.None, shared);
                Assert.True(left.Outstanding is null || completion?.Request == left.Outstanding);
                var current = observations.Single(o => o.Session == request.Session);
                Assert.Equal(!current.IsCollapsed, request.Collapse);
                Assert.True(current.Policy.Enabled && current.IsReady && current.IsVisible);
                Assert.Equal(AutoPostureProtection.None, current.Protection);
                Assert.Equal(current.EntityRevision, request.ExpectedEntityRevision);
                Assert.Equal(request, a.State.Outstanding);
            }
            if (a.Cancel is not null)
            {
                cancelled++; Assert.Null(a.Request); Assert.Equal(left.Outstanding, a.Cancel);
                Assert.False(left.CancellationIssued);
            }
            Assert.Equal(issued + 1L, a.State.NextRequestId);
            left = a.State; right = b.State;
        }
        Assert.True(issued > 10); Assert.True(cancelled > 0);
    }
}
