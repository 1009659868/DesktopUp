namespace DesktopUp.Application;

// Responsive geometry uses a fixed native allocation on BOTH axes. Its visible frame and
// logical content are still resized together; this is not an equal-width posture lease.
public sealed record PosterGeometryTransitionSurface(int Width, int Height)
{
    public static PosterGeometryTransitionSurface Create(DesktopPixelRect before, DesktopPixelRect after)
    {
        if (!before.IsUsable || !after.IsUsable)
            throw new ArgumentException("Geometry surfaces require valid endpoints.");
        return new(Math.Max(before.Width, after.Width), Math.Max(before.Height, after.Height));
    }

    public DesktopPixelRect At(DesktopPixelRect visible)
    {
        if (!visible.IsUsable || visible.Width > Width || visible.Height > Height)
            throw new ArgumentException("Visible geometry frame exceeds its prepared surface.");
        return visible with { Width = Width, Height = Height };
    }
}
