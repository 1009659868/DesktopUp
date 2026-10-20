using DesktopUp.Domain;

namespace DesktopUp.Application;

// Single owner of creation ordering, settling and mapping observation decisions.
// Missing/replaced/unreadable directories never count as an empty successful scan.
public sealed partial class MappedDirectoryUseCase(IMappedDirectoryStorage storage, IContentObservationFiles files,
    TimeProvider? clock = null, Action? beforeCommit = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<Guid, (string Fingerprint, DateTimeOffset At)> _settling = [];

    public IReadOnlyList<string> ActivePaths() => ActiveCollections(storage.ReadState()).Select(c => c.StorageLocator!).ToArray();
    private static IEnumerable<Collection> ActiveCollections(DesktopStateSnapshot state) => state.ContentCatalog.Collections.Where(c =>
        c.Kind == ContentCollectionKind.Mapped && state.ContentCatalog.Bindings.Any(b => b.CollectionId == c.Id));

    public StateTransactionResult Create(CreatePosterCommand command, string path, CancellationToken token = default)
    {
        using var executor = storage.AcquireCreation();
        storage.RequireIdle();
        var before = storage.ReadState();
        path = files.Normalize(path);
        storage.ValidateRoot(before, path);
        using var root = files.LockDirectory(path);
        var scan = files.ScanMapping(path, token);
        token.ThrowIfCancellationRequested();
        return storage.Create(command, scan);
    }

    public MappedDirectorySyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null)
    {
        bool changed = false, pending = false, retryAll = false;
        var retries = new List<ContentObservationRetry>();
        var notices = new List<string>();
        var states = new List<MappedDirectoryState>();
        try
        {
            storage.RequireIdle();
            var observation = storage.ReadObservation(token, directories);
            var state = observation.State;
            var active = ActiveCollections(state).ToArray();
            foreach (var removed in _settling.Keys.Except(active.Select(c => c.Id)).ToArray()) _settling.Remove(removed);
            foreach (var collection in active)
            {
                token.ThrowIfCancellationRequested();
                if (directories is not null && !directories.Contains(collection.StorageLocator!)) continue;
                var binding = state.ContentCatalog.Bindings.SingleOrDefault(b => b.CollectionId == collection.Id);
                if (binding is null) { _settling.Remove(collection.Id); continue; }
                string owner = binding.PosterId;
                try
                {
                    var entry = observation.Directories[collection.Id];
                    if (entry.Error is { } error) throw new IOException(error);
                    var baseline = entry.Evidence ?? throw new IOException("目录缺少本轮观察证据。");
                    using var root = files.LockDirectory(collection.StorageLocator!);
                    var scan = files.ScanMapping(collection.StorageLocator!, token);
                    if (baseline.Identity != scan.Identity) throw new IOException("目录身份已变化，未接管同名替代目录。");
                    var plan = DirectoryMappingPolicy.Reconcile(state, owner, scan.Items, baseline.Items);
                    states.Add(new(owner, scan.Path, true));
                    if (!plan.Plan.HasChanges) { _settling.Remove(collection.Id); continue; }
                    string witness = scan.Fingerprint + ":" + state.ContentVersion;
                    if (!_settling.TryGetValue(collection.Id, out var prior) || prior.Fingerprint != witness)
                    { _settling[collection.Id] = (witness, _clock.GetUtcNow()); Retry(); continue; }
                    if (_clock.GetUtcNow() - prior.At < TimeSpan.FromSeconds(2)) { Retry(); continue; }
                    beforeCommit?.Invoke(); token.ThrowIfCancellationRequested();
                    using var lease = storage.AcquireObservation();
                    storage.RequireIdle();
                    storage.ValidateRoot(state, collection.StorageLocator!, collection.Id);
                    if (scan.Fingerprint != files.ScanMapping(collection.StorageLocator!, token).Fingerprint) throw new IOException("目录仍在变化，稍后核对。");
                    changed |= CommitObservation(state, collection, scan);
                    _settling.Remove(collection.Id);
                    observation = storage.ReadObservation(token, directories);
                    state = observation.State;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _settling.Remove(collection.Id);
                    notices.Add(collection.Name + "：" + e.Message);
                    states.RemoveAll(s => s.PosterId == owner);
                    states.Add(new(owner, collection.StorageLocator!, false, e.Message));
                    Retry(unavailable: true);
                }
                void Retry(bool unavailable = false)
                { pending = true; retries.Add(new(collection.StorageLocator!, unavailable)); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { pending = true; retryAll = true; notices.Add(e.Message); }
        return new(changed, pending, notices) { States = states, Retries = retries, RetryAll = retryAll };
    }
    public bool CommitObservation(DesktopStateSnapshot state, Collection collection, MappedDirectoryScan scan,
        IReadOnlyList<string>? incoming = null, IncomingContentPlacement? landing = null)
    {
        var baseline = storage.Evidence(collection.Id);
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
        var result = storage.Commit(new(
            new("mapped-observation-" + operation.ToString("N"), scan.Fingerprint, state.Revision, reads),
            plan, collection.Id, new(scan.Identity, update.Observations)));
        if (result.Status != StateCommandStatus.Committed) throw new IOException(result.Error);
        return true;
    }

}
