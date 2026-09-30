namespace DesktopUp.Domain;

// Shared geometry for the only native title surface. Decoration never owns input.
public enum PosterTitleChromeKind { Basic, Cloud }
public enum PosterTitleChromeTarget { Desktop, Drag, Collapse, More }
public readonly record struct TitleChromeRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double x, double y) => double.IsFinite(x) && double.IsFinite(y) &&
        x >= X && x < Right && y >= Y && y < Bottom;
}

public sealed record PosterTitleChromeLayout(
    PosterTitleChromeKind Kind, double Width, double Height, TitleChromeRect Name,
    TitleChromeRect Collapse, TitleChromeRect More, IReadOnlyList<TitleChromeRect> GripBands)
{
    // Button priority remains stable; lock/capture/gesture decisions belong to the original owner.
    public PosterTitleChromeTarget HitTest(double x, double y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return PosterTitleChromeTarget.Desktop;
        if (Collapse.Contains(x, y)) return PosterTitleChromeTarget.Collapse;
        if (More.Contains(x, y)) return PosterTitleChromeTarget.More;
        return GripBands.Any(b => b.Contains(x, y)) ? PosterTitleChromeTarget.Drag : PosterTitleChromeTarget.Desktop;
    }
}

public static class PosterTitleChromePolicy
{
    public const double ExpandedCloudHeightDip = 84;
    // Only validated, installed module capabilities can choose a style, not imported executable code.
    public static PosterTitleChromeLayout Resolve(double width, bool collapsed,
        PosterTitleChromeKind requested, bool capabilityAvailable = true)
    {
        if (!double.IsFinite(width) || width < PosterFrameGeometryPolicy.MinimumWidthDip)
            throw new ArgumentOutOfRangeException(nameof(width));
        bool cloud = capabilityAvailable && requested == PosterTitleChromeKind.Cloud;
        if (!cloud)
        {
            var old = PosterTitleBarLayoutPolicy.Resolve(width);
            return new(PosterTitleChromeKind.Basic, width, old.HeaderHeightDip,
                new(14, 12, old.CollapseAction.XDip - 22, 24),
                new(old.CollapseAction.XDip, old.CollapseAction.YDip, old.CollapseAction.SizeDip, old.CollapseAction.SizeDip),
                new(old.MoreAction.XDip, old.MoreAction.YDip, old.MoreAction.SizeDip, old.MoreAction.SizeDip),
                Array.AsReadOnly(new[] { new TitleChromeRect(0, 0, width, old.HeaderHeightDip) }));
        }
        double height = collapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : ExpandedCloudHeightDip;
        // Conservative solid-body bands, not the whole cloud's bounding box or animated mist.
        // All slots remain at the same coordinates in both postures. Name has an ellipsis budget.
        var bands = CloudGripBands(width);
        return new(PosterTitleChromeKind.Cloud, width, height, new(20, 28, width - 100, 24),
            new(width - 70, 28, 24, 24), new(width - 40, 28, 24, 24), bands);
    }

    // One exact shape declaration, consumed by paint and conservative input strips alike.
    public static IReadOnlyList<TitleChromeRect> CloudEllipses(double width) => Array.AsReadOnly(new[]
    {
        new TitleChromeRect(width * .14, 9, width * .26, 36),
        new TitleChromeRect(width * .28, 2, width * .38, 42),
        new TitleChromeRect(width * .54, 11, width * .28, 38),
        new TitleChromeRect(4, 24, 36, 36), new TitleChromeRect(width - 40, 24, 36, 36)
    });
    public static TitleChromeRect CloudBody(double width) => new(22, 24, width - 44, 36);

    private static IReadOnlyList<TitleChromeRect> CloudGripBands(double width)
    {
        var bands = new List<TitleChromeRect> { new(22, 25, width - 44, 34) };
        foreach (var ellipse in CloudEllipses(width))
            for (double y = Math.Ceiling(ellipse.Y) + 1; y + 2 < ellipse.Bottom; y += 2)
            {
                double cy = ellipse.Y + ellipse.Height / 2, ry = ellipse.Height / 2;
                double dy = Math.Max(Math.Abs(y - cy), Math.Abs(y + 2 - cy));
                double half = ellipse.Width / 2 * Math.Sqrt(Math.Max(0, 1 - dy * dy / (ry * ry))) - 1;
                if (half > 0) bands.Add(new(ellipse.X + ellipse.Width / 2 - half, y, half * 2, 2));
            }
        return bands.AsReadOnly();
    }
}
