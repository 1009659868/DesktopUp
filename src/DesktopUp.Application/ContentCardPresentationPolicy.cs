namespace DesktopUp.Application;

public static class ContentCardPresentationPolicy
{
    // Reserve the maximum selection border up front: selecting never changes the image size.
    public static (double Width, double Height) ImageArea(double width, double height, double labelHeight)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(labelHeight) || width <= 0 || height <= 0 || labelHeight < 0)
            throw new ArgumentException("Invalid content card dimensions.");
        return (Math.Max(1, width - 4), Math.Max(1, height - labelHeight - 4));
    }
    public static bool IsSelected(IReadOnlyList<string> selection, string itemId) => selection.Contains(itemId);
    public static bool ShowOutline(IReadOnlyList<string> selection, string itemId, bool explicitSelection, bool editing) =>
        (explicitSelection || editing) && IsSelected(selection, itemId);
}
