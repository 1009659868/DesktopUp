using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivityConsumerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Presentation_notification_failure_does_not_reconsume_committed_batches_or_lose_later_work()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        int attempts = 0;
        var errors = new List<Exception>(); var seen = new List<Guid>();
        var store = new Store(() => Interlocked.Increment(ref attempts) switch
        {
            1 => new(1, 1, true, [first]),
            2 => new(2, 1, false, [second]),
            _ => new(2, 0, false, [])
        });
        var consumer = new ContentActivityConsumer(store, errors.Add);
        consumer.StatisticsChanged += ids =>
        {
            seen.AddRange(ids);
            if (ids.Contains(first)) throw new IOException("presentation unavailable");
        };
        consumer.RequestRefresh();
        await consumer.WhenIdleAsync().WaitAsync(Timeout);
        Assert.Equal(new[] { first, second }, seen);
        Assert.Equal(2, attempts);
        Assert.Single(errors);
        consumer.RequestRefresh();
        await consumer.WhenIdleAsync().WaitAsync(Timeout);
        Assert.Equal(2, seen.Count);
        await consumer.StopAsync();
    }

    [Fact]
    public async Task A_refresh_arriving_while_failure_is_reported_is_consumed_without_another_external_signal()
    {
        using var releaseFailure = new ManualResetEventSlim();
        var failureStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        var store = new Store(() =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new IOException("transient storage failure");
            return new(1, 1, false, []);
        });
        var consumer = new ContentActivityConsumer(store, _ =>
        {
            failureStarted.TrySetResult(true);
            if (!releaseFailure.Wait(Timeout)) throw new TimeoutException("Failure release was not signalled.");
        });
        try
        {
            consumer.RequestRefresh();
            await failureStarted.Task.WaitAsync(Timeout);
            // A second successful open arrives before the first attempt relinquishes its task.
            consumer.RequestRefresh();
            releaseFailure.Set();
            await consumer.WhenIdleAsync().WaitAsync(Timeout);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }
        finally
        {
            releaseFailure.Set();
            await consumer.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task Stop_waits_for_the_current_batch_and_leaves_further_batches_for_the_next_startup()
    {
        using var releaseBatch = new ManualResetEventSlim();
        var batchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        var errors = new List<Exception>();
        var store = new Store(() =>
        {
            Interlocked.Increment(ref attempts);
            batchStarted.TrySetResult(true);
            if (!releaseBatch.Wait(Timeout)) throw new TimeoutException("Batch release was not signalled.");
            return new(128, 128, true, []);
        });
        var consumer = new ContentActivityConsumer(store, errors.Add);
        try
        {
            consumer.RequestRefresh();
            await batchStarted.Task.WaitAsync(Timeout);
            consumer.RequestRefresh();
            var stopped = consumer.StopAsync();
            Assert.False(stopped.IsCompleted);
            consumer.RequestRefresh();
            releaseBatch.Set();
            await stopped.WaitAsync(Timeout);
            consumer.RequestRefresh();
            await consumer.WhenIdleAsync().WaitAsync(Timeout);
            Assert.Equal(1, Volatile.Read(ref attempts));
            Assert.Empty(errors);
        }
        finally
        {
            releaseBatch.Set();
            await consumer.StopAsync().WaitAsync(Timeout);
        }
    }

    private sealed class Store(Func<ContentActivityBatch> consume) : IContentActivityStore
    {
        public ContentActivityBatch ConsumeNext(int maximumFacts = 128) => consume();
        public ContentActivitySnapshot Read(IReadOnlyCollection<Guid> contentIds) => throw new NotSupportedException();
    }
}
