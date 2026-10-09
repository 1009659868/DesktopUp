using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentRelationWitness(Guid PosterItemId, string PosterId, long PosterItemVersion,
    Guid MembershipId, long MembershipVersion, Guid CollectionId, long CollectionVersion,
    Guid ContentItemId, long ItemVersion, long BindingVersion);
public sealed record ContentRelationResolution(ContentItem Item, PosterItem PosterItem, ContentRelationWitness Witness);
public sealed record ContentDetachPlan(Guid RequestId, ContentRelationWitness ReadSet, string RemovedLayoutItemId);

// Pure preparation, NOT a second state store. The future coordinator must check the witness
// again inside the existing SQLite transaction and remove the matching layout placement atomically.
public static class ContentRelationPolicy
{
    public static ContentRelationResolution? Resolve(ContentCatalogSnapshot snapshot, string posterId, string layoutItemId)
    {
        var catalog = ContentCatalogPolicy.Freeze(snapshot);
        var relation = catalog.PosterItems.SingleOrDefault(x => x.PosterId == posterId && x.LayoutItemId == layoutItemId);
        if (relation is null) return null;
        var member = catalog.Memberships.Single(x => x.Id == relation.MembershipId);
        var item = catalog.Items.Single(x => x.Id == member.ContentItemId);
        var collection = catalog.Collections.Single(x => x.Id == member.CollectionId);
        var binding = catalog.Bindings.Single(x => x.PosterId == posterId);
        return new(item, relation, new(relation.Id, posterId, relation.Version, member.Id, member.Version,
            collection.Id, collection.Version, item.Id, item.Version, binding.Version));
    }

    public static bool IsCurrent(ContentCatalogSnapshot snapshot, ContentRelationWitness witness)
    {
        var current = Resolve(snapshot, witness.PosterId,
            new PosterItem(witness.PosterItemId, witness.PosterId, witness.MembershipId).LayoutItemId);
        return current?.Witness == witness;
    }

    public static ContentDetachPlan? PrepareDetach(ContentCatalogSnapshot snapshot, Guid requestId, ContentRelationWitness witness)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("An explicit request identity is required.");
        return IsCurrent(snapshot, witness)
            ? new(requestId, witness, new PosterItem(witness.PosterItemId, witness.PosterId, witness.MembershipId).LayoutItemId)
            : null;
    }
}
