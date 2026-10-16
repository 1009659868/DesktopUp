using DesktopUp.Domain;

namespace DesktopUp.Application;

// An immutable projection of the transaction snapshot; no disk access on the UI/render thread.
public sealed class ReferenceContentViewSource : IContentItemViewSource
{
    private readonly IReadOnlyDictionary<string, ContentItem> items;
    public long Version { get; }
    public ReferenceContentViewSource(ContentCatalogSnapshot catalog, string posterId, long version)
    {
        catalog = ContentCatalogPolicy.Freeze(catalog);
        Version = Math.Max(1, version);
        var members = catalog.Memberships.ToDictionary(m => m.Id);
        var entities = catalog.Items.ToDictionary(i => i.Id);
        items = catalog.PosterItems.Where(p => p.PosterId == posterId)
            .ToDictionary(p => p.LayoutItemId, p => entities[members[p.MembershipId].ContentItemId]);
    }
    public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document) => document.Placements.Where(p => items.ContainsKey(p.ItemId))
        .Select(p => { var item = items[p.ItemId]; return new ContentItemView(p.ItemId,
            item.DisplayName.Length > 256 ? item.DisplayName[..256] : item.DisplayName,
            item.Kind switch { ContentKind.Folder => "DIR", ContentKind.Shortcut => "LINK", ContentKind.Application => "APP", _ => "FILE" }, 0xFF718DAB)
            { IsAvailable = item.Availability == ContentAvailability.Available }; }).ToArray();
}
