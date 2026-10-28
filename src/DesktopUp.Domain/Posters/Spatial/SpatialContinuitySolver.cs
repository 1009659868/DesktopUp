namespace DesktopUp.Domain;

// Session-bound, bounded state; no window handles and no additive displacement history.
public sealed record SpatialContinuityNode(SpatialPoster Expected, long HomeY, int UpInset, string Cohort);
public sealed record SpatialContinuityState(SpatialEnvironment Environment, IReadOnlyList<SpatialContinuityNode> Nodes,
    int SchemaVersion = 1);
public sealed record SpatialContinuityResult(SpatialGroupPlan Group, SpatialContinuityState? Next,
    SpatialExpansionOutcome? Outcome = null, int MinimumHeight = 0, int EffectiveHeight = 0);

public static class SpatialContinuitySolver
{
    private const int MaximumNodes = 256;
    private const int MaximumFrames = 4096;
    private const int Step = 32;
    private const long MaximumWork = 4_000_000;

    public static SpatialContinuityResult Plan(SpatialSnapshot scene, string sourceId, bool collapsed,
        int compactHeight, string operationId, PosterContentMetrics metrics, SpatialContinuityState? previous = null)
    {
        SpatialContinuityResult Reject(SpatialFailure reason) => new(new(SpatialPlan.Reject(reason, sourceId), []), null);
        var invalid = PosterSpatialSolver.ValidateLayout(scene);
        if (invalid.Count != 0) return new(new(SpatialPlan.Reject(invalid), []), null);
        if (scene.Posters.Count > MaximumNodes) return Reject(SpatialFailure.BudgetExceeded);
        if (string.IsNullOrWhiteSpace(operationId) || compactHeight < 1) return Reject(SpatialFailure.InvalidInput);
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == sourceId);
        if (source is null || !source.IsVisible) return Reject(SpatialFailure.MissingTarget);
        if (previous is not null && (previous.SchemaVersion != 1 || previous.Nodes.Count > MaximumNodes ||
            previous.Nodes.Select(n => n.Expected.PosterId).Distinct(StringComparer.Ordinal).Count() != previous.Nodes.Count))
            return Reject(SpatialFailure.InvalidInput);
        var known = previous?.Environment == scene.Environment
            ? previous.Nodes.ToDictionary(n => n.Expected.PosterId, StringComparer.Ordinal)
            : new Dictionary<string, SpatialContinuityNode>(StringComparer.Ordinal);
        var nodes = scene.Posters.ToDictionary(p => p.PosterId, p =>
            known.TryGetValue(p.PosterId, out var n) && n.Expected == p && n.HomeY >= int.MinValue * 2L &&
                n.HomeY <= int.MaxValue * 2L && n.UpInset >= 0 &&
                n.UpInset <= Math.Max(0, p.Bounds.Height - compactHeight) && !string.IsNullOrWhiteSpace(n.Cohort)
                ? n : Fresh(p), StringComparer.Ordinal);
        if (source.IsCollapsed == collapsed)
            return new(new(SpatialPlan.Create(new(sourceId, source.Bounds, source.Bounds, collapsed, collapsed,
                collapsed ? SpatialChangeCause.Collapse : SpatialChangeCause.Expand), []), []), Freeze(scene.Environment, nodes));

        if (!collapsed)
        {
            var expansion = SpatialExpansionPolicy.Plan(scene, sourceId, compactHeight, operationId, metrics);
            if (!expansion.Group.Plan.Succeeded)
                return new(expansion.Group, null, expansion.Outcome, expansion.MinimumHeight, expansion.EffectiveHeight);
            var final = expansion.Group.Plan.Changes.Single(c => c.PosterId == sourceId).After;
            var cohorts = expansion.Group.Plan.Changes.Select(c => nodes[c.PosterId].Cohort).ToHashSet(StringComparer.Ordinal);
            // A second source may occupy an already displaced peer's return corridor without
            // pushing it another pixel. This is a live constraint, not a new displacement.
            foreach (var peer in scene.Posters.Where(p => p.IsVisible && p.PosterId != sourceId))
            {
                var node = nodes[peer.PosterId];
                var home = peer.Bounds with { Y = checked((int)Math.Clamp(node.HomeY,
                    scene.Environment.WorkArea.Y, scene.Environment.WorkArea.Bottom - peer.Bounds.Height)) };
                if (home != peer.Bounds && expansion.Group.Plan.Changes.Any(c => c.PosterId != peer.PosterId &&
                    SpatialPathPolicy.Crosses(peer.Bounds, home, c.After)))
                    cohorts.Add(node.Cohort);
            }
            string merged = cohorts.Order(StringComparer.Ordinal).First();
            foreach (var id in nodes.Keys.ToArray())
                if (cohorts.Contains(nodes[id].Cohort)) nodes[id] = nodes[id] with { Cohort = merged };
            int up = source.Bounds.Y - final.Y;
            nodes[sourceId] = nodes[sourceId] with { HomeY = nodes[sourceId].HomeY - up, UpInset = up };
            var group = expansion.Group with { NewProof = null, ConsumeOperationId = null };
            return new(group, Complete(scene, nodes, group.Plan, operationId), expansion.Outcome,
                expansion.MinimumHeight, expansion.EffectiveHeight);
        }

        if (compactHeight > source.Bounds.Height) return Reject(SpatialFailure.InvalidInput);
        var sourceNode = nodes[sourceId];
        var anchor = source.Bounds with { Y = checked(source.Bounds.Y + sourceNode.UpInset), Height = compactHeight };
        var sourcePlan = SpatialPostureSolver.Plan(new(scene.Environment, [source]), sourceId, true, compactHeight, anchor);
        if (!sourcePlan.Succeeded) return new(new(sourcePlan, []), null);
        long distance = source.Bounds.Height - (long)compactHeight;
        long frameCount = sourcePlan.Path.Sum(l => (Math.Abs((long)l.To.Height - l.From.Height) + Step - 1) / Step);
        // Relaxation and continuous all-pairs checks are bounded independently of pixels/history length.
        long work = (frameCount + 2) * Math.Max(1, scene.Posters.Count) * scene.Posters.Count * scene.Posters.Count;
        if (frameCount >= MaximumFrames || work > MaximumWork) return Reject(SpatialFailure.BudgetExceeded);
        nodes[sourceId] = sourceNode with { HomeY = sourceNode.HomeY + sourceNode.UpInset, UpInset = 0 };
        var cohort = scene.Posters.Where(p => p.IsVisible && nodes[p.PosterId].Cohort == sourceNode.Cohort).ToArray();
        var finalSource = sourcePlan.Changes.Single().After;
        var ideal = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        ideal[sourceId] = finalSource;
        // The caller can itself still carry a displacement from an earlier source. If its
        // complete compact goal lies inside the shrinking footprint, absorb that displacement
        // into the same two-leg collapse instead of leaving a persistent title-bar drift.
        Relax(cohort, "", nodes, ideal, scene.Environment.WorkArea);
        var sourceGoal = ideal[sourceId];
        if (source.Bounds.Contains(sourceGoal) && !scene.Posters.Any(p => p.IsVisible &&
            p.PosterId != sourceId && !cohort.Any(c => c.PosterId == p.PosterId) &&
            SpatialPathPolicy.Crosses(finalSource, sourceGoal, p.Bounds)))
        {
            sourcePlan = SpatialPostureSolver.Plan(new(scene.Environment, [source]), sourceId, true, compactHeight, sourceGoal);
            finalSource = sourcePlan.Changes.Single().After;
        }
        ideal = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        ideal[sourceId] = finalSource;
        // Solve the complete cohort goal first. External/new obstacles are checked afterwards;
        // they may prevent a COMPLETE return, but must not redefine a half-return as its goal.
        Relax(cohort, sourceId, nodes, ideal, scene.Environment.WorkArea);
        var destinations = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        destinations[sourceId] = finalSource;
        var pending = cohort.Where(p => p.PosterId != sourceId && !p.IsLocked && ideal[p.PosterId] != p.Bounds)
            .OrderBy(p => p.PosterId, StringComparer.Ordinal).ToList();
        var targets = new Dictionary<string, SpatialRect>(StringComparer.Ordinal);
        bool progress;
        do
        {
            progress = false;
            foreach (var p in pending.ToArray())
            {
                var goal = ideal[p.PosterId];
                if (scene.Posters.Any(o => o.IsVisible && o.PosterId != p.PosterId &&
                    SpatialPathPolicy.Crosses(p.Bounds, goal, destinations[o.PosterId]))) continue;
                targets[p.PosterId] = goal; destinations[p.PosterId] = goal;
                pending.Remove(p); progress = true;
            }
        } while (progress && pending.Count != 0);

        var working = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        var frames = new List<SpatialBatchFrame>();
        foreach (var step in SpatialPostureSolver.Frames(sourcePlan, Step))
        {
            var before = new Dictionary<string, SpatialRect>(working, StringComparer.Ordinal);
            working[sourceId] = step.To;
            Advance(scene, targets, working, source.Bounds.Height - (long)step.To.Height, distance);
            var visible = scene.Posters.Where(p => p.IsVisible).ToArray();
            for (int i = 0; i < visible.Length; i++)
            for (int j = i + 1; j < visible.Length; j++)
                if (SpatialPathPolicy.CrossesMoving(before[visible[i].PosterId], working[visible[i].PosterId],
                    before[visible[j].PosterId], working[visible[j].PosterId])) return Reject(SpatialFailure.PathBlocked);
            frames.Add(new(Array.AsReadOnly(scene.Posters.Where(p => before[p.PosterId] != working[p.PosterId])
                .Select(p => new SpatialPathLeg(p.PosterId, before[p.PosterId], working[p.PosterId])).ToArray())));
        }
        if (targets.Any(t => working[t.Key] != t.Value)) return Reject(SpatialFailure.PathBlocked);
        var changes = scene.Posters.Where(p => p.PosterId == sourceId || working[p.PosterId] != p.Bounds)
            .Select(p => new SpatialChange(p.PosterId, p.Bounds, working[p.PosterId], p.IsCollapsed,
                p.PosterId == sourceId || p.IsCollapsed,
                p.PosterId == sourceId ? SpatialChangeCause.Collapse : SpatialChangeCause.ConditionalReturn)).ToArray();
        var plan = new SpatialPlan(SpatialPlanStatus.Accepted, Array.AsReadOnly(changes),
            Array.AsReadOnly(frames.SelectMany(f => f.Changes).ToArray()), []);
        return new(new(plan, frames.AsReadOnly()), Complete(scene, nodes, plan, operationId), EffectiveHeight: compactHeight);
    }

    private static SpatialContinuityNode Fresh(SpatialPoster p) =>
        new(p, p.Bounds.Y, 0, "anchor:" + p.PosterId + ":" + p.LayoutGeneration);

    private static SpatialContinuityState Freeze(SpatialEnvironment env, Dictionary<string, SpatialContinuityNode> nodes) =>
        new(env, Array.AsReadOnly(nodes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToArray()));

    private static SpatialContinuityState Complete(SpatialSnapshot scene, Dictionary<string, SpatialContinuityNode> nodes,
        SpatialPlan plan, string operationId)
    {
        foreach (var change in plan.Changes)
        {
            var n = nodes[change.PosterId];
            nodes[change.PosterId] = n with { Expected = n.Expected with { Bounds = change.After,
                IsCollapsed = change.IsCollapsed, LayoutGeneration = checked(n.Expected.LayoutGeneration + 1),
                LastLayoutOperationId = operationId } };
        }
        // Settled cohorts dissolve. Their own posture inset survives; there is no permanent grouping.
        foreach (var group in nodes.Values.GroupBy(n => n.Cohort).ToArray())
            if (group.All(n => n.HomeY == n.Expected.Bounds.Y))
                foreach (var node in group) nodes[node.Expected.PosterId] = node with { Cohort = Fresh(node.Expected).Cohort };
        return Freeze(scene.Environment, nodes);
    }

    private static void Relax(IReadOnlyList<SpatialPoster> peers, string sourceId,
        IReadOnlyDictionary<string, SpatialContinuityNode> nodes, Dictionary<string, SpatialRect> positions, SpatialRect area)
    {
        // Each coordinate moves monotonically towards its own anchor. Ordering is geometric,
        // stable IDs break ties only. No force integration, recursive push-back, or offset addition.
        var order = peers.OrderBy(p => p.Bounds.Y).ThenBy(p => p.PosterId, StringComparer.Ordinal).ToArray();
        for (int pass = 0; pass < peers.Count; pass++)
        {
            bool changed = false;
            foreach (var p in order)
            {
                if (p.PosterId == sourceId || p.IsLocked) continue;
                var r = positions[p.PosterId];
                long lo = area.Y, hi = area.Bottom - r.Height;
                foreach (var other in peers.Where(o => o.PosterId != p.PosterId))
                {
                    var q = positions[other.PosterId];
                    if (r.X >= q.Right || r.Right <= q.X) continue;
                    if (other.Bounds.Bottom <= p.Bounds.Y) lo = Math.Max(lo, q.Bottom);
                    else if (other.Bounds.Y >= p.Bounds.Bottom) hi = Math.Min(hi, (long)q.Y - r.Height);
                }
                if (lo > hi) continue; // current legal coordinate is retained; never invent an overlap.
                int y = checked((int)Math.Clamp(nodes[p.PosterId].HomeY, lo, hi));
                if (y == r.Y) continue;
                positions[p.PosterId] = r with { Y = y }; changed = true;
            }
            if (!changed) break;
        }
    }

    private static void Advance(SpatialSnapshot scene, IReadOnlyDictionary<string, SpatialRect> targets,
        Dictionary<string, SpatialRect> positions, long releasedTotal, long collapseDistance)
    {
        for (int pass = 0; pass < targets.Count; pass++)
        {
            bool moved = false;
            foreach (var t in targets.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                var current = positions[t.Key];
                // Older blocked returns can be longer than this source's height delta. Share
                // normalized progress, not a one-pixel-per-source-pixel quota. Collision clipping
                // still prevents entering space before the actual blocking frontier releases it.
                var initial = scene.Posters.Single(p => p.PosterId == t.Key).Bounds;
                long y = initial.Y + ((long)t.Value.Y - initial.Y) * releasedTotal / collapseDistance;
                foreach (var p in scene.Posters.Where(p => p.IsVisible && p.PosterId != t.Key))
                {
                    var r = positions[p.PosterId];
                    if (current.X >= r.Right || current.Right <= r.X) continue;
                    if (y < current.Y && r.Bottom <= current.Y) y = Math.Max(y, r.Bottom);
                    else if (y > current.Y && r.Y >= current.Bottom) y = Math.Min(y, r.Y - (long)current.Height);
                }
                if (y == current.Y) continue;
                positions[t.Key] = current with { Y = checked((int)y) }; moved = true;
            }
            if (!moved) break;
        }
    }
}
