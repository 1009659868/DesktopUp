using System.Text.Json;
using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record StorageProvisionClosure(int FormatVersion,StorageProvisionIntent Intent,string Fingerprint,long StateRevision);
public sealed class StorageProvisionStopPreview
{
    internal StorageProvisionStopPreview(StorageProvisionIntent intent,long revision)
    {Intent=intent;Revision=revision;Fingerprint=StateRequestFingerprint.Compute(new{Purpose="preserve-directory-stop-v1",intent,revision});}
    public StorageProvisionIntent Intent {get;}
    public long Revision {get;}
    public string Fingerprint {get;}
    public string CollectionPath=>Path.Combine(Intent.Root.Locator,"collection-"+Intent.CollectionId.ToString("N"));
}

public sealed partial class ManagedStorageProvisioner
{
    // This is a safe exit from an unproven mkdir/claim gap, not an ownership adoption.
    // Keep intent/root/child markers byte-for-byte. The same root stays reserved as history;
    // setting up another root requires a fresh preview and explicit confirmation.
    public IReadOnlyList<StorageProvisionClosure> Stopped()=>store.ExecuteRead(ReadStopped);
    private static IReadOnlyList<StorageProvisionClosure> ReadStopped(SqliteConnection c,SqliteTransaction t)
    {
        using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT collection_id,body_json FROM storage_provision_closures";
        var results=new List<StorageProvisionClosure>();using var r=q.ExecuteReader();
        while(r.Read())
        {
            string body=r.GetString(1);if(body.Length>32768||results.Count>=256)throw new IOException("目录结束记录超限，保留原文。");
            var row=JsonSerializer.Deserialize<StorageProvisionClosure>(body,Json)??throw new IOException("空目录结束记录。");
            if(row.FormatVersion!=1||row.Intent.FormatVersion!=1||row.Intent.CollectionId.ToString()!=r.GetString(0)||row.Fingerprint.Length!=64||row.StateRevision<0)
                throw new IOException("未知目录结束记录，不能自动继续登记。");
            results.Add(row);
        }
        return results;
    }
    private void EnsureNotStopped(StorageProvisionIntent intent)
    {
        if(Stopped().Any(x=>x.Intent.CollectionId==intent.CollectionId))
            throw new IOException("此目录创建已停止；已有文件和所有权记录保留，不能自动重新认领。请另选持久目录。");
    }
    public StorageProvisionStopPreview PreviewStop(Guid collectionId)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        var intent=Pending().Single(x=>x.CollectionId==collectionId);
        CheckStopIntent(intent);
        return store.ExecuteRead((c,t)=>
        {
            CheckUnregistered(c,t,intent);
            return new StorageProvisionStopPreview(intent,SqliteStateCommandRepository.ReadSnapshot(c,t).Revision);
        });
    }
    public void ConfirmStop(StorageProvisionStopPreview preview,string confirmation)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if(preview.Fingerprint!=confirmation)throw new IOException("请明确确认保留目录并停止创建。");
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        store.ExecuteWrite((c,t)=>
        {
            var old=ReadStopped(c,t).SingleOrDefault(x=>x.Intent.CollectionId==preview.Intent.CollectionId);
            if(old is not null)
            {
                if(old.Fingerprint!=confirmation)throw new IOException("此记录已由其他确认结束，请刷新。");
                return 0;
            }
            var intent=Read<StorageProvisionIntent>(IntentFile(preview.Intent.Root.Locator));
            CheckStopIntent(intent);CheckUnregistered(c,t,intent);
            var fresh=new StorageProvisionStopPreview(intent,SqliteStateCommandRepository.ReadSnapshot(c,t).Revision);
            if(fresh.Fingerprint!=confirmation)throw new IOException("目录记录或配置已变化，请重新确认。");
            var closure=new StorageProvisionClosure(1,intent,confirmation,preview.Revision);
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT INTO storage_provision_closures VALUES($id,$body)";
            q.Parameters.AddWithValue("$id",intent.CollectionId.ToString());q.Parameters.AddWithValue("$body",JsonSerializer.Serialize(closure,Json));
            q.ExecuteNonQuery();return 0;
        });
    }
    private void CheckStopIntent(StorageProvisionIntent intent)
    {
        if(intent.FormatVersion!=1||intent.ProfileIdentity!=store.TransferProfileIdentity||intent.CollectionId==Guid.Empty||
            Read<StorageProvisionIntent>(IntentFile(intent.Root.Locator))!=intent)
            throw new IOException("不是当前配置的完整创建意图；保留记录，不推测归属。");
    }
    private static void CheckUnregistered(SqliteConnection c,SqliteTransaction t,StorageProvisionIntent intent)
    {
        using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT count(*) FROM managed_storage_collections WHERE collection_id=$id OR root_id=$root";
        q.Parameters.AddWithValue("$id",intent.CollectionId.ToString());q.Parameters.AddWithValue("$root",intent.Root.Id.ToString());
        if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new IOException("目录已经登记；不能用停止创建来解除现有收纳存储。");
    }
}
