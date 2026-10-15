using DesktopUp.Domain;

namespace DesktopUp.Application;

public static class ContentReturnPolicy
{
    public static bool IsMapped(ContentCatalogSnapshot catalog, string posterId) =>
        catalog.Bindings.Any(b => b.PosterId == posterId && catalog.Collections.Any(c => c.Id == b.CollectionId && c.Kind == ContentCollectionKind.Mapped));

    public static IReadOnlyList<PosterItem> ManagedCards(ContentCatalogSnapshot catalog, string posterId)
    {
        if (IsMapped(catalog, posterId)) return [];
        var owned = catalog.Memberships.Where(m => m.Origin == ContentMembershipOrigin.Source &&
            catalog.Collections.Any(c => c.Id == m.CollectionId && c.Kind == ContentCollectionKind.Managed)).Select(m => m.ContentItemId).ToHashSet();
        return catalog.PosterItems.Where(p => p.PosterId == posterId && catalog.Memberships.Any(m => m.Id == p.MembershipId && owned.Contains(m.ContentItemId))).ToArray();
    }
}
