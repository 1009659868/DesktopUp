using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentRasterBinding? _rasterBinding;
    private ContentRasterBinding Rasters => _rasterBinding ??= new(Presenter, inputSession, ledger,
        () => new(_disposed, _contentSession.IsSuspended, _geometry.IsCollapsed, _transitionLayout is not null));

    private void SyncContentAssets(ContentLayoutDocument document, ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport, IReadOnlyList<ContentItemView> views, IncomingLayoutPreview? incoming = null)
    {
        if (_disposed || _window is null || _monitor is null) return;
        Rasters.Sync(_contentPorts.Assets, _window.DispatcherQueue, _monitor, _presentation.Parameters,
            _contentSession.SourceVersion, _contentLayout.BrowseMode, document, snapshot, viewport, views, incoming);
    }

    private void RefreshContentAssetDemand()
    {
        if (_contentLayout.Current is { } frame)
            SyncContentAssets(EffectiveContentDocument, frame.Snapshot, frame.Viewport, ReadContentViews());
    }

    private void ReleaseContentAssets() => _rasterBinding?.Reset();
}
