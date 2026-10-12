using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal sealed partial class SqliteStorageRelocationJournal
{
    internal void Begin(StorageRelocationPlan plan,string confirmation,Action<StorageRelocationCheckpoint>? checkpoint=null)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        if(confirmation!=plan.Fingerprint)throw new IOException("Confirm this exact storage migration.");
        if(Read().SingleOrDefault(x=>x.Plan.Request.OperationId==plan.Request.OperationId) is { } old)
        {if(old.Plan.Fingerprint!=plan.Fingerprint||old.Closure is not null)throw new IOException("Operation already used/closed.");return;}
        using var sources=LockPlanSources(plan);
        using var target=LocalTransferFiles.LockDirectory(plan.Request.Destination.Locator);
        if(LocalTransferFiles.Identity(plan.Request.Destination.Locator)!=plan.Request.Destination.ObjectIdentity||
            Directory.EnumerateFileSystemEntries(plan.Request.Destination.Locator).Any())throw new IOException("Destination changed.");
        if(BuildPlan(plan.Request).Fingerprint!=plan.Fingerprint)throw new IOException("Storage preview changed.");
        store.PreserveTransferGuard();
        store.ExecuteWrite((c,t)=>
        {
            RequireNoPending(c,t);CheckReads(c,t,plan);
            if(SqliteTransferJournal.ReadItems(c,t).Any(x=>!x.IsFinished&&plan.Roots.Any(r=>Overlaps(r.Root.Locator,x.File.Source)||Overlaps(r.Root.Locator,x.File.Destination))))
                throw new IOException("A transfer still owns a migration path.");
            using(var opens=Command(c,t,"SELECT request_json FROM content_open_requests WHERE completed=0"))
            using(var r=opens.ExecuteReader())while(r.Read())
                if(plan.Roots.Any(root=>Overlaps(root.Root.Locator,Decode<ContentOpenRequest>(r.GetString(0)).Target.Locator)))
                    throw new IOException("An open request still owns an affected path.");
            using(var count=Command(c,t,"SELECT count(*) FROM storage_relocations"))
                if(Convert.ToInt64(count.ExecuteScalar())>=4096)throw new IOException("Migration history capacity reached.");
            var body=Encode(new StoredRelocation(1,plan.Request,plan.Reads,plan.Roots.ToArray(),plan.Catalog,plan.Fingerprint));
            if(body.Length>4*1024*1024)throw new IOException("Migration journal budget exceeded.");
            using(var owner=Command(c,t,"UPDATE storage_location_state SET profile_identity=$p WHERE singleton_id=1",("$p",store.TransferProfileIdentity)))owner.ExecuteNonQuery();
            using var q=Command(c,t,"INSERT INTO storage_relocations VALUES($id,$fp,$body,1,NULL,NULL)",
                ("$id",plan.Request.OperationId.ToString()),("$fp",plan.Fingerprint),("$body",body));q.ExecuteNonQuery();return 0;
        });
        checkpoint?.Invoke(StorageRelocationCheckpoint.IntentWritten);
    }
    internal IReadOnlyList<StorageRelocationRow> Read()=>store.ExecuteRead((c,t)=>{_=Location(c,t);return ReadRows(c,t);});
    internal StorageRelocationRow Get(Guid id)=>Read().Single(x=>x.Plan.Request.OperationId==id);

    internal void RecordTargets(Guid id,IStorageRelocationVerifier verifier,Action<StorageRelocationCheckpoint>? checkpoint=null)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();var row=Get(id);
        if(row.Commit is not null||row.Closure is not null||row.Phase is not (StorageRelocationPhase.IntentDurable or StorageRelocationPhase.Copying or StorageRelocationPhase.TargetsVerified))
            throw new IOException("Migration cannot record targets in this state.");
        using var proof=verifier.Acquire(row.Plan);ValidateTargets(row.Plan,proof.Targets);
        using var sources=LockPlanSources(row.Plan);
        using var owners=StorageBundleOwnership.LockPrepared(store,row.Plan,proof.Targets.Claim);
        if(BuildPlan(row.Plan.Request).Fingerprint!=row.Plan.Fingerprint)throw new IOException("Sources changed during preparation.");
        store.ExecuteWrite((c,t)=>
        {
            CheckReads(c,t,row.Plan);var current=ReadRows(c,t).Single(x=>x.Plan.Request.OperationId==id);
            if(current.Closure is not null||current.Commit is not null)throw new IOException("Migration already finished.");
            using var q=Command(c,t,"UPDATE storage_relocations SET phase=3,targets_json=$p WHERE operation_id=$id",
                ("$p",Encode(proof.Targets)),("$id",id.ToString()));q.ExecuteNonQuery();return 0;
        });
        checkpoint?.Invoke(StorageRelocationCheckpoint.TargetsRecorded);
    }
    internal StorageRelocationCommit Commit(Guid id,IStorageRelocationVerifier verifier,Action<StorageRelocationCheckpoint>? checkpoint=null)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();var row=Get(id);
        if(row.Commit is { } receipt)return receipt; // Already published; never re-run the file phase.
        if(row.Phase!=StorageRelocationPhase.TargetsVerified||row.Closure is not null||row.Targets is null)throw new IOException("Verified targets required.");
        using var proof=verifier.Acquire(row.Plan);ValidateTargets(row.Plan,proof.Targets);
        using var sources=LockPlanSources(row.Plan);
        if(Encode(proof.Targets)!=Encode(row.Targets))throw new IOException("Target evidence changed since verification.");
        using var owners=StorageBundleOwnership.LockPrepared(store,row.Plan,proof.Targets.Claim);
        if(BuildPlan(row.Plan.Request).Fingerprint!=row.Plan.Fingerprint)throw new IOException("Migration sources/read set changed.");
        var result=store.ExecuteWrite((c,t)=>
        {
            CheckReads(c,t,row.Plan);var current=ReadRows(c,t).Single(x=>x.Plan.Request.OperationId==id);
            if(current.Commit is not null)return current.Commit;
            if(current.Closure is not null||current.Phase!=StorageRelocationPhase.TargetsVerified||Encode(current.Targets)!=Encode(proof.Targets))throw new IOException("Migration state changed.");
            var before=SqliteStateCommandRepository.ReadSnapshot(c,t);long revision=checked(before.Revision+1);
            var next=Project(before.ContentCatalog,row.Plan,proof.Targets.Claim);
            using(var root=Command(c,t,"INSERT INTO managed_storage_roots VALUES($id,$body)",("$id",row.Plan.Request.Destination.Id.ToString()),("$body",Encode(row.Plan.Request.Destination))))root.ExecuteNonQuery();
            foreach(var binding in proof.Targets.Claim.Collections)
            {
                using var q=Command(c,t,"UPDATE managed_storage_collections SET root_id=$root,object_identity=$identity WHERE collection_id=$id",
                    ("$root",row.Plan.Request.Destination.Id.ToString()),("$identity",binding.ObjectIdentity),("$id",binding.CollectionId.ToString()));
                if(q.ExecuteNonQuery()!=1)throw new IOException("Collection binding disappeared.");
            }
            foreach(string poster in row.Plan.AffectedPosterIds)
                SqlitePosterRepository.UpdatePoster(c,t,before.Catalog.Posters.Single(x=>x.Poster.PosterId==poster).Poster,revision,"storage-relocate-"+id.ToString("N"),false);
            SqliteContentRepository.Apply(c,t,before,new(){ContentCatalog=next},revision);
            using(var select=Command(c,t,"UPDATE storage_location_state SET active_root_id=$root,version=version+1 WHERE singleton_id=1",("$root",row.Plan.Request.Destination.Id.ToString())))select.ExecuteNonQuery();
            foreach(var oldRoot in row.Plan.Roots)
            {using var q=Command(c,t,"DELETE FROM managed_storage_roots WHERE root_id=$id",("$id",oldRoot.Root.Id.ToString()));q.ExecuteNonQuery();}
            SqliteStateCommandRepository.AdvanceHead(c,t,before.Revision,"storage-relocate-"+id.ToString("N"));
            checkpoint?.Invoke(StorageRelocationCheckpoint.IndexWritten);
            var committed=new StorageRelocationCommit(id,row.Plan.Fingerprint,row.Plan.Request.Destination.Id,revision);
            using(var q=Command(c,t,"INSERT INTO storage_relocation_commits VALUES($id,$fp,$root,$rev)",("$id",id.ToString()),("$fp",committed.PlanFingerprint),("$root",committed.ActiveRootId.ToString()),("$rev",revision)))q.ExecuteNonQuery();
            checkpoint?.Invoke(StorageRelocationCheckpoint.ReceiptWritten);
            using(var q=Command(c,t,"UPDATE storage_relocations SET phase=4 WHERE operation_id=$id",("$id",id.ToString())))q.ExecuteNonQuery();
            return committed;
        });
        checkpoint?.Invoke(StorageRelocationCheckpoint.Committed);return result;
    }
    // No file cleanup here. D2-03 must reconcile its own temporary copies. Closing an uncommitted
    // intent releases content editing but never grants permission to delete a path.
    internal void Cancel(Guid id)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        store.ExecuteWrite((c,t)=>
        {
            _=Location(c,t);var row=ReadRows(c,t).Single(x=>x.Plan.Request.OperationId==id);
            if(row.Commit is not null||row.Closure is not null)return 0;
            using var q=Command(c,t,"UPDATE storage_relocations SET closure='cancelled-preserve-files' WHERE operation_id=$id",("$id",id.ToString()));q.ExecuteNonQuery();return 0;
        });
    }
    private void CheckReads(SqliteConnection c,SqliteTransaction t,StorageRelocationPlan plan)
    {
        var location=Location(c,t);var state=SqliteStateCommandRepository.ReadSnapshot(c,t);var r=plan.Reads;
        if(r.ProfileIdentity!=store.TransferProfileIdentity||r.RuntimeGeneration!=store.StorageRuntimeGeneration||
            r.SelectionVersion!=location.Selection.Version||r.RootSetVersion!=location.RootSetVersion||r.ContentVersion!=state.ContentVersion||
            r.PosterVersions.Any(p=>state.Catalog.Posters.All(x=>x.Poster.PosterId!=p.Key||x.EntityVersion!=p.Value))||
            ContentCatalogCodec.Write(state.ContentCatalog)!=ContentCatalogCodec.Write(plan.Catalog)||
            Encode(Roots(c,t).Select(x=>new{x.Root,x.CollectionIds}).ToArray())!=Encode(plan.Roots.Select(x=>new{x.Root,x.CollectionIds}).ToArray()))
            throw new IOException("Storage migration read set is stale/foreign; preserve files and refresh.");
    }
    private static ContentCatalogSnapshot Project(ContentCatalogSnapshot before,StorageRelocationPlan plan,StorageBundleClaim claim)
    {
        var paths=claim.Collections.ToDictionary(x=>x.CollectionId,x=>x.Locator);
        var items=before.Items.ToDictionary(x=>x.Id);
        foreach(var inventory in plan.Request.Inventories)
        {
            string from=before.Collections.Single(x=>x.Id==inventory.CollectionId).StorageLocator!,to=paths[inventory.CollectionId];
            foreach(Guid id in inventory.AffectedContentIds)
            {
                var item=items[id];if(item.Source.ProviderId!="local-file-reference-v1"||!Overlaps(from,item.Target.Locator)||LocalTransferFiles.Contains(item.Target.Locator,from))
                    throw new IOException("Only resolved local descendants can be relocated.");
                string target=LocalTransferFiles.Normalize(Path.Combine(to,Path.GetRelativePath(from,item.Target.Locator)));
                items[id]=item with {Source=item.Source with {Key=target},Target=item.Target with {Locator=target},Version=checked(item.Version+1)};
            }
        }
        return ContentCatalogPolicy.Freeze(before with {Items=before.Items.Select(x=>items[x.Id]).ToArray(),Collections=before.Collections.Select(x=>paths.TryGetValue(x.Id,out var path)?x with {StorageLocator=path,Version=checked(x.Version+1)}:x).ToArray()});
    }
    private static void ValidateTargets(StorageRelocationPlan plan,StorageRelocationTargets targets)
    {
        if(targets.PlanFingerprint!=plan.Fingerprint||targets.ContentWitness.Length is < 1 or > 32768||targets.ContentWitness.Any(char.IsControl))throw new IOException("Unbound target proof.");
        StorageBundleOwnership.ValidateShape(plan,targets.Claim);
    }
    private static IReadOnlyList<StorageRelocationRow> ReadRows(SqliteConnection c,SqliteTransaction t)
    {
        using var q=Command(c,t,"SELECT r.operation_id,r.fingerprint,r.body_json,r.phase,r.targets_json,r.closure,c.fingerprint,c.active_root_id,c.revision,(SELECT committed_revision FROM state_operations WHERE operation_id='storage-relocate-'||replace(r.operation_id,'-','')) FROM storage_relocations r LEFT JOIN storage_relocation_commits c ON c.operation_id=r.operation_id ORDER BY r.operation_id LIMIT 4097");
        using var rows=q.ExecuteReader();var result=new List<StorageRelocationRow>();
        while(rows.Read())
        {
            if(result.Count>=4096)throw new InvalidDataException("Storage journal capacity exceeded.");
            var saved=Decode<StoredRelocation>(rows.GetString(2));
            if(saved.FormatVersion!=1)throw new InvalidDataException("Unknown storage intent; preserve it.");
            var plan=ManagedStorageRelocationPolicy.Prepare(saved.Request,saved.Reads,saved.Roots,saved.Catalog);
            if(plan.Fingerprint!=saved.Fingerprint||plan.Fingerprint!=rows.GetString(1)||plan.Request.OperationId.ToString()!=rows.GetString(0))throw new InvalidDataException("Storage journal fingerprint mismatch.");
            var phase=(StorageRelocationPhase)rows.GetInt32(3);if(!Enum.IsDefined(phase))throw new InvalidDataException("Unknown storage phase.");
            var proof=rows.IsDBNull(4)?null:Decode<StorageRelocationTargets>(rows.GetString(4));if(proof is not null)ValidateTargets(plan,proof);
            var commit=rows.IsDBNull(6)?null:new StorageRelocationCommit(plan.Request.OperationId,rows.GetString(6),Guid.Parse(rows.GetString(7)),rows.GetInt64(8));
            if(commit is not null&&(commit.PlanFingerprint!=plan.Fingerprint||commit.ActiveRootId!=plan.Request.Destination.Id||commit.CommittedRevision<1||rows.IsDBNull(9)||rows.GetInt64(9)!=commit.CommittedRevision)||
                commit is null&&!rows.IsDBNull(9)||
                phase>=StorageRelocationPhase.IndexCommitted&&commit is null||commit is not null&&proof is null)
                throw new InvalidDataException("Storage commit receipt missing or mismatched.");
            string? closure=rows.IsDBNull(5)?null:rows.GetString(5);
            if(closure is not (null or "cancelled-preserve-files")||commit is not null&&closure is not null)throw new InvalidDataException("Unknown/contradictory migration closure.");
            result.Add(new(plan,phase,closure,proof,commit));
        }
        return result;
    }
    internal static void RequireNoPending(SqliteConnection c,SqliteTransaction t)
    {if(ReadRows(c,t).Any(x=>x.Closure is null&&x.Commit is null))throw new IOException("Storage relocation is pending.");}
    internal static bool AllowsMutation(SqliteConnection c,SqliteTransaction t,StateMutationPlan plan)
    {
        var pending=ReadRows(c,t).Where(x=>x.Closure is null&&x.Commit is null).ToArray();
        if(pending.Length==0)return true;
        if(plan.ContentCatalog is not null)return false;
        var ids=pending.SelectMany(x=>x.Plan.AffectedPosterIds).ToHashSet();
        return !plan.Updates.Select(x=>x.PosterId).Concat(plan.Removes).Any(ids.Contains);
    }
    internal static bool AllowsOpen(SqliteConnection c,SqliteTransaction t,ContentOpenRequest request)=>
        !ReadRows(c,t).Any(x=>x.Closure is null&&x.Commit is null&&x.Plan.Roots.Any(r=>Overlaps(r.Root.Locator,request.Target.Locator)));
    internal static void RequirePathsAvailable(SqliteConnection c,SqliteTransaction t,IEnumerable<string> paths)
    {
        var owned=ReadRows(c,t).Where(x=>x.Closure is null&&x.Phase!=StorageRelocationPhase.Completed)
            .SelectMany(x=>x.Commit is null?x.Plan.Roots.Select(r=>r.Root.Locator).Append(x.Plan.Request.Destination.Locator):x.Plan.Roots.Select(r=>r.Root.Locator)).ToArray();
        if(paths.Any(p=>owned.Any(r=>Overlaps(p,r))))throw new IOException("Migration owns these paths.");
    }
}
