using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed record MappedDirectoryScan(string Path, string Identity, IReadOnlyList<MappedContentFact> Items)
{
    public string Fingerprint => StateRequestFingerprint.Compute(new { Path, Identity,
        Items = Items.Select(f => new { f.Item.Target.Locator, f.Item.Kind, f.Identity, f.Stamp }).ToArray() });
}
public sealed record MappedDirectoryState(string PosterId,string Path,bool Available,string? Message=null);
public sealed record MappedDirectorySyncResult(bool Changed, bool Pending, IReadOnlyList<string> Notices)
{ public IReadOnlyList<MappedDirectoryState> States { get; init; } = []; }
public sealed record MappedNewContent(Guid CollectionId, string Path, string Identity);

// Borrowed roots have only local DB evidence; no owner markers or file moves. Missing,
// unreadable, replaced or oversized directories never become an empty successful scan.
public sealed class MappedDirectoryService(SqliteStateStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<Guid, (string Fingerprint, DateTimeOffset At)> _settling = [];
    internal Action? BeforeCommit { get; set; }
    public static MappedDirectoryScan Scan(string input, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        string path = LocalTransferFiles.Normalize(input);
        using var root = LocalTransferFiles.LockDirectory(path);
        string identity = LocalTransferFiles.Identity(path);
        var timer = System.Diagnostics.Stopwatch.StartNew(); var facts = new List<MappedContentFact>();
        int count = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            token.ThrowIfCancellationRequested();
            if (++count > ContentLayoutPolicy.MaximumItems || timer.Elapsed > TimeSpan.FromSeconds(3)) throw new IOException("映射目录超过1000个直接子项或本轮读取预算，已保留原状态。");
            var attributes = File.GetAttributes(entry);
            if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            var preview = LocalContentReferenceProbe.Inspect(entry);
            var item = preview.Item ?? throw new IOException(preview.Message);
            string objectId = LocalTransferFiles.Identity(entry) + ":" + File.GetCreationTimeUtc(entry).Ticks;
            facts.Add(new(item, objectId, $"{preview.Length}:{preview.LastWriteTicks}"));
        }
        return new(path, identity, facts.OrderBy(f => f.Item.Target.Locator, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public IReadOnlyList<string> ActivePaths() => ActiveCollections(new SqliteStateCommandRepository(store).Read()).Select(c => c.StorageLocator!).ToArray();
    private static IEnumerable<Collection> ActiveCollections(DesktopStateSnapshot state) => state.ContentCatalog.Collections.Where(c =>
        c.Kind == ContentCollectionKind.Mapped && state.ContentCatalog.Bindings.Any(b => b.CollectionId == c.Id));

    private void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null)
    {
        if (store.ProtectedTransferPath(path, rootRegistration: true)) throw new IOException("此目录与应用、系统或临时数据重叠，不能映射。");
        var managed = new SqliteTransferJournal(store).StorageLocations();
        if (managed.Any(s => Overlap(path, s.Root.Locator)) || ActiveCollections(state).Any(c => c.Id != own && Overlap(path, c.StorageLocator!)))
            throw new IOException("目录与已有收纳位置或目录映射重叠，请选择独立目录。");
    }
    private static bool Overlap(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(a, b) || LocalTransferFiles.Contains(b, a);
    private void RequireIdle()
    {
        if (MappedDirectoryIngress.Pending(store)) throw new IOException("目录文件操作进行中，稍后同步。");
        if (new SqliteTransferJournal(store).Read().Any(r => !r.IsFinished) || new NativeShellExitJournal(store).Pending().Count != 0 ||
            new SqliteStorageRelocationJournal(store).Read().Any(r => r.Commit is null && r.Closure is null)) throw new IOException("文件操作进行中，稍后核对映射。");
    }

    public StateTransactionResult Create(CreatePosterCommand command, string path, CancellationToken token = default)
    {
        using var executor = new SqliteTransferJournal(store).AcquireExecutor(); RequireIdle();
        var before = new SqliteStateCommandRepository(store).Read(); path = LocalTransferFiles.Normalize(path);
        ValidateRoot(before, path);
        using var root = LocalTransferFiles.LockDirectory(path); var scan = Scan(path, token);
        token.ThrowIfCancellationRequested();
        return store.ExecuteWrite((c, t) =>
        {
            Guid? newCollection = null; IReadOnlyList<MappedContentObservation>? observations = null;
            var result = SqliteStateCommandRepository.ExecuteInTransaction(c, t,
                new(command.RequestId, StateRequestFingerprint.Compute(new { command, scan.Fingerprint }), command.ExpectedRevision, command.ReadSet), state =>
                {
                    var create = DirectoryMappingPolicy.PreparePoster(command, state); if (create.Rejection is not null) return create;
                    var poster = create.Creates.Single(); var collection = new Collection(Guid.NewGuid(), Path.GetFileName(path), ContentCollectionKind.Mapped, path, ContentStorageOwnership.Borrowed);
                    newCollection = collection.Id;
                    if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var emptyLayout, out var layoutError))
                        throw new IOException(layoutError);
                    poster = poster with { CollectionId = collection.Id.ToString(), ContentLayoutJson = ContentLayoutPolicy.Serialize(emptyLayout!) };
                    var catalog = state.ContentCatalog with { Collections = state.ContentCatalog.Collections.Append(collection).ToArray(),
                        Bindings = state.ContentCatalog.Bindings.Append(new ContentPosterBinding(poster.PosterId, collection.Id)).ToArray() };
                    // Run the same reconciliation for the initial empty mapping, in the SAME transaction.
                    var synthetic = new PosterStateSnapshot(state.Revision, state.Revision, poster, DateTimeOffset.UtcNow);
                    var prepared = DirectoryMappingPolicy.Reconcile(state with { ContentCatalog = catalog,
                        Catalog = state.Catalog with { Posters = state.Catalog.Posters.Append(synthetic).ToArray() } }, poster.PosterId, scan.Items, []);
                    observations = prepared.Observations;
                    return create with { Creates = [prepared.Plan.Updates.SingleOrDefault() ?? poster], ContentCatalog = prepared.Plan.ContentCatalog ?? catalog };
                });
            if (result.Status == StateCommandStatus.Committed && newCollection is { } id)
                MappedDirectoryStore.Put(c, t, id, new(scan.Identity, observations!));
            return result;
        });
    }

    public MappedDirectorySyncResult Reconcile(CancellationToken token = default)
    {
        bool changed = false, pending = false; var notices = new List<string>();
        var states=new List<MappedDirectoryState>();
        try
        {
            RequireIdle();
            var state = new SqliteStateCommandRepository(store).Read();
            var active = ActiveCollections(state).ToArray();
            foreach (var removed in _settling.Keys.Except(active.Select(c => c.Id)).ToArray()) _settling.Remove(removed);
            foreach (var collection in active)
            {
                token.ThrowIfCancellationRequested();
                string owner=state.ContentCatalog.Bindings.Single(b=>b.CollectionId==collection.Id).PosterId;
                try
                {
                    ValidateRoot(state, collection.StorageLocator!, collection.Id);
                    using var root = LocalTransferFiles.LockDirectory(collection.StorageLocator!);
                    var baseline = Evidence(collection.Id); var scan = Scan(collection.StorageLocator!, token);
                    if (baseline.Identity != scan.Identity) throw new IOException("目录身份已变化，未接管同名替代目录。");
                    string posterId = state.ContentCatalog.Bindings.Single(b => b.CollectionId == collection.Id).PosterId;
                    var plan = DirectoryMappingPolicy.Reconcile(state, posterId, scan.Items, baseline.Items);
                    states.Add(new(owner,scan.Path,true));
                    if (!plan.Plan.HasChanges) { _settling.Remove(collection.Id); continue; }
                    string witness = scan.Fingerprint + ":" + state.ContentVersion;
                    if (!_settling.TryGetValue(collection.Id, out var prior) || prior.Fingerprint != witness)
                    { _settling[collection.Id] = (witness, _clock.GetUtcNow()); pending = true; continue; }
                    if (_clock.GetUtcNow() - prior.At < TimeSpan.FromSeconds(2)) { pending = true; continue; }
                    BeforeCommit?.Invoke(); token.ThrowIfCancellationRequested();
                    using var lease = new SqliteTransferJournal(store).AcquireObservationExecutor(); RequireIdle();
                    if (scan.Fingerprint != Scan(collection.StorageLocator!, token).Fingerprint) throw new IOException("目录仍在变化，稍后核对。");
                    changed |= CommitObservation(state, collection, scan);
                    _settling.Remove(collection.Id); state = new SqliteStateCommandRepository(store).Read();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                { _settling.Remove(collection.Id); notices.Add(collection.Name + "：" + e.Message);
                    states.RemoveAll(s=>s.PosterId==owner);states.Add(new(owner,collection.StorageLocator!,false,e.Message));pending=true; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { pending = true; notices.Add(e.Message); }
        return new(changed, pending, notices) { States=states };
    }
    private MappedDirectoryEvidence Evidence(Guid collection) => store.ExecuteRead((c, t) => MappedDirectoryStore.Read(c, t, collection))
        ?? throw new IOException("目录缺少本机身份记录，未自动认领。请删除映射海报后重新选择目录。");
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
        IReadOnlyList<string>? incoming = null, IncomingContentPlacement? landing = null)
    {
        var baseline = Evidence(collection.Id);
        if (baseline.Identity != scan.Identity) throw new IOException("映射目录已被替换。");
        string poster = state.ContentCatalog.Bindings.Single(b => b.CollectionId == collection.Id).PosterId;
        var update = DirectoryMappingPolicy.Reconcile(state, poster, scan.Items, baseline.Items);
        if (!update.Plan.HasChanges) return false;
        Guid operation = Guid.NewGuid(); var plan = update.Plan with { ExternalSyncReceiptId = operation };
        if(landing is not null && incoming is not null)
        {
            var updated=plan.Updates.Single();
            if(!ContentLayoutPolicy.TryRead(updated.ContentLayoutJson,poster,out var document,out var error))throw new IOException(error);
            foreach(string path in incoming)
            {
                var card=plan.ContentCatalog!.PosterItems.FirstOrDefault(p=>p.PosterId==poster &&
                    ContentRelationPolicy.Resolve(plan.ContentCatalog,poster,p.LayoutItemId)!.Item.Target.Locator==path);
                if(card is null || state.ContentCatalog.PosterItems.Any(p=>p.Id==card.Id))continue;
                document=landing.Apply(document!,card.LayoutItemId);landing=landing.Advance(document,card.LayoutItemId);
            }
            plan=plan with {Updates=[updated with {ContentLayoutJson=ContentLayoutPolicy.Serialize(document!)}]};
        }
        var reads = new StateReadSet(new Dictionary<string, long> { [poster] = state.Catalog.Posters.Single(p => p.Poster.PosterId == poster).EntityVersion }, ContentVersion: state.ContentVersion);
        var result = store.ExecuteWrite((c, t) =>
        {
            var committed = SqliteStateCommandRepository.ExecuteInTransaction(c, t,
                new("mapped-observation-" + operation.ToString("N"), scan.Fingerprint, state.Revision, reads), _ => plan, authorizedExternalSyncId: operation);
            if (committed.Status == StateCommandStatus.Committed) MappedDirectoryStore.Put(c, t, collection.Id, new(scan.Identity, update.Observations));
            return committed;
        });
        if (result.Status != StateCommandStatus.Committed) throw new IOException(result.Error);
        return true;
    }

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
