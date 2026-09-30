using DesktopUp.Domain;
using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private Canvas? _titleCanvas;
    private Border? _titleName, _titleCollapse, _titleMore;
    private Microsoft.UI.Xaml.Shapes.Path? _titleCloud;
    private double _titlePaintWidth = -1;
    internal bool VerifyTitleChromeForProbe()
    {
        if (_monitor is null || inputRouter.ReadSurface(inputSession) is not { } surface ||
            _titleName is null || _titleCollapse is null || _titleMore is null) return false;
        var layout = PosterTitleChromePolicy.Resolve(surface.Frame.Width / _monitor.ScaleX, _geometry.IsCollapsed, _presentation.TitleChrome);
        bool Place(FrameworkElement element, TitleChromeRect rect) => Math.Abs(Canvas.GetLeft(element) - rect.X) < .01 &&
            Math.Abs(Canvas.GetTop(element) - rect.Y) < .01 && Math.Abs(element.ActualWidth - rect.Width) < .01 && Math.Abs(element.ActualHeight - rect.Height) < .01;
        bool Input(PosterInputTargetKind kind, TitleChromeRect rect) => surface.Layout.Regions.Any(r => r.Kind == kind &&
            r.Bounds.X == (int)Math.Round(rect.X * _monitor.ScaleX) && r.Bounds.Y == (int)Math.Round(rect.Y * _monitor.ScaleY) &&
            r.Bounds.Width == (int)Math.Round(rect.Width * _monitor.ScaleX) && r.Bounds.Height == (int)Math.Round(rect.Height * _monitor.ScaleY));
        return Place(_titleName, layout.Name) && Place(_titleCollapse, layout.Collapse) && Place(_titleMore, layout.More) &&
            Input(PosterInputTargetKind.CollapseButton, layout.Collapse) && Input(PosterInputTargetKind.MoreButton, layout.More) &&
            _titleCollapse.Background is SolidColorBrush { Color.A: >= 240 } && _titleMore.Background is SolidColorBrush { Color.A: >= 240 } &&
            _collapseGlyph?.Foreground is SolidColorBrush { Color.R: 255, Color.G: 255, Color.B: 255 };
    }
    internal void ShowPosturePending(bool pending)
    {
        if (_collapseGlyph is null) return;
        _collapseGlyph.Text = pending ? "…" : _geometry.IsCollapsed ? "⌃" : "⌄";
        ToolTipService.SetToolTip(_collapseGlyph, pending ? "已记住操作，等待相关海报让出空间。" : "收起 / 展开");
    }

    private Grid CreateTitleChrome()
    {
        var header = new Grid();
        _titleCanvas = new Canvas { IsHitTestVisible = false };
        header.Children.Add(_titleCanvas);
        _titleCloud = new() { IsHitTestVisible = false };
        _titleCanvas.Children.Add(_titleCloud);
        var name = Text(_authoritativePoster?.Title ?? "Desktop collection", 14, 400, 255, 242, 242, 242);
        name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxLines = 1;
        _collapseGlyph = CreateCircleGlyph("⌄"); _moreGlyph = CreateCircleGlyph("⋯");
        _collapseGlyph.Foreground = _moreGlyph.Foreground = ColorBrush("#FFFFFF");
        Border Plate(UIElement child) => new() { Child = child, CornerRadius = new(6),
            Background = ColorBrush("#18232C", 245), IsHitTestVisible = false };
        _titleName = Plate(name); _titleCollapse = Plate(_collapseGlyph); _titleMore = Plate(_moreGlyph);
        _titleCanvas.Children.Add(_titleName); _titleCanvas.Children.Add(_titleCollapse); _titleCanvas.Children.Add(_titleMore);
        _titlePaintWidth = -1;
        header.SizeChanged += (_, _) => RefreshTitleChrome(header.ActualWidth);
        RefreshTitleChrome(_geometry.WidthDip);
        return header;
    }

    private void RefreshTitleChrome(double? actualWidth = null)
    {
        if (_titleCanvas is null || _titleCloud is null || _titleName is null || _titleCollapse is null || _titleMore is null) return;
        double width = Math.Max(PosterFrameGeometryPolicy.MinimumWidthDip, actualWidth ?? (_titlePaintWidth > 0 ? _titlePaintWidth : _geometry.WidthDip));
        var layout = PosterTitleChromePolicy.Resolve(width, _geometry.IsCollapsed, _presentation.TitleChrome);
        void Place(FrameworkElement element, TitleChromeRect rect)
        {
            Canvas.SetLeft(element, rect.X); Canvas.SetTop(element, rect.Y); element.Width = rect.Width; element.Height = rect.Height;
        }
        Place(_titleName, layout.Name); Place(_titleCollapse, layout.Collapse); Place(_titleMore, layout.More);
        bool cloud = layout.Kind == PosterTitleChromeKind.Cloud;
        _titleName.Background = cloud ? ColorBrush("#18232C", 180) : ColorBrush("#18232C", 220);
        if (_titleName.Child is TextBlock name) { name.Text = _authoritativePoster?.Title ?? "Desktop collection"; name.Padding = new(3, 1, 3, 0); }
        _titleCloud.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        if (cloud)
        {
            if (width != _titlePaintWidth || _titleCloud.Data is null)
            {
                var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
                var body = PosterTitleChromePolicy.CloudBody(width);
                geometry.Children.Add(new RectangleGeometry { Rect = new(body.X, body.Y, body.Width, body.Height) });
                foreach (var ellipse in PosterTitleChromePolicy.CloudEllipses(width))
                    geometry.Children.Add(new EllipseGeometry { Center = new(ellipse.X + ellipse.Width / 2, ellipse.Y + ellipse.Height / 2),
                        RadiusX = ellipse.Width / 2, RadiusY = ellipse.Height / 2 });
                _titleCloud.Data = geometry;
            }
            string color = _presentation.SceneCapability?.TitleColorBinding is { } binding
                ? _presentation.TemplateValues.Color(binding, "#3F576D") : "#3F576D";
            Color tone = ColorBrush(color).Color;
            byte Lighter(byte c) => (byte)(c + (255 - c) * .18);
            _titleCloud.Fill = new LinearGradientBrush { StartPoint = new(.25, 0), EndPoint = new(.7, 1),
                GradientStops = { new() { Offset = 0, Color = Color.FromArgb(255, Lighter(tone.R), Lighter(tone.G), Lighter(tone.B)) },
                    new() { Offset = .55, Color = tone }, new() { Offset = 1, Color = Color.FromArgb(255, (byte)(tone.R * .72), (byte)(tone.G * .72), (byte)(tone.B * .72)) } } };
        }
        _titlePaintWidth = width;
    }
}
