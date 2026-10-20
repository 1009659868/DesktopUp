using System.Text.Json;
using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Operational receipt/facts share the authoritative SQLite store and lock, not a second database.
// They do not author poster configuration revisions. No replay of unfinished requests after a crash.
public sealed class SqliteContentOpenJournal(SqliteStateStore store) : IContentOpenJournal
{

    public void RetireInterruptedRequests() => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "UPDATE content_open_requests SET completed=1 WHERE completed=0;";
        return q.ExecuteNonQuery();
    });

    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "SELECT activation_id, request_json, target_hash, completed, result_json FROM content_open_requests LIMIT 1;";
        using (q.ExecuteReader()) { }
        q.CommandText = "SELECT activation_id, fact_json FROM content_open_facts LIMIT 1;";
        using (q.ExecuteReader()) { }
    }

    public ContentOpenRequestAdmission TryStart(ContentOpenRequest request) => store.ExecuteWrite((c, t) =>
    {
        _ = ContentOpenContract.Begin(request);
        string body = JsonSerializer.Serialize(request);
        using var old = c.CreateCommand(); old.Transaction = t;
        old.CommandText = "SELECT request_json FROM content_open_requests WHERE activation_id=$id;";
        old.Parameters.AddWithValue("$id", request.ActivationId.ToString());
        if (old.ExecuteScalar() is string known) return known == body ? ContentOpenRequestAdmission.Duplicate : ContentOpenRequestAdmission.Conflict;
        if (!SqliteStorageRelocationJournal.AllowsOpen(c,t,request)) return ContentOpenRequestAdmission.Busy;
        var state = ContentOpenContract.Start(ContentOpenContract.Begin(request), SqliteContentRepository.Read(c,t));
        if (state.State.Phase != ContentOpenPhase.Running) return ContentOpenRequestAdmission.Conflict;
        string hash = StateRequestFingerprint.Compute(new { request.Source.ProviderId, request.Target });
        using var busy = c.CreateCommand(); busy.Transaction = t;
        busy.CommandText = "SELECT COUNT(*) FROM content_open_requests WHERE completed=0 AND target_hash=$hash;";
        busy.Parameters.AddWithValue("$hash", hash);
        if (Convert.ToInt64(busy.ExecuteScalar()) > 0) return ContentOpenRequestAdmission.Busy;
        using var capacity = c.CreateCommand(); capacity.Transaction = t;
        capacity.CommandText = "SELECT COUNT(*) FROM content_open_requests WHERE completed=0;";
        if (Convert.ToInt64(capacity.ExecuteScalar()) >= 4) return ContentOpenRequestAdmission.Busy;
        using var insert = c.CreateCommand(); insert.Transaction = t;
        insert.CommandText = "INSERT INTO content_open_requests VALUES($id,$body,$hash,0,NULL);";
        insert.Parameters.AddWithValue("$id",request.ActivationId.ToString()); insert.Parameters.AddWithValue("$body",body); insert.Parameters.AddWithValue("$hash",hash);
        insert.ExecuteNonQuery(); return ContentOpenRequestAdmission.Ready;
    });

    public ContentOpenFact? Complete(ContentOpenRequest request, ContentOpenCompletion completion) => store.ExecuteWrite<ContentOpenFact?>((c,t) =>
    {
        using var read = c.CreateCommand(); read.Transaction=t;
        read.CommandText="SELECT request_json,completed FROM content_open_requests WHERE activation_id=$id;";
        read.Parameters.AddWithValue("$id",request.ActivationId.ToString());
        using (var row=read.ExecuteReader())
            if (!row.Read() || row.GetInt64(1)!=0 || row.GetString(0)!=JsonSerializer.Serialize(request)) return null;
        var transition=ContentOpenContract.Complete(new(request,ContentOpenPhase.Running),completion);
        if(!transition.Accepted) return null;
        using var update=c.CreateCommand(); update.Transaction=t;
        update.CommandText="UPDATE content_open_requests SET completed=1,result_json=$result WHERE activation_id=$id;";
        update.Parameters.AddWithValue("$result",JsonSerializer.Serialize(completion)); update.Parameters.AddWithValue("$id",request.ActivationId.ToString()); update.ExecuteNonQuery();
        if(transition.NewFact is { } fact)
        {
            using var add=c.CreateCommand(); add.Transaction=t;
            add.CommandText="INSERT INTO content_open_facts VALUES($id,$fact);";
            add.Parameters.AddWithValue("$id",fact.ActivationId.ToString()); add.Parameters.AddWithValue("$fact",JsonSerializer.Serialize(fact)); add.ExecuteNonQuery();
        }
        return transition.NewFact;
    });
}
