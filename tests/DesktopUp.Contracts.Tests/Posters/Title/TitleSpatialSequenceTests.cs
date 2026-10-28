using DesktopUp.Application;
using DesktopUp.Domain;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleSpatialSequenceTests
{
    [Fact]
    public void UpwardChainTakeoverReturnsPeersToTheirOwnHomesWithoutMovingTheGrabAnchor()
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 0, 940), TitleSpatialTestData.P("B", 0, 880), TitleSpatialTestData.P("A", 0, 820)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene, paused: false), TitleSpatialTestData.Metrics);
        int grabbedY = s.Presented.Posters.Single(p => p.PosterId == "C").Bounds.Y;
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 600));
        TitleSpatialTestData.Release(s, 600);
        Assert.Equal(grabbedY, s.Presented.Posters.Single(p => p.PosterId == "C").Bounds.Y);
        Assert.Equal(880, s.Presented.Posters.Single(p => p.PosterId == "B").Bounds.Y);
        Assert.Equal(820, s.Presented.Posters.Single(p => p.PosterId == "A").Bounds.Y);
    }

    [Fact]
    public void FastClickDuringUpwardCollapsePreservesTheOriginalFutureCompactAnchor()
    {
        var m = new SpatialContinuityTests.Model(new(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 100, 940)]));
        m.Apply("C", false);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(m.Scene, paused: true, continuity: m.State),
            TitleSpatialTestData.Metrics, m.State);
        TitleSpatialTestData.Release(s);
        m.Scene = s.Presented; m.State = s.Continuity;
        m.Apply("C", true);
        Assert.Equal(940, m.Find("C").Bounds.Y);
    }

    [Theory]
    [InlineData("display")] [InlineData("shell")] [InlineData("session")] [InlineData("workarea")]
    [InlineData("dpi")] [InlineData("workspace")] [InlineData("capture")] [InlineData("epoch")]
    public void StaleRuntimeEvidenceInvalidatesPendingAndTerminalCommit(string change)
    {
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(new(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 100, 100)]), paused: false), TitleSpatialTestData.Metrics);
        var proposal = s.Propose(TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30))!;
        var env = s.Start.Initial.Environment; var token = s.Intent.Token;
        switch (change)
        {
            case "display": env = env with { DisplayGeneration = 2 }; break;
            case "shell": env = env with { ShellGeneration = 2 }; break;
            case "session": env = env with { RuntimeSessionId = Guid.NewGuid() }; break;
            case "workarea": env = env with { WorkArea = env.WorkArea with { Height = 950 } }; break;
            case "dpi": env = env with { DpiY = 144 }; break;
            case "workspace": env = env with { WorkspaceId = "other" }; break;
            case "capture": token = token with { CaptureId = Guid.NewGuid() }; break;
            case "epoch": token = token with { EnvironmentEpoch = 2 }; break;
        }
        Assert.False(s.ValidateCurrent(env, token)); Assert.True(s.Cancelled);
        Assert.False(s.Accept(proposal, true));
        Assert.Throws<InvalidOperationException>(() => s.Freeze());
    }

    [Theory]
    [InlineData(TitleInvalidation.CaptureLost)] [InlineData(TitleInvalidation.Hidden)]
    [InlineData(TitleInvalidation.Removed)] [InlineData(TitleInvalidation.Locked)]
    [InlineData(TitleInvalidation.PolicyDisabled)] [InlineData(TitleInvalidation.EnvironmentChanged)]
    [InlineData(TitleInvalidation.Conflict)] [InlineData(TitleInvalidation.PreparationFailed)]
    [InlineData(TitleInvalidation.Disposed)]
    public void CancellationEndsWithoutClickOrCommitAndDoesNotAdoptUnpreparedGeometry(TitleInvalidation reason)
    {
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(new(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 100, 100)]), paused: false), TitleSpatialTestData.Metrics);
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
        var before = TitleSpatialTestData.Geometry(s.Presented);
        var sample = TitleSpatialTestData.Input(s, TitlePointerEventKind.Invalidate);
        TitleSpatialTestData.Accept(s, sample with { Pointer = sample.Pointer! with { Reason = reason } });
        Assert.True(s.Cancelled); Assert.False(s.Complete);
        Assert.Equal(before, TitleSpatialTestData.Geometry(s.Presented));
        Assert.Throws<InvalidOperationException>(() => s.Freeze());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void LockedFailedDragResumesOriginalGroupRatherThanCommittingPartialHeight(bool collapsing)
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env,
            [TitleSpatialTestData.P("C", 100, 100, collapsing ? 360 : 60) with { IsLocked = true }]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial, paused: collapsing), TitleSpatialTestData.Metrics);
        var originX = s.Presented.Posters.Single().Bounds.X;
        TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
        TitleSpatialTestData.Release(s, 30);
        Assert.Equal(originX, s.Presented.Posters.Single().Bounds.X);
        Assert.Equal(collapsing ? 60 : 360, s.Presented.Posters.Single().Bounds.Height);
        Assert.Equal(collapsing, s.Presented.Posters.Single().IsCollapsed);
    }

    [Theory] [InlineData(.1)] [InlineData(.5)] [InlineData(.9)]
    public void ClickDuringCollapseFastExpandsWithoutReversingOrChangingPreferredHeight(double fraction)
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 100, 360)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(initial, paused: true, fraction: fraction), TitleSpatialTestData.Metrics);
        TitleSpatialTestData.Release(s);
        Assert.False(s.Presented.Posters.Single().IsCollapsed);
        Assert.Equal(360, s.Presented.Posters.Single().Bounds.Height);
        Assert.True(s.Complete); Assert.Null(s.Propose(TitleSpatialTestData.Input(s, TitlePointerEventKind.Release)));
    }

    [Fact]
    public void SeededContinuousAcceptedPathsAreDeterministicSafeFiniteAndPreservePeerHomes()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var initial = new SpatialSnapshot(TitleSpatialTestData.Env,
                [TitleSpatialTestData.P("C", 0, 0), TitleSpatialTestData.P("B", 0, 60), TitleSpatialTestData.P("A", 0, 120)]);
            var start = TitleSpatialTestData.Start(initial, paused: false, fraction: .1 + seed % 8 * .1);
            var a = new TitleSpatialSession(start, TitleSpatialTestData.Metrics);
            var b = new TitleSpatialSession(start, TitleSpatialTestData.Metrics);
            var random = new Random(seed); int dx = 0, dy = 0;
            for (int i = 0; i < 100; i++)
            {
                dx = random.Next(-50, 800); dy = random.Next(-100, 600);
                var sample = TitleSpatialTestData.Input(a, TitlePointerEventKind.Move, dx, dy);
                TitleSpatialTestData.Accept(a, sample); TitleSpatialTestData.Accept(b, sample);
                Assert.Equal(TitleSpatialTestData.Geometry(a.Presented), TitleSpatialTestData.Geometry(b.Presented));
                Assert.Equal(60, a.Continuity.Nodes.Single(n => n.Expected.PosterId == "B").HomeY);
                Assert.Equal(120, a.Continuity.Nodes.Single(n => n.Expected.PosterId == "A").HomeY);
            }
            TitleSpatialTestData.Release(a, dx, dy);
            var replay = TitleSpatialSession.Replay(a.Freeze(), TitleSpatialTestData.Metrics);
            Assert.Equal(TitleSpatialTestData.Geometry(a.Presented), TitleSpatialTestData.Geometry(replay.Presented));
            Assert.Equal(JsonSerializer.Serialize(a.Continuity), JsonSerializer.Serialize(replay.Continuity));
            Assert.True(a.Complete); Assert.Equal(60, a.Presented.Posters.Single(p => p.PosterId == "C").Bounds.Height);
            Assert.True(a.SampleCount <= TitleSpatialSession.MaximumSamples + 1);
        }
    }

    [Fact]
    public void RepeatedTakeoverMoveAndReturnCyclesDoNotDrift()
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 100)]);
        var home = scene.Posters.Single().Bounds;
        SpatialContinuityState? continuity = null;
        for (int i = 0; i < 100; i++)
        {
            var start = TitleSpatialTestData.Start(scene, paused: false, fraction: .2 + i % 6 * .1, continuity: continuity);
            var s = new TitleSpatialSession(start, TitleSpatialTestData.Metrics, continuity);
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 0));
            TitleSpatialTestData.Release(s);
            scene = s.Presented; continuity = s.Continuity;
            Assert.Equal(home, scene.Posters.Single().Bounds);
        }
    }
}
