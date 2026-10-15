namespace DesktopUp.Infrastructure;

// One worker per profile. Watcher events only invalidate; they never remove individual paths.
// A bounded periodic full pass covers startup, missed events, watcher failure and root reappearance.
public sealed class ManagedContentMonitor : IAsyncDisposable
{
    private readonly SqliteTransferJournal _journal;
    private readonly ManagedContentSynchronizer _sync;
    private readonly MappedDirectoryService _mapped;
    private readonly Action<ManagedContentSyncResult> _completed;
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _worker;
    private int _dirty = 1;
    public ManagedContentMonitor(SqliteStateStore store, Action<ManagedContentSyncResult> completed)
    { _journal = new(store); _sync = new(store); _mapped = new(store); _completed = completed; _worker = Task.Run(Run); }
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
                    var result = _sync.Reconcile(_stop.Token);
                    var mapped = _mapped.Reconcile(_stop.Token);
                    result = result with { IndexChanged = result.IndexChanged || mapped.Changed, MappingStates=mapped.States,
                        Pending = result.Pending || mapped.Pending, Notices = result.Notices.Concat(mapped.Notices.Select(n => "目录映射：" + n)).ToArray() };
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
        var managed = _journal.StorageLocations().Select(l => l.Collection.StorageLocator!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var paths = managed.Concat(_mapped.ActivePaths()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removed in _watchers.Keys.Except(paths, StringComparer.OrdinalIgnoreCase).ToArray())
        { _watchers[removed].Dispose(); _watchers.Remove(removed); Invalidate(); }
        foreach (var path in paths.Where(p => !_watchers.ContainsKey(p)))
        {
            try
            {
                var watcher = new FileSystemWatcher(path) { IncludeSubdirectories = managed.Contains(path),
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
