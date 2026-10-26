using DesktopUp.Domain;

namespace DesktopUp.Application;

// One application-scoped service; its dependencies own concurrency and storage.
// Execution can complete without a poster view, including after that view closes.
public sealed class ContentActionService(
    Func<ContentCatalogSnapshot> currentCatalog,
    ILocalContentWorker worker,
    IContentOpenJournal journal,
    ContentActivityConsumer? activity = null) : IContentActionService
{
    public async ValueTask<ContentActionResult> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
            return Feedback(false, "已取消。", ContentOpenOutcome.Cancelled);

        var prepared = ContentOpenContract.Prepare(currentCatalog(), intent, intent.Target);
        if (prepared.Request is not { } request)
            return Feedback(false, "来源或关联已变化，请刷新或修复引用。", ContentOpenOutcome.Unavailable);

        var claim = journal.TryStart(request);
        if (claim != ContentOpenRequestAdmission.Ready)
            return Feedback(false, claim == ContentOpenRequestAdmission.Busy
                ? "此内容正在打开，请稍候。" : "该请求已处理或已失效，没有再次打开。", ContentOpenOutcome.Busy);

        LocalContentResponse result;
        ContentItem? dispatched = null;
        try
        {
            var relation = ContentRelationPolicy.Resolve(currentCatalog(), request.ReadSet.PosterId, intent.Target.ItemId);
            if (relation is null || relation.Witness != request.ReadSet)
                result = new(request.ActivationId, ContentAvailability.Unavailable, "关联已变化，未打开。")
                    { Outcome = ContentOpenOutcome.Unavailable };
            else
            {
                dispatched = relation.Item;
                var dispatch = new LocalContentRequest(request.ActivationId, LocalContentAction.Open, relation.Item);
                result = await worker.RunAsync(dispatch, cancellation).ConfigureAwait(false);
                if (!LocalContentProtocol.Validate(dispatch, result))
                    result = new(request.ActivationId, ContentAvailability.Unavailable, "提供方返回无效结果，未记为成功。")
                        { Outcome = ContentOpenOutcome.Failed };
            }
        }
        catch (Exception)
        {
            // Preserve the existing worker-exception mapping; no implicit retry.
            result = new(request.ActivationId, ContentAvailability.Unavailable, "打开失败，未自动重试。")
                { Outcome = ContentOpenOutcome.Failed };
        }

        var outcome = result.Evidence != ContentOpenEvidence.None ? ContentOpenOutcome.Succeeded :
            result.Outcome == ContentOpenOutcome.None ? ContentOpenOutcome.Unavailable : result.Outcome;

        // Confirmed handoff remains a fact even when the visual owner is gone.
        // Complete must succeed before publishing the observation or feedback.
        var fact = journal.Complete(request, new(request.ActivationId, request.Source.ProviderId,
            outcome, result.Evidence, DateTimeOffset.UtcNow));
        if (fact is not null) activity?.RequestRefresh();
        var observation = dispatched is null ? null : new ContentSourceObservation(intent.Target.ItemId, dispatched, result);
        return new(new(outcome == ContentOpenOutcome.Succeeded, result.Message, outcome), observation);
    }

    public async ValueTask<ContentActionResult> RevealAsync(string itemId, CancellationToken cancellation)
    {
        var catalog = currentCatalog();
        var relation = catalog.PosterItems.SingleOrDefault(p => p.LayoutItemId == itemId);
        var resolved = relation is null ? null : ContentRelationPolicy.Resolve(catalog, relation.PosterId, itemId);
        if (resolved is null || !ContentCapabilityPolicy.Evaluate(resolved.Item, ContentOperation.Reveal).Allowed)
            return Feedback(false, "当前引用无法定位，请修复来源。", ContentOpenOutcome.Unavailable);

        var dispatch = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.Reveal, resolved.Item);
        var result = await worker.RunAsync(dispatch, cancellation).ConfigureAwait(false);
        if (!LocalContentProtocol.Validate(dispatch, result))
            return Feedback(false, "提供方返回无效结果。", ContentOpenOutcome.Failed);

        // Reveal deliberately does not write an open fact.
        bool succeeded = result.Evidence != ContentOpenEvidence.None;
        return new(new(succeeded, result.Message, succeeded ? ContentOpenOutcome.Succeeded : ContentOpenOutcome.Unavailable),
            new(itemId, resolved.Item, result));
    }

    private static ContentActionResult Feedback(bool succeeded, string message, ContentOpenOutcome outcome) =>
        new(new(succeeded, message, outcome));
}
