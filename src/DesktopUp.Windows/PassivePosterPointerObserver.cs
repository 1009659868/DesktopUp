using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed record PosterPointerTarget(PosterInputSessionKey Session, nint Visual, nint Input,
    DesktopShellHostSnapshot? Shell, bool Enabled, bool Ready, bool Visible, bool Collapsed,
    AutoPostureProtection Protection = AutoPostureProtection.None);

internal interface IDesktopPointerNative
{
    bool TryReadCursor(out PointerScreenPoint point);
    bool TryReadProtection(out AutoPostureProtection protection);
    nint ReceiverRoot(PointerScreenPoint point);
    IReadOnlyList<nint>? ReadWindowOrder();
    bool IsSurfaceCurrent(nint visual, nint input, DesktopPixelRect frame);
    bool IsDesktopReceiver(nint root, DesktopShellHostSnapshot shell);
    bool IsDesktopRoot(nint root);
    bool CoversPoint(nint window, PointerScreenPoint point);
}

// One shared, on-demand, UI-thread adapter. No timer/hook/window, no input injection,
// activation, hit-test mutation or writes. App's M08-04 scheduler owns wake-ups and timing.
public sealed class PassivePosterPointerObserver
{
    private readonly PosterInputRouter _router;
    private readonly IDesktopPointerNative _native;
    private readonly PosterPointerTracker _tracker = new();
    private PosterInputSessionKey? _nativeInteractionOwner;
    private const AutoPostureProtection RawPointer = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture;
    public PassivePosterPointerObserver(PosterInputRouter router) : this(router, new NativeDesktopPointerPlatform()) { }
    internal PassivePosterPointerObserver(PosterInputRouter router, IDesktopPointerNative native)
    { _router = router; _native = native; }
    public PosterPointerSnapshot Current => _tracker.Current;

    public PosterPointerSnapshot Sample(long sequence, long environmentEpoch,
        IReadOnlyList<PosterPointerTarget> targets, AutoPostureProtection shared = AutoPostureProtection.None)
    {
        if (sequence <= Current.Sequence || environmentEpoch < Current.EnvironmentEpoch) return Current;
        if (environmentEpoch != Current.EnvironmentEpoch || _nativeInteractionOwner is { } previousOwner &&
            !targets.Any(t => t.Session == previousOwner && t.Ready && t.Visible)) _nativeInteractionOwner = null;
        // Disabled/hidden targets are retained only as obstacles; no dedicated polling at zero demand.
        bool demand = targets.Any(t => t.Enabled && t.Visible && t.Ready);
        if (!demand) _nativeInteractionOwner = null;
        var readings = new List<PosterPointerReading>(targets.Count);
        // Router capture starts on DOWN, before the input island's drag threshold is met.
        // Actual poster gestures are supplied by the spatial runtime/target, not inferred here.
        if (_router.Capture is not null) shared |= AutoPostureProtection.PointerCapture;
        if (DesktopMenuInteraction.IsActive) shared |= AutoPostureProtection.Menu;
        PointerScreenPoint? position = null;
        nint receiver = 0;
        IReadOnlyList<nint>? order = null;
        bool reliable = false;
        AutoPostureProtection nativeProtection = AutoPostureProtection.None;
        bool protectionRead = false;
        if (demand && _native.TryReadCursor(out var point))
        {
            position = point;
            reliable = protectionRead = _native.TryReadProtection(out nativeProtection);
            shared |= nativeProtection & ~RawPointer;
            if (reliable)
            {
                receiver = _native.ReceiverRoot(point);
                order = _native.ReadWindowOrder();
                reliable = receiver != 0 && order is not null;
            }
        }
        if (demand && !reliable) shared |= AutoPostureProtection.Recovery;
        // Read published INPUT geometry, not the larger animation visual HWND or stored preferred size.
        var surfaces = targets.ToDictionary(t => t.Session,
            t => t.Ready && t.Visible ? _router.ReadSurface(t.Session) : null);
        if (protectionRead && (nativeProtection & RawPointer) == 0) _nativeInteractionOwner = null;
        if (reliable && order is not null && (nativeProtection & RawPointer) != 0)
        {
            // Disabled-auto peers still participate in collision/push space. Retain the
            // interaction owner until release even when its pointer moves outside the frame.
            foreach (var target in targets)
                if (target.Ready && target.Visible && surfaces[target.Session] is { } surface &&
                    target.Shell is { } shell && _native.IsDesktopReceiver(shell.Identity.ContainerHandle, shell) &&
                    order.Contains(target.Visual) && order.Contains(target.Input) &&
                    _native.IsSurfaceCurrent(target.Visual, target.Input, surface.Frame) &&
                    PosterInputGeometry.Contains(surface, position!.Value) &&
                    CanReceive(target, position.Value, receiver, order, targets, surfaces))
                { _nativeInteractionOwner ??= target.Session; break; }
            if (_nativeInteractionOwner is not null || _router.Capture is not null)
                shared |= nativeProtection & RawPointer;
        }
        foreach (var target in targets)
        {
            var surface = surfaces[target.Session];
            var presence = AutoPosturePresence.Unknown;
            if (target.Enabled && target.Ready && target.Visible && reliable && surface is not null &&
                target.Shell is { } currentShell && _native.IsDesktopReceiver(currentShell.Identity.ContainerHandle, currentShell) &&
                order is not null && order.Contains(target.Visual) && order.Contains(target.Input) &&
                _native.IsSurfaceCurrent(target.Visual, target.Input, surface.Frame))
            {
                if (!PosterInputGeometry.Contains(surface, position!.Value)) presence = AutoPosturePresence.Outside;
                else if (receiver != target.Visual && receiver != target.Input && _native.IsDesktopRoot(receiver) &&
                    !_native.IsDesktopReceiver(receiver, currentShell)) presence = AutoPosturePresence.Unknown;
                else if (CanReceive(target, position.Value, receiver, order!, targets, surfaces))
                    presence = target.Collapsed && PosterInputGeometry.IsExpansionZone(surface, position.Value)
                        ? AutoPosturePresence.ExpansionZone : AutoPosturePresence.Inside;
                else presence = AutoPosturePresence.Outside;
            }
            readings.Add(new(target.Session, surface, presence, target.Protection));
        }
        return _tracker.Observe(sequence, environmentEpoch, position, readings, shared);
    }

    private bool CanReceive(PosterPointerTarget target, PointerScreenPoint point, nint receiver,
        IReadOnlyList<nint> order, IReadOnlyList<PosterPointerTarget> targets,
        IReadOnlyDictionary<PosterInputSessionKey, PosterInputSurfaceSnapshot?> surfaces)
    {
        bool own = receiver == target.Visual || receiver == target.Input;
        bool desktop = target.Shell is { } shell && _native.IsDesktopReceiver(receiver, shell);
        if (!own && !desktop) return false; // includes other posters, popup, taskbar and Explorer folder windows
        foreach (nint window in order)
        {
            if (window == target.Visual || window == target.Input) return true;
            var peer = targets.FirstOrDefault(t => t.Visual == window || t.Input == window);
            if (peer is not null)
            {
                // A peer's capacity region is not its visible footprint.
                if (surfaces[peer.Session] is { } peerSurface && PosterInputGeometry.Contains(peerSurface, point)) return false;
                continue;
            }
            if (target.Shell is { } host && window == host.Identity.ContainerHandle) continue;
            // WindowFromPoint skips disabled windows. Conservatively honour visible occluders
            // above the poster too; do not let old routed evidence override a blocking window.
            if (_native.CoversPoint(window, point)) return false;
        }
        return false; // missing current surface in physical z-order is not hover evidence
    }
}
