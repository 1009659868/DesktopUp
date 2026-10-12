using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public enum TransferFinishKind { Completed, NotMoved, Preserved }
public sealed record TransferFinish(DurableTransferItem Item, TransferFinishKind Kind);

// Production boundary: finish an attempt, never retry its Move. The lower-level executor
// remains available to fault tests and diagnostic tools, not to a user task workflow.
public sealed class AutomaticTransferCompletion(SqliteTransferJournal journal)
{
    public IReadOnlyList<TransferFinish> Execute(Guid operationId, CancellationToken cancellation = default,
        Action<TransferExecutionCheckpoint,int>? checkpoint = null)
    {
        new LocalTransferExecutor(journal).Execute(operationId,cancellation,checkpoint);
        return Settle(operationId);
    }

    public IReadOnlyList<TransferFinish> Settle(Guid? operationId = null)
    {
        // Includes committed copies whose source cleanup was interrupted; never repeats a move.
        new LocalTransferExecutor(journal).Recover(operationId);
        var result = new List<TransferFinish>();
        foreach (var initial in journal.Read().Where(x => operationId is null || x.OperationId == operationId))
        {
            if (!initial.IsFinished)
            {
                // Reconcile verified physical results first; this NEVER executes a file move.
                new LocalTransferExecutor(journal).Recover(initial.OperationId,initial.Index);
                var row = Current(initial);
                if (!row.IsFinished && !LocalTransferFiles.Exists(row.File.Source))
                {
                    try
                    {
                        // Rebase only the original intent, never choose a new poster or undo
                        // an independently repaired source. Both APIs pin/verify the result.
                        var ticket = journal.PreviewIndexRecovery(row.OperationId,row.Index,automatic:true);
                        journal.ConfirmIndexRecovery(ticket,ticket.Fingerprint);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
                    { /* Preserve below. No guessed file rollback. */ }
                }
                row = Current(initial);
                if (!row.IsFinished) journal.FinishAttempt(row.OperationId,row.Index);
            }
            var final = Current(initial);
            result.Add(new(final, final.Receipt is {OwnedCopies:not null,CleanupFinished:false} ? TransferFinishKind.Preserved : final.Phase is DurableTransferPhase.IndexCommitted or DurableTransferPhase.NoOp
                ? TransferFinishKind.Completed : final.Phase == DurableTransferPhase.Cancelled ||
                    final.Error?.StartsWith(SqliteTransferJournal.UnmovedFinishPrefix,StringComparison.Ordinal) == true
                    ? TransferFinishKind.NotMoved : TransferFinishKind.Preserved));
        }
        return result;
    }
    private DurableTransferItem Current(DurableTransferItem row) =>
        journal.Read().Single(x=>x.OperationId==row.OperationId && x.Index==row.Index);
}

public sealed partial class SqliteTransferJournal
{
    internal const string UnmovedFinishPrefix = "自动收尾：原文件未移动；";
    // No executor can be active while an attempt is closed. History/guard stay intact,
    // but a failed operation no longer reserves paths against a new drag.
    internal void FinishAttempt(Guid operationId,int index)
    {
        using var executor = AcquireExecutor();
        var row = Read().Single(x=>x.OperationId==operationId && x.Index==index);
        if(row.IsFinished)return;
        IDisposable? source = null;
        bool unchanged = false;
        try
        {
            source = LocalTransferFiles.LockVerifiedDestination(row.File with {Destination=row.File.Source,SameVolume=true},null);
            unchanged = !LocalTransferFiles.Exists(row.File.Destination);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        try
        {
            store.ExecuteWrite((c,t)=>
            {
                var current = ReadItems(c,t).Single(x=>x.OperationId==operationId && x.Index==index);
                if(current.IsFinished)return 0;
                if(StateRequestFingerprint.Compute(current)!=StateRequestFingerprint.Compute(row))
                    throw new IOException("Transfer changed during finalization.");
                string note = (unchanged ? UnmovedFinishPrefix : "自动收尾：结果未确认，所有文件保持原位；") + row.Error;
                // Record the actual outcome, not a user confirmation that never occurred.
                using(var update = Command(c,t,"UPDATE content_transfer_items SET phase=7,error=$note WHERE operation_id=$id AND item_index=$i",
                    ("$note",note),("$id",operationId.ToString()),("$i",index)))update.ExecuteNonQuery();
                var updated = current with {Phase=DurableTransferPhase.Review,Error=note};
                long revision=SqliteStateCommandRepository.ReadSnapshot(c,t).Revision;
                var closure = new TransferClosure(1,StateRequestFingerprint.Compute(new {Purpose="automatic-attempt-finish-v1",updated,revision}),
                    StateRequestFingerprint.Compute(updated),revision);
                using var insert=Command(c,t,"INSERT INTO content_transfer_closures VALUES($id,$i,$body)",
                    ("$id",operationId.ToString()),("$i",index),("$body",Encode(closure)));
                insert.ExecuteNonQuery();return 0;
            });
        }
        finally { source?.Dispose(); }
    }
}
