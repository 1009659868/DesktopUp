using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record EffectivePosterLayout(PosterDefinition Intent, SpatialRect Bounds, SpatialEnvironment Environment,
    string MetricsVersion, long Revision, long LayoutGeneration)
{
    public double HeightDip => Bounds.Height * 96d / Environment.DpiY;
    public bool IsConstrained => !Intent.IsCollapsed && Bounds.Height < (int)Math.Round(Intent.ExpandedHeightDip * Environment.DpiY / 96d);
    public bool Matches(PosterDefinition p, SpatialEnvironment environment, string metricsVersion) =>
        MetricsVersion == metricsVersion && Environment.WorkspaceId == environment.WorkspaceId &&
        Environment.WorkArea == environment.WorkArea && Environment.DpiX == environment.DpiX && Environment.DpiY == environment.DpiY &&
        SameGeometry(Intent, p);
    public static bool SameGeometry(PosterDefinition a, PosterDefinition b) =>
        a.MonitorDeviceName == b.MonitorDeviceName && a.FallbackXDip == b.FallbackXDip && a.FallbackYDip == b.FallbackYDip &&
        a.WidthDip == b.WidthDip && a.ExpandedHeightDip == b.ExpandedHeightDip && a.HorizontalAnchor == b.HorizontalAnchor &&
        a.VerticalAnchor == b.VerticalAnchor && a.NormalizedWidth == b.NormalizedWidth && a.NormalizedExpandedHeight == b.NormalizedExpandedHeight &&
        a.IsCollapsed == b.IsCollapsed;
}

// Session-only derived projection. No persistence API, no command authority and no animation replay.
public sealed class EffectivePosterLayouts
{
    private readonly Dictionary<string, EffectivePosterLayout> _entries = new(StringComparer.Ordinal);
    public EffectivePosterLayout? Find(PosterSurfaceProjection target) =>
        _entries.TryGetValue(target.Poster.PosterId, out var entry) && entry.Revision <= target.Revision &&
        EffectivePosterLayout.SameGeometry(entry.Intent, target.Poster)
            // Content edits reflow inside the current outer bounds. A new minimum applies
            // to the next explicit expansion, not an unsolicited window move on refresh.
            ? entry with { Intent = target.Poster, Revision = target.Revision, LayoutGeneration = target.LayoutGeneration,
                MetricsVersion = target.Presentation.Metrics.Version } : null;
    public void Set(PosterSurfaceProjection target, SpatialRect bounds, SpatialEnvironment environment) =>
        _entries[target.Poster.PosterId] = new(target.Poster, bounds, environment, target.Presentation.Metrics.Version, target.Revision, target.LayoutGeneration);
    public void RemoveMissing(DesktopUpProjection projection)
    {
        foreach (var id in _entries.Keys.Where(id => !projection.PostersById.TryGetValue(id, out var p) || !p.Poster.IsVisible).ToArray()) _entries.Remove(id);
    }

    // Restore from durable intent in a NEW scene. A valid ordinary layout remains untouched.
    // Only conflicting preferred rectangles need deterministic compact-seed planning. No old proof
    // or old temporary height is accepted as authority after a process restart/display change.
    public static SpatialSnapshot? Rebuild(SpatialSnapshot preferred, PosterContentMetrics metrics, int compactHeight,
        IReadOnlyDictionary<string, SpatialRect>? compactSeeds = null)
        => RebuildCore(preferred, _ => metrics, compactHeight, compactSeeds);

    public static SpatialSnapshot? Rebuild(SpatialSnapshot preferred, IReadOnlyDictionary<string, PosterContentMetrics> metrics,
        int compactHeight, IReadOnlyDictionary<string, SpatialRect>? compactSeeds = null) =>
        preferred.Posters.Any(p => !metrics.ContainsKey(p.PosterId)) ? null :
            RebuildCore(preferred, p => metrics[p.PosterId], compactHeight, compactSeeds);

    private static SpatialSnapshot? RebuildCore(SpatialSnapshot preferred, Func<SpatialPoster, PosterContentMetrics> metricsFor,
        int compactHeight, IReadOnlyDictionary<string, SpatialRect>? compactSeeds)
    {
        int Minimum(SpatialPoster p) => metricsFor(p).MinimumPixels(preferred.Environment.DpiY);
        if (PosterSpatialSolver.ValidateLayout(preferred).Count == 0 && !preferred.Posters.Any(p => p.IsVisible && !p.IsCollapsed &&
            p.ExpandedHeight >= Minimum(p) && p.Bounds.Height < Minimum(p))) return preferred;
        bool LegacyShort(SpatialPoster p) => !p.IsCollapsed && p.ExpandedHeight < Minimum(p);
        var compact = new SpatialSnapshot(preferred.Environment, preferred.Posters.Select(p => p with
            { Bounds = LegacyShort(p) ? p.Bounds : compactSeeds is not null && compactSeeds.TryGetValue(p.PosterId, out var seed) ? seed :
                p.Bounds with { Height = Math.Min(compactHeight, p.Bounds.Height) },
                IsCollapsed = !LegacyShort(p) }));
        if (PosterSpatialSolver.ValidateLayout(compact).Count != 0) return null;
        // Reserve full-size neighbors that leave every compact anchor clear BEFORE expanding
        // conflict targets. Otherwise a locked conflict target could take space from a fitting
        // locked neighbor merely because its stable ID sorts first.
        foreach (var intent in preferred.Posters.Where(p => p.IsVisible && !p.IsCollapsed && !LegacyShort(p))
            .OrderByDescending(p => p.IsLocked).ThenBy(p => p.PosterId, StringComparer.Ordinal))
        {
            if (!preferred.Environment.WorkArea.Contains(intent.Bounds) || compact.Posters.Any(p => p.IsVisible &&
                p.PosterId != intent.PosterId && p.Bounds.Overlaps(intent.Bounds))) continue;
            compact = new(compact.Environment, compact.Posters.Select(p => p.PosterId == intent.PosterId ? intent : p));
        }
        foreach (var intent in preferred.Posters.Where(p => p.IsVisible && !p.IsCollapsed && !LegacyShort(p))
            .OrderByDescending(p => p.IsLocked).ThenBy(p => p.PosterId, StringComparer.Ordinal))
        {
            if (!compact.Posters.Single(p => p.PosterId == intent.PosterId).IsCollapsed) continue;
            var result = SpatialExpansionPolicy.Plan(compact, intent.PosterId, compactHeight, "effective-rebuild-" + intent.PosterId, metricsFor(intent));
            if (!result.Group.Plan.Succeeded) return null; // Fail closed; do not show overlapping/half-restored windows.
            var changes = result.Group.Plan.Changes.ToDictionary(c => c.PosterId);
            compact = new(compact.Environment, compact.Posters.Select(p => changes.TryGetValue(p.PosterId, out var c)
                ? p with { Bounds = c.After, IsCollapsed = c.IsCollapsed } : p));
        }
        return PosterSpatialSolver.ValidateLayout(compact).Count == 0 ? compact : null;
    }
}
