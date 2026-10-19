using DesktopUp.Application;
using DesktopUp.Domain;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal readonly record struct ContentRasterActivity(bool Closed, bool Suspended, bool Collapsed, bool Transition);

/// <summary>One UI-thread raster binding owns demand, flights and cache subscription. Provider owns
/// bytes; Presenter owns decoded UI images. Activity is sampled only on the UI thread.</summary>
internal sealed class ContentRasterBinding(PosterContentPresenter presenter, PosterInputSessionKey identity,
    IResourceLedger ledger, Func<ContentRasterActivity> activity)
{
    private ContentAssetCache? cache;
    private readonly Dictionary<string,ContentAssetKey> visible=new(StringComparer.Ordinal);
    private readonly HashSet<ContentAssetKey> requests=new();
    private readonly Dictionary<ContentAssetKey,IDisposable> leases=new();
    private readonly HashSet<ContentAssetKey> attempted = new();
    private Action? slotFreed;
    private int pumpQueued;
    private long acceptedCount;
    private long requestedCount;
    private PosterPresentationParameters? currentTheme;
    private long themeVersion=1, lifetimeVersion;

    private IContentAssetProvider? provider;
    private DispatcherQueue? dispatcher;
    public ContentAssetCache? Cache => cache;
    public IReadOnlyDictionary<string, ContentAssetKey> VisibleKeys => visible;
    public int Requests => requests.Count;
    public int Attempted => attempted.Count;
    public long RequestedCount => requestedCount;
    public long AcceptedCount => acceptedCount;
    // Shared visible-demand budget; real Shell work is isolated behind the asset provider.
    // DEMO has no AssetKey and keeps its existing vector fallback.
    public void Sync(IContentAssetProvider source, DispatcherQueue ui, DisplayMonitorSnapshot monitor,
        PosterPresentationParameters theme, long sourceVersion, ContentBrowseMode mode,
        ContentLayoutDocument document, ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport, IReadOnlyList<ContentItemView> views, IncomingLayoutPreview? incoming = null)
    {
        var state = activity();
        if (state.Closed) return;
        if (provider is not null && !ReferenceEquals(provider, source)) Reset();
        provider = source; dispatcher = ui;
        presenter.VisibleKeys = visible;
        if (state.Suspended) { Reset(); return; }
        if (state.Collapsed)
        {
            // Posture is not a source/window lifetime boundary. Keep the last bounded set
            // of decoded images so the prepared expansion tree can paint them on frame one.
            // Stop unseen work, but retain the same cache and real in-flight concurrency budget.
            // Source notifications still prune obsolete keys while compact; destruction and
            // source replacement still release everything through Reset.
            cache?.SetVisible([]);
            attempted.Clear();
            return;
        }
        if (state.Transition) return;
        if (cache is null)
        {
            var created = cache = new(source);
            var dispatcher = ui;
            long lifetime = lifetimeVersion;
            slotFreed = () =>
            {
                if (Interlocked.Exchange(ref pumpQueued, 1) != 0) return;
                if (!dispatcher.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref pumpQueued, 0);
                    if (!activity().Closed && lifetime == lifetimeVersion && ReferenceEquals(created, cache)) Pump();
                })) Interlocked.Exchange(ref pumpQueued, 0);
            };
            created.LoadFinished += slotFreed;
        }
        if(currentTheme!=theme)
        {
            currentTheme=theme;
            themeVersion=checked(themeVersion+1);
        }
        visible.Clear();
        // Admission is bounded to the cache entry budget, independent of the rendered Item count.
        IEnumerable<ContentAssetKey> demands = ContentAssetDemand.Build(document, snapshot, viewport, mode, views,
            identity.Epoch, monitor.DpiX, monitor.DpiY, themeVersion, sourceVersion);
        if (incoming is not null)
            demands = demands.Concat(ContentAssetDemand.Build(incoming.Document, incoming.Snapshot, viewport, mode, views,
                identity.Epoch, monitor.DpiX, monitor.DpiY, themeVersion, sourceVersion));
        // Preserve original-visible pixels during hover. Newly visible real items may fill only
        // the remaining budget; placeholders have no source/asset and never reach the worker.
        foreach (var demand in demands.DistinctBy(k => k.ItemId).Take(64))
        {
            var key = provider is IContentRasterIdentity raster ? raster.RasterKey(demand) : demand;
            if (provider is IContentRasterIdentity current && !current.IsCurrentRaster(key)) continue;
            visible.Add(key.ItemId, key);
        }
        attempted.IntersectWith(visible.Values);
        foreach (var expired in presenter.Bitmaps.Keys.Where(k => !visible.Values.Contains(k)).ToArray()) presenter.Bitmaps.Remove(expired);
        cache.SetVisible(visible.Values);
        Pump();
    }

    private void Pump()
    {
        var state = activity();
        if (state.Closed || state.Suspended || state.Collapsed || state.Transition || cache is null) return;
        foreach(var key in visible.Values)
        {
            if (cache.RunningLoads >= 4) break;
            if (attempted.Contains(key) || cache.IsLoading(key)) continue;
            // A cached byte payload may return after its decoded bitmap was evicted offscreen.
            if(presenter.Bitmaps.ContainsKey(key) || !requests.Add(key)) continue;
            attempted.Add(key);
            requestedCount++;
            var lease=ledger.Acquire("content.asset-request:"+Guid.NewGuid().ToString("N"),key.ItemId);
            leases[key]=lease;
            _=Observe(cache,key,lifetimeVersion,lease);
        }
    }

    private async Task Observe(ContentAssetCache observed,ContentAssetKey key,long lifetime,IDisposable lease)
    {
        // Acquire the WinUI dispatcher while still on its owning thread, before ConfigureAwait(false).
        var dispatcher = this.dispatcher;
        ContentAssetResult? result=null;
        try { result=await observed.RequestAsync(key).ConfigureAwait(false); }
        catch(Exception) { /* Provider/key failures retain the existing DEMO fallback. */ }
        finally { lease.Dispose(); }
        // Captured lifetime plus a fresh UI-thread TryGet protects the completion-to-dispatch gap.
        if(dispatcher is null) return;
        dispatcher.TryEnqueue(()=>
        {
            if(activity().Closed || lifetime!=lifetimeVersion || !ReferenceEquals(observed,cache)) return;
            requests.Remove(key);leases.Remove(key);
            if (result?.Status is ContentAssetStatus.Capacity or ContentAssetStatus.Stale)
            {
                attempted.Remove(key);
                // The slot-free callback may beat this wrapper's UI cleanup. Retry admission
                // here too; IsLoading prevents spinning on a cancellation-resistant same-key flight.
                Pump();
            }
            if(result?.Status is not (ContentAssetStatus.Loaded or ContentAssetStatus.Cached) ||
                identity.PosterId!=key.PosterId || identity.Epoch!=key.SessionEpoch ||
                !visible.TryGetValue(key.ItemId,out var currentKey) || currentKey!=key ||
                provider is IContentRasterIdentity raster && !raster.IsCurrentRaster(key) ||
                !observed.TryGet(key,out var payload) || payload is null) return;
            acceptedCount++;
            if (payload.MediaType == "image/bgra32" && LocalContentProtocol.ValidPixels(payload.Bytes.ToArray(), payload.PixelWidth, payload.PixelHeight))
            {
                var bitmap = new WriteableBitmap(payload.PixelWidth, payload.PixelHeight);
                using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(payload.Bytes.Span);
                bitmap.Invalidate();
                presenter.Bitmaps[key] = bitmap;
                // Pixels do not alter layout, hit regions or control-center fields. Update only
                // the existing image hosts (including loop copies), even during an Item gesture.
                if (!activity().Transition) presenter.UpdateImage(key.ItemId);
            }
            // Only bounded, already-rasterized BGRA is consumed here. Shell/image decoding
            // remains outside this UI process, after the same stable-key check.
            // Do not interpret SourceKey as a path or mutate item selection/layout here.
        });
    }

    public bool PruneChangedDemand()
    {
        if (provider is not IContentRasterIdentity raster) return false;
        bool changed = false;
        foreach (var pair in visible.Where(p => !raster.IsCurrentRaster(p.Value)).ToArray())
        {
            visible.Remove(pair.Key); presenter.Bitmaps.Remove(pair.Value);
            attempted.Remove(pair.Value); changed = true;
        }
        cache?.SetVisible(visible.Values);
        return changed;
    }

    public void ClearDemand()
    {
        cache?.SetVisible([]);
        visible.Clear(); attempted.Clear();
    }

    public void Reset()
    {
        presenter.Bitmaps.Clear();
        lifetimeVersion=unchecked(lifetimeVersion+1);
        if (cache is not null && slotFreed is not null) cache.LoadFinished -= slotFreed;
        cache?.Dispose();cache=null;
        foreach(var lease in leases.Values) lease.Dispose();
        leases.Clear();requests.Clear();visible.Clear();
        currentTheme=null;
        slotFreed=null; attempted.Clear(); Interlocked.Exchange(ref pumpQueued,0);
    }
}
