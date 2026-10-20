using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialAnimationTests
{
    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(8)] [InlineData(12)] [InlineData(32)] [InlineData(64)]
    public void RefinementKeepsEndpointsSafetyAndContactForBothNestedOrders(int pixels)
    {
        foreach (bool innerFirst in new[] { false, true })
        {
            var model = new SpatialContinuityTests.Model(SpatialContinuityTests.Chain());
            foreach (var op in new[] { ("C", false), ("B", false), (innerFirst ? "B" : "C", true), (innerFirst ? "C" : "B", true) })
            {
                var initial = model.Scene; var group = model.Apply(op.Item1, op.Item2).Group;
                var schedule = SpatialAnimationSchedule.Create(initial, group.Frames, new(MaximumStepPixels: pixels));
                SpatialContinuityTests.AssertSafety(initial, group with { Frames = schedule.Frames }, op.Item1);
                var positions = initial.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
                double previous = 0;
                for (int i = 0; i < schedule.Frames.Count; i++)
                {
                    foreach (var change in schedule.Frames[i].Changes)
                    {
                        Assert.InRange(SpatialAnimationSchedule.EdgeDistance(change.From, change.To), 1, pixels);
                        positions[change.PosterId] = change.To;
                    }
                    Assert.Equal(positions["C"].Bottom, positions["B"].Y);
                    Assert.Equal(positions["B"].Bottom, positions["A"].Y);
                    Assert.True(schedule.DueMilliseconds[i] - previous >= 16 - .0001);
                    previous = schedule.DueMilliseconds[i];
                }
                Assert.Equal(previous, schedule.NominalDurationMs);
            }
        }
    }

    [Fact]
    public void NegativeCoordinatesAndUpwardTwoLegPathsKeepSharedEdges()
    {
        var original = SpatialContinuityTests.Chain();
        var env = original.Environment with { WorkArea = new(-1000, -1000, 1000, 1000) };
        var scene = new SpatialSnapshot(env, original.Posters.Select(p => p with { Bounds = p.Bounds with { X = -500, Y = p.Bounds.Y - 300 } }));
        var model = new SpatialContinuityTests.Model(scene);
        foreach (var op in new[] { ("A", false), ("B", false), ("A", true), ("B", true) })
        {
            var initial = model.Scene; var group = model.Apply(op.Item1, op.Item2).Group;
            Assert.True(group.Plan.Succeeded);
            var schedule = SpatialAnimationSchedule.Create(initial, group.Frames, new(MaximumStepPixels: 7));
            SpatialContinuityTests.AssertSafety(initial, group with { Frames = schedule.Frames }, op.Item1);
        }
    }

    [Fact]
    public void SeededMixedWidthPathsRemainSafeAfterRounding()
    {
        for (int seed = 0; seed < 16; seed++)
        {
            var random = new Random(seed);
            var model = new SpatialContinuityTests.Model(new(SpatialContinuityTests.Environment,
                Enumerable.Range(0, 6).Select(i => SpatialContinuityTests.P("P" + i,
                    random.Next(3) * 100, i * 160, 120 + random.Next(3) * 100, preferred: 240 + random.Next(3) * 60))));
            for (int n = 0; n < 20; n++)
            {
                string id = "P" + random.Next(6); var initial = model.Scene;
                var group = model.Apply(id, !model.Find(id).IsCollapsed).Group;
                if (!group.Plan.Succeeded) continue;
                var schedule = SpatialAnimationSchedule.Create(initial, group.Frames, new(MaximumStepPixels: 7));
                SpatialContinuityTests.AssertSafety(initial, group with { Frames = schedule.Frames }, id);
            }
        }
    }

    [Fact]
    public void SlowOrDuplicateRenderingCallbacksNeverSkipOrBurst()
    {
        var model = new SpatialContinuityTests.Model(SpatialContinuityTests.Chain()); var initial = model.Scene;
        var options = new PosterAnimationOptions(8, 16, 240);
        var schedule = SpatialAnimationSchedule.Create(initial, model.Apply("C", false).Group.Frames, options);
        var cursor = new SpatialAnimationCursor(schedule, options);
        Assert.False(cursor.TryTake(0, out _)); Assert.False(cursor.TryTake(double.NaN, out _));
        for (int i = 0; i < schedule.Frames.Count; i++)
        {
            double time = 10000 + i * 50;
            Assert.True(cursor.TryTake(time, out var frame)); Assert.Same(schedule.Frames[i], frame);
            Assert.False(cursor.TryTake(time, out _)); Assert.False(cursor.TryTake(time + 15, out _));
        }
        Assert.True(cursor.Complete); Assert.False(cursor.TryTake(99999, out _));
    }

    [Fact]
    public void RejectedOrEmptySchedulesNeverPartiallyMutateInput()
    {
        var scene = SpatialContinuityTests.Chain();
        Assert.Empty(SpatialAnimationSchedule.Create(scene, [], new()).Frames);
        var p = scene.Posters[0];
        var bad = new SpatialBatchFrame([new(p.PosterId, p.Bounds with { Y = 1 }, p.Bounds)]);
        Assert.Throws<ArgumentException>(() => SpatialAnimationSchedule.Create(scene, [bad], new()));
        var env = scene.Environment with { WorkArea = new(0, 0, 1000, 50000) };
        var big = new SpatialBatchFrame([new("C", p.Bounds, p.Bounds with { Height = 20000 })]);
        Assert.Throws<ArgumentException>(() => SpatialAnimationSchedule.Create(new(env, [p]), [big], new(MaximumStepPixels: 1)));
        Assert.Equal(60, p.Bounds.Height);
    }

    [Fact]
    public void PreparationDoesNotClipLiveWindowsAndRenderingSubscriptionIsPaired()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Directory.Build.props"))) folder = folder.Parent;
        Assert.NotNull(folder);
        string visual = File.ReadAllText(Path.Combine(folder.FullName, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.cs"));
        string prepare = visual.Split("public PreparedSpatialFrame? PrepareSpatialFrame")[1].Split("public bool CommitSpatialFrames")[0];
        Assert.DoesNotContain("ApplyVisualRegion", prepare); Assert.DoesNotContain("UpdateItemScrollState", prepare);
        string batch = visual.Split("public bool CommitSpatialFrames")[1].Split("public PreparedSpatialFrame? PreparePostureRestore")[0];
        Assert.True(batch.IndexOf("ApplyPreparedBoundsBatch", StringComparison.Ordinal) < batch.IndexOf("owner.ApplyVisualRegion", StringComparison.Ordinal));
        Assert.Contains("root.UpdateLayout()", batch); Assert.Contains("finally", batch);
        string runtime = File.ReadAllText(Path.Combine(folder.FullName, "src/DesktopUp.App/Runtime/Posters/PosterSpatialRuntime.Posture.cs"));
        Assert.Contains("CompositionTarget.Rendering += run.RenderingHandler", runtime);
        Assert.Contains("CompositionTarget.Rendering -= run.RenderingHandler", runtime);
        Assert.Contains("timer.Tick += WatchPosture", runtime); Assert.DoesNotContain("timer.Tick += AdvancePosture", runtime);
    }
}
