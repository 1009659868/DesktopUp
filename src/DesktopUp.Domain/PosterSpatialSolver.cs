namespace DesktopUp.Domain;

public sealed record SpatialFrameRequest(string PosterId, SpatialRect Desired,
    PosterGestureKind Gesture, PosterOperatingMode Mode, int MinimumWidth, int MinimumHeight);

public static class PosterSpatialSolver
{
    public static IReadOnlyList<SpatialIssue> ValidateLayout(SpatialSnapshot snapshot)
    {
        var issues = new List<SpatialIssue>();
        if (!snapshot.Environment.IsValid) return Array.AsReadOnly(new[] { new SpatialIssue(SpatialFailure.InvalidInput, null) });
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialPoster poster in snapshot.Posters)
        {
            if (string.IsNullOrWhiteSpace(poster.PosterId) || !identities.Add(poster.PosterId) ||
                !poster.Bounds.IsValid || poster.LayoutGeneration < 0 ||
                poster.ExpandedHeight < poster.Bounds.Height)
                issues.Add(new(SpatialFailure.InvalidInput, poster.PosterId));
            else if (poster.IsVisible && !snapshot.Environment.WorkArea.Contains(poster.Bounds))
                issues.Add(new(poster.Bounds.Width > snapshot.Environment.WorkArea.Width ||
                    poster.Bounds.Height > snapshot.Environment.WorkArea.Height
                    ? SpatialFailure.NoSpace : SpatialFailure.OutsideWorkArea, poster.PosterId));
        }
        if (issues.Count > 0) return issues.AsReadOnly();
        for (int i = 0; i < snapshot.Posters.Count; i++)
        for (int j = i + 1; j < snapshot.Posters.Count; j++)
        {
            SpatialPoster a = snapshot.Posters[i], b = snapshot.Posters[j];
            if (a.IsVisible && b.IsVisible && a.Bounds.Overlaps(b.Bounds))
                issues.Add(new(SpatialFailure.InitialOverlap, a.PosterId, b.PosterId));
        }
        return issues.AsReadOnly();
    }

    // Exact accept/reject contract. M04-03 will derive clamped/sliding candidates without dropping capture.
    public static SpatialPlan PlanFrameChange(SpatialSnapshot snapshot, SpatialFrameRequest request)
    {
        SpatialPlan? invalid = ValidateTarget(snapshot, request.PosterId, out SpatialPoster? source);
        if (invalid is not null) return invalid;
        SpatialPoster poster = source!;
        if (!Enum.IsDefined(request.Gesture) || !Enum.IsDefined(request.Mode) ||
            request.MinimumWidth <= 0 || request.MinimumHeight <= 0 || !request.Desired.IsValid)
            return SpatialPlan.Reject(SpatialFailure.InvalidInput, poster.PosterId);
        bool move = request.Gesture == PosterGestureKind.Move;
        if (poster.IsLocked || (!move && (poster.IsCollapsed || request.Mode != PosterOperatingMode.Editing)))
            return SpatialPlan.Reject(SpatialFailure.PermissionDenied, poster.PosterId);
        if (!PreservesInactiveEdges(poster.Bounds, request.Desired, request.Gesture) ||
            (!move && (request.Desired.Width < request.MinimumWidth || request.Desired.Height < request.MinimumHeight)))
            return SpatialPlan.Reject(SpatialFailure.InvalidInput, poster.PosterId);
        var path = new[] { new SpatialPathLeg(poster.PosterId, poster.Bounds, request.Desired) };
        IReadOnlyList<SpatialIssue> blocked = CheckPath(snapshot, poster.PosterId, path);
        if (blocked.Count > 0) return SpatialPlan.Reject(blocked);
        return SpatialPlan.Create(new(poster.PosterId, poster.Bounds, request.Desired,
            poster.IsCollapsed, poster.IsCollapsed, move ? SpatialChangeCause.Move : SpatialChangeCause.Resize), path);
    }

    // Pure no-push plan. Obstacles are explicit rejection until M04-05 adds bounded propagation.
    public static SpatialPlan PlanExpansion(SpatialSnapshot snapshot, string posterId)
    {
        SpatialPlan? invalid = ValidateTarget(snapshot, posterId, out SpatialPoster? source);
        if (invalid is not null) return invalid;
        SpatialPoster poster = source!;
        if (!poster.IsCollapsed)
            return SpatialPlan.Create(new(posterId, poster.Bounds, poster.Bounds, false, false, SpatialChangeCause.Expand), []);
        SpatialRect work = snapshot.Environment.WorkArea, compact = poster.Bounds;
        if (poster.ExpandedHeight > work.Height || compact.Width > work.Width)
            return SpatialPlan.Reject(SpatialFailure.NoSpace, posterId);
        int extra = poster.ExpandedHeight - compact.Height;
        int down = (int)Math.Min(extra, work.Bottom - compact.Bottom);
        int up = extra - down;
        var middle = compact with { Height = compact.Height + down };
        var final = middle with { Y = compact.Y - up, Height = poster.ExpandedHeight };
        var path = new List<SpatialPathLeg>();
        if (down > 0) path.Add(new(posterId, compact, middle));
        if (up > 0) path.Add(new(posterId, middle, final));
        IReadOnlyList<SpatialIssue> blocked = CheckPath(snapshot, posterId, path);
        if (blocked.Count > 0) return SpatialPlan.Reject(blocked);
        return SpatialPlan.Create(new(posterId, compact, final, true, false, SpatialChangeCause.Expand), path);
    }

    // Reused by future placement/show/import/return adapters, but those runtime paths are not wired here.
    public static IReadOnlyList<SpatialIssue> CheckPath(SpatialSnapshot snapshot, string posterId,
        IReadOnlyList<SpatialPathLeg> path)
    {
        var invalid = ValidateLayout(snapshot);
        if (invalid.Count > 0) return invalid;
        var source = snapshot.Posters.FirstOrDefault(p => p.PosterId == posterId);
        if (source is null || !source.IsVisible)
            return Array.AsReadOnly(new[] { new SpatialIssue(source is null ? SpatialFailure.MissingTarget : SpatialFailure.HiddenTarget, posterId) });
        var issues = new List<SpatialIssue>();
        SpatialRect expected = source.Bounds;
        foreach (SpatialPathLeg leg in path)
        {
            if (leg.PosterId != posterId || !leg.From.IsValid || !leg.To.IsValid || leg.From != expected)
            {
                issues.Add(new(SpatialFailure.InvalidInput, posterId));
                continue;
            }
            expected = leg.To;
            if (!snapshot.Environment.WorkArea.Contains(leg.From) || !snapshot.Environment.WorkArea.Contains(leg.To))
                issues.Add(new(leg.To.Width > snapshot.Environment.WorkArea.Width || leg.To.Height > snapshot.Environment.WorkArea.Height
                    ? SpatialFailure.NoSpace : SpatialFailure.OutsideWorkArea, posterId));
            foreach (SpatialPoster obstacle in snapshot.Posters.Where(p => p.IsVisible && p.PosterId != posterId))
                if (SpatialPathPolicy.Crosses(leg.From, leg.To, obstacle.Bounds))
                    issues.Add(new(SpatialFailure.PathBlocked, posterId, obstacle.PosterId));
        }
        return Array.AsReadOnly(issues.Distinct().OrderBy(i => i.Reason)
            .ThenBy(i => i.PosterId, StringComparer.Ordinal).ThenBy(i => i.ObstacleId, StringComparer.Ordinal).ToArray());
    }

    private static SpatialPlan? ValidateTarget(SpatialSnapshot snapshot, string id, out SpatialPoster? source)
    {
        source = null;
        IReadOnlyList<SpatialIssue> invalid = ValidateLayout(snapshot);
        if (invalid.Count > 0) return SpatialPlan.Reject(invalid);
        source = snapshot.Posters.FirstOrDefault(p => p.PosterId == id);
        if (source is null) return SpatialPlan.Reject(SpatialFailure.MissingTarget, id);
        return source.IsVisible ? null : SpatialPlan.Reject(SpatialFailure.HiddenTarget, id);
    }

    private static bool PreservesInactiveEdges(SpatialRect a, SpatialRect b, PosterGestureKind gesture)
    {
        if (gesture == PosterGestureKind.Move) return a.Width == b.Width && a.Height == b.Height;
        bool left = gesture is PosterGestureKind.ResizeLeft or PosterGestureKind.ResizeTopLeft or PosterGestureKind.ResizeBottomLeft;
        bool right = gesture is PosterGestureKind.ResizeRight or PosterGestureKind.ResizeTopRight or PosterGestureKind.ResizeBottomRight;
        bool top = gesture is PosterGestureKind.ResizeTop or PosterGestureKind.ResizeTopLeft or PosterGestureKind.ResizeTopRight;
        bool bottom = gesture is PosterGestureKind.ResizeBottom or PosterGestureKind.ResizeBottomLeft or PosterGestureKind.ResizeBottomRight;
        return (left || a.X == b.X) && (right || a.Right == b.Right) &&
            (top || a.Y == b.Y) && (bottom || a.Bottom == b.Bottom);
    }
}

public sealed record SpatialReturnProof(string OperationId, SpatialEnvironment Environment,
    SpatialPoster SourceAfter, SpatialPoster TargetBefore, SpatialPoster TargetAfter, bool Consumed = false);

public static class SpatialReturnPolicy
{
    // Eligibility only. Call CheckPath and validate the complete return subset before any mutation.
    public static SpatialIssue? CheckEligibility(SpatialSnapshot current, SpatialReturnProof? proof)
    {
        if (proof is null) return new(SpatialFailure.MissingProof, null);
        string id = proof.TargetAfter.PosterId;
        if (proof.Consumed) return new(SpatialFailure.ConsumedProof, id);
        if (current.Environment != proof.Environment) return new(SpatialFailure.StaleEnvironment, id);
        if (string.IsNullOrWhiteSpace(proof.OperationId) || proof.TargetBefore.PosterId != id ||
            proof.SourceAfter.PosterId == id || !proof.TargetBefore.Bounds.IsValid ||
            !proof.TargetAfter.Bounds.IsValid || !proof.SourceAfter.IsVisible ||
            proof.SourceAfter.LastLayoutOperationId != proof.OperationId ||
            proof.TargetBefore.LayoutGeneration < 0 || proof.TargetAfter.LayoutGeneration <= proof.TargetBefore.LayoutGeneration ||
            proof.TargetBefore.IsLocked || proof.TargetBefore.IsCollapsed != proof.TargetAfter.IsCollapsed ||
            proof.TargetBefore.IsVisible != proof.TargetAfter.IsVisible ||
            proof.TargetBefore.ExpandedHeight != proof.TargetAfter.ExpandedHeight ||
            proof.TargetBefore.Bounds.Width != proof.TargetAfter.Bounds.Width ||
            proof.TargetBefore.Bounds.Height != proof.TargetAfter.Bounds.Height ||
            proof.TargetAfter.LastLayoutOperationId != proof.OperationId)
            return new(SpatialFailure.MissingProof, id);
        SpatialPoster? source = current.Posters.FirstOrDefault(p => p.PosterId == proof.SourceAfter.PosterId);
        if (source != proof.SourceAfter) return new(SpatialFailure.SourceChanged, id);
        SpatialPoster? target = current.Posters.FirstOrDefault(p => p.PosterId == id);
        if (target?.IsLocked == true) return new(SpatialFailure.LockedTarget, id);
        if (target != proof.TargetAfter || target is null || !target.IsVisible)
            return new(SpatialFailure.TargetChanged, id);
        return null;
    }
}
