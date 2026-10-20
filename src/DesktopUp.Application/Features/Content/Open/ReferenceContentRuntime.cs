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
    public void RefreshItem(string id)
    {
        lock(gate)
        {
            if(!items.TryGetValue(id,out var entry))return;
            items[id]=entry with {Generation=checked(++rasterGeneration)};
            version=checked(version+1);
        }
        Changed?.Invoke(this,EventArgs.Empty);
    }
    public static string AssetKey(ContentItem item) => $"local-icon:{item.Id:N}:{item.Version}";
    private static string AssetKeyFor(Entry entry) => entry.Icon is { } custom ? "custom:" + custom : AssetKey(entry.Item);
}
