using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentSourcePorts _contentPorts = contentPorts ?? ContentSourcePorts.Demo;
    private long _contentSourceVersion = contentPorts?.Views.Version ?? 1;
    private EventHandler? _contentSourceChanged;
    private int _contentSourceRefreshQueued;
    private bool _contentSourceRefreshPending;

    // Host-owned adapter binding, not a template/script/plugin execution entry point.
    internal void BindContentSource(ContentSourcePorts ports)
    {
        ArgumentNullException.ThrowIfNull(ports.Views); ArgumentNullException.ThrowIfNull(ports.Assets);
        ArgumentNullException.ThrowIfNull(ports.Open);
        if (_disposed || ports.Views.Version < 1) throw new InvalidOperationException("Invalid or disposed content source.");
        if (ReferenceEquals(_contentPorts, ports) && (ports.Views is not IObservableContentItemViewSource || _contentSourceChanged is not null))
        { RefreshContentSource(); return; }
        ReleaseContentSourceSubscription();
        _contentOpenLifetime++;
        ReleaseContentAssets();
        _contentPorts = ports; _contentSourceVersion = 0;
        if (ports.Views is IObservableContentItemViewSource observable)
        {
            var dispatcher = _window?.DispatcherQueue ?? throw new InvalidOperationException("Source binding requires a live dispatcher.");
            _contentSourceChanged = (_, _) =>
            {
                if (Interlocked.Exchange(ref _contentSourceRefreshQueued, 1) != 0) return;
                if (!dispatcher.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref _contentSourceRefreshQueued, 0);
                    if (!_disposed && ReferenceEquals(_contentPorts, ports)) RefreshContentSource();
                })) Interlocked.Exchange(ref _contentSourceRefreshQueued, 0);
            };
            observable.Changed += _contentSourceChanged;
        }
        RefreshContentSource();
    }

    private void RefreshContentSource()
    {
        if (_disposed || _contentPorts.Views.Version == _contentSourceVersion) return;
        // Reject a regressing source; never re-author layout on a metadata notification.
        if (_contentPorts.Views.Version < _contentSourceVersion || _contentPorts.Views.Version < 1) return;
        if (_transitionLayout is not null || _activeCommandRequestId is not null)
        {
            // Invalidate result admission immediately, but retain the R4 prepared tree until
            // its existing end/recovery safe point. No new timer or parallel layout owner.
            _contentSourceRefreshPending = true;
            if (_contentPorts.Assets is IContentRasterIdentity) PruneChangedRasterDemand();
            else { _contentAssets?.SetVisible([]); _contentVisibleAssetKeys.Clear(); _contentAssetAttempted.Clear(); }
            return;
        }
        _contentSourceRefreshPending = false;
        _contentSourceVersion = _contentPorts.Views.Version;
        // Keep the same cache's real in-flight budget across metadata churn. A source which
        // ignores cancellation cannot gain four more slots on every version notification.
        bool invalidated;
        if (_contentPorts.Assets is IContentRasterIdentity) invalidated = PruneChangedRasterDemand();
        else
        {
            _contentAssets?.SetVisible([]);
            _contentVisibleAssetKeys.Clear(); _contentAssetAttempted.Clear(); invalidated = true;
        }
        if (invalidated)
        {
            _contentSpaceEpoch = checked(_contentSpaceEpoch + 1);
            CancelContentGesture("source-version-changed"); _contentActivation.Reset();
        }
        if (!_contentPresentationSuspended && _window is not null) RefreshContentSurface();
        spatial.NotifyContentEditorChanged(PosterId);
    }

    private void ReleaseContentSourceSubscription()
    {
        if (_disposed) { _contentOpenLifetime++; _contentOpenCancellation.Cancel(); }
        if (_contentSourceChanged is not null && _contentPorts.Views is IObservableContentItemViewSource previous)
            previous.Changed -= _contentSourceChanged;
        _contentSourceChanged = null;
        Interlocked.Exchange(ref _contentSourceRefreshQueued, 0);
    }

    private IReadOnlyList<ContentItemView> ReadContentViews() =>
        ContentSourceProjection.Read(_contentPorts.Views, ContentDocument, _contentSourceVersion);
}
