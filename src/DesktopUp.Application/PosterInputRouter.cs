namespace DesktopUp.Application;

public readonly record struct PosterInputSessionKey(string PosterId, long Epoch);
public sealed record PosterInputHit(PosterInputSessionKey Session, long LayoutRevision,
    DesktopPixelRect Frame, PosterInputRegion Region);
public sealed record PosterInputCapture(Guid Id, PosterInputHit Press);

// UI-thread owned. This is a routing/capture lease, never a second layout or state authority.
public sealed class PosterInputRouter
{
    private sealed record Surface(PosterInputSessionKey Key, DesktopPixelRect Frame, PosterInputLayoutSnapshot? Layout, long LastRevision = 0);
    private readonly Dictionary<string, Surface> _surfaces = new(StringComparer.Ordinal);
    private bool _suspended;
    public PosterInputCapture? Capture { get; private set; }
    public int Count => _surfaces.Count;
    public int ReadyCount => _suspended ? 0 : _surfaces.Values.Count(s => s.Layout is not null);

    // A wake-up hint only. Observers must re-read native ownership and current geometry;
    // routed events do not assert hover or create an interactive command for blank areas.
    public event Action? PointerActivity;
    public bool HasPointerObservers => PointerActivity is not null;
    public Exception? NotifyPointerActivity(PosterInputSessionKey key, long revision)
    {
        try { if (IsCurrent(key, revision)) PointerActivity?.Invoke(); }
        catch (Exception error) { return error; } // a passive observer cannot break active input
        return null;
    }
    public PosterInputSurfaceSnapshot? ReadSurface(PosterInputSessionKey key) => !_suspended &&
        _surfaces.TryGetValue(key.PosterId, out var s) && s.Key == key && s.Layout is { } layout
            ? new(key, s.Frame, layout) : null;

    public void Register(PosterInputSessionKey key)
    {
        if (string.IsNullOrWhiteSpace(key.PosterId) || key.Epoch < 1) throw new ArgumentException("Invalid input identity.");
        if (_surfaces.TryGetValue(key.PosterId, out var old))
        {
            if (old.Key.Epoch >= key.Epoch) throw new InvalidOperationException("Input epochs must increase.");
            Invalidate(old.Key);
        }
        _surfaces[key.PosterId] = new(key, default, null);
    }

    public bool Publish(PosterInputSessionKey key, DesktopPixelRect frame, PosterInputLayoutSnapshot layout)
    {
        if (!_surfaces.TryGetValue(key.PosterId, out var current) || current.Key != key ||
            layout.Revision < current.LastRevision || (current.Layout is null && layout.Revision <= current.LastRevision) || layout.Revision < 1 || !frame.IsUsable ||
            frame.Width != layout.FrameWidth || frame.Height != layout.FrameHeight || layout.OuterCornerRadius < 0 ||
            layout.Regions.Any(r => !r.Bounds.IsUsable || r.Bounds.X < 0 || r.Bounds.Y < 0 ||
                r.Bounds.Right > frame.Width || r.Bounds.Bottom > frame.Height)) return false;
        if (current.Layout is { } previous && previous.Revision == layout.Revision &&
            (current.Frame != frame || previous.OuterCornerRadius != layout.OuterCornerRadius ||
                !previous.Regions.SequenceEqual(layout.Regions))) return false;
        _surfaces[key.PosterId] = new(key, frame, layout with { Regions = Array.AsReadOnly(layout.Regions.ToArray()) }, layout.Revision);
        return true;
    }

    public bool IsCurrent(PosterInputSessionKey key, long revision) => !_suspended &&
        _surfaces.TryGetValue(key.PosterId, out var s) && s.Key == key && s.Layout?.Revision == revision;

    // Caller supplies actual visual HWND order; database stack order is not physical evidence.
    // The first containing poster owns the whole frame. Blank space blocks lower posters
    // with the content viewport as the last, explicit marquee target.
    public PosterInputHit? Resolve(int screenX, int screenY, IEnumerable<PosterInputSessionKey> frontToBack)
    {
        if (_suspended || Capture is not null) return null;
        foreach (var key in frontToBack)
        {
            if (!_surfaces.TryGetValue(key.PosterId, out var s) || s.Key != key || s.Layout is not { } layout) continue;
            int x = screenX - s.Frame.X, y = screenY - s.Frame.Y;
            if (!PosterDropSurfacePolicy.ReceivesOle(s.Frame, new(screenX, screenY))) continue;
            if (PosterInputGeometry.InsideRoundedFrame(x, y, layout))
                foreach (var r in layout.Regions)
                    if (PosterInputGeometry.CapturesPointer(r) && Contains(r.Bounds, x, y)) return new(key, layout.Revision, s.Frame, r);
            return null;
        }
        return null;
    }

    // A right-button release must retain the exact published region from its press. A new
    // region for the same ItemId (including a different loop tile) is not the same intent.
    // Native callers supply current physical ordering and independently reject occlusion.
    // This does not acquire, end or replace any left-button capture.
    public PosterContentContextRequest? ResolveContext(PosterInputHit press, PointerScreenPoint release,
        IEnumerable<PosterInputSessionKey> frontToBack, PointerScreenPoint? pressScreenPoint = null)
    {
        var order = frontToBack.ToArray();
        if (press.Region.Kind != PosterInputTargetKind.ContentItem ||
            !PosterContentInputPolicy.HasValidIdentity(press.Region) ||
            !IsCurrent(press.Session, press.LayoutRevision) ||
            Resolve(release.X, release.Y, order) != press ||
            (pressScreenPoint is { } start && Resolve(start.X, start.Y, order) != press)) return null;
        return new(press, release) { PressScreenPoint = pressScreenPoint };
    }

    public PosterInputCapture? Begin(PosterInputHit hit)
    {
        if (Capture is not null || !PosterInputGeometry.CapturesPointer(hit.Region) || !IsCurrent(hit.Session, hit.LayoutRevision) ||
            _surfaces[hit.Session.PosterId].Frame != hit.Frame ||
            !_surfaces[hit.Session.PosterId].Layout!.Regions.Contains(hit.Region)) return null;
        return Capture = new(Guid.NewGuid(), hit);
    }
    public bool Owns(PosterInputCapture? token) => token is not null && Capture == token && !_suspended &&
        _surfaces.TryGetValue(token.Press.Session.PosterId, out var s) && s.Key == token.Press.Session && s.Layout is not null;
    public bool CanInvoke(PosterInputCapture token, int x, int y) => Owns(token) &&
        IsCurrent(token.Press.Session, token.Press.LayoutRevision) &&
        Contains(token.Press.Region.Bounds, x - token.Press.Frame.X, y - token.Press.Frame.Y);
    public void End(PosterInputCapture? token) { if (token is not null && Capture == token) Capture = null; }
    public void Invalidate(PosterInputSessionKey key)
    {
        if (!_surfaces.TryGetValue(key.PosterId, out var s) || s.Key != key) return;
        if (Capture?.Press.Session == key) Capture = null;
        _surfaces[key.PosterId] = s with { Layout = null };
    }
    public void Remove(PosterInputSessionKey key)
    {
        Invalidate(key);
        if (_surfaces.TryGetValue(key.PosterId, out var s) && s.Key == key) _surfaces.Remove(key.PosterId);
    }
    public void Suspend() { _suspended = true; Capture = null; }
    public void Resume() => _suspended = false;
    private static bool Contains(DesktopPixelRect r, int x, int y) => x >= r.X && y >= r.Y && x < r.Right && y < r.Bottom;
}

public sealed record PosterInputSurfaceSnapshot(PosterInputSessionKey Session, DesktopPixelRect Frame,
    PosterInputLayoutSnapshot Layout);

// Shared by active routing and passive observation. No second set of title/edge constants.
public static class PosterInputGeometry
{
    // Regions are priority ordered: controls/items precede the blank-content viewport.
    public static bool CapturesPointer(PosterInputRegion region) => true;

    public static bool Contains(PosterInputSurfaceSnapshot surface, PointerScreenPoint point) =>
        InsideRoundedFrame(point.X - surface.Frame.X, point.Y - surface.Frame.Y, surface.Layout);

    public static bool IsExpansionZone(PosterInputSurfaceSnapshot surface, PointerScreenPoint point)
    {
        if (!Contains(surface, point)) return false;
        int x = point.X - surface.Frame.X, y = point.Y - surface.Frame.Y;
        // Same ordered regions as actual routing; buttons and resize edges precede TitleBar.
        foreach (var region in surface.Layout.Regions)
            if (CapturesPointer(region) && x >= region.Bounds.X && y >= region.Bounds.Y && x < region.Bounds.Right && y < region.Bounds.Bottom)
                return region.Kind == PosterInputTargetKind.TitleBar;
        return false;
    }

    public static bool InsideRoundedFrame(int x, int y, PosterInputLayoutSnapshot layout)
    {
        if (x < 0 || y < 0 || x >= layout.FrameWidth || y >= layout.FrameHeight) return false;
        double radius = Math.Min(layout.OuterCornerRadius, Math.Min(layout.FrameWidth, layout.FrameHeight) / 2d);
        double dx = Math.Max(Math.Max(radius - (x + .5), x + .5 - (layout.FrameWidth - radius)), 0);
        double dy = Math.Max(Math.Max(radius - (y + .5), y + .5 - (layout.FrameHeight - radius)), 0);
        return dx * dx + dy * dy <= radius * radius;
    }
}
