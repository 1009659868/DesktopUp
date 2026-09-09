using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record SpatialPreparedChange(SpatialChange Geometry, long ExpectedLayoutGeneration,
    long NextLayoutGeneration);

// A frozen proposal, not a repository or a public UI execution endpoint.
// M04-03..05 must revalidate inside the existing transaction and prepare the whole physical batch.
public sealed class SpatialBatchCommand
{
    public string OperationId { get; }
    public string SourcePosterId { get; }
    public StateCommandSource Origin { get; }
    public long ObservedRevision { get; }
    public SpatialEnvironment Environment { get; }
    public StateReadSet ReadSet { get; }
    public IReadOnlyList<SpatialPreparedChange> Changes { get; }

    public SpatialBatchCommand(string operationId, string sourcePosterId, StateCommandSource origin,
        long observedRevision, SpatialEnvironment environment, StateReadSet readSet,
        IEnumerable<SpatialPreparedChange> changes)
    {
        OperationId = operationId;
        SourcePosterId = sourcePosterId;
        Origin = origin;
        ObservedRevision = observedRevision;
        Environment = environment;
        ReadSet = readSet.Freeze();
        Changes = Array.AsReadOnly(changes.OrderBy(c => c.Geometry.PosterId, StringComparer.Ordinal).ToArray());
    }
}

public enum SpatialCommandFailure
{
    InvalidCommand, IncompleteReadSet, ReadConflict, StaleEnvironment, InvalidCandidate, PermissionDenied
}
public sealed record SpatialCommandIssue(SpatialCommandFailure Reason, string? PosterId, string Detail);

public static class SpatialCommandContract
{
    // Deliberately includes hidden/off-workspace entities. An entity can enter the occupied region
    // without changing catalog membership; restricting reads to moved/visible posters is unsafe.
    public static StateReadSet CaptureReadSet(DesktopStateSnapshot snapshot) => new StateReadSet(
        snapshot.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion, StringComparer.Ordinal),
        CatalogVersion: snapshot.Catalog.CatalogVersion).Freeze();

    // Pure preflight diagnostics only; not a substitute for the repository's transaction CAS.
    public static IReadOnlyList<SpatialCommandIssue> Validate(SpatialBatchCommand command,
        DesktopStateSnapshot current, SpatialSnapshot physical)
    {
        var issues = new List<SpatialCommandIssue>();
        if (string.IsNullOrWhiteSpace(command.OperationId) || command.OperationId.Length > 128 ||
            string.IsNullOrWhiteSpace(command.SourcePosterId) || !Enum.IsDefined(command.Origin) ||
            command.ObservedRevision < 0 || command.ObservedRevision > current.Revision ||
            command.ReadSet.SettingsVersion is not null || !command.Environment.IsValid)
            issues.Add(new(SpatialCommandFailure.InvalidCommand, null, "Invalid spatial command identity or observation."));
        if (command.Environment != physical.Environment)
            issues.Add(new(SpatialCommandFailure.StaleEnvironment, null, "Workspace/DPI/display/shell/session changed."));

        var currentById = current.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, StringComparer.Ordinal);
        if (command.ReadSet.CatalogVersion is null ||
            (command.ReadSet.CatalogVersion == current.Catalog.CatalogVersion &&
             !command.ReadSet.Posters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(currentById.Keys)))
            issues.Add(new(SpatialCommandFailure.IncompleteReadSet, null, "Full catalog membership and entity versions are required."));
        if (command.ReadSet.CatalogVersion != current.Catalog.CatalogVersion)
            issues.Add(new(SpatialCommandFailure.ReadConflict, null, "Catalog membership/order changed."));
        foreach ((string id, long expected) in command.ReadSet.Posters)
            if (!currentById.TryGetValue(id, out PosterStateSnapshot? value) || value.EntityVersion != expected)
                issues.Add(new(SpatialCommandFailure.ReadConflict, id, "Read entity was changed or removed."));
        if (!currentById.ContainsKey(command.SourcePosterId))
            issues.Add(new(SpatialCommandFailure.InvalidCommand, command.SourcePosterId, "Source is missing."));
        if (command.Changes.Count == 0 || command.Changes.Select(c => c.Geometry.PosterId).Distinct(StringComparer.Ordinal).Count() != command.Changes.Count)
            issues.Add(new(SpatialCommandFailure.InvalidCommand, null, "Changes must be nonempty and uniquely targeted."));
        SpatialPreparedChange? sourceChange = command.Changes.FirstOrDefault(c => c.Geometry.PosterId == command.SourcePosterId);
        if (sourceChange is null || sourceChange.Geometry.Cause is SpatialChangeCause.Push or SpatialChangeCause.ConditionalReturn)
            issues.Add(new(SpatialCommandFailure.InvalidCommand, command.SourcePosterId, "An explicit source operation is required."));
        IReadOnlyList<SpatialIssue> badLayout = PosterSpatialSolver.ValidateLayout(physical);
        if (badLayout.Count > 0)
            issues.Add(new(SpatialCommandFailure.InvalidCandidate, null, "Current layout requires recovery."));
        // Do not construct an ambiguous ID map or examine unsafe geometry after malformed input.
        if (issues.Count > 0) return issues.AsReadOnly();

        var physicalById = physical.Posters.ToDictionary(p => p.PosterId, StringComparer.Ordinal);
        var candidate = new Dictionary<string, SpatialPoster>(physicalById, StringComparer.Ordinal);
        foreach (SpatialPreparedChange prepared in command.Changes)
        {
            SpatialChange change = prepared.Geometry;
            if (!physicalById.TryGetValue(change.PosterId, out SpatialPoster? before) ||
                !currentById.TryGetValue(change.PosterId, out PosterStateSnapshot? state) ||
                !command.ReadSet.Posters.ContainsKey(change.PosterId) || !Enum.IsDefined(change.Cause) ||
                change.Before != before.Bounds || change.WasCollapsed != before.IsCollapsed ||
                prepared.ExpectedLayoutGeneration != before.LayoutGeneration || before.LayoutGeneration == long.MaxValue ||
                prepared.NextLayoutGeneration != before.LayoutGeneration + 1 || !change.After.IsValid ||
                !before.IsVisible || state.Poster.IsVisible != before.IsVisible || state.Poster.IsLocked != before.IsLocked ||
                state.Poster.IsCollapsed != before.IsCollapsed)
            {
                issues.Add(new(SpatialCommandFailure.InvalidCandidate, change.PosterId, "Geometry, posture or layout generation is stale/invalid."));
                continue;
            }
            bool ownCollapse = change.PosterId == command.SourcePosterId &&
                change.Cause is SpatialChangeCause.Expand or SpatialChangeCause.Collapse;
            if (before.IsLocked && !ownCollapse)
                issues.Add(new(SpatialCommandFailure.PermissionDenied, change.PosterId, "Locked posters are fixed obstacles."));
            bool neighbor = change.PosterId != command.SourcePosterId;
            if (!neighbor && !MatchesSourceIntent(change, before))
                issues.Add(new(SpatialCommandFailure.InvalidCandidate, change.PosterId, "Source operation does not match the proposed geometry/posture."));
            if (neighbor && sourceChange!.Geometry.Cause is not (SpatialChangeCause.Expand or SpatialChangeCause.Collapse))
                issues.Add(new(SpatialCommandFailure.InvalidCandidate, change.PosterId, "Ordinary dragging does not authorize moving neighbors."));
            if (neighbor && (change.Cause is not (SpatialChangeCause.Push or SpatialChangeCause.ConditionalReturn) ||
                change.Before.Width != change.After.Width || change.Before.Height != change.After.Height ||
                change.WasCollapsed != change.IsCollapsed))
                issues.Add(new(SpatialCommandFailure.InvalidCandidate, change.PosterId, "A neighbor may only translate without changing posture/size."));
            candidate[change.PosterId] = before with { Bounds = change.After, IsCollapsed = change.IsCollapsed,
                ExpandedHeight = change.Cause == SpatialChangeCause.Resize ? change.After.Height : before.ExpandedHeight,
                LayoutGeneration = prepared.NextLayoutGeneration, LastLayoutOperationId = command.OperationId };
        }
        if (issues.Count == 0 && PosterSpatialSolver.ValidateLayout(new(physical.Environment, candidate.Values)).Count > 0)
            issues.Add(new(SpatialCommandFailure.InvalidCandidate, null, "Final candidate overlaps or is outside the work area."));
        // Continuous multi-window trajectories, causal receipts and permission/intent validation
        // are mandatory at the future execution boundary; a successful preflight is NOT commit authority.
        return issues.AsReadOnly();
    }

    private static bool MatchesSourceIntent(SpatialChange change, SpatialPoster before) => change.Cause switch
    {
        SpatialChangeCause.Move => change.Before.Width == change.After.Width &&
            change.Before.Height == change.After.Height && change.WasCollapsed == change.IsCollapsed,
        SpatialChangeCause.Resize => !change.WasCollapsed && !change.IsCollapsed,
        SpatialChangeCause.Expand => change.WasCollapsed && !change.IsCollapsed &&
            change.After.Width == change.Before.Width && change.After.Height == before.ExpandedHeight,
        SpatialChangeCause.Collapse => !change.WasCollapsed && change.IsCollapsed &&
            change.After.Width == change.Before.Width && change.After.Height <= before.ExpandedHeight,
        SpatialChangeCause.Placement or SpatialChangeCause.Recovery => change.After.Width == change.Before.Width &&
            change.After.Height == change.Before.Height && change.WasCollapsed == change.IsCollapsed,
        _ => false
    };
}
