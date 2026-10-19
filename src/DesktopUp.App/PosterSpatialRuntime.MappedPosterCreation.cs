using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

// The host prepares spatial placement and publishes; Application owns mapping creation.
internal sealed partial class PosterSpatialRuntime
{
    private MappedDirectoryUseCase? _mapping;
    internal void ConfigureMappingUseCase(MappedDirectoryUseCase mapping) => _mapping = mapping;

    internal async Task<string> CreateMappedPoster(string path, CancellationToken cancellation)
    {
        // The center owns its picker/action lease. It intentionally blocks outside drag/drop,
        // but must not reject its own catalog creation because that same lease is active.
        if (!FileServiceReady || _mapping is null) throw new IOException("请先结束当前文件操作。");
        if (!state.TryGetCurrent(out var current) || current is null) throw new IOException("桌面尚未就绪。");
        var reads = new StateReadSet(current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
            CatalogVersion: current.CatalogVersion, ContentVersion: current.ContentVersion);
        CreatePosterCommand? prepared = null;
        var request = new ControlCenterCatalogRequest("map-" + Guid.NewGuid().ToString("N"), current.Revision, reads, null, Path.GetFileName(path));
        var preparation = ApplyControlCenterCatalog(request, command =>
        {
            prepared = command;
            // Only captures the validated placement command; no state is published here.
            return new(StateCommandStatus.NotReady, request.RequestId, current.Revision, current.Revision, current, null);
        });
        if (prepared is null) throw new IOException(preparation.Error);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _fileTransferStopping.Token);
        var result = await Task.Run(() => _mapping.Create(prepared, path, stopping.Token));
        state.Refresh();
        if (result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed)) throw new IOException(result.Error);
        log.Write("info", "content.directory_mapped", new { result.ResultPosterId, path });
        return result.ResultPosterId!;
    }
}
