namespace DesktopUp.Application;

/// <summary>One source binding's notification coalescer. May be signalled off-dispatcher.
/// Closing is terminal: an old callback cannot consume a new binding's notification.</summary>
public sealed class PosterSourceRefresh
{
    // 0 idle, 1 queued, 2 closed. No timers, tasks or native resources.
    private int state;
    public bool TryQueue() => Interlocked.CompareExchange(ref state, 1, 0) == 0;
    public bool Complete() => Interlocked.CompareExchange(ref state, 0, 1) == 1;
    public void Close() => Interlocked.Exchange(ref state, 2);
}
