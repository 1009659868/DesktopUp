using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentHeatExpansionProof(SpatialSnapshot Initial, ContentHeatProposal Proposal);
public interface IContentHeatExpansionCommitter
{
    StateCommandResult ExecuteContentHeatExpansion(PosterStateCommand command);
}

// Reuses the exact frame planner used by the manual scale slider. Only the caller's
// blocked-growth behavior differs: heat can cap its derived display without lowering facts.
public static class ContentHeatExpansion
{
    public static ControlCenterGeometryPlan? Plan(PosterDefinition current, ContentHeatProposal proposal,
        SpatialSnapshot scene, out string? error)
    {
        error = null;
        if (!ContentLayoutPolicy.TryRead(current.ContentLayoutJson, current.PosterId, out var author, out error) ||
            proposal.AuthorDocumentVersion != author!.DocumentVersion) return null;
        try
        {
            var presentation = PosterPresentationPolicy.Resolve(current);
            var viewport = new ContentLayoutViewport(10000, 10000).WithPresentation(presentation.Parameters);
            var target = ContentHeatSizing.Project(author, proposal, viewport, false);
            if (!ContentFrameMinimumPolicy.TryForPoster(current with { ContentLayoutJson = ContentLayoutPolicy.Serialize(target) },
                out var minimum, out error)) return null;
            return ContentScaleExpansion.PlanToMinimum(current, current, scene, minimum!, out error);
        }
        catch (ArgumentException failure) { error = failure.Message; return null; }
    }

    internal static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        var proof = command.ContentHeatExpansion!;
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (command.Source != StateCommandSource.System || command.Kind != PosterStateCommandKind.Resize || current is null ||
            command.ControlCenterGeometry is not null || command.ContentScaleExpansion is not null ||
            command.SpatialGesture is not null || command.SpatialPosture is not null ||
            !ControlCenterSpatial.CompleteReads(command.ReadSet, state) || !ControlCenterSpatial.SceneReadsCurrent(proof.Initial, state))
            return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, "Heat expansion requires exclusive current scene/version reads.");
        var plan = Plan(current, proof.Proposal, proof.Initial, out var error);
        if (plan is null || plan.Desired != command.DesiredState)
            return StateMutationPlan.Reject(StateCommandStatus.InsufficientSpace, error ?? "Heat expansion proof does not match.");
        return new() { Updates = current == plan.Desired ? [] : [plan.Desired], ResultPosterId = current.PosterId };
    }
}
