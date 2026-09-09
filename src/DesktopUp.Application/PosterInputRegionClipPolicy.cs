namespace DesktopUp.Application;

public static class PosterInputRegionClipPolicy
{
    // Posture animation legitimately passes below the normal expanded minimum height.
    // No input region (including the scroll rail) may extend past that presented frame.
    public static IReadOnlyList<PosterInputRegion> Clip(int width, int height, IEnumerable<PosterInputRegion> regions)
    {
        var result = new List<PosterInputRegion>();
        foreach (var region in regions)
        {
            var r = region.Bounds;
            long x = Math.Max(0, r.X), y = Math.Max(0, r.Y);
            long right = Math.Min(width, (long)r.X + r.Width), bottom = Math.Min(height, (long)r.Y + r.Height);
            if (right > x && bottom > y) result.Add(region with { Bounds = new((int)x, (int)y, (int)(right - x), (int)(bottom - y)) });
        }
        return result.AsReadOnly();
    }
}
