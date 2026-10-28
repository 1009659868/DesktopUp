using DesktopUp.Application;

namespace DesktopUp.Windows;

internal static class DesktopPresentationFlags
{
    internal static uint Resolve(DesktopFramePhase phase, bool visual) => (phase, visual) switch
    {
        (DesktopFramePhase.Animation, true) => DesktopZOrderPolicy.SpatialBatchFlags | 0x0008 /* NOREDRAW */ | 0x0001 /* NOSIZE: allocation is already ready */,
        (DesktopFramePhase.FinalRestore, true) => DesktopZOrderPolicy.SpatialBatchFlags & ~0x0100u,
        (DesktopFramePhase.SurfacePreparation, true) => DesktopZOrderPolicy.SpatialBatchFlags & ~0x0100u,
        _ => DesktopZOrderPolicy.SpatialBatchFlags
    };
}
