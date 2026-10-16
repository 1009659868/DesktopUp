namespace DesktopUp.Application;

// Windows owns deletion/necessary prompts. Only observed absence removes a card.
// Cancellation or an uncertain result is never retried here.
public sealed class ContentDeletionUseCase(IContentDeletionStorage storage, IContentActionFiles files, IShellFileDeletion shell)
{
    public async Task<ContentDeletionResult> Delete(string posterId, IReadOnlyList<string> itemIds,
        nint owner, CancellationToken token = default)
    {
        var attempt = storage.BeginAttempt();
        var errors = new List<string>(); bool cancelled = false; int removed = 0;
        try
        {
            using (attempt.AcquireExecutor())
            {
                token.ThrowIfCancellationRequested();
                var paths = attempt.Prepare(posterId, itemIds);
                if (paths.Any(path => paths.Any(other => path != other && files.Contains(path, other))))
                    throw new IOException("请不要同时选择父目录及其中的文件。");
                attempt.AuthorizeDelete();
                foreach (var path in paths)
                {
                    token.ThrowIfCancellationRequested();
                    bool Current() => !token.IsCancellationRequested && attempt.IsCurrent(path);
                    if (!Current()) { errors.Add("所选文件或海报已变化，剩余项未删除。"); break; }
                    using var parent = files.LockDirectory(Path.GetDirectoryName(path)!);
                    if (!Current()) throw new IOException("来源目录已变化，未删除。");
                    var result = await shell.Recycle(path, owner, Current, token).ConfigureAwait(false);
                    if (result.Cancelled) { cancelled = true; break; }
                    if (result.HResult < 0) errors.Add(Path.GetFileName(path) + "：Windows未完成删除。");
                }
            }
        }
        catch (OperationCanceledException) { cancelled = true; }
        finally
        {
            try { removed = attempt.ObserveAndClose(); }
            catch (Exception e) { errors.Add("文件结果保留，卡片稍后核对：" + e.Message); }
        }
        return new(removed, cancelled, errors);
    }
}
