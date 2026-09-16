namespace DesktopUp.Application;

// Captured by the SAME native read used to accept/reject a frame, before recovery.
// Null rectangles mean unavailable, not a zero-sized successful read. Not a GPU fence.
public readonly record struct DesktopVisualSurfaceReadback(
    bool Verified,
    string? Failure,
    DesktopPixelRect? Window = null,
    DesktopPixelRect? Client = null,
    DesktopPixelRect? Region = null,
    int? WindowRegionResult = null,
    int? RegionBoxResult = null)
{
    public static DesktopVisualSurfaceReadback Compare(
        DesktopPixelRect surface, DesktopPixelRect visible,
        DesktopPixelRect? window, DesktopPixelRect? client, DesktopPixelRect? region,
        int? windowRegionResult, int? regionBoxResult)
    {
        string? failure = !surface.IsUsable || !visible.IsUsable ||
            surface.X != visible.X || surface.Y != visible.Y ||
            surface.Width < visible.Width || surface.Height < visible.Height ? "surface-contract-invalid" :
            window is null ? "window-read-failed" : window != surface ? "window-bounds-mismatch" :
            client is null ? "client-read-failed" :
            client != new DesktopPixelRect(0, 0, surface.Width, surface.Height) ? "client-bounds-mismatch" :
            windowRegionResult is null ? "region-allocation-failed" :
            windowRegionResult == 0 ? "window-region-read-failed" :
            regionBoxResult is null or 0 || region is null ? "region-box-read-failed" :
            region != new DesktopPixelRect(0, 0, visible.Width, visible.Height) ? "region-bounds-mismatch" : null;
        return new(failure is null, failure, window, client, region, windowRegionResult, regionBoxResult);
    }
}
