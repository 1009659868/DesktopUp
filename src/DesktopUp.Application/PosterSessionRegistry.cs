namespace DesktopUp.Application;

public sealed record PosterSessionEnvironment(
    DesktopShellHostSnapshot Shell, DisplayTopologySnapshot Displays)
{
    public DesktopRecoveryGenerationPair Generations => new(Shell.Generation, Displays.Generation);
}

// Dispatcher-affine port. Implementations must not persist native state or initialize a profile.
public interface IPosterSession : IDisposable
{
    string PosterId { get; }
    nint WindowHandle { get; }
    bool IsEditing { get; }
    DesktopRecoveryGenerationPair? CurrentGenerationPair { get; }
    bool Start(PosterSurfaceProjection projection, PosterSessionEnvironment environment, CancellationToken lifetime);
    bool ApplyProjection(StateProjectionChangedEventArgs change);
    bool Recover(PosterSessionEnvironment environment, long request);
    bool IsAttached(DesktopShellHostSnapshot shell);
    bool SetEditing(bool editing);
}

public interface IPosterSessionFactory
{
    IPosterSession Create(string posterId, long epoch);
}

public enum PosterSessionStatus { Starting, Ready, Degraded }

public sealed record PosterSessionSnapshot(string PosterId, long Epoch, PosterSessionStatus Status,
    string WindowHandle, DesktopRecoveryGenerationPair? Generations, string? Error);

/// <summary>
/// One UI-dispatcher registry, driven only by committed catalog projections. Discovery is shared
/// by the caller; delayed recovery resolves the CURRENT desired set, never a captured ID list.
/// Hidden/deleted entries cancel their lifetime before disposal; a new incarnation gets a new epoch.
/// </summary>
public sealed class PosterSessionRegistry(IPosterSessionFactory factory, IStructuredLog log) : IDisposable
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private StateProjectionChangedEventArgs? _desired;
    private PosterSessionEnvironment? _environment;
    private Func<bool>? _environmentIsCurrent;
    private long _environmentRequest;
    private long _nextEpoch;
    private bool _draining;
    private bool _pending;
    private bool _disposed;

    public IReadOnlyList<PosterSessionSnapshot> Sessions => _entries.Values
        .Select(e => new PosterSessionSnapshot(e.Id, e.Epoch, e.Status,
            $"0x{e.Session?.WindowHandle ?? 0:X}", e.Session?.CurrentGenerationPair, e.Error)).ToArray();

    public IPosterSession? Find(string? posterId) => posterId is not null &&
        _entries.TryGetValue(posterId, out Entry? entry) && entry.Status == PosterSessionStatus.Ready && !entry.Lifetime.IsCancellationRequested
            ? entry.Session : null;

    public void ApplyProjection(StateProjectionChangedEventArgs change)
    {
        if (_disposed || (_desired is not null && change.Projection.Revision < _desired.Projection.Revision)) return;
        _desired = change;
        // Cancel immediately even during reentrant native dispatch. Drain disposes after Start returns.
        foreach (Entry entry in _entries.Values.ToArray())
            if (!Wants(entry.Id)) entry.Lifetime.Cancel();
        Drain();
    }

    public void SuspendEnvironment(long request)
    {
        if (_disposed || request < _environmentRequest) return;
        _environmentRequest = request;
        _environment = null;
        _environmentIsCurrent = null;
    }

    public bool Recover(PosterSessionEnvironment environment, long request, Func<bool> isCurrent)
    {
        if (_disposed || request < _environmentRequest || !isCurrent()) return false;
        _environmentRequest = request;
        _environment = environment;
        _environmentIsCurrent = isCurrent;
        Drain();
        return !_disposed && isCurrent() && _environment == environment && _desired is not null &&
            _entries.Values.All(e => !e.Lifetime.IsCancellationRequested) &&
            _desired.Projection.Posters.Where(p => p.Poster.IsVisible).All(p =>
                _entries.TryGetValue(p.Poster.PosterId, out Entry? e) && e.Status == PosterSessionStatus.Ready &&
                e.Session?.CurrentGenerationPair == environment.Generations);
    }

    public bool NeedsRecovery(DesktopShellHostSnapshot shell)
    {
        if (_disposed || _desired is null) return false;
        if (_entries.Values.Any(e => e.Lifetime.IsCancellationRequested)) return true;
        foreach (var p in _desired.Projection.Posters.Where(p => p.Poster.IsVisible))
        {
            try
            {
                if (!_entries.TryGetValue(p.Poster.PosterId, out Entry? e) ||
                    e.Status != PosterSessionStatus.Ready || e.Session?.IsAttached(shell) != true) return true;
            }
            catch { return true; } // Local readback fault enters isolated recovery, not a global locator reset.
        }
        return false;
    }

    private bool Wants(string id) => !_disposed && _desired?.Projection.PostersById.TryGetValue(id, out var p) == true && p.Poster.IsVisible;

    private void Drain()
    {
        _pending = true;
        if (_draining) return;
        _draining = true;
        try
        {
            while (_pending)
            {
                _pending = false;
                foreach (Entry entry in _entries.Values.ToArray())
                    if (!Wants(entry.Id) || entry.Lifetime.IsCancellationRequested) Release(entry);
                if (_disposed || _desired is not { } change) continue;
                foreach (PosterSurfaceProjection target in change.Projection.Posters.Where(p => p.Poster.IsVisible))
                {
                    if (_disposed || !ReferenceEquals(change, _desired)) { _pending = true; break; }
                    string id = target.Poster.PosterId;
                    if (!_entries.TryGetValue(id, out Entry? entry))
                    {
                        entry = new Entry(id, ++_nextEpoch);
                        _entries.Add(id, entry);
                    }
                    try
                    {
                        // Cleanup failure quarantines an incarnation; it must never be reused.
                        if (entry.Lifetime.IsCancellationRequested) continue;
                        PosterSessionEnvironment? env = _environment;
                        bool validEnvironment = env is not null && _environmentIsCurrent?.Invoke() == true;
                        if (entry.Status == PosterSessionStatus.Degraded && entry.Session is not null)
                        {
                            entry.Session.Dispose();
                            entry.Session = null;
                        }
                        if (entry.Session is null)
                        {
                            if (!validEnvironment) continue;
                            entry.Status = PosterSessionStatus.Starting;
                            entry.Session = factory.Create(id, entry.Epoch);
                            if (entry.Session.PosterId != id) throw new InvalidOperationException("session-id-mismatch");
                            if (!entry.Session.Start(target, env!, entry.Lifetime.Token))
                                throw new InvalidOperationException("session-start-failed");
                            entry.Applied = change;
                        }
                        else if (!ReferenceEquals(entry.Applied, change))
                        {
                            if (!entry.Session.ApplyProjection(change)) throw new InvalidOperationException("projection-apply-failed");
                            entry.Applied = change;
                        }
                        if (!Wants(id) || entry.Lifetime.IsCancellationRequested) { _pending = true; continue; }
                        if (!validEnvironment || _environment != env || _environmentIsCurrent?.Invoke() != true) continue;
                        if (entry.Session.CurrentGenerationPair != env!.Generations || !entry.Session.IsAttached(env.Shell))
                            if (!entry.Session.Recover(env, _environmentRequest)) throw new InvalidOperationException("session-recovery-failed");
                        if (_environment != env || _environmentIsCurrent?.Invoke() != true) continue;
                        if (entry.Session.CurrentGenerationPair != env.Generations || !entry.Session.IsAttached(env.Shell))
                            throw new InvalidOperationException("session-recovery-readback-failed");
                        if (entry.Status != PosterSessionStatus.Ready)
                            log.Write("info", "poster_session.ready", new { posterId = id, entry.Epoch, request = _environmentRequest, generations = env.Generations });
                        entry.Status = PosterSessionStatus.Ready;
                        entry.Error = null;
                    }
                    catch (Exception exception)
                    {
                        entry.Status = PosterSessionStatus.Degraded;
                        entry.Error = exception.Message;
                        entry.Lifetime.Cancel();
                        log.Write("warning", "poster_session.degraded", new { posterId = id, entry.Epoch, exception.Message });
                        // A failed partial create must be cleaned before retry. Keep failed cleanup referenced.
                        try { entry.Session?.Dispose(); entry.Session = null; }
                        catch (Exception cleanup) { entry.Error += "; cleanup: " + cleanup.Message; }
                    }
                }
            }
        }
        finally { _draining = false; }
    }

    private void Release(Entry entry)
    {
        entry.Lifetime.Cancel();
        try
        {
            entry.Session?.Dispose();
            _entries.Remove(entry.Id);
            entry.Lifetime.Dispose();
            log.Write("info", "poster_session.released", new { posterId = entry.Id, entry.Epoch });
        }
        catch (Exception exception)
        {
            entry.Status = PosterSessionStatus.Degraded;
            entry.Error = "cleanup: " + exception.Message;
            log.Write("error", "poster_session.cleanup_failed", new { posterId = entry.Id, entry.Epoch, exception.Message });
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _environment = null;
        _environmentIsCurrent = null;
        foreach (Entry entry in _entries.Values.ToArray()) entry.Lifetime.Cancel();
        Drain();
    }

    private sealed class Entry(string id, long epoch)
    {
        public string Id { get; } = id;
        public long Epoch { get; } = epoch;
        public CancellationTokenSource Lifetime { get; } = new();
        public IPosterSession? Session { get; set; }
        public StateProjectionChangedEventArgs? Applied { get; set; }
        public PosterSessionStatus Status { get; set; } = PosterSessionStatus.Starting;
        public string? Error { get; set; }
    }
}
