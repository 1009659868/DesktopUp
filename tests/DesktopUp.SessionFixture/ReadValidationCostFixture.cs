using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

// P1 diagnostic only: original product assemblies, no production timing hooks or skipped validation.
internal static class ReadValidationCostFixture
{
    internal static void Run(string requested)
    {
        string root = NewOutput(requested);
        var scenarios = new List<object>();
        CheckSqlCounter();
        foreach (int count in new[] { 1, 6, 12 })
        {
            string area = Path.Combine(root, $"mapped-{count}");
            using var store = new SqliteStateStore(new UserDataPaths(Path.Combine(area, "profile")));
            var initialized = store.Initialize();
            if (!initialized.Succeeded) throw new IOException(initialized.Error);
            var repository = new SqliteStateCommandRepository(store);
            repository.EnsureInitialized();
            ReadObservationCostFixture.SeedMappings(store, area, count);
            var clock = new FixtureClock();
            var files = new LocalContentObservationFiles();
            var calls = new Dictionary<string, int>();
            var timings = new Dictionary<string, CostPortTiming>();
            var plain = Observation(false);
            var observed = Observation(true);
            var projection = new StateProjectionCoordinator(repository);
            projection.EnsureInitialized();
            var open = Method<SqliteStateStore>("OpenWritableConnection").CreateDelegate<Func<SqliteConnection>>(store);
            var verify = Method<SqliteStateStore>("VerifyCurrentSchema").CreateDelegate<Func<SqliteConnection, StateStoreHead>>();
            var read = Method<SqliteStateCommandRepository>("ReadSnapshot")
                .CreateDelegate<Func<SqliteConnection, SqliteTransaction, DesktopStateSnapshot>>();
            var project = Method<StateProjectionCoordinator>("BuildProjection")
                .CreateDelegate<Func<DesktopStateSnapshot, DesktopUpProjection>>();
            using var stageConnection = open();
            var state = repository.Read();
            var measurements = new List<object>();

            Measure("open-configure-close", () => { using var connection = open(); return null; });
            Measure("schema-only", () => verify(stageConnection));
            Measure("snapshot-with-transaction", () =>
            {
                using var transaction = stageConnection.BeginTransaction(deferred: true);
                var value = read(stageConnection, transaction);
                transaction.Commit();
                return value;
            });
            Measure("projection-build-only", () => project(state));
            Measure("enumerate-names-only", () => Enumerable.Range(0, count)
                .Sum(i => Directory.EnumerateFileSystemEntries(Path.Combine(area, $"directory-{i}")).Count()));
            Measure("scan-mapping-with-metadata", () => Enumerable.Range(0, count)
                .Select(i => files.ScanMapping(Path.Combine(area, $"directory-{i}"))).ToArray());
            Measure("full-state", () => repository.Read());
            Measure("unchanged-projection", () => projection.Refresh());
            Measure("watch-targets", () => plain.WatchTargets());
            Measure("unchanged-reconcile", () => Quiet(plain.Reconcile()), 6);

            // Separate inclusive port pass. Reflection/proxy accounting is not in the plain measurements.
            _ = observed.Reconcile();
            calls.Clear(); timings.Clear();
            for (int i = 0; i < 3; i++) Quiet(observed.Reconcile());
            var portCosts = new Dictionary<string, CostPortTiming>(timings);

            // Real isolated change: settle outside the measured interval, then commit through the use case.
            // Each sample adds one file; subsequent samples have slightly more items. Not a no-change baseline.
            var commits = new List<object>();
            var report = new Dictionary<string, object?>
            {
                ["MappedPosters"] = count, ["ItemsPerMapping"] = 20, ["Measurements"] = measurements,
                ["PortPassIterations"] = 3, ["InclusivePortCosts"] = portCosts,
                ["Commits"] = commits, ["CommitStatus"] = "NotStarted"
            };
            scenarios.Add(report);
            Save(); // Preserve completed read measurements even if the separate commit probe fails.
            try
            {
                report["CommitStatus"] = "Running";
                for (int i = 0; i < 3; i++)
                {
                    string name = $"p1-commit-{i}.txt";
                    report["CommitSample"] = name;
                    PrepareChange(name);
                    calls.Clear(); timings.Clear();
                    var sample = Time(() => RequireChange(observed.Reconcile()));
                    if (!repository.Read().ContentCatalog.Items.Any(item => item.DisplayName == name))
                        throw new IOException("Committed sample is missing from state.");
                    commits.Add(new { Sample = sample, Ports = new Dictionary<string, CostPortTiming>(timings) });
                    Save();
                }
                report["CommitSample"] = "p1-counted-commit.txt";
                PrepareChange("p1-counted-commit.txt");
                SqliteExecutionCounter.SqlCount commitSql;
                using (var counter = new SqliteExecutionCounter())
                {
                    RequireChange(plain.Reconcile());
                    commitSql = counter.Snapshot();
                }
                report["CommitSql"] = commitSql;
                report["CommitStatus"] = "Passed";
            }
            catch (Exception e)
            {
                report["CommitStatus"] = "Failed";
                report["CommitError"] = e.ToString();
                report["FailurePorts"] = new Dictionary<string, CostPortTiming>(timings);
                Save();
                throw; // Never retry or turn an interrupted probe into a successful measurement run.
            }
            Save();
            Console.WriteLine($"P1 attribution: {count} maps complete: {root}");

            ContentObservationService Observation(bool instrument)
            {
                IContentObservationFiles physical = instrument ? CostPortProxy.Wrap<IContentObservationFiles>(files, calls, timings) : files;
                IManagedContentObservationStorage owned = new SqliteManagedContentObservationStorage(store);
                IMappedDirectoryStorage mapped = new SqliteMappedDirectoryStorage(store);
                if (instrument)
                {
                    owned = CostPortProxy.Wrap(owned, calls, timings);
                    mapped = CostPortProxy.Wrap(mapped, calls, timings);
                }
                return new(new(owned, physical), new(mapped, physical, clock), new SqliteContentWatchTargetSource(store));
            }

            void PrepareChange(string name)
            {
                File.WriteAllText(Path.Combine(area, "directory-0", name), "P1 owned sample");
                // Both use-case instances keep their own settling witnesses; synchronize outside measurement.
                if (!observed.Reconcile().Pending || !plain.Reconcile().Pending)
                    throw new IOException("Expected first-witness settling before a commit.");
                clock.Advance(TimeSpan.FromSeconds(3));
            }

            void Measure(string name, Func<object?> action, int iterations = 12)
            {
                for (int i = 0; i < 3; i++) _ = action();
                var samples = Enumerable.Range(0, iterations).Select(_ => Time(action)).ToArray();
                SqliteExecutionCounter.SqlCount sql;
                using (var counter = new SqliteExecutionCounter())
                {
                    counter.Attach(stageConnection);
                    counter.Reset();
                    _ = action();
                    sql = counter.Snapshot();
                }
                var sorted = samples.Select(s => s.Milliseconds).Order().ToArray();
                measurements.Add(new { Name = name, Samples = samples, MedianMs = sorted[sorted.Length / 2],
                    P95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
                    MeanAllocatedBytes = samples.Average(s => s.AllocatedBytes), SqlOneUntimedInvocation = sql });
            }
        }
        void Save() => File.WriteAllText(Path.Combine(root, "attribution.json"), JsonSerializer.Serialize(new
        {
            Runtime = Environment.Version.ToString(),
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            CounterSelfTest = "PASS: two SELECT starts, detached existing/future connections",
            Notes = "Read baselines are warmed; commit samples include first-use costs. Synchronous wall time and current-thread allocations. SQL STMT trace counts run separately; plain starts and comment/subprogram events are distinguished, not assumed to be user commands or triggers. Stage measurements exclude their prerequisites and must NOT be added to end-to-end totals. Port times are inclusive, in a separate pass. Commit samples grow the isolated directory. No UI/CPU/RSS claim.",
            Scenarios = scenarios
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record Sample(double Milliseconds, long AllocatedBytes);
    private static Sample Time(Func<object?> action)
    {
        long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        _ = action();
        return new(Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocation);
    }

    private static MethodInfo Method<T>(string name) => typeof(T).GetMethod(name,
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
        ?? throw new MissingMethodException(typeof(T).FullName, name);

    private static object Quiet(ManagedContentSyncResult result)
    {
        if (result.Pending || result.Changed || result.Notices.Count != 0)
            throw new IOException("Expected quiet reconciliation: " + string.Join(";", result.Notices));
        return result;
    }

    private static object RequireChange(ManagedContentSyncResult result)
    {
        if (!result.Changed || result.Pending || result.Notices.Count != 0)
            throw new IOException("Expected exactly one settled change: " + string.Join(";", result.Notices));
        return result;
    }

    private static string NewOutput(string requested)
    {
        string root = Path.GetFullPath(requested), repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
            repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
        if (!root.StartsWith(Path.Combine(repo, "out", "drop-d3") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(root) || File.Exists(root))
            throw new IOException("New isolated output directory required; existing data is never overwritten.");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CheckSqlCounter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open(); // Initializes the same provider/native library used by the product.
        using var counter = new SqliteExecutionCounter();
        counter.Attach(connection);
        counter.Reset();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1; SELECT 2;";
        command.ExecuteNonQuery();
        if (counter.Snapshot().StatementStarts != 2) throw new IOException("SQL counter self-test failed.");
        counter.Dispose();
        command.ExecuteNonQuery();
        using var later = new SqliteConnection("Data Source=:memory:");
        later.Open();
        using var laterCommand = later.CreateCommand();
        laterCommand.CommandText = "SELECT 3;";
        laterCommand.ExecuteNonQuery();
        if (counter.Snapshot().StatementStarts != 2) throw new IOException("SQL counter leaked callbacks.");
    }

    private sealed class FixtureClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan elapsed) => now += elapsed;
    }
}
