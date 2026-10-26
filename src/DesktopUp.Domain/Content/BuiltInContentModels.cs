namespace DesktopUp.Domain;

public sealed class ActivityContentModel : IContentBehaviorModel
{
    public ContentModelDescriptor Descriptor { get; } = new(ContentModelProtocol.BuiltInId, 1, "综合热度",
        ContentModelCapabilities.Ranking | ContentModelCapabilities.Scaling);

    public ContentModelOutput Evaluate(ContentModelInput input, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return new(Array.AsReadOnly(ContentActivityPolicy.Evaluate(input.Items, input.Author.Activity, input.Now)
            .Select(x => new ContentModelItem(x.ItemId, x.Rank, x.RelativeScale, Reason: "real-activity-v1")).ToArray()));
    }
}

// Independent model using the same facts and continuous size policy. Placement is a suggestion,
// not a new layout engine: the host may discard this strip when protected authoring or space wins.
public sealed class RecentStripContentModel : IContentBehaviorModel
{
    public ContentModelDescriptor Descriptor { get; } = new(ContentModelProtocol.RecentId, 1, "最近使用 · 条带",
        ContentModelProtocol.All);

    public ContentModelOutput Evaluate(ContentModelInput input, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var scales = ContentActivityPolicy.Evaluate(input.Items, input.Author.Activity, input.Now)
            .ToDictionary(x => x.ItemId, x => x.RelativeScale, StringComparer.Ordinal);
        var sorted = input.Items.OrderByDescending(x => x.Usage.LastLaunchedAt).ThenBy(x => x.PreviousOrder)
            .ThenBy(x => x.ItemId, StringComparer.Ordinal).ToArray();
        // Obtain host-equivalent continuous dimensions before suggesting non-overlapping slots.
        var sizes = new ContentHeatProposal(input.Author.PosterId, input.Author.ItemSetVersion, input.RequestVersion,
            sorted.Select((x, i) => new ContentHeatItem(x.ItemId, i, scales[x.ItemId])).ToArray(), 2)
        { AuthorDocumentVersion = input.Author.DocumentVersion };
        var projected = ContentHeatSizing.Project(input.Author, sizes, input.Viewport).Placements.ToDictionary(x => x.ItemId);
        bool vertical = input.Author.FlowAxis == ContentFlowAxis.Vertical;
        int main = 0;
        var result = new List<ContentModelItem>(sorted.Length);
        foreach (var (item, rank) in sorted.Select((item, rank) => (item, rank)))
        {
            cancellation.ThrowIfCancellationRequested();
            var placement = projected[item.ItemId];
            var position = main <= 65535 ? new ContentModelPosition(vertical ? 0 : main, vertical ? main : 0) : null;
            result.Add(new(item.ItemId, rank, scales[item.ItemId], position, "recent-strip-v1"));
            main += (int)Math.Ceiling(vertical ? placement.RowSpan : placement.ColumnSpan) + 1;
        }
        return new(Array.AsReadOnly(result.ToArray()), new(ContentModelTransitionKind.Fade));
    }
}
