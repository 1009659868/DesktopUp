using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal DesktopPixelRect CanvasPreviewBounds(PosterDefinition desired, DesktopPixelRect baseline)
    {
        if (_monitor is null) throw new InvalidOperationException("显示器尚未就绪。");
        var work = _monitor.WorkArea;
        if (desired.WidthDip * _monitor.ScaleX > work.Width || desired.ExpandedHeightDip * _monitor.ScaleY > work.Height)
            throw new ArgumentException("画板尺寸超出当前屏幕可用空间；请缩小画板再预览。不会拉伸或裁剪作品。");
        return desired.WidthDip == _authoritativePoster?.WidthDip && desired.ExpandedHeightDip == _authoritativePoster.ExpandedHeightDip
            ? baseline : PosterGeometryProjector.Project(new(desired.WidthDip, desired.ExpandedHeightDip, desired.HorizontalAnchor, desired.VerticalAnchor), _monitor);
    }

    private DesktopRoundedRegion CanvasVisualRegion(DesktopPixelRect frame)
    {
        var design = _presentation.Parameters.Canvas?.Current;
        if (_geometry.IsCollapsed || design?.Recipe.Surface is not { } surface)
            return new(frame.Width, frame.Height, ResolveOuterCornerRadiusPixels());
        double scale = Math.Min(frame.Width / design.Recipe.Artboard!.Width, frame.Height / design.Recipe.Artboard.Height);
        int radius = surface.Shape == "rounded" ? (int)Math.Round(Math.Min(surface.Radius * scale, Math.Min(frame.Width, frame.Height) / 2d)) : 0;
        return new(frame.Width, frame.Height, radius, surface.Shape);
    }

    private PosterTitleChromeLayout ResolveTitleLayout(double width, double? height = null) =>
        CanvasSurfaceGeometry.Title(_presentation.Parameters.Canvas?.Current, width,
            CanvasFor(_bounds)?.Height ?? height ?? (_bounds.Height / (_monitor?.ScaleY ?? 1)),
            _geometry.IsCollapsed, _presentation.TitleChrome);
}
