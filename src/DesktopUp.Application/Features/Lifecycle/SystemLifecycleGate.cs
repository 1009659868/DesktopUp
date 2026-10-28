namespace DesktopUp.Application;

public enum SystemLifecycleSignal { PowerSuspend, PowerResume, SessionLock, SessionUnlock, SessionDisconnect, SessionConnect }
public enum SystemLifecycleAction { None, Suspend, Recover }

// Independent reasons, not a single toggle: power resume must not release a locked session.
// This gate is runtime-only; no deadline or pause flag is persisted.
public sealed class SystemLifecycleGate
{
    [Flags] private enum Reasons { None = 0, Power = 1, Locked = 2, Disconnected = 4 }
    private Reasons _reasons;
    public bool IsSuspended => _reasons != Reasons.None;
    public SystemLifecycleAction Observe(SystemLifecycleSignal signal)
    {
        var before = _reasons;
        _reasons = signal switch
        {
            SystemLifecycleSignal.PowerSuspend => _reasons | Reasons.Power,
            SystemLifecycleSignal.PowerResume => _reasons & ~Reasons.Power,
            SystemLifecycleSignal.SessionLock => _reasons | Reasons.Locked,
            SystemLifecycleSignal.SessionUnlock => _reasons & ~Reasons.Locked,
            SystemLifecycleSignal.SessionDisconnect => _reasons | Reasons.Disconnected,
            SystemLifecycleSignal.SessionConnect => _reasons & ~Reasons.Disconnected,
            _ => throw new ArgumentOutOfRangeException(nameof(signal))
        };
        if (IsSuspended) return (_reasons & ~before) == Reasons.None ? SystemLifecycleAction.None : SystemLifecycleAction.Suspend;
        // A resume can arrive without its suspend (startup/lost notification). Rebaseline safely;
        // duplicate resumes are coalesced by the existing desktop recovery debounce/version gate.
        return signal is SystemLifecycleSignal.PowerResume or SystemLifecycleSignal.SessionUnlock or SystemLifecycleSignal.SessionConnect
            ? SystemLifecycleAction.Recover : SystemLifecycleAction.None;
    }
}
