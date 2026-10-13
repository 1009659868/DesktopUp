using DesktopUp.Domain;

namespace DesktopUp.Application;

// Shared pure index operation. Callers own confirmation, locks and physical evidence.
// Never deletes an entity, a file or an icon asset. Exact relation IDs, not filenames.
public static class ContentRelationRemoval
{
    public static StateMutationPlan Prepare(DesktopStateSnapshot state, IReadOnlyCollection<Guid> relationIds)
    {
        var ids = relationIds.ToHashSet();
        var removed = state.ContentCatalog.PosterItems.Where(p => ids.Contains(p.Id)).ToArray();
        if (ids.Count != relationIds.Count || removed.Length != ids.Count)
            return StateMutationPlan.Reject(StateCommandStatus.Conflict, "内容关联已变化，请重试。");
        var updates = new List<PosterDefinition>();
        foreach (var group in removed.GroupBy(p => p.PosterId))
        {
            var poster = state.Catalog.Posters.Single(p => p.Poster.PosterId == group.Key).Poster;
            if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var layout, out var error))
                return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error!);
            var layoutIds = group.Select(p => p.LayoutItemId).ToHashSet(StringComparer.Ordinal);
            var survivors = layout!.Placements.Where(p => !layoutIds.Contains(p.ItemId)).ToArray();
            Dictionary<string, int> Pins(Func<ContentPlacement, int?> select)
            {
                int ceiling = survivors.Length;
                var result = new Dictionary<string, int>();
                foreach (var p in survivors.Where(p => select(p).HasValue).OrderByDescending(select))
                { int value = Math.Min(select(p)!.Value, ceiling); result[p.ItemId] = value; ceiling = value - 1; }
                return result;
            }
            var pins = Pins(p => p.PinnedIndex);
            var savedPins = Pins(p => p.SavedAutomaticState?.PinnedIndex);
            survivors = survivors.Select(p =>
            {
                var updated = p with { PinnedIndex = pins.TryGetValue(p.ItemId, out int pin) ? pin : null,
                    SavedAutomaticState = p.SavedAutomaticState is { } saved
                        ? saved with { PinnedIndex = savedPins.TryGetValue(p.ItemId, out int oldPin) ? oldPin : null } : null };
                return updated == p ? p : updated with { Version = checked(p.Version + 1) };
            }).ToArray();
            var next = layout with { Placements = survivors, DocumentVersion = checked(layout.DocumentVersion + 1),
                ItemSetVersion = checked(layout.ItemSetVersion + 1) };
            ContentLayoutCommandPolicy.ValidateAuthoring(next);
            updates.Add(poster with { ContentLayoutJson = ContentLayoutPolicy.Serialize(next) });
        }
        return new() { ContentCatalog = state.ContentCatalog with
            { PosterItems = state.ContentCatalog.PosterItems.Where(p => !ids.Contains(p.Id)).ToArray() },
            ExplicitDetachedRelations = removed.Select(p => p.Id).ToArray(), Updates = updates };
    }
}
