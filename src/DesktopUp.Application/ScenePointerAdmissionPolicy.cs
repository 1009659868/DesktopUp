namespace DesktopUp.Application;

// Read-only decoration does not share the gesture-admission rules of automatic posture.
// The observer still proves current geometry, desktop identity and non-occlusion. No input
// is captured, rerouted or manufactured here; unknown/new protection bits fail closed.
public static class ScenePointerAdmissionPolicy
{
    private const AutoPostureProtection NonMutatingInteraction = AutoPostureProtection.PointerButton |
        AutoPostureProtection.PointerCapture | AutoPostureProtection.Gesture | AutoPostureProtection.Menu |
        AutoPostureProtection.Confirmation | AutoPostureProtection.DragDrop;

    public static bool CanAnimate(bool enabled, AutoPosturePresence presence, AutoPostureProtection protection) =>
        enabled && (presence is AutoPosturePresence.Inside or AutoPosturePresence.Outside or AutoPosturePresence.ExpansionZone) &&
        (protection & ~NonMutatingInteraction) == AutoPostureProtection.None;
}
