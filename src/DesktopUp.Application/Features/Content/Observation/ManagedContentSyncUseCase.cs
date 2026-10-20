using DesktopUp.Domain;

namespace DesktopUp.Application;

// Observes only registered collections. No file mutations, global search or imports.
public sealed class ManagedContentSyncUseCase(IManagedContentObservationStorage storage, IContentObservationFiles files,
    TimeProvider? clock = null, Action? beforeCommit = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<Guid, (string Witness, DateTimeOffset Since)> _stable = [];

    public ManagedContentSyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null)
    {
        if(storage.HasPendingDirectoryOrReturn())return new(0,0,true,[]) { RetryAll = true };
        int removed = 0, renamed = 0; bool pending = false, changed = false, retryAll = false; var notices = new List<string>();
        var retries = new List<ContentObservationRetry>();
        try
        {
            // ObserveReleasedMove requires a durable released=1 receipt AND Missing=false.
            // Its separate user-confirmed stale-removal method is never called by a monitor.
            int nativeRemoved = storage.ReconcileReleasedExits();
            removed += nativeRemoved; changed |= nativeRemoved > 0;
            var nativePending = storage.PendingExits();
            if (storage.HasPendingRelocation())
            { _stable.Clear(); return new(removed, 0, true, []) { IndexChanged = changed, RetryAll = true }; }
            var transfers = storage.PendingTransfers();
            var locations = storage.Locations();
            foreach (var obsolete in _stable.Keys.Except(locations.Select(l => l.Collection.Id)).ToArray()) _stable.Remove(obsolete);
            foreach (var location in locations)
            {
                token.ThrowIfCancellationRequested();
                string directory = location.Collection.StorageLocator!;
                if (directories is not null && !directories.Contains(directory)) continue;
                try
                {
                    if (transfers.Any(r => Overlaps(directory, r.Source) || Overlaps(directory, r.Destination)) ||
                        nativePending.Any(i => i.Items.Any(item => Overlaps(directory,item.Path) &&
                            (item.Missing || !storage.HasReleasedReceipt(i)))))
                    { _stable.Remove(location.Collection.Id); Retry(); continue; }
                    using var root = files.LockDirectory(location.Root.Locator);
                    using var child = files.LockDirectory(directory);
                    storage.ValidateRegistration(location);
                    var state = storage.ReadState();
                    var members = state.ContentCatalog.Memberships.Where(m => m.CollectionId == location.Collection.Id).ToArray();
                    if (members.Length == 0) { _stable.Remove(location.Collection.Id); continue; }
                    var inventory = files.Inventory(directory, token);
                    var baselines = storage.Evidence();
                    var missing = new HashSet<Guid>(); var moves = new Dictionary<Guid, string>();
                    var observed = new List<ManagedContentObservation>();
                    foreach (var member in members)
                    {
                        var item = state.ContentCatalog.Items.Single(i => i.Id == member.ContentItemId);
                        string path = item.Target.Locator;
                        if (item.Source.ProviderId != "local-file-reference-v1" || !files.Contains(directory, path))
                        { notices.Add("受管内容路径不在登记集合中，已保留关联。"); continue; }
                        baselines.TryGetValue(member.Id, out var baseline);
                        string? identity = baseline?.Locator == path ? baseline.Identity : storage.HistoricalIdentity(item.Id, path);
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
                    storage.SaveEvidence(state.ContentVersion, observed);
                    if (missing.Count == 0 && moves.Count == 0) { _stable.Remove(location.Collection.Id); continue; }
                    string witness = StateRequestFingerprint.Compute(new { location, state.ContentVersion, inventory });
                    var now = _clock.GetUtcNow();
                    if (!_stable.TryGetValue(location.Collection.Id, out var previous) || previous.Witness != witness)
                    { _stable[location.Collection.Id] = (witness, now); Retry(); continue; }
                    if (now - previous.Since < TimeSpan.FromSeconds(2)) { Retry(); continue; }
                    beforeCommit?.Invoke(); token.ThrowIfCancellationRequested();
                    // Enumeration/settling do NOT hold the move executor. Take it only for the
                    // final validation and commit, yielding when an actual file operation owns it.
                    using var commitLease = storage.AcquireObservation();
                    // A second complete enumeration immediately before the CAS rejects transient scans.
                    if (!Same(inventory, files.Inventory(directory, token)))
                    { _stable.Remove(location.Collection.Id); Retry(); continue; }
                    storage.ValidateRegistration(location);
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
                    var updatedEvidence = observed
                        .Where(o => moves.ContainsKey(members.Single(m => m.Id == o.MembershipId).ContentItemId))
                        .Select(o => o with { Locator = moves[members.Single(m => m.Id == o.MembershipId).ContentItemId] }).ToArray();
                    var result = storage.Commit(new(
                        new("managed-observation-" + operation.ToString("N"), witness, state.Revision, reads),
                        plan, updatedEvidence));
                    _stable.Remove(location.Collection.Id);
                    if (result.Status == StateCommandStatus.Committed) { changed = true; removed += cardIds.Length; renamed += moves.Count; }
                    else { Retry(); notices.Add(result.Error ?? "状态已变化，稍后重新核对。"); }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                { _stable.Remove(location.Collection.Id); Retry(unavailable: true); notices.Add("收纳集合暂不可核实，已保留关联：" + e.Message); }
                void Retry(bool unavailable = false) { pending = true; retries.Add(new(directory, unavailable)); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { _stable.Clear(); pending = true; retryAll = true; notices.Add("收纳操作进行中或目录不可用，稍后核对：" + e.Message); }
        return new(removed, renamed, pending, notices.Distinct().ToArray()) { IndexChanged = changed, Retries = retries, RetryAll = retryAll };
    }

    private static bool Same(IReadOnlyDictionary<string,string> a, IReadOnlyDictionary<string,string> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var id) && id == p.Value);
    private bool Overlaps(string a, string b) => string.Equals(a,b,StringComparison.OrdinalIgnoreCase) ||
        files.Contains(a,b) || files.Contains(b,a);
}
