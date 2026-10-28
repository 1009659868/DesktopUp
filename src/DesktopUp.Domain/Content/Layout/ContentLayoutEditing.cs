namespace DesktopUp.Domain;

public enum ContentLayoutEditKind { Move, Resize, Fixed, Pinned, Mode, Guides, SizeOverride, Activity }
public sealed record ContentLayoutEdit(ContentLayoutEditKind Kind, string? ItemId = null)
{
    public int? Column { get; init; }
    public int? Row { get; init; }
    public double? ColumnSpan { get; init; }
    public double? RowSpan { get; init; }
    public int? PinnedIndex { get; init; }
    /// <summary>Zero-based final read position for automatic dragging; persisted pins are one-based.</summary>
    public int? TargetReadOrder { get; init; }
    public bool? Enabled { get; init; }
    public bool? ShowBaselineGrid { get; init; }
    public bool? ShowSpanLabels { get; init; }
    public ContentLayoutMode? Mode { get; init; }
    public ManualLayoutExit? ManualExit { get; init; }
    public ContentActivityOptions? Activity { get; init; }
}
public sealed record ContentLayoutEditResult(bool Accepted, ContentLayoutDocument Document, string? Error = null)
{
    public ContentLayoutSnapshot? PreviewSnapshot { get; init; }
    public bool AdoptsProjection { get; init; }
}

public static class ContentLayoutEditing
{
    public static ContentLayoutEditResult Apply(ContentLayoutDocument document, ContentLayoutEdit edit, ContentLayoutViewport viewport,
        ContentLayoutSnapshot? presented = null)
    {
        try
        {
            if (!Enum.IsDefined(edit.Kind)) throw new ContentLayoutException("Unknown edit action.");
            if (ContentLayoutPolicy.Validate(document) is { } invalid) throw new ContentLayoutException(invalid);
            var before = presented ?? ContentLayoutSolver.Resolve(document, viewport);
            ValidatePresented(document, before, viewport);
            var candidate = document;
            var items = document.Placements.ToArray();
            int index = Array.FindIndex(items, p => p.ItemId == edit.ItemId);
            bool itemAction = edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize or ContentLayoutEditKind.Fixed or ContentLayoutEditKind.Pinned or ContentLayoutEditKind.SizeOverride;
            if (itemAction && index < 0) throw new ContentLayoutException("The edited item no longer belongs to this poster.");
            var source = index < 0 ? null : items[index];
            var slot = source is null ? null : before.Items.Single(p => p.ItemId == source.ItemId);
            bool manualGeometry = document.Mode == ContentLayoutMode.Manual &&
                edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize;
            bool adoptsProjection = false;
            if (edit.Kind != ContentLayoutEditKind.Guides && before.IsLayoutReadOnly)
                throw new ContentLayoutException("Layout editing requires a normal, complete current composition.");
            // Decide semantic no-ops before capturing responsive geometry or upgrading the source schema.
            if (edit.Kind == ContentLayoutEditKind.Move && document.Mode == ContentLayoutMode.Manual &&
                edit.Column == slot!.Column - slot.RegionColumn && edit.Row == slot.Row - slot.RegionRow ||
                edit.Kind == ContentLayoutEditKind.Resize && edit.ColumnSpan == slot!.ColumnSpan && edit.RowSpan == slot.RowSpan)
                return new(true, document) { PreviewSnapshot = before };
            if (manualGeometry)
            {
                items = CapturePicture(document, before);
                adoptsProjection = !items.SequenceEqual(document.Placements);
                source = items[index];
            }
            switch (edit.Kind)
            {
                case ContentLayoutEditKind.Move:
                    if (document.Mode != ContentLayoutMode.Manual && !source!.FixedPosition)
                    {
                        if (edit.TargetReadOrder is not { } target || target < 0 || target >= items.Length ||
                            edit.Column.HasValue || edit.Row.HasValue)
                            throw new ContentLayoutException("Automatic dragging requires a final read position, not hard coordinates.");
                        int pin = FindAvailablePin(items, source.ItemId, target + 1);
                        if (pin - 1 == slot!.ReadOrder) return new(true, document) { PreviewSnapshot = before };
                        items[index] = source with { PinnedIndex = pin };
                    }
                    else
                    {
                        if (edit.Column is null || edit.Row is null || edit.TargetReadOrder.HasValue)
                            throw new ContentLayoutException("Manual or legacy fixed movement requires a grid position.");
                        items[index] = source! with { Column = edit.Column, Row = edit.Row };
                    }
                    break;
                case ContentLayoutEditKind.Resize:
                    if (edit.ColumnSpan is null || edit.RowSpan is null) throw new ContentLayoutException("Resize requires both finite spans.");
                    items[index] = source! with { ColumnSpan = edit.ColumnSpan.Value, RowSpan = edit.RowSpan.Value, ManualSize = true,
                        AutomaticSizeBasis = document.Mode != ContentLayoutMode.Manual && !source!.ManualSize
                            ? new(source.ColumnSpan, source.RowSpan) : source!.AutomaticSizeBasis };
                    break;
                case ContentLayoutEditKind.SizeOverride:
                    if (edit.Enabled is null) throw new ContentLayoutException("Size override requires an explicit enabled value.");
                    if (document.Mode == ContentLayoutMode.Manual)
                        throw new ContentLayoutException("Automatic size participation is changed only in automatic mode.");
                    if (source!.ManualSize == edit.Enabled.Value) return new(true, document) { PreviewSnapshot = before };
                    if (!edit.Enabled.Value && source.AutomaticSizeBasis is null)
                        throw new ContentLayoutException("The original automatic size basis is unknown; restoring size cannot guess a default.");
                    items[index] = edit.Enabled.Value
                        ? source with { ManualSize = true, ColumnSpan = slot!.ColumnSpan, RowSpan = slot.RowSpan,
                            AutomaticSizeBasis = new(source.ColumnSpan, source.RowSpan) }
                        : source with { ManualSize = false, ColumnSpan = source.AutomaticSizeBasis!.ColumnSpan,
                            RowSpan = source.AutomaticSizeBasis.RowSpan, AutomaticSizeBasis = null };
                    break;
                case ContentLayoutEditKind.Fixed:
                    if (edit.Enabled is null) throw new ContentLayoutException("Fixed requires an explicit enabled value.");
                    if (edit.Enabled.Value == source!.FixedPosition) return new(true, document) { PreviewSnapshot = before };
                    throw new ContentLayoutException("Legacy hard-position conversion requires an explicit compatibility conversion policy.");
                case ContentLayoutEditKind.Pinned:
                    if (edit.Enabled is null) throw new ContentLayoutException("Pinned requires an explicit enabled value.");
                    if (document.Mode == ContentLayoutMode.Manual || source!.FixedPosition)
                        throw new ContentLayoutException("Order pins belong to ordinary automatic cards; legacy hard positions are retained.");
                    if (edit.Enabled == true)
                    {
                        int pin = FindAvailablePin(items, source.ItemId, edit.PinnedIndex ?? slot!.ReadOrder + 1);
                        items[index] = source! with { PinnedIndex = pin };
                    }
                    else items[index] = source! with { PinnedIndex = null };
                    break;
                case ContentLayoutEditKind.Mode:
                    if (edit.Mode is not { } mode || !Enum.IsDefined(mode)) throw new ContentLayoutException("An explicit layout mode is required.");
                    if (mode == document.Mode || mode != ContentLayoutMode.Manual && document.Mode != ContentLayoutMode.Manual)
                        return new(true, document) { PreviewSnapshot = before };
                    if (document.Mode == ContentLayoutMode.Manual)
                    {
                        if (edit.ManualExit.HasValue)
                            throw new ContentLayoutException("Legacy manual exit strategies do not describe the accepted return-to-automatic operation.");
                        if (document.SavedAutomaticMode is not { } savedMode || items.Any(p => p.SavedAutomaticState is null))
                            throw new ContentLayoutException("Original automatic intent is unknown; this manual composition cannot invent a return state.");
                        items = items.Select(RestoreAutomatic).ToArray();
                        candidate = candidate with { Mode = savedMode, SavedAutomaticMode = null };
                    }
                    else if (mode == ContentLayoutMode.Manual)
                    {
                        var author = document.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
                        items = CapturePicture(document, before).Select(p => p with
                        {
                            ManualSize = true,
                            SavedAutomaticState = SaveAutomatic(author[p.ItemId])
                        }).ToArray();
                        candidate = candidate with { Mode = mode, SavedAutomaticMode = document.Mode };
                    }
                    break;
                case ContentLayoutEditKind.Guides:
                    candidate = candidate with { ShowBaselineGrid = edit.ShowBaselineGrid ?? candidate.ShowBaselineGrid,
                        ShowSpanLabels = edit.ShowSpanLabels ?? candidate.ShowSpanLabels };
                    break;
                case ContentLayoutEditKind.Activity:
                    if (edit.Activity is null || edit.Activity.Validate() is not null || edit.Enabled is null)
                        throw new ContentLayoutException("Explicit valid activity settings and size switch are required.");
                    candidate = candidate with { Activity = edit.Activity, AllowHeatDrivenSize = edit.Enabled.Value };
                    break;
            }
            candidate = candidate with { Placements = Array.AsReadOnly(items) };
            if (SameState(document, candidate)) return new(true, document) { PreviewSnapshot = before };
            if (edit.Kind != ContentLayoutEditKind.Guides)
            {
                if (document.SchemaVersion < 3 && document.Mode == ContentLayoutMode.Automatic && document.Placements.Any(p => p.PinnedIndex.HasValue))
                    throw new ContentLayoutException("Legacy automatic pins have no active source semantics; explicit conversion is required before upgrading them.");
                candidate = candidate with { SchemaVersion = ContentLayoutPolicy.SupportedSchemaVersion };
            }
            if (ContentLayoutPolicy.Validate(candidate) is { } validation) throw new ContentLayoutException(validation);
            if (manualGeometry)
                candidate = PushManualPeers(candidate, before, edit.ItemId!, viewport);
            var preview = ContentLayoutSolver.Resolve(candidate, viewport);
            if (edit.Kind != ContentLayoutEditKind.Guides && preview.IsLayoutReadOnly)
                throw new ContentLayoutException("The edit conflicts with the available space or preserved legacy constraints.");
            if (SameState(document, candidate)) return new(true, document);
            var old = document.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
            candidate = candidate with { DocumentVersion = checked(document.DocumentVersion + 1),
                Placements = Array.AsReadOnly(candidate.Placements.Select(p => p == old[p.ItemId] ? p : p with { Version = checked(old[p.ItemId].Version + 1) }).ToArray()) };
            return new(true, ContentLayoutPolicy.Freeze(candidate))
            { PreviewSnapshot = preview with { DocumentVersion = candidate.DocumentVersion }, AdoptsProjection = adoptsProjection };
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        { return new(false, document, error.Message); }
    }

    private static int FindAvailablePin(ContentPlacement[] items, string sourceId, int pin)
    {
        if (pin < 1 || pin > items.Length) throw new ContentLayoutException("Order anchors are one-based and bounded by the member set.");
        var occupied = items.Where(p => p.ItemId != sourceId && p.PinnedIndex.HasValue).Select(p => p.PinnedIndex!.Value).ToHashSet();
        while (pin <= items.Length && occupied.Contains(pin)) pin++;
        if (pin > items.Length) throw new ContentLayoutException("No following order-anchor slot is available.");
        return pin;
    }

    private static ContentAutomaticPlacementState SaveAutomatic(ContentPlacement p) =>
        new(p.Order, p.ColumnSpan, p.RowSpan, p.ManualSize, p.PinnedIndex, p.Column, p.Row, p.FixedPosition)
        { AutomaticSizeBasis = p.AutomaticSizeBasis };

    private static ContentPlacement RestoreAutomatic(ContentPlacement p)
    {
        var saved = p.SavedAutomaticState!;
        return p with { Order = saved.Order, ColumnSpan = saved.ColumnSpan, RowSpan = saved.RowSpan,
            ManualSize = saved.ManualSize, PinnedIndex = saved.PinnedIndex, Column = saved.Column, Row = saved.Row,
            FixedPosition = saved.FixedPosition, AutomaticSizeBasis = saved.AutomaticSizeBasis, SavedAutomaticState = null };
    }

    private static ContentPlacement[] CapturePicture(ContentLayoutDocument document, ContentLayoutSnapshot snapshot)
    {
        var slots = snapshot.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        return document.Placements.Select(p => p with { Column = slots[p.ItemId].Column - slots[p.ItemId].RegionColumn,
            Row = slots[p.ItemId].Row - slots[p.ItemId].RegionRow, ColumnSpan = slots[p.ItemId].ColumnSpan,
            RowSpan = slots[p.ItemId].RowSpan }).ToArray();
    }

    private static void ValidatePresented(ContentLayoutDocument document, ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        ContentLayoutSolver.ValidateViewport(viewport);
        if (snapshot.PosterId != document.PosterId || snapshot.DocumentVersion != document.DocumentVersion ||
            snapshot.ItemSetVersion != document.ItemSetVersion || snapshot.ViewportGeneration != viewport.Generation ||
            snapshot.ThemeMetricsVersion != viewport.ThemeMetricsVersion || snapshot.FlowAxis != document.FlowAxis ||
            snapshot.LoopScroll != document.LoopScroll || snapshot.ResolvedScale != viewport.Scale || snapshot.ResolvedSpacing != viewport.Spacing ||
            snapshot.AvailableCrossAxisDip != (document.FlowAxis == ContentFlowAxis.Vertical ? viewport.WidthDip : viewport.HeightDip) ||
            snapshot.CellDip != viewport.CellDip(document) || snapshot.GapDip != viewport.GapDip(document) ||
            snapshot.Items.Count != document.Placements.Count)
            throw new ContentLayoutException("The presented composition is stale, incomplete or belongs to another viewport/author.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var orders = new HashSet<int>();
        double pitch = snapshot.CellDip + snapshot.GapDip;
        foreach (var slot in snapshot.Items)
        {
            if (!ids.Add(slot.ItemId) || slot.SourceIndex < 0 || slot.SourceIndex >= document.Placements.Count ||
                document.Placements[slot.SourceIndex].ItemId != slot.ItemId ||
                slot.ReadOrder < 0 || slot.ReadOrder >= snapshot.Items.Count || !orders.Add(slot.ReadOrder) ||
                slot.Column < 0 || slot.Row < 0 || !ContentLayoutPolicy.ValidSpan(slot.ColumnSpan) ||
                !ContentLayoutPolicy.ValidSpan(slot.RowSpan))
                throw new ContentLayoutException("The presented composition has invalid member geometry or read order.");
            var source = document.Placements[slot.SourceIndex];
            var region = document.Regions.Single(r => r.Id == source.RegionId);
            if (slot.RegionColumn != region.Column || slot.RegionRow != region.Row ||
                slot.Column < region.Column || slot.Row < region.Row ||
                slot.Bounds != new ContentRect(slot.Column * pitch, slot.Row * pitch,
                    slot.ColumnSpan * pitch - snapshot.GapDip, slot.RowSpan * pitch - snapshot.GapDip) ||
                source.FixedPosition && (slot.Column != region.Column + source.Column || slot.Row != region.Row + source.Row))
                throw new ContentLayoutException("The presented composition cannot replace preserved region or hard-position intent.");
        }
        // Clipping is transient, but a supplied snapshot must witness the same viewport dimensions
        // and normalized scroll offsets as the interaction that is asking to capture it.
        var clipped = ContentLayoutSolver.Reclip(snapshot, viewport);
        if (clipped.ScrollXDip != snapshot.ScrollXDip || clipped.ScrollYDip != snapshot.ScrollYDip ||
            clipped.Items.Where((p, i) => p.Clip != snapshot.Items[i].Clip).Any())
            throw new ContentLayoutException("The presented composition no longer matches the interaction viewport.");
    }

    private static ContentLayoutDocument PushManualPeers(ContentLayoutDocument candidate, ContentLayoutSnapshot previous,
        string sourceId, ContentLayoutViewport viewport)
    {
        double cell = viewport.CellDip(candidate), gap = viewport.GapDip(candidate);
        double minimum = (ContentLayoutPolicy.MinimumReadableCardDip + gap) / (cell + gap);
        bool vertical = candidate.FlowAxis == ContentFlowAxis.Vertical;
        var previousById = previous.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var regions = candidate.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var pending = candidate.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        // Only explicitly managed keep-out areas block a drop. Decorative spaces are drawn but never
        // turn free space into an invisible obstacle, so ordinary empty ground stays droppable.
        ContentRect Normalize(double x, double y, double width, double height) => vertical
            ? new(x, y, width, height) : new(y, x, height, width);
        var held = candidate.ReservedSpaces.Where(s => s.Kind == ContentReservedKind.Explicit)
            .Select(s => Normalize(s.Column, s.Row, s.ColumnSpan, s.RowSpan)).ToList();
        double maximumCrossSpan = candidate.Placements.Select(p => Math.Max(minimum, vertical ? p.ColumnSpan : p.RowSpan)).DefaultIfEmpty(0).Max();
        bool onePerBand = maximumCrossSpan > 0 && previous.AvailableCrossAxisDip <= maximumCrossSpan * (cell + gap) - gap + 1e-7;
        ContentRect Rect(ContentPlacement p)
        {
            var r = regions[p.RegionId]; var old = previousById[p.ItemId];
            return Normalize(r.Column + (p.Column ?? old.Column - old.RegionColumn), r.Row + (p.Row ?? old.Row - old.RegionRow),
                Math.Ceiling(Math.Max(minimum, p.ColumnSpan)), Math.Ceiling(Math.Max(minimum, p.RowSpan)));
        }
        void RequireBounds(ContentRect rect, ContentPlacement p)
        {
            var r = regions[p.RegionId];
            double physicalX = vertical ? rect.X : rect.Y, physicalWidth = vertical ? rect.Width : rect.Height;
            if (rect.X < 0 || rect.Y < 0 || rect.Right > previous.CrossAxisCells || rect.Bottom > ContentLayoutPolicy.MaximumRow + 1d ||
                r.Columns > 0 && physicalX + physicalWidth > r.Column + r.Columns)
                throw new ContentLayoutException("The edit exceeds the available cross-axis space or preserved region bounds.");
        }
        foreach (var p in candidate.Placements.Where(p => p.ItemId == sourceId || p.FixedPosition)
            .OrderBy(p => p.ItemId == sourceId ? 1 : 0).ThenBy(p => p.ItemId, StringComparer.Ordinal))
        {
            var rect = Rect(p);
            RequireBounds(rect, p);
            if (onePerBand && rect.X != 0) throw new ContentLayoutException("The minimum cross-axis composition requires one card per band.");
            if (held.Any(h => h.Intersects(rect))) throw new ContentLayoutException("The edit conflicts with a fixed card or reserved space.");
            held.Add(rect);
        }
        foreach (var p in candidate.Placements.Where(p => p.ItemId != sourceId && !p.FixedPosition)
            .OrderBy(p => previousById[p.ItemId].ReadOrder))
        {
            var rect = Rect(p);
            if (onePerBand) rect = rect with { X = 0 };
            RequireBounds(rect, p);
            for (int steps = 0; ; steps++)
            {
                if (steps > ContentLayoutPolicy.MaximumItems + candidate.ReservedSpaces.Count)
                    throw new ContentLayoutException("Peer push exceeded the bounded dependency count.");
                var blockers = held.Where(h => h.Intersects(rect)).ToArray();
                if (blockers.Length == 0) break;
                rect = rect with { Y = blockers.Max(h => h.Bottom) };
                if (rect.Bottom > ContentLayoutPolicy.MaximumRow + 1d) throw new ContentLayoutException("Peer push exhausted the row range.");
            }
            var region = regions[p.RegionId];
            RequireBounds(rect, p);
            pending[p.ItemId] = p with { Column = (int)(vertical ? rect.X : rect.Y) - region.Column,
                Row = (int)(vertical ? rect.Y : rect.X) - region.Row };
            held.Add(rect);
        }
        return candidate with { Placements = Array.AsReadOnly(candidate.Placements.Select(p => pending[p.ItemId]).ToArray()) };
    }

    /// <summary>
    /// Optional/adjunct capability, deferred from the main flow of this batch: no command, UI entry or
    /// persistence migration is wired for it, and nothing on the normal path creates a region.
    /// The contract is kept for a later batch: explicit = blocking, visible in the editing state and
    /// always removable; decorative = drawn but never blocking.
    /// Agreed creation gesture (recorded here, deliberately not implemented yet): rubber-band selection
    /// inside the editing state followed by an explicit "keep this area" action; each region stays
    /// individually removable and no region is authored implicitly.
    /// These helpers only change intent; persistence still goes through the normal single-revision CAS
    /// path, and callers must resolve the result before committing.
    /// </summary>
    public static ContentLayoutDocument AddReservedSpace(ContentLayoutDocument document, ContentReservedSpace space)
    {
        if (space is null) throw new ContentLayoutException("A reserved space is required.");
        return Commit(document, document with { ReservedSpaces = Array.AsReadOnly(document.ReservedSpaces.Append(space).ToArray()) });
    }
    public static ContentLayoutDocument RemoveReservedSpace(ContentLayoutDocument document, ContentReservedSpace space)
    {
        if (space is null) throw new ContentLayoutException("A reserved space is required.");
        var kept = document.ReservedSpaces.Where(s => s != space).ToArray();
        return kept.Length == document.ReservedSpaces.Count
            ? document
            : Commit(document, document with { ReservedSpaces = Array.AsReadOnly(kept) });
    }
    public static ContentLayoutDocument ClearReservedSpaces(ContentLayoutDocument document) =>
        document.ReservedSpaces.Count == 0
            ? document
            : Commit(document, document with { ReservedSpaces = Array.Empty<ContentReservedSpace>() });

    private static ContentLayoutDocument Commit(ContentLayoutDocument before, ContentLayoutDocument candidate)
    {
        if (ContentLayoutPolicy.Validate(candidate) is { } error) throw new ContentLayoutException(error);
        return ContentLayoutPolicy.Freeze(candidate with { DocumentVersion = checked(before.DocumentVersion + 1) });
    }

    public static bool SameState(ContentLayoutDocument a, ContentLayoutDocument b, bool ignoreVersions = false)
    {
        if (ignoreVersions)
        {
            a = a with { DocumentVersion = 1, ItemSetVersion = 1, Placements = a.Placements.Select(p => p with { Version = 1 }).ToArray() };
            b = b with { DocumentVersion = 1, ItemSetVersion = 1, Placements = b.Placements.Select(p => p with { Version = 1 }).ToArray() };
        }
        return a.Placements.SequenceEqual(b.Placements) && a.Regions.SequenceEqual(b.Regions) && a.ReservedSpaces.SequenceEqual(b.ReservedSpaces) &&
            (a with { Placements = b.Placements, Regions = b.Regions, ReservedSpaces = b.ReservedSpaces }) == b;
    }
}

public sealed record ContentLayoutHistoryProposal(Guid Token, bool IsUndo, ContentLayoutDocument Before, ContentLayoutDocument Document);
/// <summary>Per-poster RAM history. Proposing never consumes history; only successful CAS confirmation does.</summary>
public sealed class ContentLayoutHistory
{
    public const int Capacity = 64;
    private sealed record Entry(ContentLayoutDocument Before, ContentLayoutDocument After);
    private readonly List<Entry> _undo = new(), _redo = new();
    private ContentLayoutHistoryProposal? _proposal;
    private string? _posterId;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public bool CanUndo(ContentLayoutDocument current) => CanPropose(current, true);
    public bool CanRedo(ContentLayoutDocument current) => CanPropose(current, false);
    private bool CanPropose(ContentLayoutDocument current, bool undo)
    {
        var stack = undo ? _undo : _redo;
        return stack.Count > 0 && _posterId == current.PosterId &&
            ContentLayoutEditing.SameState(undo ? stack[^1].After : stack[^1].Before, current, true);
    }
    public void Record(ContentLayoutDocument before, ContentLayoutDocument committedAfter)
    {
        if (before.PosterId != committedAfter.PosterId || _posterId is not null && _posterId != before.PosterId)
            throw new ContentLayoutException("History belongs to one poster.");
        string? error = ContentLayoutPolicy.Validate(before) ?? ContentLayoutPolicy.Validate(committedAfter);
        if (error is not null) throw new ContentLayoutException(error);
        if (ContentLayoutEditing.SameState(before, committedAfter, true)) return;
        if (committedAfter.DocumentVersion <= before.DocumentVersion) throw new ContentLayoutException("History requires a successful newer committed document.");
        _posterId = before.PosterId; _proposal = null;
        _undo.Add(new(ContentLayoutPolicy.Freeze(before), ContentLayoutPolicy.Freeze(committedAfter)));
        if (_undo.Count > Capacity) _undo.RemoveAt(0);
        _redo.Clear();
    }
    public ContentLayoutHistoryProposal? ProposeUndo(ContentLayoutDocument current) => Propose(current, true);
    public ContentLayoutHistoryProposal? ProposeRedo(ContentLayoutDocument current) => Propose(current, false);
    private ContentLayoutHistoryProposal? Propose(ContentLayoutDocument current, bool undo)
    {
        var stack = undo ? _undo : _redo;
        if (stack.Count == 0 || _posterId != current.PosterId) return null;
        var entry = stack[^1]; var expected = undo ? entry.After : entry.Before;
        if (!ContentLayoutEditing.SameState(expected, current, true)) return null;
        var target = undo ? entry.Before : entry.After;
        var placements = current.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        bool membershipChanged = !placements.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(target.Placements.Select(p => p.ItemId));
        target = ContentLayoutPolicy.Freeze(target with { DocumentVersion = checked(current.DocumentVersion + 1),
            ItemSetVersion = checked(current.ItemSetVersion + (membershipChanged ? 1 : 0)),
            Placements = target.Placements.Select(p => !placements.TryGetValue(p.ItemId, out var prior)
                ? p with { Version = 1 }
                : p with { Version = checked(prior.Version + ((p with { Version = prior.Version }) == prior ? 0 : 1)) }).ToArray() });
        return _proposal = new(Guid.NewGuid(), undo, ContentLayoutPolicy.Freeze(current), target);
    }
    public bool Confirm(ContentLayoutHistoryProposal proposal, ContentLayoutDocument committed)
    {
        if (_proposal != proposal || committed.DocumentVersion <= proposal.Before.DocumentVersion ||
            !ContentLayoutEditing.SameState(proposal.Document, committed)) return false;
        var source = proposal.IsUndo ? _undo : _redo; var destination = proposal.IsUndo ? _redo : _undo;
        if (source.Count == 0) return false;
        destination.Add(source[^1]); source.RemoveAt(source.Count - 1); _proposal = null; return true;
    }
    public void CancelProposal() => _proposal = null;
    public void Clear() { _undo.Clear(); _redo.Clear(); _proposal = null; _posterId = null; }
}
