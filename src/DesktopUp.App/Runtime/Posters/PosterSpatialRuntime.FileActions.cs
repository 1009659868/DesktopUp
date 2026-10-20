using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private IContentFileActionService? _fileActions;
    internal void ConfigureFileActions(IContentFileActionService actions) => _fileActions = actions;
    private IContentFileActionService FileActions => _fileActions ?? throw new IOException("文件服务尚未就绪。");
    internal Task<string> CaptureFileActionIdentity(string path) => Task.Run(() => FileActions.CaptureIdentity(path));
    internal Task<string> SuggestContentName(string directory, NewContentKind kind) =>
        Task.Run(() => FileActions.SuggestName(directory, kind));
    internal async Task<ContentShellOutcome> InvokeShellAction(string posterId, string? itemId,
        ContentRelationResolution? expected, NewContentLocation? location, string path, ShellUiAction action,
        int x, int y, CancellationToken token)
    {
        var outcome = await Task.Run(() => FileActions.InvokeShell(posterId, itemId, expected, location, path, action, x, y, token));
        // Publish accepted facts even when the invoking view no longer exists.
        if (outcome.Registered) state.Refresh();
        return outcome;
    }

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
        if(_chooseTransferStorage is null || _fileActions is null)return null;
        return (await _chooseTransferStorage())?.Collection.StorageLocator;
    }

    internal Task ValidateMappedAction(string posterId, string? itemId) => _fileActions is null || ContentActionPath(posterId, null) is null ? Task.CompletedTask :
        Task.Run(() => FileActions.ValidateMappedAction(posterId, itemId));

    internal async Task RenameContent(string posterId,string itemId,Guid contentId,long version,string name,string identity)
    {
        await Task.Run(()=>FileActions.Rename(posterId,itemId,contentId,version,name,identity,_contentOperations.Stopping));
        state.Refresh();
    }

    internal void RefreshFileActionState() => state.Refresh();

    internal async Task<ContentDeletionResult> DeleteContent(string posterId, string[] items, nint owner, CancellationToken cancellation)
    {
        if (!TransfersAvailable || HasCenterDraft(posterId)) throw new IOException("请先完成当前文件操作。");
        using var active = BeginFileOperation(posterId);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _contentOperations.Stopping);
        var operation = Task.Run(() => FileActions.Delete(posterId, items, owner, stop.Token));
        try
        {
            var result = await operation;
            log.Write("info", "content.deleted", new { posterId, result.Removed, result.Cancelled, result.Errors });
            return result;
        }
        finally
        {
            FinishFileOperation(active);
        }
    }

    internal async Task<NewContentLocation?> PrepareNewContent(string posterId)
    {
        if (!TransfersAvailable || _chooseTransferStorage is null || HasCenterDraft(posterId))
            throw new IOException("请先完成当前文件操作或未保存的编辑。");
        if (ContentActionPath(posterId, null) is not null)
        {
            return await Task.Run(() => FileActions.PrepareNewContent(posterId, null, mapped: true));
        }
        var storage = await _chooseTransferStorage();
        if (storage is null) return null;
        if (!TransfersAvailable || HasCenterDraft(posterId)) throw new IOException("当前状态已变化，请重新打开新建菜单。");
        return await Task.Run(() => FileActions.PrepareNewContent(posterId, storage, mapped: false));
    }

    internal async Task<string> CreateContent(string posterId, NewContentLocation location, NewContentKind kind,
        string name, string? target, CancellationToken cancellation)
    {
        if (!TransfersAvailable || HasCenterDraft(posterId)) throw new IOException("请等待当前文件操作完成。");
        using var active = BeginFileOperation(posterId);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _contentOperations.Stopping);
        try
        {
            string path = await Task.Run(() => FileActions.Create(posterId, location, kind, name, target, stopping.Token));
            log.Write("info", "content.created", new { posterId, kind, path });
            return path;
        }
        finally { FinishFileOperation(active); }
    }
    internal void NotifyFileActionState(string? id)
    { NotifyContentEditorChanged(id); ActivityChanged?.Invoke(); }

}
