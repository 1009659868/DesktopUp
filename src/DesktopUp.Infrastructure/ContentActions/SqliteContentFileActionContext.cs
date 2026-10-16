using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class SqliteContentFileActionContext(SqliteStateStore store) : IContentFileActionContext
{
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    public MappedNewContent ResolveMappedLocation(string posterId) => new MappedDirectoryService(store).ResolveNewContent(posterId);
    public void ValidateMappedAction(string posterId, string? itemId) => new MappedDirectoryService(store).ValidateAction(posterId, itemId);
    public void RegisterShellCreated(string posterId, NewContentLocation location, string directoryIdentity, ShellCreatedItem created) =>
        new ShellCreatedContentService(store).Register(posterId, location, directoryIdentity, created);
}
