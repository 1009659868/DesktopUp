using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public enum TransferExecutionCheckpoint { Claimed, BeforeMove, AfterCopy, CopyRecorded, AfterMove, FileRecorded, BeforeIndex, IndexCommitted, CopyProgress }
// Test-only fault type deliberately bypasses the ordinary error path to emulate abrupt loss
// of the executor. A second instance must recover only from disk and the durable journal.
public sealed class TransferInterruptionException(string message) : Exception(message);

public sealed class LocalTransferExecutor(SqliteTransferJournal journal)
{
    public IReadOnlyList<DurableTransferItem> Execute(Guid operationId, CancellationToken cancellation=default,
        Action<TransferExecutionCheckpoint,int>? checkpoint=null)
    {
        using var lease=journal.AcquireExecutor();
        foreach(var item in journal.Read().Where(i=>i.OperationId==operationId).OrderBy(i=>i.Index))
        {
            if(item.IsFinished||item.Phase!=DurableTransferPhase.Intent)continue; // No implicit retries.
            if(cancellation.IsCancellationRequested)
            { journal.Mark(operationId,item.Index,DurableTransferPhase.Cancelled);continue; }
            try
            {
                journal.Claim(operationId,item.Index);
                checkpoint?.Invoke(TransferExecutionCheckpoint.Claimed,item.Index);
                if(!item.File.SameVolume && journal.KeepsSource(operationId))
                {
                    checkpoint?.Invoke(TransferExecutionCheckpoint.BeforeMove,item.Index);
                    var copy=IncomingTransferCopies.Prepare(item.File,r=>journal.RecordCopyProgress(operationId,item.Index,r),
                        cancellation,point=>checkpoint?.Invoke(point,item.Index));
                    using(var source=LocalTransferFiles.LockVerifiedDestination(item.File with {Destination=item.File.Source,SameVolume=true},null))
                    using(var target=LocalTransferFiles.LockVerifiedDestination(item.File,copy))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        journal.Mark(operationId,item.Index,DurableTransferPhase.FileVerified,copy);
                        checkpoint?.Invoke(TransferExecutionCheckpoint.BeforeIndex,item.Index);
                        cancellation.ThrowIfCancellationRequested();
                        var committed=journal.CommitVerified(operationId,item.Index);
                        if(committed.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed))throw new IOException(committed.Error);
                    }
                    checkpoint?.Invoke(TransferExecutionCheckpoint.IndexCommitted,item.Index);
                    journal.SettleCopies(journal.Read().Single(x=>x.OperationId==operationId && x.Index==item.Index));
                    continue;
                }
                var receipt=LocalTransferFiles.Move(item.File,
                    copy=>journal.Mark(operationId,item.Index,DurableTransferPhase.CopyVerified,copy),
                    point=>checkpoint?.Invoke(point switch
                    {
                        LocalTransferCheckpoint.BeforeMove=>TransferExecutionCheckpoint.BeforeMove,
                        LocalTransferCheckpoint.AfterCopy=>TransferExecutionCheckpoint.AfterCopy,
                        LocalTransferCheckpoint.CopyRecorded=>TransferExecutionCheckpoint.CopyRecorded,
                        LocalTransferCheckpoint.CopyProgress=>TransferExecutionCheckpoint.CopyProgress,
                        _=>TransferExecutionCheckpoint.AfterMove
                    },item.Index),cancellation);
                journal.Mark(operationId,item.Index,DurableTransferPhase.FileVerified,receipt);
                checkpoint?.Invoke(TransferExecutionCheckpoint.FileRecorded,item.Index);
                Reconcile(item with {Phase=DurableTransferPhase.FileVerified,Receipt=receipt},checkpoint);
            }
            catch(Exception e) when(e is not TransferInterruptionException && e is IOException or UnauthorizedAccessException or OperationCanceledException or NotSupportedException or Microsoft.Data.Sqlite.SqliteException)
            {
                if(!SqliteTransferJournal.Terminal(journal.Read().Single(i=>i.OperationId==operationId&&i.Index==item.Index)))
                    journal.Mark(operationId,item.Index,DurableTransferPhase.Review,error:e.Message);
            }
            var final=journal.Read().Single(x=>x.OperationId==operationId && x.Index==item.Index);
            journal.SettleCopies(final);
        }
        return journal.Read().Where(i=>i.OperationId==operationId).ToArray();
    }

    public IReadOnlyList<DurableTransferItem> Recover(Guid? operationId=null,int? index=null)
    {
        using var lease=journal.AcquireExecutor();
        foreach(var copy in journal.Read().Where(i=>i.Receipt is {OwnedCopies:not null,CleanupFinished:false} &&
            (operationId is null||i.OperationId==operationId)&&(index is null||i.Index==index)))journal.SettleCopies(copy);
        foreach(var item in journal.Read().Where(i=>!SqliteTransferJournal.Terminal(i)&&
            (operationId is null||i.OperationId==operationId)&&(index is null||i.Index==index)))
        {
            try { Reconcile(item,null); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,error:e.Message); }
        }
        return journal.Read();
    }
    private void Reconcile(DurableTransferItem item,Action<TransferExecutionCheckpoint,int>? checkpoint)
    {
        if(item.Receipt?.OwnedCopies is not null)
        { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,error:"Uncommitted copy attempt ended; source retained.");return; }
        // Never execute an unstarted intent on restart. Neither a Shell effect nor cancellation
        // is evidence. Existing originals/new same-name objects always require explicit review.
        if(item.Phase==DurableTransferPhase.Intent)
        { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,error:"Unexecuted intent: repreview before retry.");return; }
        if(LocalTransferFiles.Exists(item.File.Source))
        { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,error:"Source still exists; preserve both paths for review.");return; }
        if(!LocalTransferFiles.VerifyDestination(item.File,item.Receipt))
        { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,error:"Destination identity/integrity is unproven; preserve files.");return; }
        var receipt=item.Receipt??new(LocalTransferFiles.Identity(item.File.Destination),LocalTransferFiles.TreeHash(item.File.Objects));
        using var destinationLease=LocalTransferFiles.LockVerifiedDestination(item.File,receipt);
        journal.Mark(item.OperationId,item.Index,DurableTransferPhase.FileVerified,receipt);
        checkpoint?.Invoke(TransferExecutionCheckpoint.BeforeIndex,item.Index);
        var result=journal.CommitVerified(item.OperationId,item.Index);
        if(result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed))
        { journal.Mark(item.OperationId,item.Index,DurableTransferPhase.Review,receipt,result.Error);return; }
        checkpoint?.Invoke(TransferExecutionCheckpoint.IndexCommitted,item.Index);
    }
}
