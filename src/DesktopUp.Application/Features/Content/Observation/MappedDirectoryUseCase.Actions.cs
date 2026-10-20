using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed partial class MappedDirectoryUseCase
{
    public MappedNewContent ResolveNewContent(string posterId)
    {
        var state = storage.ReadState();
        var binding = state.ContentCatalog.Bindings.Single(b => b.PosterId == posterId);
        var collection = ActiveCollections(state).Single(c => c.Id == binding.CollectionId);
        storage.ValidateRoot(state, collection.StorageLocator!, collection.Id);
        var evidence = storage.Evidence(collection.Id);
        if (files.Identity(collection.StorageLocator!) != evidence.Identity) throw new IOException("映射目录已变化。");
        return new(collection.Id, collection.StorageLocator!, evidence.Identity);
    }

    // A stale view cannot act on a same-name replacement directory/item.
    public void ValidateAction(string posterId, string? layoutItemId = null, bool allowMissing = false)
    {
        var state = storage.ReadState();
        var binding = state.ContentCatalog.Bindings.SingleOrDefault(b => b.PosterId == posterId);
        if (binding is null || !ActiveCollections(state).Any(c => c.Id == binding.CollectionId)) return;
        _ = ResolveNewContent(posterId);
        if (layoutItemId is null) return;
        var relation = ContentRelationPolicy.Resolve(state.ContentCatalog, posterId, layoutItemId)
            ?? throw new IOException("映射内容已变化。");
        var card = state.ContentCatalog.PosterItems.Single(p => p.PosterId == posterId && p.LayoutItemId == layoutItemId);
        var observation = storage.Evidence(binding.CollectionId).Items.SingleOrDefault(i => i.MembershipId == card.MembershipId)
            ?? throw new IOException("映射内容缺少身份记录。");
        string path = relation.Item.Target.Locator;
        if (allowMissing)
        {
            var entries = files.Enumerate(Path.GetDirectoryName(path)!).Take(ContentLayoutPolicy.MaximumItems + 1).ToArray();
            if (entries.Length > ContentLayoutPolicy.MaximumItems) throw new IOException("映射目录超出本轮核对预算。");
            if (!entries.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase))) return;
        }
        if (files.MappedItemIdentity(path) != observation.Identity)
            throw new IOException("映射对象已被替换，正在等待目录同步，请稍后重试。");
    }

    public void CommitIncoming(string posterId, IReadOnlyList<string> paths,
        IncomingContentPlacement? landing, long expectedVersion)
    {
        var target = ResolveNewContent(posterId);
        var state = storage.ReadState();
        if (state.Catalog.Posters.Single(p => p.Poster.PosterId == posterId).EntityVersion != expectedVersion)
            landing = null;
        var collection = state.ContentCatalog.Collections.Single(c => c.Id == target.CollectionId);
        using var root = files.LockDirectory(target.Path);
        var scan = files.ScanMapping(target.Path);
        if (scan.Identity != target.Identity) throw new IOException("映射目录已变化。");
        CommitObservation(state, collection, scan, paths, landing);
    }
}
