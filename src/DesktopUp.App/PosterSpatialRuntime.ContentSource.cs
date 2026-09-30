using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Host adapter lifetime follows the catalog, not a temporary hidden/recreated HWND.
    private readonly Dictionary<string, ContentSourcePorts> _contentSources = new(StringComparer.Ordinal);
    internal ContentSourcePorts ContentSourceFor(string id) => _contentSources.GetValueOrDefault(id) ?? ContentSourcePorts.Demo;
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
