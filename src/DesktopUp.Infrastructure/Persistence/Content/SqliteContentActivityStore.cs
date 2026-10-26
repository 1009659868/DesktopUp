using System.Collections.ObjectModel;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class SqliteContentActivityStore(SqliteStateStore store) : IContentActivityStore
{
    public ContentActivityBatch ConsumeNext(int maximumFacts = 128)
    {
        if (maximumFacts is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(maximumFacts));
        return store.ExecuteWrite((c, t) =>
        {
            using var read = c.CreateCommand(); read.Transaction = t;
            read.CommandText = """
                SELECT p.sequence, p.activation_id, f.fact_json FROM content_activity_pending p
                JOIN content_open_facts f ON f.activation_id=p.activation_id
                ORDER BY p.sequence LIMIT $limit;
                """;
            read.Parameters.AddWithValue("$limit", maximumFacts);
            var facts = new List<(long Sequence, ContentOpenFact Fact)>();
            using (var rows = read.ExecuteReader())
                while (rows.Read())
                {
                    var fact = JsonSerializer.Deserialize<ContentOpenFact>(rows.GetString(2))
                        ?? throw new InvalidDataException("Missing open fact.");
                    if (fact.ActivationId.ToString() != rows.GetString(1) ||
                        ContentOpenFactPolicy.Admit([], fact) != ContentOpenFactAdmission.Added)
                        throw new InvalidDataException("Invalid open fact in activity queue.");
                    facts.Add((rows.GetInt64(0), fact));
                }
            long position = Position(c, t);
            foreach (var (sequence, fact) in facts)
            {
                if (sequence <= position) throw new InvalidDataException("Activity consumer position is inconsistent.");
                long observed = fact.ObservedAt.ToUnixTimeMilliseconds();
                using var apply = c.CreateCommand(); apply.Transaction = t;
                apply.CommandText = """
                    INSERT INTO content_activity_items(content_item_id, added_at_unix_ms, total_launches, last_launched_unix_ms)
                    VALUES($item,$time,1,$time)
                    ON CONFLICT(content_item_id) DO UPDATE SET
                        total_launches=content_activity_items.total_launches+1,
                        last_launched_unix_ms=MAX(COALESCE(content_activity_items.last_launched_unix_ms,$time),$time);
                    INSERT INTO content_activity_recent VALUES($item,$seq,$activation,$time,$poster);
                    DELETE FROM content_activity_recent WHERE sequence IN (
                        SELECT sequence FROM content_activity_recent WHERE content_item_id=$item
                        ORDER BY observed_unix_ms DESC, sequence DESC LIMIT -1 OFFSET 32);
                    DELETE FROM content_activity_pending WHERE sequence=$seq;
                    UPDATE content_activity_state SET consumed_position=$seq WHERE singleton_id=1;
                    """;
                apply.Parameters.AddWithValue("$item", fact.ContentItemId.ToString());
                apply.Parameters.AddWithValue("$time", observed);
                apply.Parameters.AddWithValue("$seq", sequence);
                apply.Parameters.AddWithValue("$activation", fact.ActivationId.ToString());
                apply.Parameters.AddWithValue("$poster", fact.PosterId);
                apply.ExecuteNonQuery();
                position = sequence;
            }
            return new ContentActivityBatch(position, facts.Count, HasPending(c, t),
                Array.AsReadOnly(facts.Select(x => x.Fact.ContentItemId).Distinct().ToArray()));
        });
    }

    public ContentActivitySnapshot Read(IReadOnlyCollection<Guid> contentIds)
    {
        if (contentIds.Count > ContentLayoutPolicy.MaximumItems || contentIds.Contains(Guid.Empty))
            throw new ArgumentException("Invalid activity query membership.", nameof(contentIds));
        return store.ExecuteRead((c, t) =>
        {
            using var q = c.CreateCommand(); q.Transaction = t;
            q.CommandText = """
                SELECT content_item_id,added_at_unix_ms,total_launches,last_launched_unix_ms
                FROM content_activity_items WHERE content_item_id IN (SELECT value FROM json_each($ids));
                """;
            q.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(contentIds.Distinct().Select(x => x.ToString())));
            var values = new Dictionary<Guid, ContentActivityUsage>();
            using (var rows = q.ExecuteReader())
                while (rows.Read())
                {
                    var id = Guid.Parse(rows.GetString(0));
                    values.Add(id, new(id, DateTimeOffset.FromUnixTimeMilliseconds(rows.GetInt64(1)), rows.GetInt64(2),
                        rows.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeMilliseconds(rows.GetInt64(3))));
                }
            return new ContentActivitySnapshot(Position(c, t), HasPending(c, t),
                new ReadOnlyDictionary<Guid, ContentActivityUsage>(values));
        });
    }

    private static long Position(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT consumed_position FROM content_activity_state WHERE singleton_id=1;";
        return q.ExecuteScalar() is long value && value >= 0 ? value : throw new InvalidDataException("Missing activity consumer position.");
    }

    private static bool HasPending(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT EXISTS(SELECT 1 FROM content_activity_pending);";
        return (long)q.ExecuteScalar()! != 0;
    }

    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        _ = Position(c, t);
        foreach (string sql in new[] {
            "SELECT content_item_id,added_at_unix_ms,total_launches,last_launched_unix_ms FROM content_activity_items LIMIT 1;",
            "SELECT sequence,activation_id FROM content_activity_pending LIMIT 1;",
            "SELECT content_item_id,sequence,activation_id,observed_unix_ms,poster_id FROM content_activity_recent LIMIT 1;" })
        {
            using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql;
            using (q.ExecuteReader()) { }
        }
        using var triggers = c.CreateCommand(); triggers.Transaction = t;
        triggers.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='trigger' AND name IN ('content_activity_item_added','content_activity_fact_added');";
        if ((long)triggers.ExecuteScalar()! != 2) throw new InvalidDataException("Missing activity persistence triggers.");
    }
}
