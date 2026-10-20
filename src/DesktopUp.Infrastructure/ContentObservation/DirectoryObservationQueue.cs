using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Only known routes can occupy a slot. Events coalesce; work already waiting keeps its FIFO position.
// The gate protects watcher callbacks, not IO. All IO stays on the monitor's single worker.
internal sealed class DirectoryObservationQueue
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private long _sequence;
    private sealed class Entry(ContentWatchTarget target)
    {
        internal ContentWatchTarget Target = target;
        internal long Order;
        internal DateTimeOffset Due;
        internal int Failures;
    }
    internal void ReplaceTargets(IEnumerable<ContentWatchTarget> targets, DateTimeOffset now)
    {
        lock (_gate)
        {
            var current = targets.ToDictionary(t => t.Path, StringComparer.OrdinalIgnoreCase);
            foreach (string removed in _entries.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase).ToArray()) _entries.Remove(removed);
            foreach (var target in current.Values)
                if (!_entries.TryGetValue(target.Path, out var old) || old.Target != target)
                { var entry = new Entry(target); _entries[target.Path] = entry; Enqueue(entry, now); }
        }
    }
    internal void Signal(string path, DateTimeOffset now)
    {
        lock (_gate)
            if (_entries.TryGetValue(path, out var entry))
            { entry.Failures = 0; Enqueue(entry, now); entry.Due = now; }
    }
    internal void ScheduleAll(DateTimeOffset now)
    {
        lock (_gate) foreach (var entry in _entries.Values) { Enqueue(entry, now); entry.Due = now; }
    }
    internal string[] Take(DateTimeOffset now, int limit)
    {
        lock (_gate)
        {
            var selected = _entries.Where(p => p.Value.Order != 0 && p.Value.Due <= now)
                .OrderBy(p => p.Value.Order).Take(limit).ToArray();
            foreach (var pair in selected) pair.Value.Order = 0;
            return selected.Select(p => p.Key).ToArray();
        }
    }
    internal void Complete(IReadOnlySet<string> attempted, ManagedContentSyncResult result, DateTimeOffset now)
    {
        lock (_gate)
        {
            var retries = result.Retries.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Any(r => r.Unavailable), StringComparer.OrdinalIgnoreCase);
            foreach (string path in attempted)
            {
                if (!_entries.TryGetValue(path, out var entry)) continue;
                // A real event during the scan wins over stale completion/backoff hints.
                if (entry.Order != 0) continue;
                bool retry = retries.TryGetValue(path, out bool unavailable) ||
                    (result.Pending && (result.RetryAll || result.Retries.Count == 0));
                if (!retry) { entry.Failures = 0; continue; }
                entry.Failures = unavailable ? Math.Min(entry.Failures + 1, 5) : 0;
                Enqueue(entry, now + RetryDelay(entry.Failures));
            }
        }
    }
    internal static TimeSpan RetryDelay(int failures) => TimeSpan.FromSeconds(failures <= 1 ? 2 : Math.Min(30, 1 << failures));
    private void Enqueue(Entry entry, DateTimeOffset due)
    {
        if (entry.Order != 0) return;
        entry.Order = ++_sequence; entry.Due = due;
    }
}
