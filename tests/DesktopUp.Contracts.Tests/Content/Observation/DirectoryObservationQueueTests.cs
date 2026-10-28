using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class DirectoryObservationQueueTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
    private static HashSet<string> Paths(params string[] values) => new(values, StringComparer.OrdinalIgnoreCase);
    [Fact]
    public void BurstsAreBoundedAndCannotOvertakeWaitingDirectoriesOrEraseInFlightEvents()
    {
        var queue = new DirectoryObservationQueue();
        queue.ReplaceTargets(Enumerable.Range(0, 12).Select(i => new ContentWatchTarget("root-" + i, false)), Now);
        Assert.Equal(["root-0", "root-1", "root-2", "root-3"], queue.Take(Now, 4));
        for (int i = 0; i < 10000; i++) { queue.Signal("root-0", Now); queue.Signal("unregistered-" + i, Now); }
        queue.Complete(Paths("root-0", "root-1", "root-2", "root-3"), new(0, 0, false, []), Now);
        Assert.Equal(["root-4", "root-5", "root-6", "root-7"], queue.Take(Now, 4));
        Assert.Equal(["root-8", "root-9", "root-10", "root-11"], queue.Take(Now, 4));
        Assert.Equal(["root-0"], queue.Take(Now, 4));
        Assert.Empty(queue.Take(Now, 4));
    }
    [Fact]
    public void UnavailableBackoffDoesNotBlockHealthyRootsAndRealEventsOverrideIt()
    {
        var queue = new DirectoryObservationQueue();
        queue.ReplaceTargets([new("bad", false), new("good", false)], Now);
        var all = Paths(queue.Take(Now, 4));
        var failed = new ManagedContentSyncResult(0, 0, true, []) { Retries = [new("bad", true)] };
        queue.Complete(all, failed, Now);
        queue.Signal("good", Now);
        Assert.Equal(["good"], queue.Take(Now, 4));
        var time = Now;
        foreach (int delay in new[] { 2, 4, 8, 16, 30, 30 })
        {
            Assert.Empty(queue.Take(time.AddSeconds(delay - 1), 4));
            time = time.AddSeconds(delay);
            Assert.Equal(["bad"], queue.Take(time, 4));
            queue.Complete(Paths("bad"), failed, time);
        }
        queue.Signal("bad", time);
        Assert.Equal(["bad"], queue.Take(time, 4));
        // Pending result from that scan must not delay an event that arrived during it.
        queue.Signal("bad", time); queue.Complete(Paths("bad"), failed, time);
        Assert.Equal(["bad"], queue.Take(time, 4));
    }
    [Fact]
    public void DiscoveryDoesNotDirtyUnchangedRoutesAndFallbackPrunesRemovedWork()
    {
        var queue = new DirectoryObservationQueue();
        queue.ReplaceTargets([new("same", false), new("old", false)], Now);
        queue.Take(Now, 4);
        queue.ReplaceTargets([new("same", false), new("new", false)], Now);
        Assert.Equal(["new"], queue.Take(Now, 4));
        queue.Signal("old", Now); Assert.Empty(queue.Take(Now, 4));
        queue.ScheduleAll(Now);
        Assert.Equal(Paths("same", "new"), Paths(queue.Take(Now, 4)));
        queue.ReplaceTargets([new("same", true)], Now);
        Assert.Equal(["same"], queue.Take(Now, 4));
    }
}
