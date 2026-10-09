namespace DesktopUp.Application;

// Transient selection, separate from author layout, order pins and activation receipts.
public sealed record ContentSelection(IReadOnlyList<string> Ids, string? Anchor, string? Focus)
{
    // A modifier/Select All selection is visually explicit even when it contains one item.
    // Ordinary focus/click still selects a target, but does not paint a browsing outline.
    public bool ShowHighlight { get; init; }
    public static ContentSelection Empty { get; } = new([], null, null);
    public ContentSelection Select(IReadOnlyList<string> order, string? id, bool control = false, bool shift = false)
    {
        var current = Prune(order);
        if (id is null || !order.Contains(id)) return Empty;
        var chosen = current.Ids.ToHashSet(StringComparer.Ordinal);
        if (shift && current.Anchor is { } anchor)
        {
            if (!control) chosen.Clear();
            int a = order.ToList().IndexOf(anchor), b = order.ToList().IndexOf(id);
            foreach (string member in order.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1)) chosen.Add(member);
        }
        else if (control) { if (!chosen.Add(id)) chosen.Remove(id); }
        else { chosen.Clear(); chosen.Add(id); }
        return new(order.Where(chosen.Contains).Distinct().ToArray(), shift ? current.Anchor ?? id : id,
            chosen.Contains(id) ? id : order.LastOrDefault(chosen.Contains)) { ShowHighlight = chosen.Count > 0 && (control || shift) };
    }
    public ContentSelection Prune(IReadOnlyList<string> order)
    {
        var kept = order.Where(Ids.Contains).Distinct().ToArray();
        return new(kept, Anchor is not null && order.Contains(Anchor) ? Anchor : kept.FirstOrDefault(),
            Focus is not null && kept.Contains(Focus) ? Focus : kept.LastOrDefault()) { ShowHighlight = kept.Length > 0 && ShowHighlight };
    }
}
