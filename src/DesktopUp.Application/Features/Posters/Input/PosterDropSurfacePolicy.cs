namespace DesktopUp.Application;

public static class PosterDropSurfacePolicy
{
    // Full visible rectangle for OLE, independent of material alpha, cards and rounded corners.
    // The caller supplies the CURRENT collapsed/animated frame, never the saved expanded height.
    public static bool ReceivesOle(DesktopPixelRect frame, PointerScreenPoint point) => frame.IsUsable &&
        point.X >= frame.X && point.Y >= frame.Y &&
        (long)point.X < (long)frame.X + frame.Width && (long)point.Y < (long)frame.Y + frame.Height;

    // Ordinary input and OLE both own the current bounded frame, regardless of
    // material alpha. The command router separately checks title/buttons/items.
    public static bool ReceivesPointer(DesktopPixelRect frame, PosterInputLayoutSnapshot layout, PointerScreenPoint point) =>
        frame.Width == layout.FrameWidth && frame.Height == layout.FrameHeight && ReceivesOle(frame, point);
}
