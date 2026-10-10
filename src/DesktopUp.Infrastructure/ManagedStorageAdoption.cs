using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record StorageAdoptionBinding(int FormatVersion,string ProfileIdentity,ManagedStorageRoot Root,
    Guid CollectionId,string CollectionPath,string CollectionIdentity);
public sealed class StorageAdoptionPreview
{
    internal StorageAdoptionPreview(StorageAdoptionBinding binding,long revision,IReadOnlyList<LocalTransferObject> inventory)
    {
        Binding=binding;Revision=revision;InventoryWitness=StateRequestFingerprint.Compute(inventory);
        FileCount=inventory.Count(x=>!x.Directory);DirectoryCount=inventory.Count(x=>x.Directory)-1;
        Fingerprint=StateRequestFingerprint.Compute(new{Purpose="registered-directory-adoption-v1",binding,revision,InventoryWitness});
    }
    public StorageAdoptionBinding Binding {get;}
    public long Revision {get;}
    public string InventoryWitness {get;}
    public string Fingerprint {get;}
    public int FileCount {get;}
    public int DirectoryCount {get;}
}
public enum StorageAdoptionCheckpoint { IntentWritten,RootClaimed,ChildClaimed,Activated }

// Only migrates ownership metadata for an already registered legacy collection. No moves,
// recursive imports, marker replacement, arbitrary-directory adoption or startup activation.
public sealed class ManagedStorageAdoption(SqliteStateStore store)
{
    internal const string RootMarker=".desktopup-storage-adoption.json",ChildMarker=".desktopup-collection-adoption.json";
    private const string OldRootMarker=".desktopup-storage-owner.json",OldChildMarker=".desktopup-collection-owner.json";
    internal const string MigrationSql="""
        CREATE TABLE managed_storage_adoptions(
            fingerprint TEXT PRIMARY KEY,
            collection_id TEXT NOT NULL REFERENCES managed_storage_collections(collection_id),
            body_json TEXT NOT NULL CHECK(json_valid(body_json)),
            activated INTEGER NOT NULL CHECK(activated IN (0,1))) STRICT;
        CREATE UNIQUE INDEX one_active_storage_adoption ON managed_storage_adoptions(collection_id) WHERE activated=1;
        """;
    private sealed record Intent(int FormatVersion,StorageAdoptionBinding Binding,long Revision,string InventoryWitness,string Fingerprint);
    private static readonly JsonSerializerOptions Json=new()
    {UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=16};
    internal static void Verify(SqliteConnection c,SqliteTransaction? t)
    {
        using var q=c.CreateCommand();q.Transaction=t;
        q.CommandText="SELECT fingerprint,collection_id,body_json,activated FROM managed_storage_adoptions LIMIT 0";
        using(q.ExecuteReader()){}
    }
    public IReadOnlyList<RegisteredManagedStorage> Candidates()=>new SqliteTransferJournal(store).StorageLocations()
        .Where(x=>!LocalTransferFiles.Exists(Path.Combine(x.Root.Locator,OldRootMarker)) &&
            !store.ExecuteRead((c,t)=>ReadIntents(c,t,x.Collection.Id).Any(x=>x.Active))).ToArray();

    public StorageAdoptionPreview Preview(Guid collectionId)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        return Inspect(collectionId);
    }
    public void Confirm(StorageAdoptionPreview preview,string confirmation,Action<StorageAdoptionCheckpoint>? checkpoint=null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if(confirmation!=preview.Fingerprint)throw new IOException("请先确认旧目录的归属预览。");
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        using var root=LocalTransferFiles.LockDirectory(preview.Binding.Root.Locator);
        using var child=LocalTransferFiles.LockDirectory(preview.Binding.CollectionPath);
        var prior=store.ExecuteRead((c,t)=>ReadIntents(c,t,preview.Binding.CollectionId).SingleOrDefault(x=>x.Active));
        if(prior.Intent is not null)
        {
            if(prior.Intent.Fingerprint!=confirmation)throw new IOException("目录已被另一确认认领，请刷新。");
            ValidateActive(store,preview.Binding.Root,new(preview.Binding.CollectionId,"",ContentCollectionKind.Managed,
                preview.Binding.CollectionPath,ContentStorageOwnership.Managed));return;
        }
        if(Inspect(preview.Binding.CollectionId).Fingerprint!=confirmation)throw new IOException("目录内容、身份或配置已变化，请重新预览确认。");
        store.PreserveTransferGuard();
        var intent=new Intent(1,preview.Binding,preview.Revision,preview.InventoryWitness,confirmation);
        store.ExecuteWrite((c,t)=>
        {
            CheckCurrent(c,t,preview);
            using var count=Command(c,t,"SELECT count(*) FROM managed_storage_adoptions");
            if(Convert.ToInt64(count.ExecuteScalar())>=512)throw new IOException("目录认领记录达到预算，保留现场。");
            var existing=ReadIntents(c,t,preview.Binding.CollectionId).SingleOrDefault(x=>x.Intent.Fingerprint==confirmation);
            if(existing.Intent is not null)
            {if(existing.Intent!=intent)throw new IOException("认领意图冲突。");return 0;}
            using var add=Command(c,t,"INSERT INTO managed_storage_adoptions VALUES($fp,$id,$body,0)",
                ("$fp",confirmation),("$id",preview.Binding.CollectionId.ToString()),("$body",JsonSerializer.Serialize(intent,Json)));
            add.ExecuteNonQuery();return 0;
        });
        checkpoint?.Invoke(StorageAdoptionCheckpoint.IntentWritten);
        string rootPath=Path.Combine(preview.Binding.Root.Locator,RootMarker),childPath=Path.Combine(preview.Binding.CollectionPath,ChildMarker);
        WriteOrMatch(rootPath,preview.Binding);
        using var rootClaim=ReadLease(rootPath);
        checkpoint?.Invoke(StorageAdoptionCheckpoint.RootClaimed);
        WriteOrMatch(childPath,preview.Binding);
        using var childClaim=ReadLease(childPath);
        checkpoint?.Invoke(StorageAdoptionCheckpoint.ChildClaimed);
        // A crash here leaves only a prepared intent. Markers alone never authorize moves.
        if(Inspect(preview.Binding.CollectionId).Fingerprint!=confirmation)throw new IOException("认领期间目录或配置变化；凭证保留，需重新确认。文件未移动。");
        store.ExecuteWrite((c,t)=>
        {
            CheckCurrent(c,t,preview);
            using var activate=Command(c,t,"UPDATE managed_storage_adoptions SET activated=1 WHERE fingerprint=$fp AND activated=0",("$fp",confirmation));
            if(activate.ExecuteNonQuery()!=1)throw new IOException("认领意图不再可用。");return 0;
        });
        checkpoint?.Invoke(StorageAdoptionCheckpoint.Activated);
    }
    private StorageAdoptionPreview Inspect(Guid id)
    {
        var data=store.ExecuteRead((c,t)=>
        {
            if(ReadIntents(c,t,id).Any(x=>x.Active))throw new IOException("此目录已完成显式认领，无需重复迁移。");
            var binding=ReadBinding(c,t,id,store.TransferProfileIdentity);
            CheckPending(c,t,binding);
            return(binding,revision:SqliteStateCommandRepository.ReadSnapshot(c,t).Revision);
        });
        var b=data.binding;
        using var root=LocalTransferFiles.LockDirectory(b.Root.Locator);
        using var child=LocalTransferFiles.LockDirectory(b.CollectionPath);
        ValidatePhysical(store,b);
        CheckMarkers(b,required:false);
        var inventory=LocalTransferFiles.Inspect(b.CollectionPath,b.CollectionPath).Objects
            .Where(x=>x.RelativePath!=ChildMarker).ToArray(); // only this exact, validated ownership file is metadata
        return new(b,data.revision,inventory);
    }
    private static StorageAdoptionBinding ReadBinding(SqliteConnection c,SqliteTransaction t,Guid id,string profile)
    {
        var collection=SqliteStateCommandRepository.ReadSnapshot(c,t).ContentCatalog.Collections.SingleOrDefault(x=>x.Id==id);
        if(collection is null||collection.Kind!=ContentCollectionKind.Managed||collection.Ownership!=ContentStorageOwnership.Managed)
            throw new IOException("只可认领当前配置已经登记的受管集合。");
        using var q=Command(c,t,"SELECT r.body_json,m.object_identity,(SELECT count(*) FROM managed_storage_collections b WHERE b.root_id=m.root_id) FROM managed_storage_collections m JOIN managed_storage_roots r ON r.root_id=m.root_id WHERE m.collection_id=$id",("$id",id.ToString()));
        using var r=q.ExecuteReader();
        if(!r.Read()||r.GetInt64(2)!=1)throw new IOException("旧目录未登记或一根多集合；本批不猜测归属。");
        var root=Decode<ManagedStorageRoot>(r.GetString(0));
        return new(1,profile,root,id,collection.StorageLocator!,r.GetString(1));
    }
    private static void CheckPending(SqliteConnection c,SqliteTransaction t,StorageAdoptionBinding b)
    {
        using var closed=Command(c,t,"SELECT count(*) FROM storage_provision_closures WHERE collection_id=$id",("$id",b.CollectionId.ToString()));
        if(Convert.ToInt64(closed.ExecuteScalar())!=0)throw new IOException("已停止的创建不属于本批旧登记目录认领。");
        if(SqliteTransferJournal.ReadItems(c,t).Any(x=>!x.IsFinished &&
            (Overlap(b.Root.Locator,x.File.Source)||Overlap(b.Root.Locator,x.File.Destination))))
            throw new IOException("此目录仍有未结束文件操作，请先处理或明确停止重试，再确认归属。");
    }
    private static bool Overlap(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(a,b)||LocalTransferFiles.Contains(b,a);
    private void CheckCurrent(SqliteConnection c,SqliteTransaction t,StorageAdoptionPreview preview)
    {
        if(SqliteStateCommandRepository.ReadSnapshot(c,t).Revision!=preview.Revision||
            ReadBinding(c,t,preview.Binding.CollectionId,store.TransferProfileIdentity)!=preview.Binding)
            throw new IOException("当前配置或登记关系已变化，请重新确认。");
        CheckPending(c,t,preview.Binding);
    }
    internal static void ValidateActive(SqliteStateStore store,ManagedStorageRoot root,Collection collection)
    {
        var b=store.ExecuteRead((c,t)=>
        {
            var row=ReadIntents(c,t,collection.Id).SingleOrDefault(x=>x.Active);
            if(row.Intent is null)throw new IOException("旧目录缺少已确认的归属记录；请在收纳存储目录中显式认领。");
            var binding=ReadBinding(c,t,collection.Id,store.TransferProfileIdentity);
            if(row.Intent.Binding!=binding||binding.Root!=root||binding.CollectionPath!=collection.StorageLocator)
                throw new IOException("目录认领不属于当前配置或登记关系已变化。");
            return binding;
        });
        ValidatePhysical(store,b);CheckMarkers(b,required:true);
    }
    private static void ValidatePhysical(SqliteStateStore store,StorageAdoptionBinding b)
    {
        if(b.FormatVersion!=1||b.ProfileIdentity!=store.TransferProfileIdentity||ManagedStoragePolicy.Evaluate(b.Root)!=ManagedStorageDecision.Ready||
            store.ProtectedTransferPath(b.Root.Locator,true)||LocalTransferFiles.Normalize(b.Root.Locator)!=b.Root.Locator||
            LocalTransferFiles.Normalize(b.CollectionPath)!=b.CollectionPath||Path.GetDirectoryName(b.CollectionPath)!=b.Root.Locator||
            LocalTransferFiles.Identity(b.Root.Locator)!=b.Root.ObjectIdentity||LocalTransferFiles.Identity(b.CollectionPath)!=b.CollectionIdentity)
            throw new IOException("根目录、子目录或配置文件身份不同，不能认领。文件保持原位。");
    }
    private static void CheckMarkers(StorageAdoptionBinding b,bool required)
    {
        if(LocalTransferFiles.Exists(Path.Combine(b.Root.Locator,OldRootMarker))||LocalTransferFiles.Exists(Path.Combine(b.CollectionPath,OldChildMarker)))
            throw new IOException("已有其他创建声明，不能用旧目录认领覆盖；请走原创建记录或另选目录。");
        foreach(string path in new[]{Path.Combine(b.Root.Locator,RootMarker),Path.Combine(b.CollectionPath,ChildMarker)})
            if(LocalTransferFiles.Exists(path))
            {if(ReadMarker(path)!=b)throw new IOException("目录声明属于其他配置或未知版本；原文保留。");}
            else if(required)throw new IOException("认领凭证丢失，不能仅凭路径继续移动文件。");
    }
    private static List<(Intent Intent,bool Active)> ReadIntents(SqliteConnection c,SqliteTransaction t,Guid id)
    {
        using var q=Command(c,t,"SELECT fingerprint,body_json,activated FROM managed_storage_adoptions WHERE collection_id=$id",("$id",id.ToString()));
        using var r=q.ExecuteReader();var list=new List<(Intent,bool)>();
        while(r.Read())
        {
            if(list.Count>=512)throw new IOException("目录认领记录超限。");
            var row=Decode<Intent>(r.GetString(1));
            if(row.FormatVersion!=1||row.Binding.FormatVersion!=1||row.Binding.CollectionId!=id||row.Revision<0||
                row.Fingerprint!=r.GetString(0)||row.Fingerprint.Length!=64||row.InventoryWitness.Length!=64||
                StateRequestFingerprint.Compute(new{Purpose="registered-directory-adoption-v1",binding=row.Binding,revision=row.Revision,row.InventoryWitness})!=row.Fingerprint)
                throw new IOException("目录认领记录不完整或版本未知；保留记录。");
            list.Add((row,r.GetInt64(2)==1));
        }
        return list;
    }
    private static T Decode<T>(string body)
    {
        if(body.Length>32768)throw new IOException("目录凭证超过预算。");
        return JsonSerializer.Deserialize<T>(body,Json)??throw new IOException("空目录凭证。");
    }
    private static FileStream ReadLease(string path)
    {_=LocalTransferFiles.Identity(path);return new(path,FileMode.Open,FileAccess.Read,FileShare.Read);}
    private static StorageAdoptionBinding ReadMarker(string path)
    {
        using var s=ReadLease(path);if(s.Length>32768)throw new IOException("目录凭证超过预算。");
        return JsonSerializer.Deserialize<StorageAdoptionBinding>(s,Json)??throw new IOException("空目录凭证。");
    }
    private static void WriteOrMatch(string path,StorageAdoptionBinding binding)
    {
        if(LocalTransferFiles.Exists(path))
        {if(ReadMarker(path)!=binding)throw new IOException("目录凭证冲突，不覆盖。");return;}
        using var s=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
        s.Write(JsonSerializer.SerializeToUtf8Bytes(binding,Json));s.Flush(true);
    }
    private static SqliteCommand Command(SqliteConnection c,SqliteTransaction t,string sql,params (string Name,object Value)[] args)
    {var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;foreach(var a in args)q.Parameters.AddWithValue(a.Name,a.Value);return q;}
}
