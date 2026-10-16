using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

internal sealed record CrossPosterIntent(string Kind, Guid Id, ShellExitIntent Source, string TargetPoster,
    MappedNewContent Target, RegisteredManagedStorage? Storage, IncomingContentPlacement? Landing);

// An authenticated in-process OLE handoff, not a second drag protocol. The existing
// directory operation journal stores a tagged intent; no schema change/file replay.
public sealed class CrossPosterTransfer(SqliteStateStore store, IShellDirectoryTransfer? shell = null) : ICrossPosterStorage
{
    internal const string Kind = "cross-poster-v1";
    internal Action<string>? Checkpoint { get; set; }
    public Task<MappedIngressResult> Execute(ShellExitIntent source, string targetId, IReadOnlyList<string> paths,
        RegisteredManagedStorage? storage, IncomingContentPlacement? landing, nint owner, Func<bool> current,
        CancellationToken token = default, long? expectedTargetVersion = null) =>
        new CrossPosterTransferUseCase(this, shell ?? new ShellDirectoryTransfer(), Checkpoint)
            .Execute(source, targetId, paths, storage, landing, owner, current, token, expectedTargetVersion);

    public ICrossPosterAttempt BeginAttempt() => new Attempt(this, store);

    // SQLite receipts, pinned identity and atomic catalog updates only. The application
    // owns iteration, Shell invocation, cancellation and receipt/commit ordering.
    private sealed class Attempt(CrossPosterTransfer adapter, SqliteStateStore store) : ICrossPosterAttempt
    {
        private readonly IDisposable _lease = new SqliteTransferJournal(store).AcquireExecutor();
        private IDisposable? _targetPin;
        private CrossPosterIntent? _intent;
        public CrossPosterPreparation Prepare(ShellExitIntent source, string targetId, IReadOnlyList<string> paths,
            RegisteredManagedStorage? storage, IncomingContentPlacement? landing, Func<bool> current,
            CancellationToken token, long? expectedTargetVersion)
        {
            token.ThrowIfCancellationRequested();
            var journal = new SqliteTransferJournal(store);
            if (!current() || source.PosterId == targetId || MappedDirectoryIngress.Pending(store) || journal.Read().Any(r => !r.IsFinished) ||
                new SqliteStorageRelocationJournal(store).Read().Any(r => r.Commit is null && r.Closure is null)) throw new IOException("当前文件操作尚未结束。");
            var sources = source.Items.Where(i => !i.Missing).ToArray();
            if (sources.Length is < 1 or > 100 || !sources.Select(i => i.Path).SequenceEqual(paths, StringComparer.OrdinalIgnoreCase))
                throw new IOException("拖动来源已变化，未交接。");
            var target = adapter.ResolveTarget(targetId, storage);
            _targetPin = LocalTransferFiles.LockDirectory(target.Path);
            var before = new SqliteStateCommandRepository(store).Read();
            long version = before.Catalog.Posters.Single(p => p.Poster.PosterId == targetId).EntityVersion;
            if (expectedTargetVersion is { } expected && expected != version) throw new IOException("松手后目标海报已变化。");
            RequirePosters(before, source.PosterId, targetId);
            if (before.ContentCatalog.PosterItems.Count(p => p.PosterId == targetId) + sources.Length > ContentLayoutPolicy.MaximumItems)
                throw new IOException("目标海报内容数量已达上限。");
            foreach (var item in sources)
            {
                RequireSource(before, source.PosterId, item);
                new MappedDirectoryService(store).ValidateAction(source.PosterId, before.ContentCatalog.PosterItems.Single(p => p.Id == item.CardId).LayoutItemId);
                if (store.ProtectedTransferPath(item.Path) || item.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase) ||
                    LocalTransferFiles.Contains(item.Path, target.Path) || new MappedDirectoryService(store).ActivePaths().Any(p =>
                        p.Equals(item.Path, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(item.Path, p)))
                    throw new IOException("来源与受保护目录或映射根重叠。");
                MappedDirectoryIngress.ValidateTree(store, item.Path, token);
                if (before.ContentCatalog.PosterItems.Any(p => p.Id != item.CardId &&
                    ContentRelationPolicy.Resolve(before.ContentCatalog, p.PosterId, p.LayoutItemId) is { } child && LocalTransferFiles.Contains(item.Path, child.Item.Target.Locator)))
                    throw new IOException("此文件夹内另有独立卡片；请先移出内部卡片后再移动文件夹，避免留下失效来源。");
            }
            var intent = new CrossPosterIntent(Kind, Guid.NewGuid(), source, targetId, target, storage, landing);
            store.PreserveTransferGuard();
            _intent = intent;
            adapter.WriteIntent(intent);
            return new(target, sources, version);
        }
        public bool IsCurrent(ShellExitItem source, long version)
        {
            var intent = _intent!;
            if (adapter.ResolveTarget(intent.TargetPoster, intent.Storage) != intent.Target) return false;
            var now = ReadState(); RequirePosters(now, intent.Source.PosterId, intent.TargetPoster);
            if (now.Catalog.Posters.Single(p => p.Poster.PosterId == intent.TargetPoster).EntityVersion != version) return false;
            RequireSource(now, intent.Source.PosterId, source); return true;
        }
        public bool IsStillMissing(ShellExitItem source) => new NativeShellExitJournal(store).IsStillMissing(source);
        public CrossPosterResult CaptureDestination(ShellExitItem source, string destination)
        {
            var intent = _intent!;
            if (!string.Equals(Path.GetDirectoryName(destination), intent.Target.Path, StringComparison.OrdinalIgnoreCase) ||
                adapter.ResolveTarget(intent.TargetPoster, intent.Storage) != intent.Target)
                throw new IOException("Windows返回的目标位置无法核实；文件保留。");
            return new(source.CardId, destination, LocalTransferFiles.Identity(destination));
        }
        public void RecordResults(IReadOnlyList<CrossPosterResult> results) => adapter.WriteResults(_intent!, results);
        public void Commit(CrossPosterResult result, IncomingContentPlacement? landing, long expectedTargetVersion) =>
            adapter.Commit(_intent!, result, landing, expectedTargetVersion);
        public DesktopStateSnapshot ReadState() => new SqliteStateCommandRepository(store).Read();
        public void Close() => adapter.Close(_intent!.Id);
        public void Dispose()
        {
            try { _targetPin?.Dispose(); }
            finally { _lease.Dispose(); }
        }
    }

    private MappedNewContent ResolveTarget(string id, RegisteredManagedStorage? storage)
    {
        var state = new SqliteStateCommandRepository(store).Read();
        var binding = state.ContentCatalog.Bindings.SingleOrDefault(b => b.PosterId == id);
        if (binding is not null && state.ContentCatalog.Collections.Any(c => c.Id == binding.CollectionId && c.Kind == ContentCollectionKind.Mapped))
            return new MappedDirectoryService(store).ResolveNewContent(id);
        if (storage is null) throw new IOException("目标收纳位置不可用。");
        ManagedStorageProvisioner.ValidateRegistration(store, storage.Root, storage.Collection);
        return new(storage.Collection.Id, storage.Collection.StorageLocator!, LocalTransferFiles.Identity(storage.Collection.StorageLocator!));
    }
    private static void RequirePosters(DesktopStateSnapshot state, string source, string target)
    {
        if (new[] { source, target }.Any(id => !state.Catalog.Posters.Any(p => p.Poster.PosterId == id && !p.Poster.IsLocked)))
            throw new IOException("来源或目标海报已移除/锁定。");
    }
    private static void RequireSource(DesktopStateSnapshot state, string poster, ShellExitItem source, bool afterMove = false)
    {
        if (!state.ContentCatalog.PosterItems.Any(p => p.Id == source.CardId && p.PosterId == poster &&
            state.ContentCatalog.Memberships.Any(m => m.Id == p.MembershipId && m.ContentItemId == source.ContentId)) ||
            !state.ContentCatalog.Items.Any(i => i.Id == source.ContentId && i.Version == source.Version && i.Target.Locator == source.Path))
            throw new IOException("来源卡片已变化。");
        if (LocalTransferFiles.Identity(Path.GetDirectoryName(source.Path)!) != source.ParentIdentity ||
            !afterMove && LocalTransferFiles.Identity(source.Path) != source.Identity) throw new IOException("来源文件身份已变化。");
    }

    private void Commit(CrossPosterIntent intent, CrossPosterResult result, IncomingContentPlacement? landing, long? expectedTargetVersion = null)
    {
        var source = intent.Source.Items.Single(i => i.CardId == result.CardId);
        if (ResolveTarget(intent.TargetPoster, intent.Storage) != intent.Target ||
            !string.Equals(Path.GetDirectoryName(result.Path), intent.Target.Path, StringComparison.OrdinalIgnoreCase) ||
            LocalTransferFiles.Identity(result.Path) != result.Identity) throw new IOException("目标文件身份已变化，未接管替代文件。");
        bool same = string.Equals(source.Path, result.Path, StringComparison.OrdinalIgnoreCase);
        if (!same && !new NativeShellExitJournal(store).IsStillMissing(source)) throw new IOException("未能证明源文件已移动；保留原卡片。");
        var probe = LocalContentReferenceProbe.Inspect(result.Path);
        if (probe.Item is null) throw new IOException(probe.Message);
        using var pin = new LocalTransferFiles.PinnedObject(result.Path, delete: false);
        store.ExecuteWrite((c, t) =>
        {
            var state = SqliteStateCommandRepository.ReadSnapshot(c, t);
            string request = $"cross-poster-{intent.Id:N}-{source.CardId:N}";
            var reads = new StateReadSet(state.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), ContentVersion: state.ContentVersion);
            var commit = SqliteStateCommandRepository.ExecuteInTransaction(c, t, new(request, StateRequestFingerprint.Compute(result), state.Revision, reads), before =>
            {
                RequireSource(before, intent.Source.PosterId, source, afterMove: !same);
                RequirePosters(before, intent.Source.PosterId, intent.TargetPoster);
                var old = before.ContentCatalog.Items.Single(i => i.Id == source.ContentId);
                var item = same ? old : old with { Target = old.Target with { Locator = result.Path }, Source = old.Source with { Key = result.Path },
                    DisplayName = probe.Item.DisplayName, Version = checked(old.Version + 1) };
                // A late layout edit cannot be overwritten by the old hover coordinates.
                var position = expectedTargetVersion is { } expected && before.Catalog.Posters.Single(p => p.Poster.PosterId == intent.TargetPoster).EntityVersion != expected ? null : landing;
                var plan = CrossPosterPlacement.Prepare(before, source.CardId, intent.TargetPoster, item, position);
                if (plan.Rejection is not null) return plan;
                var catalog = plan.ContentCatalog!;
                // Mapping membership belongs to the source directory, not to the moved file.
                var replacedIds = before.ContentCatalog.PosterItems.Where(p => plan.ExplicitDetachedRelations.Contains(p.Id) && p.Id != source.CardId)
                    .Select(p => before.ContentCatalog.Memberships.Single(m => m.Id == p.MembershipId).ContentItemId).ToHashSet();
                var removed = before.ContentCatalog.Memberships.Where(m => m.Origin == ContentMembershipOrigin.Source &&
                    (m.ContentItemId == source.ContentId && m.CollectionId != intent.Target.CollectionId || replacedIds.Contains(m.ContentItemId)) &&
                    !catalog.PosterItems.Any(p => p.MembershipId == m.Id)).Select(m => m.Id).ToArray();
                var members = catalog.Memberships.Where(m => !removed.Contains(m.Id)).ToList();
                if (intent.Storage is not null && !members.Any(m => m.CollectionId == intent.Target.CollectionId && m.ContentItemId == item.Id))
                    members.Add(new(Guid.NewGuid(), intent.Target.CollectionId, item.Id, ContentMembershipOrigin.Source));
                return plan with { ContentCatalog = catalog with { Memberships = members }, RemovedStorageMemberships = removed, ExternalSyncReceiptId = intent.Id };
            }, authorizedExternalSyncId: intent.Id);
            if (commit.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed)) throw new IOException(commit.Error);
            if (commit.Status == StateCommandStatus.Committed)
            {
                if (intent.Storage is not null) ContentReturnStore.Capture(c, t, commit.Snapshot.ContentCatalog, source.ContentId, source.Path);
                if (intent.Storage is not null) ManagedContentObservationStore.Capture(c, t, commit.Snapshot.ContentCatalog, [source.ContentId]);
                else
                {
                    var evidence = MappedDirectoryStore.Read(c, t, intent.Target.CollectionId) ?? throw new IOException("映射身份记录缺失。");
                    var member = commit.Snapshot.ContentCatalog.Memberships.Single(m => m.CollectionId == intent.Target.CollectionId && m.ContentItemId == source.ContentId);
                    var observed = new MappedContentObservation(member.Id, result.Identity + ":" + File.GetCreationTimeUtc(result.Path).Ticks, $"{probe.Length}:{probe.LastWriteTicks}");
                    MappedDirectoryStore.Put(c, t, intent.Target.CollectionId, evidence with { Items = evidence.Items.Where(i => i.MembershipId != member.Id).Append(observed).ToArray() });
                }
            }
            return 0;
        });
    }

    // Startup may repair only a recorded physical result. It never calls Shell or retries movement.
    public IReadOnlyList<string> Reconcile()
    {
        using var lease = new SqliteTransferJournal(store).AcquireExecutor();
        var pending = store.ExecuteRead((c, t) =>
        {
            using var q = c.CreateCommand(); q.Transaction = t;
            q.CommandText = "SELECT body_json,results_json FROM mapped_directory_ingress WHERE closed=0 AND json_extract(body_json,'$.Kind')=$kind";
            q.Parameters.AddWithValue("$kind", Kind); using var reader = q.ExecuteReader(); var rows = new List<(CrossPosterIntent, CrossPosterResult[])>();
            while (reader.Read()) rows.Add((JsonSerializer.Deserialize<CrossPosterIntent>(reader.GetString(0))!, JsonSerializer.Deserialize<CrossPosterResult[]>(reader.GetString(1))!));
            return rows;
        });
        var errors = new List<string>();
        foreach (var (intent, results) in pending)
        {
            foreach (var result in results)
                try { Commit(intent, result, null); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                { errors.Add(result.Path + "：" + e.Message); }
            foreach (var source in intent.Source.Items.Where(i => !i.Missing && results.All(r => r.CardId != i.CardId)))
                try
                {
                    if (new NativeShellExitJournal(store).IsStillMissing(source))
                        errors.Add($"上次移动未记录完整返回位置，未重试或删除文件；请核对 {source.Path} 与 {intent.Target.Path}。");
                }
                catch (IOException e) { errors.Add(source.Path + "：" + e.Message); }
            Close(intent.Id); // Preserve the evidence, never permanently require a recovery workflow.
        }
        return errors;
    }
    private void WriteIntent(CrossPosterIntent intent) => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "INSERT INTO mapped_directory_ingress(operation_id,body_json) VALUES($id,$body)";
        q.Parameters.AddWithValue("$id", intent.Id.ToString()); q.Parameters.AddWithValue("$body", JsonSerializer.Serialize(intent)); return q.ExecuteNonQuery();
    });
    private void WriteResults(CrossPosterIntent intent, IReadOnlyList<CrossPosterResult> results) => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = "UPDATE mapped_directory_ingress SET results_json=$body WHERE operation_id=$id";
        q.Parameters.AddWithValue("$id", intent.Id.ToString()); q.Parameters.AddWithValue("$body", JsonSerializer.Serialize(results)); return q.ExecuteNonQuery();
    });
    private void Close(Guid id) => store.ExecuteWrite((c, t) =>
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = "UPDATE mapped_directory_ingress SET closed=1 WHERE operation_id=$id";
        q.Parameters.AddWithValue("$id", id.ToString()); return q.ExecuteNonQuery();
    });
}
