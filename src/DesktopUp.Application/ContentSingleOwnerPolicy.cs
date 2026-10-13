using DesktopUp.Domain;

namespace DesktopUp.Application;

// Source ownership is distinct from historical entities retained for receipts/rollback.
public static class ContentSingleOwnerPolicy
{
    public static string Key(ContentItem item)
    {
        if (item.Source.ProviderId == "local-file-reference-v1")
            return "file:" + Path.GetFullPath(item.Target.Locator).TrimEnd('\\','/').ToUpperInvariant();
        return item.Source.ProviderId + ":" + item.Source.Key;
    }
    public static IReadOnlyList<PosterItem> DuplicateCards(ContentCatalogSnapshot catalog, IReadOnlyList<string>? posterOrder = null)
    {
        var members=catalog.Memberships.ToDictionary(m=>m.Id); var items=catalog.Items.ToDictionary(i=>i.Id);
        var order=(posterOrder??catalog.Bindings.Select(b=>b.PosterId).ToArray()).Select((id,n)=>(id,n)).ToDictionary(x=>x.id,x=>x.n);
        return catalog.PosterItems.OrderBy(p=>order.GetValueOrDefault(p.PosterId,int.MaxValue)).ThenBy(p=>p.Id)
            .GroupBy(p=>Key(items[members[p.MembershipId].ContentItemId]),StringComparer.Ordinal)
            .SelectMany(g=>g.Skip(1)).ToArray();
    }
    public static string? Validate(ContentCatalogSnapshot catalog) => DuplicateCards(catalog).Count == 0
        ? null : "同一对象只能属于一张海报；请移动已有卡片，不再创建共享引用。";
}
