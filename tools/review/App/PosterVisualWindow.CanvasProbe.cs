using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal Dictionary<string, FrameworkElement> CaptureCanvasCardsForProbe() => Presenter.Cards.ToDictionary(p => p.Key, p => p.Value);
    internal Action CanvasWarmReadbackForProbe()
    {
        var warm = Presenter.Bitmaps.ToArray();
        if (warm.Length == 0) throw new InvalidOperationException("No warm raster to verify.");
        return () =>
        {
            // A zero-height viewport may virtualize cards away. Their ready bitmaps must survive.
            if (warm.Any(p => !ReferenceEquals(Presenter.Bitmaps.GetValueOrDefault(p.Key), p.Value)))
                throw new InvalidOperationException("Posture discarded warm canvas rasters.");
        };
    }
    internal void RequireCanvasCardsForProbe(Dictionary<string, FrameworkElement> previous)
    {
        if (previous.Count != Presenter.Cards.Count || previous.Any(p => !ReferenceEquals(Presenter.Cards.GetValueOrDefault(p.Key), p.Value)))
            throw new InvalidOperationException("Canvas design replaced stable content controls.");
    }
    internal void VerifyCanvasOriginForProbe()
    {
        if (_presentation.Parameters.Canvas?.Current is null) return;
        if (_window?.Content is not FrameworkElement root || Presenter.Surface is null) throw new InvalidOperationException("Missing actual canvas visual.");
        var point = Presenter.Surface.TransformToVisual(root).TransformPoint(new(0, 0));
        if (Math.Abs(point.X - ContentLeftDip) > .1 || Math.Abs(point.Y - ContentTopDip) > .1)
            throw new InvalidOperationException($"Canvas visual/input origin mismatch: {point} vs {ContentLeftDip},{ContentTopDip}");
        if (_sceneInstance is not CanvasSceneInstance) throw new InvalidOperationException("Canvas renderer is missing.");
    }
}
internal sealed partial class PosterSpatialRuntime
{
    internal PosterVisualWindow CanvasWindowForProbe(string id) => _windows[id];
}
