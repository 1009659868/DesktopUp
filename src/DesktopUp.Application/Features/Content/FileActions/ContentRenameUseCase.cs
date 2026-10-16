using DesktopUp.Domain;

namespace DesktopUp.Application;

// Owns rename sequencing, acknowledgement ambiguity and observation-only restart.
public sealed class ContentRenameUseCase(IContentRenameJournal journal, IContentActionFiles files,
    Action<string>? checkpoint = null)
{
    public void Rename(string posterId, string itemId, Guid contentId, long version, string name, CancellationToken token = default,
        string? expectedIdentity = null)
    {
        if (LocalFileNamePolicy.Error(name) is { } error) throw new IOException(error);
        using var executor = journal.AcquireExecutor();
        journal.RequireIdle();
        var snapshot = journal.ReadState();
        journal.ValidateMappedAction(posterId, itemId);
        var relation = ContentRelationPolicy.Resolve(snapshot.ContentCatalog, posterId, itemId);
        if (relation is null || relation.Item.Id != contentId || relation.Item.Version != version ||
            !LocalContentProtocol.IsSupported(relation.Item)) throw new IOException("所选对象已变化，请重新打开菜单。");
        string source = files.Normalize(relation.Item.Target.Locator);
        string destination = files.Normalize(Path.Combine(Path.GetDirectoryName(source)!, name));
        if (source == destination) return;
        if (journal.ProtectedPath(source) || journal.ProtectedPath(destination)) throw new IOException("此路径受保护，不能改名。");
        string identity = files.Identity(source), parent = files.Identity(Path.GetDirectoryName(source)!);
        if(expectedIdentity is not null && identity != expectedIdentity)throw new IOException("改名窗口打开后对象已被替换，未执行。");
        var targets = snapshot.ContentCatalog.Items.Where(i => i.Source.ProviderId == "local-file-reference-v1" &&
                (string.Equals(i.Target.Locator,source,StringComparison.OrdinalIgnoreCase) || files.Contains(source,i.Target.Locator)))
            .Select(i => new ContentRenameTarget(i.Id,i.Version,i.Target.Locator,destination+i.Target.Locator[source.Length..])).ToArray();
        var intent = new ContentRenameIntent(Guid.NewGuid(),source,destination,identity,parent,targets);
        token.ThrowIfCancellationRequested();
        journal.Prepare(intent, snapshot.ContentVersion);
        checkpoint?.Invoke("Prepared");
        try
        {
            token.ThrowIfCancellationRequested();
            files.RenameKnown(source,destination,identity,parent);
            checkpoint?.Invoke("Renamed");
            journal.CommitObserved(intent);
        }
        catch
        {
            // Commit acknowledgment can fail after durable commit. Never move the file back
            // underneath an already updated index; retain ambiguous outcomes for observation.
            var current = journal.ReadState();
            if(intent.Targets.All(target=>current.ContentCatalog.Items.Any(i=>i.Id==target.ContentId &&
                i.Target.Locator==target.After && i.Version==target.Version+1)))return;
            // Roll back only our exact object, if the original name is still free. A competing
            // file is never removed to make room. Uncertain receipts stay for observation.
            try
            {
                if (files.Identity(Path.GetDirectoryName(source)!) == parent &&
                    files.Identity(destination) == identity &&
                    (string.Equals(source,destination,StringComparison.OrdinalIgnoreCase) || !files.Exists(source)))
                    files.RenameKnown(destination,source,identity,parent);
                if (files.Identity(source)==identity) journal.Close(intent.Id);
            }
            catch (Exception e) when(e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public int Reconcile()
    {
        var pending = journal.Pending();
        int changed=0;
        foreach(var intent in pending)
        {
            using var executor = journal.AcquireExecutor();
            try
            {
                if(files.Identity(Path.GetDirectoryName(intent.Source)!)!=intent.ParentIdentity)continue;
                // Case-only renames must use the actual directory spelling, not Exists(old).
                string? observed=files.Enumerate(Path.GetDirectoryName(intent.Source)!)
                    .Take(10001).FirstOrDefault(p=>string.Equals(p,intent.Destination,StringComparison.OrdinalIgnoreCase));
                if(observed==intent.Destination && files.Identity(observed)==intent.Identity)
                { journal.CommitObserved(intent);changed++; }
                else if(files.Identity(intent.Source)==intent.Identity)journal.Close(intent.Id);
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        }
        return changed;
    }
}
