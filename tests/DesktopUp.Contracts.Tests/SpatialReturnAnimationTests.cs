using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialReturnAnimationTests
{
    private static readonly SpatialEnvironment Environment = TestEnvironment with { WorkArea = new(0, 0, 1200, 1400) };

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 12)]
    [InlineData(true, 0)]
    [InlineData(true, 12)]
    public void TwoCompactPostersReturnInTheSameFramesAsTheSource(bool upward, int gap)
    {
        int sourceY = upward ? 1340 : 0;
        int peerY = upward ? sourceY - 60 - gap : sourceY + 60 + gap;
        var initial = new SpatialSnapshot(Environment, [Compact("B", sourceY), Compact("A", peerY)]);
        var (expanded, current, collapsed) = RoundTrip(initial, "B");
        Assert.Single(expanded.NewProof!.Participants);
        Assert.All(collapsed.Frames, frame => Assert.Contains(frame.Changes, c => c.PosterId == "B"));
        Assert.Contains(collapsed.Frames[0].Changes, c => c.PosterId == "A");
        CheckFrames(current, collapsed, bounds =>
        {
            long distance = upward ? bounds["B"].Y - bounds["A"].Bottom : bounds["A"].Y - bounds["B"].Bottom;
            Assert.InRange(distance, 0L, gap);
        });
        AssertReturned(initial, collapsed);
    }

    [Fact]
    public void ChainReturnsTogetherRegardlessOfIdsInsteadOfOneNeighborAtATime()
    {
        // IDs deliberately oppose the spatial dependency order.
        var initial = new SpatialSnapshot(Environment, [Compact("source", 0), Compact("z-near", 60), Compact("a-far", 120)]);
        var (_, current, collapsed) = RoundTrip(initial, "source");
        Assert.Equal(3, collapsed.Frames[0].Changes.Count);
        Assert.All(collapsed.Frames, f => Assert.Equal(3, f.Changes.Count));
        CheckFrames(current, collapsed, bounds =>
        {
            Assert.Equal(bounds["source"].Bottom, bounds["z-near"].Y);
            Assert.Equal(bounds["z-near"].Bottom, bounds["a-far"].Y);
        });
        AssertReturned(initial, collapsed);
        var reordered = SpatialPushSolver.Plan(new(current.Environment, current.Posters.Reverse()), "source", true, 60,
            "collapse", SpatialPushSolver.Plan(initial, "source", false, 60, "expand").NewProof);
        Assert.Equal(collapsed.Frames.SelectMany(f => f.Changes), reordered.Frames.SelectMany(f => f.Changes));
    }

    [Fact]
    public void TwoLegCollapseReleasesAboveThenBelowAndFinishesWithoutATail()
    {
        var initial = new SpatialSnapshot(Environment, [Compact("source", 900), Compact("above", 840)]);
        var (_, current, collapsed) = RoundTrip(initial, "source");
        Assert.Contains(collapsed.Frames[0].Changes, c => c.PosterId == "above");
        Assert.All(collapsed.Frames, f => Assert.Contains(f.Changes, c => c.PosterId == "source"));
        CheckFrames(current, collapsed, bounds => Assert.InRange(bounds["source"].Y - bounds["above"].Bottom, 0L, 0L));
        AssertReturned(initial, collapsed);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("adjusted-back")]
    [InlineData("new-obstacle")]
    public void IneligibleOrBlockedPeerStaysPutThroughoutCollapse(string reason)
    {
        var peer = Compact("peer", 60);
        if (reason == "new-obstacle") peer = peer with { Bounds = peer.Bounds with { Width = 300 } };
        var initial = new SpatialSnapshot(Environment, [Compact("source", 0), peer]);
        var expanded = SpatialPushSolver.Plan(initial, "source", false, 60, "expand");
        var current = After(initial, expanded, "expand");
        current = new(current.Environment, current.Posters.Select(p => p.PosterId != "peer" ? p : reason switch
        {
            "lock" => p with { IsLocked = true },
            "adjusted-back" => p with { LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = "manual" },
            _ => p
        }).Concat(reason == "new-obstacle" ? [Poster("obstacle", 240, 300, 30, 60) with { IsLocked = true }] : []));
        var collapsed = SpatialPushSolver.Plan(current, "source", true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded);
        Assert.Single(collapsed.Plan.Changes);
        Assert.All(collapsed.Frames, f => Assert.DoesNotContain(f.Changes, c => c.PosterId == "peer"));
        CheckFrames(current, collapsed, _ => { });
    }

    [Fact]
    public void ReleasedPeerReturnsSmoothlyWhenAnotherPeerWasMovedAside()
    {
        var initial = new SpatialSnapshot(Environment, [Compact("source", 0), Compact("near", 60), Compact("far", 120)]);
        var expanded = SpatialPushSolver.Plan(initial, "source", false, 60, "expand");
        var current = After(initial, expanded, "expand");
        current = new(Environment, current.Posters.Select(p => p.PosterId == "near" ? p with
            { Bounds = p.Bounds with { X = 500 }, LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = "manual" } : p));
        var collapsed = SpatialPushSolver.Plan(current, "source", true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded);
        Assert.DoesNotContain(collapsed.Plan.Changes, c => c.PosterId == "near");
        Assert.Equal(120, collapsed.Plan.Changes.Single(c => c.PosterId == "far").After.Y);
        Assert.All(collapsed.Frames, f => Assert.Contains(f.Changes, c => c.PosterId == "source"));
        CheckFrames(current, collapsed, _ => { });
    }

    [Fact]
    public void SeededMixedHeightChainsPreserveContinuousSafetyAndSourceFrameCount()
    {
        var random = new Random(70407);
        var environment = Environment with { WorkArea = new(-500, -1000, 2000, 20000) };
        for (int sample = 0; sample < 200; sample++)
        {
            bool upward = sample % 2 == 0;
            int sourceY = upward ? (int)environment.WorkArea.Bottom - 60 : environment.WorkArea.Y;
            var source = Compact("source", sourceY) with { ExpandedHeight = random.Next(300, 1000) };
            var posters = new List<SpatialPoster> { source };
            int edge = upward ? sourceY : sourceY + 60;
            int count = random.Next(2, 7);
            for (int i = 0; i < count; i++)
            {
                bool compact = random.Next(2) == 0;
                int height = compact ? 60 : random.Next(240, 400), gap = random.Next(0, 35);
                int y = upward ? edge - gap - height : edge + gap;
                posters.Add(Poster($"peer-{count - i}", 0, y, 240, height) with
                    { IsCollapsed = compact, ExpandedHeight = compact ? 480 : height });
                edge = upward ? y : y + height;
            }
            var initial = new SpatialSnapshot(environment, posters);
            var (expanded, current, collapsed) = RoundTrip(initial, "source");
            var solo = SpatialPostureSolver.Plan(new(environment, current.Posters.Where(p => p.PosterId == "source")),
                "source", true, 60, expanded.NewProof!.CompactSource.Bounds);
            Assert.Equal(SpatialPostureSolver.Frames(solo).Count, collapsed.Frames.Count);
            CheckFrames(current, collapsed, _ => { });
            AssertReturned(initial, collapsed);
        }
    }

    private static SpatialPoster Compact(string id, int y) => Poster(id, 0, y, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 };
    private static (SpatialGroupPlan Expanded, SpatialSnapshot Current, SpatialGroupPlan Collapsed) RoundTrip(SpatialSnapshot initial, string source)
    {
        var expanded = SpatialPushSolver.Plan(initial, source, false, 60, "expand");
        Assert.True(expanded.Plan.Succeeded);
        var current = After(initial, expanded, "expand");
        var collapsed = SpatialPushSolver.Plan(current, source, true, 60, "collapse", expanded.NewProof);
        Assert.True(collapsed.Plan.Succeeded);
        return (expanded, current, collapsed);
    }
    private static SpatialSnapshot After(SpatialSnapshot initial, SpatialGroupPlan group, string operation) => new(initial.Environment,
        initial.Posters.Select(p => group.Plan.Changes.FirstOrDefault(c => c.PosterId == p.PosterId) is { } change
            ? p with { Bounds = change.After, IsCollapsed = change.IsCollapsed, LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = operation } : p));
    private static void AssertReturned(SpatialSnapshot initial, SpatialGroupPlan collapsed) => Assert.All(collapsed.Plan.Changes,
        c => Assert.Equal(initial.Posters.Single(p => p.PosterId == c.PosterId).Bounds, c.After));
    private static void CheckFrames(SpatialSnapshot initial, SpatialGroupPlan group, Action<IReadOnlyDictionary<string, SpatialRect>> check)
    {
        var current = initial.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
        foreach (var frame in group.Frames)
        {
            var before = new Dictionary<string, SpatialRect>(current);
            foreach (var change in frame.Changes)
            {
                Assert.Equal(before[change.PosterId], change.From);
                Assert.InRange(Math.Abs((long)change.To.Y - change.From.Y), 0, 32);
                current[change.PosterId] = change.To;
                Assert.True(initial.Environment.WorkArea.Contains(change.To));
            }
            var ids = current.Keys.ToArray();
            for (int i = 0; i < ids.Length; i++) for (int j = i + 1; j < ids.Length; j++)
                Assert.False(SpatialPathPolicy.CrossesMoving(before[ids[i]], current[ids[i]], before[ids[j]], current[ids[j]]));
            check(current);
        }
    }
}
