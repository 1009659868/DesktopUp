using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Resources follow the same projected tiles as painting/hits. Copies share one key;
/// visible items outrank one-pitch overscan. Exhaustion keeps the renderer's normal fallback.</summary>
public static class ContentAssetDemand
{
    public static IReadOnlyList<ContentAssetKey> Build(ContentLayoutDocument document, ContentLayoutSnapshot snapshot,
        ContentLayoutViewport viewport, ContentBrowseMode mode, IReadOnlyList<ContentItemView> views,
        long sessionEpoch, uint dpiX, uint dpiY, long themeVersion, long sourceVersion, int maximumItems = 64)
    {
        if (maximumItems is < 1 or > 512 || sourceVersion < 1 || sessionEpoch < 1 || themeVersion < 1 ||
            dpiX is < 48 or > 960 || dpiY is < 48 or > 960 || document.PosterId != snapshot.PosterId ||
            document.DocumentVersion != snapshot.DocumentVersion || document.ItemSetVersion != snapshot.ItemSetVersion)
            throw new ArgumentException("Invalid asset demand witness or budget.");
        var byId = views.ToDictionary(v => v.ItemId, StringComparer.Ordinal);
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport, mode, snapshot.CellDip + snapshot.GapDip)
            .OrderBy(t => t.Clip.IsEmpty).ThenBy(t => t.ReadOrder).ThenBy(t => t.LoopIndex);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<ContentAssetKey>();
        foreach (var tile in tiles)
        {
            if (!seen.Add(tile.ItemId) || !byId.TryGetValue(tile.ItemId, out var view) || !view.IsAvailable && !view.RequestAssetWhenUnavailable ||
                string.IsNullOrWhiteSpace(view.AssetKey) || view.AssetKey.Length > 256 || view.AssetKey.Any(char.IsControl)) continue;
            if (tile.SourceIndex < 0 || tile.SourceIndex >= document.Placements.Count ||
                document.Placements[tile.SourceIndex].ItemId != tile.ItemId) throw new ArgumentException("Foreign asset tile.");
            int width = (int)Math.Clamp(Math.Ceiling(tile.Bounds.Width * dpiX / 96d), 1, 4096);
            int height = (int)Math.Clamp(Math.Ceiling(tile.Bounds.Height * dpiY / 96d), 1, 4096);
            keys.Add(new(document.PosterId, tile.ItemId, sessionEpoch, document.DocumentVersion, document.ItemSetVersion,
                document.Placements[tile.SourceIndex].Version, view.AssetKey, width, height, dpiX, dpiY, themeVersion, sourceVersion));
            if (keys.Count == maximumItems) break;
        }
        return keys.AsReadOnly();
    }
}
