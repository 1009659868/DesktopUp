using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class SqliteContentFileActionContext(SqliteStateStore store) : IContentFileActionContext
{
    private readonly MappedDirectoryUseCase _mapping = new(
        new SqliteMappedDirectoryStorage(store), new LocalContentObservationFiles());
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    public MappedNewContent ResolveMappedLocation(string posterId) => _mapping.ResolveNewContent(posterId);
    public void ValidateMappedAction(string posterId, string? itemId) => _mapping.ValidateAction(posterId, itemId);
    public void RegisterShellCreated(string posterId, NewContentLocation location, string directoryIdentity, ShellCreatedItem created) =>
        new ShellCreatedContentService(store).Register(posterId, location, directoryIdentity, created);
}
