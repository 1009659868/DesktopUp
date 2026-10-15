using DesktopUp.Domain;

namespace DesktopUp.Application;

// One frozen card/selection identity across the native picker, decoder and confirmation.
// Pixel data remains in memory until the existing icon transaction is explicitly committed.
public sealed class ContentIconPreview
{
    private readonly ContentIconCommand basis;
    public string PosterId => basis.Relation.PosterId;
    public string LayoutItemId { get; }
    public string DisplayName { get; }
    public string SourcePath { get; }
    public bool HasCustomIcon { get; }
    private ContentIconPreview(ContentIconCommand command, ContentRelationResolution relation)
    {
        basis = command; LayoutItemId = relation.PosterItem.LayoutItemId;
        DisplayName = relation.Item.DisplayName; SourcePath = relation.Item.Target.Locator;
        HasCustomIcon = relation.PosterItem.IconAssetId is not null;
    }
    public static ContentIconPreview? Capture(DesktopUpProjection state, string? posterId,
        string? itemId, IReadOnlyList<string> selection)
    {
        if (posterId is null || itemId is null || selection.Count != 1 || selection[0] != itemId ||
            !state.PostersById.TryGetValue(posterId, out var poster) || poster.Poster.IsLocked ||
            ContentRelationPolicy.Resolve(state.ContentCatalog, posterId, itemId) is not { } relation) return null;
        return new(new("icon-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, state.Revision,
            StateReadSet.ForPoster(posterId, poster.EntityVersion) with { ContentVersion = state.ContentVersion },
            relation.Witness, null, true), relation);
    }
    public bool IsCurrent(DesktopUpProjection state, string? posterId, string? itemId, IReadOnlyList<string> selection) =>
        posterId == PosterId && itemId == LayoutItemId && selection.Count == 1 && selection[0] == LayoutItemId &&
        state.ContentVersion == basis.ReadSet.ContentVersion && state.PostersById.TryGetValue(PosterId, out var owner) &&
        !owner.Poster.IsLocked && owner.EntityVersion == basis.ReadSet.Posters[PosterId] &&
        ContentRelationPolicy.IsCurrent(state.ContentCatalog, basis.Relation);
    public ContentIconCommand Confirm(ContentIconAsset? asset) => basis with { Asset = asset };
}
