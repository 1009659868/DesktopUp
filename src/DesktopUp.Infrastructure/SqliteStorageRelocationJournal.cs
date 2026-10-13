using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal sealed record StorageLocationState(ManagedStorageSelection Selection, long RootSetVersion);
internal sealed record StoredRelocation(int FormatVersion, StorageRelocationRequest Request,
    StorageRelocationReads Reads, StorageRootInventory[] Roots, ContentCatalogSnapshot Catalog, string Fingerprint);
internal sealed record StorageRelocationRow(StorageRelocationPlan Plan, StorageRelocationPhase Phase,
    string? Closure, StorageRelocationTargets? Targets, StorageRelocationCommit? Commit);
internal enum StorageRelocationCheckpoint { IntentWritten, TargetsRecorded, IndexWritten, ReceiptWritten, Committed }

// Infrastructure-only; StorageRelocationFiles supplies the real copy/verification adapter. No UI can supply
// a boolean "copy succeeded" or a JSON receipt to authorize an index switch.
internal interface IStorageRelocationVerifier
{
    // Must recheck source and target content and retain native identity/content leases until
    // Dispose, including across the SQLite transaction. Test substitutes are not production proof.
    StorageRelocationProofLease Acquire(StorageRelocationPlan plan);
}
internal abstract class StorageRelocationProofLease : IDisposable
{
    public abstract StorageRelocationTargets Targets { get; }
    // Read while the verifier still holds its target leases. Persistence-only fixtures supply
    // their own identity adapter; the production verifier always uses native file identity.
    public virtual string ObjectIdentity(string path) => LocalTransferFiles.Identity(path);
    public abstract void Dispose();
}
internal sealed record StorageRelocationTargets(string PlanFingerprint, string ContentWitness, StorageBundleClaim Claim);

internal sealed partial class SqliteStorageRelocationJournal(SqliteStateStore store)
{
    internal const string MigrationSql = """
        CREATE TABLE storage_location_state(singleton_id INTEGER PRIMARY KEY CHECK(singleton_id=1),
            format_version INTEGER NOT NULL CHECK(format_version>=1), profile_identity TEXT NULL,
            version INTEGER NOT NULL CHECK(version>=0), root_set_version INTEGER NOT NULL CHECK(root_set_version>=0),
            active_root_id TEXT NULL REFERENCES managed_storage_roots(root_id)) STRICT;
        INSERT INTO storage_location_state VALUES(1,1,NULL,0,0,NULL);
        CREATE TABLE storage_relocations(operation_id TEXT PRIMARY KEY, fingerprint TEXT NOT NULL,
            body_json TEXT NOT NULL CHECK(json_valid(body_json)), phase INTEGER NOT NULL CHECK(phase BETWEEN 1 AND 6),
            targets_json TEXT NULL CHECK(targets_json IS NULL OR json_valid(targets_json)), closure TEXT NULL) STRICT;
        CREATE TABLE storage_relocation_commits(operation_id TEXT PRIMARY KEY REFERENCES storage_relocations(operation_id),
            fingerprint TEXT NOT NULL, active_root_id TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision>0)) STRICT;
        CREATE TRIGGER storage_roots_insert AFTER INSERT ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        CREATE TRIGGER storage_roots_update AFTER UPDATE ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        CREATE TRIGGER storage_roots_delete AFTER DELETE ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        CREATE TRIGGER storage_collections_insert AFTER INSERT ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        CREATE TRIGGER storage_collections_update AFTER UPDATE ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        CREATE TRIGGER storage_collections_delete AFTER DELETE ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
        """;
    private static readonly JsonSerializerOptions Json = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 64 };
    internal static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Decode<T>(string value)
    {
        if(value.Length>4*1024*1024) throw new InvalidDataException("Storage journal budget exceeded.");
        return JsonSerializer.Deserialize<T>(value,Json) ?? throw new InvalidDataException("Empty storage record.");
    }
    internal static void Verify(SqliteConnection c,SqliteTransaction? t)
    {
        using var q=Command(c,t,"SELECT format_version,profile_identity,version,root_set_version,active_root_id FROM storage_location_state WHERE singleton_id=1");
        using(var r=q.ExecuteReader()) if(!r.Read()||r.GetInt32(0)!=1||r.GetInt64(2)<0||r.GetInt64(3)<0)
            throw new InvalidDataException("Unknown/invalid storage selection; preserve database.");
        foreach(string sql in new[]{"SELECT operation_id,fingerprint,body_json,phase,targets_json,closure FROM storage_relocations LIMIT 0",
            "SELECT operation_id,fingerprint,active_root_id,revision FROM storage_relocation_commits LIMIT 0"})
        { using var shape=Command(c,t,sql);using(shape.ExecuteReader()){} }
        using var triggers=Command(c,t,"SELECT count(*) FROM sqlite_master WHERE type='trigger' AND name IN ('storage_roots_insert','storage_roots_update','storage_roots_delete','storage_collections_insert','storage_collections_update','storage_collections_delete')");
        if(Convert.ToInt64(triggers.ExecuteScalar())!=6)throw new InvalidDataException("Storage version guards missing.");
    }
    internal StorageLocationState ReadLocation()=>store.ExecuteRead(Location);
    private StorageLocationState Location(SqliteConnection c,SqliteTransaction t)
    {
        using var q=Command(c,t,"SELECT format_version,profile_identity,version,root_set_version,active_root_id FROM storage_location_state WHERE singleton_id=1");
        using var r=q.ExecuteReader();if(!r.Read())throw new InvalidDataException("Storage selection missing.");
        string profile=store.TransferProfileIdentity;
        if(r.GetInt32(0)!=1||!r.IsDBNull(1)&&r.GetString(1)!=profile)throw new IOException("Storage belongs to another physical profile; do not adopt it.");
        return new(new(profile,r.GetInt64(2),r.IsDBNull(4)?null:Guid.Parse(r.GetString(4))),r.GetInt64(3));
    }
    internal void Select(Guid rootId,StorageLocationState expected)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        var locations=new SqliteTransferJournal(store).StorageLocations().Where(x=>x.Root.Id==rootId).ToArray();
        if(locations.Length==0)throw new IOException("Registered storage not found.");
        using var leases=LockSources(locations);
        store.PreserveTransferGuard();
        store.ExecuteWrite((c,t)=>
        {
            RequireNoPending(c,t);var current=Location(c,t);
            if(current!=expected)throw new IOException("Storage selection changed; refresh.");
            if(current.Selection.ActiveRootId==rootId)return 0;
            using var q=Command(c,t,"UPDATE storage_location_state SET profile_identity=$p,active_root_id=$r,version=version+1 WHERE singleton_id=1",
                ("$p",current.Selection.ProfileIdentity),("$r",rootId.ToString()));q.ExecuteNonQuery();
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            SqliteStateCommandRepository.AdvanceHead(c,t,state.Revision,"storage-select-"+Guid.NewGuid().ToString("N"));return 0;
        });
    }

    internal StorageRelocationPlan Preview(string destination)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        destination=LocalTransferFiles.Normalize(destination);
        if(store.ProtectedTransferPath(destination,true))throw new IOException("Protected storage destination.");
        using var target=LocalTransferFiles.LockDirectory(destination);
        if(Directory.EnumerateFileSystemEntries(destination).Any())throw new IOException("Choose an empty destination; no files will be adopted or overwritten.");
        var root=new ManagedStorageRoot(Guid.NewGuid(),destination,LocalTransferFiles.Identity(destination),1,ManagedRootLocation.UserPersistent,true,true);
        return BuildPlan(new(Guid.NewGuid(),root,root.ObjectIdentity,StorageDestinationRelation.Disjoint,true,true,[]));
    }
    private StorageRelocationPlan BuildPlan(StorageRelocationRequest request)
    {
        var data=store.ExecuteRead((c,t)=>(State:SqliteStateCommandRepository.ReadSnapshot(c,t),Location:Location(c,t),Roots:Roots(c,t)));
        // Empty collections are supported. A registered root with NO collection binding has
        // no verifiable v1/adoption owner and is not silently treated as a usable empty root.
        if(data.Roots.Any(r=>r.CollectionIds.Count==0))throw new IOException("Storage root lacks a verifiable collection binding; preserve it.");
        var registered=data.Roots.SelectMany(r=>r.CollectionIds.Select(id=>new RegisteredManagedStorage(r.Root,data.State.ContentCatalog.Collections.Single(x=>x.Id==id)))).ToArray();
        using var leases=LockSources(registered);
        if(data.Roots.Any(r=>Overlaps(r.Root.Locator,request.Destination.Locator)))throw new IOException("Destination overlaps an existing root.");
        var roots=data.Roots.Select(r=>r with {OwnerProfileIdentity=store.TransferProfileIdentity,OwnershipWitness=OwnerWitness(r.Root.Locator)}).ToArray();
        var inventories=registered.Select(r=>
        {
            string path=r.Collection.StorageLocator!;
            var tree=LocalTransferFiles.Inspect(path,path).Objects;
            var aliases=data.State.ContentCatalog.Items.Where(i=>i.Source.ProviderId=="local-file-reference-v1"&&
                (string.Equals(i.Target.Locator,path,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(path,i.Target.Locator))).Select(i=>i.Id).ToArray();
            return new StorageCollectionInventory(r.Collection.Id,r.Root.Id,tree[0].Identity,LocalTransferFiles.TreeHash(tree),
                tree.LongCount(x=>!x.Directory),tree.Sum(x=>x.Length),aliases,true);
        }).ToArray();
        var reads=new StorageRelocationReads(store.TransferProfileIdentity,data.Location.Selection.Version,data.Location.RootSetVersion,
            data.State.ContentVersion,store.StorageRuntimeGeneration,data.State.Catalog.Posters.ToDictionary(x=>x.Poster.PosterId,x=>x.EntityVersion));
        return ManagedStorageRelocationPolicy.Prepare(request with {Inventories=inventories},reads,roots,data.State.ContentCatalog);
    }
    private Leases LockSources(IEnumerable<RegisteredManagedStorage> locations)
    {
        var leases=new Leases();
        try
        {
            foreach(var item in locations)
            {
                leases.Add(LocalTransferFiles.LockDirectory(item.Root.Locator));
                leases.Add(LocalTransferFiles.LockDirectory(item.Collection.StorageLocator!));
                ManagedStorageProvisioner.ValidateRegistration(store,item.Root,item.Collection);
                foreach(string marker in OwnerFiles(item.Root.Locator).Concat(OwnerFiles(item.Collection.StorageLocator!)))
                    leases.Add(new FileStream(marker,FileMode.Open,FileAccess.Read,FileShare.Read));
            }
            return leases;
        }
        catch {leases.Dispose();throw;}
    }
    private Leases LockPlanSources(StorageRelocationPlan plan)=>LockSources(plan.Roots.SelectMany(r=>r.CollectionIds.Select(id=>
        new RegisteredManagedStorage(r.Root,plan.Catalog.Collections.Single(x=>x.Id==id)))));
    internal IDisposable LockOwnedSources(StorageRelocationPlan plan)=>LockPlanSources(plan);
    internal void RequireCurrentPlan(StorageRelocationPlan plan)
    {if(BuildPlan(plan.Request).Fingerprint!=plan.Fingerprint)throw new IOException("Storage plan changed before file preparation.");}
    internal static IEnumerable<string> OwnerFiles(string path)=>new[]{".desktopup-storage-owner.json",".desktopup-collection-owner.json",
        ManagedStorageAdoption.RootMarker,ManagedStorageAdoption.ChildMarker,StorageBundleOwnership.RootMarker,StorageBundleOwnership.ChildMarker}
        .Select(n=>Path.Combine(path,n)).Where(File.Exists);
    internal static string OwnerWitness(string path)=>StateRequestFingerprint.Compute(OwnerFiles(path).Order(StringComparer.Ordinal)
        .Select(p=>new{File=Path.GetFileName(p),Hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p)))}).ToArray());
    private static StorageRootInventory[] Roots(SqliteConnection c,SqliteTransaction t)
    {
        var mapping=new Dictionary<Guid,List<Guid>>();
        using(var q=Command(c,t,"SELECT root_id,collection_id FROM managed_storage_collections ORDER BY collection_id"))
        using(var r=q.ExecuteReader())while(r.Read())
        {var id=Guid.Parse(r.GetString(0));if(!mapping.TryGetValue(id,out var ids))mapping[id]=ids=[];ids.Add(Guid.Parse(r.GetString(1)));}
        using var roots=Command(c,t,"SELECT body_json FROM managed_storage_roots ORDER BY root_id");
        using var rows=roots.ExecuteReader();var result=new List<StorageRootInventory>();
        while(rows.Read())
        {if(result.Count>=256)throw new IOException("Too many roots.");var root=Decode<ManagedStorageRoot>(rows.GetString(0));result.Add(new(root,"","",mapping.GetValueOrDefault(root.Id)??[]));}
        return result.ToArray();
    }
    internal static bool Overlaps(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(a,b)||LocalTransferFiles.Contains(b,a);
    internal static SqliteCommand Command(SqliteConnection c,SqliteTransaction? t,string sql,params (string Name,object? Value)[] values)
    {var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;foreach(var (n,v)in values)q.Parameters.AddWithValue(n,v??DBNull.Value);return q;}
    internal sealed class Leases:IDisposable
    {
        private readonly List<IDisposable> items=[];
        internal void Add(IDisposable lease)=>items.Add(lease);
        public void Dispose(){for(int i=items.Count-1;i>=0;i--)items[i].Dispose();items.Clear();}
    }
}
