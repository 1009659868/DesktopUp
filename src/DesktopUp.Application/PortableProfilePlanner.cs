using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum PortableImportMode { Append, Replace }
public sealed record PortableImportPlan(PortableProfileDocument? Document, PortableImportMode Mode, bool ImportSettings,
    DisplayTopologySnapshot InitialTopology, long BasisRevision, StateReadSet ReadSet,
    IReadOnlyList<PosterDefinition> Creates, IReadOnlyList<string> Removes, IReadOnlyList<PosterDefinition> Updates,
    DesktopUpSettings? Settings, IReadOnlyList<string> PendingPosterIds, IReadOnlyList<string> Warnings)
{
    public string TopologySignature => InitialTopology.Signature;
    public bool IsRetry => Document is null;
}

// Preview is pure and finite; Apply must recheck the full read set, topology and live native occupation.
public static class PortableProfilePlanner
{
    public const int MaximumPlacementCandidates = 8192;

    public static PortableImportPlan Plan(PortableProfileDocument document, DesktopStateSnapshot current,
        DisplayTopologySnapshot topology, PortableImportMode mode, bool importSettings, Func<string>? idFactory = null)
    {
        document = PortableProfileCodec.Freeze(document);
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Unknown import mode.");
        topology = FreezeTopology(topology);
        idFactory ??= () => Guid.NewGuid().ToString("N");
        var occupied = BuildObstacles(mode == PortableImportMode.Replace ? [] : current.Catalog.Posters.Select(p => p.Poster), topology);
        var ids = current.Catalog.Posters.Select(p => p.Poster.PosterId).ToHashSet(StringComparer.Ordinal);
        var themeIds = current.Catalog.Posters.Select(p => p.Poster.ThemeInstanceId).ToHashSet(StringComparer.Ordinal);
        var creates = new List<PosterDefinition>(); var warnings = new List<string>();
        long nextStack = mode == PortableImportMode.Replace || current.Catalog.Posters.Count == 0 ? 0 :
            checked(current.Catalog.Posters.Max(p => p.Poster.StackOrder) + 1);
        foreach (var portable in document.Posters.OrderBy(p => p.StackOrder).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            string token = idFactory();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 96 || token.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
                throw new ArgumentException("ID factory returned an invalid token.");
            string id = "poster-" + token;
            if (!ids.Add(id) || !themeIds.Add("theme-" + token)) throw new ArgumentException("ID factory returned a duplicate identity.");
            var definition = new PosterDefinition(id, portable.Title, portable.Subtitle, portable.MonitorHint,
                0, 0, portable.WidthDip, portable.ExpandedHeightDip, portable.HorizontalAnchor, portable.VerticalAnchor,
                1, 1, portable.IsLocked, portable.IsCollapsed, portable.AutoCollapseEnabled,
                portable.LayoutKind, portable.LayoutParametersJson, portable.TemplateKey)
            {
                Purpose = portable.Purpose, IsVisible = portable.IsVisible,
                AutoCollapseDelaySeconds = portable.AutoCollapseDelaySeconds,
                StackOrder = mode == PortableImportMode.Replace ? portable.StackOrder : nextStack++,
                ThemeInstanceId = "theme-" + token, CollectionId = null,
                ContentLayoutJson = portable.ContentLayout is { } content
                    ? ContentLayoutPolicy.Serialize(PortableContentLayoutCodec.Import(content, id)) : null
            };
            var placed = Place(definition, topology, occupied, warnings);
            if (!PosterPresentationPolicy.IsBuiltIn(definition.TemplateKey))
                warnings.Add($"{definition.Title}：主题 {definition.TemplateKey} 未安装，将使用内置回退；原参数和主题身份保留。");
            creates.Add(placed);
        }
        var settings = importSettings ? current.Settings.Settings with
        {
            DynamicEffectsPaused = document.Settings.DynamicEffectsPaused,
            PerformanceMode = document.Settings.PerformanceMode
            // LaunchAtStartupRequested is intentionally local and unchanged.
        } : null;
        return new(document, mode, importSettings, topology, current.Revision, Reads(current),
            creates.AsReadOnly(), Array.AsReadOnly(mode == PortableImportMode.Replace
                ? current.Catalog.Posters.Select(p => p.Poster.PosterId).ToArray() : []), Array.Empty<PosterDefinition>(),
            settings, Array.AsReadOnly(creates.Where(p => p.PlacementPending).Select(p => p.PosterId).ToArray()), warnings.AsReadOnly());
    }

    public static PortableImportPlan TryPlacePending(DesktopStateSnapshot current, DisplayTopologySnapshot topology)
    {
        topology = FreezeTopology(topology);
        var occupied = BuildObstacles(current.Catalog.Posters.Select(p => p.Poster), topology);
        var updates = new List<PosterDefinition>(); var pending = new List<string>(); var warnings = new List<string>();
        foreach (var original in current.Catalog.Posters.Where(p => p.Poster.PlacementPending)
            .OrderBy(p => p.Poster.StackOrder).ThenBy(p => p.Poster.PosterId, StringComparer.Ordinal).Select(p => p.Poster))
        {
            var placed = Place(original, topology, occupied, warnings);
            if (placed.PlacementPending) pending.Add(original.PosterId);
            // A failed retry never rewrites the pending intent or creates a gratuitous revision.
            else updates.Add(placed);
        }
        return new(null, PortableImportMode.Append, false, topology, current.Revision, Reads(current),
            Array.Empty<PosterDefinition>(), Array.Empty<string>(), updates.AsReadOnly(), null,
            pending.AsReadOnly(), warnings.AsReadOnly());
    }

    private static StateReadSet Reads(DesktopStateSnapshot state)
    {
        if (state.Catalog.Revision != state.Settings.Revision)
            throw new ArgumentException("Planning requires one committed state snapshot.");
        return new StateReadSet(state.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion, StringComparer.Ordinal),
            state.Settings.EntityVersion, state.Catalog.CatalogVersion).Freeze();
    }

    private static DisplayTopologySnapshot FreezeTopology(DisplayTopologySnapshot topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (string.IsNullOrWhiteSpace(topology.Signature) || topology.Generation < 0 || topology.Monitors is null ||
            topology.Monitors.Count is < 1 or > 64 || topology.Monitors.Select(m => m.DeviceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != topology.Monitors.Count ||
            topology.Monitors.Any(m => string.IsNullOrWhiteSpace(m.DeviceName) || m.DpiX is < 48 or > 768 || m.DpiY is < 48 or > 768 || !Rect(m.WorkArea).IsValid))
            throw new ArgumentException("A valid stable display topology is required.");
        return topology with { Monitors = Array.AsReadOnly(topology.Monitors.ToArray()) };
    }

    private static DisplayMonitorSnapshot Monitor(string hint, DisplayTopologySnapshot topology) =>
        topology.Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, hint, StringComparison.OrdinalIgnoreCase)) ?? topology.PrimaryMonitor!;

    private static Dictionary<string, List<SpatialRect>> BuildObstacles(IEnumerable<PosterDefinition> posters, DisplayTopologySnapshot topology)
    {
        var result = topology.Monitors.ToDictionary(m => m.DeviceName, _ => new List<SpatialRect>(), StringComparer.OrdinalIgnoreCase);
        // Persistent visibility is the occupation basis, even when the tray temporarily hides everything.
        foreach (var poster in posters.Where(p => p.IsVisible && !p.PlacementPending))
        {
            var monitor = Monitor(poster.MonitorDeviceName, topology);
            // Match PosterGeometryProjector's rounded/clamped restoration for existing configuration.
            // Production additionally compares native effective bounds before commit (constrained-height cases).
            var area = Rect(monitor.WorkArea);
            int width = (int)Math.Clamp(Math.Round(poster.WidthDip * monitor.ScaleX), 1, area.Width);
            double heightDip = poster.IsCollapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : poster.ExpandedHeightDip;
            int height = (int)Math.Clamp(Math.Round(heightDip * monitor.ScaleY), 1, area.Height);
            int x = checked(area.X + (int)Math.Round((area.Width - width) * poster.HorizontalAnchor));
            int y = checked(area.Y + (int)Math.Round((area.Height - height) * poster.VerticalAnchor));
            result[monitor.DeviceName].Add(new(x, y, width, height));
        }
        return result;
    }

    private static PosterDefinition Place(PosterDefinition definition, DisplayTopologySnapshot topology,
        Dictionary<string, List<SpatialRect>> occupied, List<string> warnings)
    {
        var monitor = Monitor(definition.MonitorDeviceName, topology);
        if (!string.Equals(monitor.DeviceName, definition.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"{definition.Title}：原屏幕缺失，映射到主屏幕 {monitor.DeviceName}。");
        var work = Rect(monitor.WorkArea);
        int width = checked((int)Math.Round(definition.WidthDip * monitor.ScaleX));
        double heightDip = definition.IsCollapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : definition.ExpandedHeightDip;
        int height = checked((int)Math.Round(heightDip * monitor.ScaleY));
        var preferred = new SpatialRect(checked(work.X + (int)Math.Round(Math.Max(0, work.Width - width) * definition.HorizontalAnchor)),
            checked(work.Y + (int)Math.Round(Math.Max(0, work.Height - height) * definition.VerticalAnchor)), width, height);
        var desired = definition with
        {
            MonitorDeviceName = monitor.DeviceName,
            FallbackXDip = (preferred.X - work.X) / monitor.ScaleX,
            FallbackYDip = (preferred.Y - work.Y) / monitor.ScaleY,
            NormalizedWidth = Math.Clamp(definition.WidthDip * monitor.ScaleX / work.Width, .000001, 1),
            NormalizedExpandedHeight = Math.Clamp(definition.ExpandedHeightDip * monitor.ScaleY / work.Height, .000001, 1)
        };
        SpatialRect? final = null;
        if (work.Contains(preferred))
        {
            var obstacles = occupied[monitor.DeviceName];
            if (!definition.IsVisible || obstacles.All(o => !o.Overlaps(preferred))) final = preferred;
            else
            {
                var xs = obstacles.SelectMany(o => new[] { o.Right, (long)o.X - width }).Append(work.X).Append(work.Right - width)
                    .Append(preferred.X).Where(x => x >= work.X && x + width <= work.Right).Distinct().Order().ToArray();
                var ys = obstacles.SelectMany(o => new[] { o.Bottom, (long)o.Y - height }).Append(work.Y).Append(work.Bottom - height)
                    .Append(preferred.Y).Where(y => y >= work.Y && y + height <= work.Bottom).Distinct().Order().ToArray();
                int budget = MaximumPlacementCandidates;
                foreach (long y in ys)
                {
                    foreach (long x in xs)
                    {
                        if (--budget < 0) break;
                        var candidate = new SpatialRect((int)x, (int)y, width, height);
                        if (obstacles.All(o => !o.Overlaps(candidate))) { final = candidate; break; }
                    }
                    if (final is not null || budget < 0) break;
                }
            }
        }
        if (final is not { } bounds)
        {
            warnings.Add($"{definition.Title}：当前空间无法安全安置，保留尺寸/收起/可见意图并暂不呈现；可在控制中心重试安置。");
            return desired with { PlacementPending = true };
        }
        if (definition.IsVisible) occupied[monitor.DeviceName].Add(bounds);
        return desired with
        {
            PlacementPending = false, FallbackXDip = (bounds.X - work.X) / monitor.ScaleX,
            FallbackYDip = (bounds.Y - work.Y) / monitor.ScaleY,
            HorizontalAnchor = Math.Clamp((bounds.X - work.X) / (double)Math.Max(1, work.Width - width), 0, 1),
            VerticalAnchor = Math.Clamp((bounds.Y - work.Y) / (double)Math.Max(1, work.Height - height), 0, 1)
            // Keep authored DIP extents exactly; repeated monitor projection must not accumulate rounding drift.
        };
    }
    private static SpatialRect Rect(DesktopPixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
