namespace DesktopUp.Application;

public static class PosterDropSurfacePolicy
{
    // Full visible rectangle for OLE, independent of material alpha, cards and rounded corners.
    // The caller supplies the CURRENT collapsed/animated frame, never the saved expanded height.
    public static bool ReceivesOle(DesktopPixelRect frame, PointerScreenPoint point) => frame.IsUsable &&
        point.X >= frame.X && point.Y >= frame.Y &&
        (long)point.X < (long)frame.X + frame.Width && (long)point.Y < (long)frame.Y + frame.Height;

    // Ordinary pointer input remains sparse. This does not prove cross-process click-through;
    // the production native host must pass its own desktop/environment gate.
    public static bool ReceivesPointer(DesktopPixelRect frame, PosterInputLayoutSnapshot layout, PointerScreenPoint point)
    {
        if (frame.Width != layout.FrameWidth || frame.Height != layout.FrameHeight || !ReceivesOle(frame, point)) return false;
        int x = point.X - frame.X, y = point.Y - frame.Y;
        return PosterInputGeometry.InsideRoundedFrame(x, y, layout) && layout.Regions.Any(r =>
            PosterInputGeometry.CapturesPointer(r) && x >= r.Bounds.X && y >= r.Bounds.Y && x < r.Bounds.Right && y < r.Bounds.Bottom);
    }
}
