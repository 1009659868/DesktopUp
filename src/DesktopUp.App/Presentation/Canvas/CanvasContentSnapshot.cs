using DesktopUp.Domain;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed record CanvasContentPreviewItem(string Name, string Monogram, ImageSource? Image, CanvasRect Box);
internal sealed record CanvasContentSnapshot(double Width, double Height, string Foreground, IReadOnlyList<CanvasContentPreviewItem> Items)
{
    /// <summary>Read back the host's already prepared visible content. No enumeration, worker requests or ownership transfer.</summary>
    public static CanvasContentSnapshot? Read(PosterContentPresenter presenter)
    {
        if (presenter.Frame is not { } frame || frame.Viewport.WidthDip <= 0 || frame.Viewport.HeightDip <= 0) return null;
        var views = frame.Views.ToDictionary(v => v.ItemId);
        var items = new List<CanvasContentPreviewItem>();
        foreach (var tile in ContentLayoutFlow.ProjectTiles(frame.Snapshot, frame.Viewport, frame.BrowseMode).Take(128))
        {
            if (!views.TryGetValue(tile.ItemId, out var view)) continue;
            ImageSource? bitmap = presenter.VisibleKeys.TryGetValue(tile.ItemId, out var key) && presenter.Bitmaps.TryGetValue(key, out var image) ? image : null;
            items.Add(new(view.Name, view.Monogram, bitmap, new(tile.Bounds.X, tile.Bounds.Y, tile.Bounds.Width, tile.Bounds.Height)));
        }
        return new(frame.Viewport.WidthDip, frame.Viewport.HeightDip, frame.Presentation.EffectiveContentForeground, items);
    }
}
