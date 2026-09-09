using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private readonly EffectivePosterLayouts _effective = new();
    private DisplayTopologySnapshot? _effectiveTopology;
    public void SetEffectiveTopology(DisplayTopologySnapshot topology) => _effectiveTopology = topology;
    public event Action? EffectiveLayoutChanged;
    public EffectivePosterLayout? GetEffectiveLayout(PosterSurfaceProjection target) => _effective.Find(target);

    public DesktopPixelRect? ResolveEffectiveBounds(PosterSurfaceProjection target, DisplayMonitorSnapshot monitor)
    {
        var environment = new SpatialEnvironment(monitor.DeviceName, Rect(monitor.WorkArea), monitor.DpiX, monitor.DpiY, 0, 0, _runtimeId);
        if (_effective.Find(target) is { } cached && cached.Matches(target.Poster, environment, target.Presentation.Metrics.Version))
            return Pixels(cached.Bounds);
        if (!state.TryGetCurrent(out var current) || current is null) return null;
        // The registry resolves unknown/disconnected monitor affinities to the primary display.
        // Use the same monitor mapping, retaining all siblings in this workspace for recovery.
        var targets = current.Posters.Where(p => p.Poster.IsVisible &&
            (string.Equals(p.Poster.MonitorDeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase) ||
             monitor == _effectiveTopology?.PrimaryMonitor &&
             !_effectiveTopology.Monitors.Any(m => string.Equals(m.DeviceName, p.Poster.MonitorDeviceName, StringComparison.OrdinalIgnoreCase)))).ToArray();
        if (!targets.Any(p => p.Poster.PosterId == target.Poster.PosterId)) targets = targets.Append(target).ToArray();
        var preferred = targets.Select(p =>
        {
            int height = Math.Max(1, (int)Math.Round(p.Poster.ExpandedHeightDip * monitor.ScaleY));
            var placement = new PosterPlacementState(Math.Min(p.Poster.WidthDip, monitor.WorkArea.Width / monitor.ScaleX),
                p.Poster.IsCollapsed ? Math.Min(PosterFrameGeometryPolicy.CollapsedHeightDip, monitor.WorkArea.Height / monitor.ScaleY)
                    : Math.Min(p.Poster.ExpandedHeightDip, monitor.WorkArea.Height / monitor.ScaleY), p.Poster.HorizontalAnchor, p.Poster.VerticalAnchor);
            var rect = Rect(PosterGeometryProjector.Project(placement, monitor));
            return new SpatialPoster(p.Poster.PosterId, rect, Math.Max(height, rect.Height), p.Poster.IsVisible,
                p.Poster.IsLocked, p.Poster.IsCollapsed, p.LayoutGeneration, p.LastLayoutOperationId);
        }).ToArray();
        int compactHeight = Math.Max(1, (int)Math.Round(PosterFrameGeometryPolicy.CollapsedHeightDip * monitor.ScaleY));
        var compactSeeds = targets.Where(t => t.LayoutGeneration > 0).ToDictionary(t => t.Poster.PosterId, t =>
        {
            var rect = preferred.Single(p => p.PosterId == t.Poster.PosterId).Bounds;
            int height = Math.Min(compactHeight, rect.Height);
            // Saved top-left is position intent, not a saved temporary height. It prevents a
            // preferred-height anchor from shifting a compact recovery seed into a locked peer.
            int x = (int)Math.Clamp(monitor.WorkArea.X + Math.Round(t.Poster.FallbackXDip * monitor.ScaleX),
                monitor.WorkArea.X, monitor.WorkArea.Right - rect.Width);
            int y = (int)Math.Clamp(monitor.WorkArea.Y + Math.Round(t.Poster.FallbackYDip * monitor.ScaleY),
                monitor.WorkArea.Y, monitor.WorkArea.Bottom - height);
            return new SpatialRect(x, y, rect.Width, height);
        });
        var rebuilt = EffectivePosterLayouts.Rebuild(new(environment, preferred),
            targets.ToDictionary(t => t.Poster.PosterId, t => t.Presentation.Metrics), compactHeight, compactSeeds);
        if (rebuilt is null)
        {
            log.Write("warning", "spatial.effective_rebuild_blocked", new { target.Poster.PosterId, monitor.DeviceName, reason = "no-safe-intent-layout" });
            return null;
        }
        foreach (var p in rebuilt.Posters) _effective.Set(targets.Single(t => t.Poster.PosterId == p.PosterId), p.Bounds, environment);
        return Pixels(rebuilt.Posters.Single(p => p.PosterId == target.Poster.PosterId).Bounds);
    }

    public void RecordEffectiveFrame(PosterSurfaceProjection target, DesktopPixelRect bounds, SpatialEnvironment environment)
    {
        _effective.Set(target, Rect(bounds), environment);
        EffectiveLayoutChanged?.Invoke();
    }
}
