namespace DesktopUp.Domain;

// Derives pixels from author intent every time. Only the scrolling cross axis constrains size;
// both dimensions use one multiplier so a narrow frame cannot distort an item's aspect ratio.
public static class ContentHeatSizing
{
    public static ContentLayoutDocument Project(ContentLayoutDocument author, ContentHeatProposal proposal,
        ContentLayoutViewport viewport, bool constrainToViewport = true)
    {
        ContentLayoutProposals.Validate(proposal);
        if (proposal.MappingVersion != ContentLayoutProposals.ContinuousMappingVersion ||
            proposal.PosterId != author.PosterId || proposal.ItemSetVersion != author.ItemSetVersion)
            throw new ContentLayoutException("Continuous heat does not match the current membership.");
        var heat = proposal.Items.ToDictionary(x => x.ItemId, StringComparer.Ordinal);
        if (heat.Keys.Except(author.Placements.Select(x => x.ItemId), StringComparer.Ordinal).Any())
            throw new ContentLayoutException("Heat includes a removed content item.");
        return Transform(author, viewport, p => heat.TryGetValue(p.ItemId, out var h) ? h : null, constrainToViewport);
    }

    public static ContentLayoutDocument LowerBound(ContentLayoutDocument author, ContentLayoutViewport viewport) =>
        Transform(author, viewport, p => new(p.ItemId, p.Order, author.Activity.MinimumScale), false);

    private static ContentLayoutDocument Transform(ContentLayoutDocument author, ContentLayoutViewport viewport,
        Func<ContentPlacement, ContentHeatItem?> readHeat, bool constrain)
    {
        if (ContentLayoutPolicy.Validate(author) is { } error) throw new ContentLayoutException(error);
        if (author.Mode == ContentLayoutMode.Manual) return author;
        double gap = viewport.GapDip(author);
        double pitch = viewport.CellDip(author) + gap;
        if (!double.IsFinite(pitch) || pitch <= 0 || !double.IsFinite(gap) || gap < 0)
            throw new ContentLayoutException("Invalid heat geometry metrics.");
        bool vertical = author.FlowAxis == ContentFlowAxis.Vertical;
        double available = vertical ? viewport.WidthDip : viewport.HeightDip;
        var placements = author.Placements.Select(p =>
        {
            if (readHeat(p) is not { } h) return p;
            var ordered = p with { Order = h.Rank };
            if (!author.AllowHeatDrivenSize || p.ManualSize || p.FixedPosition) return ordered;
            double width = Math.Max(ContentLayoutPolicy.MinimumReadableCardDip, p.ColumnSpan * pitch - gap);
            double height = Math.Max(ContentLayoutPolicy.MinimumReadableCardDip, p.RowSpan * pitch - gap);
            double lower = Math.Max(author.Activity.MinimumScale,
                Math.Max(ContentLayoutPolicy.MinimumReadableCardDip / width, ContentLayoutPolicy.MinimumReadableCardDip / height));
            double spanLimit = ContentLayoutPolicy.MaximumSpan * pitch - gap;
            double crossLimit = author.MaximumColumns * pitch - gap;
            double upper = Math.Min(author.Activity.MaximumScale, Math.Min(spanLimit / width, spanLimit / height));
            upper = Math.Min(upper, crossLimit / (vertical ? width : height));
            if (constrain) upper = Math.Min(upper, available / (vertical ? width : height));
            if (!double.IsFinite(upper) || upper + 1e-9 < lower)
                throw new ContentLayoutException("Heat has no readable interval in the available frame.");
            double scale = Math.Clamp(h.RelativeScale, lower, Math.Max(lower, upper));
            return ordered with { ColumnSpan = (width * scale + gap) / pitch, RowSpan = (height * scale + gap) / pitch };
        }).ToArray();
        bool resized = placements.Where((p, i) => p.ColumnSpan != author.Placements[i].ColumnSpan ||
            p.RowSpan != author.Placements[i].RowSpan).Any();
        if (resized && author.SchemaVersion < 3 && author.Placements.Any(p => p.PinnedIndex.HasValue))
            throw new ContentLayoutException("Legacy dormant pins require explicit conversion before continuous heat sizing.");
        return ContentLayoutPolicy.Freeze(author with { Placements = placements,
            SchemaVersion = resized ? ContentLayoutPolicy.SupportedSchemaVersion : author.SchemaVersion });
    }
}
