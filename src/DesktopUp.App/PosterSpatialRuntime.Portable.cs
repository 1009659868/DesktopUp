using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    public DisplayTopologySnapshot? PortableTopology() => _suspended || IsSpatialBusy ? null : _effectiveTopology;

    public bool CanApplyPortable(PortableImportPlan plan)
    {
        if (_suspended || IsSpatialBusy || _effectiveTopology is not { } topology ||
            topology.Generation != plan.InitialTopology.Generation || topology.Signature != plan.TopologySignature ||
            !topology.Monitors.SequenceEqual(plan.InitialTopology.Monitors) ||
            !state.TryGetCurrent(out var current) || current is null || current.Revision != plan.BasisRevision ||
            current.CatalogVersion != plan.ReadSet.CatalogVersion ||
            current.ControlCenter.Settings.EntityVersion != plan.ReadSet.SettingsVersion ||
            current.Posters.Count != plan.ReadSet.Posters.Count ||
            current.Posters.Any(p => plan.ReadSet.Posters.GetValueOrDefault(p.Poster.PosterId) != p.EntityVersion)) return false;
        var occupied = new List<SpatialRect>();
        var replaced = plan.Removes.Concat(plan.Updates.Select(p => p.PosterId)).ToHashSet(StringComparer.Ordinal);
        foreach (var target in current.Posters.Where(p => p.IsEffectivelyVisible && !replaced.Contains(p.Poster.PosterId)))
        {
            DesktopPixelRect bounds;
            if (globallyHidden?.Invoke() == true)
            {
                var monitor = topology.Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, target.Poster.MonitorDeviceName,
                    StringComparison.OrdinalIgnoreCase)) ?? topology.PrimaryMonitor!;
                if (ResolveEffectiveBounds(target, monitor) is not { } latent) return false;
                bounds = latent;
            }
            else
            {
                if (!_windows.TryGetValue(target.Poster.PosterId, out var window) ||
                    !window.TryReadSpatial(_runtimeId, out _, out var actual, out long version) ||
                    version != target.EntityVersion || actual is null) return false;
                bounds = Pixels(actual.Bounds);
            }
            occupied.Add(Rect(bounds));
        }
        foreach (var poster in plan.Creates.Concat(plan.Updates).Where(p => p.IsVisible && !p.PlacementPending))
        {
            var monitor = topology.Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, poster.MonitorDeviceName,
                StringComparison.OrdinalIgnoreCase));
            if (monitor is null) return false;
            double height = poster.IsCollapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : poster.ExpandedHeightDip;
            if (Math.Round(poster.WidthDip * monitor.ScaleX) > monitor.WorkArea.Width ||
                Math.Round(height * monitor.ScaleY) > monitor.WorkArea.Height) return false;
            var bounds = Rect(PosterGeometryProjector.Project(new(poster.WidthDip, height,
                poster.HorizontalAnchor, poster.VerticalAnchor), monitor));
            if (!Rect(monitor.WorkArea).Contains(bounds) || occupied.Any(r => r.Overlaps(bounds))) return false;
            occupied.Add(bounds);
        }
        return true;
    }
}
