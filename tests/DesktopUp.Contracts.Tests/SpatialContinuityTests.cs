using DesktopUp.Domain;
using System.Text.Json;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialContinuityTests
{
    internal static readonly PosterContentMetrics Metrics = new("continuity", 40, 80, 30);
    internal static readonly SpatialEnvironment Environment = new("test", new(0, 0, 1000, 1600), 96, 96, 1, 1, Guid.NewGuid());
    internal static SpatialPoster P(string id, int x, int y, int width = 240, int height = 60, int preferred = 360) =>
        new(id, new(x, y, width, height), preferred, true, false, height == 60, 0);
    internal static SpatialSnapshot Chain() => new(Environment, [P("C", 0, 0), P("B", 0, 60), P("A", 0, 120)]);

    internal sealed class Model(SpatialSnapshot initial)
    {
        public SpatialSnapshot Scene = initial;
        public SpatialContinuityState? State;
        private int _serial;
        public SpatialPoster Find(string id) => Scene.Posters.Single(p => p.PosterId == id);
        public SpatialContinuityResult Apply(string id, bool collapsed)
        {
            string op = "op-" + ++_serial;
            string before = JsonSerializer.Serialize(Scene);
            var result = SpatialContinuitySolver.Plan(Scene, id, collapsed, 60, op, Metrics, State);
            Assert.Equal(before, JsonSerializer.Serialize(Scene));
            if (!result.Group.Plan.Succeeded) { Assert.Null(result.Next); Assert.Empty(result.Group.Frames); return result; }
            AssertSafety(Scene, result.Group, id);
            var changes = result.Group.Plan.Changes.ToDictionary(c => c.PosterId);
            Scene = new(Scene.Environment, Scene.Posters.Select(p => changes.TryGetValue(p.PosterId, out var c) && c.Before != c.After
                ? p with { Bounds = c.After, IsCollapsed = c.IsCollapsed, LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = op } : p));
            State = result.Next;
            Assert.True(State!.Nodes.Count <= Scene.Posters.Count);
            Assert.Empty(PosterSpatialSolver.ValidateLayout(Scene));
            return result;
        }
    }

    internal static void AssertSafety(SpatialSnapshot start, SpatialGroupPlan group, string source)
    {
        var current = start.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
        foreach (var frame in group.Frames)
        {
            var next = new Dictionary<string, SpatialRect>(current);
            Assert.Equal(frame.Changes.Count, frame.Changes.Select(c => c.PosterId).Distinct().Count());
            foreach (var change in frame.Changes)
            {
                Assert.Equal(current[change.PosterId], change.From);
                Assert.True(start.Environment.WorkArea.Contains(change.To)); next[change.PosterId] = change.To;
            }
            var visible = start.Posters.Where(p => p.IsVisible).ToArray();
            for (int i = 0; i < visible.Length; i++)
            for (int j = i + 1; j < visible.Length; j++)
                Assert.False(SpatialPathPolicy.CrossesMoving(current[visible[i].PosterId], next[visible[i].PosterId],
                    current[visible[j].PosterId], next[visible[j].PosterId]));
            current = next;
        }
        foreach (var change in group.Plan.Changes)
        {
            Assert.Equal(change.After, current[change.PosterId]);
            if (change.PosterId == source) continue;
            Assert.False(start.Posters.Single(p => p.PosterId == change.PosterId).IsLocked);
            Assert.Equal(change.Before.Height, change.After.Height); Assert.Equal(change.Before.Width, change.After.Width);
            Assert.Equal(change.WasCollapsed, change.IsCollapsed);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedPosturesCloseInEitherOrderWithoutDrift(bool innerFirst)
    {
        var m = new Model(Chain()); var before = m.Scene.Posters.Select(p => p.Bounds).ToArray();
        for (int i = 0; i < 20; i++)
        {
            Assert.True(m.Apply("C", false).Group.Plan.Succeeded); Assert.True(m.Apply("B", false).Group.Plan.Succeeded);
            Assert.True(m.Apply(innerFirst ? "B" : "C", true).Group.Plan.Succeeded);
            if (!innerFirst) { Assert.Equal(60, m.Find("B").Bounds.Y); Assert.Equal(420, m.Find("A").Bounds.Y); Assert.False(m.Find("B").IsCollapsed); }
            Assert.True(m.Apply(innerFirst ? "C" : "B", true).Group.Plan.Succeeded);
            Assert.Equal(before, m.Scene.Posters.Select(p => p.Bounds));
        }
    }

    [Fact]
    public void BranchMergeSharesConstraintsAndNeverAddsDisplacements()
    {
        var m = new Model(new(Environment, [P("L", 0, 0, 120), P("R", 120, 0, 120), P("B", 0, 60, 240), P("A", 0, 120, 240)]));
        var initial = m.Scene.Posters.Select(p => p.Bounds).ToArray();
        m.Apply("L", false); m.Apply("R", false); m.Apply("B", false);
        m.Apply("L", true); Assert.Equal(360, m.Find("B").Bounds.Y);
        m.Apply("R", true); Assert.Equal(60, m.Find("B").Bounds.Y); Assert.Equal(420, m.Find("A").Bounds.Y);
        m.Apply("B", true); Assert.Equal(initial, m.Scene.Posters.Select(p => p.Bounds));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UpwardAndTwoLegExpansionReturnTheirTranslatedCompactAnchor(bool twoLeg)
    {
        var scene = new SpatialSnapshot(Environment with { WorkArea = new(0, 0, 1000, 900) },
            [P("C", 0, twoLeg ? 700 : 840), P("B", 0, 640), P("A", 0, 580)]);
        var m = new Model(scene); var original = scene.Posters.Select(p => p.Bounds).ToArray();
        Assert.True(m.Apply("C", false).Group.Plan.Succeeded);
        Assert.True(m.Apply("B", false).Group.Plan.Succeeded);
        Assert.True(m.Apply("C", true).Group.Plan.Succeeded);
        Assert.True(m.Apply("B", true).Group.Plan.Succeeded);
        Assert.Equal(original, m.Scene.Posters.Select(p => p.Bounds));
    }

    [Fact]
    public void NewObstacleBlocksEntireReturnRatherThanAcceptingPartialMotion()
    {
        var m = new Model(new(Environment, [P("C", 0, 0, 120), P("B", 0, 60), P("A", 0, 120)]));
        m.Apply("C", false); var oldB = m.Find("B").Bounds;
        m.Scene = new(m.Scene.Environment, m.Scene.Posters.Append(P("obstacle", 120, 180, 120, height: 100) with { IsLocked = true }));
        var result = m.Apply("C", true);
        Assert.True(result.Group.Plan.Succeeded); Assert.Equal(oldB, m.Find("B").Bounds);
        Assert.DoesNotContain(result.Group.Plan.Changes, c => c.PosterId is "B" or "A");
    }

    [Theory]
    [InlineData("move")] [InlineData("roundtrip")] [InlineData("lock")] [InlineData("resize")]
    public void ExplicitInterventionReanchorsOnlyThatEntity(string kind)
    {
        var m = new Model(Chain()); m.Apply("C", false);
        m.Scene = new(m.Scene.Environment, m.Scene.Posters.Select(p => p.PosterId != "B" ? p : p with {
            Bounds = kind == "move" ? p.Bounds with { X = 500 } : kind == "resize" ? p.Bounds with { Width = 200 } : p.Bounds,
            IsLocked = kind == "lock", LayoutGeneration = p.LayoutGeneration + 1, LastLayoutOperationId = "user" }));
        var before = m.Find("B"); m.Apply("C", true); Assert.Equal(before, m.Find("B"));
        if (kind == "move") Assert.Equal(120, m.Find("A").Bounds.Y);
    }

    [Fact]
    public void EnvironmentChangeDoesNotReplayOldPixelAnchors()
    {
        var m = new Model(Chain()); m.Apply("C", false);
        m.Scene = new(m.Scene.Environment with { ShellGeneration = 2 }, m.Scene.Posters);
        var before = m.Find("B").Bounds; m.Apply("C", true); Assert.Equal(before, m.Find("B").Bounds);
    }

    [Fact]
    public void LongSeededSequencesAreSafeBoundedAndEnumerationIndependent()
    {
        for (int seed = 0; seed < 12; seed++)
        {
            var random = new Random(seed); var a = new Model(Chain()); var b = new Model(new(Environment, Chain().Posters.Reverse()));
            for (int i = 0; i < 80; i++)
            {
                string id = new[] { "A", "B", "C" }[random.Next(3)]; bool collapse = !a.Find(id).IsCollapsed;
                var x = a.Apply(id, collapse); var y = b.Apply(id, collapse);
                Assert.Equal(x.Group.Plan.Status, y.Group.Plan.Status);
                Assert.Equal(a.Scene.Posters, b.Scene.Posters); Assert.True(x.Group.Frames.Count < 4096);
            }
            foreach (var id in new[] { "A", "B", "C" }) if (!a.Find(id).IsCollapsed) a.Apply(id, true);
            Assert.Equal(Chain().Posters.Select(p => p.Bounds), a.Scene.Posters.Select(p => p.Bounds));
        }
    }

    [Fact]
    public void SixNodeMixedHeightChainsCloseUnderDifferentPermutations()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var rng = new Random(seed + 100);
            var posters = Enumerable.Range(0, 6).Select(i => P("P" + i, 0, i * 70 + 10, preferred: 180 + rng.Next(5) * 70)).ToArray();
            var m = new Model(new(Environment, posters));
            foreach (var p in posters.OrderBy(_ => rng.Next())) m.Apply(p.PosterId, false);
            foreach (var p in posters.OrderBy(_ => rng.Next())) if (!m.Find(p.PosterId).IsCollapsed)
                Assert.True(m.Apply(p.PosterId, true).Group.Plan.Succeeded);
            Assert.True(posters.Select(p => p.Bounds).SequenceEqual(m.Scene.Posters.Select(p => p.Bounds)),
                "seed=" + seed + " final=" + string.Join(';', m.Scene.Posters.Select(p => p.PosterId + ":" + p.Bounds.Y)));
        }
    }

    [Fact]
    public void ForkedSourceReleasesBothBranchesAndTheirNestedPostures()
    {
        var m = new Model(new(Environment, [P("S", 0, 0, 480), P("L", 0, 60), P("R", 240, 60),
            P("J", 0, 120, 480)]));
        var initial = m.Scene.Posters.Select(p => p.Bounds).ToArray();
        foreach (string id in new[] { "S", "L", "R", "J" }) Assert.True(m.Apply(id, false).Group.Plan.Succeeded);
        foreach (string id in new[] { "S", "R", "L", "J" }) Assert.True(m.Apply(id, true).Group.Plan.Succeeded);
        Assert.Equal(initial, m.Scene.Posters.Select(p => p.Bounds));
    }

    [Fact]
    public void OneBlockedBranchDoesNotPreventTheOtherCompleteReturn()
    {
        var m = new Model(new(Environment, [P("S", 100, 0, 280), P("L", 0, 60), P("R", 240, 60)]));
        m.Apply("S", false);
        m.Scene = new(Environment, m.Scene.Posters.Append(P("wall", 0, 180, 100, 100) with { IsLocked = true }));
        m.Apply("S", true);
        Assert.Equal(360, m.Find("L").Bounds.Y); Assert.Equal(60, m.Find("R").Bounds.Y);
    }

    [Fact]
    public void ConstrainedHeightDoesNotGrowWhenSpaceIsReleased()
    {
        var m = new Model(new(Environment with { WorkArea = new(0, 0, 1000, 650) },
            [P("C", 0, 0), P("B", 0, 60), P("A", 0, 120)]));
        m.Apply("C", false); var expansion = m.Apply("B", false);
        Assert.Equal(SpatialExpansionOutcome.Constrained, expansion.Outcome);
        int height = m.Find("B").Bounds.Height;
        m.Apply("C", true); Assert.Equal(height, m.Find("B").Bounds.Height);
        Assert.Equal(360, m.Find("B").ExpandedHeight);
        m.Apply("B", true); Assert.Equal(Chain().Posters.Select(p => p.Bounds), m.Scene.Posters.Select(p => p.Bounds));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RemovedOrHiddenNodeCannotRetainAStaleReturn(bool hidden)
    {
        var m = new Model(Chain()); m.Apply("C", false);
        m.Scene = new(Environment, hidden ? m.Scene.Posters.Select(p => p.PosterId == "B" ? p with {
            IsVisible = false, LayoutGeneration = p.LayoutGeneration + 1 } : p) : m.Scene.Posters.Where(p => p.PosterId != "B"));
        m.Apply("C", true); Assert.Equal(120, m.Find("A").Bounds.Y);
        if (!hidden) Assert.DoesNotContain(m.State!.Nodes, n => n.Expected.PosterId == "B");
    }

    [Fact]
    public void NoChangeAndRejectedPlansDoNotInventHistory()
    {
        var scene = Chain();
        var noChange = SpatialContinuitySolver.Plan(scene, "C", true, 60, "same", Metrics);
        Assert.Equal(SpatialPlanStatus.NoChange, noChange.Group.Plan.Status); Assert.Empty(noChange.Group.Frames);
        Assert.All(noChange.Next!.Nodes, n => Assert.Equal(0, n.Expected.LayoutGeneration));
        var missing = SpatialContinuitySolver.Plan(scene, "missing", false, 60, "bad", Metrics);
        Assert.Null(missing.Next); Assert.Empty(missing.Group.Frames);
        var malformed = new SpatialContinuityState(Environment, [noChange.Next.Nodes[0], noChange.Next.Nodes[0]]);
        Assert.Null(SpatialContinuitySolver.Plan(scene, "C", false, 60, "bad", Metrics, malformed).Next);
        var large = new SpatialSnapshot(Environment with { WorkArea = new(0, 0, 1000, 20000) },
            Enumerable.Range(0, 257).Select(i => P("p" + i, 0, i * 60)));
        Assert.Equal(SpatialFailure.BudgetExceeded,
            SpatialContinuitySolver.Plan(large, "p0", false, 60, "large", Metrics).Group.Plan.Issues.Single().Reason);
    }

    [Fact]
    public void MixedHorizontalOverlapSequencesReturnWithoutHistoryDrift()
    {
        for (int seed = 0; seed < 160; seed++)
        {
            var rng = new Random(seed + 700);
            var initial = Enumerable.Range(0, 6).Select(i => P("P" + i, rng.Next(3) * 100,
                30 + i * 80, (1 + rng.Next(3)) * 100, preferred: 180 + rng.Next(6) * 60)).ToArray();
            var m = new Model(new(Environment with { WorkArea = new(0, 0, 1000, 1200) }, initial));
            var sequence = new List<string>();
            for (int i = 0; i < 28; i++)
            {
                var id = "P" + rng.Next(6); bool collapse = !m.Find(id).IsCollapsed;
                var result = m.Apply(id, collapse); sequence.Add(id + (collapse ? "-" : "+") + (result.Group.Plan.Succeeded ? "" : "!"));
            }
            foreach (var p in initial.OrderBy(_ => rng.Next())) if (!m.Find(p.PosterId).IsCollapsed)
            { m.Apply(p.PosterId, true); sequence.Add(p.PosterId + "-"); }
            Assert.True(initial.Select(p => p.Bounds).SequenceEqual(m.Scene.Posters.Select(p => p.Bounds)),
                "seed=" + seed + " initial=" + string.Join(';', initial) + " steps=" + string.Join(',', sequence) + " final=" +
                string.Join(';', m.Scene.Posters.Select(p => p.PosterId + ":" + p.Bounds.Y)));
        }
    }

    [Fact]
    public void OlderDisplacementMayExceedTheCurrentSourceHeightRelease()
    {
        var source = P("C", 0, 0, height: 180, preferred: 180);
        var peer = P("B", 0, 600);
        var m = new Model(new(Environment, [source, peer])) {
            State = new(Environment, [new(source, 0, 0, "chain"), new(peer, 60, 0, "chain")]) };
        var result = m.Apply("C", true);
        Assert.True(result.Group.Plan.Succeeded); Assert.Equal(60, m.Find("B").Bounds.Y);
        Assert.All(result.Group.Frames, frame => Assert.Contains(frame.Changes, c => c.PosterId == "C"));
    }
}
