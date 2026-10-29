using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record CanvasAnonymousItem(string Token, CanvasRect VisibleBoundsDip);
public sealed record CanvasContentObservation(long TargetVersion, string? DesignDigest, long DocumentVersion,
    long LayoutGeneration, CanvasRect AreaDip, int TotalItems, bool Truncated, IReadOnlyList<CanvasAnonymousItem> Items);
public sealed record CanvasContentContext(string Status, CanvasContentObservation? Observation = null);

public static class CanvasContentContextPolicy
{
    public static CanvasContentContext Match(CanvasContentObservation? observed, long version, string? nativeDigest, CanvasRect expectedArea)
    {
        if (observed is null) return new("unavailable");
        static bool Near(double a, double b) => double.IsFinite(a) && double.IsFinite(b) && Math.Abs(a - b) < .1;
        bool sameArea = Near(expectedArea.X, observed.AreaDip.X) && Near(expectedArea.Y, observed.AreaDip.Y) &&
            Near(expectedArea.Width, observed.AreaDip.Width) && Near(expectedArea.Height, observed.AreaDip.Height);
        if (observed.TargetVersion != version || observed.DesignDigest != nativeDigest || !sameArea) return new("stale");
        return new("current", observed);
    }
}
