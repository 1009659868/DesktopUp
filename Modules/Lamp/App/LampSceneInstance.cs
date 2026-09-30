using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace DesktopUp;

internal sealed record LampSceneObservation(bool Active, bool Connected, bool Visible, bool DecorativeOnly,
    bool StaticOnly, int SceneChildren, double Width, double Height, double PoolDiameter, double PoolOpacity,
    double ConeOpacity, string PoolColor, int PaintedCards, double CardLightRadius, string CardBaseColor, string CardLitColor)
{
    public bool MotionRunning { get; init; }
    public bool FollowingPointer { get; init; }
    public bool ReducedMotion { get; init; }
    public bool GlobalMotionPaused { get; init; }
    public double LightX { get; init; }
    public double LightY { get; init; }
    public double FixtureAngle { get; init; }
    public long MotionFrames { get; init; }
}

// Trusted Lamp painter. It owns no input, state, spatial runtime or timers.
internal sealed class LampSceneInstance : IPosterSceneInstance
{
    private readonly Canvas _lampScene;
    private readonly Ellipse _lampPool, _lampBulb;
    private readonly Polygon _lampCone;
    private readonly Grid _lampFixture;
    private readonly RadialGradientBrush _lampPoolBrush;
    private readonly LinearGradientBrush _lampConeBrush;
    private readonly RotateTransform _lampFixtureRotation;
    private LampScenePose _lampPose;
    private bool _lampMotionRunning;
    private long _lampMotionFrames;
    private bool _disposed;


    private LampTemplateParameters _parameters = LampTemplatePolicy.Defaults;
    public FrameworkElement Root => _lampScene!;
    public bool WantsMotion => _parameters.MotionEnabled && (_parameters.FollowPointer || _parameters.IdleSway > 0);
    public void StopMotion() => _lampMotionRunning = false;

    public LampSceneInstance()
    {
        _lampScene = new Canvas { IsHitTestVisible = false };
        _lampPoolBrush = new RadialGradientBrush
        {
            Center = new(.5, .5), GradientOrigin = new(.5, .5), RadiusX = .5, RadiusY = .5,
            GradientStops = { new() { Offset = 0 }, new() { Offset = 1 } }
        };
        _lampConeBrush = new LinearGradientBrush
        {
            StartPoint = new(.5, 0), EndPoint = new(.5, 1),
            GradientStops = { new() { Offset = 0 }, new() { Offset = 1 } }
        };
        _lampPool = new Ellipse { Fill = _lampPoolBrush, IsHitTestVisible = false };
        _lampCone = new Polygon { Fill = _lampConeBrush, IsHitTestVisible = false };
        _lampFixtureRotation = new RotateTransform();
        _lampFixture = new Grid { Width = 48, Height = 66, IsHitTestVisible = false,
            RenderTransform = _lampFixtureRotation, RenderTransformOrigin = new(.5, 0) };
        var cord = new Rectangle { Width = 2, Height = 42, Fill = ColorBrush("#718074"),
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        var shade = new Border { Height = 24, Margin = new(0, 34, 0, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush { StartPoint = new(0, .5), EndPoint = new(1, .5),
                GradientStops = { new() { Offset = 0, Color = LampColor("#263732") },
                    new() { Offset = .55, Color = LampColor("#63735B") }, new() { Offset = 1, Color = LampColor("#283C35") } } },
            BorderBrush = ColorBrush("#75826B"), BorderThickness = new(1), CornerRadius = new(24, 24, 4, 4), IsHitTestVisible = false };
        _lampBulb = new Ellipse { Width = 44, Height = 4, Margin = new(0, 56, 0, 0),
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        _lampFixture.Children.Add(cord); _lampFixture.Children.Add(shade); _lampFixture.Children.Add(_lampBulb);
        _lampScene.Children.Add(_lampCone); _lampScene.Children.Add(_lampPool); _lampScene.Children.Add(_lampFixture);
    }

    public void Update(SceneRenderParameters parameters, SceneFrameContext frame)
    {
        if (_disposed) return;
        var values = parameters.Bindings;
        var defaults = LampTemplatePolicy.Defaults;
        string ColorValue(string key, string fallback) => values.TryGetValue(key, out var p) ? p.Color! : fallback;
        double NumberValue(string key, double fallback) => values.TryGetValue(key, out var p) ? p.Number!.Value : fallback;
        bool BooleanValue(string key, bool fallback) => values.TryGetValue(key, out var p) ? p.Boolean!.Value : fallback;
        var value = _parameters = new(
            ColorValue("lamp.color", defaults.LightColor), NumberValue("lamp.softness", defaults.Softness),
            NumberValue("lamp.intensity", defaults.Intensity), parameters.Surface.Background,
            parameters.Surface.Card ?? defaults.Card, parameters.Surface.BackgroundAlpha * 100.0 / 255,
            BooleanValue("scene.motion", true), BooleanValue("lamp.follow", true),
            NumberValue("lamp.sway", defaults.IdleSway), NumberValue("lamp.speed", defaults.MotionSpeed));
        _lampMotionRunning = frame.CanAnimate && value.MotionEnabled;
        LampScenePoint? localPointer = frame.Pointer is { } pointer ? new(pointer.X, pointer.Y) : null;
        double width = frame.Width, height = frame.Height;
        _lampPose = LampSceneMotion.Resolve(width, height, frame.Seconds, value, _lampMotionRunning, localPointer);
        if (_lampMotionRunning) _lampMotionFrames++;
        double x = _lampPose.X, y = _lampPose.Y, radius = 70 + value.Softness * 1.5, reach = 65 + value.Softness;
        if (_lampScene.Clip is RectangleGeometry clip) clip.Rect = new(0, 0, width, height);
        else _lampScene.Clip = new RectangleGeometry { Rect = new(0, 0, width, height) };
        _lampPool.Width = _lampPool.Height = radius * 2;
        Canvas.SetLeft(_lampPool, x - radius); Canvas.SetTop(_lampPool, y - radius);
        Canvas.SetLeft(_lampFixture, width * .75 - 24); Canvas.SetTop(_lampFixture, 0);
        if (_lampFixtureRotation is not null) _lampFixtureRotation.Angle = _lampPose.FixtureAngle;
        double radians = _lampPose.FixtureAngle * Math.PI / 180;
        double sourceX = width * .75 - Math.Sin(radians) * 57, sourceY = Math.Cos(radians) * 57;
        _lampCone.Points = new PointCollection { new(sourceX - 3, sourceY), new(sourceX + 3, sourceY),
            new(x + reach, y + 40), new(x - reach, y + 40) };
        _lampPoolBrush.GradientStops[0].Color = LampColor(value.LightColor, 38);
        _lampPoolBrush.GradientStops[1].Color = LampColor(value.LightColor, 0);
        _lampConeBrush.GradientStops[0].Color = LampColor(value.LightColor, 38);
        _lampConeBrush.GradientStops[1].Color = LampColor(value.LightColor, 4);
        _lampPool.Opacity = _lampCone.Opacity = value.Intensity / 100;
        var bulbColor = LampBlend(LampColor("#63735B"), LampColor(value.LightColor), value.Intensity / 100);
        if (_lampBulb.Fill is SolidColorBrush bulbBrush) bulbBrush.Color = bulbColor;
        else _lampBulb.Fill = new SolidColorBrush(bulbColor);
    }

    public void PaintCard(SceneCardPaint paint)
    {
        var card = paint.Card; bool selected = paint.Selected;
        var value = _parameters; var basis = LampColor(value.Card); var light = LampColor(value.LightColor);
        var brush = card.Background as RadialGradientBrush ?? new RadialGradientBrush
        {
            Center = new(.5, 0), GradientOrigin = new(.5, 0),
            GradientStops = { new() { Offset = 0 }, new() { Offset = .85 } }
        };
        brush.RadiusX = brush.RadiusY = .5 + value.Softness / 100;
        double illumination = .2;
        if (paint.Left is { } left && paint.Top is { } top && card.ActualWidth > 0 && card.ActualHeight > 0)
        {
            double localX = _lampPose.X - left, localY = _lampPose.Y - top;
            brush.Center = brush.GradientOrigin = new(localX / card.ActualWidth, localY / card.ActualHeight);
            double distance = Math.Sqrt(Math.Pow(localX - card.ActualWidth / 2, 2) + Math.Pow(localY - card.ActualHeight / 2, 2));
            illumination = .025 + Math.Max(0, 1 - distance / (150 + value.Softness * 2)) * .3;
        }
        brush.GradientStops[0].Color = LampBlend(basis, light, illumination * value.Intensity / 100);
        brush.GradientStops[1].Color = basis;
        card.Background = brush;
        if (!selected)
        {
            var edgeColor = LampColor(value.LightColor, (byte)Math.Round((.1 + illumination) * 255 * value.Intensity / 100));
            if (card.BorderBrush is SolidColorBrush edgeBrush) edgeBrush.Color = edgeColor;
            else card.BorderBrush = new SolidColorBrush(edgeColor);
        }
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lampScene.Children.Clear();
        _lampMotionRunning = false; _lampPose = default;
    }

    internal LampSceneObservation ReadObservation(IEnumerable<RadialGradientBrush> cards)
    {
        bool decorative = _lampScene is not null && !_lampScene.IsHitTestVisible &&
            _lampScene.Children.All(child => !child.IsHitTestVisible) &&
            _lampFixture is not null && _lampFixture.Children.All(child => !child.IsHitTestVisible);
        var color = _lampPoolBrush?.GradientStops[0].Color;
        var cardBrushes = cards.ToArray();
        var card = cardBrushes.FirstOrDefault();
        return new(_lampScene is not null, _lampScene is not null && VisualTreeHelper.GetParent(_lampScene) is not null,
            _lampScene?.Visibility == Visibility.Visible, decorative, !_lampMotionRunning, _lampScene?.Children.Count ?? 0,
            _lampScene?.ActualWidth ?? 0, _lampScene?.ActualHeight ?? 0, _lampPool?.Width ?? 0,
            _lampPool?.Opacity ?? 0, _lampCone?.Opacity ?? 0,
            color is { } c ? LampColorText(c) : "", cardBrushes.Length, card?.RadiusX ?? 0,
            card is null ? "" : LampColorText(card.GradientStops[1].Color),
            card is null ? "" : LampColorText(card.GradientStops[0].Color))
        {
            MotionRunning = _lampMotionRunning, FollowingPointer = _lampPose.FollowingPointer,
            LightX = _lampPose.X, LightY = _lampPose.Y,
            FixtureAngle = _lampPose.FixtureAngle, MotionFrames = _lampMotionFrames
        };
    }

    private static SolidColorBrush ColorBrush(string hex, byte alpha = 255) => new(LampColor(hex, alpha));
    private static Color LampColor(string hex, byte alpha = 255) => Color.FromArgb(alpha, Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));
    private static string LampColorText(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    private static Color LampBlend(Color basis, Color light, double amount) => Color.FromArgb(255,
        (byte)Math.Round(basis.R + (light.R - basis.R) * amount),
        (byte)Math.Round(basis.G + (light.G - basis.G) * amount),
        (byte)Math.Round(basis.B + (light.B - basis.B) * amount));
}
