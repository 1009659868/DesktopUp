using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopUp.Infrastructure;

public enum DurableTransferPhase { Intent, Executing, CopyVerified, FileVerified, IndexCommitted, NoOp, Cancelled, Review }
public sealed record DurableTransferItem(Guid OperationId, int Index, DurableTransferPhase Phase,
    LocalTransferFilePlan File, LocalTransferFileReceipt? Receipt, string? Error)
{
    public TransferClosure? Closure {get;init;}
    [JsonIgnore] public bool IsFinished => Closure is not null || Phase is DurableTransferPhase.IndexCommitted or DurableTransferPhase.NoOp or DurableTransferPhase.Cancelled;
}
internal sealed record DurableTransferBatch(int FormatVersion, ContentTransferRequest Request, string Fingerprint,
    StateReadSet Reads, PlannedContentTransfer[] Entries, LocalTransferFilePlan[] Files, Guid? StorageCollectionId, long Revision);

// Operational records share the state DB but never depend on a poster FK. Unfinished rows
// survive poster removal and retain source/destination recovery paths.
public sealed partial class SqliteTransferJournal(SqliteStateStore store)
{
    internal const string GuardFileName="managed-file-history.guard";
    internal IDisposable AcquireExecutor() => new FileStream(store.TransferLockPath, FileMode.OpenOrCreate,
        FileAccess.ReadWrite, FileShare.None);
    internal const string MigrationSql = """
        CREATE TABLE managed_storage_roots(root_id TEXT PRIMARY KEY, body_json TEXT NOT NULL CHECK(json_valid(body_json))) STRICT;
        CREATE TABLE managed_storage_collections(collection_id TEXT PRIMARY KEY REFERENCES content_collections(collection_id),
            root_id TEXT NOT NULL REFERENCES managed_storage_roots(root_id), object_identity TEXT NOT NULL) STRICT;
        CREATE TABLE content_transfers(operation_id TEXT PRIMARY KEY, fingerprint TEXT NOT NULL,
            body_json TEXT NOT NULL CHECK(json_valid(body_json)), reads_json TEXT NOT NULL CHECK(json_valid(reads_json))) STRICT;
        CREATE TABLE content_transfer_items(operation_id TEXT NOT NULL REFERENCES content_transfers(operation_id),
            item_index INTEGER NOT NULL, phase INTEGER NOT NULL CHECK(phase BETWEEN 0 AND 7),
            receipt_json TEXT NULL CHECK(receipt_json IS NULL OR json_valid(receipt_json)), error TEXT NULL,
            PRIMARY KEY(operation_id,item_index)) STRICT;
        """;
    private static readonly JsonSerializerOptions Json = new()
    { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters=true, MaxDepth=64 };
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Decode<T>(string json)
    {
        if (json.Length > 4 * 1024 * 1024) throw new InvalidDataException("Transfer record exceeds budget.");
        return JsonSerializer.Deserialize<T>(json, Json) ?? throw new InvalidDataException("Empty transfer record.");
    }
    internal static void Verify(SqliteConnection c, SqliteTransaction? t)
    {
        foreach (string sql in new[] {
            "SELECT root_id,body_json FROM managed_storage_roots LIMIT 0;",
            "SELECT collection_id,root_id,object_identity FROM managed_storage_collections LIMIT 0;",
            "SELECT operation_id,fingerprint,body_json,reads_json FROM content_transfers LIMIT 0;",
            "SELECT operation_id,item_index,phase,receipt_json,error FROM content_transfer_items LIMIT 0;" })
        { using var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;using(q.ExecuteReader()){} }
    }

    public void Register(ManagedStorageRoot root, Collection collection, StateReadSet reads)
    {
        if(store.ProtectedTransferPath(root.Locator,true))throw new IOException("Storage root overlaps application, system or temporary data.");
        ManagedStorageProvisioner.ValidateRegistration(store,root,collection);
        if (LocalTransferFiles.Identity(root.Locator) != root.ObjectIdentity ||
            !LocalTransferFiles.Contains(LocalTransferFiles.Normalize(root.Locator), LocalTransferFiles.Normalize(collection.StorageLocator!)) ||
            Path.GetDirectoryName(LocalTransferFiles.Normalize(collection.StorageLocator!)) != LocalTransferFiles.Normalize(root.Locator))
            throw new IOException("Select an existing direct child directory under the verified persistent root.");
        string collectionIdentity=LocalTransferFiles.Identity(collection.StorageLocator!);
        store.ExecuteWrite((c,t)=>
        {
            var before=SqliteStateCommandRepository.ReadSnapshot(c,t);
            SqliteStorageRelocationJournal.RequireNoPending(c,t);
            using(var stopped=Command(c,t,"SELECT count(*) FROM storage_provision_closures WHERE collection_id=$id",("$id",collection.Id.ToString())))
                if(Convert.ToInt64(stopped.ExecuteScalar())!=0)throw new IOException("Directory creation was explicitly stopped; separate adoption is required.");
            using(var old=Command(c,t,"SELECT body_json FROM managed_storage_roots WHERE root_id=$id",("$id",root.Id.ToString())))
                if(old.ExecuteScalar() is string body && body!=Encode(root)) throw new InvalidDataException("Root changes require a separate migration.");
            var registration=ManagedCollectionRegistrationPolicy.Prepare(before.ContentCatalog,root,collection,root.ObjectIdentity);
            var result=SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                new("storage-register-"+collection.Id,StateRequestFingerprint.Compute(new {root,collection}),before.Revision,reads),
                _=>new(){ContentCatalog=registration.Catalog});
            if(result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed or StateCommandStatus.NoChange))
                throw new IOException(result.Error);
            using var insert=Command(c,t,"INSERT OR IGNORE INTO managed_storage_roots VALUES($id,$body)",
                ("$id",root.Id.ToString()),("$body",Encode(root)));insert.ExecuteNonQuery();
            using var link=Command(c,t,"INSERT OR IGNORE INTO managed_storage_collections VALUES($id,$root,$object)",
                ("$id",collection.Id.ToString()),("$root",root.Id.ToString()),("$object",collectionIdentity));link.ExecuteNonQuery();
            using var check=Command(c,t,"SELECT root_id,object_identity FROM managed_storage_collections WHERE collection_id=$id",("$id",collection.Id.ToString()));
            using var registered=check.ExecuteReader();
            if(!registered.Read() || registered.GetString(0)!=root.Id.ToString() || registered.GetString(1)!=collectionIdentity)
                throw new IOException("Registered directory identity changed; migration is required.");
            return 0;
        });
    }

    public void Begin(ContentTransferPlan plan, IReadOnlyList<LocalTransferFilePlan> files, string? confirmation, Guid? storageCollectionId=null)
        => BeginCore(plan, files, confirmation, storageCollectionId, null);

    private void BeginCore(ContentTransferPlan plan, IReadOnlyList<LocalTransferFilePlan> files, string? confirmation,
        Guid? storageCollectionId, TransferRetryTicket? retry)
    {
        bool replay=store.ExecuteRead((c,t)=>
        {
            using var q=Command(c,t,"SELECT fingerprint FROM content_transfers WHERE operation_id=$id",("$id",plan.Request.OperationId.ToString()));
            if(q.ExecuteScalar() is not string old)return false;
            if(old!=plan.Fingerprint)throw new IOException("Operation ID conflict.");
            return true;
        });
        if(replay)return; // A completed move no longer has its original source to inspect.
        if(files.Count!=plan.Entries.Count)throw new ArgumentException("File evidence cardinality mismatch.");
        if(files.Any(f=>store.ProtectedTransferPath(f.Source)||store.ProtectedTransferPath(f.Destination)))
            throw new IOException("Application/system data cannot enter this transfer provider.");
        // Do actual source/target inspection before taking the DB write lock.
        var inspected=files.Select(f=>LocalTransferFiles.Inspect(f.Source,f.Destination,
            allowCrossVolumeTree:plan.Request.KeepSourceUntilCommit)).ToArray();
        for(int i=0;i<files.Count;i++)
            if(Encode(files[i])!=Encode(inspected[i]) ||
                files[i].Source!=plan.Entries[i].Entry.Source.Locator || files[i].Destination!=plan.Entries[i].Entry.Destination.Locator ||
                files[i].Objects[0].Identity!=plan.Entries[i].Entry.Source.ObjectIdentity ||
                LocalTransferFiles.TreeHash(files[i].Objects)!=plan.Entries[i].Entry.Source.VersionWitness ||
                files[i].ParentIdentity!=plan.Entries[i].Entry.Destination.ParentIdentity)
                throw new IOException("Resolved filesystem evidence changed.");
        store.PreserveTransferGuard(); // Durable before intent, independent of later DB corruption/restore.
        store.ExecuteWrite((c,t)=>
        {
            using(var existing=Command(c,t,"SELECT fingerprint FROM content_transfers WHERE operation_id=$id",("$id",plan.Request.OperationId.ToString())))
                if(existing.ExecuteScalar() is string old)
                { if(old!=plan.Fingerprint)throw new IOException("Operation ID conflict."); return 0; }
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            SqliteStorageRelocationJournal.RequirePathsAvailable(c,t,files.SelectMany(f=>new[]{f.Source,f.Destination}));
            var protectedDirectories=state.ContentCatalog.Collections.Where(x=>x.Kind==ContentCollectionKind.Managed).Select(x=>x.StorageLocator!).ToList();
            using(var roots=Command(c,t,"SELECT body_json FROM managed_storage_roots"))
            using(var rootRows=roots.ExecuteReader())while(rootRows.Read())protectedDirectories.Add(Decode<ManagedStorageRoot>(rootRows.GetString(0)).Locator);
            if(files.Any(f=>protectedDirectories.Any(d=>string.Equals(f.Source,d,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(f.Source,d))))
                throw new IOException("Registered storage roots/collections need a separate relocation contract.");
            foreach(var entry in plan.Entries)
            {
                var descendants=state.ContentCatalog.Items.Where(i=>i.Source.ProviderId=="local-file-reference-v1" &&
                    LocalTransferFiles.Contains(entry.Entry.Source.Locator,i.Target.Locator)).Select(i=>i.Id).ToHashSet();
                if(!descendants.SetEquals(entry.Entry.Source.DescendantContentIds))
                    throw new IOException("Directory reference impact is incomplete.");
            }
            var snapshot=Snapshot(state,plan.Request.PosterId);
            if(ContentTransferPlanning.Revalidate(plan,plan.Request,snapshot,confirmation)!=TransferAdmission.ReadyForJournal)
                throw new IOException("Preview/confirmation is no longer current.");
            if(plan.Request.Route==ContentDropRoute.IntoManagedStorage)
            {
                if(storageCollectionId is null || plan.Request.Root is null)throw new IOException("Managed storage registration is required.");
                using var root=Command(c,t,"SELECT r.body_json,c.object_identity FROM managed_storage_collections c JOIN managed_storage_roots r ON c.root_id=r.root_id WHERE c.collection_id=$id",
                    ("$id",storageCollectionId.ToString()!));
                using var row=root.ExecuteReader();
                var collection=state.ContentCatalog.Collections.SingleOrDefault(x=>x.Id==storageCollectionId);
                if(!row.Read() || Decode<ManagedStorageRoot>(row.GetString(0))!=plan.Request.Root || collection is null ||
                    files.Any(f=>!LocalTransferFiles.Contains(collection.StorageLocator!,f.Destination)) ||
                    LocalTransferFiles.Identity(collection.StorageLocator!)!=row.GetString(1))
                    throw new IOException("Managed destination registration changed.");
                ManagedStorageProvisioner.ValidateRegistration(store,plan.Request.Root,collection);
            }
            if(retry is not null) ReleaseUnmoved(c,t,retry.Original,retry.OriginalFingerprint,"Replaced by confirmed retry "+plan.Request.OperationId);
            foreach(var pending in ReadItems(c,t).Where(p=>!Terminal(p)))
                if(files.Any(f=>Overlaps(f.Source,pending.File.Source)||Overlaps(f.Source,pending.File.Destination)||
                    Overlaps(f.Destination,pending.File.Source)||Overlaps(f.Destination,pending.File.Destination)))
                    throw new IOException("Another unresolved transfer owns these paths.");
            using(var count=Command(c,t,"SELECT COUNT(*) FROM content_transfers"))
                if(Convert.ToInt64(count.ExecuteScalar())>=4096)throw new IOException("Transfer history capacity reached; archive must be explicit.");
            using(var count=Command(c,t,"SELECT COUNT(*) FROM content_transfer_items"))
                if(Convert.ToInt64(count.ExecuteScalar())+files.Count>10000)throw new IOException("Transfer row capacity reached.");
            var batch=new DurableTransferBatch(1,plan.Request,plan.Fingerprint,plan.ReadSet,plan.Entries.ToArray(),inspected,storageCollectionId,state.Revision);
            if (batch.Request.Placement is not null)
            {
                if (batch.Entries.Length != 1 || batch.Request.Route != ContentDropRoute.IntoManagedStorage)
                    throw new IOException("Positioned ingress is journaled per item.");
                var proposed = IndexPlan(state,batch,0);
                if (proposed.Rejection is not null) throw new IOException(proposed.Error);
            }
            string body=Encode(batch);if(body.Length>4*1024*1024)throw new IOException("Transfer plan exceeds journal budget.");
            using var add=Command(c,t,"INSERT INTO content_transfers VALUES($id,$fp,$body,$reads)",
                ("$id",plan.Request.OperationId.ToString()),("$fp",plan.Fingerprint),("$body",body),("$reads",Encode(plan.ReadSet)));add.ExecuteNonQuery();
            for(int i=0;i<files.Count;i++)
            { using var item=Command(c,t,"INSERT INTO content_transfer_items VALUES($id,$i,$phase,NULL,NULL)",
                ("$id",plan.Request.OperationId.ToString()),("$i",i),("$phase",(int)(plan.Entries[i].SameLocation?DurableTransferPhase.NoOp:DurableTransferPhase.Intent)));item.ExecuteNonQuery(); }
            return 0;
        });
    }

    public IReadOnlyList<DurableTransferItem> Read() => store.ExecuteRead(ReadItems);
    internal void Claim(Guid id,int index) => store.ExecuteWrite((c,t)=>
    {
        var row=ReadItems(c,t).Single(p=>p.OperationId==id&&p.Index==index);
        if(Terminal(row)||row.Phase!=DurableTransferPhase.Intent)throw new IOException("Transfer is not an unexecuted intent.");
        using var read=Command(c,t,"SELECT reads_json FROM content_transfers WHERE operation_id=$id",("$id",id.ToString()));
        var expected=Decode<StateReadSet>((string)read.ExecuteScalar()!);
        var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
        var batch=ReadBatch(c,t,id);
        if(batch.Request.Route==ContentDropRoute.IntoManagedStorage)
            ManagedStorageProvisioner.ValidateRegistration(store,batch.Request.Root!,state.ContentCatalog.Collections.Single(x=>x.Id==batch.StorageCollectionId));
        if(state.ContentVersion!=expected.ContentVersion || expected.Posters.Any(p=>state.Catalog.Posters.All(s=>s.Poster.PosterId!=p.Key||s.EntityVersion!=p.Value)) ||
            !Snapshot(state,batch.Request.PosterId).PosterAcceptsTransfer)
            throw new IOException("Transfer state changed before file execution.");
        using var q=Command(c,t,"UPDATE content_transfer_items SET phase=1 WHERE operation_id=$id AND item_index=$i AND phase=0",("$id",id.ToString()),("$i",index));
        if(q.ExecuteNonQuery()!=1)throw new IOException("Transfer was already claimed.");
        return 0;
    });
    internal void Mark(Guid id,int index,DurableTransferPhase phase,LocalTransferFileReceipt? receipt=null,string? error=null) => store.ExecuteWrite((c,t)=>
    {
        var previous=ReadItems(c,t).Single(p=>p.OperationId==id && p.Index==index);
        if(Terminal(previous))throw new IOException("Completed transfer cannot be restarted.");
        bool allowed=phase==DurableTransferPhase.Review ||
            phase==DurableTransferPhase.Cancelled&&previous.Phase==DurableTransferPhase.Intent ||
            phase==DurableTransferPhase.CopyVerified&&previous.Phase==DurableTransferPhase.Executing&&receipt is not null ||
            phase==DurableTransferPhase.FileVerified&&previous.Phase is (DurableTransferPhase.Executing or DurableTransferPhase.CopyVerified or DurableTransferPhase.Review or DurableTransferPhase.FileVerified)&&receipt is not null;
        if(!allowed)throw new IOException("Invalid durable transfer transition.");
        using var q=Command(c,t,"UPDATE content_transfer_items SET phase=$phase,receipt_json=COALESCE($receipt,receipt_json),error=$error WHERE operation_id=$id AND item_index=$i",
            ("$phase",(int)phase),("$receipt",receipt is null?null:Encode(receipt)),("$error",error),("$id",id.ToString()),("$i",index));
        return q.ExecuteNonQuery();
    });

    internal StateTransactionResult CommitVerified(Guid id,int index) => store.ExecuteWrite((c,t)=>
    {
        var batch=ReadBatch(c,t,id);
        var row=ReadItems(c,t).Single(p=>p.OperationId==id&&p.Index==index);
        using var readsQuery=Command(c,t,"SELECT reads_json FROM content_transfers WHERE operation_id=$id",("$id",id.ToString()));
        var reads=Decode<StateReadSet>((string)readsQuery.ExecuteScalar()!);
        var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
        if(row.Closure is not null || (row.Phase!=DurableTransferPhase.FileVerified && row.Phase!=DurableTransferPhase.IndexCommitted))
            throw new IOException("Verified physical result is required.");
        var result=SqliteStateCommandRepository.ExecuteInTransaction(c,t,
            new($"transfer-index-{id:N}-{index}",batch.Fingerprint+":"+index,state.Revision,reads),
            before=>IndexPlan(before,batch,index),authorizedTransferId:id);
        if(result.Status is StateCommandStatus.Committed or StateCommandStatus.Replayed)
        {
            using var done=Command(c,t,"UPDATE content_transfer_items SET phase=4,error=NULL WHERE operation_id=$id AND item_index=$i",("$id",id.ToString()),("$i",index));done.ExecuteNonQuery();
            // Only this operation's own commit advances the batch read set. External conflicts
            // remain pending; never silently rebase over a repair/removal.
            var nextReads=reads with { ContentVersion=result.Snapshot.ContentVersion,
                Posters=reads.Posters.ToDictionary(p=>p.Key,p=>result.Snapshot.Catalog.Posters.Single(x=>x.Poster.PosterId==p.Key).EntityVersion) };
            using var update=Command(c,t,"UPDATE content_transfers SET reads_json=$reads WHERE operation_id=$id",("$reads",Encode(nextReads)),("$id",id.ToString()));update.ExecuteNonQuery();
        }
        return result;
    });

    internal static StateMutationPlan IndexPlan(DesktopStateSnapshot state,DurableTransferBatch batch,int index,bool confirmedRecovery=false)
    {
        var p=batch.Entries[index];var entry=p.Entry;var file=batch.Files[index];
        var items=state.ContentCatalog.Items.ToDictionary(i=>i.Id);
        var affected=p.AffectedContentIds.Append(entry.Item.Id).Distinct().ToArray();
        foreach(Guid id in affected)
        {
            var old=items.GetValueOrDefault(id) ?? (id==entry.Item.Id?entry.Item:throw new IOException("Affected identity missing."));
            string destination=id==entry.Item.Id||string.Equals(old.Target.Locator,file.Source,StringComparison.OrdinalIgnoreCase)
                ?file.Destination:LocalTransferFiles.Contains(file.Source,old.Target.Locator)
                    ?Path.Combine(file.Destination,Path.GetRelativePath(file.Source,old.Target.Locator)):throw new IOException("Unexpected alias target.");
            if(old.Source.ProviderId!="local-file-reference-v1")throw new IOException("Only local reference provider relocation is implemented.");
            items[id]=old with {Target=old.Target with {Locator=destination},Source=old.Source with {Key=destination},
                Version=items.ContainsKey(id)?checked(old.Version+1):1,
                Capabilities=old.Capabilities & ~ContentCapability.Move};
        }
        var storageIds=state.ContentCatalog.Collections.Where(c=>c.Kind==ContentCollectionKind.Managed).Select(c=>c.Id).ToHashSet();
        var removeMembers=state.ContentCatalog.Memberships.Where(m=>affected.Contains(m.ContentItemId)&&storageIds.Contains(m.CollectionId)).Select(m=>m.Id).ToArray();
        var members=state.ContentCatalog.Memberships.Where(m=>!removeMembers.Contains(m.Id)).ToList();
        if(batch.Request.Route==ContentDropRoute.IntoManagedStorage)
            foreach(Guid id in affected)members.Add(new(Guid.NewGuid(),batch.StorageCollectionId!.Value,id,ContentMembershipOrigin.Source));
        var moved=state.ContentCatalog with {Items=items.Values.ToArray(),Memberships=members};
        StateMutationPlan view;
        if(batch.Request.Route==ContentDropRoute.IntoManagedStorage)
        {
            var item=items[entry.Item.Id];
            view=ContentReferencePolicy.Prepare(new($"transfer-add-{batch.Request.OperationId}",StateCommandSource.ControlCenter,state.Revision,
                batch.Reads with {ContentVersion=state.ContentVersion},batch.Request.PosterId,[new(item,ContentReferenceChoice.Reuse,item.Id)],true),
                state with {ContentCatalog=moved});
        }
        else
        {
            var relation=state.ContentCatalog.PosterItems.SingleOrDefault(r=>r.Id==entry.OriginPosterItemId);
            if(relation is null && !confirmedRecovery)return StateMutationPlan.Reject(StateCommandStatus.NotFound,"Original card was removed; file remains recoverable.");
            view=relation is null ? new(){ContentCatalog=moved} : ContentDetachPolicy.Prepare(new($"transfer-out-{batch.Request.OperationId}",StateCommandSource.Poster,state.Revision,
                batch.Reads with {ContentVersion=state.ContentVersion},batch.Request.PosterId,[relation.LayoutItemId],true),state with {ContentCatalog=moved});
        }
        if(view.Rejection is not null)return view;
        if(batch.Request.Placement is { } landing && view.Updates.Count != 0)
        {
            var target=view.Updates.Single(x=>x.PosterId==batch.Request.PosterId);
            if(!ContentLayoutPolicy.TryRead(target.ContentLayoutJson,target.PosterId,out var layout,out var error))throw new IOException(error);
            var catalog=view.ContentCatalog!;
            var member=catalog.Memberships.Single(m=>m.CollectionId==catalog.Bindings.Single(b=>b.PosterId==target.PosterId).CollectionId && m.ContentItemId==entry.Item.Id);
            var card=catalog.PosterItems.Single(poster=>poster.PosterId==target.PosterId && poster.MembershipId==member.Id);
            // Existing memberships are not silently repositioned by collecting the same object again.
            if(!state.ContentCatalog.PosterItems.Any(r=>r.Id==card.Id))
                view=view with {Updates=view.Updates.Select(poster=>poster.PosterId==target.PosterId
                    ? poster with {ContentLayoutJson=ContentLayoutPolicy.Serialize(landing.Apply(layout!,card.LayoutItemId))}:poster).ToArray()};
        }
        var updates=view.Updates.ToDictionary(poster=>poster.PosterId);
        foreach(var poster in p.AffectedPosterIds)updates.TryAdd(poster,state.Catalog.Posters.Single(x=>x.Poster.PosterId==poster).Poster);
        return view with {ContentCatalog=view.ContentCatalog??moved,Updates=updates.Values.ToArray(),
            RemovedStorageMemberships=removeMembers,TransferReceiptId=batch.Request.OperationId,TransferReceiptIndex=index};
    }

    internal static bool ValidStorageRemoval(SqliteConnection c,SqliteTransaction t,StateMutationPlan plan)
    {
        if(plan.TransferReceiptId is not { } id)return plan.RemovedStorageMemberships.Count==0;
        var batch=ReadBatch(c,t,id);
        if(plan.TransferReceiptIndex<0||plan.TransferReceiptIndex>=batch.Entries.Length)return false;
        using var q=Command(c,t,"SELECT phase FROM content_transfer_items i WHERE operation_id=$id AND item_index=$i AND NOT EXISTS (SELECT 1 FROM content_transfer_closures s WHERE s.operation_id=i.operation_id AND s.item_index=i.item_index)",("$id",id.ToString()),("$i",plan.TransferReceiptIndex));
        if(q.ExecuteScalar() is not long phase || phase!=(long)DurableTransferPhase.FileVerified)return false;
        return true; // Only CommitVerified has the internal repository authorization.
    }
    internal static bool AllowsConcurrentMutation(SqliteConnection c,SqliteTransaction t,StateMutationPlan plan)
    {
        if(plan.TransferReceiptId is not null)return true;
        using var q=Command(c,t,"SELECT DISTINCT operation_id FROM content_transfer_items i WHERE phase IN (1,2,3) AND NOT EXISTS (SELECT 1 FROM content_transfer_closures s WHERE s.operation_id=i.operation_id AND s.item_index=i.item_index)");
        var pending=new List<Guid>();using(var r=q.ExecuteReader())while(r.Read())pending.Add(Guid.Parse(r.GetString(0)));
        if(pending.Count==0)return true;
        if(plan.ContentCatalog is not null)return false;
        var owned=pending.SelectMany(id=>ReadBatch(c,t,id).Reads.Posters.Keys).ToHashSet();
        return !plan.Updates.Select(p=>p.PosterId).Concat(plan.Removes).Any(owned.Contains);
    }
    private static DurableTransferBatch ReadBatch(SqliteConnection c,SqliteTransaction t,Guid id)
    {
        using var q=Command(c,t,"SELECT body_json FROM content_transfers WHERE operation_id=$id",("$id",id.ToString()));
        var batch=Decode<DurableTransferBatch>(q.ExecuteScalar() as string??throw new IOException("Transfer not found."));
        if(batch.FormatVersion!=1)throw new InvalidDataException("Unknown transfer format; preserve original record.");
        return batch;
    }
    internal static IReadOnlyList<DurableTransferItem> ReadItems(SqliteConnection c,SqliteTransaction t)
    {
        using var q=Command(c,t,"SELECT operation_id,item_index,phase,receipt_json,error FROM content_transfer_items ORDER BY operation_id,item_index");
        var rows=new List<(Guid,int,DurableTransferPhase,string?,string?)>();
        using(var r=q.ExecuteReader())while(r.Read())
        {
            if(rows.Count>=10000)throw new InvalidDataException("Transfer recovery capacity exceeded.");
            rows.Add((Guid.Parse(r.GetString(0)),r.GetInt32(1),(DurableTransferPhase)r.GetInt32(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4)));
        }
        var batches=rows.Select(x=>x.Item1).Distinct().ToDictionary(id=>id,id=>ReadBatch(c,t,id));
        var closures=ReadClosures(c,t);
        return rows.Select(x=>
        {
            var row=new DurableTransferItem(x.Item1,x.Item2,x.Item3,batches[x.Item1].Files[x.Item2],x.Item4 is null?null:Decode<LocalTransferFileReceipt>(x.Item4),x.Item5);
            var closure=closures.GetValueOrDefault((x.Item1,x.Item2));
            if(closure is not null && (row.IsFinished || closure.PreviousWitness!=StateRequestFingerprint.Compute(row)))
                throw new InvalidDataException("Closure does not match the preserved transfer evidence.");
            return row with {Closure=closure};
        }).ToArray();
    }
    internal static ContentTransferSnapshot Snapshot(DesktopStateSnapshot state,string posterId) =>
        new(state.ContentCatalog,state.ContentVersion,state.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),
            state.Catalog.Posters.Any(p=>p.Poster.PosterId==posterId&&!p.Poster.IsLocked&&p.Poster.IsVisible),
            !ContentLayoutPolicy.TryRead(state.Catalog.Posters.FirstOrDefault(p=>p.Poster.PosterId==posterId)?.Poster.ContentLayoutJson,
                posterId,out var document,out _) || document!.Placements.Count>0);
    internal static bool Terminal(DurableTransferItem item)=>item.IsFinished;
    private static bool Overlaps(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(a,b)||LocalTransferFiles.Contains(b,a);
    private static SqliteCommand Command(SqliteConnection c,SqliteTransaction? t,string sql,params (string Name,object? Value)[] values)
    {
        var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;
        foreach(var (name,value)in values)q.Parameters.AddWithValue(name,value??DBNull.Value);
        return q;
    }
}

