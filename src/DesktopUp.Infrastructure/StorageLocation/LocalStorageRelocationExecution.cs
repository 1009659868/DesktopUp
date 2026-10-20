using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// The proof-producing adapter stays private to Infrastructure. Callers request stages, never supply receipts.
public sealed class LocalStorageRelocationExecution : IStorageRelocationExecution
{
    private readonly SqliteStorageRelocationJournal _journal;
    private readonly StorageRelocationFiles _files;
    private readonly Action<StorageCopyCheckpoint>? _copyCheckpoint;
    private readonly Action<StorageRelocationCheckpoint>? _transactionCheckpoint;

    public LocalStorageRelocationExecution(SqliteStateStore store) : this(store, null, null) { }

    internal LocalStorageRelocationExecution(SqliteStateStore store,
        Action<StorageCopyCheckpoint>? copyCheckpoint, Action<StorageRelocationCheckpoint>? transactionCheckpoint)
    {
        _journal = new(store);
        _files = new(store);
        _copyCheckpoint = copyCheckpoint;
        _transactionCheckpoint = transactionCheckpoint;
    }

    public void Prepare(StorageRelocationPlan plan, string confirmation, CancellationToken token) =>
        _files.Prepare(plan, confirmation, token, _copyCheckpoint);

    public void RecordVerifiedTargets(Guid operation)
    {
        _journal.RecordTargets(operation, _files, _transactionCheckpoint);
        _copyCheckpoint?.Invoke(StorageCopyCheckpoint.Verified);
    }

    public void CommitVerifiedTargets(Guid operation) => _journal.Commit(operation, _files, _transactionCheckpoint);
    public bool HasIntent(Guid operation) => _journal.Read().Any(x => x.Plan.Request.OperationId == operation);
    public StorageRelocationResult FinishObserved(Guid operation, Func<bool>? stopCleanup = null) =>
        _files.Recover(operation, _copyCheckpoint, stopCleanup);
}
