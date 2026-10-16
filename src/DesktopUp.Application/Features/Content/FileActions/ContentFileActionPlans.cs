using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentFileActionCommit(StateTransactionRequest Request, StateMutationPlan Mutation, IReadOnlyList<Guid> ObservedIds);

// Pure index policies, called inside adapters' existing atomic transactions.
// Moving these decisions does not split a commit or create a second fact store.
public static class ContentFileActionPlans
{
    public static ContentFileActionCommit Rename(DesktopStateSnapshot state, ContentRenameIntent intent)
    {
        // A repair or later operation wins. Do not overwrite new user intent.
        if(intent.Targets.Any(target=>!state.ContentCatalog.Items.Any(i=>i.Id==target.ContentId &&
            i.Version==target.Version && i.Target.Locator==target.Before)))throw new IOException("引用已变化，改名结果需重新核对。");
        var changes=intent.Targets.ToDictionary(x=>x.ContentId);
        var catalog=state.ContentCatalog with { Items=state.ContentCatalog.Items.Select(i=>changes.TryGetValue(i.Id,out var target)
            ? i with {Target=i.Target with {Locator=target.After},Source=i.Source with {Key=target.After},
                DisplayName=Path.GetFileName(target.After),Version=checked(i.Version+1)} : i).ToArray() };
        var affected=intent.Targets.SelectMany(x=>ContentReferencePolicy.AffectedPosters(state.ContentCatalog,x.ContentId)).Distinct().ToHashSet();
        var posters=state.Catalog.Posters.Where(p=>affected.Contains(p.Poster.PosterId)).ToArray();
        return new(new("rename-" + intent.Id.ToString("N"), StateRequestFingerprint.Compute(intent), state.Revision,
            new(posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), ContentVersion: state.ContentVersion)),
            new() { ContentCatalog = catalog, Updates = posters.Select(p => p.Poster).ToArray() }, changes.Keys.ToArray());
    }

    public static ContentFileActionCommit? ShellCreated(DesktopStateSnapshot state, string posterId, long posterVersion,
        Guid collectionId, ContentItem observed, ShellCreatedItem created)
    {
        var catalog = state.ContentCatalog;
        var occupied = catalog.PosterItems.FirstOrDefault(p => ContentSingleOwnerPolicy.Key(
            ContentRelationPolicy.Resolve(catalog,p.PosterId,p.LayoutItemId)!.Item) == ContentSingleOwnerPolicy.Key(observed));
        if (occupied is not null)
        {
            if (occupied.PosterId == posterId) return null; // Same callback/result is idempotent.
            throw new IOException("此对象已属于另一海报；未改变原有关联。");
        }
        observed = observed with { Capabilities = observed.Capabilities & ~ContentCapability.Move };
        var staged = catalog with { Items = catalog.Items.Append(observed).ToArray(),
            Memberships = catalog.Memberships.Append(new(Guid.NewGuid(), collectionId, observed.Id, ContentMembershipOrigin.Source)).ToArray() };
        var reads = StateReadSet.ForPoster(posterId, posterVersion) with { ContentVersion = state.ContentVersion };
        string operation = "shell-created-" + Guid.NewGuid().ToString("N");
        var plan = ContentReferencePolicy.Prepare(new(operation, StateCommandSource.ControlCenter, state.Revision, reads,
            posterId, [new(observed, ContentReferenceChoice.Reuse, observed.Id)], true), state with { ContentCatalog = staged });
        if (plan.Rejection is not null) throw new IOException(plan.Error);
        return new(new(operation, StateRequestFingerprint.Compute(created), state.Revision, reads), plan, [observed.Id]);
    }
}
