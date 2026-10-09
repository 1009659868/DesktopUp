using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentReuseCandidate(ContentItem Item, IReadOnlyList<string> PosterIds, bool FromThisBatch);
public sealed record ContentReferencePreviewOptions(IReadOnlyList<ContentReuseCandidate> Reusable, ContentReferenceChoice DefaultChoice);

// Display-only duplicate hints, not deletion/GC or implicit identity merging.
public static class ContentReferencePreviewPolicy
{
    public static ContentReferencePreviewOptions ForItem(ContentCatalogSnapshot catalog, ContentItem incoming,
        IReadOnlyList<ContentItem> earlierInputs)
    {
        var members = catalog.Memberships.ToDictionary(m => m.Id);
        var owners = catalog.PosterItems.GroupBy(p => members[p.MembershipId].ContentItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.PosterId).Distinct().Order().ToArray());
        var reusable = catalog.Items.Where(i => owners.ContainsKey(i.Id) &&
                ContentIdentityPolicy.Compare(i, incoming) != ContentDuplicateHint.None)
            .Select(i => new ContentReuseCandidate(i, owners[i.Id], false)).ToList();
        foreach (var item in earlierInputs)
            if (reusable.All(c => c.Item.Id != item.Id) && ContentIdentityPolicy.Compare(item, incoming) != ContentDuplicateHint.None)
                reusable.Add(new(item, [], true));
        return new(reusable.AsReadOnly(), reusable.Count == 0 ? ContentReferenceChoice.CreateIndependent : ContentReferenceChoice.Skip);
    }
}
