using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

// Diagnostic executable only. Real SQLite/files, no desktop files, no timing thresholds in contracts.
internal static class ReadObservationCostFixture
{
    internal static async Task Run(string requested, bool discovery = false)
    {
        string root = Path.GetFullPath(requested), repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
            repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
        if (!root.StartsWith(Path.Combine(repo, "out", "drop-d3") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(root) || File.Exists(root)) throw new IOException("New isolated output directory required.");
        Directory.CreateDirectory(root);
        var scenarios = new List<object>();
        foreach (int count in new[] { 1, 6, 12 })
        {
            string area = Path.Combine(root, $"mapped-{count}");
            using var store = new SqliteStateStore(new UserDataPaths(Path.Combine(area, "profile")));
            var init = store.Initialize();
            if (!init.Succeeded) throw new IOException(init.Error);
            var repository = new SqliteStateCommandRepository(store);
            repository.EnsureInitialized();
            var files = new LocalContentObservationFiles();
            SeedMappings(store, area, count);
            var calls = new Dictionary<string, int>();
            var measuredFiles = CostPortProxy.Wrap<IContentObservationFiles>(files, calls);
            var managedStorage = CostPortProxy.Wrap<IManagedContentObservationStorage>(new SqliteManagedContentObservationStorage(store), calls);
            var managed = new ManagedContentSyncUseCase(managedStorage, measuredFiles);
            var mapping = new MappedDirectoryUseCase(CostPortProxy.Wrap<IMappedDirectoryStorage>(new SqliteMappedDirectoryStorage(store), calls), measuredFiles);
            var observation = new ContentObservationService(managed, mapping,
                CostPortProxy.Wrap<IContentWatchTargetSource>(new SqliteContentWatchTargetSource(store), calls));
            var projection = new StateProjectionCoordinator(repository); projection.EnsureInitialized();
            var samples = new List<object>();
            Measure("validated-head", () => store.ReadHead());
            Measure("full-state", () => repository.Read());
            Measure("unchanged-projection", () => projection.Refresh());
            // Benchmark-only copy of the previous composition, sharing this process/data/toolchain.
            // No legacy path is shipped or selectable in the product.
            Measure("previous-watch-targets", () =>
            {
                var owned = managedStorage.Locations().Select(l => l.Collection.StorageLocator!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return owned.Concat(mapping.ActivePaths()).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new ContentWatchTarget(path, owned.Contains(path))).ToArray();
            });
            Measure("watch-targets", () => observation.WatchTargets());
            Measure("unchanged-reconcile", () => CheckQuiet(observation.Reconcile()), 6);
            object? monitor = null;
            if (count == 6)
            {
                var timed = new TimedObservation(observation);
                await using (var running = new ManagedContentMonitor(timed, _ => { }))
                {
                    int idleSeconds = discovery ? 63 : 33;
                    await Task.Delay(TimeSpan.FromSeconds(idleSeconds)); // Includes startup and periodic fallback.
                    var idle = timed.Snapshot();
                    File.WriteAllText(Path.Combine(area, "directory-0", "watcher-new.txt"), "R7 event sample");
                    await Task.Delay(TimeSpan.FromSeconds(7));
                    monitor = new { IdleSeconds = idleSeconds, Idle = idle, WithChange = timed.Snapshot() };
                }
                if (!repository.Read().ContentCatalog.Items.Any(i => i.DisplayName == "watcher-new.txt"))
                    throw new IOException("Watcher change not observed within fixture window.");
            }
            scenarios.Add(new { MappedPosters = count, ItemsPerMapping = 20, Samples = samples, Monitor = monitor });
            Save(); Console.WriteLine($"R7 cost: {count} maps complete: {root}");
            void Measure(string name, Func<object> operation, int iterations = 12)
            {
                for (int i = 0; i < 2; i++) _ = operation();
                calls.Clear(); var times = new List<double>(); var bytes = new List<long>();
                for (int i = 0; i < iterations; i++)
                {
                    long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                    _ = operation(); times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    bytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocation);
                }
                double[] sorted = times.Order().ToArray();
                samples.Add(new { Name = name, Iterations = iterations, Milliseconds = times,
                    MedianMs = sorted[sorted.Length / 2], P95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
                    MeanAllocatedBytes = bytes.Average(), PortCallsTotal = new Dictionary<string, int>(calls) });
            }
        }
        void Save() => File.WriteAllText(Path.Combine(root, "cost.json"), JsonSerializer.Serialize(new {
            Runtime = Environment.Version.ToString(), Processors = Environment.ProcessorCount,
            Notes = "Warm synchronous wall time and current-thread allocations; port calls are NOT SQL statements. No UI/FPS claim.",
            Scenarios = scenarios }, new JsonSerializerOptions { WriteIndented = true }));
    }
    // Shared owned scenario; P1 and R7 use the same real mapping setup, not copied business logic.
    internal static void SeedMappings(SqliteStateStore store, string area, int count, int itemsPerMapping = 20)
    {
        var repository = new SqliteStateCommandRepository(store);
        var mapping = new MappedDirectoryUseCase(new SqliteMappedDirectoryStorage(store), new LocalContentObservationFiles());
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(area, $"directory-{i}"); Directory.CreateDirectory(path);
            for (int j = 0; j < itemsPerMapping; j++) File.WriteAllText(Path.Combine(path, $"item-{j}.txt"), "R7 isolated sample");
            var state = repository.Read();
            var created = mapping.Create(new(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, state.Revision,
                StateReadSet.ForCatalog(state.Catalog.CatalogVersion) with { ContentVersion = state.ContentVersion },
                PosterConfigurationFactory.NewBasicBox($"R7 map {i}", "DISPLAY1")), path);
            if (created.Status != StateCommandStatus.Committed) throw new IOException(created.Error);
        }
    }

    private static object CheckQuiet(ManagedContentSyncResult result)
    {
        if (result.Pending || result.Changed || result.Notices.Count != 0) throw new IOException("Expected quiet reconcile: " + string.Join(";", result.Notices));
        return result;
    }
    private sealed class TimedObservation(IContentObservation inner) : IContentObservation
    {
        private readonly object _gate = new();
        private int _targets, _reconciles, _changed;
        private double _targetMs, _reconcileMs;
        private long _targetBytes, _reconcileBytes;
        public IReadOnlyList<ContentWatchTarget> WatchTargets()
        {
            long start = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
            var value = inner.WatchTargets();
            lock (_gate) { _targets++; _targetMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _targetBytes += GC.GetAllocatedBytesForCurrentThread() - allocated; }
            return value;
        }
        public ManagedContentSyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null)
        {
            long start = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
            var value = inner.Reconcile(token, directories);
            lock (_gate) { _reconciles++; _reconcileMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _reconcileBytes += GC.GetAllocatedBytesForCurrentThread() - allocated; if (value.Changed) _changed++; }
            return value;
        }
        public object Snapshot() { lock (_gate) return new { Targets = _targets, Reconciles = _reconciles, Changed = _changed,
            TargetMs = _targetMs, ReconcileMs = _reconcileMs, TargetAllocatedBytes = _targetBytes, ReconcileAllocatedBytes = _reconcileBytes }; }
    }
}

// Test-only port accounting keeps infrastructure measurements separate from SQL counts.
public class CostPortProxy : DispatchProxy
{
    private object _target = null!;
    private Dictionary<string, int> _calls = null!;
    private Dictionary<string, CostPortTiming>? _timings;
    public static T Wrap<T>(T target, Dictionary<string, int> calls,
        Dictionary<string, CostPortTiming>? timings = null) where T : class
    {
        T proxy = Create<T, CostPortProxy>(); var counter = (CostPortProxy)(object)proxy;
        counter._target = target; counter._calls = calls; counter._timings = timings; return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        string key = method!.DeclaringType!.Name + "." + method.Name;
        _calls[key] = _calls.GetValueOrDefault(key) + 1;
        long start = _timings is null ? 0 : Stopwatch.GetTimestamp();
        long allocation = _timings is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        string? failure = null;
        try { return method.Invoke(_target, args); }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            if (_timings is not null) failure = e.InnerException.ToString();
            ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw;
        }
        finally
        {
            if (_timings is not null)
            {
                var previous = _timings.GetValueOrDefault(key) ?? new(0, 0, 0);
                _timings[key] = new(previous.Calls + 1,
                    previous.Milliseconds + Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                    previous.AllocatedBytes + GC.GetAllocatedBytesForCurrentThread() - allocation)
                    { FirstException = previous.FirstException ?? failure };
            }
        }
    }
}

public sealed record CostPortTiming(int Calls, double Milliseconds, long AllocatedBytes)
{
    public string? FirstException { get; init; }
}
