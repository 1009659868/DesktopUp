using DesktopUp.Application;

namespace DesktopUp.Windows;

public static class PosterGeometryProjector
{
    public static DesktopPixelRect Project(
        PosterPlacementState placement,
        DisplayMonitorSnapshot targetMonitor)
    {
        ArgumentNullException.ThrowIfNull(targetMonitor);
        DesktopPixelRect workArea = targetMonitor.WorkArea;
        if (!workArea.IsUsable)
        {
            throw new ArgumentException("Target monitor work area is not usable.", nameof(targetMonitor));
        }

        double widthDip = NormalizeExtent(placement.WidthDip, 420);
        double heightDip = NormalizeExtent(placement.HeightDip, 240);
        double anchorX = NormalizeAnchor(placement.HorizontalAnchor);
        double anchorY = NormalizeAnchor(placement.VerticalAnchor);
        int width = Math.Clamp(
            checked((int)Math.Round(widthDip * targetMonitor.ScaleX)),
            1,
            workArea.Width);
        int height = Math.Clamp(
            checked((int)Math.Round(heightDip * targetMonitor.ScaleY)),
            1,
            workArea.Height);
        int x = workArea.X + checked((int)Math.Round((workArea.Width - width) * anchorX));
        int y = workArea.Y + checked((int)Math.Round((workArea.Height - height) * anchorY));

        return new DesktopPixelRect(
            Math.Clamp(x, workArea.X, workArea.Right - width),
            Math.Clamp(y, workArea.Y, workArea.Bottom - height),
            width,
            height);
    }

    private static double NormalizeExtent(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double NormalizeAnchor(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5;
}
