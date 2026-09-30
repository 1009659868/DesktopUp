using DesktopUp.Domain;
namespace DesktopUp.Application;

public static class PosturePendingPolicy
{
    public const int Capacity = 64;
    public const int LifetimeMilliseconds = 5000;
    public static bool CanAdopt(PosterDefinition basis, PosterDefinition current) =>
        (current with { IsCollapsed = basis.IsCollapsed, FallbackXDip = basis.FallbackXDip, FallbackYDip = basis.FallbackYDip,
            HorizontalAnchor = basis.HorizontalAnchor, VerticalAnchor = basis.VerticalAnchor,
            NormalizedWidth = basis.NormalizedWidth, NormalizedExpandedHeight = basis.NormalizedExpandedHeight }) == basis;
}

// Reservation covers every accepted path leg, not just current or destination rectangles.
public sealed record PostureGroupReservation(string WorkspaceId, IReadOnlyDictionary<string, SpatialRect> Sweeps)
{
    public static PostureGroupReservation Create(SpatialSnapshot initial, IReadOnlyList<SpatialBatchFrame> frames)
    {
        var bounds = new Dictionary<string, SpatialRect>(StringComparer.Ordinal);
        foreach (var leg in frames.SelectMany(f => f.Changes))
        {
            var prior = bounds.GetValueOrDefault(leg.PosterId, leg.From);
            int x = Math.Min(prior.X, leg.To.X), y = Math.Min(prior.Y, leg.To.Y);
            bounds[leg.PosterId] = new(x, y, checked((int)(Math.Max(prior.Right, leg.To.Right) - x)),
                checked((int)(Math.Max(prior.Bottom, leg.To.Bottom) - y)));
        }
        return new(initial.Environment.WorkspaceId, new System.Collections.ObjectModel.ReadOnlyDictionary<string, SpatialRect>(bounds));
    }
    public bool Conflicts(PostureGroupReservation other) => Sweeps.Keys.Any(other.Sweeps.ContainsKey) ||
        WorkspaceId == other.WorkspaceId && Sweeps.Values.Any(a => other.Sweeps.Values.Any(b =>
            a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y));
}

public static class PostureGroupRebase
{
    // An independent peer may commit first. Re-solve on its newly committed geometry and
    // continuity; retain the animated group's endpoints EXACTLY and keep full catalog CAS.
    public static PosterStateCommand? TryRebase(PosterStateCommand command, SpatialSnapshot initial,
        DesktopUpProjection current, IReadOnlyList<SpatialChange> expected)
    {
        if (command.SpatialPosture is not { UseContinuity: true, ContentMetrics: not null } context ||
            !current.PostersById.TryGetValue(command.PosterId, out var target) ||
            target.Presentation.Metrics != context.ContentMetrics) return null;
        var plan = SpatialContinuitySolver.Plan(initial, command.PosterId, context.Collapsed, context.CompactHeight,
            command.RequestId, context.ContentMetrics, current.SpatialContinuities.FirstOrDefault(c => c.Environment.WorkspaceId == initial.Environment.WorkspaceId));
        if (!plan.Group.Plan.Succeeded || !plan.Group.Plan.Changes.OrderBy(c => c.PosterId).SequenceEqual(expected.OrderBy(c => c.PosterId))) return null;
        return command with { ExpectedRevision = current.Revision,
            ReadSet = new StateReadSet(current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: current.CatalogVersion).Freeze(),
            SpatialPosture = context with { Initial = initial } };
    }
}
