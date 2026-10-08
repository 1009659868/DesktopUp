using DesktopUp.Domain;
namespace DesktopUp.Application;

public static class TitleCloudInputGeometry
{
    // Explicit settings may update size while the control-center slider owns an
    // external press. The host still rejects its OWN capture/press/menu/transaction.
    // Ambient wind may never use this exception. Unknown protection fails closed.
    public static bool AllowsProtection(AutoPostureProtection protection, bool sizeChange) =>
        (protection & ~(sizeChange ? AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture : AutoPostureProtection.None)) == 0;
    // Rasterize only inside the transformed solid grip bands, not their bounding boxes.
    // Intersections at BOTH row edges conservatively retain complete physical pixels.
    public static IReadOnlyList<DesktopPixelRect> Resolve(double width, bool collapsed,
        TitleCloudPose pose, double scaleX, double scaleY)
    {
        if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0 || scaleX > 8 || scaleY > 8)
            throw new ArgumentOutOfRangeException(nameof(scaleX));
        pose = pose.Fit(width);
        var title = PosterTitleChromePolicy.Resolve(width, collapsed, PosterTitleChromeKind.Cloud);
        int w = (int)Math.Round(width * scaleX), h = (int)Math.Ceiling(title.Height * scaleY);
        var rows = new List<(int Left, int Right)>[h];
        foreach (var b in title.GripBands)
        {
            (double X, double Y) P(double x, double y)
            { var p = pose.Transform(x, y, width); return (p.X * scaleX, p.Y * scaleY); }
            var corners = new[] { P(b.X, b.Y), P(b.Right, b.Y), P(b.Right, b.Bottom), P(b.X, b.Bottom) };
            (double Left, double Right) Span(double y)
            {
                double left = double.PositiveInfinity, right = double.NegativeInfinity;
                for (int i = 0; i < 4; i++)
                {
                    var a = corners[i]; var z = corners[(i + 1) % 4];
                    if (y < Math.Min(a.Y, z.Y) - 1e-7 || y > Math.Max(a.Y, z.Y) + 1e-7) continue;
                    if (Math.Abs(a.Y - z.Y) < 1e-7) { left = Math.Min(left, Math.Min(a.X, z.X)); right = Math.Max(right, Math.Max(a.X, z.X)); }
                    else { double x = a.X + (z.X - a.X) * (y - a.Y) / (z.Y - a.Y); left = Math.Min(left, x); right = Math.Max(right, x); }
                }
                return (left, right);
            }
            int first = Math.Max(0, (int)Math.Ceiling(corners.Min(p => p.Y)));
            int last = Math.Min(h, (int)Math.Floor(corners.Max(p => p.Y)));
            for (int y = first; y < last; y++)
            {
                var a = Span(y); var z = Span(y + 1);
                if (!double.IsFinite(a.Left) || !double.IsFinite(z.Left)) continue;
                int left = Math.Max(0, (int)Math.Ceiling(Math.Max(a.Left, z.Left) - 1e-7));
                int right = Math.Min(w, (int)Math.Floor(Math.Min(a.Right, z.Right) + 1e-7));
                if (right > left) (rows[y] ??= []).Add((left, right));
            }
        }
        var result = new List<DesktopPixelRect>();
        for (int y = 0; y < h; y++)
        {
            if (rows[y] is not { Count: > 0 } spans) continue;
            spans.Sort((a, b) => a.Left.CompareTo(b.Left));
            var current = spans[0];
            foreach (var span in spans.Skip(1))
                if (span.Left <= current.Right) current = (current.Left, Math.Max(current.Right, span.Right));
                else { result.Add(new(current.Left, y, current.Right - current.Left, 1)); current = span; }
            result.Add(new(current.Left, y, current.Right - current.Left, 1));
        }
        return result.AsReadOnly();
    }

    public static bool IsTitleOnlyReplacement(PosterInputLayoutSnapshot before, PosterInputLayoutSnapshot after) =>
        after.Revision > before.Revision && before.FrameWidth == after.FrameWidth && before.FrameHeight == after.FrameHeight &&
        before.OuterCornerRadius == after.OuterCornerRadius &&
        before.Regions.Where(r => r.Kind != PosterInputTargetKind.TitleBar)
            .SequenceEqual(after.Regions.Where(r => r.Kind != PosterInputTargetKind.TitleBar));
}
