using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

internal sealed partial class StorageRelocationFiles
{
    internal StorageRelocationResult Recover(Guid operation,Action<StorageCopyCheckpoint>? checkpoint=null,Func<bool>? stopCleanup=null)
    {
        var journal=new SqliteStorageRelocationJournal(store);var row=journal.Get(operation);var plan=row.Plan;
        using var ledger=new StorageCopyLedger(store,plan);StorageRelocationResult result;
        using(var executor=new SqliteTransferJournal(store).AcquireExecutor())
        {
            row=journal.Get(operation);
            try
            {
                if(!Directory.Exists(ledger.DirectoryPath))
                {
                    using var vacant=LocalTransferFiles.LockDirectory(plan.Request.Destination.Locator);
                    bool untouched=row.Commit is null&&LocalTransferFiles.Identity(plan.Request.Destination.Locator)==plan.Request.Destination.ObjectIdentity&&
                        !Directory.EnumerateFileSystemEntries(plan.Request.Destination.Locator).Any();
                    result=new(row.Commit is not null,untouched,untouched?[]:[plan.Request.Destination.Locator],untouched?null:"Copy evidence missing; preserve paths.");
                }
                else
                {
                    ledger.Open(create:false);
                    if(ledger.Exists("done.json"))
                    {
                        result=ledger.Read<StorageRelocationResult>("done.json");
                        if(result.RetainedPaths is null||result.Committed!=(row.Commit is not null))throw new InvalidDataException("Cleanup receipt disagrees with index.");
                    }
                    else
                    {
                        var manifest=ledger.Manifest();var created=ledger.Created(manifest);
                        var retained=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if(row.Commit is not null&&stopCleanup?.Invoke()==true)
                            foreach(var root in plan.Roots)retained.Add(root.Root.Locator);
                        else if(row.Commit is not null)CleanupSources(row,ledger,manifest,retained,checkpoint,stopCleanup);
                        else CleanupTargets(plan,ledger,manifest,created,retained,checkpoint);
                        result=new(row.Commit is not null,retained.Count==0,retained.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                            retained.Count==0?null:"Some paths changed or could not be proved; they were preserved.");
                        ledger.Write("done.json",result); // Terminal decision: never delete these paths on a later retry.
                    }
                }
                if(result.Committed&&result.Cleaned)
                    store.ExecuteWrite((c,t)=>
                    {
                        using var q=SqliteStorageRelocationJournal.Command(c,t,"UPDATE storage_relocations SET phase=6 WHERE operation_id=$id AND EXISTS(SELECT 1 FROM storage_relocation_commits WHERE operation_id=$id)",("$id",operation.ToString()));
                        q.ExecuteNonQuery();return 0;
                    });
            }
            catch(TransferInterruptionException){throw;}
            catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {result=new(row.Commit is not null,false,plan.Roots.Select(x=>x.Root.Locator).Append(plan.Request.Destination.Locator).ToArray(),e.Message);}
        }
        if(row.Commit is null)journal.Cancel(operation); // No file actions after releasing the executor.
        return result;
    }
    private void CleanupTargets(StorageRelocationPlan plan,StorageCopyLedger ledger,StorageCopyManifest manifest,
        StorageCreatedObject[] created,HashSet<string> retained,Action<StorageCopyCheckpoint>? checkpoint)
    {
        string root=plan.Request.Destination.Locator;using var rootLease=LocalTransferFiles.LockDirectory(root);
        if(store.ProtectedTransferPath(root,true)||LocalTransferFiles.Identity(root)!=plan.Request.Destination.ObjectIdentity)
            throw new IOException("Target root replaced; preserve it.");
        var payload=manifest.Collections.SelectMany(c=>StorageCopyLedger.Payload(c).Select(e=>(Collection:c,Entry:e,
            Relative:Path.GetRelativePath(root,ledger.Target(c.Id,e.Object.RelativePath))))).ToDictionary(x=>x.Relative,StringComparer.OrdinalIgnoreCase);
        foreach(var createdObject in created.OrderByDescending(x=>x.RelativePath.Length))
        {
            string path=ledger.Destination(createdObject.RelativePath);
            try
            {
                if(!LocalTransferFiles.Exists(path))continue;
                LocalTransferFiles.RequireCopyable(path);
                using var target=new LocalTransferFiles.PinnedObject(path,delete:true);var observed=target.Snapshot(createdObject.Object.RelativePath);
                if(observed.Identity!=createdObject.Object.Identity||observed.Directory!=createdObject.Object.Directory)throw new IOException("Target was replaced.");
                if(payload.TryGetValue(createdObject.RelativePath,out var p)&&!observed.Directory)
                {
                    string source=ledger.Source(p.Collection.Id,p.Entry.Object.RelativePath);
                    if(LocalTransferFiles.Identity(ledger.Source(p.Collection.Id,""))!=p.Collection.Entries[0].Object.Identity)throw new IOException("Source collection was replaced.");
                    using var original=new LocalTransferFiles.PinnedObject(source);
                    LocalTransferFiles.RequireCopyable(source);
                    if(original.Snapshot(p.Entry.Object.RelativePath)!=p.Entry.Object||!LocalTransferFiles.IsExactPrefix(original,target))
                        throw new IOException("Partial target is not provably redundant; preserve it.");
                    target.Delete();
                }
                else
                {
                    if(!observed.Directory&&observed!=createdObject.Object)throw new IOException("Declaration was edited; preserve it.");
                    if(observed.Directory&&Directory.EnumerateFileSystemEntries(path).Any())throw new IOException("Non-empty directory retained.");
                    target.Delete(); // Non-recursive, identity-pinned, never the selected root itself.
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException){retained.Add(path);continue;}
            checkpoint?.Invoke(StorageCopyCheckpoint.TargetDeleted);
        }
        if(Directory.EnumerateFileSystemEntries(root).Any())retained.Add(root);
    }
    private void CleanupSources(StorageRelocationRow row,StorageCopyLedger ledger,StorageCopyManifest manifest,
        HashSet<string> retained,Action<StorageCopyCheckpoint>? checkpoint,Func<bool>? stopCleanup)
    {
        var plan=row.Plan;var journal=new SqliteStorageRelocationJournal(store);
        if(journal.ReadLocation().Selection.ActiveRootId!=plan.Request.Destination.Id)throw new IOException("Storage changed again; do not clean an earlier migration.");
        var verified=ledger.Read<StorageVerifiedCopies>("verified.json");
        using var targets=Verify(plan,ledger,manifest,verified,withSources:false);
        if(row.Targets!=targets.Targets&&SqliteStorageRelocationJournal.Encode(row.Targets)!=SqliteStorageRelocationJournal.Encode(targets.Targets))
            throw new IOException("Durable target evidence no longer matches.");
        using var roots=new SqliteStorageRelocationJournal.Leases();
        foreach(var root in plan.Roots)
        {
            roots.Add(LocalTransferFiles.LockDirectory(root.Root.Locator));
            if(store.ProtectedTransferPath(root.Root.Locator,true)||LocalTransferFiles.Identity(root.Root.Locator)!=root.Root.ObjectIdentity)throw new IOException("Old root changed.");
            foreach(var marker in SqliteStorageRelocationJournal.OwnerFiles(root.Root.Locator))roots.Add(new LocalTransferFiles.PinnedObject(marker));
            if(SqliteStorageRelocationJournal.OwnerWitness(root.Root.Locator)!=root.OwnershipWitness)throw new IOException("Old owner changed.");
        }
        foreach(var collection in manifest.Collections)
        {
            string sourceRoot=ledger.Source(collection.Id,"");using var collectionLease=LocalTransferFiles.LockDirectory(sourceRoot);
            if(LocalTransferFiles.Identity(sourceRoot)!=collection.Entries[0].Object.Identity)throw new IOException("Old collection replaced.");
            var marker=collection.Entries.Single(x=>x.Object.RelativePath==collection.MetadataName);
            using var markerLease=new LocalTransferFiles.PinnedObject(ledger.Source(collection.Id,collection.MetadataName));
            if(markerLease.Snapshot(collection.MetadataName)!=marker.Object)throw new IOException("Old collection owner changed.");
            foreach(var entry in StorageCopyLedger.Payload(collection).OrderByDescending(x=>x.Object.RelativePath.Length))
            {
                string source=ledger.Source(collection.Id,entry.Object.RelativePath);
                if(stopCleanup?.Invoke()==true){retained.Add(source);continue;}
                try
                {
                    if(!LocalTransferFiles.Exists(source))continue; // A previous cleanup already removed this exact owned entry.
                    LocalTransferFiles.RequireCopyable(source);using var pin=new LocalTransferFiles.PinnedObject(source,delete:true);
                    if(pin.Snapshot(entry.Object.RelativePath)!=entry.Object||!entry.Object.Directory&&pin.Metadata!=entry.Metadata)throw new IOException("Old content changed; retain it.");
                    if(entry.Object.Directory&&Directory.EnumerateFileSystemEntries(source).Any())throw new IOException("Unknown old children retained.");
                    pin.Delete();
                }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException){retained.Add(source);continue;}
                checkpoint?.Invoke(StorageCopyCheckpoint.SourceDeleted);
            }
            // Keep owner declarations/root directories as history; never sweep unknown root files.
            if(Directory.EnumerateFileSystemEntries(sourceRoot).Any(x=>Path.GetFileName(x)!=collection.MetadataName))retained.Add(sourceRoot);
        }
    }
}
