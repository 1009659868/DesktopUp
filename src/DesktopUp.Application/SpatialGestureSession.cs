using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record SpatialGestureCommit(SpatialSnapshot Initial, SpatialRect Final,
    IReadOnlyList<SpatialPathLeg> Path, PosterGestureKind Gesture, bool HasUserAdjusted);

// Dispatcher-owned, one instance for the complete press/release sequence. Feed only presented
// frames into Accept: replacing an unpresented 16ms sample must not become a hidden movement path.
public sealed class SpatialGestureSession
{
    private const int MaximumPathLegs = 8192;
    private readonly List<SpatialPathLeg> _path = new();
    public string OperationId { get; } = "spatial-gesture-" + Guid.NewGuid().ToString("N");
    public string PosterId { get; }
    public SpatialSnapshot Initial { get; }
    public StateReadSet Reads { get; }
    public long ObservedRevision { get; }
    public PosterGestureKind Gesture { get; }
    public PosterOperatingMode Mode { get; }
    public SpatialRect Presented { get; private set; }
    public bool HasUserAdjusted { get; private set; }
    public bool IsCancelled { get; private set; }
    public int MinimumWidth { get; }
    public int MinimumHeight { get; }

    public SpatialGestureSession(string posterId, SpatialSnapshot initial, DesktopUpProjection projection,
        PosterGestureKind gesture, PosterOperatingMode mode, int minimumWidth, int minimumHeight)
    {
        PosterId = posterId; Initial = initial; Gesture = gesture; Mode = mode;
        MinimumWidth = minimumWidth; MinimumHeight = minimumHeight;
        Presented = initial.Posters.Single(p => p.PosterId == posterId).Bounds;
        Reads = new StateReadSet(projection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
            CatalogVersion: projection.CatalogVersion).Freeze();
        ObservedRevision = projection.Revision;
    }

    public bool IsCurrent(DesktopUpProjection projection, SpatialEnvironment environment) => !IsCancelled &&
        environment == Initial.Environment && projection.CatalogVersion == Reads.CatalogVersion &&
        projection.Posters.Count == Reads.Posters.Count && Reads.Posters.All(p =>
            projection.PostersById.TryGetValue(p.Key, out var entry) && entry.EntityVersion == p.Value);

    public SpatialPlan Preview(SpatialRect desired)
    {
        if (IsCancelled) return SpatialPlan.Reject(SpatialFailure.StaleEnvironment, PosterId);
        var scene = new SpatialSnapshot(Initial.Environment, Initial.Posters.Select(p => p.PosterId == PosterId
            ? p with { Bounds = Presented, ExpandedHeight = p.IsCollapsed ? p.ExpandedHeight : Presented.Height } : p));
        return SpatialCollisionSolver.Resolve(scene, new(PosterId, desired, Gesture, Mode, MinimumWidth, MinimumHeight));
    }

    public bool Accept(SpatialPlan plan)
    {
        if (IsCancelled || !plan.Succeeded || _path.Count + plan.Path.Count > MaximumPathLegs) { Cancel(); return false; }
        if (plan.Status == SpatialPlanStatus.NoChange) return true;
        SpatialChange change = plan.Changes.Single();
        if (change.PosterId != PosterId || change.Before != Presented) { Cancel(); return false; }
        _path.AddRange(plan.Path);
        Presented = change.After;
        HasUserAdjusted |= change.Before != change.After;
        return true;
    }

    public void Cancel() => IsCancelled = true;
    public SpatialGestureCommit Freeze() => new(Initial, Presented, Array.AsReadOnly(_path.ToArray()), Gesture, HasUserAdjusted);
}

public static class SpatialGestureCommitPolicy
{
    public static string? Validate(PosterStateCommand command, DesktopStateSnapshot state)
    {
        if (command.SpatialGesture is not { } gesture) return null;
        if (command.Kind is not (PosterStateCommandKind.Move or PosterStateCommandKind.Resize) ||
            (command.Kind == PosterStateCommandKind.Move) != (gesture.Gesture == PosterGestureKind.Move)) return "Spatial gesture kind mismatch.";
        if (command.ReadSet is not { CatalogVersion: not null } reads || reads.CatalogVersion != state.Catalog.CatalogVersion ||
            !reads.Posters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Catalog.Posters.Select(p => p.Poster.PosterId)))
            return "Spatial commit requires the complete catalog entity read set.";
        var source = gesture.Initial.Posters.FirstOrDefault(p => p.PosterId == command.PosterId);
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (source is null || current is null || current.PlacementPending || source.IsLocked || !source.IsVisible ||
            source.IsCollapsed != current.IsCollapsed || source.IsLocked != current.IsLocked ||
            source.IsVisible != current.IsVisible || gesture.HasUserAdjusted != gesture.Path.Any(p => p.From != p.To) ||
            gesture.Path.Count > 8192 || !gesture.Final.IsValid) return "Invalid spatial source or trace.";
        if (PosterSpatialSolver.CheckPath(gesture.Initial, command.PosterId, gesture.Path).Count != 0 ||
            (gesture.Path.LastOrDefault()?.To ?? source.Bounds) != gesture.Final) return "Spatial path is stale, discontinuous or blocked.";
        SpatialRect previous = source.Bounds;
        foreach (var leg in gesture.Path)
        {
            var scene = new SpatialSnapshot(gesture.Initial.Environment, gesture.Initial.Posters.Select(p => p.PosterId == command.PosterId
                ? p with { Bounds = previous, ExpandedHeight = p.IsCollapsed ? p.ExpandedHeight : previous.Height } : p));
            var result = PosterSpatialSolver.PlanFrameChange(scene, new(command.PosterId, leg.To, gesture.Gesture,
                PosterOperatingMode.Editing, Math.Min(gesture.Initial.Environment.WorkArea.Width, Math.Max(1, (int)Math.Round(240 * gesture.Initial.Environment.DpiX / 96d))),
                Math.Min(gesture.Initial.Environment.WorkArea.Height, Math.Max(1, (int)Math.Round(240 * gesture.Initial.Environment.DpiY / 96d)))));
            if (!result.Succeeded) return "Spatial trace contains an invalid geometry transition.";
            previous = leg.To;
        }
        PosterDefinition expected = Capture(current, gesture.Final, gesture.Initial.Environment,
            command.Kind == PosterStateCommandKind.Resize ? gesture.Final.Height / (gesture.Initial.Environment.DpiY / 96d) : current.ExpandedHeightDip);
        return command.DesiredState == expected ? null : "Stored geometry does not match the swept physical result.";
    }

    public static PosterDefinition Capture(PosterDefinition current, SpatialRect bounds, SpatialEnvironment environment, double expandedHeightDip)
    {
        double sx = environment.DpiX / 96d, sy = environment.DpiY / 96d;
        SpatialRect work = environment.WorkArea;
        return current with
        {
            MonitorDeviceName = environment.WorkspaceId,
            FallbackXDip = (bounds.X - work.X) / sx, FallbackYDip = (bounds.Y - work.Y) / sy,
            WidthDip = bounds.Width / sx, ExpandedHeightDip = expandedHeightDip,
            HorizontalAnchor = Math.Clamp((bounds.X - work.X) / (double)Math.Max(1, work.Width - bounds.Width), 0, 1),
            VerticalAnchor = Math.Clamp((bounds.Y - work.Y) / (double)Math.Max(1, work.Height - bounds.Height), 0, 1),
            NormalizedWidth = Math.Clamp(bounds.Width / (double)work.Width, 0.000001, 1),
            NormalizedExpandedHeight = Math.Clamp(expandedHeightDip * sy / work.Height, 0.000001, 1)
        };
    }
}
