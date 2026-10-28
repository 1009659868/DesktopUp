namespace DesktopUp.Domain;

/// <summary>Shared physical-screen to content-viewport conversion. Scroll and loop folding are
/// separate: internal drags keep unwrapped travel, whereas an external drop selects a point.</summary>
public static class ContentScreenCoordinates
{
    public static (double X, double Y) ToViewport(double screenX, double screenY,
        double frameX, double frameY, double scaleX, double scaleY, double leftDip, double topDip)
    {
        if (!double.IsFinite(screenX) || !double.IsFinite(screenY) || !double.IsFinite(frameX) || !double.IsFinite(frameY) ||
            !double.IsFinite(scaleX) || !double.IsFinite(scaleY) || !double.IsFinite(leftDip) || !double.IsFinite(topDip) ||
            scaleX <= 0 || scaleY <= 0)
            throw new ContentLayoutException("A finite screen/DPI basis is required.");
        double x = (screenX - frameX) / scaleX - leftDip, y = (screenY - frameY) / scaleY - topDip;
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 16_000_000 || Math.Abs(y) > 16_000_000)
            throw new ContentLayoutException("Pointer exceeds the content coordinate budget.");
        return (x, y);
    }
}
