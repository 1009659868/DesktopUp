namespace DesktopUp.Domain;

public sealed class ContentLayoutException(string message) : ArgumentException(message);

/// <summary>One bounded occupancy engine in main/cross coordinates. Physical card dimensions and
/// authoring data never rotate or shrink when the flow axis or viewport changes.</summary>
public static class ContentLayoutSolver
{
    public const int MaximumSearchProbes = 4_194_304;
    public const double MaximumViewportDip = 1_000_000;

    public static ContentLayoutSnapshot Resolve(ContentLayoutDocument document, ContentLayoutViewport viewport,
        ContentScrollAnchor? anchor = null)
    {
        if (ContentLayoutPolicy.Validate(document) is { } error) throw new ContentLayoutException(error);
        ValidateViewport(viewport);
        document = ContentLayoutPolicy.Freeze(document);
        var axis = new Axis(document.FlowAxis == ContentFlowAxis.Vertical);
        double cell = document.BaseCellDip * viewport.Scale, gap = document.BaseGapDip * viewport.Spacing;
        double pitch = cell + gap;
        int readable = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + gap) / pitch);
        bool responsiveManual = document.Mode == ContentLayoutMode.Manual && document.Regions.Count == 1 &&
            document.Regions[0] is { Column: 0, Row: 0, Columns: 0 } &&
            document.Placements.All(p => p.Column.HasValue && !p.FixedPosition) &&
            document.ReservedSpaces.All(s => s.Kind == ContentReservedKind.Decorative);
        var ordered = OrderedPlacements(document);
        if (responsiveManual)
            ordered = ordered.OrderBy(p => axis.Main(p.Column!.Value, p.Row!.Value))
                .ThenBy(p => axis.Cross(p.Column!.Value, p.Row!.Value)).ThenBy(p => p.ItemId, StringComparer.Ordinal).ToArray();
        var candidates = ordered.Select(p => new Candidate(p, Math.Max(readable, p.ColumnSpan),
            Math.Max(readable, p.RowSpan), p.FixedPosition || document.Mode == ContentLayoutMode.Manual && p.Column.HasValue,
            p.ColumnSpan < readable || p.RowSpan < readable ? "readable-minimum" : null)).ToArray();
        int minimumCells = candidates.Select(c => axis.Cross(c.Width, c.Height)).DefaultIfEmpty(0).Max();
        if (minimumCells > document.MaximumColumns)
            throw new ContentLayoutException("The configured cross-axis capacity cannot accommodate the largest readable card.");
        double minimumDip = minimumCells == 0 ? 0 : minimumCells * pitch - gap;
        double availableDip = axis.Cross(viewport.WidthDip, viewport.HeightDip);
        int availableCells = Math.Clamp((int)Math.Floor((availableDip + gap + 1e-9) / pitch), 0, document.MaximumColumns);
        int crossCells = Math.Max(minimumCells, availableCells);
        bool onePerBand = minimumCells > 0 && availableDip <= minimumDip + 1e-7;
        var status = availableDip + 1e-7 < minimumDip
            ? ContentLayoutSpaceStatus.InsufficientCrossAxis : ContentLayoutSpaceStatus.Normal;
        var diagnostics = new List<string>();
        var occupancy = new Occupancy();
        var regions = document.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var sourceIndices = document.Placements.Select((p, i) => (p.ItemId, i)).ToDictionary(p => p.ItemId, p => p.i, StringComparer.Ordinal);
        var orders = ordered.Select((p, i) => (p.ItemId, i)).ToDictionary(p => p.ItemId, p => p.i, StringComparer.Ordinal);
        var result = new List<ContentResolvedPlacement>();
        int extentCross = 0, extentMain = 0;

        void Include(int cross, int main, int crossSpan, int mainSpan)
        {
            if (!Occupancy.InBounds(cross, main, crossSpan, mainSpan))
                throw new ContentLayoutException("Content intent exceeds the bounded main/cross coordinate range.");
            extentCross = Math.Max(extentCross, cross + crossSpan);
            extentMain = Math.Max(extentMain, main + mainSpan);
        }
        void LegacyCross(int required)
        {
            if (required > ContentLayoutPolicy.MaximumColumns)
                throw new ContentLayoutException("Legacy content constraints exceed the supported cross-axis capacity.");
            if (required > crossCells) status = ContentLayoutSpaceStatus.LegacyConstraintConflict;
        }
        foreach (var space in document.ReservedSpaces.Where(s => s.Kind == ContentReservedKind.Explicit))
        {
            int cross = axis.Cross(space.Column, space.Row), main = axis.Main(space.Column, space.Row);
            int width = axis.Cross(space.ColumnSpan, space.RowSpan), height = axis.Main(space.ColumnSpan, space.RowSpan);
            Include(cross, main, width, height); LegacyCross(cross + width);
            occupancy.Add(cross, main, width, height);
        }
        foreach (var region in document.Regions)
        {
            int start = axis.Cross(region.Column, region.Row);
            LegacyCross(start + (axis.Vertical ? region.Columns : 0));
        }
        // Ordinary manual projection may adapt legal authoring, but must never repair overlapping
        // or out-of-capacity authored rectangles into a deceptively valid new document.
        if (responsiveManual)
        {
            var authored = new Occupancy();
            foreach (var c in candidates)
            {
                int cross = axis.Cross(c.Item.Column!.Value, c.Item.Row!.Value);
                int main = axis.Main(c.Item.Column.Value, c.Item.Row.Value);
                int width = axis.Cross(c.Width, c.Height), height = axis.Main(c.Width, c.Height);
                if (!authored.Fits(cross, main, width, height))
                    throw new ContentLayoutException("Invalid or overlapping manual authoring: " + c.Item.ItemId);
                authored.Add(cross, main, width, height);
            }
        }
        // Future regions' authored obstacles must be installed before any flowing peer.
        foreach (var c in candidates.Where(c => c.Positioned && !responsiveManual))
        {
            var r = regions[c.Item.RegionId];
            int x = checked(r.Column + c.Item.Column!.Value), y = checked(r.Row + c.Item.Row!.Value);
            int cross = axis.Cross(x, y), main = axis.Main(x, y);
            int width = axis.Cross(c.Width, c.Height), height = axis.Main(c.Width, c.Height);
            if (r.Columns > 0 && c.Item.Column.Value + c.Width > r.Columns ||
                !occupancy.Fits(cross, main, width, height))
                throw new ContentLayoutException("Authored position conflicts with content, reserved space or region: " + c.Item.ItemId);
            Include(cross, main, width, height); LegacyCross(cross + width);
            if (onePerBand && cross != 0) status = ContentLayoutSpaceStatus.LegacyConstraintConflict;
            occupancy.Add(cross, main, width, height);
        }
        foreach (var region in document.Regions.OrderBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var members = candidates.Where(c => c.Item.RegionId == region.Id).ToArray();
            int startCross = axis.Cross(region.Column, region.Row), startMain = axis.Main(region.Column, region.Row);
            int regionCross = axis.Vertical && region.Columns > 0 ? region.Columns : Math.Max(0, crossCells - startCross);
            int needed = members.Select(c => (c.Positioned && !responsiveManual ? axis.Cross(c.Item.Column!.Value, c.Item.Row!.Value) : 0)
                + axis.Cross(c.Width, c.Height)).DefaultIfEmpty(0).Max();
            if (!(axis.Vertical && region.Columns > 0)) regionCross = Math.Max(regionCross, needed);
            LegacyCross(startCross + regionCross);
            int mainLimit = !axis.Vertical && region.Columns > 0
                ? checked(startMain + region.Columns) : ContentLayoutPolicy.MaximumRow + 1;
            if (responsiveManual)
            {
                int previousMain = -1, previousCross = -1, previousEnd = 0;
                foreach (var c in members)
                {
                    int width = axis.Cross(c.Width, c.Height), height = axis.Main(c.Width, c.Height);
                    int cross = onePerBand ? 0 : Math.Clamp(axis.Cross(c.Item.Column!.Value, c.Item.Row!.Value), 0, regionCross - width);
                    int authoredMain = axis.Main(c.Item.Column!.Value, c.Item.Row!.Value);
                    int main = Math.Max(authoredMain, onePerBand ? previousEnd : previousMain);
                    if (!onePerBand && main == previousMain && cross < previousCross) main++;
                    bool found = false;
                    for (; main <= mainLimit - height; main++)
                    {
                        if (!occupancy.Fits(cross, main, width, height)) continue;
                        occupancy.Add(cross, main, width, height); Add(c, cross, main, region);
                        previousMain = main; previousCross = cross; previousEnd = main + height;
                        found = true; break;
                    }
                    if (!found) throw new ContentLayoutException("Manual projection exhausted its bounded main-axis range.");
                }
                continue;
            }
            foreach (var c in members.Where(c => c.Positioned))
                Add(c, axis.Cross(region.Column + c.Item.Column!.Value, region.Row + c.Item.Row!.Value),
                    axis.Main(region.Column + c.Item.Column.Value, region.Row + c.Item.Row.Value), region);
            int cursorMain = startMain, cursorCross = startCross;
            foreach (var c in members.Where(c => !c.Positioned))
            {
                int width = axis.Cross(c.Width, c.Height), height = axis.Main(c.Width, c.Height);
                if (width > regionCross) throw new ContentLayoutException("Readable card cannot fit the declared region: " + c.Item.ItemId);
                bool found = false;
                for (int main = cursorMain; main <= mainLimit - height && !found; main++)
                {
                    int firstCross = main == cursorMain ? cursorCross : startCross;
                    int lastCross = onePerBand ? startCross : startCross + regionCross - width;
                    for (int cross = firstCross; cross <= lastCross; cross++)
                    {
                        if (!occupancy.Fits(cross, main, width, height)) continue;
                        occupancy.Add(cross, main, width, height);
                        Add(c, cross, main, region);
                        cursorMain = onePerBand ? main + height : main;
                        cursorCross = onePerBand ? startCross : cross + width;
                        found = true; break;
                    }
                }
                if (!found) throw new ContentLayoutException("Content layout exhausted its bounded main-axis range or declared region.");
            }
        }
        void Add(Candidate c, int cross, int main, ContentLayoutRegion region)
        {
            Include(cross, main, axis.Cross(c.Width, c.Height), axis.Main(c.Width, c.Height));
            int x = axis.Vertical ? cross : main, y = axis.Vertical ? main : cross;
            result.Add(new(c.Item.ItemId, sourceIndices[c.Item.ItemId], orders[c.Item.ItemId], x, y, c.Width, c.Height,
                new(x * pitch, y * pitch, c.Width * pitch - gap, c.Height * pitch - gap), default, c.Diagnostic)
            { RegionColumn = region.Column, RegionRow = region.Row });
        }
        // A global order pin cannot be honoured merely by sorting the returned array: preserved
        // region origins or fixed positions may put a different item first on the physical canvas.
        if (UsesOrderPins(document) && document.Placements.Any(p => p.PinnedIndex.HasValue))
        {
            var visual = result.OrderBy(p => axis.Main(p.Column, p.Row)).ThenBy(p => axis.Cross(p.Column, p.Row)).ToArray();
            if (ordered.Where(p => p.PinnedIndex.HasValue).Any(p => visual[p.PinnedIndex!.Value - 1].ItemId != p.ItemId))
                status = ContentLayoutSpaceStatus.LegacyConstraintConflict;
        }
        if (status != ContentLayoutSpaceStatus.Normal) diagnostics.Add("space:" + status);
        double crossExtent = extentCross == 0 ? 0 : extentCross * pitch - gap;
        double mainExtent = extentMain == 0 ? 0 : extentMain * pitch - gap;
        var snapshot = new ContentLayoutSnapshot(document.PosterId, document.DocumentVersion, document.ItemSetVersion,
            viewport.Generation, viewport.ThemeMetricsVersion, axis.Vertical ? crossCells : extentMain, cell, gap,
            axis.Vertical ? crossExtent : mainExtent, axis.Vertical ? mainExtent : crossExtent,
            0, 0, Array.AsReadOnly(result.OrderBy(p => p.ReadOrder).ToArray()), Array.AsReadOnly(diagnostics.ToArray()))
        {
            FlowAxis = document.FlowAxis, LoopScroll = document.LoopScroll, CrossAxisCells = crossCells,
            MinimumCrossAxisDip = minimumDip, AvailableCrossAxisDip = availableDip, SpaceStatus = status,
            ResolvedScale = viewport.Scale, ResolvedSpacing = viewport.Spacing
        };
        // Compatibility for callers still using the original vertical anchor. New adapters use
        // ContentBrowseAnchor, which also preserves the physical internal/viewport point across axes.
        if (anchor is not null)
        {
            if (!double.IsFinite(anchor.InsideOffsetDip) || anchor.PreviousReadOrder < 0)
                throw new ContentLayoutException("Invalid scroll anchor.");
            var match = snapshot.Items.FirstOrDefault(i => i.ItemId == anchor.ItemId);
            var replacement = match ?? snapshot.Items.FirstOrDefault(i => i.ReadOrder >= anchor.PreviousReadOrder) ?? snapshot.Items.LastOrDefault();
            double offset = replacement is null ? 0 : axis.Main(replacement.Bounds.X, replacement.Bounds.Y) +
                (match is null ? 0 : anchor.InsideOffsetDip);
            viewport = axis.Vertical ? viewport with { ScrollYDip = offset } : viewport with { ScrollXDip = offset };
        }
        return Reclip(snapshot, viewport);
    }

    /// <summary>Retains complete geometry; ordinary reclip never grants passive two-axis eligibility.</summary>
    public static ContentLayoutSnapshot Reclip(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentBrowseMode mode = ContentBrowseMode.SingleAxis)
    {
        ValidateViewport(viewport);
        if (snapshot.ResolvedScale is { } scale && (scale != viewport.Scale || snapshot.ResolvedSpacing != viewport.Spacing ||
            snapshot.ThemeMetricsVersion != viewport.ThemeMetricsVersion ||
            snapshot.AvailableCrossAxisDip != (snapshot.FlowAxis == ContentFlowAxis.Vertical ? viewport.WidthDip : viewport.HeightDip)))
            throw new ContentLayoutException("Cross-axis space or layout metrics changed; resolve authoring before clipping.");
        var offsets = ContentLayoutFlow.NormalizeOffsets(snapshot, viewport, mode);
        var view = new ContentRect(0, 0, viewport.WidthDip, viewport.HeightDip);
        var items = snapshot.Items.Select(p => p with
        {
            Clip = (p.Bounds with { X = p.Bounds.X - offsets.ScrollXDip, Y = p.Bounds.Y - offsets.ScrollYDip }).Intersect(view)
        }).ToArray();
        return snapshot with { ViewportGeneration = viewport.Generation, ThemeMetricsVersion = viewport.ThemeMetricsVersion,
            ScrollXDip = offsets.ScrollXDip, ScrollYDip = offsets.ScrollYDip, Items = Array.AsReadOnly(items) };
    }

    internal static ContentPlacement[] OrderedPlacements(ContentLayoutDocument document)
    {
        var sorted = document.Placements.OrderBy(p => p.Order).ThenBy(p => p.ItemId, StringComparer.Ordinal).ToArray();
        if (!UsesOrderPins(document)) return sorted;
        var result = new ContentPlacement[sorted.Length];
        foreach (var p in sorted.Where(p => p.PinnedIndex.HasValue)) result[p.PinnedIndex!.Value - 1] = p;
        var remaining = new Queue<ContentPlacement>(sorted.Where(p => !p.PinnedIndex.HasValue));
        for (int i = 0; i < result.Length; i++) result[i] ??= remaining.Dequeue();
        return result;
    }

    private static bool UsesOrderPins(ContentLayoutDocument document) => document.Mode == ContentLayoutMode.Anchored ||
        document.Mode == ContentLayoutMode.Automatic && document.SchemaVersion >= 3;

    internal static void ValidateViewport(ContentLayoutViewport viewport)
    {
        if (viewport is null || !In(viewport.WidthDip, 0, MaximumViewportDip) || !In(viewport.HeightDip, 0, MaximumViewportDip) ||
            !In(viewport.ScrollXDip, -16_000_000, 16_000_000) || !In(viewport.ScrollYDip, -16_000_000, 16_000_000) ||
            !In(viewport.Scale, .8, 1.5) || !In(viewport.Spacing, .5, 1.5) || viewport.Generation < 0 || viewport.ThemeMetricsVersion < 1)
            throw new ContentLayoutException("Invalid content viewport or metric version.");
    }
    private static bool In(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private readonly record struct Axis(bool Vertical)
    {
        public int Cross(int x, int y) => Vertical ? x : y;
        public int Main(int x, int y) => Vertical ? y : x;
        public double Cross(double x, double y) => Vertical ? x : y;
        public double Main(double x, double y) => Vertical ? y : x;
    }
    private sealed record Candidate(ContentPlacement Item, int Width, int Height, bool Positioned, string? Diagnostic);

    private sealed class Occupancy
    {
        private readonly Dictionary<int, ulong> _bands = new();
        private int _probes;
        public static bool InBounds(int cross, int main, int width, int height) => cross >= 0 && main >= 0 && width >= 1 && height >= 1 &&
            (long)cross + width <= ContentLayoutPolicy.MaximumColumns && (long)main + height <= (long)ContentLayoutPolicy.MaximumRow + 1;
        public bool Fits(int cross, int main, int width, int height)
        {
            if (++_probes > MaximumSearchProbes) throw new ContentLayoutException("Content layout search budget exceeded.");
            if (!InBounds(cross, main, width, height)) return false;
            ulong mask = Mask(cross, width);
            for (int band = main; band < main + height; band++) if ((_bands.GetValueOrDefault(band) & mask) != 0) return false;
            return true;
        }
        public void Add(int cross, int main, int width, int height)
        {
            if (!InBounds(cross, main, width, height)) throw new ContentLayoutException("Invalid occupancy rectangle.");
            ulong mask = Mask(cross, width);
            for (int band = main; band < main + height; band++) _bands[band] = _bands.GetValueOrDefault(band) | mask;
        }
        private static ulong Mask(int cross, int width) => width == 64 ? ulong.MaxValue : ((1UL << width) - 1) << cross;
    }
}
