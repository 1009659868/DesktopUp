using DesktopUp.Domain;

namespace DesktopUp.Application;

// A narrow manual title command through the existing repository/CAS/publication machinery.
// No arbitrary Updates or neighbor rectangles are accepted from the caller.
public sealed record TitleSpatialCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, TitleSpatialTrace Trace);

public static class TitleSpatialCommitPolicy
{
    public static StateMutationPlan Prepare(TitleSpatialCommand command, DesktopStateSnapshot state)
        => PrepareCore(command, state, null);

    internal static StateMutationPlan PrepareAttested(TitleSpatialCommand command, DesktopStateSnapshot state,
        SpatialSnapshot initial, TitleProgressCheckpoint? checkpoint) => PrepareCore(command, state, initial, checkpoint);

    private static StateMutationPlan PrepareCore(TitleSpatialCommand command, DesktopStateSnapshot state, SpatialSnapshot? attested, TitleProgressCheckpoint? checkpoint = null)
    {
        StateMutationPlan Reject(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
        try
        {
            var start = command.Trace.Start;
            string id = start.Press.Capture.Press.Session.PosterId;
            var reads = command.ReadSet;
            if (command.Source != StateCommandSource.Poster || command.RequestId != start.Press.OperationId ||
                reads.CatalogVersion != state.Catalog.CatalogVersion || reads.SettingsVersion is not null ||
                !reads.Posters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Catalog.Posters.Select(p => p.Poster.PosterId)))
                return Reject("Title requires its manual identity and complete catalog CAS.");
            if (state.Catalog.Posters.Any(p => reads.Posters[p.Poster.PosterId] != p.EntityVersion))
                return StateMutationPlan.Reject(StateCommandStatus.Conflict, "A spatial read entity changed.");
            var source = state.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == id);
            if (source is null || source.Poster.PlacementPending || !source.Poster.AutoCollapseEnabled || !source.Poster.IsVisible)
                return Reject("Title source is not enabled/visible.");
            var initial = start.Initial;
            var previous = state.SpatialContinuities.SingleOrDefault(c => c.Environment.WorkspaceId == initial.Environment.WorkspaceId);
            string? error = ValidateInitial(initial, state, previous, attested);
            if (error is not null) return Reject(error);
            var replay = TitleSpatialSession.ReplayBounded(command.Trace, PosterPresentationPolicy.Resolve(source.Poster).Metrics,
                previous, checkpoint, true);
            var final = replay.Presented;
            var touched = final.Posters.Where(p => p != initial.Posters.Single(q => q.PosterId == p.PosterId))
                .Select(p => p.PosterId).ToHashSet(StringComparer.Ordinal);
            if (replay.HasUserMoved) touched.Add(id); // out-and-back is still explicit user adjustment
            if (touched.Count == 0) return new() { ResultPosterId = id };
            var updates = touched.Order(StringComparer.Ordinal).Select(key =>
            {
                var entity = state.Catalog.Posters.Single(p => p.Poster.PosterId == key);
                var physical = final.Posters.Single(p => p.PosterId == key);
                return SpatialGestureCommitPolicy.Capture(entity.Poster, physical.Bounds, initial.Environment,
                    entity.Poster.ExpandedHeightDip) with { IsCollapsed = physical.IsCollapsed };
            }).ToArray();
            // All accepted presentation samples map to exactly one durable layout generation.
            var durable = new SpatialSnapshot(final.Environment, final.Posters.Select(p => touched.Contains(p.PosterId)
                ? p with { LayoutGeneration = checked(p.LayoutGeneration + 1), LastLayoutOperationId = command.RequestId } : p));
            return new() { Updates = updates, ResultPosterId = id, TouchLayoutIds = touched.ToArray(),
                NewContinuity = TitleSpatialKernel.Rebase(durable, replay.Continuity) };
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException)
        { return Reject("Invalid title proof: " + e.Message); }
    }

    // Ordinary anchor projection or the current durable effective-layout witness. No arbitrary
    // recovery geometry is authorized: a newly rebuilt runtime layout needs a trusted adapter witness.
    private static string? ValidateInitial(SpatialSnapshot initial, DesktopStateSnapshot state, SpatialContinuityState? continuity, SpatialSnapshot? attested)
    {
        var env = initial.Environment;
        bool hostWitness = attested is not null && attested.Environment == initial.Environment &&
            attested.Posters.SequenceEqual(initial.Posters);
        var expected = state.Catalog.Posters.Where(p => p.Poster.IsVisible && !p.Poster.PlacementPending && (hostWitness
            ? attested!.Posters.Any(s => s.PosterId == p.Poster.PosterId)
            : p.Poster.MonitorDeviceName == env.WorkspaceId)).ToArray();
        if (!initial.Posters.Select(p => p.PosterId).ToHashSet(StringComparer.Ordinal)
            .SetEquals(expected.Select(p => p.Poster.PosterId))) return "Incomplete workspace occupancy.";
        foreach (var physical in initial.Posters)
        {
            var entry = expected.Single(p => p.Poster.PosterId == physical.PosterId);
            var p = entry.Poster;
            int preferred = checked((int)Math.Round(p.ExpandedHeightDip * env.DpiY / 96d));
            if (physical.LayoutGeneration != entry.LayoutGeneration || physical.LastLayoutOperationId != entry.LastLayoutOperationId ||
                physical.IsLocked != p.IsLocked || physical.IsVisible != p.IsVisible || physical.IsCollapsed != p.IsCollapsed ||
                physical.ExpandedHeight != preferred) return "Initial entity, posture or generation changed.";
            bool witnessed = hostWitness || continuity?.Environment == env && continuity.Nodes.Any(n => n.Expected == physical);
            var work = env.WorkArea;
            int width = Math.Clamp(checked((int)Math.Round(p.WidthDip * env.DpiX / 96d)), 1, work.Width);
            int height = Math.Clamp(p.IsCollapsed ? TitleSpatialKernel.Compact(env) : preferred, 1, work.Height);
            var ordinary = new SpatialRect(
                work.X + checked((int)Math.Round((work.Width - width) * p.HorizontalAnchor)),
                work.Y + checked((int)Math.Round((work.Height - height) * p.VerticalAnchor)), width, height);
            if (!witnessed && physical.Bounds != ordinary) return "Unwitnessed initial physical geometry.";
        }
        return null;
    }

    public static TitleSpatialCommand Freeze(TitleSpatialCommand command) => command with
    {
        ReadSet = command.ReadSet.Freeze(),
        Trace = command.Trace with { Samples = Array.AsReadOnly(command.Trace.Samples.ToArray()),
            Continuity = command.Trace.Continuity with { Nodes = Array.AsReadOnly(command.Trace.Continuity.Nodes.ToArray()) } }
    };
}
