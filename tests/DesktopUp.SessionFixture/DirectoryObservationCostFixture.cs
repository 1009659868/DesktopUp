using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Infrastructure;

// Same diagnostic on both revisions; only new owned directories, no desktop or installation data.
internal static class DirectoryObservationCostFixture
{
    internal static async Task Run(string requested)
    {
        string root = Path.GetFullPath(requested), repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
            repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
        if (!root.StartsWith(Path.Combine(repo, "out", "drop-d3") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(root) || File.Exists(root)) throw new IOException("New isolated output directory required.");
        Directory.CreateDirectory(root);
        using var store = new SqliteStateStore(new UserDataPaths(Path.Combine(root, "profile")));
        var init = store.Initialize(); if (!init.Succeeded) throw new IOException(init.Error);
        var repository = new SqliteStateCommandRepository(store); repository.EnsureInitialized();
        ReadObservationCostFixture.SeedMappings(store, root, 12);
        var files = new CountingFiles();
        var observation = new ContentObservationService(new(new SqliteManagedContentObservationStorage(store), files),
            new(new SqliteMappedDirectoryStorage(store), files), new SqliteContentWatchTargetSource(store));
        int completions = 0;
        var states = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await using var monitor = new ManagedContentMonitor(observation, result =>
        {
            foreach (var state in result.MappingStates) states[state.Path] = state.Available;
            Interlocked.Increment(ref completions);
        });
        await Until(() => files.Counts.Count == 12);
        await Task.Delay(700); files.Counts.Clear();
        long start = Stopwatch.GetTimestamp();
        // A bursts while B changes once. Existing item bytes do not imply a fake file move.
        for (int i = 0; i < 20; i++)
        {
            File.WriteAllText(Path.Combine(root, "directory-0", "item-0.txt"), "burst-" + i);
            if (i == 5) File.WriteAllText(Path.Combine(root, "directory-1", "healthy-new.txt"), "owned B");
            await Task.Delay(200);
        }
        await Until(() => repository.Read().ContentCatalog.Items.Any(i => i.DisplayName == "healthy-new.txt"));
        double healthyMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        await Task.Delay(2500);
        var burst = files.Snapshot(); files.Counts.Clear();
        string missing = Path.Combine(root, "directory-2"), away = Path.Combine(root, "directory-2-away");
        Directory.Move(missing, away);
        File.WriteAllText(Path.Combine(root, "directory-3", "other-new.txt"), "owned D");
        // Root moves need not emit a child event on Windows. Include the periodic full-pass fallback.
        await Task.Delay(35000);
        bool otherVisible = repository.Read().ContentCatalog.Items.Any(i => i.DisplayName == "other-new.txt");
        bool unavailableReported = states.TryGetValue(missing, out bool available) && !available;
        var unavailable = files.Snapshot();
        Directory.Move(away, missing);
        await Until(() => states.TryGetValue(missing, out bool restored) && restored);
        // Disposal below cancels an idle/pending worker; test-only files remain inside this owned evidence root.
        File.WriteAllText(Path.Combine(root, "cost.json"), JsonSerializer.Serialize(new { MappedPosters = 12,
            ItemsPerMapping = 20, Burst = burst, HealthyVisibleMs = healthyMs, Unavailable = unavailable,
            OtherVisible = otherVisible, UnavailableReported = unavailableReported, Restored = true, Completions = completions,
            Notes = "Real single monitor, 2s ticks; per-root full-scan calls, not SQL/FPS. Startup excluded; no App callback; missing-root phase 35s includes periodic fallback." },
            new JsonSerializerOptions { WriteIndented = true }));
        if (!otherVisible || !unavailableReported) throw new IOException("Healthy root or periodic unavailable feedback did not converge.");
        Console.WriteLine("Directory observation cost: " + root);
    }
    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(40)) throw new TimeoutException("Observation did not converge."); await Task.Delay(100); }
    }
    private sealed class CountingFiles : IContentObservationFiles
    {
        private readonly LocalContentObservationFiles _inner = new();
        internal readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase);
        internal object Snapshot() => Counts.OrderBy(p => p.Key).ToDictionary(p => Path.GetFileName(p.Key), p => p.Value);
        public string Identity(string path) => _inner.Identity(path);
        public string MappedItemIdentity(string path) => _inner.MappedItemIdentity(path);
        public IEnumerable<string> Enumerate(string path) => _inner.Enumerate(path);
        public string Normalize(string path) => _inner.Normalize(path);
        public IDisposable LockDirectory(string path) => _inner.LockDirectory(path);
        public bool Contains(string parent, string child) => _inner.Contains(parent, child);
        public MappedDirectoryScan ScanMapping(string path, CancellationToken token = default)
        { Counts.AddOrUpdate(path, 1, (_, n) => n + 1); return _inner.ScanMapping(path, token); }
        public IReadOnlyDictionary<string, string> Inventory(string path, CancellationToken token = default)
        { Counts.AddOrUpdate(path, 1, (_, n) => n + 1); return _inner.Inventory(path, token); }
    }
}
