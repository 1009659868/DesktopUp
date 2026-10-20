namespace DesktopUp.Application;

public sealed record ContentRenameTarget(Guid ContentId, long Version, string Before, string After);
public sealed record ContentRenameIntent(Guid Id, string Source, string Destination, string Identity,
    string ParentIdentity, IReadOnlyList<ContentRenameTarget> Targets);


public interface IContentRenameJournal
{
    IDisposable AcquireExecutor();
    void RequireIdle();
    DesktopStateSnapshot ReadState();
    void ValidateMappedAction(string posterId, string itemId);
    bool ProtectedPath(string path);
    void Prepare(ContentRenameIntent intent, long expectedContentVersion);
    void CommitObserved(ContentRenameIntent intent);
    void Close(Guid id);
    IReadOnlyList<ContentRenameIntent> Pending();
}
