namespace DesktopUp.Application;

public sealed record AutoPostureSample(IReadOnlyList<AutoPostureObservation> Posters,
    bool SpatialBusy, AutoPostureProtection Protection = AutoPostureProtection.None)
{
    public bool HasDemand => Posters.Any(p => p.Policy.Enabled && p.IsVisible && p.IsReady);
}

// Dispatcher-confined. Native input callbacks may observe immediately, but never run spatial
// effects on their stack. There is one queued pump, one clock and one replaceable timer.
public sealed class AutoPostureScheduler(
    Func<long> clock, Func<AutoPostureSample> read, Func<Action, bool> enqueue,
    Action<int?> armTimer, Action<AutoPostureRequest, Action<AutoPostureOutcome>> start,
    Action<AutoPostureRequest> cancel, Action<string, object>? trace = null) : IDisposable
{
    public const int FallbackIntervalMs = 120;
    private AutoPostureState _state = AutoPostureState.Empty;
    private AutoPostureRequest? _executing;
    private AutoPostureRequest? _cancel;
    private long _sequence;
    private bool _queued, _disposed;
    public AutoPostureState State => _state;

    public void Observe()
    {
        if (_disposed) return;
        var sample = Read();
        Advance(sample, deferStart: true);
        if (sample.HasDemand || _state.Outstanding is not null) Queue();
        else armTimer(null);
    }

    private AutoPostureSample Read()
    {
        try { return read(); }
        catch (Exception e)
        {
            Trace("observation_failed", new { e.Message });
            return new([], false, AutoPostureProtection.Recovery);
        }
    }

    private AutoPostureStep Advance(AutoPostureSample sample, bool deferStart, AutoPostureCompletion? completion = null)
    {
        var previous = _state;
        var step = AutoPostureModel.Advance(_state, new(++_sequence, clock(), sample.Posters,
            deferStart || sample.SpatialBusy, sample.Protection, completion));
        _state = step.State;
        if (step.Cancel is { } cancelled)
        {
            _cancel = cancelled;
            var owner = sample.Posters.FirstOrDefault(p => p.Session.PosterId == cancelled.Session.PosterId);
            // Capture evidence at invalidation, not at the later pump after button-up.
            Trace("invalidated", new { cancelled.Id, cancelled.Session, cancelled.Collapse,
                sharedProtection = sample.Protection.ToString(), localProtection = owner?.Protection.ToString(),
                presence = owner?.Pointer.Presence.ToString(), owner?.IsReady, owner?.IsVisible,
                owner?.EnvironmentEpoch, owner?.ManualIntentVersion, owner?.Policy });
        }
        foreach (var (id, node) in _state.Posters)
            if (!previous.Posters.TryGetValue(id, out var old) || old.Phase != node.Phase)
                Trace("phase", new { id, phase = node.Phase.ToString() });
        return step;
    }

    private void Queue()
    {
        if (_queued || _disposed) return;
        _queued = true;
        if (!enqueue(Pump)) { _queued = false; Dispose(); }
    }

    private void Pump()
    {
        _queued = false;
        if (_disposed) return;
        var sample = Read(); // Never execute a captured target list or expired observation.
        var step = Advance(sample, deferStart: false);
        if (_cancel is { } stop)
        {
            _cancel = null;
            Trace("cancel", new { stop.Id, stop.Session });
            if (_executing == stop) cancel(stop);
            else Complete(stop, AutoPostureOutcome.Cancelled);
        }
        else if (step.Request is { } request)
        {
            _executing = request;
            Trace("request", new { request.Id, request.Session, request.Collapse });
            // The port must acknowledge exactly once, after physical restoration, even on
            // synchronous rejection. Complete observes but cannot start another effect here.
            start(request, outcome => Complete(request, outcome));
        }
        var due = _state.Posters.Values.Where(n => n.DeadlineMs is not null && n.DeadlineMs > _state.NowMs)
            .Select(n => n.DeadlineMs!.Value - _state.NowMs).DefaultIfEmpty(FallbackIntervalMs).Min();
        armTimer(sample.HasDemand ? (int)Math.Clamp(due, 1, FallbackIntervalMs) : null);
    }

    private void Complete(AutoPostureRequest request, AutoPostureOutcome outcome)
    {
        if (_disposed || _state.Outstanding != request) return;
        _executing = null;
        if (_cancel == request) _cancel = null;
        Trace("completed", new { request.Id, request.Session, outcome = outcome.ToString() });
        Advance(Read(), deferStart: true, new(request, outcome));
        Queue();
    }

    private void Trace(string name, object data)
    {
        try { trace?.Invoke(name, data); } catch { /* Logging cannot interrupt scheduling or native input. */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        armTimer(null);
        if (_executing is { } request) cancel(request);
        _executing = null; _cancel = null; _state = AutoPostureState.Empty;
    }
}
