using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record SpatialPostureCommit(SpatialSnapshot Initial, bool Collapsed, int CompactHeight, SpatialRect? CompactAnchor)
{
    public bool AllowPush { get; init; }
    public bool UseContinuity { get; init; }
    public string? ReturnOperationId { get; init; }
    public PosterContentMetrics? ContentMetrics { get; init; }
}

public static class SpatialPostureCommitPolicy
{
    public static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        var context = command.SpatialPosture!;
        StateMutationPlan Invalid(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
        if (command.SpatialGesture is not null || command.Kind != PosterStateCommandKind.SetCollapsed || command.ReadSet is not { CatalogVersion: not null } reads ||
            reads.CatalogVersion != state.Catalog.CatalogVersion || !reads.Posters.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(state.Catalog.Posters.Select(p => p.Poster.PosterId))) return Invalid("Posture requires complete catalog CAS.");
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        var source = context.Initial.Posters.FirstOrDefault(p => p.PosterId == command.PosterId);
        if (source is null || current is null || source.IsLocked != current.IsLocked || source.IsCollapsed != current.IsCollapsed ||
            source.IsVisible != current.IsVisible) return Invalid("Posture source changed.");
        if (context.AllowPush)
        {
            foreach (var physical in context.Initial.Posters)
            {
                var entity = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == physical.PosterId);
                if (entity is null || entity.LayoutGeneration != physical.LayoutGeneration || entity.LastLayoutOperationId != physical.LastLayoutOperationId ||
                    entity.Poster.IsLocked != physical.IsLocked || entity.Poster.IsVisible != physical.IsVisible || entity.Poster.IsCollapsed != physical.IsCollapsed)
                    return Invalid("Spatial participant posture/generation changed.");
                if (context.ContentMetrics is not null && physical.ExpandedHeight !=
                    Math.Max(1, (int)Math.Round(entity.Poster.ExpandedHeightDip * context.Initial.Environment.DpiY / 96d)))
                    return Invalid("Physical snapshot replaced preferred height with an effective height.");
            }
            var proof = context.ReturnOperationId is { } op ? state.SpatialOperations.FirstOrDefault(p => p.OperationId == op) : null;
            if (context.ReturnOperationId is not null && proof is null) return Invalid("Return proof is no longer active.");
            if (context.ContentMetrics is { } metrics && metrics != PosterPresentationPolicy.Resolve(current).Metrics)
                return Invalid("Content metrics version changed.");
            if (context.UseContinuity && (context.ContentMetrics is null || context.ReturnOperationId is not null))
                return Invalid("Continuity requires current content metrics and no legacy return proof.");
            var continuous = context.UseContinuity
                ? SpatialContinuitySolver.Plan(context.Initial, command.PosterId, context.Collapsed, context.CompactHeight,
                    command.RequestId, context.ContentMetrics!, state.SpatialContinuities.FirstOrDefault(s =>
                        s.Environment.WorkspaceId == context.Initial.Environment.WorkspaceId)) : null;
            var group = continuous?.Group ?? (!context.Collapsed && context.ContentMetrics is not null
                ? SpatialExpansionPolicy.Plan(context.Initial, command.PosterId, context.CompactHeight, command.RequestId, context.ContentMetrics).Group
                : SpatialPushSolver.Plan(context.Initial, command.PosterId, context.Collapsed, context.CompactHeight, command.RequestId, proof));
            if (!group.Plan.Succeeded) return Invalid("No safe group layout: " + string.Join(',', group.Plan.Issues.Select(i => i.Reason)));
            if (group.Plan.Status == SpatialPlanStatus.NoChange)
                return command.DesiredState == current ? new() { ResultPosterId = current.PosterId } : Invalid("Unexpected no-change state.");
            var updates = group.Plan.Changes.Select(change =>
            {
                var before = state.Catalog.Posters.Single(p => p.Poster.PosterId == change.PosterId).Poster;
                return SpatialGestureCommitPolicy.Capture(before, change.After, context.Initial.Environment, before.ExpandedHeightDip)
                    with { IsCollapsed = change.IsCollapsed };
            }).ToArray();
            if ((updates.FirstOrDefault(p => p.PosterId == command.PosterId) ?? current) != command.DesiredState)
                return Invalid("Group endpoint or content parameters mismatch.");
            return new() { Updates = updates, ResultPosterId = current.PosterId,
                NewSpatialProof = group.NewProof, ConsumeSpatialOperationId = group.ConsumeOperationId,
                NewContinuity = continuous?.Next,
                TouchLayoutIds = continuous is null ? [] : group.Plan.Changes.Select(c => c.PosterId).ToArray() };
        }
        var plan = SpatialPostureSolver.Plan(context.Initial, command.PosterId, context.Collapsed, context.CompactHeight, context.CompactAnchor);
        if (!plan.Succeeded) return Invalid("No safe full-size posture path: " + string.Join(',', plan.Issues.Select(i => i.Reason)));
        if (plan.Status == SpatialPlanStatus.NoChange) return command.DesiredState == current ? new() { ResultPosterId = current.PosterId } : Invalid("Unexpected no-change geometry.");
        var geometry = plan.Changes.Single();
        var desired = SpatialGestureCommitPolicy.Capture(current, geometry.After, context.Initial.Environment, current.ExpandedHeightDip)
            with { IsCollapsed = context.Collapsed };
        if (desired != command.DesiredState) return Invalid("Posture geometry or unrelated parameters mismatch.");
        return new() { Updates = new[] { desired }, ResultPosterId = current.PosterId };
    }
}
