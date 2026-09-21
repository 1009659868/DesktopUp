using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopUp.Domain;

public enum ContentLayoutMode { Automatic, Anchored, Manual }
public enum ContentImageFit { Contain, Cover }
public enum ManualLayoutExit { KeepOrder, ConvertToAnchors, RestoreAutomatic }
public sealed record ContentLayoutRegion(string Id, int Order, int Column = 0, int Columns = 0, int Row = 0);
public sealed record ContentReservedSpace(int Column, int Row, int ColumnSpan, int RowSpan);
public sealed record ContentPlacement(string ItemId, int Order, int ColumnSpan = 4, int RowSpan = 4)
{
    public long Version { get; init; } = 1;
    public string RegionId { get; init; } = "main";
    public int? Column { get; init; }
    public int? Row { get; init; }
    public bool FixedPosition { get; init; }
    public bool ManualSize { get; init; }
    public int? PinnedIndex { get; init; }
    public ContentImageFit ImageFit { get; init; }
}

// Intent only. Resolved coordinates, scroll, pixels and native handles never enter this document.
public sealed record ContentLayoutDocument(string PosterId, IReadOnlyList<ContentPlacement> Placements)
{
    public int SchemaVersion { get; init; } = 1;
    public string EngineId { get; init; } = "span-grid";
    public int EngineVersion { get; init; } = 1;
    public long DocumentVersion { get; init; } = 1;
    public long ItemSetVersion { get; init; } = 1;
    public ContentLayoutMode Mode { get; init; }
    public double BaseCellDip { get; init; } = 24;
    public double BaseGapDip { get; init; } = 6;
    public int MaximumColumns { get; init; } = 64;
    public bool ShowBaselineGrid { get; init; } = true;
    public bool ShowSpanLabels { get; init; } = true;
    public IReadOnlyList<ContentLayoutRegion> Regions { get; init; } = Array.AsReadOnly(new[] { new ContentLayoutRegion("main", 0) });
    public IReadOnlyList<ContentReservedSpace> ReservedSpaces { get; init; } = Array.Empty<ContentReservedSpace>();
}

public sealed record ContentItemView(string ItemId, string Name, string Monogram, uint Color, string? AssetKey = null);
public interface IContentItemViewSource
{
    long Version { get; }
    IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document);
}

public readonly record struct ContentRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public bool Contains(double x, double y) => !IsEmpty && x >= X && x < Right && y >= Y && y < Bottom;
    public bool Intersects(ContentRect other) => !IsEmpty && !other.IsEmpty && X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
    public ContentRect Intersect(ContentRect other) => new(Math.Max(X, other.X), Math.Max(Y, other.Y),
        Math.Max(0, Math.Min(Right, other.Right) - Math.Max(X, other.X)), Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y)));
}

public sealed record ContentScrollAnchor(string ItemId, double InsideOffsetDip, int PreviousReadOrder);
public sealed record ContentLayoutViewport(double WidthDip, double HeightDip, double ScrollXDip = 0, double ScrollYDip = 0,
    long Generation = 0, double Scale = 1, double Spacing = 1, long ThemeMetricsVersion = 1);
public sealed record ContentResolvedPlacement(string ItemId, int SourceIndex, int ReadOrder, int Column, int Row,
    int ColumnSpan, int RowSpan, ContentRect Bounds, ContentRect Clip, string? Diagnostic = null)
{
    public int RegionColumn { get; init; }
    public int RegionRow { get; init; }
}
public sealed record ContentLayoutSnapshot(string PosterId, long DocumentVersion, long ItemSetVersion,
    long ViewportGeneration, long ThemeMetricsVersion, int Columns, double CellDip, double GapDip,
    double ExtentWidthDip, double ExtentHeightDip, double ScrollXDip, double ScrollYDip,
    IReadOnlyList<ContentResolvedPlacement> Items, IReadOnlyList<string> Diagnostics)
{
    public ContentResolvedPlacement? HitTest(double x, double y) => Items.FirstOrDefault(i => i.Clip.Contains(x, y));
    public ContentScrollAnchor? Anchor() => Items.Where(i => i.Bounds.Bottom > ScrollYDip)
        .OrderBy(i => i.Bounds.Y).ThenBy(i => i.ReadOrder).Select(i => new ContentScrollAnchor(i.ItemId, ScrollYDip - i.Bounds.Y, i.ReadOrder)).FirstOrDefault();
}

public static class ContentLayoutPolicy
{
    public const int MaximumItems = 1000, MaximumSpan = 32, MaximumColumns = 64, MaximumRow = 65535;
    public const int MaximumJsonBytes = 1048576, MaximumRegions = 8;
    public const double MinimumReadableCardDip = 48;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    public static string? Validate(ContentLayoutDocument? d)
    {
        if (d is null || d.SchemaVersion != 1 || d.EngineId != "span-grid" || d.EngineVersion != 1)
            return "Unsupported content layout engine/schema; original document is retained read-only.";
        if (string.IsNullOrWhiteSpace(d.PosterId) || d.PosterId.Length > 128 || d.DocumentVersion < 1 || d.ItemSetVersion < 1 || !Enum.IsDefined(d.Mode))
            return "Invalid layout identity/version/mode.";
        if (!double.IsFinite(d.BaseCellDip) || d.BaseCellDip is < 16 or > 96 || !double.IsFinite(d.BaseGapDip) || d.BaseGapDip is < 0 or > 40 || d.MaximumColumns is < 2 or > MaximumColumns)
            return "Invalid baseline grid metrics.";
        if (d.Placements is null || d.Placements.Count > MaximumItems || d.Regions is null || d.Regions.Count is < 1 or > MaximumRegions)
            return "Content layout capacity exceeded.";
        var regions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in d.Regions)
            if (r is null || string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 64 || !regions.Add(r.Id) || r.Order < 0 || r.Column is < 0 or >= MaximumColumns || r.Columns is < 0 or > MaximumColumns || r.Column + r.Columns > MaximumColumns || r.Row is < 0 or > MaximumRow)
                return "Invalid or duplicate region.";
        if (d.ReservedSpaces is null || d.ReservedSpaces.Count > 32 || d.ReservedSpaces.Any(s => s is null ||
            s.Column is < 0 or >= MaximumColumns || s.Row is < 0 or > MaximumRow || s.ColumnSpan is < 1 or > MaximumColumns ||
            s.RowSpan is < 1 or > MaximumRow || s.Column + s.ColumnSpan > MaximumColumns || s.Row + s.RowSpan > MaximumRow))
            return "Invalid reserved space.";
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var pins = new HashSet<int>();
        foreach (var p in d.Placements)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.ItemId) || p.ItemId.Length > 192 || !p.ItemId.StartsWith(d.PosterId + ":", StringComparison.Ordinal) || !ids.Add(p.ItemId) || p.Version < 1 || p.Order < 0 || !regions.Contains(p.RegionId) || !Enum.IsDefined(p.ImageFit))
                return "Invalid, foreign or duplicate placement identity.";
            if (p.ColumnSpan is < 1 or > MaximumSpan || p.RowSpan is < 1 or > MaximumSpan || p.Column is < 0 or >= MaximumColumns || p.Row is < 0 or > MaximumRow || p.Column.HasValue != p.Row.HasValue || p.FixedPosition && !p.Column.HasValue)
                return "Invalid span or logical position.";
            if (p.PinnedIndex is { } pin && (pin < 1 || pin > d.Placements.Count || !pins.Add(pin))) return "Invalid or duplicate order anchor.";
        }
        return null;
    }
    public static ContentLayoutDocument Freeze(ContentLayoutDocument d) => d with
    {
        Placements = Array.AsReadOnly(d.Placements.ToArray()), Regions = Array.AsReadOnly(d.Regions.ToArray()),
        ReservedSpaces = Array.AsReadOnly(d.ReservedSpaces.ToArray())
    };
    public static string Serialize(ContentLayoutDocument d)
    {
        if (Validate(d) is { } error) throw new ArgumentException(error);
        var text = JsonSerializer.Serialize(d, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaximumJsonBytes) throw new ArgumentException("Layout JSON exceeds capacity.");
        return text;
    }
    public static bool TryRead(string? text, string posterId, out ContentLayoutDocument? document, out string? error)
    {
        document = null; error = null;
        if (text is null) { document = ContentLayoutSamples.Equal(posterId); return true; }
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(text) > MaximumJsonBytes) throw new ArgumentException("Layout JSON exceeds capacity.");
            using (var tree = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 24 })) RejectDuplicates(tree.RootElement);
            var d = JsonSerializer.Deserialize<ContentLayoutDocument>(text, Json);
            error = Validate(d);
            if (error is null && d!.PosterId != posterId) error = "Layout owner does not match poster.";
            if (error is not null) return false;
            document = Freeze(d!); return true;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException or OverflowException)
        { error = e.Message; return false; }
    }
    public static ContentLayoutDocument Remap(ContentLayoutDocument d, string newPosterId) => Freeze(d with
    {
        PosterId = newPosterId, DocumentVersion = 1, ItemSetVersion = 1,
        Placements = d.Placements.Select((p, i) => p with { ItemId = newPosterId + ":item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Version = 1 }).ToArray()
    });
    private static void RejectDuplicates(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject()) { if (!names.Add(p.Name)) throw new ArgumentException("Duplicate layout member."); RejectDuplicates(p.Value); }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var e in node.EnumerateArray()) RejectDuplicates(e);
    }
}

public static class ContentLayoutSamples
{
    public static ContentLayoutDocument Equal(string posterId, int count = 6) => new(posterId,
        Array.AsReadOnly(Enumerable.Range(0, count).Select(i => new ContentPlacement(posterId + ":item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), i)).ToArray()));
    public static ContentLayoutDocument Illustration(string id) => Sample(id, 32,
        [(0,0,8,8),(0,8,6,6),(6,8,6,6),(0,14,4,4),(4,14,4,4),(8,14,4,4),(0,18,4,4),(4,18,4,4),(8,18,4,4)], ContentImageFit.Contain)
        with { ReservedSpaces = Array.AsReadOnly(new[] { new ContentReservedSpace(8, 0, 4, 8) }) };
    public static ContentLayoutDocument Covers(string id) => Sample(id, 32,
        [(0,0,12,6),(0,6,6,5),(6,6,6,5),(0,11,4,4),(4,11,4,4),(8,11,4,4),(0,15,3,3),(3,15,3,3),(6,15,3,3),(9,15,3,3)], ContentImageFit.Cover);
    public static ContentLayoutDocument Landscape(string id)
    {
        var d = Sample(id, 40, [(0,0,5,8),(0,0,4,4),(4,0,3,4),(0,4,2,2),(2,4,2,2),(4,4,3,2),(0,6,4,2),(4,6,3,2)], ContentImageFit.Cover);
        return d with { Regions = Array.AsReadOnly(new[] { new ContentLayoutRegion("main",0,0,5), new ContentLayoutRegion("side",1,5,7) }),
            Placements = Array.AsReadOnly(d.Placements.Select((p,i) => i == 0 ? p : p with { RegionId = "side" }).ToArray()) };
    }
    private static ContentLayoutDocument Sample(string id, double cell, (int x,int y,int w,int h)[] slots, ContentImageFit fit) =>
        new(id, Array.AsReadOnly(slots.Select((s,i) => new ContentPlacement(id + ":item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), i,s.w,s.h)
        { Column=s.x, Row=s.y, ManualSize=true, ImageFit=fit }).ToArray()))
        { Mode=ContentLayoutMode.Manual, MaximumColumns=12, BaseCellDip=cell };
}

// L-stage source only: no files, Shell activity, launch statistics or network assets.
public sealed class DemoContentItemViewSource : IContentItemViewSource
{
    public long Version => 1;
    private static readonly string[] Names = ["AFTERLIGHT", "GHOST SIGNAL", "IRON VEIL", "LAST ORBIT", "SABLE RUN", "HOLLOW CROWN"];
    private static readonly string[] Marks = ["AL", "GS", "IV", "LO", "SR", "HC"];
    private static readonly uint[] Colors = [0xFF8456BE,0xFF449AAB,0xFFB14549,0xFFB57630,0xFF4167AD,0xFF408F71];
    public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document) => Array.AsReadOnly(document.Placements.Select((p,i) =>
        new ContentItemView(p.ItemId, Names[i%6] + (i<6 ? "" : " " + (i+1)), Marks[i%6], Colors[i%6])).ToArray());
}
