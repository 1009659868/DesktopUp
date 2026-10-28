using DesktopUp.Domain;

namespace DesktopUp.Application;

// Frozen landing intent belongs to the transfer journal, not to the permanent layout schema.
public sealed record IncomingContentPlacement(ContentLayoutViewport Viewport, int? ReadOrder = null,
    int? Column = null, int? Row = null)
{
    public static IncomingContentPlacement? From(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        IncomingDropLocation location)
    {
        if (location.Area == IncomingDropArea.Frame) return null;
        double x = location.ContentX ?? throw new ArgumentException("Missing content point."),
            y = location.ContentY ?? throw new ArgumentException("Missing content point.");
        double pitch = snapshot.CellDip + snapshot.GapDip;
        int column=checked((int)Math.Floor(x/pitch)),row=checked((int)Math.Floor(y/pitch));
        int crossSpan=Math.Max(4,(int)Math.Ceiling((ContentLayoutPolicy.MinimumReadableCardDip+snapshot.GapDip)/pitch));
        if(snapshot.FlowAxis==ContentFlowAxis.Vertical)column=Math.Clamp(column,0,Math.Max(0,snapshot.CrossAxisCells-crossSpan));
        else row=Math.Clamp(row,0,Math.Max(0,snapshot.CrossAxisCells-crossSpan));
        return new(viewport, ContentDragPolicy.FindInsertionReadOrder(snapshot, x, y),
            column,row);
    }

    public IncomingContentPlacement Advance(ContentLayoutDocument committed, string addedId)
    {
        var s = ContentLayoutSolver.Resolve(committed, Viewport);
        var added = s.Items.Single(x => x.ItemId == addedId);
        return this with { ReadOrder = added.ReadOrder + 1,
            Column = committed.FlowAxis == ContentFlowAxis.Vertical ? Column : added.Column + (int)Math.Ceiling(added.ColumnSpan),
            Row = committed.FlowAxis == ContentFlowAxis.Vertical ? added.Row + (int)Math.Ceiling(added.RowSpan) : Row };
    }

    public ContentLayoutDocument Apply(ContentLayoutDocument withNewMember, string id)
    {
        var snapshot = ContentLayoutSolver.Resolve(withNewMember, Viewport);
        var placement = withNewMember.Placements.Single(x => x.ItemId == id);
        var region = withNewMember.Regions.Single(x => x.Id == placement.RegionId);
        var edit = withNewMember.Mode == ContentLayoutMode.Manual
            ? new ContentLayoutEdit(ContentLayoutEditKind.Move, id) { Column = Column - region.Column, Row = Row - region.Row }
            : new ContentLayoutEdit(ContentLayoutEditKind.Move, id) { TargetReadOrder = Math.Min(ReadOrder ?? 0, withNewMember.Placements.Count - 1) };
        var result = ContentLayoutEditing.Apply(withNewMember, edit, Viewport, snapshot);
        if (!result.Accepted) throw new IOException(result.Error);
        var document = result.Document;
        // Insertion is explicit even when the appended item is already at the chosen read position.
        if (withNewMember.Mode != ContentLayoutMode.Manual && document.Placements.Single(x => x.ItemId == id).PinnedIndex is null)
        {
            result = ContentLayoutEditing.Apply(document, new(ContentLayoutEditKind.Pinned, id)
                { Enabled = true, PinnedIndex = Math.Min(ReadOrder ?? 0, document.Placements.Count - 1) + 1 }, Viewport);
            if (!result.Accepted) throw new IOException(result.Error);
            document = result.Document;
        }
        // Addition and its landing are one author edit and one database commit.
        return ContentLayoutPolicy.Freeze(document with { DocumentVersion = withNewMember.DocumentVersion,
            Placements = document.Placements.Select(p => p.ItemId == id ? p with { Version = 1 } : p).ToArray() });
    }
}
