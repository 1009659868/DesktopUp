using DesktopUp.Domain;

namespace DesktopUp.Application;

// Persisted shapes keep their field names; no schema or journal format changes.
public sealed record PosterContentReturnResult(bool Removed, int Returned,
    IReadOnlyList<string> Destinations, IReadOnlyList<string> Errors);
public sealed record ContentReturnIntent(Guid Id, string PosterId, ShellExitItem Source,
    string Directory, string DirectoryIdentity)
{
    public const string UninstallScope = "@uninstall";
}
public sealed record ContentReturnReceipt(string Path, string Identity);
public sealed record PendingContentReturn(ContentReturnIntent Intent, ContentReturnReceipt? Receipt);
public sealed record UninstallContentInventory(long Revision, IReadOnlyList<ShellExitItem> Files,
    IReadOnlyList<string> Locations);
public sealed record UninstallContentResult(bool Completed, IReadOnlyList<string> Returned,
    IReadOnlyList<string> Retained, IReadOnlyList<string> Errors);

// Identity/ownership guards and atomic receipts stay together in the storage adapter.
// This is not a general repository: callers cannot submit arbitrary SQL or mutation callbacks.
public interface IContentReturnStorage
{
    IDisposable AcquireExecution();
    DesktopStateSnapshot ReadState();
    void BackfillOrigins();
    string? OriginalPath(Guid itemId);
    ShellExitItem PreparePosterSource(DesktopStateSnapshot state, PosterItem card, CancellationToken token);
    bool SourceCurrent(ContentReturnIntent intent);
    bool IsStillMissing(ShellExitItem source);
    void WriteIntent(ContentReturnIntent intent);
    void RecordResult(Guid id, ContentReturnReceipt receipt);
    void Close(Guid id);
    IReadOnlyList<PendingContentReturn> Pending();
    DesktopStateSnapshot Commit(ContentReturnIntent intent, ContentReturnReceipt receipt);
    StateCommandResult RemovePoster(RemovePosterCommand command);
    IReadOnlyList<string> RetainedLocations();
    UninstallContentInventory Inventory();
}
public interface IContentReturnFiles
{
    string DesktopDirectory { get; }
    string Identity(string path);
    string? UsableDestination(string? path, ContentCatalogSnapshot catalog);
}
public interface IUninstallProfile
{
    // False only for a never-started profile; does not create a default database/poster.
    bool OpenExisting();
    void SaveReport(string outcome, IEnumerable<string> returned,
        IEnumerable<string> retained, IEnumerable<string> errors);
}
