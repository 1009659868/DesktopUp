using DesktopUp.Application;
namespace DesktopUp;
internal sealed record BackdropPostureProbeFrame(string RequestId, string PosterId, bool Collapsed,
    int AcceptedFrame, int TotalFrames, DesktopPixelRect InitialBounds, DesktopPixelRect FinalBounds,
    DesktopPixelRect VisibleBounds);

internal sealed partial class PosterSpatialRuntime
{
    internal PosterVisualWindow BackdropWindowForProbe(string id) => _windows[id];
    // Null in production. The isolated probe can pace three frames without changing the
    // animation schedule, cursor, native commit, or watchdog's elapsed-time budget.
    internal Func<BackdropPostureProbeFrame, Task>? BackdropFrameObserverForProbe { get; set; }
}
