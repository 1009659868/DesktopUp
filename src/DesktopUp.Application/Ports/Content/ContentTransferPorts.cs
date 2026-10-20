using DesktopUp.Domain;

namespace DesktopUp.Application;

// Wire shapes are unchanged: these receipts also exist in already-written journals.
public sealed record ShellExitItem(Guid CardId, Guid ContentId, long Version, string Path,
    string ParentIdentity, string? Identity, bool Missing);
public sealed record ShellExitIntent(Guid Id, string PosterId, DateTimeOffset Created, IReadOnlyList<ShellExitItem> Items)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExplicitDelete { get; init; }
}
public sealed record ShellExitCompletion(bool Released, bool ReturnedToOrigin, uint Effect, bool HandedToPoster);
public interface INativeShellExitStore
{
    ShellExitIntent Begin(string posterId, IReadOnlyList<string> layoutIds, bool explicitDelete = false);
    void End(ShellExitIntent intent, bool released);
    int ObserveReleasedMove(ShellExitIntent intent);
    int RemoveStaleReferencesAfterUserConfirmation(ShellExitIntent intent);
    bool HasRemainingPresentCards(ShellExitIntent intent);
    int Reconcile();
}
public interface INativeExitPresentation
{
    void PublishObservedChanges();
    Task<bool> ConfirmMissingReferences(IReadOnlyList<string> paths, CancellationToken token);
}

public sealed record ShellDirectoryResult(string? Destination, bool Cancelled, int HResult);
public interface IShellDirectoryTransfer
{
    Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner,
        Func<bool> current, CancellationToken token);
}
public sealed record MappedIngressResult(int Completed, int Skipped, IReadOnlyList<string> Errors);
public sealed record MappedIngressSource(string Path, string Identity);
public sealed record MappedIngressPreparation(MappedNewContent Target, IReadOnlyList<MappedIngressSource> Sources, int Skipped);
public interface IMappedIngressStorage
{
    IMappedIngressAttempt BeginAttempt();
}
// A scope pins the executor and target. Observation happens after that scope releases.
public interface IMappedIngressAttempt
{
    IDisposable AcquireExecution();
    MappedIngressPreparation Prepare(string posterId, IReadOnlyList<string> paths, bool copy,
        Func<bool> current, CancellationToken token, long? expectedPosterVersion);
    bool IsCurrent(MappedIngressSource source);
    bool AcceptDestination(string path);
    void RecordCompleted(IReadOnlyList<string> paths);
    void ObserveDepartures(ICollection<string> errors);
    bool HasPreparedOperation { get; }
    void PublishAndClose(IReadOnlyList<string> completed, IncomingContentPlacement? landing, ICollection<string> errors);
}
public sealed record CrossPosterPreparation(MappedNewContent Target, IReadOnlyList<ShellExitItem> Sources, long Version);
public sealed record CrossPosterResult(Guid CardId, string Path, string Identity);
public interface ICrossPosterStorage
{
    IReadOnlyList<string> Reconcile();
    ICrossPosterAttempt BeginAttempt();
}
public interface ICrossPosterAttempt : IDisposable
{
    CrossPosterPreparation Prepare(ShellExitIntent source, string targetId, IReadOnlyList<string> paths,
        RegisteredManagedStorage? storage, IncomingContentPlacement? landing, Func<bool> current,
        CancellationToken token, long? expectedTargetVersion);
    bool IsCurrent(ShellExitItem source, long version);
    bool IsStillMissing(ShellExitItem source);
    CrossPosterResult CaptureDestination(ShellExitItem source, string destination);
    void RecordResults(IReadOnlyList<CrossPosterResult> results);
    void Commit(CrossPosterResult result, IncomingContentPlacement? landing, long expectedTargetVersion);
    DesktopStateSnapshot ReadState();
    void Close();
}

public enum TransferFinishKind { Completed, NotMoved, Preserved }
public interface IManagedIngressItem
{
    Guid ContentId { get; }
    string Destination { get; }
    void RecordIntent();
    TransferFinishKind Complete(CancellationToken token);
}
public interface IManagedIngressStorage
{
    DesktopStateSnapshot ReadState();
    IManagedIngressItem Prepare(DesktopStateSnapshot state, string posterId, string path,
        RegisteredManagedStorage storage, IncomingContentPlacement? landing);
}
// Calls are on the caller's synchronization context; storage/file work is offloaded.
// The host supplies interaction facts and publishes the projection, never file decisions.
public interface IIncomingTransferPresentation
{
    bool IsCurrent { get; }
    void ReportProgress(string path, int completed);
    void PublishCommittedChanges();
}
public sealed record ManagedIngressResult(int Completed, int Failed, int Total, string? LastError);
