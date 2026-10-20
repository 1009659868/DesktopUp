using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Identity/ownership guards and atomic publication; application owns scan/retry decisions.
public sealed class SqliteMappedDirectoryStorage(SqliteStateStore store) : IMappedDirectoryStorage
{
    public IDisposable AcquireCreation() => new SqliteTransferJournal(store).AcquireExecutor();
    public IDisposable AcquireObservation() => new SqliteTransferJournal(store).AcquireObservationExecutor();
    public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
    private static IEnumerable<Collection> ActiveCollections(DesktopStateSnapshot state) => state.ContentCatalog.Collections.Where(c =>
        c.Kind == ContentCollectionKind.Mapped && state.ContentCatalog.Bindings.Any(b => b.CollectionId == c.Id));

    public void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null)
    {
        RequirePermittedRoot(path);
        ValidateRootRelations(state, path, own, new SqliteTransferJournal(store).StorageLocations());
    }
    private void RequirePermittedRoot(string path)
    {
        if (store.ProtectedTransferPath(path, rootRegistration: true))
            throw new IOException("此目录与应用、系统或临时数据重叠，不能映射。");
    }
    private static void ValidateRootRelations(DesktopStateSnapshot state, string path, Guid? own,
        IReadOnlyList<RegisteredManagedStorage> managed)
    {
        if (managed.Any(s => Overlap(path, s.Root.Locator)) || ActiveCollections(state).Any(c => c.Id != own && Overlap(path, c.StorageLocator!)))
            throw new IOException("目录与已有收纳位置或目录映射重叠，请选择独立目录。");
    }
    private static bool Overlap(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(a, b) || LocalTransferFiles.Contains(b, a);
    public void RequireIdle()
    {
        if (MappedDirectoryIngress.Pending(store)) throw new IOException("目录文件操作进行中，稍后同步。");
        if (new SqliteTransferJournal(store).Read().Any(r => !r.IsFinished) || new NativeShellExitJournal(store).Pending().Count != 0 ||
            new SqliteStorageRelocationJournal(store).Read().Any(r => r.Commit is null && r.Closure is null)) throw new IOException("文件操作进行中，稍后核对映射。");
    }

    // All data is materialized and the transaction is closed before any filesystem scan.
    public MappedObservationRead ReadObservation(CancellationToken token = default, IReadOnlySet<string>? directories = null) => store.ExecuteRead((c, t) =>
    {
        token.ThrowIfCancellationRequested();
        var state = SqliteStateCommandRepository.ReadSnapshot(c, t);
        var active = ActiveCollections(state).Where(c => directories is null || directories.Contains(c.StorageLocator!)).ToArray();
        var entries = new Dictionary<Guid, MappedObservationEntry>();
        if (active.Length == 0) return new MappedObservationRead(state, entries);
        var managed = SqliteTransferJournal.ReadStorageLocations(c, t, state.ContentCatalog);
        foreach (var collection in active)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                RequirePermittedRoot(collection.StorageLocator!);
                ValidateRootRelations(state, collection.StorageLocator!, collection.Id, managed);
                entries.Add(collection.Id, new(ReadEvidence(c, t, collection.Id)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                entries.Add(collection.Id, new(null, e.Message));
            }
        }
        return new MappedObservationRead(state, entries);
    });

    public MappedDirectoryEvidence Evidence(Guid collection) => store.ExecuteRead((c, t) => ReadEvidence(c, t, collection));
    private static MappedDirectoryEvidence ReadEvidence(SqliteConnection c, SqliteTransaction t, Guid collection) =>
        MappedDirectoryStore.Read(c, t, collection)
        ?? throw new IOException("目录缺少本机身份记录，未自动认领。请删除映射海报后重新选择目录。");

    public StateTransactionResult Create(CreatePosterCommand command, MappedDirectoryScan scan) => store.ExecuteWrite((c, t) =>
    {
        MappedCreationPlan? prepared = null;
        var result = SqliteStateCommandRepository.ExecuteInTransaction(c, t,
            new(command.RequestId, StateRequestFingerprint.Compute(new { command, scan.Fingerprint }), command.ExpectedRevision, command.ReadSet),
            state => (prepared = MappedDirectoryCreation.Prepare(command, state, scan)).Mutation);
        if (result.Status == StateCommandStatus.Committed && prepared?.CollectionId is { } id)
            MappedDirectoryStore.Put(c, t, id, prepared.Evidence!);
        return result;
    });

    public StateTransactionResult Commit(MappedObservationCommit commit) => store.ExecuteWrite((c, t) =>
    {
        var result = SqliteStateCommandRepository.ExecuteInTransaction(c, t,
            commit.Request, _ => commit.Mutation, authorizedExternalSyncId: commit.Mutation.ExternalSyncReceiptId);
        if (result.Status == StateCommandStatus.Committed)
            MappedDirectoryStore.Put(c, t, commit.CollectionId, commit.Evidence);
        return result;
    });
}
