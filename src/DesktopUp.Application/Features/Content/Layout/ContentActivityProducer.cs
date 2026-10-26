using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class ContentActivityProducer(IContentActivityStore store, ContentActivityConsumer consumer)
{
    public async Task<ContentHeatProposal?> ProduceAsync(ContentLayoutDocument author, ContentCatalogSnapshot catalog,
        long version, DateTimeOffset now, CancellationToken cancellation)
    {
        // Membership is supplied from one immutable committed projection, never resolved by path.
        var relations = author.Placements.Select(p => ContentRelationPolicy.Resolve(catalog, author.PosterId, p.ItemId)).ToArray();
        if (relations.Any(x => x is null) || relations.Length == 0) return null;
        consumer.RequestRefresh();
        await consumer.WhenIdleAsync().WaitAsync(cancellation).ConfigureAwait(false);
        var snapshot = await Task.Run(() => store.Read(relations.Select(x => x!.Item.Id).Distinct().ToArray()), cancellation).ConfigureAwait(false);
        if (snapshot.HasPending || relations.Any(x => !snapshot.Items.ContainsKey(x!.Item.Id))) return null;
        var scores = ContentActivityPolicy.Evaluate(author.Placements.Select((p, i) =>
            new ContentActivityInput(p.ItemId, p.Order, snapshot.Items[relations[i]!.Item.Id])).ToArray(), author.Activity, now);
        return new(author.PosterId, author.ItemSetVersion, version,
            scores.Select(x => new ContentHeatItem(x.ItemId, x.Rank, x.RelativeScale, "real-activity-v1")).ToArray(),
            ContentLayoutProposals.ContinuousMappingVersion) { AuthorDocumentVersion = author.DocumentVersion };
    }
}
