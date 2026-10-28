namespace DesktopUp.Application;

// Layout-independent visible viewport. The full-size content is measured once per transition.
public readonly record struct PosterTransitionViewport(double BodyBottom, double FooterTop, double FooterVisibleHeight)
{
    public static PosterTransitionViewport Resolve(double height, double compactHeight, double footerHeight, bool collapsed)
    {
        if (!double.IsFinite(height) || !double.IsFinite(compactHeight) || !double.IsFinite(footerHeight) ||
            height <= 0 || compactHeight <= 0 || footerHeight < 0) throw new ArgumentOutOfRangeException(nameof(height));
        double visible = collapsed ? 0 : Math.Clamp(height - compactHeight, 0, footerHeight);
        return new(height - visible, height - visible, visible);
    }
}
