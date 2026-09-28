namespace DesktopUp.Application;

// UI-thread owned. The caller validates the current menu/basis; this gate only limits
// one menu instance to one action. Closing cannot retract an already granted dispatch.
public sealed class PosterMenuActionGate
{
    private bool _closedOrConsumed;

    public bool TryConsume(bool isCurrent)
    {
        if (_closedOrConsumed) return false;
        _closedOrConsumed = true; // A stale attempt also ends this menu's authority.
        return isCurrent;
    }

    public void Close() => _closedOrConsumed = true;
}
