namespace DesktopUp.Application;

// Host-facing intent boundary; no database, native helper or window dependency.
public interface IContentFileActionService
{
    string CaptureIdentity(string path);
    string SuggestName(string directory, NewContentKind kind);
    void ValidateMappedAction(string posterId, string? itemId);
    NewContentLocation PrepareNewContent(string posterId, RegisteredManagedStorage? storage, bool mapped);
    string Create(string posterId, NewContentLocation location, NewContentKind kind, string name,
        string? target, CancellationToken token);
    void Rename(string posterId, string itemId, Guid contentId, long version, string name,
        string identity, CancellationToken token);
    Task<ContentDeletionResult> Delete(string posterId, IReadOnlyList<string> items, nint owner, CancellationToken token);
    Task<ContentShellOutcome> InvokeShell(string posterId, string? itemId, ContentRelationResolution? expected,
        NewContentLocation? location, string path, ShellUiAction action, int x, int y, CancellationToken token);
}
