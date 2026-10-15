using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record ContentRenameTarget(Guid ContentId, long Version, string Before, string After);
public sealed record ContentRenameIntent(Guid Id, string Source, string Destination, string Identity,
    string ParentIdentity, IReadOnlyList<ContentRenameTarget> Targets);

// A small write-ahead receipt, not a user recovery workflow. Startup observes; it never
// replays the rename. Neither failure nor restart deletes a file or overwrites a collision.
public sealed class ContentRenameService(SqliteStateStore store)
{
    internal const string MigrationSql = """
        CREATE TABLE content_renames(operation_id TEXT PRIMARY KEY,body_json TEXT NOT NULL,
            closed INTEGER NOT NULL DEFAULT 0 CHECK(closed IN(0,1))) STRICT;
        """;
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    { using var q=c.CreateCommand(); q.Transaction=t; q.CommandText="SELECT operation_id,body_json,closed FROM content_renames LIMIT 0"; using var r=q.ExecuteReader(); }
    internal Action<string>? Checkpoint { get; set; }

    public void Rename(string posterId, string itemId, Guid contentId, long version, string name, CancellationToken token = default,
        string? expectedIdentity = null)
    {
        if (LocalFileNamePolicy.Error(name) is { } error) throw new IOException(error);
        using var executor = new SqliteTransferJournal(store).AcquireExecutor();
        if(new SqliteTransferJournal(store).Read().Any(r=>!r.IsFinished) ||
            new SqliteStorageRelocationJournal(store).Read().Any(r=>r.Closure is null && r.Commit is null))
            throw new IOException("请等待当前文件移动或收纳位置迁移完成。");
        var snapshot = new SqliteStateCommandRepository(store).Read();
        new MappedDirectoryService(store).ValidateAction(posterId, itemId);
        var relation = ContentRelationPolicy.Resolve(snapshot.ContentCatalog, posterId, itemId);
        if (relation is null || relation.Item.Id != contentId || relation.Item.Version != version ||
            !LocalContentProtocol.IsSupported(relation.Item)) throw new IOException("所选对象已变化，请重新打开菜单。");
        string source = LocalTransferFiles.Normalize(relation.Item.Target.Locator);
        string destination = LocalTransferFiles.Normalize(Path.Combine(Path.GetDirectoryName(source)!, name));
        if (source == destination) return;
        if (store.ProtectedTransferPath(source) || store.ProtectedTransferPath(destination)) throw new IOException("此路径受保护，不能改名。");
        string identity = LocalTransferFiles.Identity(source), parent = LocalTransferFiles.Identity(Path.GetDirectoryName(source)!);
        if(expectedIdentity is not null && identity != expectedIdentity)throw new IOException("改名窗口打开后对象已被替换，未执行。");
        var targets = snapshot.ContentCatalog.Items.Where(i => i.Source.ProviderId == "local-file-reference-v1" &&
                (string.Equals(i.Target.Locator,source,StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(source,i.Target.Locator)))
            .Select(i => new ContentRenameTarget(i.Id,i.Version,i.Target.Locator,destination+i.Target.Locator[source.Length..])).ToArray();
        var intent = new ContentRenameIntent(Guid.NewGuid(),source,destination,identity,parent,targets);
        token.ThrowIfCancellationRequested();
        store.ExecuteWrite((c,t) =>
        {
            if(SqliteContentRepository.ReadVersion(c,t)!=snapshot.ContentVersion)throw new IOException("内容已变化，未改名。");
            using var q=c.CreateCommand();q.Transaction=t;
            q.CommandText="INSERT INTO content_renames(operation_id,body_json) VALUES($id,$body)";
            q.Parameters.AddWithValue("$id",intent.Id.ToString());q.Parameters.AddWithValue("$body",JsonSerializer.Serialize(intent));return q.ExecuteNonQuery();
        });
        Checkpoint?.Invoke("Prepared");
        try
        {
            token.ThrowIfCancellationRequested();
            LocalTransferFiles.RenameKnown(source,destination,identity,parent);
            Checkpoint?.Invoke("Renamed");
            CommitObserved(intent);
        }
        catch
        {
            // Commit acknowledgment can fail after durable commit. Never move the file back
            // underneath an already updated index; retain ambiguous outcomes for observation.
            var current = new SqliteStateCommandRepository(store).Read();
            if(intent.Targets.All(target=>current.ContentCatalog.Items.Any(i=>i.Id==target.ContentId &&
                i.Target.Locator==target.After && i.Version==target.Version+1)))return;
            // Roll back only our exact object, if the original name is still free. A competing
            // file is never removed to make room. Uncertain receipts stay for observation.
            try
            {
                if (LocalTransferFiles.Identity(Path.GetDirectoryName(source)!) == parent &&
                    LocalTransferFiles.Identity(destination) == identity &&
                    (string.Equals(source,destination,StringComparison.OrdinalIgnoreCase) || !File.Exists(source) && !Directory.Exists(source)))
                    LocalTransferFiles.RenameKnown(destination,source,identity,parent);
                if (LocalTransferFiles.Identity(source)==identity) Close(intent.Id);
            }
            catch (Exception e) when(e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public int Reconcile()
    {
        var pending = store.ExecuteRead((c,t) =>
        {
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT body_json FROM content_renames WHERE closed=0 LIMIT 101";
            using var r=q.ExecuteReader();var list=new List<ContentRenameIntent>();
            while(r.Read())list.Add(JsonSerializer.Deserialize<ContentRenameIntent>(r.GetString(0)) ?? throw new IOException("Invalid rename receipt."));return list;
        });
        int changed=0;
        foreach(var intent in pending)
        {
            using var executor = new SqliteTransferJournal(store).AcquireExecutor();
            try
            {
                if(LocalTransferFiles.Identity(Path.GetDirectoryName(intent.Source)!)!=intent.ParentIdentity)continue;
                // Case-only renames must use the actual directory spelling, not Exists(old).
                string? observed=Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(intent.Source)!)
                    .Take(10001).FirstOrDefault(p=>string.Equals(p,intent.Destination,StringComparison.OrdinalIgnoreCase));
                if(observed==intent.Destination && LocalTransferFiles.Identity(observed)==intent.Identity)
                { CommitObserved(intent);changed++; }
                else if(LocalTransferFiles.Identity(intent.Source)==intent.Identity)Close(intent.Id);
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        }
        return changed;
    }
    private void CommitObserved(ContentRenameIntent intent)
    {
        using var parent=LocalTransferFiles.LockDirectory(Path.GetDirectoryName(intent.Destination)!);
        if(LocalTransferFiles.Identity(Path.GetDirectoryName(intent.Destination)!)!=intent.ParentIdentity ||
            LocalTransferFiles.Identity(intent.Destination)!=intent.Identity)throw new IOException("改名结果暂不可确认，文件已保留。");
        store.ExecuteWrite((c,t) =>
        {
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            // A repair or later operation wins. Do not overwrite new user intent.
            if(intent.Targets.Any(target=>!state.ContentCatalog.Items.Any(i=>i.Id==target.ContentId &&
                i.Version==target.Version && i.Target.Locator==target.Before)))throw new IOException("引用已变化，改名结果需重新核对。");
            var changes=intent.Targets.ToDictionary(x=>x.ContentId);
            var catalog=state.ContentCatalog with { Items=state.ContentCatalog.Items.Select(i=>changes.TryGetValue(i.Id,out var target)
                ? i with {Target=i.Target with {Locator=target.After},Source=i.Source with {Key=target.After},
                    DisplayName=Path.GetFileName(target.After),Version=checked(i.Version+1)} : i).ToArray() };
            var affected=intent.Targets.SelectMany(x=>ContentReferencePolicy.AffectedPosters(state.ContentCatalog,x.ContentId)).Distinct().ToHashSet();
            var posters=state.Catalog.Posters.Where(p=>affected.Contains(p.Poster.PosterId)).ToArray();
            var result=SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                new("rename-"+intent.Id.ToString("N"),StateRequestFingerprint.Compute(intent),state.Revision,
                    new(posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),ContentVersion:state.ContentVersion)),
                _=>new() {ContentCatalog=catalog,Updates=posters.Select(p=>p.Poster).ToArray()});
            if(result.Status!=StateCommandStatus.Committed)throw new IOException(result.Error??"改名索引未提交。");
            ManagedContentObservationStore.Capture(c,t,catalog,changes.Keys);
            Close(c,t,intent.Id);return 0;
        });
    }
    private void Close(Guid id)=>store.ExecuteWrite((c,t)=>{Close(c,t,id);return 0;});
    private static void Close(SqliteConnection c,SqliteTransaction t,Guid id)
    { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE content_renames SET closed=1 WHERE operation_id=$id";q.Parameters.AddWithValue("$id",id.ToString());q.ExecuteNonQuery(); }
}
