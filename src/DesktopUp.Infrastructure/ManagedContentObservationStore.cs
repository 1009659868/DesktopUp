using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal sealed record ManagedContentObservation(Guid MembershipId, string Locator, string Identity);

// Local operational evidence, never portable configuration. Membership deletion cascades it.
internal static class ManagedContentObservationStore
{
    internal const string MigrationSql = """
        CREATE TABLE managed_content_observations(
            membership_id TEXT PRIMARY KEY REFERENCES content_memberships(membership_id) ON DELETE CASCADE,
            locator TEXT NOT NULL, object_identity TEXT NOT NULL) STRICT;
        """;
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT membership_id,locator,object_identity FROM managed_content_observations LIMIT 0";
        using var r = q.ExecuteReader();
    }
    internal static Dictionary<Guid, ManagedContentObservation> Read(SqliteConnection c, SqliteTransaction t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT membership_id,locator,object_identity FROM managed_content_observations LIMIT 10001";
        using var r = q.ExecuteReader(); var result = new Dictionary<Guid, ManagedContentObservation>();
        while (r.Read())
        {
            if (result.Count >= 10000) throw new InvalidDataException("Managed observation budget exceeded.");
            var id = Guid.Parse(r.GetString(0)); result.Add(id, new(id, r.GetString(1), r.GetString(2)));
        }
        return result;
    }
    internal static void Put(SqliteConnection c, SqliteTransaction t, ManagedContentObservation observation)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = """
            INSERT INTO managed_content_observations VALUES($id,$path,$identity)
            ON CONFLICT(membership_id) DO UPDATE SET locator=excluded.locator,object_identity=excluded.object_identity
            WHERE locator<>excluded.locator OR object_identity<>excluded.object_identity
            """;
        q.Parameters.AddWithValue("$id", observation.MembershipId.ToString());
        q.Parameters.AddWithValue("$path", observation.Locator); q.Parameters.AddWithValue("$identity", observation.Identity);
        q.ExecuteNonQuery();
    }
    internal static void Capture(SqliteConnection c, SqliteTransaction t, ContentCatalogSnapshot catalog,
        IEnumerable<Guid> contentIds, Func<string,string>? readIdentity = null)
    {
        var ids = contentIds.ToHashSet();
        var managed = catalog.Collections.Where(x => x.Kind == ContentCollectionKind.Managed).Select(x => x.Id).ToHashSet();
        foreach (var member in catalog.Memberships.Where(m => managed.Contains(m.CollectionId) && ids.Contains(m.ContentItemId)))
        {
            string path = catalog.Items.Single(i => i.Id == member.ContentItemId).Target.Locator;
            Put(c, t, new(member.Id, path, (readIdentity ?? LocalTransferFiles.Identity)(path)));
        }
    }
}
