using System.Collections.ObjectModel;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record StateReadSet(
    IReadOnlyDictionary<string, long> Posters,
    long? SettingsVersion = null,
    long? CatalogVersion = null)
{
    public static StateReadSet ForPoster(string id, long version) =>
        new(new ReadOnlyDictionary<string, long>(new Dictionary<string, long> { [id] = version }));
    public static StateReadSet ForSettings(long version) => new(
        new ReadOnlyDictionary<string, long>(new Dictionary<string, long>()), SettingsVersion: version);
    public static StateReadSet ForCatalog(long version) => new(
        new ReadOnlyDictionary<string, long>(new Dictionary<string, long>()), CatalogVersion: version);
    public StateReadSet Freeze() => this with
    {
        Posters = new ReadOnlyDictionary<string, long>(
            new SortedDictionary<string, long>(Posters.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal))
    };
}

public sealed record StateReadConflict(string Kind, string Id, long Expected, long? Actual, string Reason);
public sealed record DesktopStateSnapshot(PosterCatalogSnapshot Catalog, SettingsStateSnapshot Settings)
{
    public long Revision => Catalog.Revision;
    public IReadOnlyList<SpatialOperationProof> SpatialOperations { get; init; } = Array.Empty<SpatialOperationProof>();
}

public sealed record CreatePosterCommand(
    string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, PosterDefinition Configuration);
public sealed record ClonePosterCommand(
    string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, string SourcePosterId);
public sealed record RemovePosterCommand(
    string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, string PosterId, bool Confirmed);
public enum BatchPosterCommandKind { SetLocked, SetVisible }
public sealed record BatchPosterCommand(
    string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, IReadOnlyList<string> PosterIds,
    BatchPosterCommandKind Kind, bool Value, bool AllPosters = false);

public sealed record PosterRemovalPreview(
    string PosterId, string Title, long Revision, StateReadSet ReadSet,
    bool IsLastPoster, string? CollectionId, string Consequences);

// Internal application/infrastructure transaction port; UI only sees commands.
public sealed record StateTransactionRequest(
    string RequestId, string Fingerprint, long ObservedRevision, StateReadSet? ReadSet);
public sealed record StateMutationPlan
{
    public IReadOnlyList<PosterDefinition> Updates { get; init; } = Array.Empty<PosterDefinition>();
    public IReadOnlyList<PosterDefinition> Creates { get; init; } = Array.Empty<PosterDefinition>();
    public IReadOnlyList<string> Removes { get; init; } = Array.Empty<string>();
    public DesktopUpSettings? Settings { get; init; }
    public string? ResultPosterId { get; init; }
    public StateCommandStatus? Rejection { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> TouchLayoutIds { get; init; } = Array.Empty<string>();
    public SpatialOperationProof? NewSpatialProof { get; init; }
    public string? ConsumeSpatialOperationId { get; init; }
    public bool HasChanges => Updates.Count + Creates.Count + Removes.Count > 0 || Settings is not null;
    public static StateMutationPlan Reject(StateCommandStatus status, string error) =>
        new() { Rejection = status, Error = error };
}
public sealed record StateTransactionResult(
    StateCommandStatus Status, DesktopStateSnapshot Snapshot, long? CommittedRevision,
    string? ResultPosterId, IReadOnlyList<StateReadConflict> Conflicts, string? Error);
public interface IStateCommandRepository
{
    DesktopStateSnapshot EnsureInitialized(PosterDefinition defaultPoster);
    DesktopStateSnapshot Read();
    StateTransactionResult Execute(StateTransactionRequest request, Func<DesktopStateSnapshot, StateMutationPlan> prepare);
}
