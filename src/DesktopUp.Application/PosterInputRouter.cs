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
    // Only an input region is eligible. A transparent hole can fall through to a lower cluster/desktop.
    public PosterInputHit? Resolve(int screenX, int screenY, IEnumerable<PosterInputSessionKey> frontToBack)
    {
        if (_suspended || Capture is not null) return null;
        foreach (var key in frontToBack)
        {
            if (!_surfaces.TryGetValue(key.PosterId, out var s) || s.Key != key || s.Layout is not { } layout) continue;
            int x = screenX - s.Frame.X, y = screenY - s.Frame.Y;
            if (!InsideRoundedFrame(x, y, layout)) continue;
            foreach (var r in layout.Regions)
                if (Contains(r.Bounds, x, y)) return new(key, layout.Revision, s.Frame, r);
        }
        return null;
    }

    public PosterInputCapture? Begin(PosterInputHit hit)
    {
        if (Capture is not null || !IsCurrent(hit.Session, hit.LayoutRevision) ||
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
    private static bool InsideRoundedFrame(int x, int y, PosterInputLayoutSnapshot layout)
    {
        if (x < 0 || y < 0 || x >= layout.FrameWidth || y >= layout.FrameHeight) return false;
        double radius = Math.Min(layout.OuterCornerRadius, Math.Min(layout.FrameWidth, layout.FrameHeight) / 2d);
        double dx = Math.Max(Math.Max(radius - (x + .5), x + .5 - (layout.FrameWidth - radius)), 0);
        double dy = Math.Max(Math.Max(radius - (y + .5), y + .5 - (layout.FrameHeight - radius)), 0);
        return dx * dx + dy * dy <= radius * radius;
    }
}
