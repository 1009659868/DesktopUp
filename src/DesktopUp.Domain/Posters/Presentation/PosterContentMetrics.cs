namespace DesktopUp.Domain;

// One renderer/solver contract. A row includes its label and internal spacing, even for an empty list.
public sealed record PosterContentMetrics(string Version, double ReservedTopDip, double CompleteItemRowDip, double ReservedBottomDip)
{
    public const double MetaHeightDip = 130;
    public const double ItemsTopInsetDip = 10;
    public const double FooterHeightDip = 42;
    public const double ItemRowHeightDip = 118;
    public static PosterContentMetrics Current { get; } = new("night-ops-v1",
        PosterTitleBarLayoutPolicy.HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip, ItemRowHeightDip, FooterHeightDip);

    public int MinimumPixels(uint dpi)
    {
        if (string.IsNullOrWhiteSpace(Version) || dpi == 0 ||
            !double.IsFinite(ReservedTopDip) || !double.IsFinite(CompleteItemRowDip) || !double.IsFinite(ReservedBottomDip) ||
            ReservedTopDip <= 0 || CompleteItemRowDip <= 0 || ReservedBottomDip < 0)
            throw new ArgumentException("Invalid content metrics.");
        // Round each occupied section outwards; never claim that a clipped label is a complete row.
        return checked((int)(Math.Ceiling(ReservedTopDip * dpi / 96d) +
            Math.Ceiling(CompleteItemRowDip * dpi / 96d) + Math.Ceiling(ReservedBottomDip * dpi / 96d)));
    }
}
