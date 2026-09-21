using DesktopUp.Domain;

namespace DesktopUp.Application;

// File-local membership keys only: no machine poster IDs, document/CAS versions or source paths.
public sealed record PortableContentPlacement(string Key, int Order, int ColumnSpan, int RowSpan,
    string RegionId, int? Column, int? Row, bool FixedPosition, bool ManualSize, int? PinnedIndex, ContentImageFit ImageFit);
public sealed record PortableContentLayout(int SchemaVersion, string EngineId, int EngineVersion,
    ContentLayoutMode Mode, double BaseCellDip, double BaseGapDip, int MaximumColumns,
    bool ShowBaselineGrid, bool ShowSpanLabels, IReadOnlyList<ContentLayoutRegion> Regions,
    IReadOnlyList<PortableContentPlacement> Placements)
{
    public IReadOnlyList<ContentReservedSpace> ReservedSpaces { get; init; } = Array.Empty<ContentReservedSpace>();
}

public static class PortableContentLayoutCodec
{
    public static PortableContentLayout Export(ContentLayoutDocument document) => new(
        document.SchemaVersion, document.EngineId, document.EngineVersion, document.Mode,
        document.BaseCellDip, document.BaseGapDip, document.MaximumColumns,
        document.ShowBaselineGrid, document.ShowSpanLabels, Array.AsReadOnly(document.Regions.ToArray()),
        Array.AsReadOnly(document.Placements.Select((p, i) => new PortableContentPlacement(
            "item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), p.Order, p.ColumnSpan, p.RowSpan,
            p.RegionId, p.Column, p.Row, p.FixedPosition, p.ManualSize, p.PinnedIndex, p.ImageFit)).ToArray()))
        { ReservedSpaces = Array.AsReadOnly(document.ReservedSpaces.ToArray()) };

    public static ContentLayoutDocument Import(PortableContentLayout layout, string posterId)
    {
        if (layout is null || layout.Placements is null || layout.Regions is null || layout.ReservedSpaces is null ||
            layout.Placements.Any(p => p is null || string.IsNullOrWhiteSpace(p.Key) || p.Key.Length > 64 ||
                p.Key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))))
            throw new ArgumentException("Invalid portable content membership key.");
        var document = new ContentLayoutDocument(posterId, Array.AsReadOnly(layout.Placements.Select(p =>
            new ContentPlacement(posterId + ":" + p.Key, p.Order, p.ColumnSpan, p.RowSpan)
            {
                RegionId = p.RegionId, Column = p.Column, Row = p.Row, FixedPosition = p.FixedPosition,
                ManualSize = p.ManualSize, PinnedIndex = p.PinnedIndex, ImageFit = p.ImageFit
            }).ToArray()))
        {
            SchemaVersion = layout.SchemaVersion, EngineId = layout.EngineId, EngineVersion = layout.EngineVersion,
            Mode = layout.Mode, BaseCellDip = layout.BaseCellDip, BaseGapDip = layout.BaseGapDip,
            MaximumColumns = layout.MaximumColumns, ShowBaselineGrid = layout.ShowBaselineGrid,
            ShowSpanLabels = layout.ShowSpanLabels, Regions = Array.AsReadOnly(layout.Regions.ToArray()),
            ReservedSpaces = Array.AsReadOnly(layout.ReservedSpaces.ToArray())
        };
        // Full strict domain validation and the independent 1 MiB document budget.
        _ = ContentLayoutPolicy.Serialize(document);
        ContentLayoutCommandPolicy.ValidateAuthoring(document);
        return document;
    }
}
