using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Compatibility entry for startup/fixtures; all sequencing lives in Application.
public sealed class ContentRenameService(SqliteStateStore store)
{
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    { using var q=c.CreateCommand(); q.Transaction=t; q.CommandText="SELECT operation_id,body_json,closed FROM content_renames LIMIT 0"; using var r=q.ExecuteReader(); }
    internal Action<string>? Checkpoint { get; set; }


    private ContentRenameUseCase UseCase => new(new SqliteContentRenameJournal(store), new LocalContentActionFiles(), Checkpoint);
    public void Rename(string posterId, string itemId, Guid contentId, long version, string name,
        CancellationToken token = default, string? expectedIdentity = null) =>
        UseCase.Rename(posterId, itemId, contentId, version, name, token, expectedIdentity);
    public int Reconcile() => UseCase.Reconcile();
}
