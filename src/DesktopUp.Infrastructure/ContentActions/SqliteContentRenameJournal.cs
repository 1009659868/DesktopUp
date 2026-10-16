using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Atomic receipt/index persistence and identity guards; never starts or retries rename.
public sealed class SqliteContentRenameJournal(SqliteStateStore store) : IContentRenameJournal
{
    public IDisposable AcquireExecutor() => new SqliteTransferJournal(store).AcquireExecutor();
    public void RequireIdle()
    {
        if(new SqliteTransferJournal(store).Read().Any(r=>!r.IsFinished) ||
            new SqliteStorageRelocationJournal(store).Read().Any(r=>r.Closure is null && r.Commit is null))
            throw new IOException("请等待当前文件移动或收纳位置迁移完成。");
    }
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    public void ValidateMappedAction(string posterId, string itemId) => new MappedDirectoryService(store).ValidateAction(posterId,itemId);
    public bool ProtectedPath(string path) => store.ProtectedTransferPath(path);
    public void Prepare(ContentRenameIntent intent, long expectedContentVersion)
    {
        store.ExecuteWrite((c,t) =>
        {
            if(SqliteContentRepository.ReadVersion(c,t)!=expectedContentVersion)throw new IOException("内容已变化，未改名。");
            using var q=c.CreateCommand();q.Transaction=t;
            q.CommandText="INSERT INTO content_renames(operation_id,body_json) VALUES($id,$body)";
            q.Parameters.AddWithValue("$id",intent.Id.ToString());q.Parameters.AddWithValue("$body",JsonSerializer.Serialize(intent));return q.ExecuteNonQuery();
        });
    }
    public IReadOnlyList<ContentRenameIntent> Pending()
    {
        return store.ExecuteRead((c,t) =>
        {
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT body_json FROM content_renames WHERE closed=0 LIMIT 101";
            using var r=q.ExecuteReader();var list=new List<ContentRenameIntent>();
            while(r.Read())list.Add(JsonSerializer.Deserialize<ContentRenameIntent>(r.GetString(0)) ?? throw new IOException("Invalid rename receipt."));return list;
        });
    }
    public void CommitObserved(ContentRenameIntent intent)
    {
        using var parent=LocalTransferFiles.LockDirectory(Path.GetDirectoryName(intent.Destination)!);
        if(LocalTransferFiles.Identity(Path.GetDirectoryName(intent.Destination)!)!=intent.ParentIdentity ||
            LocalTransferFiles.Identity(intent.Destination)!=intent.Identity)throw new IOException("改名结果暂不可确认，文件已保留。");
        store.ExecuteWrite((c,t) =>
        {
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            var plan = ContentFileActionPlans.Rename(state, intent);
            var result = SqliteStateCommandRepository.ExecuteInTransaction(c, t, plan.Request, _ => plan.Mutation);
            if(result.Status!=StateCommandStatus.Committed)throw new IOException(result.Error??"改名索引未提交。");
            ManagedContentObservationStore.Capture(c, t, plan.Mutation.ContentCatalog!, plan.ObservedIds);
            Close(c,t,intent.Id);return 0;
        });
    }
    public void Close(Guid id)=>store.ExecuteWrite((c,t)=>{Close(c,t,id);return 0;});
    private static void Close(SqliteConnection c,SqliteTransaction t,Guid id)
    { using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE content_renames SET closed=1 WHERE operation_id=$id";q.Parameters.AddWithValue("$id",id.ToString());q.ExecuteNonQuery(); }
}
