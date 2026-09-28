using System.Text.Json.Serialization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// File-local membership keys only: no machine poster IDs, document/CAS versions or source paths.
public sealed record PortableContentPlacement(string Key, int Order, int ColumnSpan, int RowSpan,
    string RegionId, int? Column, int? Row, bool FixedPosition, bool ManualSize, int? PinnedIndex, ContentImageFit ImageFit)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ContentAutomaticPlacementState? SavedAutomaticState { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ContentAutomaticSizeBasis? AutomaticSizeBasis { get; init; }
}
public sealed record PortableContentLayout(int SchemaVersion, string EngineId, int EngineVersion,
    ContentLayoutMode Mode, double BaseCellDip, double BaseGapDip, int MaximumColumns,
    bool ShowBaselineGrid, bool ShowSpanLabels, IReadOnlyList<ContentLayoutRegion> Regions,
    IReadOnlyList<PortableContentPlacement> Placements)
{
    public IReadOnlyList<ContentReservedSpace> ReservedSpaces { get; init; } = Array.Empty<ContentReservedSpace>();

    /// <summary>v3 carries the single scroll axis. Null means the member was absent: v2 archives are
    /// imported as vertical, while an archive claiming version three must state its axis.</summary>
    public ContentFlowAxis? FlowAxis { get; init; }
    /// <summary>Optional in v3. Absent means heat-driven sizing stays off, so importing an older
    /// archive never silently starts resizing manual or pinned cards.</summary>
    public bool? AllowHeatDrivenSize { get; init; }
    /// <summary>Optional in v3. Absent means loop scrolling stays off; the switch is independent of
    /// the flow axis and never changes squeezing or wrapping.</summary>
    public bool? LoopScroll { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ContentLayoutMode? SavedAutomaticMode { get; init; }
}

public static class PortableContentLayoutCodec
{
    public static PortableContentLayout Export(ContentLayoutDocument document)
    {
        _ = ContentLayoutCapabilities.Inspect(document);
        return new(
            document.SchemaVersion, document.EngineId, document.EngineVersion, document.Mode,
            document.BaseCellDip, document.BaseGapDip, document.MaximumColumns,
            document.ShowBaselineGrid, document.ShowSpanLabels, Array.AsReadOnly(document.Regions.ToArray()),
            Array.AsReadOnly(document.Placements.Select((p, i) => new PortableContentPlacement(
                "item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), p.Order, p.ColumnSpan, p.RowSpan,
                p.RegionId, p.Column, p.Row, p.FixedPosition, p.ManualSize, p.PinnedIndex, p.ImageFit)
                { SavedAutomaticState = p.SavedAutomaticState, AutomaticSizeBasis = p.AutomaticSizeBasis }).ToArray()))
        {
            // Both explicit keep-out intent and decorative metadata survive portable snapshots.
            // Archive capability does not grant permission to instantiate or edit this layout.
            ReservedSpaces = Array.AsReadOnly(document.ReservedSpaces.ToArray()),
            FlowAxis = document.FlowAxis,
            AllowHeatDrivenSize = document.AllowHeatDrivenSize,
            LoopScroll = document.LoopScroll,
            SavedAutomaticMode = document.SavedAutomaticMode
        };
    }

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
                ManualSize = p.ManualSize, PinnedIndex = p.PinnedIndex, ImageFit = p.ImageFit,
                SavedAutomaticState = p.SavedAutomaticState, AutomaticSizeBasis = p.AutomaticSizeBasis
            }).ToArray()))
        {
            SchemaVersion = layout.SchemaVersion, EngineId = layout.EngineId, EngineVersion = layout.EngineVersion,
            Mode = layout.Mode, BaseCellDip = layout.BaseCellDip, BaseGapDip = layout.BaseGapDip,
            MaximumColumns = layout.MaximumColumns, ShowBaselineGrid = layout.ShowBaselineGrid,
            ShowSpanLabels = layout.ShowSpanLabels, Regions = Array.AsReadOnly(layout.Regions.ToArray()),
            ReservedSpaces = Array.AsReadOnly(layout.ReservedSpaces.ToArray()),
            FlowAxis = layout.FlowAxis ?? ContentFlowAxis.Vertical,
            AllowHeatDrivenSize = layout.AllowHeatDrivenSize ?? false,
            LoopScroll = layout.LoopScroll ?? false,
            SavedAutomaticMode = layout.SavedAutomaticMode
        };
        // Full strict domain validation and the independent 1 MiB document budget.
        _ = ContentLayoutCapabilities.Inspect(document);
        return document;
    }
}
