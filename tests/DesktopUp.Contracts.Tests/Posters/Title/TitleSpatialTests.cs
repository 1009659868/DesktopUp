using DesktopUp.Application;
using DesktopUp.Domain;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

internal static class TitleSpatialTestData
{
    internal static readonly PosterContentMetrics Metrics = new("title-test", 60, 118, 42);
    internal static readonly SpatialEnvironment Env = new("test", new(0, 0, 1200, 1000), 96, 96, 1, 1,
        Guid.Parse("00000000-0000-0000-0000-000000000002"));
    internal static SpatialPoster P(string id, int x, int y, int height = 60, int width = 240, int preferred = 360) =>
        new(id, new(x, y, width, height), preferred, true, false, height == 60, 1);
    internal static TitleSpatialStart Start(SpatialSnapshot scene, string id = "C", bool? paused = null,
        double fraction = .5, SpatialContinuityState? continuity = null, PosterContentMetrics? metrics = null)
    {
        metrics ??= Metrics;
        var options = new PosterAnimationOptions(12);
        var presented = scene;
        var p = scene.Posters.Single(p => p.PosterId == id);
        var stage = p.IsCollapsed ? TitlePresentedPosture.Compact : TitlePresentedPosture.Expanded;
        TitlePausedPosture? prefix = null;
        if (paused is { } collapsed)
        {
            var plan = SpatialContinuitySolver.Plan(scene, id, collapsed, (int)(60 * scene.Environment.DpiY / 96d),
                "auto-before-title", metrics, continuity);
            Assert.True(plan.Group.Plan.Succeeded, string.Join(',', plan.Group.Plan.Issues));
            var schedule = SpatialAnimationSchedule.Create(scene, plan.Group.Frames, options);
            int count = Math.Clamp((int)(fraction * schedule.Frames.Count), 0, schedule.Frames.Count - 1);
            prefix = new("auto-before-title", collapsed, count);
            presented = Apply(scene, schedule.Frames.Take(count).ToArray());
            stage = collapsed ? TitlePresentedPosture.Collapsing : TitlePresentedPosture.Expanding;
        }
        p = presented.Posters.Single(p => p.PosterId == id);
        var r = p.Bounds;
        var press = new TitlePress("title-operation", 1, 1,
            new(Guid.Parse("00000000-0000-0000-0000-000000000003"),
                new(new(id, 1), 1, new(r.X, r.Y, r.Width, r.Height),
                    new(PosterInputTargetKind.TitleBar, 0, new(8, 8, r.Width - 60, 35)))),
            new(r.X + 20, r.Y + 20), stage, new(AutoPosturePresence.ExpansionZone, 2, 1), IsLocked: p.IsLocked);
        return new(scene, press, options, prefix);
    }
    internal static SpatialSnapshot Apply(SpatialSnapshot scene, IReadOnlyList<SpatialBatchFrame> frames)
    {
        foreach (var frame in frames)
        {
            var before = scene;
            scene = new(scene.Environment, scene.Posters.Select(p => frame.Changes.FirstOrDefault(c => c.PosterId == p.PosterId) is { } leg
                ? p with { Bounds = leg.To } : p));
            foreach (var leg in frame.Changes) Assert.Equal(before.Posters.Single(p => p.PosterId == leg.PosterId).Bounds, leg.From);
            foreach (var a in before.Posters.Where(p => p.IsVisible))
            foreach (var b in before.Posters.Where(p => p.IsVisible && string.CompareOrdinal(p.PosterId, a.PosterId) > 0))
                Assert.False(SpatialPathPolicy.CrossesMoving(a.Bounds, scene.Posters.Single(p => p.PosterId == a.PosterId).Bounds,
                    b.Bounds, scene.Posters.Single(p => p.PosterId == b.PosterId).Bounds));
            Assert.Empty(PosterSpatialSolver.ValidateLayout(scene));
        }
        return scene;
    }
    internal static TitleSpatialSample Input(TitleSpatialSession s, TitlePointerEventKind kind, int dx = 0, int dy = 0) =>
        new(new(s.Intent.Token, s.Intent.Sequence + 1, kind,
            new(s.Start.Press.Position.X + dx, s.Start.Press.Position.Y + dy),
            true, new(AutoPosturePresence.ExpansionZone, 2, 1)));
    internal static TitleSpatialProposal Accept(TitleSpatialSession s, TitleSpatialSample sample)
    {
        var proposal = Assert.IsType<TitleSpatialProposal>(s.Propose(sample));
        var endpoint = Apply(s.Presented, proposal.Frames);
        Assert.Equal(endpoint.Posters.Select(p => p.Bounds), proposal.Endpoint.Posters.Select(p => p.Bounds));
        Assert.True(s.Accept(proposal, true));
        return proposal;
    }
    internal static void Release(TitleSpatialSession s, int dx = 0, int dy = 0) =>
        Accept(s, Input(s, TitlePointerEventKind.Release, dx, dy));
    internal static string Geometry(SpatialSnapshot s) => JsonSerializer.Serialize(s.Posters.Select(p =>
        new { p.PosterId, p.Bounds, p.IsCollapsed }));
}

public sealed class TitleSpatialTests
{
    public static IEnumerable<object[]> PartialCases =>
        from y in new[] { 0, 600, 940 } from f in new[] { .1, .5, .9 } select new object[] { y, f };

    [Theory] [MemberData(nameof(PartialCases))]
    public void ExpansionTakeoverMovesNowAndRetractsAtPresentedTitle(int y, double fraction)
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, y)]);
        var start = TitleSpatialTestData.Start(scene, paused: false, fraction: fraction);
        var s = new TitleSpatialSession(start, TitleSpatialTestData.Metrics);
        var origin = s.Presented.Posters.Single().Bounds;
        var proposal = TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
        var current = s.Presented.Posters.Single().Bounds;
        Assert.Equal(origin.X + 30, current.X); Assert.Equal(origin.Y, current.Y);
        Assert.True(current.Height < origin.Height); Assert.Single(proposal.Frames);
        TitleSpatialTestData.Release(s, 30);
        Assert.Equal(new(origin.X + 30, origin.Y, origin.Width, 60), s.Presented.Posters.Single().Bounds);
        Assert.True(s.Complete); Assert.NotNull(s.Intent.Rearm);
        var replay = TitleSpatialSession.Replay(s.Freeze(), TitleSpatialTestData.Metrics);
        Assert.Equal(TitleSpatialTestData.Geometry(s.Presented), TitleSpatialTestData.Geometry(replay.Presented));
    }

    [Theory] [InlineData(.1)] [InlineData(.5)] [InlineData(.9)]
    public void CollapseTakeoverDoesNotRestoreOldCompactAnchor(double fraction)
    {
        var m = new SpatialContinuityTests.Model(new(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 940)]));
        m.Apply("C", false);
        var start = TitleSpatialTestData.Start(m.Scene, paused: true, fraction: fraction, continuity: m.State);
        var s = new TitleSpatialSession(start, TitleSpatialTestData.Metrics, m.State);
        var before = s.Presented.Posters.Single().Bounds;
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
        TitleSpatialTestData.Release(s, 30);
        Assert.Equal(before.Y, s.Presented.Posters.Single().Bounds.Y);
        Assert.Equal(before.X + 30, s.Presented.Posters.Single().Bounds.X);
        Assert.Equal(60, s.Presented.Posters.Single().Bounds.Height);
    }

    [Theory] [InlineData(0)] [InlineData(600)] [InlineData(940)]
    public void FastClickAndHoverAndManualHaveExactlyTheSameSpatialResult(int y)
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 0, y)]);
        var normal = SpatialContinuitySolver.Plan(initial, "C", false, 60, "normal", TitleSpatialTestData.Metrics);
        foreach (bool partial in new[] { false, true })
        {
            var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial, paused: partial ? false : null), TitleSpatialTestData.Metrics);
            var proposal = TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Release));
            Assert.True(proposal.ImmediateGroup);
            Assert.Equal(normal.Group.Plan.Changes.Single().After, s.Presented.Posters.Single().Bounds);
            Assert.False(s.Presented.Posters.Single().IsCollapsed);
        }
    }

    [Theory] [InlineData(220, false)] [InlineData(100, true)]
    public void FastClickUsesTheExistingMinimumAndDoesNotChangePreferredHeight(int gap, bool insufficient)
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 0), TitleSpatialTestData.P("lock", 0, gap, 1000 - gap, preferred: 1000) with { IsLocked = true }]);
        var normal = SpatialContinuitySolver.Plan(scene, "C", false, 60, "normal", TitleSpatialTestData.Metrics);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene), TitleSpatialTestData.Metrics);
        TitleSpatialTestData.Release(s);
        var result = s.Presented.Posters.Single(p => p.PosterId == "C");
        Assert.Equal(insufficient, result.IsCollapsed); Assert.Equal(360, result.ExpandedHeight);
        if (!insufficient) Assert.Equal(normal.Group.Plan.Changes.Single().After, result.Bounds);
        else Assert.Equal(scene.Posters.First(p => p.PosterId == "C").Bounds, result.Bounds);
        Assert.True(s.Complete);
    }

    [Fact]
    public void ActualFootprintBlocksNarrowGapUntilShrinkingMakesItSafeAndReleaseTerminates()
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 400),
             TitleSpatialTestData.P("upper", 400, 0, 400, preferred: 500) with { IsLocked = true },
             TitleSpatialTestData.P("lower", 400, 500, 500, preferred: 500) with { IsLocked = true }]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial, paused: false, fraction: .5), TitleSpatialTestData.Metrics);
        Assert.True(s.Presented.Posters.Single(p => p.PosterId == "C").Bounds.Height > 100);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 400));
        Assert.True(s.Presented.Posters.Single(p => p.PosterId == "C").Bounds.Right <= 400);
        for (int i = 0; i < 20; i++)
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 400));
        TitleSpatialTestData.Release(s, 400);
        var compact = s.Presented.Posters.Single(p => p.PosterId == "C");
        Assert.Equal(new(400, 400, 240, 60), compact.Bounds); Assert.True(compact.IsCollapsed);
        var next = SpatialContinuitySolver.Plan(s.Presented, "C", false, 60, "retry-expand", TitleSpatialTestData.Metrics, s.Continuity);
        Assert.Equal(SpatialExpansionOutcome.InsufficientSpace, next.Outcome);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ChainAndBranchMergePreservePeerHomes(bool branch)
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, branch
            ? [TitleSpatialTestData.P("C", 0, 0, width: 120), TitleSpatialTestData.P("R", 120, 0, width: 120),
               TitleSpatialTestData.P("B", 0, 60), TitleSpatialTestData.P("A", 0, 120)]
            : [TitleSpatialTestData.P("C", 0, 0), TitleSpatialTestData.P("B", 0, 60), TitleSpatialTestData.P("A", 0, 120)]);
        var m = new SpatialContinuityTests.Model(scene);
        m.Apply("C", false); if (branch) m.Apply("R", false); m.Apply("B", false);
        var start = TitleSpatialTestData.Start(m.Scene, paused: true, fraction: .3, continuity: m.State);
        var s = new TitleSpatialSession(start, TitleSpatialTestData.Metrics, m.State);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 600));
        TitleSpatialTestData.Release(s, 600);
        Assert.Equal(60, s.Continuity.Nodes.Single(n => n.Expected.PosterId == "B").HomeY);
        Assert.Equal(120, s.Continuity.Nodes.Single(n => n.Expected.PosterId == "A").HomeY);
        Assert.False(s.Presented.Posters.Single(p => p.PosterId == "B").IsCollapsed);
        m.Scene = s.Presented; m.State = s.Continuity;
        if (branch) m.Apply("R", true);
        m.Apply("B", true);
        Assert.Equal(60, m.Find("B").Bounds.Y); Assert.Equal(120, m.Find("A").Bounds.Y);
    }

    [Theory] [InlineData("move")] [InlineData("lock")] [InlineData("roundtrip")] [InlineData("obstacle")]
    public void UserInterventionAndNewObstaclesDoNotBecomePartialOrUnrelatedReturns(string kind)
    {
        var m = new SpatialContinuityTests.Model(new(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 0, width: 120), TitleSpatialTestData.P("B", 0, 60), TitleSpatialTestData.P("A", 0, 120)]));
        m.Apply("C", false);
        if (kind == "obstacle") m.Scene = new(m.Scene.Environment, m.Scene.Posters.Append(
            TitleSpatialTestData.P("obstacle", 120, 180, 100, width: 120) with { IsLocked = true }));
        else m.Scene = new(m.Scene.Environment, m.Scene.Posters.Select(p => p.PosterId == "B" ? p with {
            Bounds = kind == "move" ? p.Bounds with { X = 700 } : p.Bounds,
            IsLocked = kind == "lock", LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = "user" } : p));
        var old = m.Find("B");
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(m.Scene, paused: true, fraction: 0, continuity: m.State),
            TitleSpatialTestData.Metrics, m.State);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, -10));
        TitleSpatialTestData.Release(s, -10);
        Assert.Equal(old.Bounds, s.Presented.Posters.Single(p => p.PosterId == "B").Bounds);
    }

    [Theory] [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(192)]
    public void NegativeOriginsAndReverseDraggingDoNotAccumulateDrift(int dpi)
    {
        int compact = 60 * dpi / 96;
        var env = TitleSpatialTestData.Env with { WorkArea = new(-1800, -1000, 3000, 2200), DpiX = (uint)dpi, DpiY = (uint)dpi };
        var scene = new SpatialSnapshot(env, [TitleSpatialTestData.P("C", -1000, -500, compact, preferred: 360 * dpi / 96) with { IsCollapsed = true }]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene), TitleSpatialTestData.Metrics);
        for (int i = 0; i < 100; i++)
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, i % 2 == 0 ? 30 : 0, i % 2 == 0 ? 15 : 0));
        TitleSpatialTestData.Release(s);
        Assert.Equal(scene.Posters.Single().Bounds, s.Presented.Posters.Single().Bounds);
        Assert.True(s.HasUserMoved); Assert.True(s.Complete);
    }

    [Fact]
    public void CompletedConstrainedExpansionDragsWithoutShrinking()
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 0, 100, 220)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial), TitleSpatialTestData.Metrics);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 100));
        TitleSpatialTestData.Release(s, 100);
        Assert.Equal(220, s.Presented.Posters.Single().Bounds.Height); Assert.False(s.Presented.Posters.Single().IsCollapsed);
    }

    [Fact]
    public void PendingDoesNotShrinkAndStaleForeignAndFailedProposalsAreNotAccepted()
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 0, 0)]);
        var start = TitleSpatialTestData.Start(initial, paused: false);
        var s = new TitleSpatialSession(start, TitleSpatialTestData.Metrics);
        var original = TitleSpatialTestData.Geometry(s.Presented);
        for (int i = 0; i < 1000; i++) TitleSpatialTestData.Accept(s, new());
        Assert.Equal(0, s.SampleCount); Assert.Equal(original, TitleSpatialTestData.Geometry(s.Presented));
        var old = s.Propose(TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30))!;
        var latest = s.Propose(TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 50))!;
        Assert.False(s.Accept(old, true));
        var other = new TitleSpatialSession(start, TitleSpatialTestData.Metrics);
        Assert.False(other.Accept(latest, true));
        Assert.False(s.Accept(latest, false)); Assert.True(s.Cancelled);
        Assert.Equal(original, TitleSpatialTestData.Geometry(s.Presented));
        Assert.Throws<InvalidOperationException>(() => s.Freeze());
        Assert.False(s.Accept(latest, true)); Assert.Null(s.Propose(new()));
    }

    [Fact]
    public void MotionBudgetReservesReleaseAndFiniteCompactCompletion()
    {
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(new(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 0)]), paused: false), TitleSpatialTestData.Metrics);
        for (int i = 0; i < s.MotionBudget; i++)
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, i % 2 == 0 ? 30 : 0));
        Assert.Null(s.Propose(TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 80)));
        TitleSpatialTestData.Release(s, 80);
        Assert.True(s.Complete); Assert.Equal(60, s.Presented.Posters.Single().Bounds.Height);
        Assert.True(s.SampleCount <= TitleSpatialSession.MaximumSamples + 1);
        Assert.True(TitleSpatialSession.Replay(s.Freeze(), TitleSpatialTestData.Metrics).Complete);
    }
}
