using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class ContentActivityProducer(IContentActivityStore store, ContentActivityConsumer consumer,
    ContentModelRegistry? models = null)
{
    private readonly ContentModelRegistry registry = models ?? ContentModelRegistry.BuiltIns();
    public IReadOnlyList<ContentModelDescriptor> Models => registry.Models;

    public async Task<ContentHeatProposal?> ProduceAsync(ContentLayoutDocument author, ContentCatalogSnapshot catalog,
        long version, DateTimeOffset now, CancellationToken cancellation, ContentLayoutViewport? viewport = null,
        ContentLayoutSnapshot? currentLayout = null)
    {
        // Membership is supplied from one immutable committed projection, never resolved by path.
        if (author.Placements.Count == 0) return null;
        var relations = ContentRelationPolicy.ResolveMany(catalog, author.PosterId,
            author.Placements.Select(p => p.ItemId).ToArray());
        if (relations.Any(x => x is null)) return null;
        consumer.RequestRefresh();
        await consumer.WhenIdleAsync().WaitAsync(cancellation).ConfigureAwait(false);
        var snapshot = await Task.Run(() => store.Read(relations.Select(x => x!.Item.Id).Distinct().ToArray()), cancellation).ConfigureAwait(false);
        if (snapshot.HasPending || relations.Any(x => !snapshot.Items.ContainsKey(x!.Item.Id))) return null;
        var input = new ContentModelInput(author, Array.AsReadOnly(author.Placements.Select((p, i) =>
            new ContentActivityInput(p.ItemId, p.Order, snapshot.Items[relations[i]!.Item.Id])).ToArray()),
            viewport ?? new(10000, 10000), now, version) { CurrentLayout = currentLayout };
        return await registry.EvaluateAsync(input, cancellation).ConfigureAwait(false);
    }
}
