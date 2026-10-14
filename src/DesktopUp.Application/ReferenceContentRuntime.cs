using DesktopUp.Domain;

namespace DesktopUp.Application;

// Per-poster adapter retained across committed catalog deltas. It observes, never authors relations/layout.
public sealed class ReferenceContentRuntime : IObservableContentItemViewSource, IAtomicContentItemViewSource, IContentAssetProvider, IContentRasterIdentity
{
    private sealed record Entry(ContentItem Item, Guid MembershipId, string? Icon, long Generation);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> items = new();
    private readonly Dictionary<string, (bool Available, string Name, string Type)> metadata = new();
    private readonly ILocalContentWorker worker;
    private readonly IContentIconStore? iconStore;
    private readonly string posterId;
    private long rasterGeneration;
    private long version;
    public long Version { get { lock (gate) return version; } }
    public event EventHandler? Changed;
    public ReferenceContentRuntime(ContentCatalogSnapshot catalog, string posterId, long version, ILocalContentWorker worker, IContentIconStore? iconStore = null)
    {
        this.iconStore = iconStore; this.posterId = posterId;
        this.worker = worker; this.version = Math.Max(1, version);
        Reconcile(catalog);
        this.version = Math.Max(1, version);
    }
    public bool Reconcile(ContentCatalogSnapshot catalog)
    {
        catalog = ContentCatalogPolicy.Freeze(catalog);
        var members = catalog.Memberships.ToDictionary(m => m.Id);
        var entities = catalog.Items.ToDictionary(i => i.Id);
        var next = catalog.PosterItems.Where(p => p.PosterId == posterId).ToArray();
        bool changed = false;
        lock (gate)
        {
            var ids = next.Select(p => p.LayoutItemId).ToHashSet(StringComparer.Ordinal);
            foreach (var id in items.Keys.Where(id => !ids.Contains(id)).ToArray())
            { items.Remove(id); metadata.Remove(id); changed = true; }
            foreach (var p in next)
            {
                var item = entities[members[p.MembershipId].ContentItemId];
                if (items.TryGetValue(p.LayoutItemId, out var old) && old.Item == item &&
                    old.MembershipId == p.MembershipId && old.Icon == p.IconAssetId) continue;
                items[p.LayoutItemId] = new(item, p.MembershipId, p.IconAssetId, checked(++rasterGeneration));
                metadata[p.LayoutItemId] = (item.Availability == ContentAvailability.Available, item.DisplayName, item.Kind.ToString());
                changed = true;
            }
            if (changed) version = checked(version + 1);
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
        return changed;
    }
    public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document)
    {
        lock (gate)
            return document.Placements.Where(p => items.ContainsKey(p.ItemId)).Select(p =>
            {
                var entry = items[p.ItemId]; var item = entry.Item; var observed = metadata[p.ItemId];
                return new ContentItemView(p.ItemId, observed.Name.Length > 256 ? observed.Name[..256] : observed.Name,
                    item.Kind == ContentKind.Folder ? "DIR" : item.Kind == ContentKind.Shortcut ? "LINK" : "FILE",
                    0xFF718DAB, AssetKeyFor(entry)) { IsAvailable = observed.Available, TypeName = observed.Type, RequestAssetWhenUnavailable = true };
            }).ToArray();
    }
    public ContentAssetKey RasterKey(ContentAssetKey demand)
    {
        lock (gate)
        {
            // These positive sentinel fields describe an intrinsic raster, not a layout witness.
            // Real membership/source freshness is the per-entry generation, not the whole catalog.
            return demand with { DocumentVersion = 1, ItemSetVersion = 1, PlacementVersion = 1,
                PixelWidth = 256, PixelHeight = 256, DpiX = 96, DpiY = 96, ThemeVersion = 1,
                SourceVersion = items.TryGetValue(demand.ItemId, out var entry) ? entry.Generation : 1 };
        }
    }
    public bool IsCurrentRaster(ContentAssetKey key)
    { lock (gate) return Current(key); }
    private bool Current(ContentAssetKey key) => key.PosterId == posterId &&
        items.TryGetValue(key.ItemId, out var entry) && entry.Generation == key.SourceVersion && key.SourceKey == AssetKeyFor(entry);
    public async ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken cancellation)
    {
        Entry entry;
        lock (gate) { if (!Current(key)) return null; entry = items[key.ItemId]; }
        var item = entry.Item;
        if (entry.Icon is { } iconId)
        {
            if (iconStore is null) return null; // Missing/corrupt custom asset is not silently replaced by a source icon.
            var icon = await iconStore.ReadAsync(iconId, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (icon is null || icon.Id != iconId || !IsCurrentRaster(key)) return null;
            // Appearance overrides must not freeze source availability/name observations.
            // Inspect metadata only: never replace the custom pixels with a provider icon.
            var inspection = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.Inspect, item);
            var observed = await worker.RunAsync(inspection, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!IsCurrentRaster(key)) return null;
            if (LocalContentProtocol.Validate(inspection, observed)) Observe(key.ItemId, observed, key: key);
            return new("image/bgra32", icon.Pixels) { PixelWidth = icon.Width, PixelHeight = icon.Height };
        }
        var request = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.Icon, item);
        var result = await worker.RunAsync(request, cancellation).ConfigureAwait(false);
        if (!LocalContentProtocol.Validate(request, result)) return null;
        if (cancellation.IsCancellationRequested || !IsCurrentRaster(key)) return null;
        Observe(key.ItemId, result, key: key);
        return LocalContentProtocol.ValidPixels(result.Pixels, result.Width, result.Height)
            ? new("image/bgra32", result.Pixels!) { PixelWidth = result.Width, PixelHeight = result.Height } : null;
    }
    public void Observe(string id, LocalContentResponse result, ContentItem? expected = null, ContentAssetKey? key = null)
    {
        bool changed;
        lock (gate)
        {
            if (!items.TryGetValue(id, out var entry) || expected is not null && entry.Item != expected || key is not null && !Current(key)) return;
            var old = metadata[id];
            // Busy/timeouts are not proof that the file vanished; leave prior availability.
            bool available = result.Outcome is ContentOpenOutcome.Busy or ContentOpenOutcome.TimedOut or ContentOpenOutcome.Cancelled
                ? old.Available : result.Availability == ContentAvailability.Available;
            var next = (Available: available, Name: result.Name ?? old.Name, Type: result.TypeName ?? old.Type);
            changed = next != old;
            metadata[id] = next;
            if (changed) version = checked(version + 1);
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Refresh()
    {
        lock (gate)
        {
            foreach (var id in items.Keys.ToArray()) items[id] = items[id] with { Generation = checked(++rasterGeneration) };
            version = checked(version + 1);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public static string AssetKey(ContentItem item) => $"local-icon:{item.Id:N}:{item.Version}";
    private static string AssetKeyFor(Entry entry) => entry.Icon is { } custom ? "custom:" + custom : AssetKey(entry.Item);
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
        ContentItem? dispatched = null;
        try
        {
            var relation = ContentRelationPolicy.Resolve(currentCatalog(), request.ReadSet.PosterId, intent.Target.ItemId);
            if (relation is null || relation.Witness != request.ReadSet)
                result = new(request.ActivationId, ContentAvailability.Unavailable, "关联已变化，未打开。") { Outcome = ContentOpenOutcome.Unavailable };
            else
            {
                dispatched = relation.Item;
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
        if (dispatched is not null) views.Observe(intent.Target.ItemId, result, dispatched);
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
        views.Observe(itemId, result, resolved.Item);
        // Reveal deliberately does not write an open fact.
        return new(result.Evidence != ContentOpenEvidence.None, result.Message,
            result.Evidence != ContentOpenEvidence.None ? ContentOpenOutcome.Succeeded : ContentOpenOutcome.Unavailable);
    }
}
