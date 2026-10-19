namespace DesktopUp.Application;

// Owns deletion consent, sequential completion and the shared single-file return protocol.
// It never guesses a completed move, retries on cancellation, or rolls a successful item back.
public sealed class ContentReturnUseCase(IContentReturnStorage storage, IContentReturnFiles files,
    IShellDirectoryTransfer shell, Action<string>? checkpoint = null)
{
    public string? OriginalPath(Guid itemId) => storage.OriginalPath(itemId);

    public async Task<PosterContentReturnResult> Execute(RemovePosterCommand command, nint owner,
        Func<bool> current, CancellationToken token = default)
    {
        using var lease = storage.AcquireExecution();
        var initial = storage.ReadState();
        if (!ConsentMatches(command, initial))
            return new(false, 0, [], ["海报或内容已变化，请重新删除；文件未移动。"]);

        storage.BackfillOrigins();
        var cards = ContentReturnPolicy.ManagedCards(initial.ContentCatalog, command.PosterId);
        var destinations = new List<string>();
        var errors = new List<string>();
        var expected = initial;

        bool Current()
        {
            if (token.IsCancellationRequested || !current()) return false;
            var now = storage.ReadState();
            return now.ContentVersion == expected.ContentVersion &&
                now.Catalog.CatalogVersion == expected.Catalog.CatalogVersion &&
                now.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == command.PosterId)?.EntityVersion ==
                expected.Catalog.Posters.Single(p => p.Poster.PosterId == command.PosterId).EntityVersion;
        }

        foreach (var card in cards)
        {
            try
            {
                if (!Current()) throw new IOException("归还已停止或内容已变化；剩余文件仍在海报中。");
                var source = storage.PreparePosterSource(initial, card, token);
                var returned = await ReturnOne(command.PosterId, source, initial.ContentCatalog,
                    Current, owner, token).ConfigureAwait(false);
                expected = returned.State;
                destinations.Add(returned.Path);
                if (returned.Stopped || token.IsCancellationRequested)
                {
                    errors.Add("已停止；已完成项保留在返回位置，剩余文件仍在海报。");
                    break;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                errors.Add(e.Message);
                break;
            }
        }

        if (errors.Count == 0)
        {
            if (!Current())
                errors.Add("海报已变化或操作停止，未删除海报；已归还文件保持原位。");
            else
            {
                var remove = command with
                {
                    RequestId = command.RequestId + "-returned",
                    ExpectedRevision = expected.Revision,
                    ReadSet = StateReadSet.ForPoster(command.PosterId,
                        expected.Catalog.Posters.Single(p => p.Poster.PosterId == command.PosterId).EntityVersion)
                        with { CatalogVersion = expected.Catalog.CatalogVersion, ContentVersion = expected.ContentVersion }
                };
                var result = storage.RemovePoster(remove);
                if (result.Status is StateCommandStatus.Committed or StateCommandStatus.Replayed)
                    return new(true, destinations.Count, destinations, []);
                errors.Add(result.Error ?? "文件已归还，但海报配置尚未移除，请重试删除海报。");
            }
        }
        return new(false, destinations.Count, destinations, errors);
    }

    private static bool ConsentMatches(RemovePosterCommand command, DesktopStateSnapshot state) =>
        command.Confirmed &&
        command.Source is StateCommandSource.Poster or StateCommandSource.ControlCenter &&
        command.ReadSet.CatalogVersion == state.Catalog.CatalogVersion &&
        command.ReadSet.Posters.GetValueOrDefault(command.PosterId) ==
            state.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == command.PosterId)?.EntityVersion &&
        (command.ReadSet.ContentVersion is not { } content || content == state.ContentVersion);

    internal async Task<(DesktopStateSnapshot State, string Path, bool Stopped)> ReturnOne(
        string posterId, ShellExitItem source, DesktopUp.Domain.ContentCatalogSnapshot catalog,
        Func<bool> current, nint owner, CancellationToken token)
    {
        ContentReturnIntent? intent = null;
        bool recorded = false;
        try
        {
            string desktop = files.DesktopDirectory;
            string? original = OriginalPath(source.ContentId);
            string directory = files.UsableDestination(original is null ? null : Path.GetDirectoryName(original), catalog) ??
                files.UsableDestination(desktop, catalog) ??
                throw new IOException("原目录和系统桌面均不可用；文件保留：" + source.Path);
            intent = new(Guid.NewGuid(), posterId, source, directory, files.Identity(directory));
            storage.WriteIntent(intent);
            checkpoint?.Invoke("Prepared");

            bool Valid() => !token.IsCancellationRequested && current() &&
                storage.SourceCurrent(intent) && files.Identity(directory) == intent.DirectoryIdentity;

            if (!Valid()) throw new IOException("来源或目标已变化，未执行归还。");
            var result = await shell.Run(source.Path, directory, false, owner, Valid, token).ConfigureAwait(false);
            if (CanFallBack(result) && !string.Equals(directory, desktop, StringComparison.OrdinalIgnoreCase) && Valid())
            {
                storage.Close(intent.Id);
                directory = files.UsableDestination(desktop, catalog) ?? throw new IOException("系统桌面不可用，文件保留。");
                intent = intent with { Id = Guid.NewGuid(), Directory = directory, DirectoryIdentity = files.Identity(directory) };
                storage.WriteIntent(intent);
                result = await shell.Run(source.Path, directory, false, owner, Valid, token).ConfigureAwait(false);
            }
            if (result.Destination is null)
                throw new IOException(result.Cancelled ? "已取消归还；剩余内容保留。" : $"归还未完成；请核对 {source.Path} 与 {directory}。");

            // Record late success even when Shell also reports cancellation. Never move it again.
            var receipt = new ContentReturnReceipt(result.Destination, files.Identity(result.Destination));
            storage.RecordResult(intent.Id, receipt);
            recorded = true;
            checkpoint?.Invoke("ResultRecorded");
            var state = storage.Commit(intent, receipt);
            storage.Close(intent.Id);
            return (state, receipt.Path, result.Cancelled);
        }
        catch
        {
            if (intent is not null && !recorded) storage.Close(intent.Id);
            throw;
        }
    }

    private static bool CanFallBack(ShellDirectoryResult result) =>
        result.Destination is null && !result.Cancelled &&
        result.HResult is unchecked((int)0x80070005) or unchecked((int)0x80070003) or unchecked((int)0x80070002);

    public IReadOnlyList<string> Reconcile()
    {
        using var lease = storage.AcquireExecution();
        storage.BackfillOrigins();
        var notices = new List<string>();
        foreach (var (intent, receipt) in storage.Pending())
        {
            try
            {
                if (receipt is not null) storage.Commit(intent, receipt);
                else if (storage.IsStillMissing(intent.Source))
                    notices.Add($"上次归还未记录完整结果，请核对 {intent.Source.Path} 与 {intent.Directory}；未重试搬移或删除文件。");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                notices.Add(e.Message);
            }
            storage.Close(intent.Id);
        }
        return notices;
    }
}
