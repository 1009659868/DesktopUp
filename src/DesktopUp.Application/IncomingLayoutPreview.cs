using DesktopUp.Domain;

namespace DesktopUp.Application;

// Shared by persisted reference additions and transient OLE previews. No catalog or file I/O.
public static class ContentMemberPlacement
{
    public static ContentPlacement Append(ContentLayoutDocument layout, IReadOnlyList<ContentPlacement> placements, string id)
    {
        int order = placements.Count == 0 ? 0 : checked(placements.Max(p => p.Order) + 1);
        var placement = new ContentPlacement(id, order, Math.Min(4, layout.MaximumColumns), 4)
            { RegionId = layout.Regions.OrderBy(r => r.Order).First().Id };
        if (layout.Mode == ContentLayoutMode.Manual)
        {
            bool vertical = layout.FlowAxis == ContentFlowAxis.Vertical;
            int end = placements.Select(p => vertical ? (p.Row ?? 0) + p.RowSpan : (p.Column ?? 0) + p.ColumnSpan)
                .Concat(layout.ReservedSpaces.Select(s => vertical ? s.Row + s.RowSpan : s.Column + s.ColumnSpan)).DefaultIfEmpty(0).Max();
            placement = placement with { Column = vertical ? 0 : end, Row = vertical ? end : 0,
                SavedAutomaticState = layout.SavedAutomaticMode is null ? null : new(order, placement.ColumnSpan, placement.RowSpan, false, null) };
        }
        return placement;
    }
}

public sealed record IncomingLayoutPreview(ContentLayoutDocument Document, ContentLayoutSnapshot Snapshot,
    IReadOnlySet<string> Placeholders)
{
    // Predict the same successive add -> land -> advance sequence used by RunIncoming.
    // IDs never leave this object; a preview is not a commit plan or evidence of file ownership.
    public static IncomingLayoutPreview Create(ContentLayoutDocument basis, IncomingContentPlacement landing, int count)
    {
        if (count is < 1 or > 100 || basis.Placements.Count + count > ContentLayoutPolicy.MaximumItems)
            throw new ArgumentException("Incoming preview exceeds the content budget.");
        var document = ContentLayoutPolicy.Freeze(basis);
        var placeholders = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string id = basis.PosterId + ":incoming-preview:" + i;
            if (basis.Placements.Any(p => p.ItemId == id)) throw new ArgumentException("Preview identity collision.");
            placeholders.Add(id);
            document = ContentLayoutPolicy.Freeze(document with {
                Placements = document.Placements.Append(ContentMemberPlacement.Append(document, document.Placements, id)).ToArray(),
                DocumentVersion = checked(document.DocumentVersion + 1), ItemSetVersion = checked(document.ItemSetVersion + 1) });
            document = landing.Apply(document, id);
            if (i + 1 < count) landing = landing.Advance(document, id);
        }
        var snapshot = ContentLayoutSolver.Resolve(document, landing.Viewport);
        if (snapshot.IsLayoutReadOnly) throw new ArgumentException("Incoming preview cannot fit this content space.");
        return new(document, snapshot, placeholders);
    }
}
