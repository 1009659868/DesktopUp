using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record ShellExitItem(Guid CardId, Guid ContentId, long Version, string Path,
    string ParentIdentity, string? Identity, bool Missing);
public sealed record ShellExitIntent(Guid Id, string PosterId, DateTimeOffset Created, IReadOnlyList<ShellExitItem> Items);

// Durable observation only. Shell owns execution; this class never moves, copies or deletes
// a file and never interprets DROPEFFECT as evidence of completed physical movement.
public sealed class NativeShellExitJournal(SqliteStateStore store)
{
    internal const string MigrationSql = """
        CREATE TABLE native_shell_exits(operation_id TEXT PRIMARY KEY, body_json TEXT NOT NULL,
            released INTEGER, closed INTEGER NOT NULL DEFAULT 0 CHECK(closed IN(0,1))) STRICT;
        """;
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT operation_id,body_json,released,closed FROM native_shell_exits LIMIT 0"; using var r = q.ExecuteReader(); }

    public ShellExitIntent Begin(string posterId, IReadOnlyList<string> layoutIds)
    {
        var state = new SqliteStateCommandRepository(store).Read();
        if(ContentSingleOwnerPolicy.Validate(state.ContentCatalog) is { } ownershipError)throw new IOException(ownershipError);
        var sources = DirectTransferPreparation.OutgoingSources(state, posterId, layoutIds);
        var items = sources.Select(s =>
        {
            var item = state.ContentCatalog.Items.Single(i => i.Id == s.ContentId);
            if (item.Source.ProviderId != "local-file-reference-v1" || item.Kind is not (ContentKind.File or ContentKind.Folder or ContentKind.Shortcut))
                throw new IOException("当前对象不提供本地文件原生拖放。");
            if (store.ProtectedTransferPath(s.Locator)) throw new IOException("受保护路径不能用于拖放。");
            string parent = Path.GetDirectoryName(s.Locator) ?? throw new IOException("不能拖动磁盘根目录。");
            string parentId = LocalTransferFiles.Identity(parent);
            // A complete, accessible directory observation is required. File.Exists(false)
            // alone does not distinguish missing files from denied/offline roots.
            var entries = Entries(parent);
            var found = entries.FirstOrDefault(p => string.Equals(p, s.Locator, StringComparison.OrdinalIgnoreCase));
            return new ShellExitItem(s.CardId, s.ContentId, s.ContentVersion, s.Locator,
                parentId, found is null ? null : LocalTransferFiles.Identity(found), found is null);
        }).ToArray();
        var intent = new ShellExitIntent(Guid.NewGuid(), posterId, DateTimeOffset.UtcNow, items);
        store.ExecuteWrite((c,t) =>
        {
            if (SqliteContentRepository.ReadVersion(c,t) != state.ContentVersion) throw new IOException("内容已变化，请重新拖动。");
            using var q = Command(c,t,"INSERT INTO native_shell_exits(operation_id,body_json) VALUES($id,$body)", intent.Id);
            q.Parameters.AddWithValue("$body", JsonSerializer.Serialize(intent)); q.ExecuteNonQuery(); return 0;
        });
        return intent;
    }
    public void End(ShellExitIntent intent, bool released)
        => store.ExecuteWrite((c,t) =>
        {
            using var q = Command(c,t,"UPDATE native_shell_exits SET released=$r,closed=$closed WHERE operation_id=$id",intent.Id);
            q.Parameters.AddWithValue("$r",released ? 1 : 0); q.Parameters.AddWithValue("$closed",released ? 0 : 1); return q.ExecuteNonQuery();
        });
    public bool IsStillMissing(ShellExitItem item)
    {
        string parent = Path.GetDirectoryName(item.Path)!;
        if (LocalTransferFiles.Identity(parent) != item.ParentIdentity) return false;
        var entries = Entries(parent);
        if (entries.Any(p => string.Equals(p,item.Path,StringComparison.OrdinalIgnoreCase))) return false;
        // A rename inside the same folder is not an exit. Leave it to external sync.
        return item.Identity is null || entries.All(p => LocalTransferFiles.Identity(p) != item.Identity);
    }
    public int ObserveReleasedMove(ShellExitIntent intent) => RemoveProven(intent, staleOnly:false);
    // The only caller is the post-release confirmation dialog's affirmative branch.
    public int RemoveStaleReferencesAfterUserConfirmation(ShellExitIntent intent) => RemoveProven(intent, staleOnly:true);
    private int RemoveProven(ShellExitIntent intent, bool staleOnly)
    {
        if (!HasReleasedReceipt(intent)) return 0;
        var proven = new List<ShellExitItem>();
        foreach (var item in intent.Items.Where(i => i.Missing == staleOnly))
            try { if (IsStillMissing(item)) proven.Add(item); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (proven.Count == 0) return 0;
        using var lease = new SqliteTransferJournal(store).AcquireObservationExecutor();
        return store.ExecuteWrite((c,t) =>
        {
            var state = SqliteStateCommandRepository.ReadSnapshot(c,t);
            var cards = proven.Where(p => state.ContentCatalog.PosterItems.Any(r => r.Id == p.CardId && r.PosterId == intent.PosterId &&
                    state.ContentCatalog.Memberships.Any(m => m.Id == r.MembershipId && m.ContentItemId == p.ContentId)) &&
                state.ContentCatalog.Items.Any(i => i.Id == p.ContentId && i.Version == p.Version && i.Target.Locator == p.Path) && IsStillMissing(p))
                .Select(p => p.CardId).ToArray();
            if (cards.Length == 0) return 0;
            var plan = ContentRelationRemoval.Prepare(state,cards);
            if (plan.Rejection is not null) return 0;
            var memberIds = state.ContentCatalog.PosterItems.Where(p => cards.Contains(p.Id)).Select(p => p.MembershipId).ToHashSet();
            var removedMembers = state.ContentCatalog.Memberships.Where(m => memberIds.Contains(m.Id) &&
                m.Origin == ContentMembershipOrigin.Source && state.ContentCatalog.Collections.Any(col => col.Id == m.CollectionId && col.Kind == ContentCollectionKind.Managed) &&
                plan.ContentCatalog!.PosterItems.All(p => p.MembershipId != m.Id)).Select(m => m.Id).ToArray();
            var operation = Guid.NewGuid();
            plan = plan with { RemovedStorageMemberships = removedMembers, ExternalSyncReceiptId = operation,
                ContentCatalog = plan.ContentCatalog! with { Memberships = plan.ContentCatalog.Memberships.Where(m => !removedMembers.Contains(m.Id)).ToArray() } };
            var reads = new StateReadSet(plan.Updates.ToDictionary(p => p.PosterId,
                p => state.Catalog.Posters.Single(x => x.Poster.PosterId == p.PosterId).EntityVersion), ContentVersion:state.ContentVersion);
            var result = SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                new("shell-exit-"+operation.ToString("N"),StateRequestFingerprint.Compute(new {intent.Id,cards}),state.Revision,reads),
                _ => plan, authorizedExternalSyncId:operation);
            return result.Status == StateCommandStatus.Committed ? cards.Length : 0;
        });
    }
    public int Reconcile()
    {
        int removed = 0;
        foreach (var intent in Pending())
        {
            // Never process original stale cards or an interrupted/unreleased gesture.
            try { removed += ObserveReleasedMove(intent); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            // A late/offline result is retained as an ordinary stale card. Never keep a
            // permanent recovery workflow or replay a move on startup.
            if (DateTimeOffset.UtcNow - intent.Created > TimeSpan.FromMinutes(2) ||
                new SqliteStateCommandRepository(store).Read().ContentCatalog.PosterItems.All(p => intent.Items.All(i => i.CardId != p.Id)))
                Close(intent);
        }
        return removed;
    }
    public void Close(ShellExitIntent intent) => store.ExecuteWrite((c,t) =>
    { using var q = Command(c,t,"UPDATE native_shell_exits SET closed=1 WHERE operation_id=$id",intent.Id); return q.ExecuteNonQuery(); });
    public IReadOnlyList<ShellExitIntent> Pending() => store.ExecuteRead((c,t) =>
    {
        using var q = c.CreateCommand(); q.Transaction=t;
        q.CommandText="SELECT body_json FROM native_shell_exits WHERE closed=0 AND (released IS NULL OR released=1) LIMIT 101";
        using var r=q.ExecuteReader(); var results=new List<ShellExitIntent>();
        while(r.Read())results.Add(JsonSerializer.Deserialize<ShellExitIntent>(r.GetString(0)) ?? throw new InvalidDataException("Invalid native exit receipt."));
        return results;
    });
    internal bool HasReleasedReceipt(ShellExitIntent intent) => store.ExecuteRead((c,t) =>
    {
        using var q=Command(c,t,"SELECT body_json FROM native_shell_exits WHERE operation_id=$id AND released=1 AND closed=0",intent.Id);
        return q.ExecuteScalar() as string == JsonSerializer.Serialize(intent);
    });
    private static string[] Entries(string parent)
    {
        var entries=Directory.EnumerateFileSystemEntries(parent).Take(10001).ToArray();
        if(entries.Length>10000)throw new IOException("目录核对超出预算，保留卡片。");
        return entries;
    }
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction t, string sql, Guid id)
    { var q=c.CreateCommand(); q.Transaction=t; q.CommandText=sql; q.Parameters.AddWithValue("$id",id.ToString()); return q; }
}
