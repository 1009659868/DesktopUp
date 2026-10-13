using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed record ManagedContentSyncResult(int RemovedCards, int RenamedItems, bool Pending, IReadOnlyList<string> Notices)
{
    public bool IndexChanged { get; init; }
    public bool Changed => IndexChanged || RemovedCards != 0 || RenamedItems != 0;
}

// Observes only this profile's registered collections. No file writes, global search or imports.
// Serialized by the same executor lease as incoming/outgoing/relocation; DB authorization stays internal.
public sealed class ManagedContentSynchronizer(SqliteStateStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<Guid, (string Witness, DateTimeOffset Since)> _stable = [];
    internal Action? BeforeCommit { get; set; }
    internal Action<string>? Enumerating { get; set; }

    public ManagedContentSyncResult Reconcile(CancellationToken token = default)
    {
        int removed = 0, renamed = 0; bool pending = false, changed = false; var notices = new List<string>();
        var journal = new SqliteTransferJournal(store);
        try
        {
            var native = new NativeShellExitJournal(store);
            // ObserveReleasedMove requires a durable released=1 receipt AND Missing=false.
            // Its separate user-confirmed stale-removal method is never called by a monitor.
            int nativeRemoved = native.Reconcile();
            removed += nativeRemoved; changed |= nativeRemoved > 0;
            var nativePending = native.Pending();
            if (new SqliteStorageRelocationJournal(store).Read().Any(r => r.Closure is null && r.Commit is null))
            { _stable.Clear(); return new(0, 0, true, []); }
            var transfers = journal.Read().Where(r => !r.IsFinished).ToArray();
            var locations = journal.StorageLocations();
            foreach (var obsolete in _stable.Keys.Except(locations.Select(l => l.Collection.Id)).ToArray()) _stable.Remove(obsolete);
            foreach (var location in locations)
            {
                token.ThrowIfCancellationRequested();
                string directory = location.Collection.StorageLocator!;
                try
                {
                    if (transfers.Any(r => Overlaps(directory, r.File.Source) || Overlaps(directory, r.File.Destination)) ||
                        nativePending.Any(i => i.Items.Any(item => Overlaps(directory,item.Path) &&
                            (item.Missing || !native.HasReleasedReceipt(i)))))
                    { _stable.Remove(location.Collection.Id); pending = true; continue; }
                    using var root = LocalTransferFiles.LockDirectory(location.Root.Locator);
                    using var child = LocalTransferFiles.LockDirectory(directory);
                    ManagedStorageProvisioner.ValidateRegistration(store, location.Root, location.Collection);
                    var state = new SqliteStateCommandRepository(store).Read();
                    var members = state.ContentCatalog.Memberships.Where(m => m.CollectionId == location.Collection.Id).ToArray();
                    if (members.Length == 0) { _stable.Remove(location.Collection.Id); continue; }
                    var inventory = Inventory(directory, token);
                    var baselines = store.ExecuteRead(ManagedContentObservationStore.Read);
                    var missing = new HashSet<Guid>(); var moves = new Dictionary<Guid, string>();
                    var observed = new List<ManagedContentObservation>();
                    foreach (var member in members)
                    {
                        var item = state.ContentCatalog.Items.Single(i => i.Id == member.ContentItemId);
                        string path = item.Target.Locator;
                        if (item.Source.ProviderId != "local-file-reference-v1" || !LocalTransferFiles.Contains(directory, path))
                        { notices.Add("受管内容路径不在登记集合中，已保留关联。"); continue; }
                        baselines.TryGetValue(member.Id, out var baseline);
                        string? identity = baseline?.Locator == path ? baseline.Identity : journal.HistoricalManagedIdentity(item.Id, path);
                        // Upgrade old profiles conservatively. Existing file establishes a baseline; absent
                        // files without a persisted transfer identity are not guessed to be deletions.
                        if (identity is null && inventory.TryGetValue(path, out var present)) identity = present;
                        if (identity is null) { notices.Add("缺少旧内容身份凭证，已保留关联。"); continue; }
                        observed.Add(new(member.Id, path, identity));
                        if (inventory.TryGetValue(path, out var atPath) && atPath == identity) continue;
                        var matches = inventory.Where(p => p.Value == identity).Select(p => p.Key).ToArray();
                        if (matches.Length == 1) moves.Add(item.Id, matches[0]);
                        else if (matches.Length == 0 && !inventory.ContainsKey(path)) missing.Add(item.Id);
                        else notices.Add("内容身份或同名对象不明确，已保留关联。");
                    }
                    // Baselines belong to the matching content revision, never a newer repair/relocation.
                    store.ExecuteWrite((c,t) =>
                    {
                        if (SqliteContentRepository.ReadVersion(c,t) != state.ContentVersion) throw new IOException("Content changed during observation.");
                        foreach (var entry in observed) ManagedContentObservationStore.Put(c,t,entry);
                        return 0;
                    });
                    if (missing.Count == 0 && moves.Count == 0) { _stable.Remove(location.Collection.Id); continue; }
                    string witness = StateRequestFingerprint.Compute(new { location, state.ContentVersion, inventory });
                    var now = _clock.GetUtcNow();
                    if (!_stable.TryGetValue(location.Collection.Id, out var previous) || previous.Witness != witness)
                    { _stable[location.Collection.Id] = (witness, now); pending = true; continue; }
                    if (now - previous.Since < TimeSpan.FromSeconds(2)) { pending = true; continue; }
                    BeforeCommit?.Invoke(); token.ThrowIfCancellationRequested();
                    // Enumeration/settling do NOT hold the move executor. Take it only for the
                    // final validation and commit, yielding when an actual file operation owns it.
                    using var commitLease = journal.AcquireObservationExecutor();
                    // A second complete enumeration immediately before the CAS rejects transient scans.
                    if (!Same(inventory, Inventory(directory, token)))
                    { _stable.Remove(location.Collection.Id); pending = true; continue; }
                    ManagedStorageProvisioner.ValidateRegistration(store, location.Root, location.Collection);
                    var removedMembers = members.Where(m => missing.Contains(m.ContentItemId)).Select(m => m.Id).ToArray();
                    var related = state.ContentCatalog.Memberships.Where(m => missing.Contains(m.ContentItemId)).Select(m => m.Id).ToHashSet();
                    var cardIds = state.ContentCatalog.PosterItems.Where(p => related.Contains(p.MembershipId)).Select(p => p.Id).ToArray();
                    var plan = ContentRelationRemoval.Prepare(state, cardIds);
                    if (plan.Rejection is not null) throw new IOException(plan.Error);
                    var items = plan.ContentCatalog!.Items.Select(i => moves.TryGetValue(i.Id, out var target)
                        ? i with { Target = i.Target with { Locator = target }, Source = i.Source with { Key = target }, Version = checked(i.Version + 1) } : i).ToArray();
                    var updates = plan.Updates.ToDictionary(p => p.PosterId);
                    foreach (var id in moves.Keys.SelectMany(id => ContentReferencePolicy.AffectedPosters(state.ContentCatalog, id)).Distinct())
                        updates.TryAdd(id, state.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster);
                    var operation = Guid.NewGuid();
                    plan = plan with { ContentCatalog = plan.ContentCatalog with { Items = items,
                        Memberships = plan.ContentCatalog.Memberships.Where(m => !removedMembers.Contains(m.Id)).ToArray() },
                        RemovedStorageMemberships = removedMembers, Updates = updates.Values.ToArray(), ExternalSyncReceiptId = operation };
                    var reads = new StateReadSet(updates.Keys.ToDictionary(id => id,
                        id => state.Catalog.Posters.Single(p => p.Poster.PosterId == id).EntityVersion), ContentVersion: state.ContentVersion);
                    var result = store.ExecuteWrite((c,t) =>
                    {
                        // Intent can be queued before its executor starts; do not invalidate its
                        // content read-set between Begin and Claim, even for a different collection.
                        if(SqliteTransferJournal.ReadItems(c,t).Any(r=>!r.IsFinished))
                            throw new IOException("A queued transfer has priority over external observation.");
                        var committed = SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                            new("managed-observation-" + operation.ToString("N"), witness, state.Revision, reads),
                            _ => plan, authorizedExternalSyncId: operation);
                        if (committed.Status == StateCommandStatus.Committed)
                            foreach (var old in observed.Where(o => moves.ContainsKey(members.Single(m => m.Id == o.MembershipId).ContentItemId)))
                                ManagedContentObservationStore.Put(c,t,old with { Locator = moves[members.Single(m => m.Id == old.MembershipId).ContentItemId] });
                        return committed;
                    });
                    _stable.Remove(location.Collection.Id);
                    if (result.Status == StateCommandStatus.Committed) { changed = true; removed += cardIds.Length; renamed += moves.Count; }
                    else { pending = true; notices.Add(result.Error ?? "状态已变化，稍后重新核对。"); }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                { _stable.Remove(location.Collection.Id); notices.Add("收纳集合暂不可核实，已保留关联：" + e.Message); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { _stable.Clear(); pending = true; notices.Add("收纳操作进行中或目录不可用，稍后核对：" + e.Message); }
        return new(removed, renamed, pending, notices.Distinct().ToArray()) { IndexChanged = changed };
    }

    private Dictionary<string,string> Inventory(string root, CancellationToken token)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        void Visit(string directory)
        {
            token.ThrowIfCancellationRequested(); Enumerating?.Invoke(directory);
            // Explicitly do not skip inaccessible entries or traverse links. Any incomplete tree defers.
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new IOException("收纳集合核对超时，本轮保留关系。");
                if (result.Count >= LocalTransferFiles.MaximumTreeEntries) throw new IOException("收纳集合超过本轮核对预算。");
                var attributes = File.GetAttributes(entry);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.Encrypted)) != 0)
                    throw new IOException("收纳集合包含不可安全核对的路径。");
                result.Add(entry, LocalTransferFiles.Identity(entry));
                if ((attributes & FileAttributes.Directory) != 0) Visit(entry);
            }
        }
        Visit(root); return result;
    }
    private static bool Same(Dictionary<string,string> a, Dictionary<string,string> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var id) && id == p.Value);
    private static bool Overlaps(string a, string b) => string.Equals(a,b,StringComparison.OrdinalIgnoreCase) ||
        LocalTransferFiles.Contains(a,b) || LocalTransferFiles.Contains(b,a);
}
