namespace DesktopUp.Domain;

public static class SpatialCollisionSolver
{
    // The existing resize policy supplies work-area/minimum-size limits. Collision owns only
    // contact and sliding; neither neighbors nor the input capture are mutated here.
    public static SpatialPlan Resolve(SpatialSnapshot scene, SpatialFrameRequest request)
    {
        SpatialPlan direct = PosterSpatialSolver.PlanFrameChange(scene, request);
        if (direct.Succeeded || direct.Issues.Any(i => i.Reason != SpatialFailure.PathBlocked)) return direct;
        SpatialPoster source = scene.Posters.Single(p => p.PosterId == request.PosterId);
        SpatialRect start = source.Bounds;
        var obstacles = scene.Posters.Where(p => p.IsVisible && p.PosterId != source.PosterId).Select(p => p.Bounds).ToArray();
        var path = new List<SpatialPathLeg>();
        SpatialRect current = Advance(start, request.Desired, obstacles);
        Add(start, current);
        // Deterministic axis order at a corner. Every slide is swept, not teleported.
        SpatialRect horizontal = request.Desired with { Y = current.Y, Height = current.Height };
        SpatialRect next = Advance(current, horizontal, obstacles);
        Add(current, next);
        current = next;
        SpatialRect vertical = request.Desired with { X = current.X, Width = current.Width };
        next = Advance(current, vertical, obstacles);
        Add(current, next);
        current = next;
        var issues = PosterSpatialSolver.CheckPath(scene, source.PosterId, path);
        if (issues.Count > 0) return SpatialPlan.Reject(issues);
        return SpatialPlan.Create(new(source.PosterId, start, current, source.IsCollapsed, source.IsCollapsed,
            request.Gesture == PosterGestureKind.Move ? SpatialChangeCause.Move : SpatialChangeCause.Resize), path);

        void Add(SpatialRect from, SpatialRect to)
        {
            if (from != to) path.Add(new(source.PosterId, from, to));
        }
    }

    private static SpatialRect Advance(SpatialRect from, SpatialRect desired, SpatialRect[] obstacles)
    {
        decimal fraction = 1;
        foreach (SpatialRect obstacle in obstacles)
            if (SpatialPathPolicy.TryContact(from, desired, obstacle, out decimal contact)) fraction = Math.Min(fraction, contact);
        SpatialRect candidate = Interpolate(from, desired, fraction);
        if (Safe(candidate)) return candidate;
        // Independent rounding of edges can change the segment slightly. Keep only an explicitly
        // swept-safe prefix; never trade one pixel of penetration for apparent smoothness.
        decimal lower = 0, upper = fraction;
        SpatialRect lastGood = from;
        for (int i = 0; i < 40; i++)
        {
            decimal middle = (lower + upper) / 2;
            candidate = Interpolate(from, desired, middle);
            if (Safe(candidate)) { lower = middle; lastGood = candidate; }
            else upper = middle;
        }
        return lastGood;
        bool Safe(SpatialRect rect) => rect.IsValid && obstacles.All(o => !SpatialPathPolicy.Crosses(from, rect, o));
    }

    private static SpatialRect Interpolate(SpatialRect a, SpatialRect b, decimal fraction)
    {
        int left = Edge(a.X, b.X), top = Edge(a.Y, b.Y);
        int right = Edge(a.Right, b.Right), bottom = Edge(a.Bottom, b.Bottom);
        return new(left, top, right - left, bottom - top);
        int Edge(long start, long end) => checked((int)(start + decimal.Truncate((end - start) * fraction)));
    }
}
