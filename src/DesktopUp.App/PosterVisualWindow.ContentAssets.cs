using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentAssetCache? _contentAssets;
    private readonly Dictionary<string,ContentAssetKey> _contentVisibleAssetKeys=new(StringComparer.Ordinal);
    private readonly HashSet<ContentAssetKey> _contentAssetRequests=new();
    private readonly Dictionary<ContentAssetKey,IDisposable> _contentAssetRequestLeases=new();
    private PosterPresentationParameters? _contentAssetTheme;
    private long _contentAssetThemeVersion=1, _contentAssetLifetime;

    // L06 binds the real lifetime and bounded request path, but not file extraction or image decoding.
    // DEMO views currently have no AssetKey; their existing vector/monogram fallback remains unchanged.
    private void SyncContentAssets(ContentLayoutDocument document,ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport,IReadOnlyList<ContentItemView> views)
    {
        if(_disposed || _window is null || _monitor is null) return;
        _contentAssets??=new(NullContentAssetProvider.Instance);
        if(_contentAssetTheme!=_presentation.Parameters)
        {
            _contentAssetTheme=_presentation.Parameters;
            _contentAssetThemeVersion=checked(_contentAssetThemeVersion+1);
        }
        double pitch=snapshot.CellDip+snapshot.GapDip;
        var overscan=new ContentRect(snapshot.ScrollXDip,Math.Max(0,snapshot.ScrollYDip-pitch),
            viewport.WidthDip,viewport.HeightDip+pitch*2);
        var byId=views.ToDictionary(v=>v.ItemId,StringComparer.Ordinal);
        _contentVisibleAssetKeys.Clear();
        // Asset admission is independent of item capacity: keep drawing every item if budget is full.
        foreach(var slot in snapshot.Items.Where(p=>p.Bounds.Intersects(overscan)).OrderBy(p=>p.Clip.IsEmpty))
        {
            if(_contentVisibleAssetKeys.Count>=512) break;
            if(!byId.TryGetValue(slot.ItemId,out var view) || string.IsNullOrWhiteSpace(view.AssetKey) ||
                view.AssetKey.Length>256 || view.AssetKey.Any(char.IsControl)) continue;
            var placement=document.Placements[slot.SourceIndex];
            int width=(int)Math.Clamp(Math.Ceiling(slot.Bounds.Width*_monitor.ScaleX),1,4096);
            int height=(int)Math.Clamp(Math.Ceiling(slot.Bounds.Height*_monitor.ScaleY),1,4096);
            var key=new ContentAssetKey(document.PosterId,slot.ItemId,inputSession.Epoch,document.DocumentVersion,
                document.ItemSetVersion,placement.Version,view.AssetKey,width,height,_monitor.DpiX,_monitor.DpiY,_contentAssetThemeVersion);
            _contentVisibleAssetKeys.Add(slot.ItemId,key);
        }
        _contentAssets.SetVisible(_contentVisibleAssetKeys.Values);
        foreach(var key in _contentVisibleAssetKeys.Values)
        {
            if(_contentAssets.TryGet(key,out _) || !_contentAssetRequests.Add(key)) continue;
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
            if(result?.Status is not (ContentAssetStatus.Loaded or ContentAssetStatus.Cached) ||
                inputSession.PosterId!=key.PosterId || inputSession.Epoch!=key.SessionEpoch ||
                !_contentVisibleAssetKeys.TryGetValue(key.ItemId,out var currentKey) || currentKey!=key ||
                !cache.TryGet(key,out var payload) || payload is null) return;
            // No bitmap decoder is registered in this stage. A future renderer may consume this
            // bounded payload only after this same stable-key check, with its own decoded-pixel cap.
            // Do not interpret SourceKey as a path or mutate item selection/layout here.
        });
    }

    private void ReleaseContentAssets()
    {
        _contentAssetLifetime=unchecked(_contentAssetLifetime+1);
        _contentAssets?.Dispose();_contentAssets=null;
        foreach(var lease in _contentAssetRequestLeases.Values) lease.Dispose();
        _contentAssetRequestLeases.Clear();_contentAssetRequests.Clear();_contentVisibleAssetKeys.Clear();
        _contentAssetTheme=null;
    }
}
