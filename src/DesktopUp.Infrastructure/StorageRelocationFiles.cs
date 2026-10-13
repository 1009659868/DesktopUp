using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

internal enum StorageCopyCheckpoint { ManifestWritten, ObjectCreated, CopyProgress, FileCopied, Prepared, Verified, SourceDeleted, TargetDeleted }

// Internal until D2-04/production acceptance. Unlike LocalTransferFiles.Move this adapter
// NEVER deletes any source before the one SQLite commit. It never resumes copying on startup.
internal sealed partial class StorageRelocationFiles(SqliteStateStore store):IStorageRelocationVerifier
{
    internal void Prepare(StorageRelocationPlan plan,string confirmation,CancellationToken cancellation=default,
        Action<StorageCopyCheckpoint>? checkpoint=null)
    {
        var journal=new SqliteStorageRelocationJournal(store);journal.Begin(plan,confirmation);
        using var ledger=new StorageCopyLedger(store,plan);
        StorageCopyManifest manifest;
        using(var executor=new SqliteTransferJournal(store).AcquireExecutor())
        using(var sources=journal.LockOwnedSources(plan))
        {
            RequirePending(plan);journal.RequireCurrentPlan(plan);cancellation.ThrowIfCancellationRequested();
            var collections=new List<StorageCopyCollection>();int count=1;long bytes=0;
            foreach(var inventory in plan.Request.Inventories)
            {
                string path=plan.Catalog.Collections.Single(x=>x.Id==inventory.CollectionId).StorageLocator!;
                var root=plan.Roots.Single(x=>x.Root.Id==inventory.RootId).Root;
                string metadata=File.Exists(Path.Combine(root.Locator,StorageBundleOwnership.RootMarker))?StorageBundleOwnership.ChildMarker:
                    File.Exists(Path.Combine(root.Locator,".desktopup-storage-owner.json"))?".desktopup-collection-owner.json":ManagedStorageAdoption.ChildMarker;
                var entries=new List<StorageCopyEntry>();
                foreach(var obj in LocalTransferFiles.Inspect(path,path,cancellation).Objects)
                {
                    string source=Path.Combine(path,obj.RelativePath);LocalTransferFiles.RequireCopyable(source);
                    using var pin=new LocalTransferFiles.PinnedObject(source);
                    if(pin.Snapshot(obj.RelativePath)!=obj)throw new IOException("Inventory changed.");
                    entries.Add(new(obj,pin.Metadata));count++;bytes+=obj.Length;
                }
                foreach(var id in inventory.AffectedContentIds)
                {
                    string locator=plan.Catalog.Items.Single(x=>x.Id==id).Target.Locator;
                    string relative=string.Equals(path,locator,StringComparison.OrdinalIgnoreCase)?"":Path.GetRelativePath(path,locator);
                    if(entries.All(x=>!string.Equals(x.Object.RelativePath,relative,StringComparison.OrdinalIgnoreCase)))
                        throw new IOException("An affected reference is missing from the physical inventory; preserve the old location.");
                }
                collections.Add(new(inventory.CollectionId,metadata,entries.ToArray()));
            }
            if(count>LocalTransferFiles.MaximumTreeEntries||bytes>LocalTransferFiles.MaximumTreeBytes)throw new IOException("Whole-storage copy budget exceeded.");
            var destinationDrive=new DriveInfo(Path.GetPathRoot(plan.Request.Destination.Locator)!);
            if(destinationDrive.AvailableFreeSpace<bytes+16L*1024*1024)throw new IOException("Insufficient destination space; sources retained.");
            manifest=new(1,store.TransferProfileIdentity,plan.Request.OperationId,plan.Fingerprint,collections.ToArray());
            ledger.Open(create:true);ledger.Write("manifest.json",manifest);_=ledger.Manifest();
        }
        checkpoint?.Invoke(StorageCopyCheckpoint.ManifestWritten);int sequence=0;
        void Created(string path,LocalTransferObject value)
        {
            ledger.Write($"created-{sequence++:D5}.json",new StorageCreatedObject(Path.GetRelativePath(plan.Request.Destination.Locator,path),value));
            checkpoint?.Invoke(StorageCopyCheckpoint.ObjectCreated);cancellation.ThrowIfCancellationRequested();
        }
        cancellation.ThrowIfCancellationRequested();
        var claim=StorageBundleOwnership.CreatePrepared(store,plan,Created);ledger.Write("prepared.json",claim);
        using(var executor=new SqliteTransferJournal(store).AcquireExecutor())
        using(var sources=journal.LockOwnedSources(plan))
        using(var owners=StorageBundleOwnership.LockPrepared(store,plan,claim))
        {
            RequirePending(plan);journal.RequireCurrentPlan(plan);
            foreach(var collection in manifest.Collections)
            foreach(var entry in StorageCopyLedger.Payload(collection))
            {
                cancellation.ThrowIfCancellationRequested();string target=ledger.Target(collection.Id,entry.Object.RelativePath);
                if(entry.Object.Directory)Created(target,LocalTransferFiles.CreateOwnedDirectory(target));
                else
                {
                    LocalTransferFiles.CopyKeepingSource(ledger.Source(collection.Id,entry.Object.RelativePath),target,entry.Object,entry.Metadata,
                        value=>Created(target,value),()=>checkpoint?.Invoke(StorageCopyCheckpoint.CopyProgress),cancellation);
                    checkpoint?.Invoke(StorageCopyCheckpoint.FileCopied);
                }
            }
            var verified=new StorageVerifiedCopies(claim,LocalTransferFiles.Inspect(plan.Request.Destination.Locator,plan.Request.Destination.Locator,cancellation).Objects.ToArray());
            using var proof=Verify(plan,ledger,manifest,verified,withSources:true);
            ledger.Write("verified.json",verified);
        }
        checkpoint?.Invoke(StorageCopyCheckpoint.Prepared);
    }
    public StorageRelocationProofLease Acquire(StorageRelocationPlan plan)
    {
        using var ledger=new StorageCopyLedger(store,plan);ledger.Open(create:false);
        var manifest=ledger.Manifest();var verified=ledger.Read<StorageVerifiedCopies>("verified.json");
        return Verify(plan,ledger,manifest,verified,withSources:true);
    }
    private void RequirePending(StorageRelocationPlan plan)
    {
        var row=new SqliteStorageRelocationJournal(store).Get(plan.Request.OperationId);
        if(row.Plan.Fingerprint!=plan.Fingerprint||row.Commit is not null||row.Closure is not null||plan.Reads.RuntimeGeneration!=store.StorageRuntimeGeneration)
            throw new IOException("Only the current pending intent may prepare files; restart reconciles, not recopies.");
    }
    private sealed class Proof(StorageRelocationTargets targets,SqliteStorageRelocationJournal.Leases leases):StorageRelocationProofLease
    {public override StorageRelocationTargets Targets=>targets;public override void Dispose()=>leases.Dispose();}
    private StorageRelocationProofLease Verify(StorageRelocationPlan plan,StorageCopyLedger ledger,StorageCopyManifest manifest,
        StorageVerifiedCopies verified,bool withSources)
    {
        var leases=new SqliteStorageRelocationJournal.Leases();
        try
        {
            if(verified.Claim is null||verified.Targets is null||verified.Targets.Any(x=>x is null))throw new InvalidDataException("Incomplete verified-copy receipt.");
            leases.Add(StorageBundleOwnership.LockPrepared(store,plan,verified.Claim));
            if(withSources)leases.Add(new SqliteStorageRelocationJournal(store).LockOwnedSources(plan));
            if(SqliteStorageRelocationJournal.Encode(ledger.Read<StorageBundleClaim>("prepared.json"))!=SqliteStorageRelocationJournal.Encode(verified.Claim))
                throw new IOException("Prepared owner proof changed.");
            var created=ledger.Created(manifest);
            if(verified.Targets.Length!=created.Length+1||verified.Targets.Length>LocalTransferFiles.MaximumTreeEntries||
                verified.Targets[0].RelativePath!=""||verified.Targets[0].Identity!=plan.Request.Destination.ObjectIdentity)
                throw new IOException("Target inventory does not match creation receipts.");
            foreach(var target in verified.Targets)
            {
                string path=ledger.Destination(target.RelativePath);var pin=new LocalTransferFiles.PinnedObject(path);leases.Add(pin);
                LocalTransferFiles.RequireCopyable(path);
                if(pin.Snapshot(target.RelativePath)!=target)throw new IOException("Target content/identity changed.");
                if(target.RelativePath!=""&&created.All(x=>x.RelativePath!=target.RelativePath||x.Object.Identity!=target.Identity||x.Object.Directory!=target.Directory))
                    throw new IOException("Unowned target entry.");
            }
            var observed=LocalTransferFiles.Inspect(plan.Request.Destination.Locator,plan.Request.Destination.Locator).Objects;
            if(!observed.SequenceEqual(verified.Targets))throw new IOException("Target inventory changed.");
            foreach(var collection in manifest.Collections)
            {
                if(withSources)
                {
                    foreach(var e in collection.Entries)
                    {
                        string path=ledger.Source(collection.Id,e.Object.RelativePath);var pin=new LocalTransferFiles.PinnedObject(path);leases.Add(pin);
                        LocalTransferFiles.RequireCopyable(path);
                        if(pin.Snapshot(e.Object.RelativePath)!=e.Object||!e.Object.Directory&&pin.Metadata!=e.Metadata)throw new IOException("Source changed; keep original index.");
                    }
                    string source=ledger.Source(collection.Id,"");
                    if(!LocalTransferFiles.Inspect(source,source).Objects.SequenceEqual(collection.Entries.Select(x=>x.Object)))throw new IOException("Source tree changed.");
                }
                foreach(var entry in StorageCopyLedger.Payload(collection))
                {
                    string target=ledger.Target(collection.Id,entry.Object.RelativePath);
                    var pin=new LocalTransferFiles.PinnedObject(target);leases.Add(pin);var o=pin.Snapshot();
                    if(o.Directory!=entry.Object.Directory||o.Length!=entry.Object.Length||o.Sha256!=entry.Object.Sha256||
                        !o.Directory&&pin.Metadata!=entry.Metadata)throw new IOException("Target is not an exact payload copy.");
                }
            }
            return new Proof(new(plan.Fingerprint,StateRequestFingerprint.Compute(verified),verified.Claim),leases);
        }
        catch{leases.Dispose();throw;}
    }
    internal StorageRelocationResult Execute(StorageRelocationPlan plan,string confirmation,CancellationToken cancellation=default,
        Action<StorageCopyCheckpoint>? checkpoint=null,Action<StorageRelocationCheckpoint>? transactionCheckpoint=null)
    {
        var journal=new SqliteStorageRelocationJournal(store);
        // Wrong confirmation cannot cancel someone else's existing operation.
        if(confirmation!=plan.Fingerprint)throw new IOException("Confirm the current plan.");
        try
        {
            Prepare(plan,confirmation,cancellation,checkpoint);cancellation.ThrowIfCancellationRequested();
            journal.RecordTargets(plan.Request.OperationId,this,transactionCheckpoint);checkpoint?.Invoke(StorageCopyCheckpoint.Verified);
            cancellation.ThrowIfCancellationRequested();journal.Commit(plan.Request.OperationId,this,transactionCheckpoint);
        }
        catch(TransferInterruptionException){throw;} // Fault fixture models a process loss, not normal failure handling.
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException or NotSupportedException or System.Text.Json.JsonException)
        {
            if(journal.Read().All(x=>x.Plan.Request.OperationId!=plan.Request.OperationId))throw;
            return Recover(plan.Request.OperationId,checkpoint,()=>cancellation.IsCancellationRequested) with {Reason=error.Message};
        }
        return Recover(plan.Request.OperationId,checkpoint,()=>cancellation.IsCancellationRequested);
    }
}
