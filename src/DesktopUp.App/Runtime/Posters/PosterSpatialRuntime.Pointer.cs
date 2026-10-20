using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Read current registrations at call time. Do not capture this set in delayed callbacks.
    public IReadOnlyList<PosterPointerTarget> ReadPointerTargets() =>
        _windows.Values.Select(w => w.ReadPointerTarget()).ToArray();
    public AutoPostureProtection PointerProtection =>
        (_suspended ? AutoPostureProtection.Recovery : AutoPostureProtection.None) |
        // Item gestures are already declared on their own PosterPointerTarget. Only frame/title
        // gestures reserve the whole spatial scene; do not cancel unrelated automatic groups.
        (_owner is not null || _title is not null ? AutoPostureProtection.Gesture : AutoPostureProtection.None);
    public bool IsSpatialBusy => _owner is not null || _posture is not null || _title is not null || _windows.Values.Any(w => w.HasContentGesture);
}
