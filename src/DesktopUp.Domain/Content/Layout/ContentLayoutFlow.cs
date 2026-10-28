namespace DesktopUp.Domain;

/// <summary>
/// One visible copy of a card produced by the loop projection.
/// LoopIndex distinguishes copies across the seam; ItemId and SourceIndex never change, so a card
/// keeps exactly one persisted placement no matter how many times it is drawn.
/// </summary>
public sealed record ContentLayoutTile(string ItemId, int SourceIndex, int ReadOrder, int LoopIndex,
    double OffsetXDip, double OffsetYDip, ContentRect Bounds, ContentRect Clip);

/// <summary>
/// Pure single-axis scrolling model (no HWND, no timer, no persistence and no pixel ownership).
/// The flow axis chooses the main direction; the independent <c>LoopScroll</c> switch decides whether
/// that axis wraps head-to-tail. Vertical and horizontal behave symmetrically: content shorter than the
/// viewport is never duplicated, and loop projection never changes the squeezing, wrapping or
/// minimum-scale limits produced by the solver.
/// </summary>
public static class ContentLayoutFlow
{
    private static bool IsVertical(ContentLayoutSnapshot snapshot) => snapshot.FlowAxis == ContentFlowAxis.Vertical;
    private static double ExtentAlongAxis(ContentLayoutSnapshot snapshot) =>
        IsVertical(snapshot) ? snapshot.ExtentHeightDip : snapshot.ExtentWidthDip;
    private static double ViewAlongAxis(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport) =>
        IsVertical(snapshot) ? viewport.HeightDip : viewport.WidthDip;

    /// <summary>True only when the poster opted into looping and the content is longer than the
    /// viewport along its main axis. Short content returns false so nothing is padded with copies.</summary>
    public static bool IsLooping(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (snapshot is null || viewport is null || !snapshot.LoopScroll || snapshot.Items is null || snapshot.Items.Count == 0 ||
            snapshot.SpaceStatus != ContentLayoutSpaceStatus.Normal) return false;
        double extent = ExtentAlongAxis(snapshot);
        double view = ViewAlongAxis(snapshot, viewport);
        return double.IsFinite(extent) && double.IsFinite(view) && view > 0 && extent > view + 0.5 &&
            double.IsFinite(extent + snapshot.GapDip);
    }

    /// <summary>Head-to-tail period in DIP along the main axis: one full strip plus the trailing gap.</summary>
    public static double LoopPeriodDip(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (snapshot is null || snapshot.Items.Count == 0) return 0;
        double period = Math.Max(0, ExtentAlongAxis(snapshot)) + Math.Max(0, snapshot.GapDip);
        return double.IsFinite(period) ? period : 0;
    }

    /// <summary>Folds any accumulated offset back into [0, period) so long scroll sessions and
    /// restore/recovery cannot accumulate coordinate drift.</summary>
    public static double NormalizeScroll(double scrollDip, double periodDip)
    {
        if (!double.IsFinite(scrollDip) || !double.IsFinite(periodDip) || periodDip <= 0) return 0;
        double mod = scrollDip % periodDip;
        if (mod < 0) mod += periodDip;
        // Rounding a tiny negative input up to the exact period must not escape [0, period).
        return mod == 0 || mod >= periodDip ? 0 : mod;
    }

    /// <summary>Continuous scrolling primitive on the horizontal axis; stays in the normalized domain.</summary>
    public static double AdvanceScrollX(double scrollXDip, double deltaDip, double periodDip)
        => AdvanceScroll(scrollXDip, deltaDip, periodDip);
    /// <summary>Continuous scrolling primitive on the vertical axis; stays in the normalized domain.</summary>
    public static double AdvanceScrollY(double scrollYDip, double deltaDip, double periodDip)
        => AdvanceScroll(scrollYDip, deltaDip, periodDip);

    private static double AdvanceScroll(double scrollDip, double deltaDip, double periodDip)
    {
        if (!double.IsFinite(scrollDip) || !double.IsFinite(deltaDip) || !double.IsFinite(periodDip) || periodDip <= 0) return 0;
        double current = NormalizeScroll(scrollDip, periodDip), delta = NormalizeScroll(deltaDip, periodDip);
        // Reduce before addition, including large signed deltas, without overflowing two finite inputs.
        return NormalizeScroll(current >= periodDip - delta ? current - (periodDip - delta) : current + delta, periodDip);
    }

    /// <summary>Shared finite/loop normalization for the solver and tile projection. Ordinary
    /// browsing exposes only the chosen main axis; authorized read-only overflow exposes two finite
    /// axes. No document preference, position or version is changed.</summary>
    public static ContentBrowseOffsets NormalizeOffsets(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentBrowseMode mode = ContentBrowseMode.SingleAxis)
    {
        ValidateSnapshot(snapshot);
        ContentLayoutSolver.ValidateViewport(viewport);
        if (!Enum.IsDefined(mode)) throw new ContentLayoutException("Unknown content browse mode.");
        if (mode == ContentBrowseMode.ReadOnlyOverflow && snapshot.SpaceStatus != ContentLayoutSpaceStatus.InsufficientCrossAxis)
            throw new ContentLayoutException("Two-axis overflow requires an eligible insufficient-space snapshot.");
        if (snapshot.Items.Count == 0) return new(0, 0);
        double maximumX = Math.Max(0, snapshot.ExtentWidthDip - viewport.WidthDip);
        double maximumY = Math.Max(0, snapshot.ExtentHeightDip - viewport.HeightDip);
        if (mode == ContentBrowseMode.ReadOnlyOverflow)
            return new(Math.Clamp(viewport.ScrollXDip, 0, maximumX), Math.Clamp(viewport.ScrollYDip, 0, maximumY));
        bool vertical = IsVertical(snapshot);
        double requested = vertical ? viewport.ScrollYDip : viewport.ScrollXDip;
        double offset = IsLooping(snapshot, viewport)
            ? NormalizeScroll(requested, LoopPeriodDip(snapshot, viewport))
            : Math.Clamp(requested, 0, vertical ? maximumY : maximumX);
        return vertical ? new(0, offset) : new(offset, 0);
    }

    /// <summary>
    /// Projects the snapshot into visible tiles. At most three cycles (-1, 0, +1) are emitted along the
    /// looping axis, so a card straddling the seam is drawn on both sides while its identity and saved
    /// state stay single-owned. The non-looping axis keeps its ordinary clamped offset.
    /// </summary>
    public static IReadOnlyList<ContentLayoutTile> ProjectTiles(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentBrowseMode mode = ContentBrowseMode.SingleAxis, double overscanDip = 0)
    {
        if (snapshot is null || viewport is null) throw new ContentLayoutException("Missing content snapshot or viewport.");
        var offsets = NormalizeOffsets(snapshot, viewport with { ScrollXDip = snapshot.ScrollXDip, ScrollYDip = snapshot.ScrollYDip }, mode);
        var view = new ContentRect(0, 0, viewport.WidthDip, viewport.HeightDip);
        if (!double.IsFinite(overscanDip) || overscanDip < 0 || overscanDip > snapshot.CellDip + snapshot.GapDip)
            throw new ContentLayoutException("Overscan must be bounded to one grid pitch.");
        // Do not enlarge the logical viewport: that would change loop eligibility/offsets.
        var admission = new ContentRect(-overscanDip, -overscanDip,
            view.Width + 2 * overscanDip, view.Height + 2 * overscanDip);
        bool looping = mode == ContentBrowseMode.SingleAxis && IsLooping(snapshot, viewport);
        bool vertical = IsVertical(snapshot);
        double period = looping ? LoopPeriodDip(snapshot, viewport) : 0;
        double scrollX = offsets.ScrollXDip, scrollY = offsets.ScrollYDip;

        var tiles = new List<ContentLayoutTile>();
        int first = looping ? -1 : 0, last = looping ? 1 : 0;
        for (int cycle = first; cycle <= last; cycle++)
        {
            double offsetX = vertical ? 0 : cycle * period;
            double offsetY = vertical ? cycle * period : 0;
            foreach (var item in snapshot.Items)
            {
                var placed = new ContentRect(item.Bounds.X + offsetX - scrollX, item.Bounds.Y + offsetY - scrollY,
                    item.Bounds.Width, item.Bounds.Height);
                if (!placed.Intersects(admission)) continue;
                var clip = placed.Intersect(view);
                tiles.Add(new ContentLayoutTile(item.ItemId, item.SourceIndex, item.ReadOrder, cycle, offsetX, offsetY, placed, clip));
            }
        }
        return Array.AsReadOnly(tiles.OrderBy(t => t.ReadOrder).ThenBy(t => t.LoopIndex).ToArray());
    }

    /// <summary>Captures the actual visible copy and point, including a copy across the loop seam.
    /// This does not grant ownership of a pointer gesture or authorize a later edit.</summary>
    public static ContentBrowseAnchor CaptureAnchor(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentLayoutTile tile, double viewportXDip, double viewportYDip, ContentBrowseMode mode = ContentBrowseMode.SingleAxis)
    {
        var tiles = ProjectTiles(snapshot, viewport, mode);
        if (tile is null || !tiles.Contains(tile) || !double.IsFinite(viewportXDip) || !double.IsFinite(viewportYDip) ||
            !tile.Clip.Contains(viewportXDip, viewportYDip))
            throw new ContentLayoutException("The browse anchor must refer to a current visible tile and point.");
        return new(tile.ItemId, tile.ReadOrder, viewportXDip - tile.Bounds.X, viewportYDip - tile.Bounds.Y,
            viewportXDip, viewportYDip);
    }

    /// <summary>Chooses the first visible geometric tile, then records its visible top-left point.</summary>
    public static ContentBrowseAnchor? CaptureAnchor(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentBrowseMode mode = ContentBrowseMode.SingleAxis)
    {
        var tiles = ProjectTiles(snapshot, viewport, mode);
        bool vertical = IsVertical(snapshot);
        var tile = tiles.OrderBy(t => vertical ? t.Clip.Y : t.Clip.X)
            .ThenBy(t => vertical ? t.Clip.X : t.Clip.Y).ThenBy(t => t.ReadOrder).FirstOrDefault();
        return tile is null ? null : CaptureAnchor(snapshot, viewport, tile, tile.Clip.X, tile.Clip.Y, mode);
    }

    /// <summary>Restores identity and the internal point at its former viewport point where bounds
    /// allow. Removed identities use the next surviving read position, then the predecessor. Finite
    /// edges clamp only as much as necessary; an eligible loop retains the equivalent phase.</summary>
    public static ContentBrowseOffsets RestoreAnchor(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentBrowseAnchor? anchor, ContentBrowseMode mode = ContentBrowseMode.SingleAxis)
    {
        // Validate the mode even when the new content is empty or the caller has no old anchor.
        _ = NormalizeOffsets(snapshot, viewport, mode);
        if (anchor is null || snapshot.Items.Count == 0)
            return NormalizeOffsets(snapshot, viewport with { ScrollXDip = 0, ScrollYDip = 0 }, mode);
        if (string.IsNullOrWhiteSpace(anchor.ItemId) || anchor.PreviousReadOrder < 0 ||
            !NonnegativeFinite(anchor.InsideXDip) || !NonnegativeFinite(anchor.InsideYDip) ||
            !NonnegativeFinite(anchor.ViewportXDip) || !NonnegativeFinite(anchor.ViewportYDip))
            throw new ContentLayoutException("Invalid content browse anchor.");
        var ordered = snapshot.Items.OrderBy(i => i.ReadOrder).ThenBy(i => i.ItemId, StringComparer.Ordinal).ToArray();
        var match = ordered.FirstOrDefault(i => i.ItemId == anchor.ItemId);
        var target = match ?? ordered.FirstOrDefault(i => i.ReadOrder >= anchor.PreviousReadOrder) ?? ordered[^1];
        double insideX = match is null ? 0 : Math.Min(anchor.InsideXDip, Math.Max(0, Math.BitDecrement(target.Bounds.Width)));
        double insideY = match is null ? 0 : Math.Min(anchor.InsideYDip, Math.Max(0, Math.BitDecrement(target.Bounds.Height)));
        double viewX = Math.Min(anchor.ViewportXDip, Math.Max(0, Math.BitDecrement(viewport.WidthDip)));
        double viewY = Math.Min(anchor.ViewportYDip, Math.Max(0, Math.BitDecrement(viewport.HeightDip)));
        return NormalizeOffsets(snapshot, viewport with
        {
            ScrollXDip = target.Bounds.X + insideX - viewX,
            ScrollYDip = target.Bounds.Y + insideY - viewY
        }, mode);
    }

    private static bool NonnegativeFinite(double value) => double.IsFinite(value) && value >= 0;

    private static void ValidateSnapshot(ContentLayoutSnapshot snapshot)
    {
        if (snapshot is null || !Enum.IsDefined(snapshot.FlowAxis) || !Enum.IsDefined(snapshot.SpaceStatus) ||
            !NonnegativeFinite(snapshot.ExtentWidthDip) || !NonnegativeFinite(snapshot.ExtentHeightDip) ||
            !NonnegativeFinite(snapshot.GapDip) || snapshot.Items is null || snapshot.Items.Count > ContentLayoutPolicy.MaximumItems)
            throw new ContentLayoutException("Invalid content browse snapshot.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
            if (item is null || string.IsNullOrWhiteSpace(item.ItemId) || !identities.Add(item.ItemId) ||
                item.ReadOrder < 0 || !NonnegativeFinite(item.Bounds.X) || !NonnegativeFinite(item.Bounds.Y) ||
                !double.IsFinite(item.Bounds.Right) || !double.IsFinite(item.Bounds.Bottom) ||
                item.Bounds.Width <= 0 || item.Bounds.Height <= 0 ||
                item.Bounds.Right > snapshot.ExtentWidthDip + 1e-7 || item.Bounds.Bottom > snapshot.ExtentHeightDip + 1e-7)
                throw new ContentLayoutException("Invalid content browse placement.");
    }
}
