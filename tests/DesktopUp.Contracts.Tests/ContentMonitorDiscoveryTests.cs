using System.Threading.Channels;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

// Exercise the real serial worker/timer and native watchers, without UI, desktop files or fake safety reads.
public sealed class ContentMonitorDiscoveryTests
{
    [Fact]
    public async Task PublicationOnlyRefreshesRoutesAndDisposalCancelsAnActivePass()
    {
        using var f = new F();
        var observation = new Observation { Targets = [new(f.Source, false)] };
        var results = Channel.CreateUnbounded<ManagedContentSyncResult>();
        var monitor = new ManagedContentMonitor(observation, r => results.Writer.TryWrite(r));
        try
        {
            await Next(results);
            monitor.RefreshTargets(); await Task.Delay(2500);
            Assert.Equal(2, observation.Reads); Assert.Equal(1, observation.Reconciles);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            observation.DuringReconcile = token => { entered.SetResult(); token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); };
            System.IO.File.WriteAllText(Path.Combine(f.Source, "cancel-scan.txt"), "owned");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
        }
        finally { await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(System.IO.File.Exists(Path.Combine(f.Source, "cancel-scan.txt")));
    }
    [Fact]
    public async Task PendingDoesNotRediscoverEachTickOrStarvePeriodicDiscoveryAndInvalidationIsNotLost()
    {
        var observation = new Observation { Pending = true };
        var results = Channel.CreateUnbounded<ManagedContentSyncResult>();
        await using var monitor = new ManagedContentMonitor(observation, r => results.Writer.TryWrite(r));
        await Next(results);
        Assert.Equal(1, observation.Reads);
        // Fifteen real 2s ticks: Pending keeps reconciliation active, not target discovery.
        for (int i = 0; i < 15; i++) await Next(results);
        Assert.Equal(2, observation.Reads);
        bool once = true;
        observation.DuringRead = () => { if (once) { once = false; monitor.Invalidate(); } };
        for (int i = 0; i < 10; i++) monitor.Invalidate(); // Coalesced, not queued work per call.
        await Next(results);
        Assert.Equal(3, observation.Reads);
        await Next(results); // Invalidation raised DURING the prior read must survive.
        Assert.Equal(4, observation.Reads);
    }

    [Fact]
    public async Task DiscoveryFailureSkipsReconcileAndRetriesWithoutPublishingAnEmptyTargetList()
    {
        using var f = new F();
        var observation = new Observation { Targets = [new(f.Source, false)] };
        var results = Channel.CreateUnbounded<ManagedContentSyncResult>();
        await using var monitor = new ManagedContentMonitor(observation, r => results.Writer.TryWrite(r));
        await Next(results);
        observation.DuringRead = () => throw new IOException("owned discovery failure");
        monitor.Invalidate();
        var failed = await Next(results);
        Assert.Contains("owned discovery failure", Assert.Single(failed.Notices));
        Assert.Equal(1, observation.Reconciles);
        observation.DuringRead = null;
        Assert.Empty((await Next(results)).Notices);
        Assert.Equal(3, observation.Reads);
        System.IO.File.WriteAllText(Path.Combine(f.Source, "retry-event.txt"), "owned");
        await Next(results);
        Assert.Equal(3, observation.Reads); // File events do not invalidate validated routing.
    }

    [Fact]
    public async Task MissingRootReattachesWithoutDatabasePollingAndRecursiveModeCanChange()
    {
        using var f = new F();
        string missing = Path.Combine(f.Source, "missing"), child = Path.Combine(missing, "nested");
        var observation = new Observation { Targets = [new(missing, false)] };
        var results = Channel.CreateUnbounded<ManagedContentSyncResult>();
        await using var monitor = new ManagedContentMonitor(observation, r => results.Writer.TryWrite(r));
        await Next(results);
        Directory.CreateDirectory(child);
        await Next(results); // Attaching the newly available root marks content dirty.
        Assert.Equal(1, observation.Reads);
        observation.Targets = [new(missing, true)];
        monitor.Invalidate();
        await Next(results);
        Assert.Equal(2, observation.Reads);
        System.IO.File.WriteAllText(Path.Combine(child, "recursive-event.txt"), "owned");
        await Next(results); // Must see the nested event before the 30s fallback.
        Assert.Equal(2, observation.Reads);
    }

    [Fact]
    public async Task RebindingDropsOldWatcherAndDisposalCancelsTheNextWait()
    {
        using var f = new F();
        string next = Path.Combine(f.DirectoryRoot, "next"); Directory.CreateDirectory(next);
        var observation = new Observation { Targets = [new(f.Source, false)] };
        var results = Channel.CreateUnbounded<ManagedContentSyncResult>();
        var monitor = new ManagedContentMonitor(observation, r => results.Writer.TryWrite(r));
        try
        {
            await Next(results);
            observation.Targets = [new(next, false)];
            monitor.Invalidate();
            await Next(results);
            int reconciles = observation.Reconciles;
            System.IO.File.WriteAllText(Path.Combine(f.Source, "old-route.txt"), "owned");
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(reconciles, observation.Reconciles);
            System.IO.File.WriteAllText(Path.Combine(next, "new-route.txt"), "owned");
            await Next(results);
            Assert.Equal(2, observation.Reads);
        }
        finally { await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private static async Task<ManagedContentSyncResult> Next(Channel<ManagedContentSyncResult> results) =>
        await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    private sealed class Observation : IContentObservation
    {
        private int _reads, _reconciles;
        internal int Reads => Volatile.Read(ref _reads);
        internal int Reconciles => Volatile.Read(ref _reconciles);
        internal IReadOnlyList<ContentWatchTarget> Targets = [];
        internal Action? DuringRead;
        internal Action<CancellationToken>? DuringReconcile;
        internal bool Pending;
        public IReadOnlyList<ContentWatchTarget> WatchTargets()
        {
            Interlocked.Increment(ref _reads);
            DuringRead?.Invoke();
            return Targets;
        }
        public ManagedContentSyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null)
        {
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _reconciles);
            DuringReconcile?.Invoke(token);
            return new(0, 0, Pending, []);
        }
    }
}
