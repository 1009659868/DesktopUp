using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class SqliteManagedIngressStorage(SqliteStateStore store) : IManagedIngressStorage
{
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    public IManagedIngressItem Prepare(DesktopStateSnapshot state, string posterId, string path,
        RegisteredManagedStorage storage, IncomingContentPlacement? landing) =>
        new Item(new SqliteTransferJournal(store),
            DirectTransferPreparation.Incoming(state, posterId, [path], storage, landing), storage.Collection.Id);

    private sealed class Item(SqliteTransferJournal journal, LocalTransferPreview preview, Guid collectionId) : IManagedIngressItem
    {
        public Guid ContentId => preview.Plan.Entries[0].Entry.Item.Id;
        public string Destination => preview.Files[0].Destination;
        public void RecordIntent() => journal.Begin(preview.Plan, preview.Files, preview.Plan.Fingerprint, collectionId);
        public TransferFinishKind Complete(CancellationToken token) =>
            new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId, token).Single().Kind;
    }
}
