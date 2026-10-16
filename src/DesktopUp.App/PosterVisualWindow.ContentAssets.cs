using DesktopUp.Application;
using DesktopUp.Domain;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentAssetCache? _contentAssets;
    private readonly Dictionary<string,ContentAssetKey> _contentVisibleAssetKeys=new(StringComparer.Ordinal);
    private readonly HashSet<ContentAssetKey> _contentAssetRequests=new();
    private readonly Dictionary<ContentAssetKey,IDisposable> _contentAssetRequestLeases=new();
    private readonly HashSet<ContentAssetKey> _contentAssetAttempted = new();
    private Action? _contentAssetSlotFreed;
    private int _contentAssetPumpQueued;
    private long _contentAssetAcceptedCount;
    private long _contentAssetRequestCount;
    private PosterPresentationParameters? _contentAssetTheme;
    private long _contentAssetThemeVersion=1, _contentAssetLifetime;

    // Shared visible-demand budget; real Shell work is isolated behind the asset provider.
    // DEMO has no AssetKey and keeps its existing vector fallback.
    private void SyncContentAssets(ContentLayoutDocument document,ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport,IReadOnlyList<ContentItemView> views,IncomingLayoutPreview? incoming = null)
    {
        if(_disposed || _window is null || _monitor is null) return;
        if (_contentPresentationSuspended) { ReleaseContentAssets(); return; }
        if (_geometry.IsCollapsed)
        {
            // Posture is not a source/window lifetime boundary. Keep the last bounded set
            // of decoded images so the prepared expansion tree can paint them on frame one.
            // Stop unseen work, but retain the same cache and real in-flight concurrency budget.
            // Source notifications still prune obsolete keys while compact; destruction and
            // source replacement still release everything through ReleaseContentAssets.
            _contentAssets?.SetVisible([]);
            _contentAssetAttempted.Clear();
            return;
        }
        if (_transitionLayout is not null) return;
        if (_contentAssets is null)
        {
            var cache = _contentAssets = new(_contentPorts.Assets);
            var dispatcher = _window.DispatcherQueue;
            long lifetime = _contentAssetLifetime;
            _contentAssetSlotFreed = () =>
            {
                if (Interlocked.Exchange(ref _contentAssetPumpQueued, 1) != 0) return;
                if (!dispatcher.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref _contentAssetPumpQueued, 0);
                    if (!_disposed && lifetime == _contentAssetLifetime && ReferenceEquals(cache, _contentAssets)) PumpContentAssets();
                })) Interlocked.Exchange(ref _contentAssetPumpQueued, 0);
            };
            cache.LoadFinished += _contentAssetSlotFreed;
        }
        if(_contentAssetTheme!=_presentation.Parameters)
        {
            _contentAssetTheme=_presentation.Parameters;
            _contentAssetThemeVersion=checked(_contentAssetThemeVersion+1);
        }
        _contentVisibleAssetKeys.Clear();
        // Admission is bounded to the cache entry budget, independent of the rendered Item count.
        IEnumerable<ContentAssetKey> demands = ContentAssetDemand.Build(document, snapshot, viewport, _contentBrowseMode, views,
            inputSession.Epoch, _monitor.DpiX, _monitor.DpiY, _contentAssetThemeVersion, _contentSourceVersion);
        if (incoming is not null)
            demands = demands.Concat(ContentAssetDemand.Build(incoming.Document, incoming.Snapshot, viewport, _contentBrowseMode, views,
                inputSession.Epoch, _monitor.DpiX, _monitor.DpiY, _contentAssetThemeVersion, _contentSourceVersion));
        // Preserve original-visible pixels during hover. Newly visible real items may fill only
        // the remaining budget; placeholders have no source/asset and never reach the worker.
        foreach (var demand in demands.DistinctBy(k => k.ItemId).Take(64))
        {
            var key = _contentPorts.Assets is IContentRasterIdentity raster ? raster.RasterKey(demand) : demand;
            if (_contentPorts.Assets is IContentRasterIdentity current && !current.IsCurrentRaster(key)) continue;
            _contentVisibleAssetKeys.Add(key.ItemId, key);
        }
        _contentAssetAttempted.IntersectWith(_contentVisibleAssetKeys.Values);
        foreach (var expired in _contentBitmaps.Keys.Where(k => !_contentVisibleAssetKeys.Values.Contains(k)).ToArray()) _contentBitmaps.Remove(expired);
        _contentAssets.SetVisible(_contentVisibleAssetKeys.Values);
        PumpContentAssets();
    }

    private void RefreshContentAssetDemand()
    {
        if (_contentSnapshot is { } snapshot && _contentViewport is { } viewport)
            SyncContentAssets(EffectiveContentDocument, snapshot, viewport, ReadContentViews());
    }

    private void PumpContentAssets()
    {
        if (_disposed || _contentPresentationSuspended || _geometry.IsCollapsed || _transitionLayout is not null || _contentAssets is null) return;
        foreach(var key in _contentVisibleAssetKeys.Values)
        {
            if (_contentAssets.RunningLoads >= 4) break;
            if (_contentAssetAttempted.Contains(key) || _contentAssets.IsLoading(key)) continue;
            // A cached byte payload may return after its decoded bitmap was evicted offscreen.
            if(_contentBitmaps.ContainsKey(key) || !_contentAssetRequests.Add(key)) continue;
            _contentAssetAttempted.Add(key);
            _contentAssetRequestCount++;
            var lease=ledger.Acquire("content.asset-request:"+Guid.NewGuid().ToString("N"),key.ItemId);
            _contentAssetRequestLeases[key]=lease;
            _=ObserveContentAssetAsync(_contentAssets,key,_contentAssetLifetime,lease);
        }
    }

    private async Task ObserveContentAssetAsync(ContentAssetCache cache,ContentAssetKey key,long lifetime,IDisposable lease)
    {
        // Acquire the WinUI dispatcher while still on its owning thread, before ConfigureAwait(false).
        var dispatcher=_window?.DispatcherQueue;
        ContentAssetResult? result=null;
        try { result=await cache.RequestAsync(key).ConfigureAwait(false); }
        catch(Exception) { /* Provider/key failures retain the existing DEMO fallback. */ }
        finally { lease.Dispose(); }
        // Captured lifetime plus a fresh UI-thread TryGet protects the completion-to-dispatch gap.
        if(dispatcher is null) return;
        dispatcher.TryEnqueue(()=>
        {
            if(_disposed || lifetime!=_contentAssetLifetime || !ReferenceEquals(cache,_contentAssets)) return;
            _contentAssetRequests.Remove(key);_contentAssetRequestLeases.Remove(key);
            if (result?.Status is ContentAssetStatus.Capacity or ContentAssetStatus.Stale)
            {
                _contentAssetAttempted.Remove(key);
                // The slot-free callback may beat this wrapper's UI cleanup. Retry admission
                // here too; IsLoading prevents spinning on a cancellation-resistant same-key flight.
                PumpContentAssets();
            }
            if(result?.Status is not (ContentAssetStatus.Loaded or ContentAssetStatus.Cached) ||
                inputSession.PosterId!=key.PosterId || inputSession.Epoch!=key.SessionEpoch ||
                !_contentVisibleAssetKeys.TryGetValue(key.ItemId,out var currentKey) || currentKey!=key ||
                _contentPorts.Assets is IContentRasterIdentity raster && !raster.IsCurrentRaster(key) ||
                !cache.TryGet(key,out var payload) || payload is null) return;
            _contentAssetAcceptedCount++;
            if (payload.MediaType == "image/bgra32" && LocalContentProtocol.ValidPixels(payload.Bytes.ToArray(), payload.PixelWidth, payload.PixelHeight))
            {
                var bitmap = new WriteableBitmap(payload.PixelWidth, payload.PixelHeight);
                using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(payload.Bytes.Span);
                bitmap.Invalidate();
                _contentBitmaps[key] = bitmap;
                // Pixels do not alter layout, hit regions or control-center fields. Update only
                // the existing image hosts (including loop copies), even during an Item gesture.
                if (_transitionLayout is null) UpdateContentCardImage(key.ItemId);
            }
            // Only bounded, already-rasterized BGRA is consumed here. Shell/image decoding
            // remains outside this UI process, after the same stable-key check.
            // Do not interpret SourceKey as a path or mutate item selection/layout here.
        });
    }

    private bool PruneChangedRasterDemand()
    {
        if (_contentPorts.Assets is not IContentRasterIdentity raster) return false;
        bool changed = false;
        foreach (var pair in _contentVisibleAssetKeys.Where(p => !raster.IsCurrentRaster(p.Value)).ToArray())
        {
            _contentVisibleAssetKeys.Remove(pair.Key); _contentBitmaps.Remove(pair.Value);
            _contentAssetAttempted.Remove(pair.Value); changed = true;
        }
        _contentAssets?.SetVisible(_contentVisibleAssetKeys.Values);
        return changed;
    }

    private void ReleaseContentAssets()
    {
        _contentBitmaps.Clear();
        _contentAssetLifetime=unchecked(_contentAssetLifetime+1);
        if (_contentAssets is not null && _contentAssetSlotFreed is not null) _contentAssets.LoadFinished -= _contentAssetSlotFreed;
        _contentAssets?.Dispose();_contentAssets=null;
        foreach(var lease in _contentAssetRequestLeases.Values) lease.Dispose();
        _contentAssetRequestLeases.Clear();_contentAssetRequests.Clear();_contentVisibleAssetKeys.Clear();
        _contentAssetTheme=null;
        _contentAssetSlotFreed=null; _contentAssetAttempted.Clear(); Interlocked.Exchange(ref _contentAssetPumpQueued,0);
    }
}
