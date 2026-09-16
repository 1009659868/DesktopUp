using DesktopUp.Domain;

namespace DesktopUp.Application;

[Flags]
public enum PosterGeometryFields { None = 0, X = 1, Y = 2, Width = 4, Height = 8, All = 15 }

// One generation-checked read of the real surface. Fallback coordinates never initialize this editor.
public sealed record ControlCenterGeometryBasis(string PosterId, long EntityVersion, long LayoutGeneration,
    SpatialEnvironment Environment, SpatialRect Bounds)
{
    public bool Matches(PosterSurfaceProjection target) => PosterId == target.Poster.PosterId &&
        EntityVersion == target.EntityVersion && LayoutGeneration == target.LayoutGeneration &&
        Environment.IsValid && Bounds.IsValid && Environment.WorkArea.Contains(Bounds);

    public PosterGeometryText Text(PosterDefinition poster) => PosterGeometryText.From(poster with
    {
        FallbackXDip = (Bounds.X - (double)Environment.WorkArea.X) * 96 / Environment.DpiX,
        FallbackYDip = (Bounds.Y - (double)Environment.WorkArea.Y) * 96 / Environment.DpiY
    });

    public bool Matches(SpatialSnapshot scene, PosterSurfaceProjection target) => Matches(target) &&
        Environment == scene.Environment && scene.Posters.Any(p => p.PosterId == PosterId && p.Bounds == Bounds &&
            p.LayoutGeneration == LayoutGeneration);

    public PosterGeometryIntent Resolve(PosterDefinition poster, PosterGeometryIntent desired, PosterGeometryFields fields)
    {
        Text(poster).TryRead(out var current);
        return new(fields.HasFlag(PosterGeometryFields.X) ? desired.X : current!.X,
            fields.HasFlag(PosterGeometryFields.Y) ? desired.Y : current!.Y,
            fields.HasFlag(PosterGeometryFields.Width) ? desired.Width : poster.WidthDip,
            fields.HasFlag(PosterGeometryFields.Height) ? desired.ExpandedHeight : poster.ExpandedHeightDip);
    }

    public static PosterGeometryFields Changed(PosterGeometryText basis, PosterGeometryText desired)
    {
        if (!basis.TryRead(out var a) || !desired.TryRead(out var b)) return PosterGeometryFields.None;
        return (a!.X != b!.X ? PosterGeometryFields.X : 0) | (a.Y != b.Y ? PosterGeometryFields.Y : 0) |
            (a.Width != b.Width ? PosterGeometryFields.Width : 0) | (a.ExpandedHeight != b.ExpandedHeight ? PosterGeometryFields.Height : 0);
    }
}
