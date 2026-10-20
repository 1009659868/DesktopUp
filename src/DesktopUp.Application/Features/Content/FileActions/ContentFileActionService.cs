namespace DesktopUp.Application;

public sealed record ContentShellOutcome(ShellUiResult Result, bool Registered, string? PublicationError = null);

// Application-owned action boundary. The host owns scheduling, UI leases and projection
// notification only; persistence/native adapters are injected at composition.
public sealed class ContentFileActionService(IContentFileActionContext context, IContentActionFiles files,
    ContentCreationUseCase creation, ContentRenameUseCase rename, ContentDeletionUseCase deletion, IShellUiClient shell) : IContentFileActionService
{
    public string CaptureIdentity(string path) => files.Identity(path);
    public string SuggestName(string directory, NewContentKind kind) => creation.SuggestName(directory, kind);
    public void ValidateMappedAction(string posterId, string? itemId) => context.ValidateMappedAction(posterId, itemId);
    public NewContentLocation PrepareNewContent(string posterId, RegisteredManagedStorage? storage, bool mapped)
    {
        var mapping = mapped ? context.ResolveMappedLocation(posterId) : null;
        var state = context.ReadState();
        var owner = state.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == posterId)
            ?? throw new IOException("海报已关闭。");
        return new(storage, state.ContentVersion, owner.EntityVersion) { Mapping = mapping };
    }
    public string Create(string posterId, NewContentLocation location, NewContentKind kind, string name,
        string? target, CancellationToken token) => creation.Create(posterId, location, kind, name, target, token);
    public void Rename(string posterId, string itemId, Guid contentId, long version, string name,
        string identity, CancellationToken token) => rename.Rename(posterId, itemId, contentId, version, name, token, identity);
    public Task<ContentDeletionResult> Delete(string posterId, IReadOnlyList<string> items, nint owner,
        CancellationToken token) => deletion.Delete(posterId, items, owner, token);

    public async Task<ContentShellOutcome> InvokeShell(string posterId, string? itemId,
        ContentRelationResolution? expected, NewContentLocation? location, string path,
        ShellUiAction action, int x, int y, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        context.ValidateMappedAction(posterId, itemId);
        if (expected is not null && ContentRelationPolicy.Resolve(context.ReadState().ContentCatalog,
                posterId, itemId!)?.Witness != expected.Witness)
            throw new IOException("所选对象已变化，请重新打开菜单。");
        string identity = files.Identity(path);
        var result = await shell.RunAsync(new(Guid.NewGuid(), action, path, x, y) { Identity = identity }, token).ConfigureAwait(false);
        if (location is null || result.CreatedItem is not { } created) return new(result, false);
        // Publication is required even if the invoking view closed after Windows created
        // the object. It never retries the native operation or removes the real object.
        try
        {
            context.RegisterShellCreated(posterId, location, identity, created);
            return new(result, true);
        }
        catch (Exception e)
        {
            return new(result, false, "文件已由 Windows 创建并保留，未删除或重试。\n" + created.Path + "\n" + e.Message);
        }
    }
}
