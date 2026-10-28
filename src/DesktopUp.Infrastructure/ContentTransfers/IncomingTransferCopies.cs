using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Per-item cross-volume preparation reuses the same bounded, handle-based copy primitives as
// storage relocation. Creation identities live in the EXISTING durable item receipt. No new DB.
internal static class IncomingTransferCopies
{
    internal static LocalTransferFileReceipt Prepare(LocalTransferFilePlan file, Action<LocalTransferFileReceipt> record,
        CancellationToken token, Action<TransferExecutionCheckpoint>? checkpoint)
    {
        using var source = LocalTransferFiles.LockVerifiedDestination(file with { Destination=file.Source, SameVolume=true }, null);
        if(LocalTransferFiles.Identity(Path.GetDirectoryName(file.Destination)!)!=file.ParentIdentity || LocalTransferFiles.Exists(file.Destination))
            throw new IOException("Destination changed; no files moved.");
        if(new DriveInfo(Path.GetPathRoot(file.Destination)!).AvailableFreeSpace < file.Objects.Sum(x=>x.Length)+1024*1024)
            throw new IOException("Insufficient target space; source retained.");
        var created = new List<LocalTransferObject>();
        LocalTransferFileReceipt Receipt(string hash="") => new(created[0].Identity,hash) { OwnedCopies=created.ToArray() };
        void Created(LocalTransferObject value, string relative)
        {
            created.Add(value with { RelativePath=relative });
            record(Receipt()); // Durable ownership before the first content write.
            token.ThrowIfCancellationRequested();
        }
        foreach(var obj in file.Objects)
        {
            token.ThrowIfCancellationRequested();
            string from=Path.Combine(file.Source,obj.RelativePath), to=Path.Combine(file.Destination,obj.RelativePath);
            LocalTransferFiles.RequireCopyable(from);
            if(obj.Directory) Created(LocalTransferFiles.CreateOwnedDirectory(to),obj.RelativePath);
            else
            {
                using var pin=new LocalTransferFiles.PinnedObject(from);
                LocalTransferFiles.CopyKeepingSource(from,to,obj,pin.Metadata,v=>Created(v,obj.RelativePath),
                    ()=>checkpoint?.Invoke(TransferExecutionCheckpoint.CopyProgress),token);
            }
        }
        checkpoint?.Invoke(TransferExecutionCheckpoint.AfterCopy);
        var receipt=Receipt(LocalTransferFiles.TreeHash(file.Objects));
        if(!LocalTransferFiles.VerifyDestination(file,receipt))throw new IOException("Target verification failed; source retained.");
        record(receipt); checkpoint?.Invoke(TransferExecutionCheckpoint.CopyRecorded);
        return receipt;
    }

    // Committed: remove only unchanged original objects while a complete target is pinned.
    // Uncommitted: remove only our created copies, while the complete original is pinned.
    // Unexpected replacements/additions are never recursively removed.
    internal static void Settle(DurableTransferItem row)
    {
        if(row.Receipt?.OwnedCopies is not { } created)return;
        bool committed=row.Phase==DurableTransferPhase.IndexCommitted;
        var file=row.File;
        string removable=committed?file.Source:file.Destination;
        using var complete=committed?LocalTransferFiles.LockVerifiedDestination(file,row.Receipt):
            LocalTransferFiles.LockVerifiedDestination(file with { Destination=file.Source, SameVolume=true },null);
        if(!LocalTransferFiles.Exists(removable))return;
        var observed=LocalTransferFiles.Inspect(removable,removable).Objects;
        var expected=(committed?file.Objects:created).ToDictionary(x=>x.RelativePath,StringComparer.OrdinalIgnoreCase);
        if(observed.Any(x=>!expected.TryGetValue(x.RelativePath,out var old)||old.Identity!=x.Identity||old.Directory!=x.Directory))
            throw new IOException("Unowned object at cleanup location; copies preserved.");
        // Verify all remaining bytes before deleting any, including a stopped partial write.
        foreach(var obj in observed.Where(x=>!x.Directory))
        {
            var original=file.Objects.Single(x=>x.RelativePath==obj.RelativePath);
            using var remaining=new LocalTransferFiles.PinnedObject(Path.Combine(removable,obj.RelativePath));
            if(committed)
            {
                if(remaining.Snapshot(obj.RelativePath)!=original)throw new IOException("Source changed after copy; keep both.");
            }
            else
            {
                using var originalPin=new LocalTransferFiles.PinnedObject(Path.Combine(file.Source,obj.RelativePath));
                if(!LocalTransferFiles.IsExactPrefix(originalPin,remaining))throw new IOException("Prepared target changed; keep both.");
            }
        }
        foreach(var obj in observed.Reverse())
        {
            string path=Path.Combine(removable,obj.RelativePath);
            using var pin=new LocalTransferFiles.PinnedObject(path,delete:true);
            if(pin.Snapshot(obj.RelativePath)!=obj)throw new IOException("Cleanup object changed; preserve it.");
            pin.Delete(); // Non-recursive handle deletion; directories must be empty.
        }
    }
}

public sealed partial class SqliteTransferJournal
{
    internal bool KeepsSource(Guid id) => store.ExecuteRead((c,t)=>ReadBatch(c,t,id).Request.KeepSourceUntilCommit);
    internal void RecordCopyProgress(Guid id,int index,LocalTransferFileReceipt receipt) => store.ExecuteWrite((c,t)=>
    {
        var row=ReadItems(c,t).Single(x=>x.OperationId==id && x.Index==index);
        if(row.Phase!=DurableTransferPhase.Executing || row.IsFinished)throw new IOException("Copy no longer owns this attempt.");
        using var update=Command(c,t,"UPDATE content_transfer_items SET receipt_json=$body WHERE operation_id=$id AND item_index=$i",
            ("$body",Encode(receipt)),("$id",id.ToString()),("$i",index));return update.ExecuteNonQuery();
    });
    internal void SettleCopies(DurableTransferItem row)
    {
        if(row.Receipt is not {OwnedCopies:not null,CleanupFinished:false} receipt || row.Closure is not null)return;
        try
        {
            IncomingTransferCopies.Settle(row);
            store.ExecuteWrite((c,t)=>
            {
                var current=ReadItems(c,t).Single(x=>x.OperationId==row.OperationId && x.Index==row.Index);
                if(current.Closure is not null || StateRequestFingerprint.Compute(current)!=StateRequestFingerprint.Compute(row))
                    throw new IOException("Copy cleanup receipt changed.");
                using var update=Command(c,t,"UPDATE content_transfer_items SET receipt_json=$body,error=NULL WHERE operation_id=$id AND item_index=$i",
                    ("$body",Encode(receipt with {CleanupFinished=true})),("$id",row.OperationId.ToString()),("$i",row.Index));return update.ExecuteNonQuery();
            });
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Persist one actionable location note; cleanup can be rechecked after restart.
            store.ExecuteWrite((c,t)=>
            {
                using var update=Command(c,t,"UPDATE content_transfer_items SET error=$error WHERE operation_id=$id AND item_index=$i",
                    ("$error",e.Message),("$id",row.OperationId.ToString()),("$i",row.Index));return update.ExecuteNonQuery();
            });
        }
    }
}
