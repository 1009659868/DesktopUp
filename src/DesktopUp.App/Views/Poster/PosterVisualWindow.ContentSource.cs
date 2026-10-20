using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentSourcePorts _contentPorts = contentPorts ?? ContentSourcePorts.Demo;
    private EventHandler? _contentSourceChanged;

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
        _contentPorts = ports;
        var binding = _contentSession.BindSource();
        if (ports.Views is IObservableContentItemViewSource observable)
        {
            var dispatcher = _window?.DispatcherQueue ?? throw new InvalidOperationException("Source binding requires a live dispatcher.");
            _contentSourceChanged = (_, _) =>
            {
                if (!binding.TryQueue()) return;
                if (!dispatcher.TryEnqueue(() =>
                {
                    if (binding.Complete() && !_disposed && ReferenceEquals(_contentPorts, ports)) RefreshContentSource();
                })) binding.Complete();
            };
            observable.Changed += _contentSourceChanged;
        }
        RefreshContentSource();
    }

    private void RefreshContentSource()
    {
        if (_disposed || _contentPorts.Views.Version <= _contentSession.SourceVersion || _contentPorts.Views.Version < 1) return;
        if (!_contentSession.TryAcceptSource(_contentPorts.Views.Version))
        {
            // Invalidate result admission immediately, but retain the R4 prepared tree until
            // its existing end/recovery safe point. No new timer or parallel layout owner.
            if (_contentPorts.Assets is IContentRasterIdentity) Rasters.PruneChangedDemand();
            else { Rasters.ClearDemand(); }
            return;
        }
        // Keep the same cache's real in-flight budget across metadata churn. A source which
        // ignores cancellation cannot gain four more slots on every version notification.
        bool invalidated;
        if (_contentPorts.Assets is IContentRasterIdentity) invalidated = Rasters.PruneChangedDemand();
        else
        {
            Rasters.ClearDemand(); invalidated = true;
        }
        if (invalidated)
        {
            _contentSpaceEpoch = checked(_contentSpaceEpoch + 1);
            CancelContentGesture("source-version-changed"); _contentActivation.Reset();
        }
        if (!_contentSession.IsSuspended && _window is not null) RefreshContentSurface();
        spatial.NotifyContentEditorChanged(PosterId);
    }

    private void ReleaseContentSourceSubscription()
    {
        if (_disposed) { _contentOpenLifetime++; _contentOpenCancellation.Cancel(); }
        if (_contentSourceChanged is not null && _contentPorts.Views is IObservableContentItemViewSource previous)
            previous.Changed -= _contentSourceChanged;
        _contentSourceChanged = null;
        _contentSession.SourceRefresh.Close();
    }

    private IReadOnlyList<ContentItemView> ReadContentViews() =>
        ContentSourceProjection.Read(_contentPorts.Views, ContentDocument, _contentSession.SourceVersion);
}
