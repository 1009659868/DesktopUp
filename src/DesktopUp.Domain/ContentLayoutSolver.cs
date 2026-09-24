namespace DesktopUp.Domain;

public sealed class ContentLayoutException(string message) : ArgumentException(message);

/// <summary>Bounded, deterministic DIP geometry. It never writes responsive projections into intent.</summary>
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
        // One axis only, chosen per poster. The horizontal strip is resolved by a separate pass so the
        // vertical path keeps its proven behaviour for every existing fixture.
        if (document.FlowAxis == ContentFlowAxis.HorizontalLoop) return ResolveHorizontalLoop(document, viewport, anchor);
        double cell = document.BaseCellDip * viewport.Scale, gap = document.BaseGapDip * viewport.Spacing;
        double pitch = cell + gap;
        int minimum = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + gap) / pitch);
        if (minimum > document.MaximumColumns)
            throw new ContentLayoutException("The configured maximum columns cannot accommodate a readable card.");
        int columns = Math.Clamp((int)Math.Floor((viewport.WidthDip + gap) / pitch), minimum, document.MaximumColumns);
        var occupied = new Occupancy();
        var diagnostics = new List<string>();
        int extentColumns = 0, extentRows = 0;
        void Include(int c, int r, int w, int h)
        { extentColumns = Math.Max(extentColumns, c + w); extentRows = Math.Max(extentRows, r + h); }
        // Decorative spaces are presentation only: free space stays droppable, only explicitly
        // managed keep-out areas block a card.
        foreach (var space in document.ReservedSpaces.Where(s => s.Kind == ContentReservedKind.Explicit))
        {
            occupied.Add(space.Column, space.Row, space.ColumnSpan, space.RowSpan);
            Include(space.Column, space.Row, space.ColumnSpan, space.RowSpan);
        }
        var ordered = OrderedPlacements(document);
        var readOrder = ordered.Select((p, i) => (p.ItemId, i)).ToDictionary(x => x.ItemId, x => x.i, StringComparer.Ordinal);
        var indices = document.Placements.Select((p, i) => (p.ItemId, i)).ToDictionary(x => x.ItemId, x => x.i, StringComparer.Ordinal);
        var result = new List<ContentResolvedPlacement>();
        // Reserve future regions' fixed intent as well; an earlier flowing region must not steal it.
        foreach (var p in ordered.Where(p => p.FixedPosition || document.Mode == ContentLayoutMode.Manual && p.Column.HasValue))
        {
            var r = document.Regions.Single(r => r.Id == p.RegionId);
            int width = Math.Max(minimum, p.ColumnSpan), height = Math.Max(minimum, p.RowSpan);
            int column = checked(r.Column + p.Column!.Value), row = checked(r.Row + p.Row!.Value);
            if (r.Columns > 0 && p.Column.Value + width > r.Columns || !occupied.Fits(column, row, width, height))
                throw new ContentLayoutException("Fixed/manual position conflicts with a card, reserved space or region: " + p.ItemId);
            occupied.Add(column, row, width, height);
        }
        int previousRegionBottom = 0;
        foreach (var region in document.Regions.OrderBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var items = ordered.Where(p => p.RegionId == region.Id).ToArray();
            int left = region.Column, top = region.Row;
            int declared = region.Columns == 0 ? Math.Max(minimum, columns - left) : region.Columns;
            bool preservesCoordinates = document.Mode == ContentLayoutMode.Manual || items.Any(p => p.FixedPosition);
            int width = declared;
            if (!preservesCoordinates)
            {
                if (left + declared > columns)
                {
                    left = 0; top = Math.Max(top, previousRegionBottom);
                    diagnostics.Add("region-folded:" + region.Id);
                }
                width = Math.Max(minimum, Math.Min(declared, columns - left));
            }
            else if (region.Columns == 0)
                width = Math.Max(width, items.Where(p => document.Mode == ContentLayoutMode.Manual || p.Column.HasValue)
                    .Select(p => (p.Column ?? 0) + Math.Max(minimum, p.ColumnSpan)).DefaultIfEmpty(0).Max());
            if (left + width > ContentLayoutPolicy.MaximumColumns)
                throw new ContentLayoutException("Region exceeds the bounded logical column range: " + region.Id);
            var prepared = items.Select(p =>
            {
                int w = Math.Max(minimum, p.ColumnSpan), h = Math.Max(minimum, p.RowSpan);
                var notes = new List<string>();
                if (w != p.ColumnSpan || h != p.RowSpan) notes.Add("readable-minimum");
                bool positioned = p.FixedPosition || document.Mode == ContentLayoutMode.Manual && p.Column.HasValue;
                // Squeeze, never clip or hide. A card that no longer fits the remaining row wraps to the
                // next line with its preferred span intact; only when the container is narrower than the
                // card itself (the single-column limit) is the span squeezed to the container, and that
                // limit is reported instead of being applied silently. Nothing is written back to intent,
                // so widening the poster restores the previous line structure.
                if (!positioned && document.Mode != ContentLayoutMode.Manual && w > width)
                { w = width; notes.Add("squeeze-single-column"); }
                if (w > width)
                    throw new ContentLayoutException("Preferred card does not fit its declared region: " + p.ItemId);
                return new Candidate(p, w, h, positioned, notes.Count == 0 ? null : string.Join(";", notes));
            }).ToArray();
            // Position intent is reserved before flowing peers, not merely when its item is encountered.
            foreach (var c in prepared.Where(c => c.Positioned))
            {
                int x = checked(left + c.Item.Column!.Value), y = checked(top + c.Item.Row!.Value);
                if (x + c.Width > left + width)
                    throw new ContentLayoutException("Fixed/manual position conflicts with a card, reserved space or region: " + c.Item.ItemId);
                Add(c, x, y);
            }
            int cursor = top;
            foreach (var c in prepared.Where(c => !c.Positioned))
            {
                bool found = false;
                for (int row = cursor; row <= ContentLayoutPolicy.MaximumRow - c.Height + 1 && !found; row++)
                for (int column = left; column <= left + width - c.Width; column++)
                {
                    if (!occupied.Fits(column, row, c.Width, c.Height)) continue;
                    occupied.Add(column, row, c.Width, c.Height); Add(c, column, row);
                    cursor = row; found = true; break;
                }
                if (!found) throw new ContentLayoutException("Content layout has exhausted the bounded row range.");
            }
            void Add(Candidate c, int column, int row)
            {
                Include(column, row, c.Width, c.Height);
                previousRegionBottom = Math.Max(previousRegionBottom, row + c.Height);
                result.Add(new(c.Item.ItemId, indices[c.Item.ItemId], readOrder[c.Item.ItemId], column, row,
                    c.Width, c.Height, new(column * pitch, row * pitch, c.Width * pitch - gap, c.Height * pitch - gap),
                    default, c.Diagnostic) { RegionColumn = left, RegionRow = top });
            }
        }
        // Vertical axis: the column count already adapts to the poster width, so the extent keeps the
        // true content range (never below a card's right edge). Sideways scrolling is suppressed in
        // Reclip instead, which preserves the "extent covers all items" invariant.
        var snapshot = new ContentLayoutSnapshot(document.PosterId, document.DocumentVersion, document.ItemSetVersion,
            viewport.Generation, viewport.ThemeMetricsVersion, columns, cell, gap,
            extentColumns == 0 ? 0 : extentColumns * pitch - gap, extentRows == 0 ? 0 : extentRows * pitch - gap,
            0, 0, Array.AsReadOnly(result.OrderBy(p => p.ReadOrder).ToArray()), Array.AsReadOnly(diagnostics.ToArray()))
        { FlowAxis = ContentFlowAxis.Vertical, LoopScroll = document.LoopScroll };
        double scrollY = viewport.ScrollYDip;
        if (anchor is not null)
        {
            if (!double.IsFinite(anchor.InsideOffsetDip) || anchor.PreviousReadOrder < 0)
                throw new ContentLayoutException("Invalid scroll anchor.");
            var match = snapshot.Items.FirstOrDefault(i => i.ItemId == anchor.ItemId);
            var replacement = match ?? snapshot.Items.FirstOrDefault(i => i.ReadOrder >= anchor.PreviousReadOrder) ?? snapshot.Items.LastOrDefault();
            scrollY = replacement is null ? 0 : replacement.Bounds.Y + (match is null ? 0 : anchor.InsideOffsetDip);
        }
        return Reclip(snapshot, viewport with { ScrollYDip = Math.Max(0, scrollY) });
    }

    /// <summary>
    /// HorizontalLoop axis: the content height adapts to the poster (rows come from the viewport
    /// height) and cards only advance to the right. The resulting strip is finite and periodic, so
    /// ContentLayoutFlow can wrap it head to tail; dragging, anchors and persistence never see a
    /// duplicated card, only a repeated projection of the same ItemId.
    /// </summary>
    private static ContentLayoutSnapshot ResolveHorizontalLoop(ContentLayoutDocument document, ContentLayoutViewport viewport,
        ContentScrollAnchor? anchor)
    {
        double cell = document.BaseCellDip * viewport.Scale, gap = document.BaseGapDip * viewport.Spacing;
        double pitch = cell + gap;
        int minimum = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + gap) / pitch);
        if (minimum > document.MaximumColumns)
            throw new ContentLayoutException("The configured maximum columns cannot accommodate a readable card.");
        // Minimum acceptable height guard: the usable row count never drops below the rows one
        // minimum-readable card needs. A viewport shorter than that is clamped up to the minimum
        // acceptable height instead of producing a clipped, unreachable row.
        double minimumHeightDip = minimum * pitch - gap;
        double usableHeightDip = Math.Max(viewport.HeightDip, minimumHeightDip);
        int availableRows = Math.Max(minimum, (int)Math.Floor((usableHeightDip + gap) / pitch));
        bool viewportHeightClamped = viewport.HeightDip + 0.5 < minimumHeightDip;
        var occupied = new Occupancy();
        var diagnostics = new List<string>();
        if (viewportHeightClamped)
            diagnostics.Add("viewport-height-clamped:" + minimumHeightDip.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        int extentColumns = 0, extentRows = 0;
        void Include(int c, int r, int w, int h)
        { extentColumns = Math.Max(extentColumns, c + w); extentRows = Math.Max(extentRows, r + h); }
        foreach (var space in document.ReservedSpaces.Where(s => s.Kind == ContentReservedKind.Explicit))
        {
            occupied.Add(space.Column, space.Row, space.ColumnSpan, space.RowSpan);
            Include(space.Column, space.Row, space.ColumnSpan, space.RowSpan);
        }
        var ordered = OrderedPlacements(document);
        var readOrder = ordered.Select((p, i) => (p.ItemId, i)).ToDictionary(x => x.ItemId, x => x.i, StringComparer.Ordinal);
        var indices = document.Placements.Select((p, i) => (p.ItemId, i)).ToDictionary(x => x.ItemId, x => x.i, StringComparer.Ordinal);
        var result = new List<ContentResolvedPlacement>();
        var prepared = ordered.Select(p =>
        {
            int w = Math.Max(minimum, p.ColumnSpan), h = Math.Max(minimum, p.RowSpan);
            var notes = new List<string>();
            // Symmetric to the vertical axis: a card taller than the remaining band moves to the next
            // column with its preferred span intact; only when the band is shorter than the card itself
            // (the single-row limit) is the height squeezed, and that limit is reported.
            if (h > availableRows) { h = Math.Max(1, availableRows); notes.Add("squeeze-single-row"); }
            if (h < minimum) notes.Add("readable-minimum-conflict");
            bool positioned = p.FixedPosition || document.Mode == ContentLayoutMode.Manual && p.Column.HasValue;
            return new Candidate(p, w, h, positioned, notes.Count == 0 ? null : string.Join(";", notes));
        }).ToArray();
        void Add(Candidate c, int column, int row)
        {
            Include(column, row, c.Width, c.Height);
            result.Add(new(c.Item.ItemId, indices[c.Item.ItemId], readOrder[c.Item.ItemId], column, row,
                c.Width, c.Height, new(column * pitch, row * pitch, c.Width * pitch - gap, c.Height * pitch - gap),
                default, c.Diagnostic));
        }
        foreach (var c in prepared.Where(c => c.Positioned))
        {
            int column = c.Item.Column!.Value, row = c.Item.Row!.Value;
            if (row + c.Height > availableRows || !occupied.Fits(column, row, c.Width, c.Height))
                throw new ContentLayoutException("Fixed/manual position conflicts with a card, reserved space or region: " + c.Item.ItemId);
            occupied.Add(column, row, c.Width, c.Height); Add(c, column, row);
        }
        int cursor = 0;
        foreach (var c in prepared.Where(c => !c.Positioned))
        {
            bool found = false;
            for (int column = cursor; column <= ContentLayoutPolicy.MaximumColumns - c.Width && !found; column++)
            for (int row = 0; row <= availableRows - c.Height; row++)
            {
                if (!occupied.Fits(column, row, c.Width, c.Height)) continue;
                occupied.Add(column, row, c.Width, c.Height); Add(c, column, row);
                cursor = column; found = true; break;
            }
            if (!found) throw new ContentLayoutException("Content layout has exhausted the bounded column range.");
        }
        var snapshot = new ContentLayoutSnapshot(document.PosterId, document.DocumentVersion, document.ItemSetVersion,
            viewport.Generation, viewport.ThemeMetricsVersion, ContentLayoutPolicy.MaximumColumns, cell, gap,
            extentColumns == 0 ? 0 : extentColumns * pitch - gap, extentRows == 0 ? 0 : extentRows * pitch - gap,
            Math.Max(0, viewport.ScrollXDip), 0, Array.AsReadOnly(result.OrderBy(p => p.ReadOrder).ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()))
        { FlowAxis = ContentFlowAxis.HorizontalLoop, LoopScroll = document.LoopScroll };
        // A vertical scroll anchor has no meaning on the horizontal axis; the strip keeps the caller's
        // horizontal offset instead of restoring a row-based position.
        return Reclip(snapshot, viewport);
    }

    /// <summary>R4/scroll path: retain full content geometry and only change its visible intersection.</summary>
    public static ContentLayoutSnapshot Reclip(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        ValidateViewport(viewport);
        if (snapshot.Items.Count > ContentLayoutPolicy.MaximumItems || !double.IsFinite(snapshot.ExtentWidthDip) ||
            !double.IsFinite(snapshot.ExtentHeightDip) || snapshot.ExtentWidthDip < 0 || snapshot.ExtentHeightDip < 0)
            throw new ContentLayoutException("Invalid snapshot extent.");
        // Single axis: the loop never scrolls vertically, and its horizontal offset stays unbounded
        // (folded back by ContentLayoutFlow at projection time) so the strip wraps instead of hitting
        // a hard end. Vertical content only grows downward for flowing cards; a manual/fixed card that
        // is wider than the current viewport keeps its horizontal reach, because narrowing must never
        // silently clip a positioned card out of reach.
        bool loop = snapshot.FlowAxis == ContentFlowAxis.HorizontalLoop;
        double x = loop
            ? Math.Max(0, viewport.ScrollXDip)
            : Math.Clamp(viewport.ScrollXDip, 0, Math.Max(0, snapshot.ExtentWidthDip - viewport.WidthDip));
        double y = loop
            ? 0
            : Math.Clamp(viewport.ScrollYDip, 0, Math.Max(0, snapshot.ExtentHeightDip - viewport.HeightDip));
        var view = new ContentRect(0, 0, viewport.WidthDip, viewport.HeightDip);
        var items = snapshot.Items.Select(p =>
        {
            if (!Finite(p.Bounds)) throw new ContentLayoutException("Invalid snapshot rectangle.");
            return p with { Clip = (p.Bounds with { X = p.Bounds.X - x, Y = p.Bounds.Y - y }).Intersect(view) };
        }).ToArray();
        return snapshot with { ViewportGeneration = viewport.Generation, ThemeMetricsVersion = viewport.ThemeMetricsVersion,
            ScrollXDip = x, ScrollYDip = y, Items = Array.AsReadOnly(items) };
    }

    internal static ContentPlacement[] OrderedPlacements(ContentLayoutDocument document)
    {
        var sorted = document.Placements.OrderBy(p => p.Order).ThenBy(p => p.ItemId, StringComparer.Ordinal).ToArray();
        if (document.Mode != ContentLayoutMode.Anchored) return sorted;
        var result = new ContentPlacement[sorted.Length];
        foreach (var p in sorted.Where(p => p.PinnedIndex.HasValue)) result[p.PinnedIndex!.Value - 1] = p;
        var remaining = new Queue<ContentPlacement>(sorted.Where(p => !p.PinnedIndex.HasValue));
        for (int i = 0; i < result.Length; i++) result[i] ??= remaining.Dequeue();
        return result;
    }

    internal static void ValidateViewport(ContentLayoutViewport viewport)
    {
        if (viewport is null || !In(viewport.WidthDip, 0, MaximumViewportDip) || !In(viewport.HeightDip, 0, MaximumViewportDip) ||
            !In(viewport.ScrollXDip, 0, 16_000_000) || !In(viewport.ScrollYDip, 0, 16_000_000) ||
            !In(viewport.Scale, .8, 1.5) || !In(viewport.Spacing, .5, 1.5) || viewport.Generation < 0 || viewport.ThemeMetricsVersion < 1)
            throw new ContentLayoutException("Invalid content viewport or metric version.");
    }
    private static bool In(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private static bool Finite(ContentRect r) => double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Right) &&
        double.IsFinite(r.Bottom) && r.Width > 0 && r.Height > 0 && r.X >= 0 && r.Y >= 0;
    private sealed record Candidate(ContentPlacement Item, int Width, int Height, bool Positioned, string? Diagnostic);

    private sealed class Occupancy
    {
        private readonly Dictionary<int, ulong> _rows = new();
        private int _probes;
        public bool Fits(int column, int row, int width, int height)
        {
            if (++_probes > MaximumSearchProbes) throw new ContentLayoutException("Content layout search budget exceeded.");
            if (column < 0 || row < 0 || width < 1 || height < 1 || column + width > ContentLayoutPolicy.MaximumColumns ||
                (long)row + height > (long)ContentLayoutPolicy.MaximumRow + 1) return false;
            ulong mask = Mask(column, width);
            for (int r = row; r < row + height; r++) if ((_rows.GetValueOrDefault(r) & mask) != 0) return false;
            return true;
        }
        public void Add(int column, int row, int width, int height)
        {
            ulong mask = Mask(column, width);
            for (int r = row; r < row + height; r++) _rows[r] = _rows.GetValueOrDefault(r) | mask;
        }
        private static ulong Mask(int column, int width) => width == 64 ? ulong.MaxValue : ((1UL << width) - 1) << column;
    }
}
