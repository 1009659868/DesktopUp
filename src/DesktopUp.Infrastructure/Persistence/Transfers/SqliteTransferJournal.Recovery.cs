using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class TransferRetryTicket
{
    internal TransferRetryTicket(DurableTransferItem original,string witness,LocalTransferPreview preview,Guid? storage)
    { Original=original;OriginalFingerprint=witness;Preview=preview;StorageCollectionId=storage; }
    public DurableTransferItem Original { get; }
    internal string OriginalFingerprint { get; }
    public LocalTransferPreview Preview { get; }
    internal Guid? StorageCollectionId { get; }
}
public sealed record TransferRecoveryEntry(DurableTransferItem Item,string PosterId,bool PosterExists,string Witness)
{
    public ContentDropRoute Route { get; init; }
}

public sealed partial class SqliteTransferJournal
{
    public IReadOnlyList<TransferRecoveryEntry> RecoveryEntries() => store.ExecuteRead((c,t)=>
    {
        var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
        return (IReadOnlyList<TransferRecoveryEntry>)ReadItems(c,t).Select(row=>
        {
            var batch=ReadBatch(c,t,row.OperationId);
            return new TransferRecoveryEntry(row,batch.Request.PosterId,
                state.Catalog.Posters.Any(p=>p.Poster.PosterId==batch.Request.PosterId),StateRequestFingerprint.Compute(row)) {Route=batch.Request.Route};
        }).ToArray();
    });

    public IReadOnlyList<RegisteredManagedStorage> StorageLocations() => store.ExecuteRead((c, t) =>
        ReadStorageLocations(c, t, SqliteStateCommandRepository.ReadSnapshot(c, t).ContentCatalog));

    // The caller supplies the catalog read in this SAME transaction, never a retained projection.
    internal static IReadOnlyList<RegisteredManagedStorage> ReadStorageLocations(
        SqliteConnection c, SqliteTransaction t, ContentCatalogSnapshot catalog)
    {
        var collections = catalog.Collections.ToDictionary(x => x.Id);
        var result=new List<RegisteredManagedStorage>();
        using var q=Command(c,t,"SELECT r.body_json,c.collection_id FROM managed_storage_collections c JOIN managed_storage_roots r ON c.root_id=r.root_id");
        using var rows=q.ExecuteReader();
        while(rows.Read())
        {
            if(result.Count>=1000)throw new InvalidDataException("Storage registration capacity exceeded.");
            result.Add(new(Decode<ManagedStorageRoot>(rows.GetString(0)),collections[Guid.Parse(rows.GetString(1))]));
        }
        return result;
    }

    // Discovery is read-only. Reconciliation is explicit and never replays a file move.
    public TransferRetryTicket PreviewRetry(Guid operationId,int index)
    {
        using var executor=AcquireExecutor();
        var entry=RecoveryEntries().Single(r=>r.Item.OperationId==operationId&&r.Item.Index==index);
        using var original=LockUnmoved(entry.Item);
        return store.ExecuteRead((c,t)=>
        {
            var batch=ReadBatch(c,t,operationId);
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            if(!entry.PosterExists)throw new IOException("原海报已不存在。文件仍保留；请先找回文件，不自动重建海报。");
            var source=batch.Entries[index].Entry;
            var item=state.ContentCatalog.Items.SingleOrDefault(x=>x.Id==source.Item.Id)??source.Item;
            if(!string.Equals(item.Target.Locator,entry.Item.File.Source,StringComparison.OrdinalIgnoreCase))
                throw new IOException("共享来源已被修复或移动，请从当前内容重新发起，不复用旧任务。");
            var preview=LocalTransferPreview.Prepare(state,batch.Request.PosterId,batch.Request.Route,
                [(item,entry.Item.File.Destination,source.OriginPosterItemId)],batch.Request.Root);
            // Always fresh facts/read set/confirmation. The old batch is immutable history.
            return new TransferRetryTicket(entry.Item,entry.Witness,preview,batch.StorageCollectionId);
        });
    }

    public Guid ConfirmRetry(TransferRetryTicket ticket,string confirmedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if(confirmedFingerprint!=ticket.Preview.Plan.Fingerprint)throw new IOException("请重新确认完整移动预览。");
        using var executor=AcquireExecutor();
        using var original=LockUnmoved(ticket.Original);
        // Old reservation release and fresh intent are in ONE SQLite transaction. Any new
        // admission/CAS failure rolls both back; no unjournaled gap permits another move.
        BeginCore(ticket.Preview.Plan,ticket.Preview.Files,confirmedFingerprint,ticket.StorageCollectionId,ticket);
        return ticket.Preview.Plan.Request.OperationId;
    }

    public void ReleaseUnexecuted(Guid operationId,int index,string confirmedWitness)
    {
        using var executor=AcquireExecutor();
        var row=Read().Single(x=>x.OperationId==operationId&&x.Index==index);
        using var original=LockUnmoved(row);
        store.ExecuteWrite((c,t)=>{ReleaseUnmoved(c,t,row,confirmedWitness,"已确认结束未移动任务；保留原文件和完整历史。");return 0;});
    }

    private static IDisposable LockUnmoved(DurableTransferItem row)
    {
        if(Terminal(row)||row.Phase is not (DurableTransferPhase.Intent or DurableTransferPhase.Review))
            throw new IOException("任务仍在执行或已经结束；不能重新移动或释放。");
        var lease=LocalTransferFiles.LockVerifiedDestination(row.File with {Destination=row.File.Source,SameVolume=true},null);
        try
        {
            if(LocalTransferFiles.Exists(row.File.Destination))
                throw new IOException("目标处已有对象，保留两处与待办；不能自动删除或再次移动。");
            return lease;
        }
        catch {lease.Dispose();throw;}
    }
    private static void ReleaseUnmoved(SqliteConnection c,SqliteTransaction t,DurableTransferItem expected,string witness,string reason)
    {
        var row=ReadItems(c,t).Single(r=>r.OperationId==expected.OperationId&&r.Index==expected.Index);
        if(Terminal(row)||StateRequestFingerprint.Compute(row)!=witness || row.Phase is not (DurableTransferPhase.Intent or DurableTransferPhase.Review))
            throw new IOException("待办已变化，请刷新后重新确认。");
        if(LocalTransferFiles.Exists(row.File.Destination))throw new IOException("目标出现新对象，保留待办。");
        using var q=Command(c,t,"UPDATE content_transfer_items SET phase=6,error=$reason WHERE operation_id=$id AND item_index=$index",
            ("$reason",reason),("$id",row.OperationId.ToString()),("$index",row.Index));
        q.ExecuteNonQuery();
    }
}
