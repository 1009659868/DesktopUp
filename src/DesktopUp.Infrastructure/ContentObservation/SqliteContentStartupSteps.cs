using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Compatibility adapter to the existing safe executors. No new startup recovery policy.
public sealed class SqliteContentStartupSteps(SqliteStateStore store, StorageLocationUseCase storage,
    ContentReturnUseCase returns) : IContentStartupSteps
{
    public int SettleStorageLocation() => storage.Reconcile().Count;
    public StartupTransferSettlement SettleTransfers()
    {
        var finished = new AutomaticTransferCompletion(new SqliteTransferJournal(store)).Settle();
        return new(finished.Count(x => !x.Item.IsFinished), finished.Count(x => x.Kind == TransferFinishKind.Preserved));
    }
    public void EnsureSingleOwnership() => SingleOwnerMigration.Apply(store);
    public void ReconcileRenames() => new ContentRenameUseCase(new SqliteContentRenameJournal(store), new LocalContentActionFiles()).Reconcile();
    public IReadOnlyList<string> ReconcileReturns() => returns.Reconcile();
    public IReadOnlyList<string> ReconcileCrossPoster() => new CrossPosterTransfer(store).Reconcile();
    public void SettleMappedIngress() => new MappedDirectoryIngress(store).SettleInterrupted();
    public void ReconcileReleasedExits() => new NativeShellExitJournal(store).Reconcile();
}
