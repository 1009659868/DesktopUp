using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Provenance is independent of current locator, card identity and poster lifetime.
internal static class ContentReturnStore
{
    internal const string MigrationSql = """
        CREATE TABLE content_return_origins(item_id TEXT PRIMARY KEY REFERENCES content_items(item_id), original_path TEXT NOT NULL) STRICT;
        CREATE TABLE content_returns(operation_id TEXT PRIMARY KEY, body_json TEXT NOT NULL, result_json TEXT, closed INTEGER NOT NULL DEFAULT 0 CHECK(closed IN(0,1))) STRICT;
        """;
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        foreach (var sql in new[] { "SELECT item_id,original_path FROM content_return_origins LIMIT 0", "SELECT operation_id,body_json,result_json,closed FROM content_returns LIMIT 0" })
        { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; using var r = q.ExecuteReader(); }
    }
    internal static void Capture(SqliteConnection c, SqliteTransaction t, ContentCatalogSnapshot catalog, Guid id, string source)
    {
        // Creation uses the ingress executor, but its private staging path is not user provenance.
        string parentName = Path.GetFileName(Path.GetDirectoryName(source)) ?? "";
        if (parentName.StartsWith(".desktopup-new-", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(parentName[15..], "N", out _)) return;
        // Do not turn an intermediate managed directory into an original location.
        if (catalog.Collections.Any(x => x.Kind == ContentCollectionKind.Managed && x.StorageLocator is { } root &&
            (string.Equals(source, root, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(root, source)))) return;
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "INSERT OR IGNORE INTO content_return_origins VALUES($id,$path)";
        q.Parameters.AddWithValue("$id", id.ToString()); q.Parameters.AddWithValue("$path", source); q.ExecuteNonQuery();
    }
    internal static void CaptureIncoming(SqliteConnection c, SqliteTransaction t, ContentCatalogSnapshot catalog, DurableTransferBatch batch, int index)
    {
        if (batch.Request.Route != ContentDropRoute.IntoManagedStorage) return;
        var entry = batch.Entries[index]; var file = batch.Files[index];
        foreach (var id in entry.AffectedContentIds.Append(entry.Entry.Item.Id).Distinct())
        {
            var item = catalog.Items.Single(i => i.Id == id);
            var original = id == entry.Entry.Item.Id ? file.Source : Path.Combine(file.Source, Path.GetRelativePath(file.Destination, item.Target.Locator));
            Capture(c, t, catalog, id, original);
        }
    }
    internal static void Backfill(SqliteStateStore store) => store.ExecuteWrite((c, t) =>
    {
        var catalog = SqliteContentRepository.Read(c, t);
        // Only committed transfer receipts with the same stable entity ID are evidence.
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT b.body_json,i.item_index FROM content_transfers b JOIN content_transfer_items i ON i.operation_id=b.operation_id WHERE i.phase=4 ORDER BY b.rowid,i.item_index";
        var entries = new List<(DurableTransferBatch Batch, int Index)>();
        using (var r = q.ExecuteReader()) while (r.Read()) entries.Add((SqliteTransferJournal.Decode<DurableTransferBatch>(r.GetString(0)), r.GetInt32(1)));
        foreach (var (batch, index) in entries)
        {
            if (batch.Request.Route != ContentDropRoute.IntoManagedStorage) continue;
            var id = batch.Entries[index].Entry.Item.Id;
            if (catalog.Items.Any(i => i.Id == id)) Capture(c, t, catalog, id, batch.Files[index].Source);
        }
        return 0;
    });
    internal static string? Original(SqliteStateStore store, Guid id) => store.ExecuteRead((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT original_path FROM content_return_origins WHERE item_id=$id";
        q.Parameters.AddWithValue("$id", id.ToString()); return q.ExecuteScalar() as string;
    });
    internal static bool Pending(SqliteStateStore store) => store.ExecuteRead((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT EXISTS(SELECT 1 FROM content_returns WHERE closed=0)";
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    });
}
