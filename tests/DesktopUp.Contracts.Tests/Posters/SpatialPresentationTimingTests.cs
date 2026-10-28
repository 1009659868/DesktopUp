using DesktopUp.Application;
using DesktopUp.Domain;
namespace DesktopUp.Contracts.Tests;

public sealed class SpatialPresentationTimingTests
{
    [Fact]
    public void EveryCoalescedPrefixCanTransferToTheExistingTitleKernel()
    {
        var initial = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 0, 0)]);
        var metrics = TitleSpatialTestData.Metrics; var options = new PosterAnimationOptions(4, 8, 160);
        var plan = SpatialContinuitySolver.Plan(initial, "C", false, 60, "auto-before-title", metrics, null);
        var schedule = SpatialAnimationSchedule.CreatePresentation(initial, plan.Group.Frames, options);
        var cursor = new SpatialAnimationCursor(schedule, options); var presented = initial;
        for (int time = 16; time <= 1000 && !cursor.Complete; time += 16)
        {
            if (!cursor.TryTake(time, out var frame)) continue;
            presented = TitleSpatialTestData.Apply(presented, [frame!]);
            if (cursor.Complete) break;
            var basis = TitleSpatialTestData.Start(initial);
            var r = presented.Posters.Single().Bounds;
            var press = basis.Press with { Posture = TitlePresentedPosture.Expanding,
                Capture = basis.Press.Capture with { Press = basis.Press.Capture.Press with { Frame = new(r.X,r.Y,r.Width,r.Height) } } };
            var session = new TitleSpatialSession(new(initial, press, options, new("auto-before-title", false, cursor.Index)), metrics, null);
            Assert.Equal(r, session.Presented.Posters.Single().Bounds);
        }
        Assert.True(cursor.Complete);
    }
    [Theory] [InlineData(16)] [InlineData(33)] [InlineData(90)]
    public void PresentationIsTimePacedWhileKeepingExactSafeProofPrefixes(int callbackMs)
    {
        var model = new SpatialContinuityTests.Model(SpatialContinuityTests.Chain());
        foreach (var op in new[] { ("C", false), ("B", false), ("C", true), ("B", true) })
        {
            var initial = model.Scene; var group = model.Apply(op.Item1, op.Item2).Group;
            var options = new PosterAnimationOptions(4, 8, 180);
            var proof = SpatialAnimationSchedule.Create(initial, group.Frames, options);
            var timed = SpatialAnimationSchedule.CreatePresentation(initial, group.Frames, options);
            Assert.Equal(proof.Frames.Count, timed.Frames.Count);
            var cursor = new SpatialAnimationCursor(timed, options);
            var frames = new List<SpatialBatchFrame>(); int prior = 0;
            for (int time = 0; time <= 5000 && !cursor.Complete; time += callbackMs)
                if (cursor.TryTake(time, out var frame))
                {
                    Assert.DoesNotContain(timed.SegmentEnds, e => e > prior && e < cursor.Index);
                    Assert.False(cursor.TryTake(time, out _));
                    frames.Add(frame!); prior = cursor.Index;
                }
            Assert.True(cursor.Complete);
            Assert.True(frames.Count <= Math.Ceiling(180d / callbackMs) + timed.SegmentEnds.Count + 1);
            SpatialContinuityTests.AssertSafety(initial, group with { Frames = frames }, op.Item1);
            var presented = initial.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
            foreach (var frame in frames) foreach (var change in frame.Changes) presented[change.PosterId] = change.To;
            foreach (var change in group.Plan.Changes) Assert.Equal(change.After, presented[change.PosterId]);
        }
    }
}
