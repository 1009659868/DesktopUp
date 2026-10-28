namespace DesktopUp.Domain;

public enum SpatialExpansionOutcome { Full, Constrained, InsufficientSpace, Rejected }
public sealed record SpatialExpansionResult(SpatialExpansionOutcome Outcome, SpatialGroupPlan Group, int MinimumHeight, int EffectiveHeight);

public static class SpatialExpansionPolicy
{
    // Bounded binary searches, not one candidate per pixel. Each candidate is a complete,
    // side-effect-free group simulation including swept collision checks.
    public static SpatialExpansionResult Plan(SpatialSnapshot scene, string id, int compactHeight, string operationId,
        PosterContentMetrics metrics, int candidateBudget = 96)
    {
        int minimum;
        try { minimum = metrics.MinimumPixels(scene.Environment.DpiY); }
        catch (Exception e) when (e is ArgumentException or OverflowException)
        { return Reject(SpatialFailure.IncompatibleMetrics, 0); }
        if (scene.Posters.Count > 256) return Reject(SpatialFailure.BudgetExceeded, minimum);
        var invalid = PosterSpatialSolver.ValidateLayout(scene);
        if (invalid.Count != 0) return new(SpatialExpansionOutcome.Rejected, new(SpatialPlan.Reject(invalid), []), minimum, 0);
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == id);
        if (source is null || !source.IsVisible || !source.IsCollapsed || compactHeight <= 0)
            return Reject(SpatialFailure.InvalidInput, minimum);
        if (source.ExpandedHeight < minimum) return Reject(SpatialFailure.IncompatibleMetrics, minimum);
        if (scene.Posters.Count > 256 || source.ExpandedHeight - (long)source.Bounds.Height > 4095L * 32 || candidateBudget <= 0)
            return Reject(SpatialFailure.BudgetExceeded, minimum);
        var start = source.Bounds;
        int extra = source.ExpandedHeight - start.Height;
        int downLimit = (int)Math.Min(extra, scene.Environment.WorkArea.Bottom - start.Bottom);
        int upLimit = Math.Min(extra, start.Y - scene.Environment.WorkArea.Y);
        SpatialFailure? fatal = null;
        long workBudget = 2_000_000;
        SpatialGroupPlan? Try(int down, int up)
        {
            if (down < 0 || up < 0 || down > downLimit || up > upLimit) return null;
            if (--candidateBudget < 0) { fatal = SpatialFailure.BudgetExceeded; return null; }
            workBudget -= Math.Max(1, (down + up + 31L) / 32 + 1) * scene.Posters.Count * scene.Posters.Count;
            if (workBudget < 0) { fatal = SpatialFailure.BudgetExceeded; return null; }
            var endpoint = new SpatialRect(start.X, start.Y - up, start.Width, start.Height + down + up);
            var group = SpatialPushSolver.Plan(scene, id, false, compactHeight, operationId, expansionBounds: endpoint);
            if (group.Plan.Succeeded) return group;
            var issue = group.Plan.Issues.FirstOrDefault(i => i.Reason is not
                (SpatialFailure.NoSpace or SpatialFailure.LockedTarget or SpatialFailure.PathBlocked));
            if (issue is not null) fatal = issue.Reason;
            return null;
        }
        SpatialExpansionResult Accepted(SpatialGroupPlan group) {
            int height = group.Plan.Changes.Single(p => p.PosterId == id).After.Height;
            return new(height == source.ExpandedHeight ? SpatialExpansionOutcome.Full : SpatialExpansionOutcome.Constrained,
                group, minimum, height);
        }
        // Preserve the established unobstructed down-then-up route first.
        var normal = Try(downLimit, extra - downLimit);
        if (normal is not null) return Accepted(normal);
        if (fatal is { } failed) return Reject(failed, minimum);

        int Capacity(int limit, Func<int, SpatialGroupPlan?> candidate)
        {
            int low = 0, high = limit;
            while (low < high && fatal is null)
            {
                int middle = low + (high - low + 1) / 2;
                if (candidate(middle) is not null) low = middle; else high = middle - 1;
            }
            return low;
        }
        // A blocked full downward candidate must not skip the legal gap/push travel below.
        // Even if a pure-up full expansion fits, consume downward capacity first; only
        // the remaining preferred height grows upward. Pure-up is valid only at down == 0.
        int down = Capacity(downLimit, d => Try(d, 0));
        // Up is evaluated AFTER the same down-prefix, not added from an unrelated neighbor scene.
        // Growth is monotone on either side of the compact anchor. Neighbor order cannot cross it.
        int up = Capacity(Math.Min(upLimit, extra - down), u => Try(down, u));
        if (fatal is { } searchFailed) return Reject(searchFailed, minimum);
        int available = start.Height + down + up;
        if (available < minimum)
            return new(SpatialExpansionOutcome.InsufficientSpace,
                new(SpatialPlan.Reject(SpatialFailure.InsufficientSpace, id), []), minimum, available);
        var selected = Try(down, up);
        return selected is not null ? Accepted(selected) : Reject(fatal ?? SpatialFailure.PathBlocked, minimum);

        SpatialExpansionResult Reject(SpatialFailure failure, int min) =>
            new(SpatialExpansionOutcome.Rejected, new(SpatialPlan.Reject(failure, id), []), min, 0);
    }
}
