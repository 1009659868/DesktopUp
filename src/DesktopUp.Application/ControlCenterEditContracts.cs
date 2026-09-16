using System.Globalization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ControlCenterEditGroup { None, Geometry, Presentation }

// Text remains a draft (including invalid input); this is not a PosterDefinition.
public sealed record PosterGeometryText(string X, string Y, string Width, string ExpandedHeight)
{
    public static PosterGeometryText From(PosterDefinition p) => new(Format(p.FallbackXDip),
        Format(p.FallbackYDip), Format(p.WidthDip), Format(p.ExpandedHeightDip));
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public bool TryRead(out PosterGeometryIntent? value)
    {
        value = null;
        if (!Read(X, out double x) || !Read(Y, out double y) ||
            !Read(Width, out double width) || !Read(ExpandedHeight, out double height)) return false;
        value = new(x, y, width, height);
        return true;
    }
    private static bool Read(string text, out double value)
    {
        value = 0;
        return text is { Length: > 0 and <= 64 } &&
            double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
}

public sealed record PosterGeometryIntent(double X, double Y, double Width, double ExpandedHeight);

// A request for the existing spatial owner, NOT an executable state command.
// The adapter must acquire current environment/effective geometry and produce a full
// spatial proof, render/verify, and commit once. This type has no ToCommand shortcut.
public sealed record ControlCenterGeometryRequest(
    string RequestId, PosterSurfaceProjection Basis, long ObservedRevision,
    StateReadSet ReadSet, PosterGeometryIntent Desired)
{
    public ControlCenterGeometryBasis? ActualBasis { get; init; }
    public PosterGeometryFields ChangedFields { get; init; } = PosterGeometryFields.All;
    public bool ReadsCurrent(DesktopUpProjection projection) =>
        projection.CatalogVersion == ReadSet.CatalogVersion &&
        projection.Posters.Count == ReadSet.Posters.Count &&
        ReadSet.Posters.All(read => projection.PostersById.TryGetValue(read.Key, out var p) &&
            p.EntityVersion == read.Value);
}
