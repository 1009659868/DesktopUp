using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record MappedDirectoryScan(string Path, string Identity, IReadOnlyList<MappedContentFact> Items)
{
    public string Fingerprint => StateRequestFingerprint.Compute(new { Path, Identity,
        Items = Items.Select(f => new { f.Item.Target.Locator, f.Item.Kind, f.Identity, f.Stamp }).ToArray() });
}
public sealed record MappedDirectoryState(string PosterId, string Path, bool Available, string? Message = null);
public sealed record MappedDirectorySyncResult(bool Changed, bool Pending, IReadOnlyList<string> Notices)
{
    public IReadOnlyList<MappedDirectoryState> States { get; init; } = [];
    public IReadOnlyList<ContentObservationRetry> Retries { get; init; } = [];
    public bool RetryAll { get; init; }
}
// Scheduling hints only. Failed scans never become evidence of an empty directory.
public sealed record ContentObservationRetry(string Path, bool Unavailable = false);
public sealed record MappedDirectoryEvidence(string Identity, IReadOnlyList<MappedContentObservation> Items);
// Detached data for one observation pass; no live transaction and no reuse after a write.
public sealed record MappedObservationRead(DesktopStateSnapshot State,
    IReadOnlyDictionary<Guid, MappedObservationEntry> Directories);
public sealed record MappedObservationEntry(MappedDirectoryEvidence? Evidence, string? Error = null);
public sealed record ManagedContentObservation(Guid MembershipId, string Locator, string Identity);
public sealed record ManagedContentSyncResult(int RemovedCards, int RenamedItems, bool Pending, IReadOnlyList<string> Notices)
{
    public IReadOnlyList<MappedDirectoryState> MappingStates { get; init; } = [];
    public bool IndexChanged { get; init; }
    public IReadOnlyList<ContentObservationRetry> Retries { get; init; } = [];
    public bool RetryAll { get; init; }
    public bool Changed => IndexChanged || RemovedCards != 0 || RenamedItems != 0;
}
public sealed record MappedCreationPlan(StateMutationPlan Mutation, Guid? CollectionId,
    MappedDirectoryEvidence? Evidence);
public sealed record MappedObservationCommit(StateTransactionRequest Request, StateMutationPlan Mutation,
    Guid CollectionId, MappedDirectoryEvidence Evidence);
public sealed record ManagedObservationCommit(StateTransactionRequest Request, StateMutationPlan Mutation,
    IReadOnlyList<ManagedContentObservation> UpdatedEvidence);

// These operations keep relationship changes AND local evidence in the same atomic commit.
// No SQL connections, transaction callbacks or file mutations cross this boundary.
public interface IMappedDirectoryStorage
{
    IDisposable AcquireCreation();
    IDisposable AcquireObservation();
    void RequireIdle();
    DesktopStateSnapshot ReadState();
    MappedObservationRead ReadObservation(CancellationToken token = default, IReadOnlySet<string>? directories = null);
    void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null);
    MappedDirectoryEvidence Evidence(Guid collection);
    StateTransactionResult Create(CreatePosterCommand command, MappedDirectoryScan scan);
    StateTransactionResult Commit(MappedObservationCommit commit);
}
public sealed record ObservationTransferPaths(string Source, string Destination);
public interface IManagedContentObservationStorage
{
    bool HasPendingDirectoryOrReturn();
    int ReconcileReleasedExits();
    IReadOnlyList<ShellExitIntent> PendingExits();
    bool HasReleasedReceipt(ShellExitIntent intent);
    bool HasPendingRelocation();
    IReadOnlyList<ObservationTransferPaths> PendingTransfers();
    IReadOnlyList<RegisteredManagedStorage> Locations();
    IDisposable AcquireObservation();
    void ValidateRegistration(RegisteredManagedStorage location);
    DesktopStateSnapshot ReadState();
    IReadOnlyDictionary<Guid, ManagedContentObservation> Evidence();
    string? HistoricalIdentity(Guid contentId, string path);
    void SaveEvidence(long contentVersion, IReadOnlyList<ManagedContentObservation> observations);
    StateTransactionResult Commit(ManagedObservationCommit commit);
}
public interface IContentObservationFiles
{
    string Identity(string path);
    string MappedItemIdentity(string path);
    IEnumerable<string> Enumerate(string directory);
    string Normalize(string path);
    IDisposable LockDirectory(string path);
    bool Contains(string parent, string child);
    MappedDirectoryScan ScanMapping(string path, CancellationToken token = default);
    IReadOnlyDictionary<string, string> Inventory(string path, CancellationToken token = default);
}
public sealed record ContentWatchTarget(string Path, bool Recursive);
// Each read is fresh and validated. The monitor may retain routes, never authoritative state/proof.
public interface IContentWatchTargetSource
{
    IReadOnlyList<ContentWatchTarget> Read();
}
public interface IContentObservation
{
    IReadOnlyList<ContentWatchTarget> WatchTargets();
    // Null is a full pass; a set restricts work to matching FRESH registrations, never authorizes paths.
    ManagedContentSyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null);
}
public interface IContentMonitor : IAsyncDisposable
{
    void Invalidate();
    void RefreshTargets();
}
