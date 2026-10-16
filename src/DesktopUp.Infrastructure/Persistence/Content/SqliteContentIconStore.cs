using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class SqliteContentIconStore(SqliteStateStore store) : IContentIconStore
{
    internal const string MigrationSql = """
        CREATE TABLE content_icon_assets (
            asset_id TEXT PRIMARY KEY NOT NULL,
            format_version INTEGER NOT NULL CHECK(format_version=1),
            width INTEGER NOT NULL CHECK(width BETWEEN 1 AND 256),
            height INTEGER NOT NULL CHECK(height BETWEEN 1 AND 256),
            pixels BLOB NOT NULL CHECK(length(pixels)=width*height*4)
        ) STRICT;
        ALTER TABLE content_poster_items ADD COLUMN icon_asset_id TEXT NULL REFERENCES content_icon_assets(asset_id);
        """;

    public async ValueTask<ContentIconAsset?> ReadAsync(string id, CancellationToken cancellation)
    {
        if (!ContentIconAsset.ValidId(id)) throw new ArgumentException("Invalid icon identity.");
        cancellation.ThrowIfCancellationRequested();
        var asset = await Task.Run(() => store.ExecuteRead((c, t) => Read(c, t, id)), cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        return asset;
    }
    private static ContentIconAsset? Read(SqliteConnection c, SqliteTransaction? t, string id)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT format_version,width,height,length(pixels),pixels FROM content_icon_assets WHERE asset_id=$id";
        q.Parameters.AddWithValue("$id", id);
        using var r = q.ExecuteReader();
        if (!r.Read()) return null;
        if (r.GetInt64(0) != 1 || r.GetInt64(3) is < 4 or > ContentIconAsset.MaximumBytes) throw new InvalidDataException("Unknown/oversized icon asset.");
        var asset = ContentIconAsset.Create(r.GetInt32(1), r.GetInt32(2), (byte[])r[4]);
        if (asset.Id != id) throw new InvalidDataException("Icon content hash mismatch.");
        return asset;
    }
    internal static void Verify(SqliteConnection c, SqliteTransaction? t, bool verifyPixels = false)
    {
        CheckBudget(c, t);
        var catalog = SqliteContentRepository.Read(c, t);
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT relation_id,icon_asset_id FROM content_poster_items";
        var ids = new Dictionary<Guid, string?>();
        using (var r = q.ExecuteReader()) while (r.Read()) ids.Add(Guid.Parse(r.GetString(0)), r.IsDBNull(1) ? null : r.GetString(1));
        foreach (var relation in catalog.PosterItems)
            if (ids[relation.Id] != relation.IconAssetId) throw new InvalidDataException("Icon reference and card disagree.");
        q.CommandText = "SELECT asset_id,format_version,width,height,length(pixels) FROM content_icon_assets";
        var assets = new List<string>();
        using (var r = q.ExecuteReader()) while (r.Read())
        {
            if (!ContentIconAsset.ValidId(r.GetString(0)) || r.GetInt64(1) != 1 || r.GetInt64(2) is < 1 or > 256 ||
                r.GetInt64(3) is < 1 or > 256 || r.GetInt64(4) != r.GetInt64(2) * r.GetInt64(3) * 4)
                throw new InvalidDataException("Invalid icon metadata.");
            assets.Add(r.GetString(0));
        }
        // Full hashing at startup/backup and demand-load, not every state/read transaction.
        if (verifyPixels) foreach (string id in assets) _ = Read(c, t, id);
        if (ids.Values.Any(id => id is not null && !assets.Contains(id))) throw new InvalidDataException("Missing icon asset.");
    }
    internal static void Put(SqliteConnection c, SqliteTransaction t, ContentIconAsset asset)
    {
        var existing = Read(c, t, asset.Id);
        if (existing is not null) return;
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "INSERT INTO content_icon_assets VALUES($id,1,$w,$h,$pixels)";
        q.Parameters.AddWithValue("$id", asset.Id); q.Parameters.AddWithValue("$w", asset.Width);
        q.Parameters.AddWithValue("$h", asset.Height); q.Parameters.AddWithValue("$pixels", asset.Pixels);
        q.ExecuteNonQuery();
    }
    internal static void CollectAndCheck(SqliteConnection c, SqliteTransaction t)
    {
        // Only our canonical BLOBs. Never deletes the user's selected picture or real target.
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "DELETE FROM content_icon_assets WHERE NOT EXISTS(SELECT 1 FROM content_poster_items p WHERE p.icon_asset_id=asset_id)";
        q.ExecuteNonQuery(); CheckBudget(c, t);
    }
    private static void CheckBudget(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT count(*),COALESCE(sum(length(pixels)),0) FROM content_icon_assets";
        using var r = q.ExecuteReader(); r.Read();
        if (r.GetInt64(0) > ContentIconAsset.MaximumAssets || r.GetInt64(1) > ContentIconAsset.MaximumStoreBytes)
            throw new InvalidDataException("Icon store budget exceeded; previous state retained.");
    }
}
