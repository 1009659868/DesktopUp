namespace DesktopUp.Domain;

public static class SpatialPostureSolver
{
    public static SpatialPlan Plan(SpatialSnapshot scene, string id, bool collapsed, int compactHeight, SpatialRect? compactAnchor = null)
    {
        var invalid = PosterSpatialSolver.ValidateLayout(scene);
        if (invalid.Count > 0) return SpatialPlan.Reject(invalid);
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == id);
        if (source is null || !source.IsVisible) return SpatialPlan.Reject(SpatialFailure.MissingTarget, id);
        if (!collapsed) return PosterSpatialSolver.PlanExpansion(scene, id);
        if (source.IsCollapsed) return SpatialPlan.Create(new(id, source.Bounds, source.Bounds, true, true, SpatialChangeCause.Collapse), []);
        if (compactHeight < 1 || compactHeight > source.Bounds.Height) return SpatialPlan.Reject(SpatialFailure.InvalidInput, id);
        SpatialRect final = source.Bounds with { Height = compactHeight };
        if (compactAnchor is { } anchor && anchor.Width == source.Bounds.Width && anchor.X == source.Bounds.X &&
            anchor.Height == compactHeight && source.Bounds.Contains(anchor)) final = anchor;
        // Reverse the two growth legs: restore the compact title Y first, then reduce the bottom.
        var middle = source.Bounds with { Y = final.Y, Height = checked((int)(source.Bounds.Bottom - final.Y)) };
        var path = new List<SpatialPathLeg>();
        if (middle != source.Bounds) path.Add(new(id, source.Bounds, middle));
        if (final != middle) path.Add(new(id, middle, final));
        var blocked = PosterSpatialSolver.CheckPath(scene, id, path);
        return blocked.Count > 0 ? SpatialPlan.Reject(blocked) : SpatialPlan.Create(
            new(id, source.Bounds, final, false, true, SpatialChangeCause.Collapse), path);
    }

    public static IReadOnlyList<SpatialPathLeg> Frames(SpatialPlan plan, int maximumStep = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStep, 1);
        var frames = new List<SpatialPathLeg>();
        foreach (var leg in plan.Path)
        {
            long extent = Math.Max(Math.Abs((long)leg.To.Height - leg.From.Height), Math.Abs((long)leg.To.Y - leg.From.Y));
            int count = Math.Max(1, checked((int)((extent + maximumStep - 1) / maximumStep)));
            SpatialRect previous = leg.From;
            for (int i = 1; i <= count; i++)
            {
                int y = checked((int)(leg.From.Y + ((long)leg.To.Y - leg.From.Y) * i / count));
                int bottom = checked((int)(leg.From.Bottom + (leg.To.Bottom - leg.From.Bottom) * i / count));
                var next = new SpatialRect(leg.From.X, y, leg.From.Width, bottom - y);
                frames.Add(new(leg.PosterId, previous, next)); previous = next;
            }
        }
        return frames.AsReadOnly();
    }
}
