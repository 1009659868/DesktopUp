namespace DesktopUp.Contracts.Tests;

public sealed class PosterDragFrameSchedulerTests
{
    private sealed class Frames
    {
        public EventHandler<object>? Rendering;
        public PosterDragFrameScheduler Scheduler { get; }
        public Frames() => Scheduler = new(h => Rendering += h, h => Rendering -= h);
        public void Tick() => Rendering?.Invoke(this, EventArgs.Empty);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(240)]
    public void LatestInputRunsOncePerRequestedRenderingWithoutAnIntervalGate(int frames)
    {
        var clock = new Frames(); var owner = new object(); var seen = new List<int>();
        for (int i = 0; i < frames; i++)
        {
            int latest = i;
            clock.Scheduler.Request(owner, () => seen.Add(-1));
            clock.Scheduler.Request(owner, () => seen.Add(latest));
            Assert.Single(clock.Rendering!.GetInvocationList());
            clock.Tick(); clock.Tick();
        }
        Assert.Equal(Enumerable.Range(0, frames), seen);
        Assert.Null(clock.Rendering); // idle does not subscribe or poll
    }

    [Fact]
    public void CancellationInvalidatesCapturedCallbackEvenForSameOwner()
    {
        var clock = new Frames(); var owner = new object(); int seen = 0;
        clock.Scheduler.Request(owner, () => seen = -1);
        var stale = clock.Rendering;
        clock.Scheduler.Cancel(owner);
        clock.Scheduler.Request(owner, () => seen = 2);
        stale!(clock, EventArgs.Empty);
        Assert.Equal(0, seen);
        clock.Tick(); Assert.Equal(2, seen);
    }

    [Fact]
    public void CallbackCanRequestNextFrameAndFailureLeavesNoDanglingSubscription()
    {
        var clock = new Frames(); var owner = new object(); int count = 0;
        clock.Scheduler.Request(owner, () =>
        {
            count++;
            clock.Scheduler.Request(owner, () => throw new InvalidOperationException("presentation failed"));
        });
        clock.Tick(); Assert.Equal(1, count);
        Assert.Throws<InvalidOperationException>(clock.Tick);
        Assert.Null(clock.Rendering);
        clock.Scheduler.Request(new object(), () => count++);
        clock.Tick(); Assert.Equal(2, count);
    }

    [Fact]
    public void OtherOwnerCannotReplaceOrCancelPendingFrame()
    {
        var clock = new Frames(); var owner = new object(); int seen = 0;
        clock.Scheduler.Request(owner, () => seen++);
        Assert.Throws<InvalidOperationException>(() => clock.Scheduler.Request(new object(), () => seen = -1));
        clock.Scheduler.Cancel(new object()); clock.Tick(); Assert.Equal(1, seen);
    }
}
