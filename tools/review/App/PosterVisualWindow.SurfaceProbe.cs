using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal void ResetVisualRegionForProbe()
    {
        if (_disposed || _windowHandle == 0 || _appliedVisualRegion is null ||
            ProbeSetWindowRgn(_windowHandle, 0, false) == 0)
            throw new InvalidOperationException("Could not inject an own-window region reset.");
        var observed = attachment.InspectVisualSurface(_windowHandle, CurrentNativeBounds(_bounds), _bounds);
        if (observed.WindowRegionResult != 0)
            throw new InvalidOperationException("Injected reset did not remove the native HRGN.");
        log.Write("info", "title_surface.region_reset_injected", new
        {
            PosterId, requestId = _contentSession.ActiveCommand,
            cachedWidth = _appliedVisualRegion?.Width, cachedHeight = _appliedVisualRegion?.Height, observed,
            nativeInput = false, databaseWrites = false
        });
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowRgn")]
    private static extern int ProbeSetWindowRgn(nint window, nint region,
        [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
