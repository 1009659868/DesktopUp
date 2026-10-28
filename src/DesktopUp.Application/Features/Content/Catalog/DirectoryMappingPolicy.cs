using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record MappedContentFact(ContentItem Item, string Identity, string Stamp);
public sealed record MappedContentObservation(Guid MembershipId, string Identity, string Stamp);
public sealed record DirectoryMappingUpdate(StateMutationPlan Plan, IReadOnlyList<MappedContentObservation> Observations);

// Source facts own membership; the existing layout owns arrangement. No filesystem operations.
public static class DirectoryMappingPolicy
{
    public static StateMutationPlan PreparePoster(CreatePosterCommand command, DesktopStateSnapshot state) => StateCommandPolicy.Prepare(command, state);
    public static DirectoryMappingUpdate Reconcile(DesktopStateSnapshot state, string posterId,
        IReadOnlyList<MappedContentFact> facts, IReadOnlyList<MappedContentObservation> baseline)
    {
        var catalog = state.ContentCatalog;
        var binding = catalog.Bindings.Single(b => b.PosterId == posterId);
        var collection = catalog.Collections.Single(c => c.Id == binding.CollectionId);
        if (collection.Kind != ContentCollectionKind.Mapped || collection.Ownership != ContentStorageOwnership.Borrowed)
            throw new ArgumentException("A borrowed directory is required.");
        if (facts.Count > ContentLayoutPolicy.MaximumItems || facts.Select(f => f.Identity).Distinct().Count() != facts.Count)
            throw new ArgumentException("目录过大或包含多个相同文件身份，未改变映射。");
        var members = catalog.Memberships.Where(m => m.CollectionId == collection.Id).ToArray();
        var evidence = baseline.ToDictionary(b => b.MembershipId);
        if (members.Any(m => !evidence.ContainsKey(m.Id))) throw new ArgumentException("目录身份记录不完整，保留原关联。");
        var membersByIdentity = members.ToDictionary(m => evidence[m.Id].Identity, StringComparer.Ordinal);
        var observed = facts.Select(f => f.Identity).ToHashSet();
        var removedMembers = members.Where(m => !observed.Contains(evidence[m.Id].Identity)).Select(m => m.Id).ToHashSet();
        var removedCards = catalog.PosterItems.Where(p => p.PosterId == posterId && removedMembers.Contains(p.MembershipId)).Select(p => p.Id).ToArray();
        var removal = ContentRelationRemoval.Prepare(state, removedCards);
        if (removal.Rejection is not null) throw new ArgumentException(removal.Error);
        var poster = removal.Updates.SingleOrDefault() ?? state.Catalog.Posters.Single(p => p.Poster.PosterId == posterId).Poster;
        if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, posterId, out var layout, out var error)) throw new ArgumentException(error);
        var items = catalog.Items.ToDictionary(i => i.Id);
        var allMembers = catalog.Memberships.ToDictionary(m => m.Id);
        var otherOwners = catalog.PosterItems.Where(p => p.PosterId != posterId)
            .Select(p => ContentSingleOwnerPolicy.Key(items[allMembers[p.MembershipId].ContentItemId])).ToHashSet(StringComparer.Ordinal);
        var nextMembers = catalog.Memberships.Where(m => !removedMembers.Contains(m.Id)).ToList();
        var cards = removal.ContentCatalog!.PosterItems.ToList(); var placements = layout!.Placements.ToList();
        var visibleMembers = cards.Where(c => c.PosterId == posterId).Select(c => c.MembershipId).ToHashSet();
        var observations = new List<MappedContentObservation>();
        bool changed = removedMembers.Count != 0, added = false;
        foreach (var fact in facts)
        {
            membersByIdentity.TryGetValue(fact.Identity, out var member);
            if (otherOwners.Contains(ContentSingleOwnerPolicy.Key(fact.Item)))
                throw new ArgumentException("目录中存在已属于其他海报的对象，未抢占关联：" + fact.Item.DisplayName);
            if (member is null)
            {
                var item = fact.Item; items.Add(item.Id, item);
                member = new(Guid.NewGuid(), collection.Id, item.Id, ContentMembershipOrigin.Source);
                nextMembers.Add(member); changed = true;
            }
            else
            {
                var old = items[member.ContentItemId];
                if (old.Target != fact.Item.Target || old.DisplayName != fact.Item.DisplayName || evidence[member.Id].Stamp != fact.Stamp)
                {
                    items[old.Id] = fact.Item with { Id = old.Id, Version = checked(old.Version + 1) }; changed = true;
                }
            }
            if (visibleMembers.Add(member.Id))
            {
                var card = new PosterItem(Guid.NewGuid(), posterId, member.Id); cards.Add(card);
                placements.Add(ContentMemberPlacement.Append(layout, placements, card.LayoutItemId)); changed = added = true;
            }
            observations.Add(new(member.Id, fact.Identity, fact.Stamp));
        }
        if (!changed) return new(new(), observations);
        if (added) layout = layout with { Placements = placements, DocumentVersion = checked(layout.DocumentVersion + 1), ItemSetVersion = checked(layout.ItemSetVersion + 1) };
        ContentLayoutCommandPolicy.ValidateAuthoring(layout);
        poster = poster with { ContentLayoutJson = ContentLayoutPolicy.Serialize(layout) };
        var result = removal with { ContentCatalog = catalog with { Items = items.Values.ToArray(), Memberships = nextMembers, PosterItems = cards },
            Updates = [poster], RemovedStorageMemberships = removedMembers.ToArray() };
        return new(result, observations);
    }
}
