using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentOperationLifetimeTests
{
    [Fact]
    public async Task FileLeaseOwnsBothPostersAndOldReleaseCannotClearNextOperation()
    {
        using var lifetime = new ContentOperationLifetime(_ => { });
        var first = lifetime.BeginFileOperation("B", "A");
        Assert.True(lifetime.IsActiveFor("A"));
        Assert.True(lifetime.IsActiveFor("B"));
        Assert.False(lifetime.IsActiveFor("C"));
        Assert.Throws<InvalidOperationException>(() => lifetime.BeginFileOperation("C"));
        Assert.False(first.Completion.IsCompleted);
        first.Dispose();
        await first.Completion;
        using (var second = lifetime.BeginFileOperation("C"))
        {
            first.Dispose();
            Assert.True(lifetime.IsActiveFor("C"));
            Assert.False(lifetime.IsActiveFor("A"));
        }
        await lifetime.StopFileOperationsAsync();
    }

    [Fact]
    public async Task ExceptionReleasesScopeAndStoppingWaitsForItsCleanupWithoutReplaying()
    {
        using var lifetime = new ContentOperationLifetime(_ => { });
        int calls = 0;
        Assert.Throws<IOException>((Action)(() =>
        {
            using var operation = lifetime.BeginFileOperation("A");
            calls++;
            throw new IOException("publication failed after commit");
        }));
        Assert.False(lifetime.FileOperationActive);
        var next = lifetime.BeginFileOperation("B");
        var stopping = lifetime.StopFileOperationsAsync();
        Assert.True(lifetime.Stopping.IsCancellationRequested);
        Assert.False(stopping.IsCompleted);
        Assert.Throws<OperationCanceledException>(() => lifetime.BeginFileOperation("C"));
        Assert.Same(stopping, lifetime.StopFileOperationsAsync());
        next.Dispose();
        await stopping;
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DrainIncludesChildRegisteredDuringStopAndReportsFaultOnce()
    {
        var failures = new List<Exception>();
        using var lifetime = new ContentOperationLifetime(failures.Add);
        var parentReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var childReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var childRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Parent()
        {
            await parentReady.Task;
            lifetime.Track(childReady.Task);
            childRegistered.SetResult();
            throw new IOException("reported failure");
        }
        var parent = Parent();
        lifetime.Track(parent);
        lifetime.Track(parent);
        await lifetime.StopFileOperationsAsync();
        var drain = lifetime.DrainAsync();
        Assert.False(drain.IsCompleted);
        parentReady.SetResult();
        await childRegistered.Task;
        Assert.False(drain.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => lifetime.Dispose());
        childReady.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(failures);
    }

    [Fact]
    public async Task ThrowingCancellationAndDiagnosticsCannotSkipFileDrain()
    {
        using var lifetime = new ContentOperationLifetime(_ => throw new IOException("log failed"));
        using var registration = lifetime.Stopping.Register(() => throw new IOException("callback failed"));
        var operation = lifetime.BeginFileOperation("A");
        var stopping = lifetime.StopFileOperationsAsync();
        Assert.True(lifetime.IsStopping);
        Assert.False(stopping.IsCompleted);
        operation.Dispose();
        await stopping;
        lifetime.Track(Task.FromException(new IOException("UI cleanup failed")));
        lifetime.Track(Task.FromCanceled(new CancellationToken(true)));
        await lifetime.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
