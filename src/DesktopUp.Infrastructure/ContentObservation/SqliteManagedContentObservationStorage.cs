using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class SqliteManagedContentObservationStorage(SqliteStateStore store) : IManagedContentObservationStorage
{
    public bool HasPendingDirectoryOrReturn() => MappedDirectoryIngress.Pending(store) || ContentReturnStore.Pending(store);
    public int ReconcileReleasedExits() => new NativeShellExitJournal(store).Reconcile();
    public IReadOnlyList<ShellExitIntent> PendingExits() => new NativeShellExitJournal(store).Pending();
    public bool HasReleasedReceipt(ShellExitIntent intent) => new NativeShellExitJournal(store).HasReleasedReceipt(intent);
    public bool HasPendingRelocation() => new SqliteStorageRelocationJournal(store).Read().Any(r => r.Closure is null && r.Commit is null);
    public IReadOnlyList<ObservationTransferPaths> PendingTransfers() => new SqliteTransferJournal(store).Read()
        .Where(r => !r.IsFinished).Select(r => new ObservationTransferPaths(r.File.Source, r.File.Destination)).ToArray();
    public IReadOnlyList<RegisteredManagedStorage> Locations() => new SqliteTransferJournal(store).StorageLocations();
    public IDisposable AcquireObservation() => new SqliteTransferJournal(store).AcquireObservationExecutor();
    public void ValidateRegistration(RegisteredManagedStorage location) => ManagedStorageProvisioner.ValidateRegistration(store, location.Root, location.Collection);
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    public IReadOnlyDictionary<Guid, ManagedContentObservation> Evidence() => store.ExecuteRead(ManagedContentObservationStore.Read);
    public string? HistoricalIdentity(Guid contentId, string path) => new SqliteTransferJournal(store).HistoricalManagedIdentity(contentId, path);
    public void SaveEvidence(long contentVersion, IReadOnlyList<ManagedContentObservation> observations) => store.ExecuteWrite((c, t) =>
    {
        if (SqliteContentRepository.ReadVersion(c,t) != contentVersion) throw new IOException("Content changed during observation.");
        foreach (var entry in observations) ManagedContentObservationStore.Put(c,t,entry);
        return 0;
    });
    public StateTransactionResult Commit(ManagedObservationCommit commit) => store.ExecuteWrite((c, t) =>
    {
        // Begin can queue intent before Claim owns the executor, including another collection.
        if (SqliteTransferJournal.ReadItems(c,t).Any(r => !r.IsFinished))
            throw new IOException("A queued transfer has priority over external observation.");
        var result = SqliteStateCommandRepository.ExecuteInTransaction(c,t,
            commit.Request, _ => commit.Mutation, authorizedExternalSyncId: commit.Mutation.ExternalSyncReceiptId);
        if (result.Status == StateCommandStatus.Committed)
            foreach (var entry in commit.UpdatedEvidence) ManagedContentObservationStore.Put(c,t,entry);
        return result;
    });
}
