namespace DesktopUp.Domain;

public enum ContentDragKind { Move, Resize }

/// <summary>A transient candidate only. A null edit without an error is a semantic no-op; the
/// caller still owns current-viewport checks, final editing validation, cancellation and commit.</summary>
public sealed record ContentDragResult(ContentLayoutEdit? Edit, string? Error = null)
{
    public bool Accepted => Error is null;
    public bool NoChange => Edit is null && Error is null;
}

/// <summary>Maps total displacement against the complete frozen press composition. It never reads
/// a moving preview, changes authoring, manages input ownership or accumulates per-frame rounding.</summary>
public static class ContentDragPolicy
{
    internal const double MaximumDisplacementDip = 16_000_000;

    public static ContentDragResult Map(ContentLayoutDocument document, ContentLayoutSnapshot pressSnapshot,
        ContentLayoutViewport pressViewport, ContentLayoutTile actualPressTile, ContentDragKind kind,
        double totalPointerXDip, double totalPointerYDip, double scrollDeltaXDip = 0, double scrollDeltaYDip = 0)
    {
        try
        {
            if (document is null || pressSnapshot is null || pressViewport is null || actualPressTile is null || !Enum.IsDefined(kind))
                throw new ContentLayoutException("A known drag kind and complete frozen press witnesses are required.");
            if (pressSnapshot.Items is null || pressSnapshot.Items.Count == 0)
                throw new ContentLayoutException("An empty composition has no drag target.");
            // Reuse the editor's full presented-member/geometry/viewport witness validation through
            // a pure no-op; this neither resolves a new picture nor changes the author document.
            var validation = ContentLayoutEditing.Apply(document, new(ContentLayoutEditKind.Guides), pressViewport, pressSnapshot);
            if (!validation.Accepted) throw new ContentLayoutException(validation.Error ?? "Invalid press composition.");
            if (pressSnapshot.IsLayoutReadOnly)
                throw new ContentLayoutException("A read-only composition cannot start an authored drag.");
            if (!ContentLayoutFlow.ProjectTiles(pressSnapshot, pressViewport).Contains(actualPressTile))
                throw new ContentLayoutException("The drag target is not the actual visible tile from the frozen press composition.");
            bool looping = ContentLayoutFlow.IsLooping(pressSnapshot, pressViewport);
            var slot = pressSnapshot.Items.Single(p => p.ItemId == actualPressTile.ItemId);
            var source = document.Placements[slot.SourceIndex];
            bool vertical = pressSnapshot.FlowAxis == ContentFlowAxis.Vertical;
            RequireDisplacement(totalPointerXDip); RequireDisplacement(totalPointerYDip);
            RequireDisplacement(scrollDeltaXDip); RequireDisplacement(scrollDeltaYDip);
            if (vertical ? scrollDeltaXDip != 0 : scrollDeltaYDip != 0)
                throw new ContentLayoutException("Single-axis dragging cannot invent cross-axis scrolling.");
            double currentScroll = vertical ? pressSnapshot.ScrollYDip + scrollDeltaYDip : pressSnapshot.ScrollXDip + scrollDeltaXDip;
            double maximumScroll = Math.Max(0, vertical ? pressSnapshot.ExtentHeightDip - pressViewport.HeightDip
                : pressSnapshot.ExtentWidthDip - pressViewport.WidthDip);
            if (!double.IsFinite(currentScroll) || !looping && (currentScroll < 0 || currentScroll > maximumScroll + 1e-7))
                throw new ContentLayoutException("The supplied scroll displacement is outside the finite content range.");
            double deltaX = totalPointerXDip + scrollDeltaXDip, deltaY = totalPointerYDip + scrollDeltaYDip;
            RequireDisplacement(deltaX); RequireDisplacement(deltaY);
            double period = looping ? ContentLayoutFlow.LoopPeriodDip(pressSnapshot, pressViewport) : 0;
            // Navigation is unwrapped, not a subtraction of two folded offsets. Remove whole
            // turns BEFORE grid rounding (the period need not be an integer number of pitches).
            // Resizing is a size delta and must never wrap into a small positive/negative size.
            if (looping && kind == ContentDragKind.Move)
            {
                if (vertical) deltaY %= period; else deltaX %= period;
            }
            double pitch = pressSnapshot.CellDip + pressSnapshot.GapDip;
            if (kind == ContentDragKind.Resize)
            {
                double columns = slot.ColumnSpan + deltaX / pitch, rows = slot.RowSpan + deltaY / pitch;
                if (deltaX == 0 && deltaY == 0) return new(null);
                if (!ContentLayoutPolicy.ValidSpan(columns) || !ContentLayoutPolicy.ValidSpan(rows))
                    throw new ContentLayoutException("The requested drag size is outside the supported span range.");
                bool positioned = document.Mode == ContentLayoutMode.Manual || source.FixedPosition;
                // An automatic card may wrap after growing; its old row/column is not a hard
                // boundary. Manual and legacy fixed cards retain the press position instead.
                RequirePlacement(document, pressSnapshot, slot, positioned ? slot.Column - slot.RegionColumn : 0,
                    positioned ? slot.Row - slot.RegionRow : 0, columns, rows);
                return new(new(ContentLayoutEditKind.Resize, source.ItemId) { ColumnSpan = columns, RowSpan = rows });
            }

            int stepX = checked((int)Math.Round(deltaX / pitch, MidpointRounding.AwayFromZero));
            int stepY = checked((int)Math.Round(deltaY / pitch, MidpointRounding.AwayFromZero));
            if (stepX == 0 && stepY == 0) return new(null);

            if (document.Mode == ContentLayoutMode.Manual || source.FixedPosition)
            {
                int column = checked(slot.Column - slot.RegionColumn + stepX), row = checked(slot.Row - slot.RegionRow + stepY);
                if (looping)
                {
                    double origin = vertical ? slot.Bounds.Y : slot.Bounds.X;
                    double moved = ContentLayoutFlow.NormalizeScroll(origin + (vertical ? deltaY : deltaX), period);
                    int mainCell = checked((int)Math.Round(moved / pitch, MidpointRounding.AwayFromZero));
                    if (mainCell * pitch >= period)
                        throw new ContentLayoutException("The drop lies in the trailing seam gap, not a canonical grid cell.");
                    if (vertical) row = mainCell - slot.RegionRow; else column = mainCell - slot.RegionColumn;
                }
                RequirePlacement(document, pressSnapshot, slot, column, row, slot.ColumnSpan, slot.RowSpan);
                if (column == slot.Column - slot.RegionColumn && row == slot.Row - slot.RegionRow) return new(null);
                return new(new(ContentLayoutEditKind.Move, source.ItemId) { Column = column, Row = row });
            }

            // Translate the whole card from the ACTUAL tile. Pointer deltas preserve the original
            // internal grab point; the pointer is never mistaken for the card centre. Removing the
            // tile offset brings the shadow back to canonical logical content, without a loop cycle.
            double centerX = actualPressTile.Bounds.X + pressSnapshot.ScrollXDip - actualPressTile.OffsetXDip
                + actualPressTile.Bounds.Width / 2 + stepX * pitch;
            double centerY = actualPressTile.Bounds.Y + pressSnapshot.ScrollYDip - actualPressTile.OffsetYDip
                + actualPressTile.Bounds.Height / 2 + stepY * pitch;
            if (looping)
            {
                if (vertical) centerY = ContentLayoutFlow.NormalizeScroll(centerY, period);
                else centerX = ContentLayoutFlow.NormalizeScroll(centerX, period);
            }
            int target = FindFinalReadOrder(pressSnapshot, slot, centerX, centerY, vertical);
            return target == slot.ReadOrder ? new(null)
                : new(new(ContentLayoutEditKind.Move, source.ItemId) { TargetReadOrder = target });
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            return new(null, error.Message);
        }
    }

    private static void RequireDisplacement(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > MaximumDisplacementDip)
            throw new ContentLayoutException("Drag displacement must be finite and within the supported coordinate range.");
    }

    private static void RequirePlacement(ContentLayoutDocument document, ContentLayoutSnapshot snapshot,
        ContentResolvedPlacement slot, int column, int row, double columns, double rows)
    {
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        double readable = (ContentLayoutPolicy.MinimumReadableCardDip + snapshot.GapDip) / (snapshot.CellDip + snapshot.GapDip);
        int width = (int)Math.Ceiling(Math.Max(readable, columns)), height = (int)Math.Ceiling(Math.Max(readable, rows));
        long x = (long)slot.RegionColumn + column, y = (long)slot.RegionRow + row;
        long crossEnd = vertical ? x + width : y + height, mainEnd = vertical ? y + height : x + width;
        var region = document.Regions.Single(r => r.Id == document.Placements[slot.SourceIndex].RegionId);
        if (column < 0 || row < 0 || column > ContentLayoutPolicy.MaximumRow || row > ContentLayoutPolicy.MaximumRow ||
            crossEnd > snapshot.CrossAxisCells || mainEnd > ContentLayoutPolicy.MaximumRow + 1L ||
            region.Columns > 0 && (long)column + width > region.Columns)
            throw new ContentLayoutException("The requested drop is outside the available cross-axis, region or logical coordinate bounds.");
    }

    private static int FindFinalReadOrder(ContentLayoutSnapshot snapshot, ContentResolvedPlacement source,
        double centerX, double centerY, bool vertical)
    {
        int insertion = FindInsertionReadOrder(snapshot, centerX, centerY);
        return Math.Clamp(insertion - (source.ReadOrder < insertion ? 1 : 0), 0, snapshot.Items.Count - 1);
    }

    // External items use the same band/midline interpretation without removing an existing member.
    public static int FindInsertionReadOrder(ContentLayoutSnapshot snapshot, double centerX, double centerY)
    {
        RequireDisplacement(centerX); RequireDisplacement(centerY);
        if (snapshot.Items.Count == 0) return 0;
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        double Main(ContentRect rect) => vertical ? rect.Y : rect.X;
        double MainEnd(ContentRect rect) => vertical ? rect.Bottom : rect.Right;
        double CrossCenter(ContentRect rect) => vertical ? rect.X + rect.Width / 2 : rect.Y + rect.Height / 2;
        double main = vertical ? centerY : centerX, cross = vertical ? centerX : centerY;
        var groups = snapshot.Items.GroupBy(p => Main(p.Bounds)).OrderBy(g => g.Key).ToArray();
        // A member belongs only to the band at its main-axis START, even when it spans several
        // later bands. Bands stop at the next start or their own far edge, whichever comes first.
        // Gaps select the nearest band edge; equal distances choose the earlier band. At a shared
        // boundary the half-open interval belongs to the later band. No preview geometry is used.
        var bands = groups.Select((g, i) => new Band(g.Key,
            Math.Min(g.Max(p => MainEnd(p.Bounds)), i + 1 < groups.Length ? groups[i + 1].Key : double.PositiveInfinity),
            g.OrderBy(p => vertical ? p.Bounds.X : p.Bounds.Y).ThenBy(p => p.ReadOrder).ToArray())).ToArray();
        var band = bands.FirstOrDefault(b => main >= b.Start && main < b.End)
            ?? bands.OrderBy(b => main < b.Start ? b.Start - main : Math.Max(0, main - b.End)).ThenBy(b => b.Start).First();
        // Exact item midlines mean "after". Resolve an insertion boundary in the frozen full
        // sequence, then remove the source once to obtain the final zero-based member position.
        var before = band.Items.FirstOrDefault(p => cross < CrossCenter(p.Bounds));
        int insertion = before?.ReadOrder ?? band.Items[^1].ReadOrder + 1;
        return Math.Clamp(insertion, 0, snapshot.Items.Count);
    }

    private sealed record Band(double Start, double End, ContentResolvedPlacement[] Items);
}
