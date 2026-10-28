using DesktopUp.Domain;

namespace DesktopUp.Application;

// A single presentation+geometry transaction; the original presentation command is validated
// unchanged, then the same bounded spatial plan is recomputed from authoritative rows.
public sealed record ContentScaleExpansionProof(SpatialSnapshot Initial, PosterDefinition Presentation,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    ContentHeatProposal? Heat = null);

public static class ContentScaleExpansion
{
    public static bool ChangesScale(PosterDefinition before, PosterDefinition after) =>
        (PosterPresentationPolicy.Resolve(before).Parameters.Preferences ?? new()).Scale !=
        (PosterPresentationPolicy.Resolve(after).Parameters.Preferences ?? new()).Scale;

    public static ControlCenterGeometryPlan? Plan(PosterDefinition current, PosterDefinition presentation,
        SpatialSnapshot scene, out string? error, ContentHeatProposal? heat = null)
    {
        error = null;
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == current.PosterId);
        var metricsPoster = presentation;
        if (heat is not null)
        {
            if (!ContentLayoutPolicy.TryRead(presentation.ContentLayoutJson, presentation.PosterId, out var author, out error)) return null;
            try
            {
                var viewport = new ContentLayoutViewport(10000, 10000).WithPresentation(PosterPresentationPolicy.Resolve(presentation).Parameters);
                var target = ContentHeatSizing.Project(author!, heat, viewport, false);
                metricsPoster = presentation with { ContentLayoutJson = ContentLayoutPolicy.Serialize(target) };
            }
            catch (ArgumentException failure) { error = failure.Message; return null; }
        }
        if (source is null || !ContentFrameMinimumPolicy.TryForPoster(metricsPoster, out var minimum, out error))
        { error ??= "海报空间尚未就绪。"; return null; }
        return PlanToMinimum(current, presentation, scene, minimum!, out error);
    }

    internal static ControlCenterGeometryPlan? PlanToMinimum(PosterDefinition current, PosterDefinition presentation,
        SpatialSnapshot scene, ContentFrameMinimum minimum, out string? error)
    {
        error = null;
        var source = scene.Posters.FirstOrDefault(p => p.PosterId == current.PosterId);
        if (source is null) { error = "海报空间尚未就绪。"; return null; }
        var env = scene.Environment;
        var min = minimum.Pixels(env.DpiX, env.DpiY);
        int width = Math.Max(source.Bounds.Width, min.Width);
        int height = Math.Max(current.IsCollapsed ? source.ExpandedHeight : source.Bounds.Height, min.Height);
        if (width == source.Bounds.Width && height == (current.IsCollapsed ? source.ExpandedHeight : source.Bounds.Height))
            return new(presentation, [], []);
        if (current.IsLocked || current.IsCollapsed)
        { error = "当前尺度需要扩大外框；请先解除锁定并展开海报。"; return null; }
        var intent = new PosterGeometryIntent((source.Bounds.X - env.WorkArea.X) * 96d / env.DpiX,
            (source.Bounds.Y - env.WorkArea.Y) * 96d / env.DpiY, width * 96d / env.DpiX, height * 96d / env.DpiY);
        // Keep the origin; do not relocate or push neighbours to make a setting fit.
        return ControlCenterSpatial.Plan(presentation, scene, intent, out error);
    }

    internal static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        var proof = command.ContentScaleExpansion!;
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (command.Kind != PosterStateCommandKind.SetPresentation || current is null ||
            command.ControlCenterGeometry is not null || command.SpatialGesture is not null || command.SpatialPosture is not null ||
            !ControlCenterSpatial.CompleteReads(command.ReadSet, state) ||
            !ControlCenterSpatial.SceneReadsCurrent(proof.Initial, state) || !ChangesScale(current, proof.Presentation))
            return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, "Scale expansion requires current complete scene/version reads.");
        var presentation = StateCommandPolicy.Prepare(command with
            { ContentScaleExpansion = null, DesiredState = proof.Presentation }, state);
        if (presentation.Rejection is not null) return presentation;
        var plan = Plan(current, proof.Presentation, proof.Initial, out var error, proof.Heat);
        if (plan is null || plan.Desired != command.DesiredState)
            return StateMutationPlan.Reject(StateCommandStatus.InsufficientSpace, error ?? "Scale expansion proof does not match the command.");
        return presentation with { Updates = [plan.Desired] };
    }
}
