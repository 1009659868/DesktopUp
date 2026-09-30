using DesktopUp.Application;
using DesktopUp.Domain;

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
    private PosterPresentationParameters? _contentAssetTheme;
    private long _contentAssetThemeVersion=1, _contentAssetLifetime;

    // L06 binds the real lifetime and bounded request path, but not file extraction or image decoding.
    // DEMO views currently have no AssetKey; their existing vector/monogram fallback remains unchanged.
    private void SyncContentAssets(ContentLayoutDocument document,ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport,IReadOnlyList<ContentItemView> views)
    {
        if(_disposed || _window is null || _monitor is null) return;
        if (_contentPresentationSuspended || _geometry.IsCollapsed) { ReleaseContentAssets(); return; }
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
        foreach (var key in ContentAssetDemand.Build(document, snapshot, viewport, _contentBrowseMode, views,
            inputSession.Epoch, _monitor.DpiX, _monitor.DpiY, _contentAssetThemeVersion, _contentSourceVersion))
            _contentVisibleAssetKeys.Add(key.ItemId, key);
        _contentAssetAttempted.IntersectWith(_contentVisibleAssetKeys.Values);
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
            if(_contentAssets.TryGet(key,out _) || !_contentAssetRequests.Add(key)) continue;
            _contentAssetAttempted.Add(key);
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
                !cache.TryGet(key,out var payload) || payload is null) return;
            _contentAssetAcceptedCount++;
            // No bitmap decoder is registered in this stage. A future renderer may consume this
            // bounded payload only after this same stable-key check, with its own decoded-pixel cap.
            // Do not interpret SourceKey as a path or mutate item selection/layout here.
        });
    }

    private void ReleaseContentAssets()
    {
        _contentAssetLifetime=unchecked(_contentAssetLifetime+1);
        if (_contentAssets is not null && _contentAssetSlotFreed is not null) _contentAssets.LoadFinished -= _contentAssetSlotFreed;
        _contentAssets?.Dispose();_contentAssets=null;
        foreach(var lease in _contentAssetRequestLeases.Values) lease.Dispose();
        _contentAssetRequestLeases.Clear();_contentAssetRequests.Clear();_contentVisibleAssetKeys.Clear();
        _contentAssetTheme=null;
        _contentAssetSlotFreed=null; _contentAssetAttempted.Clear(); Interlocked.Exchange(ref _contentAssetPumpQueued,0);
    }
}
