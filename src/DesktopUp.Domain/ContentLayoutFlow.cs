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
        if (snapshot is null || viewport is null || !snapshot.LoopScroll) return false;
        double extent = ExtentAlongAxis(snapshot);
        return extent > 1 && extent > ViewAlongAxis(snapshot, viewport) + 0.5;
    }

    /// <summary>Head-to-tail period in DIP along the main axis: one full strip plus the trailing gap.</summary>
    public static double LoopPeriodDip(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (snapshot is null || snapshot.Items.Count == 0) return 0;
        return Math.Max(0, ExtentAlongAxis(snapshot)) + Math.Max(0, snapshot.GapDip);
    }

    /// <summary>Folds any accumulated offset back into [0, period) so long scroll sessions and
    /// restore/recovery cannot accumulate coordinate drift.</summary>
    public static double NormalizeScroll(double scrollDip, double periodDip)
    {
        if (!double.IsFinite(scrollDip) || periodDip <= 1) return 0;
        double mod = scrollDip % periodDip;
        return mod < 0 ? mod + periodDip : mod;
    }

    /// <summary>Continuous scrolling primitive on the horizontal axis; stays in the normalized domain.</summary>
    public static double AdvanceScrollX(double scrollXDip, double deltaDip, double periodDip)
        => NormalizeScroll(scrollXDip + deltaDip, periodDip);
    /// <summary>Continuous scrolling primitive on the vertical axis; stays in the normalized domain.</summary>
    public static double AdvanceScrollY(double scrollYDip, double deltaDip, double periodDip)
        => NormalizeScroll(scrollYDip + deltaDip, periodDip);

    /// <summary>
    /// Projects the snapshot into visible tiles. At most three cycles (-1, 0, +1) are emitted along the
    /// looping axis, so a card straddling the seam is drawn on both sides while its identity and saved
    /// state stay single-owned. The non-looping axis keeps its ordinary clamped offset.
    /// </summary>
    public static IReadOnlyList<ContentLayoutTile> ProjectTiles(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (snapshot is null) throw new ContentLayoutException("Missing content snapshot.");
        ContentLayoutSolver.ValidateViewport(viewport);
        var view = new ContentRect(0, 0, viewport.WidthDip, viewport.HeightDip);
        bool looping = IsLooping(snapshot, viewport);
        bool vertical = IsVertical(snapshot);
        double period = looping ? LoopPeriodDip(snapshot, viewport) : 0;

        double scrollX = vertical || !looping ? Math.Max(0, snapshot.ScrollXDip) : NormalizeScroll(snapshot.ScrollXDip, period);
        double scrollY = !vertical || !looping ? Math.Max(0, snapshot.ScrollYDip) : NormalizeScroll(snapshot.ScrollYDip, period);

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
                if (placed.Right <= 0 || placed.X >= view.Width) continue;
                if (placed.Bottom <= 0 || placed.Y >= view.Height) continue;
                var clip = placed.Intersect(view);
                if (clip.IsEmpty) continue;
                tiles.Add(new ContentLayoutTile(item.ItemId, item.SourceIndex, item.ReadOrder, cycle, offsetX, offsetY, placed, clip));
            }
        }
        return Array.AsReadOnly(tiles.OrderBy(t => t.ReadOrder).ThenBy(t => t.LoopIndex).ToArray());
    }
}
