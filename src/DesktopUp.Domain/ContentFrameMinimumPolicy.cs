namespace DesktopUp.Domain;

/// <summary>Active outer-frame constraint only; never grants emergency browsing or writes authoring.</summary>
public sealed record ContentFrameMinimum(double WidthDip, double HeightDip)
{
    public (int Width, int Height) Pixels(uint dpiX, uint dpiY)
    {
        if (dpiX == 0 || dpiY == 0) throw new ArgumentOutOfRangeException(nameof(dpiX));
        return (checked((int)Math.Ceiling(WidthDip * dpiX / 96d)), checked((int)Math.Ceiling(HeightDip * dpiY / 96d)));
    }
}

public static class ContentFrameMinimumPolicy
{
    // Reserve the existing 1-DIP editing border on both sides, even when currently in daily use.
    // Entering editing must not itself clip an Item at the just-accepted outer minimum.
    public const double EditingBorderDip = 2;

    public static ContentFrameMinimum FromSnapshot(ContentLayoutSnapshot snapshot, double horizontalChromeDip, double verticalChromeDip)
    {
        if (!Enum.IsDefined(snapshot.FlowAxis) || !double.IsFinite(snapshot.MinimumCrossAxisDip) || snapshot.MinimumCrossAxisDip < 0 ||
            !double.IsFinite(horizontalChromeDip) || horizontalChromeDip < 0 || !double.IsFinite(verticalChromeDip) || verticalChromeDip < 0)
            throw new ArgumentException("Invalid content minimum or frame chrome.");
        if (snapshot.Items.Count == 0) return new(240, 240);
        // The cross axis must contain every complete Item. The scrolling axis needs only a
        // readable viewport plus chrome, not the old 240-DIP floor: changing axes must not
        // make a previously legal narrow cross axis impossible to restore after resizing.
        // No authored geometry or scroll policy changes when computing this constraint.
        double contentWidth = snapshot.FlowAxis == ContentFlowAxis.Vertical
            ? snapshot.MinimumCrossAxisDip : ContentLayoutPolicy.MinimumReadableCardDip;
        double contentHeight = snapshot.FlowAxis == ContentFlowAxis.HorizontalLoop
            ? snapshot.MinimumCrossAxisDip : ContentLayoutPolicy.MinimumReadableCardDip;
        return new(Math.Max(PosterFrameGeometryPolicy.MinimumWidthDip, contentWidth + horizontalChromeDip),
            Math.Max(PosterFrameGeometryPolicy.MinimumExpandedHeightDip, contentHeight + verticalChromeDip));
    }

    // Transaction validation recomputes from authoritative content/presentation; a UI cannot
    // weaken this witness by passing its own minimum. Unknown raw layouts stay untouched.
    public static bool TryForPoster(PosterDefinition poster, out ContentFrameMinimum? minimum, out string? error)
    {
        minimum = null;
        if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var doc, out error)) return false;
        try
        {
            var presentation = PosterPresentationPolicy.Resolve(poster);
            var preferences = presentation.Parameters.Preferences ?? new();
            double chromeX = PosterPresentationPolicy.HorizontalPaddingDip * 2 + EditingBorderDip;
            double chromeY = presentation.Metrics.ReservedTopDip + presentation.Metrics.ReservedBottomDip + EditingBorderDip;
            var viewport = new ContentLayoutViewport(Math.Max(0, poster.WidthDip - chromeX),
                Math.Max(0, poster.ExpandedHeightDip - chromeY), Scale: preferences.Scale, Spacing: preferences.Spacing);
            minimum = FromSnapshot(ContentLayoutSolver.Resolve(doc!, viewport), chromeX, chromeY);
            return true;
        }
        catch (ArgumentException ex) { error = ex.Message; return false; }
    }
}
