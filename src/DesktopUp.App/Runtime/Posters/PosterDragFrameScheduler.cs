namespace DesktopUp;

// Dispatcher-owned, one-shot rendering subscription shared by both poster drag paths.
// Input replaces the pending work, not the clock. No timer or animation easing is involved.
internal sealed class PosterDragFrameScheduler(
    Action<EventHandler<object>> subscribe, Action<EventHandler<object>> unsubscribe)
{
    private object? _owner;
    private Action? _pending;
    private EventHandler<object>? _subscription;

    public void Request(object owner, Action present)
    {
        if (_owner is not null && !ReferenceEquals(_owner, owner))
            throw new InvalidOperationException("Another drag owns the presentation frame.");
        _owner = owner;
        _pending = present;
        if (_subscription is not null) return;
        EventHandler<object>? handler = null;
        handler = (_, _) =>
        {
            // A cancelled event may already be in a dispatch snapshot. It cannot consume
            // a newer session's request, even if that session uses the same window.
            if (!ReferenceEquals(_subscription, handler)) return;
            var work = _pending;
            Cancel(owner);
            work?.Invoke();
        };
        _subscription = handler;
        subscribe(handler);
    }

    public void Cancel(object owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;
        var handler = _subscription;
        _subscription = null;
        _pending = null;
        _owner = null;
        if (handler is not null) unsubscribe(handler);
    }
}
