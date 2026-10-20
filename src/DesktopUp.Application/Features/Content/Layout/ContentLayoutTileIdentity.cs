using DesktopUp.Domain;
namespace DesktopUp.Application;

public static class ContentLayoutTileIdentity
{
    public static string Key(ContentLayoutTile tile) => tile.LoopIndex == 0 ? tile.ItemId
        : tile.ItemId + "#" + tile.LoopIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
