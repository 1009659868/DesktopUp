namespace DesktopUp.Domain;

public sealed record SpatialOperationProof(string OperationId, SpatialEnvironment Environment, SpatialPoster CompactSource,
    SpatialPoster ExpandedSource, IReadOnlyList<SpatialReturnProof> Participants, string Status = "active");
public sealed record SpatialBatchFrame(IReadOnlyList<SpatialPathLeg> Changes);
public sealed record SpatialGroupPlan(SpatialPlan Plan, IReadOnlyList<SpatialBatchFrame> Frames,
    SpatialOperationProof? NewProof = null, string? ConsumeOperationId = null);

public static class SpatialPushSolver
{
    private const int MaximumPosters = 256;
    private const int MaximumFrames = 4096;
    private const int MaximumStep = 32;

    public static SpatialGroupPlan Plan(SpatialSnapshot scene, string sourceId, bool collapsed, int compactHeight,
        string operationId, SpatialOperationProof? proof = null, SpatialRect? expansionBounds = null)
    {
        SpatialGroupPlan Reject(SpatialFailure failure) => new(SpatialPlan.Reject(failure, sourceId), []);
        var invalid = PosterSpatialSolver.ValidateLayout(scene);
        if (invalid.Count > 0) return new(SpatialPlan.Reject(invalid), []);
        if (scene.Posters.Count > MaximumPosters) return Reject(SpatialFailure.BudgetExceeded);
        if (string.IsNullOrWhiteSpace(operationId)) return Reject(SpatialFailure.InvalidInput);
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == sourceId);
        if (source is null || !source.IsVisible) return Reject(SpatialFailure.MissingTarget);
        bool validProof = proof is { Status: "active" } && proof.Environment == scene.Environment && proof.ExpandedSource == source;
        SpatialRect? anchor = validProof ? proof!.CompactSource.Bounds : null;
        // First freeze source intent without obstacles; propagation supplies the missing space.
        var sourcePlan = expansionBounds is { } endpoint
            ? ExpansionPath(scene.Environment, source, endpoint, collapsed)
            : SpatialPostureSolver.Plan(new(scene.Environment, [source]), sourceId, collapsed, compactHeight, anchor);
        if (!sourcePlan.Succeeded) return new(sourcePlan, []);
        if (sourcePlan.Status == SpatialPlanStatus.NoChange) return new(sourcePlan, []);
        long heightDistance = sourcePlan.Path.Sum(p => Math.Abs((long)p.To.Height - p.From.Height));
        if (heightDistance > (MaximumFrames - 1) * (long)MaximumStep) return Reject(SpatialFailure.BudgetExceeded);
        var working = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        var frames = new List<SpatialBatchFrame>();
        var sources = scene.Posters.ToDictionary(p => p.PosterId, StringComparer.Ordinal);
        // Freeze the safe return subset before animation. Eligibility is checked against the
        // committed scene, never against geometry already changed by a preview frame.
        var returns = collapsed && validProof
            ? FindReturnTargets(scene, sourceId, sourcePlan.Changes.Single().After, proof!)
            : new Dictionary<string, SpatialRect>(StringComparer.Ordinal);
        foreach (var step in SpatialPostureSolver.Frames(sourcePlan, MaximumStep))
        {
            var before = new Dictionary<string, SpatialRect>(working, StringComparer.Ordinal);
            working[sourceId] = step.To;
            if (!collapsed)
            {
                int direction = step.To.Y < step.From.Y ? -1 : 1;
                var queue = new Queue<string>(); queue.Enqueue(sourceId);
                int iterations = 0;
                while (queue.TryDequeue(out string? id))
                {
                    if (++iterations > MaximumPosters * MaximumPosters) return Reject(SpatialFailure.BudgetExceeded);
                    foreach (var neighbor in scene.Posters.Where(p => p.IsVisible && p.PosterId != id))
                    {
                        var current = working[id]; var next = working[neighbor.PosterId];
                        if (!current.Overlaps(next)) continue;
                        if (neighbor.PosterId == sourceId || neighbor.IsLocked) return Reject(SpatialFailure.LockedTarget);
                        long delta = direction > 0 ? current.Bottom - next.Y : current.Y - next.Bottom;
                        long newY = next.Y + delta;
                        if (newY < int.MinValue || newY > int.MaxValue) return Reject(SpatialFailure.NoSpace);
                        next = next with { Y = (int)newY };
                        if (!scene.Environment.WorkArea.Contains(next)) return Reject(SpatialFailure.NoSpace);
                        working[neighbor.PosterId] = next;
                        queue.Enqueue(neighbor.PosterId);
                    }
                }
            }
            else AdvanceReturns(scene, returns, before, working, Math.Abs(step.To.Height - step.From.Height));
            if (!AppendFrame(before)) return Reject(SpatialFailure.PathBlocked);
        }
        // Never silently leave a half-return or append a delayed return-only animation.
        if (returns.Any(p => working[p.Key] != p.Value)) return Reject(SpatialFailure.PathBlocked);
        var changes = scene.Posters.Where(p => working[p.PosterId] != p.Bounds || p.PosterId == sourceId)
            .Select(p => new SpatialChange(p.PosterId, p.Bounds, working[p.PosterId], p.IsCollapsed,
                p.PosterId == sourceId ? collapsed : p.IsCollapsed,
                p.PosterId == sourceId ? (collapsed ? SpatialChangeCause.Collapse : SpatialChangeCause.Expand) :
                    (collapsed ? SpatialChangeCause.ConditionalReturn : SpatialChangeCause.Push))).ToArray();
        var plan = new SpatialPlan(SpatialPlanStatus.Accepted, Array.AsReadOnly(changes),
            Array.AsReadOnly(frames.SelectMany(f => f.Changes).ToArray()), []);
        SpatialOperationProof? created = null;
        if (!collapsed)
        {
            var afterSource = source with { Bounds = working[sourceId], IsCollapsed = false,
                LayoutGeneration = checked(source.LayoutGeneration + 1), LastLayoutOperationId = operationId };
            var participants = changes.Where(c => c.PosterId != sourceId).Select(c => new SpatialReturnProof(operationId, scene.Environment,
                afterSource, sources[c.PosterId], sources[c.PosterId] with { Bounds = c.After,
                    LayoutGeneration = checked(sources[c.PosterId].LayoutGeneration + 1), LastLayoutOperationId = operationId })).ToArray();
            created = new(operationId, scene.Environment, source, afterSource, Array.AsReadOnly(participants));
        }
        return new(plan, frames.AsReadOnly(), created, collapsed && validProof ? proof!.OperationId : null);

        bool AppendFrame(Dictionary<string, SpatialRect> before)
        {
            if (frames.Count >= MaximumFrames) return false;
            var visible = scene.Posters.Where(p => p.IsVisible).ToArray();
            for (int i = 0; i < visible.Length; i++)
            for (int j = i + 1; j < visible.Length; j++)
            {
                string a = visible[i].PosterId, b = visible[j].PosterId;
                if (SpatialPathPolicy.CrossesMoving(before[a], working[a], before[b], working[b])) return false;
            }
            frames.Add(new(Array.AsReadOnly(scene.Posters.Where(p => before[p.PosterId] != working[p.PosterId])
                .Select(p => new SpatialPathLeg(p.PosterId, before[p.PosterId], working[p.PosterId])).ToArray())));
            return true;
        }
    }

    private static SpatialPlan ExpansionPath(SpatialEnvironment environment, SpatialPoster source, SpatialRect endpoint, bool collapsed)
    {
        var start = source.Bounds;
        if (collapsed || !source.IsCollapsed || endpoint.X != start.X || endpoint.Width != start.Width ||
            !endpoint.Contains(start) || endpoint.Height > source.ExpandedHeight || !environment.WorkArea.Contains(endpoint))
            return SpatialPlan.Reject(SpatialFailure.InvalidInput, source.PosterId);
        var middle = start with { Height = checked((int)(endpoint.Bottom - start.Y)) };
        var path = new List<SpatialPathLeg>();
        if (middle != start) path.Add(new(source.PosterId, start, middle));
        if (endpoint != middle) path.Add(new(source.PosterId, middle, endpoint));
        return SpatialPlan.Create(new(source.PosterId, start, endpoint, true, false, SpatialChangeCause.Expand), path);
    }

    private static Dictionary<string, SpatialRect> FindReturnTargets(SpatialSnapshot scene, string sourceId,
        SpatialRect compactSource, SpatialOperationProof proof)
    {
        var destinations = scene.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        destinations[sourceId] = compactSource;
        var targets = new Dictionary<string, SpatialRect>(StringComparer.Ordinal);
        var pending = proof.Participants.Where(p => SpatialReturnPolicy.CheckEligibility(scene, p) is null)
            .OrderBy(p => p.TargetBefore.PosterId, StringComparer.Ordinal).ToList();
        bool progress;
        do
        {
            progress = false;
            foreach (var entry in pending.ToArray())
            {
                string id = entry.TargetBefore.PosterId;
                var from = destinations[id]; var to = entry.TargetBefore.Bounds;
                // Current push plans only translate vertically. Do not invent a horizontal
                // return for a foreign/malformed proof, or partially approach a blocked home.
                if (from.X != to.X || !scene.Environment.WorkArea.Contains(to) ||
                    scene.Posters.Any(p => p.IsVisible && p.PosterId != id &&
                        SpatialPathPolicy.Crosses(from, to, destinations[p.PosterId]))) continue;
                destinations[id] = to; targets[id] = to;
                pending.Remove(entry); progress = true;
            }
        } while (progress && pending.Count > 0);
        return targets;
    }

    private static void AdvanceReturns(SpatialSnapshot scene, IReadOnlyDictionary<string, SpatialRect> targets,
        IReadOnlyDictionary<string, SpatialRect> before, Dictionary<string, SpatialRect> working, int releasedPixels)
    {
        // A whole chain must use the same source frame, not wait for the previous neighbor's
        // next timer tick. Repeated monotone relaxation removes stable-ID ordering dependence.
        // Every peer's budget is anchored to BEFORE this frame, so retries cannot accelerate it.
        for (int pass = 0; pass < targets.Count; pass++)
        {
            bool moved = false;
            foreach (var entry in targets.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var current = working[entry.Key];
                long y = before[entry.Key].Y + Math.Clamp((long)entry.Value.Y - before[entry.Key].Y, -releasedPixels, releasedPixels);
                foreach (var obstacle in scene.Posters.Where(p => p.IsVisible && p.PosterId != entry.Key))
                {
                    var other = working[obstacle.PosterId];
                    if (current.X >= other.Right || current.Right <= other.X) continue;
                    if (y < current.Y && other.Bottom <= current.Y) y = Math.Max(y, other.Bottom);
                    else if (y > current.Y && other.Y >= current.Bottom) y = Math.Min(y, (long)other.Y - current.Height);
                }
                if (y == current.Y) continue;
                working[entry.Key] = current with { Y = checked((int)y) };
                moved = true;
            }
            if (!moved) break;
        }
    }
}
