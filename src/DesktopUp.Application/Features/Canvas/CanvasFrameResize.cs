using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Resize the poster as one artboard. Editing its aspect belongs to the workbench.
/// Work-area/minimum constraints reject infeasible requests; no independent background fitting.</summary>
public static class CanvasFrameResize
{
    public static (int Width, int Height) Enclose(CanvasArtboard art, int width, int height, double scaleX, double scaleY)
    {
        double ratio = art.Width * scaleX / (art.Height * scaleY);
        return width / (double)height > ratio ? (width, (int)Math.Ceiling(width / ratio)) : ((int)Math.Ceiling(height * ratio), height);
    }
    public static DesktopPixelRect Resolve(CanvasArtboard art, PosterGestureKind gesture, DesktopPixelRect initial,
        int dx, int dy, DesktopPixelRect work, int minWidth, int minHeight, double scaleX, double scaleY)
    {
        if (gesture == PosterGestureKind.Move) return PosterFrameResizePolicy.Resolve(gesture, initial, dx, dy, work, minWidth, minHeight);
        bool left = gesture is PosterGestureKind.ResizeLeft or PosterGestureKind.ResizeTopLeft or PosterGestureKind.ResizeBottomLeft;
        bool top = gesture is PosterGestureKind.ResizeTop or PosterGestureKind.ResizeTopLeft or PosterGestureKind.ResizeTopRight;
        bool vertical = gesture is PosterGestureKind.ResizeTop or PosterGestureKind.ResizeBottom;
        double ratio = art.Width * scaleX / (art.Height * scaleY);
        double requestedWidth = vertical ? (initial.Height + (top ? -dy : dy)) * ratio : initial.Width + (left ? -dx : dx);
        double minimum = Math.Max(minWidth, minHeight * ratio);
        double maximum = Math.Min(left ? initial.Right - work.X : work.Right - initial.X,
            (top ? initial.Bottom - work.Y : work.Bottom - initial.Y) * ratio);
        if (maximum < minimum) return initial;
        int width = (int)Math.Round(Math.Clamp(requestedWidth, minimum, maximum));
        int height = (int)Math.Round(width / ratio);
        return new(left ? initial.Right - width : initial.X, top ? initial.Bottom - height : initial.Y, width, height);
    }
}
