using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// One worker per profile. Watcher events only invalidate; they never remove individual paths.
// A bounded periodic full pass covers startup, missed events, watcher failure and root reappearance.
public sealed class ManagedContentMonitor : IContentMonitor
{
    private readonly IContentObservation _observation;
    private readonly Action<ManagedContentSyncResult> _completed;
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _worker;
    private int _dirty = 1;
    public ManagedContentMonitor(IContentObservation observation, Action<ManagedContentSyncResult> completed)
    { _observation = observation; _completed = completed; _worker = Task.Run(Run); }
    public void Invalidate() => Interlocked.Exchange(ref _dirty, 1);
    private async Task Run()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        int fallback = 15;
        try
        {
            do
            {
                _stop.Token.ThrowIfCancellationRequested();
                try
                {
                    RefreshWatchers();
                    if (Interlocked.Exchange(ref _dirty, 0) == 0 && ++fallback < 15) continue;
                    fallback = 0;
                    var result = _observation.Reconcile(_stop.Token);
                    if (result.Pending) Invalidate();
                    _completed(result);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { _completed(new(0,0,false,["外部同步暂停，本轮未清理关联：" + e.Message])); }
            } while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { foreach (var watcher in _watchers.Values) watcher.Dispose(); _watchers.Clear(); }
    }
    private void RefreshWatchers()
    {
        var targets = _observation.WatchTargets();
        var paths = targets.Select(t => t.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removed in _watchers.Keys.Except(paths, StringComparer.OrdinalIgnoreCase).ToArray())
        { _watchers[removed].Dispose(); _watchers.Remove(removed); Invalidate(); }
        foreach (var path in paths.Where(p => !_watchers.ContainsKey(p)))
        {
            try
            {
                var watcher = new FileSystemWatcher(path) { IncludeSubdirectories = targets.Single(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase)).Recursive,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
                watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
                watcher.Changed += Changed;
                watcher.Error += (_,_) => Invalidate();
                try { watcher.EnableRaisingEvents = true; _watchers.Add(path, watcher); Invalidate(); }
                catch { watcher.Dispose(); throw; }
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { /* Retry on the next tick. */ }
        }
    }
    private void Changed(object sender, FileSystemEventArgs args) => Invalidate();
    public async ValueTask DisposeAsync()
    { _stop.Cancel(); await _worker.ConfigureAwait(false); _stop.Dispose(); }
}
