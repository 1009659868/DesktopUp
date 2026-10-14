using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal string? ContentActionPath(string posterId, string? itemId)
    {
        if (!TryGetProjection(out var projection) || projection is null) return null;
        if (itemId is not null)
        {
            var relation = ContentRelationPolicy.Resolve(projection.ContentCatalog, posterId, itemId);
            return relation is not null && LocalContentProtocol.IsSupported(relation.Item) ? relation.Item.Target.Locator : null;
        }
        var collectionId = projection.ContentCatalog.Bindings.SingleOrDefault(b => b.PosterId == posterId)?.CollectionId;
        var collection = projection.ContentCatalog.Collections.SingleOrDefault(c => c.Id == collectionId);
        if (collection?.Kind == ContentCollectionKind.Mapped) return collection.StorageLocator;
        return null; // A normal poster uses the explicitly selected storage, never an arbitrary old root.
    }

    internal ContentRelationResolution? ContentActionRelation(string posterId,string itemId) =>
        TryGetProjection(out var projection) && projection is not null
            ? ContentRelationPolicy.Resolve(projection.ContentCatalog,posterId,itemId) : null;

    internal string? ContentItemAtPath(string posterId, string path)
    {
        if (!TryGetProjection(out var projection) || projection is null) return null;
        return projection.ContentCatalog.PosterItems.Where(p => p.PosterId == posterId)
            .FirstOrDefault(p => string.Equals(ContentRelationPolicy.Resolve(projection.ContentCatalog, posterId,
                p.LayoutItemId)?.Item.Target.Locator, path, StringComparison.OrdinalIgnoreCase))?.LayoutItemId;
    }

    internal async Task<string?> ResolveActionDirectory(string posterId)
    {
        if(ContentActionPath(posterId,null) is { } mapped)
        { await ValidateMappedAction(posterId, null); return mapped; }
        if(_chooseTransferStorage is null || _transferStore is null)return null;
        return (await _chooseTransferStorage())?.Collection.StorageLocator;
    }

    internal Task ValidateMappedAction(string posterId, string? itemId) => _transferStore is null || ContentActionPath(posterId, null) is null ? Task.CompletedTask :
        Task.Run(() => new MappedDirectoryService(_transferStore).ValidateAction(posterId, itemId));

    internal async Task RenameContent(string posterId,string itemId,Guid contentId,long version,string name,string identity)
    {
        if(_transferStore is null)throw new IOException("文件服务尚未就绪。");
        await Task.Run(()=>new ContentRenameService(_transferStore).Rename(posterId,itemId,contentId,version,name,_fileTransferStopping.Token,identity));
        state.Refresh();
    }

    internal void RefreshFileActionState() => state.Refresh();

    internal async Task<NewContentLocation?> PrepareNewContent(string posterId)
    {
        if (!TransfersAvailable || _chooseTransferStorage is null || HasCenterDraft(posterId))
            throw new IOException("请先完成当前文件操作或未保存的编辑。");
        if (ContentActionPath(posterId, null) is not null)
        {
            var mapped = await Task.Run(() => new MappedDirectoryService(_transferStore!).ResolveNewContent(posterId));
            var current = new SqliteStateCommandRepository(_transferStore!).Read();
            return new(null, current.ContentVersion, current.Catalog.Posters.Single(p => p.Poster.PosterId == posterId).EntityVersion) { Mapping = mapped };
        }
        var storage = await _chooseTransferStorage();
        if (storage is null) return null;
        if (!TransfersAvailable || HasCenterDraft(posterId)) throw new IOException("当前状态已变化，请重新打开新建菜单。");
        var basis = new SqliteStateCommandRepository(_transferStore!).Read();
        var owner = basis.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == posterId)
            ?? throw new IOException("海报已关闭。");
        return new(storage, basis.ContentVersion, owner.EntityVersion);
    }

    internal async Task<string> CreateContent(string posterId, NewContentLocation location, NewContentKind kind,
        string name, string? target, CancellationToken cancellation)
    {
        if (!TransfersAvailable || HasCenterDraft(posterId)) throw new IOException("请等待当前文件操作完成。");
        _fileTransferBusy = true; _fileTransferPoster = posterId; ActivityChanged?.Invoke();
        var operation = Run();
        _fileTransferTask = ObserveForShutdown();
        return await operation;
        async Task ObserveForShutdown()
        { try { await operation; } catch (Exception) { /* The caller reports failure; shutdown still awaits cleanup. */ } }
        async Task<string> Run()
        {
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _fileTransferStopping.Token);
            try
            {
                string path = await Task.Run(() => new ContentCreationService(_transferStore!).Create(posterId, location, kind, name, target, stopping.Token));
                log.Write("info", "content.created", new { posterId, kind, path });
                return path;
            }
            finally
            {
                state.Refresh(); _fileTransferBusy = false; _fileTransferPoster = null; ActivityChanged?.Invoke();
            }
        }
    }
    internal void NotifyFileActionState(string? id)
    { NotifyContentEditorChanged(id); ActivityChanged?.Invoke(); }

    internal async Task CreateMappedPoster(string path, CancellationToken cancellation)
    {
        if (!TransfersAvailable || _transferStore is null) throw new IOException("请先结束当前文件操作。");
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
        var result = await Task.Run(() => new MappedDirectoryService(_transferStore).Create(prepared, path, stopping.Token));
        state.Refresh();
        if (result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed)) throw new IOException(result.Error);
        log.Write("info", "content.directory_mapped", new { result.ResultPosterId, path });
    }
}
