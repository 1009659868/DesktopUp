using DesktopUp.Application;
using DesktopUp.Domain;

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
        if (store.ProtectedTransferPath(path, rootRegistration: true)) throw new IOException("此目录与应用、系统或临时数据重叠，不能映射。");
        var managed = new SqliteTransferJournal(store).StorageLocations();
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

    public MappedDirectoryEvidence Evidence(Guid collection) => store.ExecuteRead((c, t) => MappedDirectoryStore.Read(c, t, collection))
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
