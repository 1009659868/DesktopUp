using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public static class DirectTransferPreparation
{
    public sealed record OutgoingSource(string LayoutId,Guid CardId,Guid ContentId,long ContentVersion,string Locator);
    public static IReadOnlyList<OutgoingSource> OutgoingSources(DesktopStateSnapshot state,string posterId,IReadOnlyList<string> layoutIds)
    {
        if(layoutIds.Count is <1 or >ContentTransferPlanning.MaximumBatch || layoutIds.Distinct().Count()!=layoutIds.Count)
            throw new IOException("请选择1～100项不重复的卡片。");
        var sources=layoutIds.Select(id=>
        {
            var relation=ContentRelationPolicy.Resolve(state.ContentCatalog,posterId,id)??throw new IOException("卡片已变化。");
            return new OutgoingSource(id,relation.PosterItem.Id,relation.Item.Id,relation.Item.Version,LocalTransferFiles.Normalize(relation.Item.Target.Locator));
        }).ToArray();
        if(sources.Any(s=>sources.Any(other=>s!=other &&
            (string.Equals(s.Locator,other.Locator,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(s.Locator,other.Locator)))))
            throw new IOException("请不要同时取出同一来源的重复卡片或文件夹及其内部项目。");
        return sources;
    }
    public static LocalTransferPreview Incoming(DesktopStateSnapshot state,string posterId,IReadOnlyList<string> paths,RegisteredManagedStorage storage,
        IncomingContentPlacement? placement=null)
    {
        if(paths.Count is <1 or >ContentTransferPlanning.MaximumBatch)throw new IOException("一次最多拖入100项。");
        var inputs=paths.Select(path=>
        {
            var observed=LocalContentReferenceProbe.Inspect(path);
            var item=observed.Item??throw new IOException(observed.Message);
            // Reuse live physical references, not detached historical entities.
            var known=state.ContentCatalog.Items.FirstOrDefault(x=>x.Source.ProviderId==item.Source.ProviderId &&
                string.Equals(x.Target.Locator,item.Target.Locator,StringComparison.OrdinalIgnoreCase) &&
                state.ContentCatalog.Memberships.Any(m=>m.ContentItemId==x.Id));
            return (known??item,Path.Combine(storage.Collection.StorageLocator!,Path.GetFileName(item.Target.Locator)),(Guid?)null);
        }).ToArray();
        return LocalTransferPreview.Prepare(state,posterId,ContentDropRoute.IntoManagedStorage,inputs,storage.Root,
            placement:placement,keepSourceUntilCommit:true);
    }

    public static LocalTransferPreview Outgoing(DesktopStateSnapshot state,string posterId,IReadOnlyList<string> layoutIds)
    {
        if(layoutIds.Count is <1 or >ContentTransferPlanning.MaximumBatch)throw new IOException("一次最多取出100项。");
        string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if(string.IsNullOrWhiteSpace(desktop))throw new IOException("系统桌面目录不可用。");
        var inputs=layoutIds.Select(id=>
        {
            var card=state.ContentCatalog.PosterItems.SingleOrDefault(x=>x.PosterId==posterId&&x.LayoutItemId==id)
                ??throw new IOException("卡片已变化，请重新拖动。");
            var member=state.ContentCatalog.Memberships.Single(x=>x.Id==card.MembershipId);
            var item=state.ContentCatalog.Items.Single(x=>x.Id==member.ContentItemId);
            return (item,Path.Combine(desktop,Path.GetFileName(item.Target.Locator)),(Guid?)card.Id);
        }).ToArray();
        return LocalTransferPreview.Prepare(state,posterId,ContentDropRoute.ToDesktop,inputs,keepSourceUntilCommit:true);
    }
    public static LocalTransferPreview RequireSupported(LocalTransferPreview preview)
    {
        if(preview.Files.Any(f=>!f.SameVolume))throw new IOException("本批仅开放同卷移动。请选择与来源同盘的收纳目录；跨卷尚未开放，文件未移动。");
        return preview;
    }
}
