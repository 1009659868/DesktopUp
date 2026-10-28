using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// One worker/timer per profile. Cached routes schedule work; only fresh use-case reads authorize it.
public sealed class ManagedContentMonitor : IContentMonitor
{
    private readonly IContentObservation _observation;
    private readonly Action<ManagedContentSyncResult> _completed;
    private readonly CancellationTokenSource _stop = new();
    private readonly DirectoryObservationQueue _queue = new();
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _worker;
    private int _targetsDirty = 1, _fullRequested = 1, _watcherFailed;
    private sealed class Registration(ContentWatchTarget target)
    {
        internal readonly ContentWatchTarget Target = target;
        internal FileSystemWatcher? Watcher;
        internal DateTimeOffset RetryAt;
        internal int Failures;
    }
    public ManagedContentMonitor(IContentObservation observation, Action<ManagedContentSyncResult> completed)
    { _observation = observation; _completed = completed; _worker = Task.Run(Run); }
    // System recovery/overflow requires full reconciliation; a content publication only refreshes routes.
    public void Invalidate() { RefreshTargets(); Interlocked.Exchange(ref _fullRequested, 1); }
    public void RefreshTargets() => Interlocked.Exchange(ref _targetsDirty, 1);

    private async Task Run()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        int targetFallback = 0, fullFallback = 0;
        bool maintenancePending = true;
        try
        {
            do
            {
                _stop.Token.ThrowIfCancellationRequested();
                var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    // Independent counters cannot be postponed by a continuously busy directory.
                    if (Interlocked.Exchange(ref _targetsDirty, 0) != 0 || ++targetFallback >= 15)
                    {
                        try { ReplaceTargets(_observation.WatchTargets()); targetFallback = 0; }
                        catch { RefreshTargets(); throw; } // Keep last good routes, skip this pass.
                    }
                    RefreshWatchers();
                    if (Interlocked.Exchange(ref _fullRequested, 0) != 0 || ++fullFallback >= 15)
                    { _queue.ScheduleAll(DateTimeOffset.UtcNow); fullFallback = 0; maintenancePending = true; }
                    attempted.UnionWith(_queue.Take(DateTimeOffset.UtcNow, limit: 4));
                    if (attempted.Count == 0 && !maintenancePending) continue;
                    var result = _observation.Reconcile(_stop.Token, attempted);
                    _queue.Complete(attempted, result, DateTimeOffset.UtcNow);
                    // No routes still needs the existing durable released-exit maintenance.
                    maintenancePending = attempted.Count == 0 && result.Pending;
                    _completed(result);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    var failure = new ManagedContentSyncResult(0, 0, true, ["外部同步暂停，本轮未清理关联：" + e.Message])
                    { Retries = attempted.Select(p => new ContentObservationRetry(p, true)).ToArray() };
                    _queue.Complete(attempted, failure, DateTimeOffset.UtcNow);
                    _completed(failure);
                }
            } while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { foreach (var registration in _registrations.Values) registration.Watcher?.Dispose(); }
    }

    private void ReplaceTargets(IReadOnlyList<ContentWatchTarget> targets)
    {
        var current = targets.ToDictionary(t => t.Path, StringComparer.OrdinalIgnoreCase);
        foreach (string removed in _registrations.Where(p => !current.TryGetValue(p.Key, out var target) || p.Value.Target != target)
                     .Select(p => p.Key).ToArray())
        { _registrations[removed].Watcher?.Dispose(); _registrations.Remove(removed); }
        foreach (var target in current.Values) _registrations.TryAdd(target.Path, new(target));
        _queue.ReplaceTargets(targets, DateTimeOffset.UtcNow);
    }
    private void RefreshWatchers()
    {
        if (Interlocked.Exchange(ref _watcherFailed, 0) != 0)
            foreach (var entry in _registrations.Values)
            { entry.Watcher?.Dispose(); entry.Watcher = null; entry.RetryAt = default; entry.Failures = 0; }
        foreach (var entry in _registrations.Values)
        {
            if (entry.Watcher is not null || entry.RetryAt > DateTimeOffset.UtcNow) continue;
            string path = entry.Target.Path;
            try
            {
                var watcher = new FileSystemWatcher(path) { IncludeSubdirectories = entry.Target.Recursive,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
                FileSystemEventHandler changed = (_, _) => _queue.Signal(path, DateTimeOffset.UtcNow);
                watcher.Created += changed; watcher.Deleted += changed; watcher.Changed += changed;
                watcher.Renamed += (_, _) => _queue.Signal(path, DateTimeOffset.UtcNow);
                watcher.Error += (_, _) => { Interlocked.Exchange(ref _watcherFailed, 1); Invalidate(); };
                try
                {
                    watcher.EnableRaisingEvents = true; entry.Watcher = watcher; entry.Failures = 0;
                    _queue.Signal(path, DateTimeOffset.UtcNow);
                }
                catch { watcher.Dispose(); throw; }
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
            {
                entry.Failures = Math.Min(entry.Failures + 1, 5);
                entry.RetryAt = DateTimeOffset.UtcNow + DirectoryObservationQueue.RetryDelay(entry.Failures);
            }
        }
    }
    public async ValueTask DisposeAsync()
    { _stop.Cancel(); await _worker.ConfigureAwait(false); _stop.Dispose(); }
}
