using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Same connection/transaction as the poster aggregate. No second store or filesystem operations.
internal static class SqliteContentRepository
{
    internal const string MigrationSql = """
        CREATE TABLE content_head (
            singleton_id INTEGER PRIMARY KEY CHECK (singleton_id = 1),
            version INTEGER NOT NULL CHECK (version >= 0)
        ) STRICT;
        INSERT INTO content_head VALUES (1, 0);
        CREATE TABLE content_items (
            item_id TEXT PRIMARY KEY NOT NULL,
            body_json TEXT NOT NULL CHECK (json_valid(body_json) AND json_extract(body_json, '$.id') = item_id)
        ) STRICT;
        CREATE TABLE content_collections (
            collection_id TEXT PRIMARY KEY NOT NULL,
            body_json TEXT NOT NULL CHECK (json_valid(body_json) AND json_extract(body_json, '$.id') = collection_id)
        ) STRICT;
        CREATE TABLE content_memberships (
            membership_id TEXT PRIMARY KEY NOT NULL,
            collection_id TEXT NOT NULL REFERENCES content_collections(collection_id),
            item_id TEXT NOT NULL REFERENCES content_items(item_id),
            body_json TEXT NOT NULL CHECK (json_valid(body_json)
                AND json_extract(body_json, '$.id') = membership_id
                AND json_extract(body_json, '$.collectionId') = collection_id
                AND json_extract(body_json, '$.contentItemId') = item_id),
            UNIQUE(collection_id, item_id)
        ) STRICT;
        CREATE TABLE content_poster_bindings (
            poster_id TEXT PRIMARY KEY NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
            collection_id TEXT NOT NULL REFERENCES content_collections(collection_id),
            body_json TEXT NOT NULL CHECK (json_valid(body_json)
                AND json_extract(body_json, '$.posterId') = poster_id
                AND json_extract(body_json, '$.collectionId') = collection_id)
        ) STRICT;
        CREATE TABLE content_poster_items (
            relation_id TEXT PRIMARY KEY NOT NULL,
            poster_id TEXT NOT NULL REFERENCES content_poster_bindings(poster_id) ON DELETE CASCADE,
            membership_id TEXT NOT NULL REFERENCES content_memberships(membership_id),
            body_json TEXT NOT NULL CHECK (json_valid(body_json)
                AND json_extract(body_json, '$.id') = relation_id
                AND json_extract(body_json, '$.posterId') = poster_id
                AND json_extract(body_json, '$.membershipId') = membership_id),
            UNIQUE(poster_id, membership_id)
        ) STRICT;
        CREATE TABLE content_demo_archive (
            poster_id TEXT PRIMARY KEY NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
            original_layout_json TEXT NULL,
            archived_revision INTEGER NOT NULL CHECK (archived_revision >= 1)
        ) STRICT;
        """;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 32
    };

    internal static long ReadVersion(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT version FROM content_head WHERE singleton_id=1;";
        return q.ExecuteScalar() is long version && version >= 0 ? version
            : throw new InvalidDataException("Content head is missing or invalid.");
    }

    internal static ContentCatalogSnapshot Read(SqliteConnection c, SqliteTransaction? t) => ContentCatalogPolicy.Freeze(new(
        Rows<ContentItem>(c, t, "content_items", "item_id"),
        Rows<Collection>(c, t, "content_collections", "collection_id"),
        Rows<CollectionMembership>(c, t, "content_memberships", "membership_id"),
        Rows<ContentPosterBinding>(c, t, "content_poster_bindings", "poster_id"),
        Rows<PosterItem>(c, t, "content_poster_items", "relation_id")));

    private static T[] Rows<T>(SqliteConnection c, SqliteTransaction? t, string table, string key)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = $"SELECT length(CAST(body_json AS BLOB)), body_json FROM {table} ORDER BY {key} LIMIT 10001;";
        using var r = q.ExecuteReader();
        var values = new List<T>();
        long bytes = 0;
        while (r.Read())
        {
            if ((bytes += r.GetInt64(0)) > ContentCatalogCodec.MaximumBytes) throw new InvalidDataException("Content table exceeds byte budget.");
            values.Add(JsonSerializer.Deserialize<T>(r.GetString(1), Json) ?? throw new InvalidDataException("Null content record."));
        }
        return values.ToArray();
    }

    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        _ = ReadVersion(c, t);
        var catalog = Read(c, t);
        foreach (var binding in catalog.Bindings)
        {
            using var poster = c.CreateCommand(); poster.Transaction = t;
            poster.CommandText = "SELECT collection_id, content_layout_json FROM posters WHERE poster_id=$id";
            poster.Parameters.AddWithValue("$id", binding.PosterId);
            using var row = poster.ExecuteReader();
            if (!row.Read() || row.IsDBNull(0) || row.GetString(0) != binding.CollectionId.ToString() || row.IsDBNull(1) ||
                !ContentLayoutPolicy.TryRead(row.GetString(1), binding.PosterId, out var layout, out _) ||
                !layout!.Placements.Select(p => p.ItemId).ToHashSet().SetEquals(catalog.PosterItems.Where(p => p.PosterId == binding.PosterId).Select(p => p.LayoutItemId)))
                throw new InvalidDataException("Stored content binding/layout is inconsistent; original database retained.");
        }
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT poster_id, original_layout_json, archived_revision FROM content_demo_archive LIMIT 1;";
        using var r = q.ExecuteReader();
    }

    internal static void Apply(SqliteConnection c, SqliteTransaction t, DesktopStateSnapshot before,
        StateMutationPlan plan, long revision)
    {
        if (plan.ContentCatalog is { } next)
        {
            foreach (var id in plan.ExplicitDetachedRelations)
            {
                using var remove = c.CreateCommand(); remove.Transaction = t;
                remove.CommandText = "DELETE FROM content_poster_items WHERE relation_id=$id;";
                remove.Parameters.AddWithValue("$id", id.ToString());
                if (remove.ExecuteNonQuery() != 1) throw new InvalidDataException("Reference detach target disappeared.");
            }
            foreach (var b in next.Bindings.Where(b => before.ContentCatalog.Bindings.All(old => old.PosterId != b.PosterId)))
            {
                var old = before.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == b.PosterId);
                if (old is null) continue; // A clone has no previous DEMO composition.
                using var q = c.CreateCommand(); q.Transaction = t;
                q.CommandText = "INSERT INTO content_demo_archive VALUES ($id,$raw,$rev);";
                q.Parameters.AddWithValue("$id", b.PosterId);
                q.Parameters.AddWithValue("$raw", (object?)old.Poster.ContentLayoutJson ?? DBNull.Value);
                q.Parameters.AddWithValue("$rev", revision); q.ExecuteNonQuery();
            }
            foreach (var v in next.Items) Put(c,t,"content_items","item_id",v.Id,v);
            foreach (var v in next.Collections) Put(c,t,"content_collections","collection_id",v.Id,v);
            foreach (var v in next.Memberships) Put(c,t,"content_memberships","membership_id",v.Id,v,
                ("collection_id", v.CollectionId.ToString()), ("item_id", v.ContentItemId.ToString()));
            foreach (var v in next.Bindings) Put(c,t,"content_poster_bindings","poster_id",v.PosterId,v,
                ("collection_id",v.CollectionId.ToString()));
            foreach (var v in next.PosterItems) Put(c,t,"content_poster_items","relation_id",v.Id,v,
                ("poster_id",v.PosterId), ("membership_id",v.MembershipId.ToString()));
        }
        if (plan.ContentCatalog is not null || before.ContentCatalog.Bindings.Any(b => plan.Removes.Contains(b.PosterId)))
        {
            using var q = c.CreateCommand(); q.Transaction = t;
            q.CommandText = "UPDATE content_head SET version=version+1 WHERE singleton_id=1 AND version=$old;";
            q.Parameters.AddWithValue("$old",before.ContentVersion);
            if(q.ExecuteNonQuery()!=1) throw new InvalidDataException("Content version changed during transaction.");
        }
    }

    private static void Put<T>(SqliteConnection c, SqliteTransaction t, string table, string key, object id,
        T value, params (string Column, string Value)[] fields)
    {
        using var q = c.CreateCommand(); q.Transaction=t;
        var columns = new[] { key, "body_json" }.Concat(fields.Select(f => f.Column)).ToArray();
        q.CommandText = $"INSERT INTO {table} ({string.Join(",",columns)}) VALUES ({string.Join(",",columns.Select((_,i)=>"$p"+i))}) " +
            $"ON CONFLICT({key}) DO UPDATE SET " + string.Join(",",columns.Skip(1).Select(n=>$"{n}=excluded.{n}")) + ";";
        q.Parameters.AddWithValue("$p0",id.ToString()!);
        q.Parameters.AddWithValue("$p1",JsonSerializer.Serialize(value,Json));
        for(int i=0;i<fields.Length;i++)q.Parameters.AddWithValue("$p"+(i+2),fields[i].Value);
        q.ExecuteNonQuery();
    }
}
