using DesktopUp.Domain;

namespace DesktopUp.Application;

// All entries are read in one SQLite snapshot at Revision. CatalogVersion tracks
// membership/order, not unrelated geometry/settings edits. Zero entries is valid.
public sealed record PosterCatalogSnapshot(
    long Revision,
    long CatalogVersion,
    bool ProfileInitialized,
    IReadOnlyList<PosterStateSnapshot> Posters);

public enum PosterCatalogWriteStatus
{
    Committed, Replayed, Conflict, NotFound, ValidationFailed, Failed
}

public sealed record PosterCatalogWriteResult(
    PosterCatalogWriteStatus Status,
    string OperationId,
    long ExpectedRevision,
    long CurrentRevision,
    long? CommittedRevision,
    string? PosterId,
    PosterCatalogSnapshot? Snapshot,
    string? Error)
{
    public bool Succeeded => Status is PosterCatalogWriteStatus.Committed or PosterCatalogWriteStatus.Replayed;
}

public interface IPosterCatalogRepository
{
    PosterCatalogSnapshot List();
    // Core generates PosterId/ThemeInstanceId and appends StackOrder; those three
    // input fields are ignored. CollectionId is a reference only (no file effects).
    PosterCatalogWriteResult Create(long expectedRevision, string operationId, PosterDefinition configuration);
    PosterCatalogWriteResult CloneConfiguration(long expectedRevision, string operationId, string sourcePosterId);
    // Removes the poster configuration, never the collection, theme files or originals.
    PosterCatalogWriteResult Remove(long expectedRevision, string operationId, string posterId);
    // Explicit empty first-run choice. Cannot reset initialization or seed later.
    PosterCatalogWriteResult InitializeEmptyProfile(long expectedRevision, string operationId);
}
