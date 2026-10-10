using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// A closure is NOT a physical result or an index receipt. The original phase, paths,
// file receipt and error remain immutable evidence; only future scheduling is stopped.
public sealed record TransferClosure(int FormatVersion,string Fingerprint,string PreviousWitness,long StateRevision);
public sealed class TransferStopPreview
{
    internal TransferStopPreview(DurableTransferItem item,long revision)
    {
        Item=item;Revision=revision;
        Fingerprint=StateRequestFingerprint.Compute(new {Purpose="preserve-files-stop-v1",item,revision});
    }
    public DurableTransferItem Item {get;}
    public long Revision {get;}
    public string Fingerprint {get;}
}

public sealed partial class SqliteTransferJournal
{
    internal const string ClosureMigrationSql="""
        CREATE TABLE content_transfer_closures(operation_id TEXT NOT NULL,item_index INTEGER NOT NULL,
            body_json TEXT NOT NULL CHECK(json_valid(body_json)), PRIMARY KEY(operation_id,item_index),
            FOREIGN KEY(operation_id,item_index) REFERENCES content_transfer_items(operation_id,item_index)) STRICT;
        CREATE TABLE storage_provision_closures(collection_id TEXT PRIMARY KEY,
            body_json TEXT NOT NULL CHECK(json_valid(body_json))) STRICT;
        """;
    internal static void VerifyClosures(SqliteConnection c,SqliteTransaction? t)
    {
        foreach(string sql in new[]{"SELECT operation_id,item_index,body_json FROM content_transfer_closures LIMIT 0;",
            "SELECT collection_id,body_json FROM storage_provision_closures LIMIT 0;"})
        {using var q=Command(c,t,sql);using(q.ExecuteReader()){} }
    }
    public TransferStopPreview PreviewStop(Guid operationId,int index)
    {
        using var executor=AcquireExecutor();
        return store.ExecuteRead((c,t)=>
        {
            var row=ReadItems(c,t).Single(x=>x.OperationId==operationId&&x.Index==index);
            if(Terminal(row))throw new IOException("该收纳记录已结束，请刷新。");
            return new TransferStopPreview(row,SqliteStateCommandRepository.ReadSnapshot(c,t).Revision);
        });
    }
    public void ConfirmStop(TransferStopPreview preview,string confirmation)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if(preview.Fingerprint!=confirmation)throw new IOException("请明确确认保留文件并停止重试。");
        using var executor=AcquireExecutor();
        store.ExecuteWrite((c,t)=>
        {
            var row=ReadItems(c,t).Single(x=>x.OperationId==preview.Item.OperationId&&x.Index==preview.Item.Index);
            if(row.Closure is {} closed)
            {
                if(closed.Fingerprint!=confirmation)throw new IOException("该记录已由其他确认结束，请刷新。");
                return 0; // Exact replay, no filesystem access or new history.
            }
            var fresh=new TransferStopPreview(row,SqliteStateCommandRepository.ReadSnapshot(c,t).Revision);
            if(Terminal(row)||fresh.Fingerprint!=confirmation)throw new IOException("配置或收纳记录已变化，请重新确认。");
            var closure=new TransferClosure(1,confirmation,StateRequestFingerprint.Compute(row),preview.Revision);
            using var q=Command(c,t,"INSERT INTO content_transfer_closures VALUES($id,$i,$body)",
                ("$id",row.OperationId.ToString()),("$i",row.Index),("$body",Encode(closure)));
            q.ExecuteNonQuery();return 0;
        });
    }
    private static IReadOnlyDictionary<(Guid,int),TransferClosure> ReadClosures(SqliteConnection c,SqliteTransaction t)
    {
        using var q=Command(c,t,"SELECT operation_id,item_index,body_json FROM content_transfer_closures");
        var result=new Dictionary<(Guid,int),TransferClosure>();
        using var r=q.ExecuteReader();while(r.Read())
        {
            var entry=Decode<TransferClosure>(r.GetString(2));
            if(result.Count>=10000 || entry.FormatVersion!=1 || entry.Fingerprint.Length!=64 || entry.PreviousWitness.Length!=64 || entry.StateRevision<0)
                throw new InvalidDataException("Unknown or invalid closure; preserve journal and refuse execution.");
            result.Add((Guid.Parse(r.GetString(0)),r.GetInt32(1)),entry);
        }
        return result;
    }
}
