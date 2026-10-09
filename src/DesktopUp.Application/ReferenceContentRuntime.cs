using DesktopUp.Domain;

namespace DesktopUp.Application;

// Per-poster immutable relations + replaceable display observations. Never changes membership/layout.
public sealed class ReferenceContentRuntime : IObservableContentItemViewSource, IContentAssetProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, ContentItem> items;
    private readonly Dictionary<string, (bool Available, string Name, string Type)> metadata = new();
    private readonly ILocalContentWorker worker;
    private long version;
    public long Version { get { lock (gate) return version; } }
    public event EventHandler? Changed;
    public ReferenceContentRuntime(ContentCatalogSnapshot catalog, string posterId, long version, ILocalContentWorker worker)
    {
        this.worker = worker; this.version = Math.Max(1, version);
        catalog = ContentCatalogPolicy.Freeze(catalog);
        var members = catalog.Memberships.ToDictionary(m => m.Id);
        var entities = catalog.Items.ToDictionary(i => i.Id);
        items = catalog.PosterItems.Where(p => p.PosterId == posterId).ToDictionary(p => p.LayoutItemId,
            p => entities[members[p.MembershipId].ContentItemId]);
        foreach (var (id, item) in items) metadata[id] = (item.Availability == ContentAvailability.Available, item.DisplayName, item.Kind.ToString());
    }
    public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document)
    {
        lock (gate)
            return document.Placements.Where(p => items.ContainsKey(p.ItemId)).Select(p =>
            {
                var item = items[p.ItemId]; var observed = metadata[p.ItemId];
                return new ContentItemView(p.ItemId, observed.Name.Length > 256 ? observed.Name[..256] : observed.Name,
                    item.Kind == ContentKind.Folder ? "DIR" : item.Kind == ContentKind.Shortcut ? "LINK" : "FILE",
                    0xFF718DAB, AssetKey(item)) { IsAvailable = observed.Available, TypeName = observed.Type, RequestAssetWhenUnavailable = true };
            }).ToArray();
    }
    public async ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken cancellation)
    {
        if (!items.TryGetValue(key.ItemId, out var item) || key.SourceKey != AssetKey(item)) return null;
        var request = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.Icon, item);
        var result = await worker.RunAsync(request, cancellation).ConfigureAwait(false);
        if (!LocalContentProtocol.Validate(request, result)) return null;
        if (cancellation.IsCancellationRequested) return null;
        Observe(key.ItemId, result);
        return LocalContentProtocol.ValidPixels(result.Pixels, result.Width, result.Height)
            ? new("image/bgra32", result.Pixels!) { PixelWidth = result.Width, PixelHeight = result.Height } : null;
    }
    public void Observe(string id, LocalContentResponse result)
    {
        bool changed;
        lock (gate)
        {
            if (!items.TryGetValue(id, out var item)) return;
            var old = metadata[id];
            // Busy/timeouts are not proof that the file vanished; leave prior availability.
            bool available = result.Outcome is ContentOpenOutcome.Busy or ContentOpenOutcome.TimedOut or ContentOpenOutcome.Cancelled
                ? old.Available : result.Availability == ContentAvailability.Available;
            var next = (Available: available, Name: result.Name ?? old.Name, Type: result.TypeName ?? old.Type);
            changed = next.Available != old.Available || next.Name != old.Name;
            metadata[id] = next;
            if (changed) version = checked(version + 1);
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Refresh() { lock (gate) version = checked(version + 1); Changed?.Invoke(this, EventArgs.Empty); }
    public static string AssetKey(ContentItem item) => $"local-icon:{item.Id:N}:{item.Version}";
}

public interface IContentOpenJournal
{
    ContentOpenRequestAdmission TryStart(ContentOpenRequest request);
    ContentOpenFact? Complete(ContentOpenRequest request, ContentOpenCompletion completion);
}
public sealed record ContentOperationFeedback(bool Succeeded, string Message, ContentOpenOutcome Outcome);
public interface IAsyncContentActions
{
    ValueTask<ContentOperationFeedback> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation);
    ValueTask<ContentOperationFeedback> RevealAsync(string itemId, CancellationToken cancellation);
    void Refresh();
}

public sealed class ReferenceContentActions(Func<ContentCatalogSnapshot> currentCatalog, ReferenceContentRuntime views,
    ILocalContentWorker worker, IContentOpenJournal journal) : IContentOpenHandler, IAsyncContentActions
{
    public void Open(ContentOpenIntent intent) => throw new InvalidOperationException("Real opening must await its result.");
    public void Refresh() => views.Refresh();
    public async ValueTask<ContentOperationFeedback> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested) return new(false, "已取消。", ContentOpenOutcome.Cancelled);
        ContentOpenPreparation prepared = ContentOpenContract.Prepare(currentCatalog(), intent, intent.Target);
        if (prepared.Request is not { } request) return new(false, "来源或关联已变化，请刷新或修复引用。", ContentOpenOutcome.Unavailable);
        var claim = journal.TryStart(request);
        if (claim != ContentOpenRequestAdmission.Ready)
            return new(false, claim == ContentOpenRequestAdmission.Busy ? "此内容正在打开，请稍候。" : "该请求已处理或已失效，没有再次打开。", ContentOpenOutcome.Busy);
        LocalContentResponse result;
        try
        {
            var relation = ContentRelationPolicy.Resolve(currentCatalog(), request.ReadSet.PosterId, intent.Target.ItemId);
            if (relation is null || relation.Witness != request.ReadSet)
                result = new(request.ActivationId, ContentAvailability.Unavailable, "关联已变化，未打开。") { Outcome = ContentOpenOutcome.Unavailable };
            else
            {
                var dispatch = new LocalContentRequest(request.ActivationId, LocalContentAction.Open, relation.Item);
                result = await worker.RunAsync(dispatch, cancellation).ConfigureAwait(false);
                if (!LocalContentProtocol.Validate(dispatch, result))
                    result = new(request.ActivationId, ContentAvailability.Unavailable, "提供方返回无效结果，未记为成功。") { Outcome = ContentOpenOutcome.Failed };
            }
        }
        catch (Exception) { result = new(request.ActivationId, ContentAvailability.Unavailable, "打开失败，未自动重试。") { Outcome = ContentOpenOutcome.Failed }; }
        var outcome = result.Evidence != ContentOpenEvidence.None ? ContentOpenOutcome.Succeeded :
            result.Outcome == ContentOpenOutcome.None ? ContentOpenOutcome.Unavailable : result.Outcome;
        // A trusted confirmed handoff remains a fact even if its visual owner was removed meanwhile.
        journal.Complete(request, new(request.ActivationId, request.Source.ProviderId, outcome, result.Evidence, DateTimeOffset.UtcNow));
        views.Observe(intent.Target.ItemId, result);
        return new(outcome == ContentOpenOutcome.Succeeded, result.Message, outcome);
    }
    public async ValueTask<ContentOperationFeedback> RevealAsync(string itemId, CancellationToken cancellation)
    {
        // Relation lookup spans only this view's local identity.
        var catalog = currentCatalog();
        var relation = catalog.PosterItems.SingleOrDefault(p => p.LayoutItemId == itemId);
        var resolved = relation is null ? null : ContentRelationPolicy.Resolve(catalog, relation.PosterId, itemId);
        if (resolved is null || !ContentCapabilityPolicy.Evaluate(resolved.Item, ContentOperation.Reveal).Allowed)
            return new(false, "当前引用无法定位，请修复来源。", ContentOpenOutcome.Unavailable);
        var dispatch = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.Reveal, resolved.Item);
        var result = await worker.RunAsync(dispatch, cancellation).ConfigureAwait(false);
        if (!LocalContentProtocol.Validate(dispatch, result)) return new(false, "提供方返回无效结果。", ContentOpenOutcome.Failed);
        views.Observe(itemId, result);
        // Reveal deliberately does not write an open fact.
        return new(result.Evidence != ContentOpenEvidence.None, result.Message,
            result.Evidence != ContentOpenEvidence.None ? ContentOpenOutcome.Succeeded : ContentOpenOutcome.Unavailable);
    }
}
