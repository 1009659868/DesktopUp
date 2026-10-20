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
    private Canvas? _titleCloudGroup;
    private Microsoft.UI.Xaml.Shapes.Path? _titleVapor, _titleWisp;
    private CompositeTransform _vaporTransform = new(), _wispTransform = new();
    private double _titlePaintWidth = -1;
    private TitleCloudPose _titlePose;
    private CompositeTransform _titleTransform = new();

    private void PaintTitlePose(double width)
    {
        _titlePose = _titlePose.Fit(width);
        _titleTransform.CenterX = width / 2; _titleTransform.CenterY = TitleCloudPose.CenterY;
        _titleTransform.ScaleX = _titleTransform.ScaleY = _titlePose.Scale;
        _titleTransform.TranslateX = _titlePose.X; _titleTransform.TranslateY = _titlePose.Y;
        _titleTransform.Rotation = _titlePose.Angle;
        _vaporTransform.CenterX=width/2; _vaporTransform.CenterY=42;
        _vaporTransform.TranslateX=_titlePose.Vapor*5; _vaporTransform.TranslateY=0;
        _wispTransform.CenterX=width/2; _wispTransform.CenterY=42;
        _wispTransform.TranslateX=_titlePose.Wisp*6; _wispTransform.TranslateY=0;
        if(_titleVapor is not null) _titleVapor.Opacity=.12+(_titlePose.Vapor+1)*.025;
        if(_titleWisp is not null) _titleWisp.Opacity=.10+(_titlePose.Wisp+1)*.02;
    }

    private void TryPaintTitleMotion(TitleCloudPose requested)
    {
        bool sizeChange=requested.Bounded().Scale!=_titlePose.Bounded().Scale;
        // A settings edit changes size only, never uses that permission to advance wind.
        // It needs no decorative timer, including when all environmental motion is off.
        if(sizeChange) requested=_titlePose with { Scale=requested.Bounded().Scale };
        var pose = requested.Fit(_monitor is null ? _geometry.WidthDip :
            inputRouter.ReadSurface(inputSession)?.Frame.Width / _monitor.ScaleX ?? _geometry.WidthDip);
        if (_presentation.TitleChrome != PosterTitleChromeKind.Cloud || _titleCloud is null || _monitor is null ||
            _transitionLayout is not null || _spatialGesture is not null || pose == _titlePose ||
            (sizeChange ? _inputIsland?.CanUpdateTitleSize : _inputIsland?.CanAnimateTitle) != true || inputRouter.ReadSurface(inputSession) is not { } surface) return;
        var grips = TitleCloudInputGeometry.Resolve(surface.Frame.Width / _monitor.ScaleX, _geometry.IsCollapsed,
            pose, _monitor.ScaleX, _monitor.ScaleY);
        var regions = surface.Layout.Regions.Where(r => r.Kind != PosterInputTargetKind.TitleBar)
            .Concat(grips.Select(b => new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, b))).ToArray();
        var next = surface.Layout with { Revision = checked(++_layoutRevision),
            Regions = PosterInputRegionClipPolicy.Clip(surface.Frame.Width, surface.Frame.Height, regions) };
        // Same dispatcher turn: native sparse region first, visual transform second.
        // No full ApplyFrame, content layout, menu cancellation or authoring write.
        if (!_inputIsland!.TryApplyTitleMotion(next,sizeChange)) return;
        _titlePose = pose;
        PaintTitlePose(surface.Frame.Width / _monitor.ScaleX);
    }
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
            _titleName.Background is null && _titleCollapse.Background is null && _titleMore.Background is null &&
            _titleName.Child is TextBlock name && name.Foreground is SolidColorBrush text &&
            _collapseGlyph?.Foreground is SolidColorBrush actions && _moreGlyph?.Foreground is SolidColorBrush more &&
            text.Color == ColorBrush((_presentation.Parameters.TitleColors ?? new()).Text).Color &&
            actions.Color == ColorBrush((_presentation.Parameters.TitleColors ?? new()).Actions).Color && more.Color == actions.Color;
    }
    internal void ShowPosturePending(bool pending)
    {
        if (_collapseGlyph is null) return;
        _collapseGlyph.Text = pending ? "…" : _geometry.IsCollapsed ? "⌃" : "⌄";
        ToolTipService.SetToolTip(_collapseGlyph, pending ? "已记住操作，等待相关海报让出空间。" : "收起 / 展开");
    }

    internal bool VerifyTitleVisibilityForProbe(bool show) =>
        _presentation.Parameters.IsTitleVisible == show &&
        _titleName?.Child is TextBlock name && name.Text == _authoritativePoster?.Title &&
        name.Opacity == (show ? 1 : 0) && _titleName.Opacity == 1 &&
        (_metaPanel?.Children.ElementAtOrDefault(1) is not TextBlock metaTitle ||
            metaTitle.Text == _authoritativePoster?.Title && metaTitle.Opacity == (show ? 1 : 0)) &&
        _titleCollapse?.Opacity == 1 && _titleMore?.Opacity == 1 &&
        _collapseGlyph?.Opacity == 1 && _moreGlyph?.Opacity == 1 && VerifyTitleChromeForProbe();

    private Grid CreateTitleChrome()
    {
        var header = new Grid();
        _titleCanvas = new Canvas { IsHitTestVisible = false };
        header.Children.Add(_titleCanvas);
        _titleTransform = new();
        _titleCloudGroup = new() { IsHitTestVisible=false, RenderTransform=_titleTransform };
        _titleCloud = new() { IsHitTestVisible = false };
        _vaporTransform=new(); _wispTransform=new();
        _titleVapor=new() { IsHitTestVisible=false, RenderTransform=_vaporTransform };
        _titleWisp=new() { IsHitTestVisible=false, RenderTransform=_wispTransform };
        _titleCloudGroup.Children.Add(_titleCloud); _titleCloudGroup.Children.Add(_titleVapor); _titleCloudGroup.Children.Add(_titleWisp);
        _titleCanvas.Children.Add(_titleCloudGroup);
        var name = Text(_authoritativePoster?.Title ?? "Desktop collection", 14, 400, 255, 242, 242, 242);
        name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxLines = 1;
        _collapseGlyph = CreateCircleGlyph("⌄"); _moreGlyph = CreateCircleGlyph("⋯");
        _collapseGlyph.Foreground = _moreGlyph.Foreground = ColorBrush("#FFFFFF");
        Border Slot(UIElement child) => new() { Child = child, IsHitTestVisible = false };
        _titleName = Slot(name); _titleCollapse = Slot(_collapseGlyph); _titleMore = Slot(_moreGlyph);
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
        if (_titleCanvas.Clip is not RectangleGeometry clip) _titleCanvas.Clip = clip = new();
        clip.Rect = new(0, 0, width, layout.Height);
        void Place(FrameworkElement element, TitleChromeRect rect)
        {
            Canvas.SetLeft(element, rect.X); Canvas.SetTop(element, rect.Y); element.Width = rect.Width; element.Height = rect.Height;
        }
        Place(_titleName, layout.Name); Place(_titleCollapse, layout.Collapse); Place(_titleMore, layout.More);
        bool cloud = layout.Kind == PosterTitleChromeKind.Cloud;
        var ink = _presentation.Parameters.TitleColors ?? new();
        _collapseGlyph!.Foreground = ColorBrush(ink.Actions); _moreGlyph!.Foreground = ColorBrush(ink.Actions);
        if (_titleName.Child is TextBlock name)
        { name.Text = _authoritativePoster?.Title ?? "Desktop collection"; name.Padding = new(3, 1, 3, 0); name.Foreground = ColorBrush(ink.Text);
            name.Opacity = _presentation.Parameters.IsTitleVisible ? 1 : 0; }
        if (_metaPanel?.Children.ElementAtOrDefault(1) is TextBlock metaTitle)
            metaTitle.Opacity = _presentation.Parameters.IsTitleVisible ? 1 : 0;
        _titleCloudGroup!.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        if (cloud)
        {
            PaintTitlePose(width);
            if (width != _titlePaintWidth || _titleCloud.Data is null)
            {
                var points=TitleCloudShape.Resolve(width);
                PathGeometry Shape()
                {
                    var figure=new PathFigure { StartPoint=new(points[0].X,points[0].Y), IsClosed=true, IsFilled=true };
                    var line=new PolyLineSegment();
                    foreach(var p in points.Skip(1)) line.Points.Add(new(p.X,p.Y));
                    figure.Segments.Add(line);
                    var geometry=new PathGeometry(); geometry.Figures.Add(figure); return geometry;
                }
                _titleCloud.Data=Shape();
                // Bounded broad cloud masses, not wind strokes. Their full drift
                // envelope stays in the core; WinUI Clip supports rectangles only.
                _titleVapor!.Data=new EllipseGeometry { Center=new(width*.5,38),RadiusX=width*.23,RadiusY=7 };
                _titleWisp!.Data=new EllipseGeometry { Center=new(width*.53,44),RadiusX=width*.19,RadiusY=5 };
            }
            string color = _presentation.SceneCapability?.TitleColorBinding is { } binding
                ? _presentation.TemplateValues.Color(binding, "#3F576D") : "#3F576D";
            Color tone = ColorBrush(color).Color;
            byte Lighter(byte c) => (byte)(c + (255 - c) * .18);
            _titleCloud.Fill = new LinearGradientBrush { StartPoint = new(.25, 0), EndPoint = new(.7, 1),
                GradientStops = { new() { Offset = 0, Color = Color.FromArgb(255, Lighter(tone.R), Lighter(tone.G), Lighter(tone.B)) },
                    new() { Offset = .55, Color = tone }, new() { Offset = 1, Color = Color.FromArgb(255, (byte)(tone.R * .72), (byte)(tone.G * .72), (byte)(tone.B * .72)) } } };
            _titleVapor!.Fill=new LinearGradientBrush { StartPoint=new(.3,0),EndPoint=new(.6,1),
                GradientStops={new(){Offset=0,Color=Color.FromArgb(255,223,234,241)},new(){Offset=1,Color=Color.FromArgb(0,223,234,241)}} };
            _titleWisp!.Fill=new LinearGradientBrush { StartPoint=new(.5,0),EndPoint=new(.7,1),
                GradientStops={new(){Offset=0,Color=Color.FromArgb(0,223,234,241)},new(){Offset=.65,Color=tone},new(){Offset=1,Color=Color.FromArgb(0,tone.R,tone.G,tone.B)}} };
        }
        _titlePaintWidth = width;
    }
}
