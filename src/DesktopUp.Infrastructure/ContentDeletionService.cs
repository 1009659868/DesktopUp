using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed record ContentDeletionResult(int Removed, bool Cancelled, IReadOnlyList<string> Errors);

// Windows owns actual deletion and its necessary prompts. The app only observes outcomes;
// a failed/cancelled item stays on the poster. Neither restart nor observation retries delete.
public sealed class ContentDeletionService(SqliteStateStore store, IShellFileDeletion? shell = null)
{
    public async Task<ContentDeletionResult> Delete(string posterId, IReadOnlyList<string> itemIds,
        nint owner, CancellationToken token = default)
    {
        var journal = new NativeShellExitJournal(store);
        ShellExitIntent? intent = null; var errors = new List<string>(); bool cancelled = false; int removed = 0;
        try
        {
            using (new SqliteTransferJournal(store).AcquireExecutor())
            {
                token.ThrowIfCancellationRequested();
                var before = new SqliteStateCommandRepository(store).Read();
                var poster = before.Catalog.Posters.Single(p => p.Poster.PosterId == posterId);
                if (poster.Poster.IsLocked) throw new IOException("海报已锁定，未删除。");
                intent = journal.Begin(posterId, itemIds, explicitDelete: true);
                if (intent.Items.Any(i => intent.Items.Any(other => i != other && LocalTransferFiles.Contains(i.Path, other.Path))))
                    throw new IOException("请不要同时选择父目录及其中的文件。");
                journal.AuthorizeDelete(intent);
                foreach (var item in intent.Items)
                {
                    token.ThrowIfCancellationRequested();
                    bool Current()
                    {
                        if (token.IsCancellationRequested || LocalTransferFiles.Identity(Path.GetDirectoryName(item.Path)!) != item.ParentIdentity ||
                            LocalTransferFiles.Identity(item.Path) != item.Identity) return false;
                        var now = new SqliteStateCommandRepository(store).Read();
                        return now.Catalog.Posters.Any(p => p.Poster.PosterId == posterId && !p.Poster.IsLocked && p.EntityVersion == poster.EntityVersion) &&
                            now.ContentCatalog.PosterItems.Any(p => p.Id == item.CardId && p.PosterId == posterId) &&
                            now.ContentCatalog.Items.Any(i => i.Id == item.ContentId && i.Version == item.Version && i.Target.Locator == item.Path);
                    }
                    if (!Current()) { errors.Add("所选文件或海报已变化，剩余项未删除。"); break; }
                    // Pin the original parent against directory substitution while Shell owns the leaf.
                    using var parent = LocalTransferFiles.LockDirectory(Path.GetDirectoryName(item.Path)!);
                    if (!Current()) throw new IOException("来源目录已变化，未删除。");
                    var result = await (shell ?? new ShellDirectoryTransfer()).Recycle(item.Path, owner, Current, token).ConfigureAwait(false);
                    if (result.Cancelled) { cancelled = true; break; }
                    if (result.HResult < 0) errors.Add(Path.GetFileName(item.Path) + "：Windows未完成删除。");
                }
            }
        }
        catch (OperationCanceledException) { cancelled = true; }
        finally
        {
            if (intent is not null)
            {
                try { removed = journal.ObserveReleasedMove(intent); journal.Close(intent); }
                catch (Exception e) { errors.Add("文件结果保留，卡片稍后核对：" + e.Message); }
            }
        }
        return new(removed, cancelled, errors);
    }
}
