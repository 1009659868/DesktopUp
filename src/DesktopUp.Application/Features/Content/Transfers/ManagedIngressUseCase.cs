using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class ManagedIngressUseCase(IManagedIngressStorage storage, IContentActionFiles files)
{
    public async Task<ManagedIngressResult> Execute(string posterId, IReadOnlyList<string> paths,
        RegisteredManagedStorage target, IncomingContentPlacement? landing,
        IIncomingTransferPresentation presentation, CancellationToken token)
    {
        int completed = 0, failed = 0;
        string? lastError = null;
        var normalized = paths.Select(files.Normalize).ToArray();
        if (normalized.Any(p => normalized.Any(other => p != other && files.Contains(p, other))))
            throw new IOException("请不要同时选择文件夹及其内部文件；本次未移动。");
        foreach (string path in normalized)
        {
            if (!presentation.IsCurrent) { lastError = "海报或桌面环境已变化，剩余项未移动。"; break; }
            try
            {
                presentation.ReportProgress(path, completed);
                var snapshot = await Task.Run(storage.ReadState);
                var item = await Task.Run(() => storage.Prepare(snapshot, posterId, path, target, landing));
                if (!presentation.IsCurrent) { lastError = "落点已变化，剩余项未移动。"; break; }
                var finish = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    item.RecordIntent();
                    return item.Complete(token);
                });
                // A completed fact must still publish if cancellation arrived after commit.
                presentation.PublishCommittedChanges();
                if (finish == TransferFinishKind.Completed)
                {
                    completed++;
                    if (landing is not null)
                        landing = IncomingPlacementProgress.Advance(storage.ReadState(), posterId, item.ContentId, landing);
                }
                else
                {
                    failed++;
                    lastError = finish == TransferFinishKind.Preserved
                        ? $"文件已保留，请查看：{path} 与 {item.Destination}" : "该项未移动，可重新拖动。";
                }
            }
            catch (OperationCanceledException) { lastError = "已取消，未完成项保留。"; break; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            { failed++; lastError = $"{Path.GetFileName(path)}：{e.Message}"; }
        }
        return new(completed, failed, normalized.Length, lastError);
    }
}

internal static class IncomingPlacementProgress
{
    internal static IncomingContentPlacement Advance(DesktopStateSnapshot state, string posterId, Guid contentId,
        IncomingContentPlacement landing)
    {
        var member = state.ContentCatalog.Memberships.FirstOrDefault(m => m.ContentItemId == contentId &&
            state.ContentCatalog.Bindings.Any(b => b.PosterId == posterId && b.CollectionId == m.CollectionId));
        var card = state.ContentCatalog.PosterItems.FirstOrDefault(p => p.PosterId == posterId && p.MembershipId == member?.Id);
        var poster = state.Catalog.Posters.Single(x => x.Poster.PosterId == posterId).Poster;
        return card is not null && ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, posterId, out var document, out _)
            ? landing.Advance(document!, card.LayoutItemId) : landing;
    }
}
