using DesktopUp.Domain;

namespace DesktopUp.Application;

// One atomic view handoff. Filesystem proof and storage membership changes belong to
// the infrastructure caller; this reuses the same removal and incoming layout rules.
public static class CrossPosterPlacement
{
    public static StateMutationPlan Prepare(DesktopStateSnapshot state, Guid sourceCardId,
        string targetId, ContentItem destination, IncomingContentPlacement? landing)
    {
        var old = state.ContentCatalog.PosterItems.Single(p => p.Id == sourceCardId);
        if (old.PosterId == targetId) throw new ArgumentException("Return-to-origin belongs to the existing layout gesture.");
        // Windows may have replaced a same-name target. Its former card is no longer
        // the owner of that physical object; remove it in the same transaction.
        var replaced = state.ContentCatalog.PosterItems.Where(p => p.PosterId == targetId &&
            ContentRelationPolicy.Resolve(state.ContentCatalog, targetId, p.LayoutItemId) is { } r &&
            r.Item.Id != destination.Id && ContentSingleOwnerPolicy.Key(r.Item) == ContentSingleOwnerPolicy.Key(destination)).Select(p => p.Id);
        var removal = ContentRelationRemoval.Prepare(state, replaced.Prepend(sourceCardId).ToArray());
        if (removal.Rejection is not null) return removal;
        var catalog = removal.ContentCatalog! with { Items = state.ContentCatalog.Items.Select(i => i.Id == destination.Id ? destination : i).ToArray() };
        var target = removal.Updates.FirstOrDefault(p => p.PosterId == targetId) ?? state.Catalog.Posters.Single(p => p.Poster.PosterId == targetId).Poster;
        var binding = catalog.Bindings.SingleOrDefault(b => b.PosterId == targetId);
        StateMutationPlan addition;
        if (binding is not null && catalog.Collections.Single(c => c.Id == binding.CollectionId).Kind == ContentCollectionKind.Mapped)
        {
            if (!ContentLayoutPolicy.TryRead(target.ContentLayoutJson, targetId, out var layout, out var error)) throw new ArgumentException(error);
            var member = catalog.Memberships.SingleOrDefault(m => m.CollectionId == binding.CollectionId && m.ContentItemId == destination.Id)
                ?? new CollectionMembership(Guid.NewGuid(), binding.CollectionId, destination.Id, ContentMembershipOrigin.Source);
            var card = new PosterItem(Guid.NewGuid(), targetId, member.Id);
            var next = layout! with { Placements = layout.Placements.Append(ContentMemberPlacement.Append(layout, layout.Placements, card.LayoutItemId)).ToArray(),
                DocumentVersion = checked(layout.DocumentVersion + 1), ItemSetVersion = checked(layout.ItemSetVersion + 1) };
            addition = new() { ContentCatalog = catalog with { Memberships = catalog.Memberships.Where(m => m.Id != member.Id).Append(member).ToArray(),
                PosterItems = catalog.PosterItems.Append(card).ToArray() }, Updates = [target with { ContentLayoutJson = ContentLayoutPolicy.Serialize(next) }] };
        }
        else
        {
            var reads = new StateReadSet(new Dictionary<string, long> { [targetId] = state.Catalog.Posters.Single(p => p.Poster.PosterId == targetId).EntityVersion }, ContentVersion: state.ContentVersion);
            addition = ContentReferencePolicy.Prepare(new(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, state.Revision,
                reads, targetId, [new(destination, ContentReferenceChoice.Reuse, destination.Id)], true), state with { ContentCatalog = catalog,
                    Catalog = state.Catalog with { Posters = state.Catalog.Posters.Select(p => p.Poster.PosterId == targetId ? p with { Poster = target } : p).ToArray() } });
            if (addition.Rejection is not null) return addition;
        }
        catalog = addition.ContentCatalog!;
        var memberIds = catalog.Memberships.Where(m => m.ContentItemId == destination.Id).Select(m => m.Id).ToHashSet();
        var added = catalog.PosterItems.Single(p => p.PosterId == targetId && memberIds.Contains(p.MembershipId));
        var updated = addition.Updates.Single();
        if (!ContentLayoutPolicy.TryRead(updated.ContentLayoutJson, targetId, out var doc, out var layoutError)) throw new ArgumentException(layoutError);
        if (landing is not null) doc = landing.Apply(doc!, added.LayoutItemId);
        ContentLayoutCommandPolicy.ValidateAuthoring(doc!);
        return removal with { ContentCatalog = catalog with { PosterItems = catalog.PosterItems.Select(p => p.Id == added.Id ? p with { IconAssetId = old.IconAssetId } : p).ToArray() },
            Updates = removal.Updates.Where(p => p.PosterId != targetId).Append(updated with { ContentLayoutJson = ContentLayoutPolicy.Serialize(doc!) }).ToArray(), ResultPosterId = targetId };
    }
}
