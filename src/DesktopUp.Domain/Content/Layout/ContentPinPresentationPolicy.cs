namespace DesktopUp.Domain;

public enum ContentPinAdornment { None, OrderPin, LegacyLock }
public enum ContentPinContextAction { ItemMenu, ReleasePin, Cancel }

/// <summary>One visual box shared by the pin drawing and its right-button subtarget.
/// This does not add an input region or change the card's left-button/scroll geometry.</summary>
public static class ContentPinPresentationPolicy
{
    public const double SizeDip = 22, InsetDip = 2;

    public static ContentPinAdornment Adornment(ContentLayoutDocument document, ContentPlacement placement) =>
        placement.FixedPosition ? ContentPinAdornment.LegacyLock :
        document.Mode != ContentLayoutMode.Manual &&
        (document.SchemaVersion >= 3 || document.Mode == ContentLayoutMode.Anchored) && placement.PinnedIndex is not null
            ? ContentPinAdornment.OrderPin : ContentPinAdornment.None;

    public static ContentRect Bounds(ContentRect item) => item.IsEmpty ? new() :
        new ContentRect(item.Right - InsetDip - SizeDip, item.Y + InsetDip, SizeDip, SizeDip).Intersect(item);

    public static ContentRect VisibleBounds(ContentLayoutTile tile) => Bounds(tile.Bounds).Intersect(tile.Clip);

    public static ContentPinContextAction ResolveContext(IEnumerable<ContentRect> visiblePins,
        (double X, double Y)? press, (double X, double Y) release)
    {
        // Legacy context callers carry only a release point. They retain the item menu and
        // cannot invoke a state-changing shortcut without the right-button press witness.
        if (press is not { } start) return ContentPinContextAction.ItemMenu;
        bool touchedPin = false;
        foreach (var pin in visiblePins)
        {
            bool began = pin.Contains(start.X, start.Y), ended = pin.Contains(release.X, release.Y);
            if (began && ended) return ContentPinContextAction.ReleasePin;
            touchedPin |= began || ended;
        }
        // A press/release across the icon boundary is neither a pin click nor an item-menu click.
        return touchedPin ? ContentPinContextAction.Cancel : ContentPinContextAction.ItemMenu;
    }
}
