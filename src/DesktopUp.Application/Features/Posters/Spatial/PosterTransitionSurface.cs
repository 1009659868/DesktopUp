namespace DesktopUp.Application;

// A temporary render allocation, never the poster's visible/persisted/spatial geometry.
public sealed record PosterTransitionSurface(int Width, int Height)
{
    public static PosterTransitionSurface Create(DesktopPixelRect before, DesktopPixelRect after)
    {
        if (!before.IsUsable || !after.IsUsable || before.Width != after.Width)
            throw new ArgumentException("Posture surfaces require valid, equal-width endpoints.");
        return new(before.Width, Math.Max(before.Height, after.Height));
    }

    public DesktopPixelRect At(DesktopPixelRect visible)
    {
        if (!visible.IsUsable || visible.Width != Width || visible.Height > Height)
            throw new ArgumentException("Visible frame exceeds its prepared surface.");
        return visible with { Width = Width, Height = Height };
    }
}
