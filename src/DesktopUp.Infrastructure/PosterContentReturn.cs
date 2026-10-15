using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed record PosterContentReturnResult(bool Removed, int Returned, IReadOnlyList<string> Destinations, IReadOnlyList<string> Errors);
internal sealed record ContentReturnIntent(Guid Id, string PosterId, ShellExitItem Source, string Directory, string DirectoryIdentity);
internal sealed record ContentReturnReceipt(string Path, string Identity);

// Shared with a future uninstall orchestrator. Never recursively deletes storage and never
// replays a filesystem operation on restart. One explicit poster-deletion consent owns the batch.
public sealed class PosterContentReturn(SqliteStateStore store, IShellDirectoryTransfer? shell = null, string? desktopDirectory = null)
{
    internal Action<string>? Checkpoint { get; set; }
    private DesktopStateSnapshot State => new SqliteStateCommandRepository(store).Read();
    public string? OriginalPath(Guid itemId) => ContentReturnStore.Original(store, itemId);

    public async Task<PosterContentReturnResult> Execute(RemovePosterCommand command, nint owner, Func<bool> current, CancellationToken token = default)
    {
        using var lease = new SqliteTransferJournal(store).AcquireExecutor();
        var initial = State;
        if (!command.Confirmed || command.Source is not (StateCommandSource.Poster or StateCommandSource.ControlCenter) ||
            command.ReadSet.CatalogVersion != initial.Catalog.CatalogVersion ||
            command.ReadSet.Posters.GetValueOrDefault(command.PosterId) != initial.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == command.PosterId)?.EntityVersion ||
            command.ReadSet.ContentVersion is { } content && content != initial.ContentVersion)
            return new(false, 0, [], ["海报或内容已变化，请重新删除；文件未移动。"]);
        ContentReturnStore.Backfill(store);
        var cards = ContentReturnPolicy.ManagedCards(initial.ContentCatalog, command.PosterId);
        var destinations = new List<string>(); var errors = new List<string>(); var expected = initial;
        bool Current()
        {
            if (token.IsCancellationRequested || !current()) return false;
            var now = State;
            return now.ContentVersion == expected.ContentVersion && now.Catalog.CatalogVersion == expected.Catalog.CatalogVersion &&
                now.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == command.PosterId)?.EntityVersion ==
                    expected.Catalog.Posters.Single(p => p.Poster.PosterId == command.PosterId).EntityVersion;
        }
        foreach (var card in cards)
        {
            ContentReturnIntent? intent = null;
            bool recorded = false;
            try
            {
                if (!Current()) throw new IOException("归还已停止或内容已变化；剩余文件仍在海报中。");
                var item = initial.ContentCatalog.Items.Single(i => i.Id == initial.ContentCatalog.Memberships.Single(m => m.Id == card.MembershipId).ContentItemId);
                var registration = new SqliteTransferJournal(store).StorageLocations().SingleOrDefault(s =>
                    initial.ContentCatalog.Memberships.Any(m => m.CollectionId == s.Collection.Id && m.ContentItemId == item.Id))
                    ?? throw new IOException("收纳目录归属不可用，文件保留：" + item.Target.Locator);
                ManagedStorageProvisioner.ValidateRegistration(store, registration.Root, registration.Collection);
                if (!string.Equals(Path.GetDirectoryName(item.Target.Locator), registration.Collection.StorageLocator, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("收纳来源不在登记目录内，未移动：" + item.Target.Locator);
                var source = new ShellExitItem(card.Id, item.Id, item.Version, item.Target.Locator,
                    LocalTransferFiles.Identity(Path.GetDirectoryName(item.Target.Locator)!), LocalTransferFiles.Identity(item.Target.Locator), false);
                if (store.ProtectedTransferPath(source.Path)) throw new IOException("来源路径受保护。");
                MappedDirectoryIngress.ValidateTree(store, source.Path, token);
                if (initial.ContentCatalog.PosterItems.Any(p => p.Id != card.Id && LocalTransferFiles.Contains(source.Path,
                    initial.ContentCatalog.Items.Single(i => i.Id == initial.ContentCatalog.Memberships.Single(m => m.Id == p.MembershipId).ContentItemId).Target.Locator)))
                    throw new IOException("此文件夹内另有独立卡片，请先归还这些卡片。文件保留：" + source.Path);
                string desktop = desktopDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string? original = OriginalPath(item.Id);
                string directory = Usable(original is null ? null : Path.GetDirectoryName(original), initial.ContentCatalog) ??
                    Usable(desktop, initial.ContentCatalog) ?? throw new IOException("原目录和系统桌面均不可用；文件保留：" + source.Path);
                intent = new(Guid.NewGuid(), command.PosterId, source, directory, LocalTransferFiles.Identity(directory));
                Write(intent); Checkpoint?.Invoke("Prepared");
                bool Valid() => Current() && SourceCurrent(intent) && LocalTransferFiles.Identity(directory) == intent.DirectoryIdentity;
                if (!Valid()) throw new IOException("来源或目标已变化，未执行归还。");
                var result = await (shell ?? new ShellDirectoryTransfer()).Run(source.Path, directory, false, owner, Valid, token).ConfigureAwait(false);
                // Only definite access/path errors with an unchanged source may fall back.
                if (result.Destination is null && !result.Cancelled && result.HResult is unchecked((int)0x80070005) or unchecked((int)0x80070003) or unchecked((int)0x80070002) &&
                    !string.Equals(directory, desktop, StringComparison.OrdinalIgnoreCase) && Current() && SourceCurrent(intent))
                {
                    Close(intent.Id);
                    directory = Usable(desktop, initial.ContentCatalog) ?? throw new IOException("系统桌面不可用，文件保留。");
                    intent = intent with { Id = Guid.NewGuid(), Directory = directory, DirectoryIdentity = LocalTransferFiles.Identity(directory) };
                    Write(intent);
                    result = await (shell ?? new ShellDirectoryTransfer()).Run(source.Path, directory, false, owner, Valid, token).ConfigureAwait(false);
                }
                if (result.Destination is null)
                    throw new IOException(result.Cancelled ? "已取消归还；剩余内容和海报保留。" : $"归还未完成；请核对 {source.Path} 与 {directory}。");
                var receipt = new ContentReturnReceipt(result.Destination, LocalTransferFiles.Identity(result.Destination));
                Record(intent.Id, receipt); recorded = true; Checkpoint?.Invoke("ResultRecorded");
                expected = Commit(intent, receipt); destinations.Add(receipt.Path); Close(intent.Id);
                if (result.Cancelled || token.IsCancellationRequested) { errors.Add("已停止；已完成项保留在返回位置，剩余文件仍在海报。"); break; }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                errors.Add(e.Message);
                if (intent is not null && !recorded) Close(intent.Id);
                break; // No blind retries, no advancing over a changed target or partial cancellation.
            }
        }
        if (errors.Count == 0)
        {
            if (!Current()) errors.Add("海报已变化或操作停止，未删除海报；已归还文件保持原位。");
            else
            {
                var remove = command with { RequestId = command.RequestId + "-returned", ExpectedRevision = expected.Revision,
                    ReadSet = StateReadSet.ForPoster(command.PosterId, expected.Catalog.Posters.Single(p => p.Poster.PosterId == command.PosterId).EntityVersion)
                        with { CatalogVersion = expected.Catalog.CatalogVersion, ContentVersion = expected.ContentVersion } };
                var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(store));
                coordinator.Refresh();
                var result = coordinator.Execute(remove);
                if (result.Status is StateCommandStatus.Committed or StateCommandStatus.Replayed) return new(true, destinations.Count, destinations, []);
                errors.Add(result.Error ?? "文件已归还，但海报配置尚未移除，请重试删除海报。");
            }
        }
        return new(false, destinations.Count, destinations, errors);
    }

    private string? Usable(string? path, ContentCatalogSnapshot catalog)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = LocalTransferFiles.Normalize(path);
            if (store.ProtectedTransferPath(path, true) || catalog.Collections.Any(c => c.Kind == ContentCollectionKind.Managed && Path.GetDirectoryName(c.StorageLocator) is { } root &&
                (string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(root, path)))) return null;
            using var lease = LocalTransferFiles.LockDirectory(path);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    private bool SourceCurrent(ContentReturnIntent intent)
    {
        var source = intent.Source; var state = State;
        return state.ContentCatalog.PosterItems.Any(p => p.Id == source.CardId && p.PosterId == intent.PosterId &&
            state.ContentCatalog.Memberships.Any(m => m.Id == p.MembershipId && m.ContentItemId == source.ContentId)) &&
            state.ContentCatalog.Items.Any(i => i.Id == source.ContentId && i.Version == source.Version && i.Target.Locator == source.Path) &&
            LocalTransferFiles.Identity(Path.GetDirectoryName(source.Path)!) == source.ParentIdentity && LocalTransferFiles.Identity(source.Path) == source.Identity;
    }
    private DesktopStateSnapshot Commit(ContentReturnIntent intent, ContentReturnReceipt receipt)
    {
        var source = intent.Source;
        using var target = LocalTransferFiles.LockDirectory(intent.Directory);
        if (LocalTransferFiles.Identity(intent.Directory) != intent.DirectoryIdentity ||
            !string.Equals(Path.GetDirectoryName(receipt.Path), intent.Directory, StringComparison.OrdinalIgnoreCase) ||
            LocalTransferFiles.Identity(receipt.Path) != receipt.Identity || !new NativeShellExitJournal(store).IsStillMissing(source))
            throw new IOException("归还结果不能确认；文件保留，请核对：" + receipt.Path);
        return store.ExecuteWrite((c, t) =>
        {
            var before = SqliteStateCommandRepository.ReadSnapshot(c, t);
            if (before.ContentCatalog.PosterItems.All(p => p.Id != source.CardId)) return before;
            if (!before.ContentCatalog.Items.Any(i => i.Id == source.ContentId && i.Version == source.Version && i.Target.Locator == source.Path))
                throw new IOException("原卡片来源已变化，未覆盖配置；归还位置：" + receipt.Path);
            var removal = ContentRelationRemoval.Prepare(before, [source.CardId]);
            if (removal.Rejection is not null) throw new IOException(removal.Error);
            var owned = before.ContentCatalog.Collections.Where(x => x.Kind == ContentCollectionKind.Managed).Select(x => x.Id).ToHashSet();
            var removed = before.ContentCatalog.Memberships.Where(m => m.ContentItemId == source.ContentId && owned.Contains(m.CollectionId)).Select(m => m.Id).ToArray();
            var plan = removal with { ExternalSyncReceiptId = intent.Id, RemovedStorageMemberships = removed,
                ContentCatalog = removal.ContentCatalog! with { Memberships = removal.ContentCatalog!.Memberships.Where(m => !removed.Contains(m.Id)).ToArray() } };
            var reads = StateReadSet.ForPoster(intent.PosterId, before.Catalog.Posters.Single(p => p.Poster.PosterId == intent.PosterId).EntityVersion) with { ContentVersion = before.ContentVersion };
            var result = SqliteStateCommandRepository.ExecuteInTransaction(c, t,
                new("content-return-" + intent.Id, StateRequestFingerprint.Compute(receipt), before.Revision, reads), _ => plan, authorizedExternalSyncId: intent.Id);
            if (result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed)) throw new IOException(result.Error);
            return result.Snapshot;
        });
    }
    public IReadOnlyList<string> Reconcile()
    {
        using var lease = new SqliteTransferJournal(store).AcquireExecutor();
        ContentReturnStore.Backfill(store);
        var rows = store.ExecuteRead((c, t) =>
        {
            using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT body_json,result_json FROM content_returns WHERE closed=0";
            using var r = q.ExecuteReader(); var list = new List<(ContentReturnIntent, ContentReturnReceipt?)>();
            while (r.Read()) list.Add((JsonSerializer.Deserialize<ContentReturnIntent>(r.GetString(0))!, r.IsDBNull(1) ? null : JsonSerializer.Deserialize<ContentReturnReceipt>(r.GetString(1))));
            return list;
        });
        var notices = new List<string>();
        foreach (var (intent, receipt) in rows)
        {
            try
            {
                if (receipt is not null) Commit(intent, receipt);
                else if (new NativeShellExitJournal(store).IsStillMissing(intent.Source))
                    notices.Add($"上次归还未记录完整结果，请核对 {intent.Source.Path} 与 {intent.Directory}；未重试搬移或删除文件。");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { notices.Add(e.Message); }
            Close(intent.Id);
        }
        return notices;
    }
    private void Write(ContentReturnIntent intent) => Query("INSERT INTO content_returns(operation_id,body_json) VALUES($id,$body)", intent.Id, JsonSerializer.Serialize(intent));
    private void Record(Guid id, ContentReturnReceipt receipt) => Query("UPDATE content_returns SET result_json=$body WHERE operation_id=$id", id, JsonSerializer.Serialize(receipt));
    private void Close(Guid id) => Query("UPDATE content_returns SET closed=1 WHERE operation_id=$id", id, null);
    private void Query(string sql, Guid id, string? body) => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; q.Parameters.AddWithValue("$id", id.ToString());
        if (body is not null) q.Parameters.AddWithValue("$body", body); return q.ExecuteNonQuery();
    });
}
