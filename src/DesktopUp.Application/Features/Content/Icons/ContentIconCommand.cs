using DesktopUp.Domain;

namespace DesktopUp.Application;

public interface IContentIconStore
{
    ValueTask<ContentIconAsset?> ReadAsync(string id, CancellationToken cancellation);
}

// Decode/preview happens before this command. A null asset explicitly restores the provider icon.
public sealed record ContentIconCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, ContentRelationWitness Relation, ContentIconAsset? Asset, bool Confirmed);

public static class ContentIconPolicy
{
    public static StateMutationPlan Prepare(ContentIconCommand command, DesktopStateSnapshot state)
    {
        StateMutationPlan Reject(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
        if (command.Source != StateCommandSource.ControlCenter) return Reject("Only the control center may replace a card icon.");
        if (!command.Confirmed) return StateMutationPlan.Reject(StateCommandStatus.ConfirmationRequired, "仅更改当前卡片图标，真实来源不变。请确认。");
        string id = command.Relation.PosterId;
        if (command.ReadSet.ContentVersion != state.ContentVersion || !command.ReadSet.Posters.ContainsKey(id) ||
            !ContentRelationPolicy.IsCurrent(state.ContentCatalog, command.Relation)) return Reject("卡片或来源已变化，请重新预览。");
        var poster = state.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster;
        if (poster.IsLocked) return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "海报已锁定。");
        var current = state.ContentCatalog.PosterItems.Single(p => p.Id == command.Relation.PosterItemId);
        if (current.IconAssetId == command.Asset?.Id) return new() { ResultPosterId = id };
        var next = current with { IconAssetId = command.Asset?.Id, Version = checked(current.Version + 1) };
        return new()
        {
            ContentCatalog = state.ContentCatalog with { PosterItems = state.ContentCatalog.PosterItems.Select(p => p.Id == next.Id ? next : p).ToArray() },
            IconEdit = new(current.Id, command.Asset), Updates = [poster], ResultPosterId = id
        };
    }

    // Transaction barrier: no hidden membership, target, layout, or second-card edit can ride along.
    public static bool ValidMutation(DesktopStateSnapshot before, StateReadSet? reads, StateMutationPlan plan)
    {
        if (plan.IconEdit is not { } edit || plan.ContentCatalog is not { } after) return false;
        var old = before.ContentCatalog.PosterItems.SingleOrDefault(p => p.Id == edit.PosterItemId);
        if (old is null || reads?.ContentVersion != before.ContentVersion || !reads.Posters.ContainsKey(old.PosterId)) return false;
        var poster = before.Catalog.Posters.Single(p => p.Poster.PosterId == old.PosterId).Poster;
        if (poster.IsLocked || plan.Updates.Count != 1 || plan.Updates[0] != poster || plan.Creates.Count != 0 ||
            plan.Removes.Count != 0 || plan.Settings is not null || plan.ExplicitDetachedRelations.Count != 0 ||
            plan.TouchLayoutIds.Count != 0 || plan.NewSpatialProof is not null || plan.NewContinuity is not null || plan.ConsumeSpatialOperationId is not null) return false;
        return before.ContentCatalog.Items.SequenceEqual(after.Items) && before.ContentCatalog.Collections.SequenceEqual(after.Collections) &&
            before.ContentCatalog.Memberships.SequenceEqual(after.Memberships) && before.ContentCatalog.Bindings.SequenceEqual(after.Bindings) &&
            before.ContentCatalog.PosterItems.Select(p => p.Id == old.Id ? p with { IconAssetId = edit.Asset?.Id, Version = checked(p.Version + 1) } : p)
                .SequenceEqual(after.PosterItems);
    }
}
public sealed record ContentIconEdit(Guid PosterItemId, ContentIconAsset? Asset);
public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(ContentIconCommand command, CancellationToken cancellation = default) => Run(command.RequestId, command.Source,
        command.ExpectedRevision, command.ReadSet, reads => command with { ReadSet = Required(reads) },
        (frozen, state) => cancellation.IsCancellationRequested
            ? StateMutationPlan.Reject(StateCommandStatus.NotReady, "图标编辑已取消或应用正在退出。") : ContentIconPolicy.Prepare(frozen, state));
}
