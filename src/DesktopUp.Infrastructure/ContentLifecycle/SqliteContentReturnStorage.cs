using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Owns identity/ownership guards and the atomic journal + relation updates.
public sealed partial class SqliteContentReturnStorage(SqliteStateStore store) : IContentReturnStorage
{
    private const string UninstallScope = ContentReturnIntent.UninstallScope;
    private DesktopStateSnapshot State => new SqliteStateCommandRepository(store).Read();
    public DesktopStateSnapshot ReadState() => State;
    public IDisposable AcquireExecution() => new SqliteTransferJournal(store).AcquireExecutor();
    public void BackfillOrigins() => ContentReturnStore.Backfill(store);
    public string? OriginalPath(Guid itemId) => ContentReturnStore.Original(store, itemId);
    public bool IsStillMissing(ShellExitItem source) => new NativeShellExitJournal(store).IsStillMissing(source);

    public ShellExitItem PreparePosterSource(DesktopStateSnapshot state, PosterItem card, CancellationToken token)
    {
        var item = state.ContentCatalog.Items.Single(i => i.Id == state.ContentCatalog.Memberships.Single(m => m.Id == card.MembershipId).ContentItemId);
        var registration = new SqliteTransferJournal(store).StorageLocations().SingleOrDefault(s =>
            state.ContentCatalog.Memberships.Any(m => m.CollectionId == s.Collection.Id && m.ContentItemId == item.Id))
            ?? throw new IOException("收纳目录归属不可用，文件保留：" + item.Target.Locator);
        ManagedStorageProvisioner.ValidateRegistration(store, registration.Root, registration.Collection);
        if (!string.Equals(Path.GetDirectoryName(item.Target.Locator), registration.Collection.StorageLocator, StringComparison.OrdinalIgnoreCase))
            throw new IOException("收纳来源不在登记目录内，未移动：" + item.Target.Locator);
        var source = new ShellExitItem(card.Id, item.Id, item.Version, item.Target.Locator,
            LocalTransferFiles.Identity(Path.GetDirectoryName(item.Target.Locator)!), LocalTransferFiles.Identity(item.Target.Locator), false);
        if (store.ProtectedTransferPath(source.Path)) throw new IOException("来源路径受保护。");
        MappedDirectoryIngress.ValidateTree(store, source.Path, token);
        if (state.ContentCatalog.PosterItems.Any(p => p.Id != card.Id && LocalTransferFiles.Contains(source.Path,
            state.ContentCatalog.Items.Single(i => i.Id == state.ContentCatalog.Memberships.Single(m => m.Id == p.MembershipId).ContentItemId).Target.Locator)))
            throw new IOException("此文件夹内另有独立卡片，请先归还这些卡片。文件保留：" + source.Path);
        return source;
    }

    public bool SourceCurrent(ContentReturnIntent intent)
    {
        var source = intent.Source; var state = State;
        if (intent.PosterId == UninstallScope)
            return UninstallSourceCurrent(source, state) && LocalTransferFiles.Identity(Path.GetDirectoryName(source.Path)!) == source.ParentIdentity &&
                LocalTransferFiles.Identity(source.Path) == source.Identity;
        return state.ContentCatalog.PosterItems.Any(p => p.Id == source.CardId && p.PosterId == intent.PosterId &&
            state.ContentCatalog.Memberships.Any(m => m.Id == p.MembershipId && m.ContentItemId == source.ContentId)) &&
            state.ContentCatalog.Items.Any(i => i.Id == source.ContentId && i.Version == source.Version && i.Target.Locator == source.Path) &&
            LocalTransferFiles.Identity(Path.GetDirectoryName(source.Path)!) == source.ParentIdentity && LocalTransferFiles.Identity(source.Path) == source.Identity;
    }
    public DesktopStateSnapshot Commit(ContentReturnIntent intent, ContentReturnReceipt receipt)
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
            if (intent.PosterId == UninstallScope) return CommitUninstall(c, t, before, intent, receipt);
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
    public IReadOnlyList<PendingContentReturn> Pending()
    {
        return store.ExecuteRead((c, t) =>
        {
            using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT body_json,result_json FROM content_returns WHERE closed=0";
            using var r = q.ExecuteReader(); var list = new List<PendingContentReturn>();
            while (r.Read()) list.Add(new(JsonSerializer.Deserialize<ContentReturnIntent>(r.GetString(0))!, r.IsDBNull(1) ? null : JsonSerializer.Deserialize<ContentReturnReceipt>(r.GetString(1))));
            return list;
        });
    }

    public StateCommandResult RemovePoster(RemovePosterCommand command)
    {
        var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(store));
        coordinator.Refresh();
        return coordinator.Execute(command);
    }

    public void WriteIntent(ContentReturnIntent intent) => Query("INSERT INTO content_returns(operation_id,body_json) VALUES($id,$body)", intent.Id, JsonSerializer.Serialize(intent));
    public void RecordResult(Guid id, ContentReturnReceipt receipt) => Query("UPDATE content_returns SET result_json=$body WHERE operation_id=$id", id, JsonSerializer.Serialize(receipt));
    public void Close(Guid id) => Query("UPDATE content_returns SET closed=1 WHERE operation_id=$id", id, null);
    private void Query(string sql, Guid id, string? body) => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; q.Parameters.AddWithValue("$id", id.ToString());
        if (body is not null) q.Parameters.AddWithValue("$body", body); return q.ExecuteNonQuery();
    });
}
