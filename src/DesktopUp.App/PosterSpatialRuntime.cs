using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

// Physical readback/gesture lease only. Committed state remains in StateProjectionCoordinator.
internal sealed partial class PosterSpatialRuntime(IStateProjectionCoordinator state, IStateCommandService commands, IStructuredLog log)
{
    private readonly Dictionary<string, PosterVisualWindow> _windows = new(StringComparer.Ordinal);
    private readonly Guid _runtimeId = Guid.NewGuid();
    private PosterVisualWindow? _owner;
    private SpatialGestureSession? _gesture;
    private bool _suspended = true;
    public bool TryGetProjection(out DesktopUpProjection? projection) => state.TryGetCurrent(out projection);
    public void Register(PosterVisualWindow window) => _windows[window.PosterId!] = window;
    public void Remove(PosterVisualWindow window)
    {
        if (_posture?.Participants.Contains(window) == true) CancelPosture("surface-disposed");
        if (ReferenceEquals(_owner, window)) End(window);
        if (window.PosterId is { } id && _windows.GetValueOrDefault(id) == window) _windows.Remove(id);
    }
    public void Suspend()
    {
        _suspended = true;
        CancelPosture("environment-invalidated");
        _owner?.CancelSpatialGesture("environment-invalidated");
        foreach (var window in _windows.Values.ToArray()) window.SuspendInput();
    }
    public void Resume() => _suspended = false;
    public void OnProjection(StateProjectionChangedEventArgs change)
    {
        _effective.RemoveMissing(change.Projection);
        if (_posture is { } posture && posture.Command.RequestId != change.RequestId &&
            !posture.ReadsCurrent(change.Projection)) CancelPosture("catalog-or-entity-changed");
        if (_gesture is { } gesture && change.RequestId != gesture.OperationId &&
            !gesture.IsCurrent(change.Projection, gesture.Initial.Environment))
            _owner?.CancelSpatialGesture("catalog-or-entity-changed");
    }

    public SpatialGestureSession? Begin(PosterVisualWindow owner, DesktopPixelRect pressFrame,
        PosterGestureKind kind, PosterOperatingMode mode, int minWidth, int minHeight)
    {
        if (_posture is not null || _owner is not null && !ReferenceEquals(_owner, owner)) return null;
        if (!TrySnapshot(owner, out var snapshot, out var projection) ||
            owner.PosterId is not { } id || snapshot!.Posters.Single(p => p.PosterId == id).Bounds != Rect(pressFrame)) return null;
        var issues = PosterSpatialSolver.ValidateLayout(snapshot);
        if (issues.Count > 0)
        {
            log.Write("warning", "spatial.gesture_unavailable", new { id, issues, reason = "layout-needs-recovery" });
            return null;
        }
        _owner = owner;
        _gesture = new(id, snapshot, projection!, kind, mode, minWidth, minHeight);
        log.Write("info", "spatial.gesture_started", new { id, _gesture.OperationId, readCount = _gesture.Reads.Posters.Count });
        return _gesture;
    }

    public bool IsCurrent(PosterVisualWindow owner, SpatialGestureSession gesture) =>
        ReferenceEquals(owner, _owner) && ReferenceEquals(gesture, _gesture) &&
        TrySnapshot(owner, out var snapshot, out var projection) && gesture.IsCurrent(projection!, snapshot!.Environment);

    public void End(PosterVisualWindow owner)
    {
        if (!ReferenceEquals(owner, _owner)) return;
        _gesture = null;
        _owner = null;
    }

    private bool TrySnapshot(PosterVisualWindow source, out SpatialSnapshot? snapshot, out DesktopUpProjection? projection)
    {
        snapshot = null;
        projection = null;
        if (_suspended || !state.TryGetCurrent(out projection) || projection is null ||
            !source.TryReadSpatial(_runtimeId, out var environment, out _, out _)) return false;
        var entries = new List<SpatialPoster>();
        foreach (var target in projection.Posters.Where(p => p.Poster.IsVisible))
        {
            if (!_windows.TryGetValue(target.Poster.PosterId, out var window) ||
                !window.TryReadSpatial(_runtimeId, out var peerEnvironment, out var peer, out long entityVersion) ||
                entityVersion != target.EntityVersion || peer!.IsLocked != target.Poster.IsLocked ||
                peer.IsCollapsed != target.Poster.IsCollapsed) return false;
            if (peerEnvironment!.WorkspaceId == environment!.WorkspaceId)
            {
                if (peerEnvironment != environment) return false;
                entries.Add(peer);
            }
        }
        snapshot = new(environment!, entries);
        return entries.Any(p => p.PosterId == source.PosterId);
    }

    public static SpatialRect Rect(DesktopPixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    public static DesktopPixelRect Pixels(SpatialRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
