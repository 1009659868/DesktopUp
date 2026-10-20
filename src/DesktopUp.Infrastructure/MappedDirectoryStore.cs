using System.Text.Json;
using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal static class MappedDirectoryStore
{
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    { using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT collection_id,root_identity,observations_json FROM mapped_directory_observations LIMIT 0"; using var r = q.ExecuteReader(); }
    internal static MappedDirectoryEvidence? Read(SqliteConnection c, SqliteTransaction t, Guid collection)
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "SELECT root_identity,observations_json FROM mapped_directory_observations WHERE collection_id=$id";
        q.Parameters.AddWithValue("$id", collection.ToString()); using var r = q.ExecuteReader(); if (!r.Read()) return null;
        string json = r.GetString(1); if (json.Length > 512 * 1024) throw new IOException("目录观察记录超限。");
        try
        {
            var items = JsonSerializer.Deserialize<MappedContentObservation[]>(json);
            if (items is null || items.Any(item => item is null))
                throw new IOException("目录观察记录无效。");
            return new(r.GetString(0), items);
        }
        catch (JsonException e)
        {
            // A malformed directory witness must not abort observation of unrelated directories.
            throw new IOException("目录观察记录无效。", e);
        }
    }
    internal static void Put(SqliteConnection c, SqliteTransaction t, Guid collection, MappedDirectoryEvidence evidence)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "INSERT INTO mapped_directory_observations VALUES($id,$root,$items) ON CONFLICT(collection_id) DO UPDATE SET root_identity=excluded.root_identity,observations_json=excluded.observations_json";
        q.Parameters.AddWithValue("$id", collection.ToString()); q.Parameters.AddWithValue("$root", evidence.Identity);
        q.Parameters.AddWithValue("$items", JsonSerializer.Serialize(evidence.Items)); q.ExecuteNonQuery();
    }
}
