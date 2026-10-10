using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Host adapter lifetime follows the catalog, not a temporary hidden/recreated HWND.
    private readonly Dictionary<string, ContentSourcePorts> _contentSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Version, ContentSourcePorts Ports)> _referenceSources = new(StringComparer.Ordinal);
    private ILocalContentWorker? _referenceWorker;
    private IContentOpenJournal? _openJournal;
    private IContentIconStore? _iconStore;
    internal void ConfigureRealContent(ILocalContentWorker worker, IContentOpenJournal journal, IContentIconStore? icons = null)
    { _referenceWorker = worker; _openJournal = journal; _iconStore = icons; }
    internal ContentSourcePorts ContentSourceFor(string id)
    {
        if (TryGetProjection(out var projection) && projection!.ContentCatalog.Bindings.Any(b => b.PosterId == id))
        {
            if (!_referenceSources.TryGetValue(id, out var cached) || cached.Version != projection.ContentVersion)
            {
                ContentSourcePorts ports;
                if (_referenceWorker is not null && _openJournal is not null)
                {
                    var source = new ReferenceContentRuntime(projection.ContentCatalog, id, projection.ContentVersion, _referenceWorker, _iconStore);
                    ports = new(source, source, new ReferenceContentActions(
                        () => TryGetProjection(out var live) ? live!.ContentCatalog : ContentCatalogSnapshot.Empty,
                        source, _referenceWorker, _openJournal));
                }
                else ports = new(new ReferenceContentViewSource(projection.ContentCatalog, id, projection.ContentVersion),
                    NullContentAssetProvider.Instance, DemoContentOpenHandler.Instance)
                    { UnavailableOpenReason = "真实内容服务尚未就绪，本次未执行文件。" };
                _referenceSources[id] = cached = (projection.ContentVersion, ports);
            }
            return cached.Ports;
        }
        _referenceSources.Remove(id);
        return _contentSources.GetValueOrDefault(id) ?? ContentSourcePorts.Demo;
    }
    private void RefreshReferenceSources(StateProjectionChangedEventArgs change)
    {
        foreach (var id in _referenceSources.Keys.Where(id => !change.Projection.PostersById.ContainsKey(id)).ToArray()) _referenceSources.Remove(id);
        foreach (var id in change.ChangedPosterIds)
            if (_windows.TryGetValue(id, out var window) && (change.Projection.ContentCatalog.Bindings.Any(b => b.PosterId == id) || _referenceSources.ContainsKey(id)))
                window.BindContentSource(ContentSourceFor(id));
    }
    internal void BindContentSource(string id, ContentSourcePorts ports)
    {
        if (!TryGetProjection(out var projection) || projection?.PostersById.ContainsKey(id) != true)
            throw new InvalidOperationException("Source target does not exist.");
        ArgumentNullException.ThrowIfNull(ports.Views); ArgumentNullException.ThrowIfNull(ports.Assets); ArgumentNullException.ThrowIfNull(ports.Open);
        if (ports.Views.Version < 1) throw new ArgumentException("Invalid source version.");
        if (ReferenceEquals(ports, ContentSourcePorts.Demo)) _contentSources.Remove(id);
        else _contentSources[id] = ports;
        if (_windows.TryGetValue(id, out var window)) window.BindContentSource(ports);
    }
}
