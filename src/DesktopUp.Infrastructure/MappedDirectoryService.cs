using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Existing action/ingress callers share the application mapping use case.
// File creation/identity guards remain here until their owning R1 slice is migrated.
public sealed class MappedDirectoryService
{
    private readonly SqliteStateStore store;
    private readonly SqliteMappedDirectoryStorage _storage;
    private readonly MappedDirectoryUseCase _useCase;
    internal Action? BeforeCommit { get; set; }
    public MappedDirectoryService(SqliteStateStore store, TimeProvider? clock = null)
    {
        this.store = store;
        _storage = new(store);
        _useCase = new(_storage, new LocalContentObservationFiles(), clock, () => BeforeCommit?.Invoke());
    }
    public static MappedDirectoryScan Scan(string input, CancellationToken token = default) =>
        new LocalContentObservationFiles().ScanMapping(input, token);
    public IReadOnlyList<string> ActivePaths() => _useCase.ActivePaths();
    private static IEnumerable<Collection> ActiveCollections(DesktopStateSnapshot state) => state.ContentCatalog.Collections.Where(c =>
        c.Kind == ContentCollectionKind.Mapped && state.ContentCatalog.Bindings.Any(b => b.CollectionId == c.Id));
    private void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null) => _storage.ValidateRoot(state, path, own);
    private void RequireIdle() => _storage.RequireIdle();
    public StateTransactionResult Create(CreatePosterCommand command, string path, CancellationToken token = default) =>
        _useCase.Create(command, path, token);
    public MappedDirectorySyncResult Reconcile(CancellationToken token = default) => _useCase.Reconcile(token);
    private MappedDirectoryEvidence Evidence(Guid collection) => _storage.Evidence(collection);
    internal void CommitIncoming(string posterId, IReadOnlyList<string> paths, IncomingContentPlacement? landing,long expectedVersion)
    {
        var target=ResolveNewContent(posterId);var state=new SqliteStateCommandRepository(store).Read();
        if(state.Catalog.Posters.Single(p=>p.Poster.PosterId==posterId).EntityVersion!=expectedVersion)landing=null;
        var collection=state.ContentCatalog.Collections.Single(c=>c.Id==target.CollectionId);
        using var root=LocalTransferFiles.LockDirectory(target.Path);var scan=Scan(target.Path);
        if(scan.Identity!=target.Identity)throw new IOException("映射目录已变化。");
        CommitObservation(state,collection,scan,paths,landing);
    }
    private bool CommitObservation(DesktopStateSnapshot state, Collection collection, MappedDirectoryScan scan,
        IReadOnlyList<string>? incoming = null, IncomingContentPlacement? landing = null) =>
        _useCase.CommitObservation(state, collection, scan, incoming, landing);
    public MappedNewContent ResolveNewContent(string posterId)
    {
        var state = new SqliteStateCommandRepository(store).Read();
        var binding = state.ContentCatalog.Bindings.Single(b => b.PosterId == posterId);
        var collection = ActiveCollections(state).Single(c => c.Id == binding.CollectionId);
        ValidateRoot(state, collection.StorageLocator!, collection.Id);
        var evidence = Evidence(collection.Id);
        if (LocalTransferFiles.Identity(collection.StorageLocator!) != evidence.Identity) throw new IOException("映射目录已变化。");
        return new(collection.Id, collection.StorageLocator!, evidence.Identity);
    }

    // A stale mapped view must not operate on a same-name replacement directory/item.
    // Ordinary posters keep their existing native action rules.
    public void ValidateAction(string posterId, string? layoutItemId = null, bool allowMissing = false)
    {
        var state = new SqliteStateCommandRepository(store).Read();
        var binding = state.ContentCatalog.Bindings.SingleOrDefault(b => b.PosterId == posterId);
        if (binding is null || !ActiveCollections(state).Any(c => c.Id == binding.CollectionId)) return;
        _ = ResolveNewContent(posterId);
        if (layoutItemId is null) return;
        var relation = ContentRelationPolicy.Resolve(state.ContentCatalog, posterId, layoutItemId)
            ?? throw new IOException("映射内容已变化。");
        var card = state.ContentCatalog.PosterItems.Single(p => p.PosterId == posterId && p.LayoutItemId == layoutItemId);
        var observation = Evidence(binding.CollectionId).Items.SingleOrDefault(i => i.MembershipId == card.MembershipId)
            ?? throw new IOException("映射内容缺少身份记录。");
        string path = relation.Item.Target.Locator;
        if (allowMissing)
        {
            var entries = Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)!).Take(ContentLayoutPolicy.MaximumItems + 1).ToArray();
            if (entries.Length > ContentLayoutPolicy.MaximumItems) throw new IOException("映射目录超出本轮核对预算。");
            if (!entries.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase))) return;
        }
        string identity = LocalTransferFiles.Identity(path) + ":" + File.GetCreationTimeUtc(path).Ticks;
        if (identity != observation.Identity) throw new IOException("映射对象已被替换，正在等待目录同步，请稍后重试。");
    }

    internal string CreateNew(string posterId, NewContentLocation location, NewContentKind kind, string name, byte[] bytes, CancellationToken token)
    {
        using var executor = new SqliteTransferJournal(store).AcquireExecutor(); RequireIdle();
        var state = new SqliteStateCommandRepository(store).Read(); var expected = location.Mapping!;
        if (state.ContentVersion != location.ContentVersion || state.Catalog.Posters.Single(p => p.Poster.PosterId == posterId).EntityVersion != location.PosterVersion)
            throw new IOException("新建窗口打开后状态已变化，请重试。");
        if (state.Catalog.Posters.Single(p => p.Poster.PosterId == posterId).Poster.IsLocked) throw new IOException("请先解除海报锁定。");
        using var directory = LocalTransferFiles.LockDirectory(expected.Path);
        if (ResolveNewContent(posterId) != expected) throw new IOException("映射位置已变化。");
        string path = Path.Combine(expected.Path, name); token.ThrowIfCancellationRequested();
        var created = kind == NewContentKind.Folder ? LocalTransferFiles.CreateOwnedDirectory(path) : LocalTransferFiles.CreateOwnedFile(path, bytes);
        try
        {
            token.ThrowIfCancellationRequested();
            var scan = Scan(expected.Path, token); BeforeCommit?.Invoke();
            token.ThrowIfCancellationRequested();
            using var pin = new LocalTransferFiles.PinnedObject(path, delete: false);
            if (pin.Snapshot() != created || created.Directory && Directory.EnumerateFileSystemEntries(path).Any())
                throw new IOException("新建内容已被外部改变，已保留，未提交新建结果。");
            var collection = state.ContentCatalog.Collections.Single(c => c.Id == expected.CollectionId);
            if (scan.Fingerprint != Scan(expected.Path, token).Fingerprint) throw new IOException("目录仍在变化。");
            CommitObservation(state, collection, scan); return path;
        }
        catch
        {
            // Failed acknowledgement may still have committed. Do not delete an indexed result.
            var now = new SqliteStateCommandRepository(store).Read();
            if (!now.ContentCatalog.PosterItems.Any(p => p.PosterId == posterId &&
                ContentRelationPolicy.Resolve(now.ContentCatalog, posterId, p.LayoutItemId)!.Item.Target.Locator == path))
                ContentCreationService.RemoveExactEmptyOrFile(path, created);
            throw;
        }
    }
}
