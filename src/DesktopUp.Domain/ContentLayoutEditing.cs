namespace DesktopUp.Domain;

public enum ContentLayoutEditKind { Move, Resize, Fixed, Pinned, Mode, Guides, SizeOverride }
public sealed record ContentLayoutEdit(ContentLayoutEditKind Kind, string? ItemId = null)
{
    public int? Column { get; init; }
    public int? Row { get; init; }
    public int? ColumnSpan { get; init; }
    public int? RowSpan { get; init; }
    public int? PinnedIndex { get; init; }
    public bool? Enabled { get; init; }
    public bool? ShowBaselineGrid { get; init; }
    public bool? ShowSpanLabels { get; init; }
    public ContentLayoutMode? Mode { get; init; }
    public ManualLayoutExit? ManualExit { get; init; }
}
public sealed record ContentLayoutEditResult(bool Accepted, ContentLayoutDocument Document, string? Error = null);

public static class ContentLayoutEditing
{
    public static ContentLayoutEditResult Apply(ContentLayoutDocument document, ContentLayoutEdit edit, ContentLayoutViewport viewport)
    {
        try
        {
            if (!Enum.IsDefined(edit.Kind)) throw new ContentLayoutException("Unknown edit action.");
            var before = ContentLayoutSolver.Resolve(document, viewport);
            var candidate = document;
            var items = document.Placements.ToArray();
            int index = Array.FindIndex(items, p => p.ItemId == edit.ItemId);
            bool itemAction = edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize or ContentLayoutEditKind.Fixed or ContentLayoutEditKind.Pinned or ContentLayoutEditKind.SizeOverride;
            if (itemAction && index < 0) throw new ContentLayoutException("The edited item no longer belongs to this poster.");
            var source = index < 0 ? null : items[index];
            switch (edit.Kind)
            {
                case ContentLayoutEditKind.Move:
                    if (document.Mode == ContentLayoutMode.Automatic)
                        throw new ContentLayoutException("Choose manual mode or explicitly fix this item before moving it.");
                    if (edit.Column is null || edit.Row is null) throw new ContentLayoutException("Move requires a grid position.");
                    items[index] = source! with { Column = edit.Column, Row = edit.Row,
                        FixedPosition = document.Mode == ContentLayoutMode.Anchored || source!.FixedPosition };
                    break;
                case ContentLayoutEditKind.Resize:
                    if (edit.ColumnSpan is null || edit.RowSpan is null) throw new ContentLayoutException("Resize requires both integer spans.");
                    items[index] = source! with { ColumnSpan = edit.ColumnSpan.Value, RowSpan = edit.RowSpan.Value, ManualSize = true };
                    break;
                case ContentLayoutEditKind.SizeOverride:
                    if (edit.Enabled is null) throw new ContentLayoutException("Size override requires an explicit enabled value.");
                    items[index] = source! with { ManualSize = edit.Enabled.Value,
                        ColumnSpan = edit.Enabled.Value ? source!.ColumnSpan : 4,
                        RowSpan = edit.Enabled.Value ? source!.RowSpan : 4 };
                    break;
                case ContentLayoutEditKind.Fixed:
                    if (edit.Enabled is null) throw new ContentLayoutException("Fixed requires an explicit enabled value.");
                    if (edit.Enabled == true)
                    {
                        var resolved = before.Items.Single(p => p.ItemId == source!.ItemId);
                        var region = document.Regions.Single(r => r.Id == source!.RegionId);
                        if (resolved.ColumnSpan < source!.ColumnSpan || resolved.RegionColumn != region.Column || resolved.RegionRow != region.Row)
                            throw new ContentLayoutException("This region/card is responsive; widen the poster or enter manual mode before fixing its position.");
                        items[index] = source with { FixedPosition = true, Column = resolved.Column - resolved.RegionColumn, Row = resolved.Row - resolved.RegionRow };
                        if (candidate.Mode == ContentLayoutMode.Automatic) candidate = candidate with { Mode = ContentLayoutMode.Anchored };
                    }
                    else items[index] = source! with { FixedPosition = false };
                    break;
                case ContentLayoutEditKind.Pinned:
                    if (edit.Enabled is null) throw new ContentLayoutException("Pinned requires an explicit enabled value.");
                    if (edit.Enabled == true)
                    {
                        int pin = edit.PinnedIndex ?? before.Items.Single(p => p.ItemId == source!.ItemId).ReadOrder + 1;
                        var occupied = items.Where(p => p.ItemId != source!.ItemId && p.PinnedIndex.HasValue).Select(p => p.PinnedIndex!.Value).ToHashSet();
                        if (pin < 1) throw new ContentLayoutException("Order anchors are one-based.");
                        while (pin <= items.Length && occupied.Contains(pin)) pin++;
                        if (pin > items.Length) throw new ContentLayoutException("No following order-anchor slot is available.");
                        items[index] = source! with { PinnedIndex = pin };
                        if (candidate.Mode == ContentLayoutMode.Automatic) candidate = candidate with { Mode = ContentLayoutMode.Anchored };
                    }
                    else items[index] = source! with { PinnedIndex = null };
                    break;
                case ContentLayoutEditKind.Mode:
                    if (edit.Mode is not { } mode || !Enum.IsDefined(mode)) throw new ContentLayoutException("An explicit layout mode is required.");
                    if (mode == document.Mode) return new(true, document);
                    if (document.Mode == ContentLayoutMode.Manual)
                    {
                        if (edit.ManualExit is not { } exit || !Enum.IsDefined(exit))
                            throw new ContentLayoutException("Manual layout is retained until an exit strategy is selected.");
                        var order = before.Items.ToDictionary(p => p.ItemId, p => p.ReadOrder, StringComparer.Ordinal);
                        items = items.Select(p => p with { Order = order[p.ItemId], Column = null, Row = null,
                            FixedPosition = false, PinnedIndex = exit == ManualLayoutExit.ConvertToAnchors ? order[p.ItemId] + 1 : null,
                            ManualSize = exit == ManualLayoutExit.RestoreAutomatic ? false : p.ManualSize,
                            ColumnSpan = exit == ManualLayoutExit.RestoreAutomatic ? 4 : p.ColumnSpan,
                            RowSpan = exit == ManualLayoutExit.RestoreAutomatic ? 4 : p.RowSpan }).ToArray();
                        candidate = candidate with { Mode = exit == ManualLayoutExit.ConvertToAnchors ? ContentLayoutMode.Anchored : ContentLayoutMode.Automatic };
                    }
                    else if (mode == ContentLayoutMode.Manual)
                    {
                        var resolved = before.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
                        items = items.Select(p => p with { Column = resolved[p.ItemId].Column - resolved[p.ItemId].RegionColumn,
                            Row = resolved[p.ItemId].Row - resolved[p.ItemId].RegionRow,
                            ColumnSpan = resolved[p.ItemId].ColumnSpan, RowSpan = resolved[p.ItemId].RowSpan,
                            Order = resolved[p.ItemId].ReadOrder, ManualSize = true }).ToArray();
                        // Explicitly entering manual mode captures the currently displayed regional composition.
                        var regions = candidate.Regions.Select(r => before.Items.FirstOrDefault(i => document.Placements[i.SourceIndex].RegionId == r.Id) is { } item
                            ? r with { Column = item.RegionColumn, Row = item.RegionRow } : r).ToArray();
                        candidate = candidate with { Mode = mode, Regions = Array.AsReadOnly(regions) };
                    }
                    else candidate = candidate with { Mode = mode };
                    break;
                case ContentLayoutEditKind.Guides:
                    candidate = candidate with { ShowBaselineGrid = edit.ShowBaselineGrid ?? candidate.ShowBaselineGrid,
                        ShowSpanLabels = edit.ShowSpanLabels ?? candidate.ShowSpanLabels };
                    break;
            }
            candidate = candidate with { Placements = Array.AsReadOnly(items) };
            if (ContentLayoutPolicy.Validate(candidate) is { } validation) throw new ContentLayoutException(validation);
            if (candidate.Mode == ContentLayoutMode.Manual && edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize or ContentLayoutEditKind.SizeOverride)
                candidate = PushManualPeers(candidate, before, edit.ItemId!, viewport);
            ContentLayoutSolver.Resolve(candidate, viewport);
            if (SameState(document, candidate)) return new(true, document);
            var old = document.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
            candidate = candidate with { DocumentVersion = checked(document.DocumentVersion + 1),
                Placements = Array.AsReadOnly(candidate.Placements.Select(p => p == old[p.ItemId] ? p : p with { Version = checked(old[p.ItemId].Version + 1) }).ToArray()) };
            return new(true, ContentLayoutPolicy.Freeze(candidate));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        { return new(false, document, error.Message); }
    }

    private static ContentLayoutDocument PushManualPeers(ContentLayoutDocument candidate, ContentLayoutSnapshot previous,
        string sourceId, ContentLayoutViewport viewport)
    {
        double cell = candidate.BaseCellDip * viewport.Scale, gap = candidate.BaseGapDip * viewport.Spacing;
        int minimum = (int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip + gap) / (cell + gap));
        var previousById = previous.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var regions = candidate.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var pending = candidate.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var held = candidate.ReservedSpaces.Select(s => new ContentRect(s.Column, s.Row, s.ColumnSpan, s.RowSpan)).ToList();
        ContentRect Rect(ContentPlacement p)
        {
            var r = regions[p.RegionId]; var old = previousById[p.ItemId];
            return new(r.Column + (p.Column ?? old.Column - old.RegionColumn), r.Row + (p.Row ?? old.Row - old.RegionRow),
                Math.Max(minimum, p.ColumnSpan), Math.Max(minimum, p.RowSpan));
        }
        foreach (var p in candidate.Placements.Where(p => p.ItemId == sourceId || p.FixedPosition)
            .OrderBy(p => p.ItemId == sourceId ? 1 : 0).ThenBy(p => p.ItemId, StringComparer.Ordinal))
        {
            var rect = Rect(p);
            if (held.Any(h => h.Intersects(rect))) throw new ContentLayoutException("The edit conflicts with a fixed card or reserved space.");
            held.Add(rect);
        }
        foreach (var p in candidate.Placements.Where(p => p.ItemId != sourceId && !p.FixedPosition)
            .OrderBy(p => previousById[p.ItemId].ReadOrder))
        {
            var rect = Rect(p);
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
            pending[p.ItemId] = p with { Column = (int)rect.X - region.Column, Row = (int)rect.Y - region.Row };
            held.Add(rect);
        }
        return candidate with { Placements = Array.AsReadOnly(candidate.Placements.Select(p => pending[p.ItemId]).ToArray()) };
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
