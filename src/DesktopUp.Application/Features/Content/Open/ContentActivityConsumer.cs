namespace DesktopUp.Application;

// One application-owned, event-driven pump. A failed projection leaves the durable facts queued;
// a later signal/startup retries statistics only, never the native open operation.
public sealed class ContentActivityConsumer(IContentActivityStore store, Action<Exception> reportFailure)
{
    private readonly object gate = new();
    private Task? worker;
    private bool requested, stopped;

    // Raised after the statistics transaction commits, never while the store is locked.
    // Observers enqueue presentation work; they must not block on this consumer.
    public event Action<IReadOnlyList<Guid>>? StatisticsChanged;

    public void RequestRefresh()
    {
        lock (gate)
        {
            if (stopped) return;
            requested = true;
            worker ??= Task.Run(DrainAsync);
        }
    }

    public Task WhenIdleAsync()
    {
        lock (gate) return worker ?? Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (gate)
        {
            stopped = true;
            requested = false;
            return worker ?? Task.CompletedTask;
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            lock (gate)
            {
                if (stopped) { worker = null; return; }
                requested = false;
            }
            bool more;
            try
            {
                var batch = store.ConsumeNext();
                more = batch.HasMore;
                if (batch.ChangedContentIds.Count > 0)
                {
                    // A presentation failure cannot roll back a confirmed open or replay counts.
                    try { StatisticsChanged?.Invoke(batch.ChangedContentIds); }
                    catch (Exception error) { try { reportFailure(error); } catch { } }
                }
            }
            catch (Exception error)
            {
                // Logging failure must not strand the task owner. Retry only when a NEW
                // signal arrived during the failed attempt, never spin on a persistent fault.
                try { reportFailure(error); } catch { }
                more = false;
            }
            lock (gate)
            {
                if (stopped || (!more && !requested)) { worker = null; return; }
            }
            // Release the database lock and the current scheduler turn between bounded batches.
            await Task.Yield();
        }
    }
}
