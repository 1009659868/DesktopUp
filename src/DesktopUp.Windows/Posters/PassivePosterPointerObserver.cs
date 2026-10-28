using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed record PosterPointerTarget(PosterInputSessionKey Session, nint Visual, nint Input,
    DesktopShellHostSnapshot? Shell, bool Enabled, bool Ready, bool Visible, bool Collapsed,
    AutoPostureProtection Protection = AutoPostureProtection.None);

public sealed record PosterPointerTargetDiagnostic(PosterInputSessionKey Session, string Reason,
    AutoPosturePresence Presence, bool Enabled, bool Ready, bool Visible, bool Collapsed,
    DesktopPixelRect? PublishedFrame, string? OccludingWindow, AutoPostureProtection Protection);
public sealed record PosterPointerSampleDiagnostic(long Sequence, long EnvironmentEpoch,
    PointerScreenPoint? Position, bool Reliable, bool ProtectionRead, string ReceiverRoot, int? WindowCount,
    AutoPostureProtection NativeProtection, AutoPostureProtection SharedProtection, IReadOnlyList<PosterPointerTargetDiagnostic> Posters);

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
    // Position belongs to the same completed sample as Current. Consumers must still require
    // their observed presence to be Inside and honour protection before using it decoratively.
    public PointerScreenPoint? LastPosition { get; private set; }
    // Diagnostics are captured by the acceptance read itself, never by a second native query.
    // This is a bounded in-memory snapshot; production sampling does not log every frame.
    public PosterPointerSampleDiagnostic? LastDiagnostics { get; private set; }

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
        var diagnostics = new List<PosterPointerTargetDiagnostic>(targets.Count);
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
                    CanReceive(target, position.Value, receiver, order, targets, surfaces, out _, out _))
                { _nativeInteractionOwner ??= target.Session; break; }
            if (_nativeInteractionOwner is not null || _router.Capture is not null)
                shared |= nativeProtection & RawPointer;
        }
        foreach (var target in targets)
        {
            var surface = surfaces[target.Session];
            var presence = AutoPosturePresence.Unknown;
            string reason;
            nint occluder = 0;
            if (!target.Enabled) reason = "not-demanded";
            else if (!target.Ready) reason = "target-not-ready";
            else if (!target.Visible) reason = "target-hidden";
            else if (!reliable) reason = "native-sample-unreliable";
            else if (surface is null) reason = "published-surface-unavailable";
            else if (target.Shell is not { } currentShell || !_native.IsDesktopReceiver(currentShell.Identity.ContainerHandle, currentShell))
                reason = "desktop-identity-stale";
            else if (order is null || !order.Contains(target.Visual) || !order.Contains(target.Input)) reason = "surface-missing-from-z-order";
            else if (!_native.IsSurfaceCurrent(target.Visual, target.Input, surface.Frame)) reason = "native-surface-mismatch";
            else
            {
                if (!PosterInputGeometry.Contains(surface, position!.Value))
                { presence = AutoPosturePresence.Outside; reason = "outside-published-frame"; }
                else if (receiver != target.Visual && receiver != target.Input && _native.IsDesktopRoot(receiver) &&
                    !_native.IsDesktopReceiver(receiver, currentShell)) reason = "alternate-desktop-root";
                else if (CanReceive(target, position.Value, receiver, order!, targets, surfaces, out reason, out occluder))
                {
                    presence = target.Collapsed && PosterInputGeometry.IsExpansionZone(surface, position.Value)
                        ? AutoPosturePresence.ExpansionZone : AutoPosturePresence.Inside;
                }
                else presence = AutoPosturePresence.Outside;
            }
            readings.Add(new(target.Session, surface, presence, target.Protection));
            diagnostics.Add(new(target.Session, reason, presence, target.Enabled, target.Ready, target.Visible,
                target.Collapsed, surface?.Frame, occluder == 0 ? null : $"0x{occluder:X}", target.Protection));
        }
        LastPosition = reliable ? position : null;
        LastDiagnostics = new(sequence, environmentEpoch, position, reliable, protectionRead, $"0x{receiver:X}",
            order?.Count, nativeProtection, shared, diagnostics.AsReadOnly());
        return _tracker.Observe(sequence, environmentEpoch, position, readings, shared);
    }

    private bool CanReceive(PosterPointerTarget target, PointerScreenPoint point, nint receiver,
        IReadOnlyList<nint> order, IReadOnlyList<PosterPointerTarget> targets,
        IReadOnlyDictionary<PosterInputSessionKey, PosterInputSurfaceSnapshot?> surfaces, out string reason, out nint occluder)
    {
        occluder = 0;
        bool own = receiver == target.Visual || receiver == target.Input;
        bool desktop = target.Shell is { } shell && _native.IsDesktopReceiver(receiver, shell);
        if (!own && !desktop)
        { reason = "receiver-not-poster-or-verified-desktop"; occluder = receiver; return false; }
        foreach (nint window in order)
        {
            if (window == target.Visual || window == target.Input)
            { reason = "inside-unoccluded-published-frame"; return true; }
            var peer = targets.FirstOrDefault(t => t.Visual == window || t.Input == window);
            if (peer is not null)
            {
                // A peer's capacity region is not its visible footprint.
                if (surfaces[peer.Session] is { } peerSurface && PosterInputGeometry.Contains(peerSurface, point))
                { reason = "peer-poster-occludes"; occluder = window; return false; }
                continue;
            }
            if (target.Shell is { } host && window == host.Identity.ContainerHandle) continue;
            // WindowFromPoint skips disabled windows. Conservatively honour visible occluders
            // above the poster too; do not let old routed evidence override a blocking window.
            if (_native.CoversPoint(window, point))
            { reason = "native-window-occludes"; occluder = window; return false; }
        }
        reason = "surface-missing-from-z-order";
        return false; // missing current surface in physical z-order is not hover evidence
    }
}
