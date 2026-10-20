using DesktopUp.Domain;

namespace DesktopUp.Application;

// Owns creation/observation ordering. File proof and atomic publication stay in adapters.
public sealed class MappedContentCreationUseCase(IMappedDirectoryStorage storage,
    IContentObservationFiles files, IOwnedContentCreationFiles creation,
    Action? beforeCommit = null)
{
    private readonly MappedDirectoryUseCase _mapping = new(storage, files);

    public string Create(string posterId, NewContentLocation location, NewContentKind kind,
        string name, byte[] bytes, CancellationToken token)
    {
        using var executor = storage.AcquireCreation();
        storage.RequireIdle();
        var state = storage.ReadState();
        var expected = location.Mapping ?? throw new IOException("映射位置缺失。");
        var poster = state.Catalog.Posters.Single(p => p.Poster.PosterId == posterId);
        if (state.ContentVersion != location.ContentVersion || poster.EntityVersion != location.PosterVersion)
            throw new IOException("新建窗口打开后状态已变化，请重试。");
        if (poster.Poster.IsLocked) throw new IOException("请先解除海报锁定。");

        using var directory = files.LockDirectory(expected.Path);
        if (_mapping.ResolveNewContent(posterId) != expected) throw new IOException("映射位置已变化。");
        string path = Path.Combine(expected.Path, name);
        token.ThrowIfCancellationRequested();
        var created = creation.Create(path, kind, bytes);
        try
        {
            token.ThrowIfCancellationRequested();
            var scan = files.ScanMapping(expected.Path, token);
            beforeCommit?.Invoke();
            token.ThrowIfCancellationRequested();
            using var pin = created.PinUnchanged();
            var collection = state.ContentCatalog.Collections.Single(c => c.Id == expected.CollectionId);
            if (scan.Fingerprint != files.ScanMapping(expected.Path, token).Fingerprint)
                throw new IOException("目录仍在变化。");
            _mapping.CommitObservation(state, collection, scan);
            return path;
        }
        catch
        {
            // An ambiguous acknowledgement is not proof of an absent commit.
            // If reading fails, preserve the result; only a proven unindexed result may be cleaned.
            var now = storage.ReadState();
            bool indexed = now.ContentCatalog.PosterItems.Any(p => p.PosterId == posterId &&
                ContentRelationPolicy.Resolve(now.ContentCatalog, posterId, p.LayoutItemId)!.Item.Target.Locator == path);
            if (!indexed) created.RemoveIfUnchanged();
            throw;
        }
    }
}
