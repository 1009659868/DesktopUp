using DesktopUp.Domain;

namespace DesktopUp.Application;

// Last invariant barrier for every command, including legacy layout and portable commands.
public static class ContentReferenceIntegrity
{
    public static string? ValidateMutation(DesktopStateSnapshot before, StateReadSet? reads, StateMutationPlan plan)
    {
        var catalog = plan.ContentCatalog ?? before.ContentCatalog;
        if (plan.IconEdit is not null && !ContentIconPolicy.ValidMutation(before, reads, plan))
            return "Icon change must be an isolated, versioned card appearance edit.";
        if (plan.ContentCatalog is not null)
        {
            if (reads?.ContentVersion != before.ContentVersion) return "Content mutations require the confirmed catalog version.";
            try { catalog = ContentCatalogPolicy.Freeze(catalog); _ = ContentCatalogCodec.Write(catalog); }
            catch (ArgumentException e) { return e.Message; }
            // Old shared profiles may still be read/backed up, but no mutation can publish
            // another shared catalog. Startup migration uses the common removal operation.
            if (ContentSingleOwnerPolicy.Validate(catalog) is { } ownershipError) return ownershipError;
            var detached = before.ContentCatalog.PosterItems.Where(p => catalog.PosterItems.All(n => n.Id != p.Id)).ToArray();
            if (!detached.Select(p => p.Id).ToHashSet().SetEquals(plan.ExplicitDetachedRelations) ||
                detached.Any(p => !reads.Posters.ContainsKey(p.PosterId) || plan.Updates.All(u => u.PosterId != p.PosterId)))
                return "Detach requires an explicit complete relation set and matching poster update.";
            // Explicit detach removes only the poster view; shared content/collection/source remain.
            var removedStorage = before.ContentCatalog.Memberships.Where(m => !catalog.Memberships.Contains(m)).ToArray();
            if (!removedStorage.Select(m => m.Id).ToHashSet().SetEquals(plan.RemovedStorageMemberships) ||
                removedStorage.Any(m => (plan.TransferReceiptId is null && plan.ExternalSyncReceiptId is null) || m.Origin != ContentMembershipOrigin.Source ||
                    before.ContentCatalog.Collections.Single(c => c.Id == m.CollectionId).Kind != ContentCollectionKind.Managed ||
                    catalog.PosterItems.Any(p => p.MembershipId == m.Id)))
                return "Storage membership removal requires verified transfer or external observation, never a view detach.";
            if (before.ContentCatalog.Items.Any(i => catalog.Items.All(n => n.Id != i.Id)) ||
                before.ContentCatalog.Collections.Any(c => !catalog.Collections.Contains(c)) ||
                before.ContentCatalog.Bindings.Any(b => !catalog.Bindings.Contains(b)) ||
                before.ContentCatalog.PosterItems.Except(detached).Any(p => !catalog.PosterItems.Contains(p) && plan.IconEdit?.PosterItemId != p.Id))
                return "Reference commands may not silently detach existing relations.";
            if (catalog.PosterItems.Any(p => p.IconAssetId is not null &&
                before.ContentCatalog.PosterItems.All(old => old.Id != p.Id) &&
                !(plan.TransferReceiptId is not null && detached.Any(old=>old.IconAssetId==p.IconAssetId &&
                    before.ContentCatalog.Memberships.Single(m=>m.Id==old.MembershipId).ContentItemId==
                    catalog.Memberships.Single(m=>m.Id==p.MembershipId).ContentItemId))))
                return "New card icon import requires an explicit atomic construction contract.";
            foreach (var item in before.ContentCatalog.Items)
            {
                var next = catalog.Items.Single(i => i.Id == item.Id);
                if (next == item) continue;
                if (next.Version != item.Version + 1 || ContentReferencePolicy.AffectedPosters(before.ContentCatalog, item.Id)
                    .Any(p => !reads.Posters.ContainsKey(p) || plan.Updates.All(u => u.PosterId != p)))
                    return "Shared repair must update every confirmed dependent poster exactly once.";
            }
        }
        var posters = before.Catalog.Posters.Select(p => p.Poster).Where(p => !plan.Removes.Contains(p.PosterId))
            .ToDictionary(p => p.PosterId);
        foreach (var p in plan.Updates.Concat(plan.Creates)) posters[p.PosterId] = p;
        foreach (var binding in catalog.Bindings.Where(b => !plan.Removes.Contains(b.PosterId)))
        {
            if (!posters.TryGetValue(binding.PosterId, out var poster) || poster.CollectionId != binding.CollectionId.ToString() ||
                poster.ContentLayoutJson is null || !ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var layout, out _))
                return "Reference poster binding/layout is inconsistent.";
            if (!layout!.Placements.Select(p => p.ItemId).ToHashSet(StringComparer.Ordinal).SetEquals(
                catalog.PosterItems.Where(p => p.PosterId == poster.PosterId).Select(p => p.LayoutItemId)))
                return "真实内容成员由引用命令管理，不能用 DEMO 样片替换或只修改布局成员。";
            if (plan.ContentCatalog is not null && !before.ContentCatalog.Bindings.Contains(binding) &&
                plan.Updates.Concat(plan.Creates).All(p => p.PosterId != binding.PosterId))
                return "New bindings require a matching poster update.";
        }
        // A configuration-only clone/import must never masquerade as live references.
        foreach (var poster in plan.Creates)
            if (ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var layout, out _) &&
                layout!.Placements.Any(p => p.ItemId.StartsWith(poster.PosterId + ":content-", StringComparison.Ordinal)) &&
                catalog.Bindings.All(b => b.PosterId != poster.PosterId)) return "Unbound real content placement.";
        return null;
    }
}
