using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Compatibility entry for existing fixtures; production composition injects the use case.
public sealed class ContentDeletionService(SqliteStateStore store, IShellFileDeletion? shell = null)
{
    public Task<ContentDeletionResult> Delete(string posterId, IReadOnlyList<string> itemIds,
        nint owner, CancellationToken token = default) =>
        new ContentDeletionUseCase(new SqliteContentDeletionStorage(store), new LocalContentActionFiles(),
            shell ?? new ShellDirectoryTransfer()).Delete(posterId, itemIds, owner, token);
}
