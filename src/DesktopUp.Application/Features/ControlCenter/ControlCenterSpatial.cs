using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ControlCenterGeometryProof(SpatialSnapshot Initial, PosterGeometryIntent Intent);
public sealed record ControlCenterPlacementProof(SpatialSnapshot Initial);
public sealed record ControlCenterGeometryPlan(PosterDefinition Desired, IReadOnlyList<SpatialPathLeg> Path,
    IReadOnlyList<SpatialChange> Changes);

// Exact numeric intent: bounded move/resize ordering, no pushing, clamping or second solver.
public static class ControlCenterSpatial
{
    public static bool SceneReadsCurrent(SpatialSnapshot scene, DesktopStateSnapshot state)
    {
        var byId = state.Catalog.Posters.ToDictionary(p => p.Poster.PosterId);
        return scene.Posters.All(p => byId.TryGetValue(p.PosterId, out var t) && !t.Poster.PlacementPending && p.LayoutGeneration == t.LayoutGeneration &&
            p.IsVisible == t.Poster.IsVisible && p.IsLocked == t.Poster.IsLocked && p.IsCollapsed == t.Poster.IsCollapsed) &&
            state.Catalog.Posters.Where(p => p.Poster.IsVisible && !p.Poster.PlacementPending && string.Equals(p.Poster.MonitorDeviceName,
                scene.Environment.WorkspaceId, StringComparison.OrdinalIgnoreCase)).All(p => scene.Posters.Any(s => s.PosterId == p.Poster.PosterId));
    }
    public static bool CompleteReads(StateReadSet? reads, DesktopStateSnapshot state) =>
        reads?.CatalogVersion == state.Catalog.CatalogVersion && reads.Posters.Count == state.Catalog.Posters.Count &&
        state.Catalog.Posters.All(p => reads.Posters.GetValueOrDefault(p.Poster.PosterId) == p.EntityVersion);

    public static ControlCenterGeometryPlan? Plan(PosterDefinition poster, SpatialSnapshot scene,
        PosterGeometryIntent intent, out string? error)
    {
        error = "空间尚未就绪、目标受保护或输入无效。";
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == poster.PosterId);
        if (source is null || poster.PlacementPending || !poster.IsVisible || poster.IsLocked || source.IsLocked != poster.IsLocked ||
            source.IsCollapsed != poster.IsCollapsed || !source.IsVisible || PosterSpatialSolver.ValidateLayout(scene).Count != 0)
            return null;
        bool resize = intent.Width != poster.WidthDip || intent.ExpandedHeight != poster.ExpandedHeightDip;
        if (resize && poster.IsCollapsed) { error = "请先展开再修改尺寸。"; return null; }
        if (AuthoritativeStateValidationPolicy.Validate(poster with { FallbackXDip = intent.X, FallbackYDip = intent.Y,
            WidthDip = intent.Width, ExpandedHeightDip = intent.ExpandedHeight }) is { } invalid)
        { error = invalid; return null; }
        var env = scene.Environment;
        double effectiveHeight = resize ? intent.ExpandedHeight : source.Bounds.Height * 96d / env.DpiY;
        if (!SpatialPixelProjection.TryProjectLocalDip(intent.X, intent.Y, intent.Width, effectiveHeight,
            env.WorkArea.X, env.WorkArea.Y, env.DpiX, env.DpiY, out var end)) return null;
        // A position-only edit keeps the exact current physical dimensions, including constrained height.
        if (!resize) end = end with { Width = source.Bounds.Width, Height = source.Bounds.Height };
        int minWidth = (int)Math.Ceiling(PosterFrameGeometryPolicy.MinimumWidthDip * env.DpiX / 96d);
        int minHeight = (int)Math.Ceiling(PosterFrameGeometryPolicy.MinimumExpandedHeightDip * env.DpiY / 96d);
        if (resize)
        {
            if (!ContentFrameMinimumPolicy.TryForPoster(poster, out var minimum, out error, heatLowerBound: true)) return null;
            (minWidth, minHeight) = minimum!.Pixels(env.DpiX, env.DpiY);
            if (end.Width < minWidth || end.Height < minHeight)
            { error = "尺寸低于最大完整 Item 与海报边距所需下限；未缩小内容或改写布局。"; return null; }
        }
        if (!env.WorkArea.Contains(end)) { error = "输入超出当前工作区；未自动缩小或改写位置。"; return null; }
        foreach (bool moveFirst in new[] { true, false })
        {
            var path = new List<SpatialPathLeg>();
            var previous = source.Bounds;
            var middle = moveFirst ? previous with { X = end.X, Y = end.Y } : previous with { Width = end.Width, Height = end.Height };
            bool accepted = true;
            foreach (var (next, kind) in new[] { (middle, moveFirst ? PosterGestureKind.Move : PosterGestureKind.ResizeBottomRight),
                (end, moveFirst ? PosterGestureKind.ResizeBottomRight : PosterGestureKind.Move) })
            {
                if (previous == next) continue;
                var frameScene = new SpatialSnapshot(env, scene.Posters.Select(p => p.PosterId == poster.PosterId
                    ? p with { Bounds = previous, ExpandedHeight = Math.Max(p.ExpandedHeight, previous.Height) } : p));
                var step = PosterSpatialSolver.PlanFrameChange(frameScene, new(poster.PosterId, next, kind,
                    PosterOperatingMode.Editing, minWidth, minHeight));
                if (!step.Succeeded) { accepted = false; break; }
                path.AddRange(step.Path); previous = next;
            }
            if (!accepted) continue;
            var desired = SpatialGestureCommitPolicy.Capture(poster, end, env,
                resize ? end.Height * 96d / env.DpiY : poster.ExpandedHeightDip);
            error = null;
            return new(desired, path.AsReadOnly(), Array.AsReadOnly(new[] { new SpatialChange(poster.PosterId,
                source.Bounds, end, poster.IsCollapsed, poster.IsCollapsed, resize ? SpatialChangeCause.Resize : SpatialChangeCause.Move) }));
        }
        error = "移动或缩放路径被其他海报阻挡；请调整输入位置或先分步移动。未修改任何海报。";
        return null;
    }

    public static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        var proof = command.ControlCenterGeometry!;
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (command.Source != StateCommandSource.ControlCenter || current is null ||
            !CompleteReads(command.ReadSet, state) || !SceneReadsCurrent(proof.Initial, state) || command.SpatialGesture is not null || command.SpatialPosture is not null)
            return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, "Geometry requires an exclusive proof and complete entity/catalog CAS.");
        var plan = Plan(current, proof.Initial, proof.Intent, out var error);
        bool resize = proof.Intent.Width != current.WidthDip || proof.Intent.ExpandedHeight != current.ExpandedHeightDip;
        if (plan is null || command.Kind != (resize ? PosterStateCommandKind.Resize : PosterStateCommandKind.Move) || command.DesiredState != plan.Desired)
            return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error ?? "Geometry proof does not match the command.");
        return new() { Updates = current == plan.Desired ? [] : new[] { plan.Desired }, ResultPosterId = current.PosterId };
    }

    // Deterministic finite bottom/edge candidates; hidden posters do not occupy the desktop.
    public static PosterDefinition? Place(PosterDefinition configuration, SpatialSnapshot scene)
    {
        if (PosterSpatialSolver.ValidateLayout(scene).Count != 0) return null;
        var env = scene.Environment;
        double height = configuration.IsCollapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : configuration.ExpandedHeightDip;
        if (!SpatialPixelProjection.TryProjectLocalDip(0, 0, configuration.WidthDip, height,
            env.WorkArea.X, env.WorkArea.Y, env.DpiX, env.DpiY, out var size)) return null;
        var obstacles = scene.Posters.Where(p => p.IsVisible).Select(p => p.Bounds).ToArray();
        var xs = obstacles.Select(p => p.Right).Append(env.WorkArea.X).Distinct().Order().ToArray();
        var ys = obstacles.Select(p => p.Bottom).Append(env.WorkArea.Y).Distinct().Order().ToArray();
        foreach (long y in ys)
        foreach (long x in xs)
        {
            if (x > int.MaxValue || y > int.MaxValue) continue;
            var candidate = size with { X = (int)x, Y = (int)y };
            if (env.WorkArea.Contains(candidate) && obstacles.All(o => !o.Overlaps(candidate)))
                return SpatialGestureCommitPolicy.Capture(configuration, candidate, env, configuration.ExpandedHeightDip);
        }
        return null;
    }
}
