using DesktopUp.Domain;

namespace DesktopUp.Application;

// Runtime-only causality; entity revision is not a proxy for user intent or new space.
public sealed class AutoPostureEvidence
{
    private readonly Dictionary<string, long> _manual = new(StringComparer.Ordinal);
    private DesktopUpProjection? _previous;
    private readonly Dictionary<PosterInputSessionKey, long> _collapseExit = [];
    public long SpaceVersion { get; private set; }
    public long ManualVersion(string id) => _manual.GetValueOrDefault(id);
    public void ManualIntent(string id) => _manual[id] = checked(ManualVersion(id) + 1);
    public void CaptureManualCollapse(PosterInputSessionKey session, AutoPosturePointer pointer, bool collapse)
    {
        if (collapse && pointer.Presence is AutoPosturePresence.Inside or AutoPosturePresence.ExpansionZone)
            _collapseExit[session] = pointer.ExitVersion;
        else if (!collapse) _collapseExit.Remove(session);
    }
    public void CaptureTitleSuppression(TitleAutoRearmSuppression suppression) =>
        _collapseExit[suppression.Session] = suppression.ExitVersion;
    public long? ManualCollapseExit(PosterInputSessionKey session, AutoPosturePointer pointer)
    {
        if (!_collapseExit.TryGetValue(session, out long exit)) return null;
        if (pointer.ExitVersion <= exit) return exit;
        _collapseExit.Remove(session); return null;
    }
    public void ResetPointerBaseline() => _collapseExit.Clear();
    public void RetainSessions(IEnumerable<PosterInputSessionKey> sessions)
    {
        var live = sessions.ToHashSet();
        foreach (var key in _collapseExit.Keys.Where(k => !live.Contains(k)).ToArray()) _collapseExit.Remove(key);
    }

    public void Apply(StateProjectionChangedEventArgs change)
    {
        var next = change.Projection;
        if (_previous is { } old && next.Revision <= old.Revision) return;
        if (_previous is { } previous && change.Source != StateCommandSource.System)
        {
            bool space = previous.CatalogVersion != next.CatalogVersion;
            foreach (var target in next.Posters)
            {
                if (!previous.PostersById.TryGetValue(target.Poster.PosterId, out var before)) { space = true; continue; }
                if (before.Poster.IsCollapsed != target.Poster.IsCollapsed) ManualIntent(target.Poster.PosterId);
                space |= !Equals(SpatialIdentity(before.Poster), SpatialIdentity(target.Poster));
            }
            if (space) SpaceVersion = checked(SpaceVersion + 1);
        }
        foreach (string id in _manual.Keys.Where(id => !next.PostersById.ContainsKey(id)).ToArray()) _manual.Remove(id);
        _previous = next;
    }

    private static object SpatialIdentity(PosterDefinition p) => new
    {
        p.MonitorDeviceName, p.FallbackXDip, p.FallbackYDip, p.WidthDip, p.ExpandedHeightDip,
        p.HorizontalAnchor, p.VerticalAnchor, p.NormalizedWidth, p.NormalizedExpandedHeight,
        p.IsVisible, p.PlacementPending, p.IsLocked, p.IsCollapsed, Metrics = PosterPresentationPolicy.Resolve(p).Metrics
    };
}
