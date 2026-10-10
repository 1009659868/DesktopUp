using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
namespace DesktopUp;

internal sealed record RainSceneObservation(int Pool, int VisibleDrops, bool CloudVisible, bool Running,
    long Frames, double ActiveSeconds, RainSceneEnvelope Envelope, RainCloudPose Cloud,
    string CloudColor, string RainColor, bool DecorativeOnly, int Nodes,
    bool TitleWeatherVisible, double TitleWindX, double LightningOpacity, double LightningInterval)
{
    public int TitleNodes { get; init; }
    public double CloudSize { get; init; }
}

// Trusted, timer-free decoration. No state commands, item identities, input or layout ownership.
internal sealed class RainSceneInstance : IPosterSceneInstance, IPosterTitleMotion
{
    private readonly Canvas _root = new() { IsHitTestVisible = false };
    private readonly Canvas _rain = new() { IsHitTestVisible = false };
    private readonly Viewbox _cloud;
    private readonly CompositeTransform _cloudTransform = new();
    private readonly Line[] _drops = new Line[RainSceneMotion.PoolSize];
    private readonly SolidColorBrush _rainBrush = new();
    private readonly LinearGradientBrush _tone;
    private readonly RainLightningDecoration _lightning;
    private readonly SceneActiveTime _time = new();
    private readonly SceneActiveTime _cloudTime = new();
    private readonly RainLightningSchedule _lightningSchedule = new(unchecked((uint)Random.Shared.Next()));
    private TemplateParameterValues _values = new([]);
    private IReadOnlyDictionary<string, TemplateParameterValue>? _bindingSource;
    private Color _cardColor, _edgeColor;
    private RainSceneEnvelope _area;
    private RainCloudPose _pose;
    private SceneFrameContext _lastFrame;
    private bool _running, _disposed;
    private int _count;
    private long _frames;
    public FrameworkElement Root => _root;
    public TitleCloudPose TitlePose => new(_pose.X, _pose.Y, _pose.Angle, _pose.Vapor, _pose.Wisp,
        TitleCloudPose.Size(_values.Number("cloud.size",1)));
    public bool WantsMotion => !_disposed && _values.Boolean("scene.motion", true) &&
        (_values.Number("rain.count", 28) > 0 || _values.Number("cloud.wind", 35) > 0 || _values.Boolean("rain.lightning", true));
    public void StopMotion() { _running = false; _time.Stop(); _cloudTime.Stop(); _lightning.Stop(); }

    public RainSceneInstance()
    {
        _cloud = CloudArt(out _tone);
        _cloud.RenderTransform = _cloudTransform; _cloud.RenderTransformOrigin = new(.5, .5);
        _root.Children.Add(_rain);
        for (int i = 0; i < _drops.Length; i++)
        {
            var line = _drops[i] = new Line { Stroke = _rainBrush, StrokeThickness = .7, Opacity = .45, IsHitTestVisible = false };
            _rain.Children.Add(line);
        }
        _root.Children.Add(_cloud);
        _lightning = new(_rainBrush);
        _root.Children.Add(_lightning.Root);
    }

    public void Update(SceneRenderParameters parameters, SceneFrameContext frame)
    {
        if (_disposed) return;
        _lastFrame = frame;
        if (!ReferenceEquals(_bindingSource, parameters.Bindings))
        {
            _bindingSource = parameters.Bindings; _values = new(parameters.Bindings);
            var color = Parse(_values.Color("cloud.color", "#3F576D"));
            _tone.GradientStops[0].Color = Mix(color, .22);
            _tone.GradientStops[1].Color = color; _tone.GradientStops[2].Color = Mix(color, -.24);
            _rainBrush.Color = Parse(_values.Color("rain.color", "#93C9E9"));
            _cardColor = Parse(_values.Color("item.color", "#253D53"), 16);
            _edgeColor = Parse(_values.Color("rain.color", "#93C9E9"), 34);
        }
        _area = RainSceneMotion.Envelope(frame);
        _running = frame.CanAnimate && WantsMotion;
        double seconds = _time.Step(frame.Seconds, _running);
        if (_running) _frames++;
        _count = Math.Clamp((int)Math.Floor(_values.Number("rain.count", 28)), 0, _drops.Length);
        double cloudSeconds = _cloudTime.Step(frame.Seconds, _running && !frame.TitleMotionPaused);
        _pose = RainSceneMotion.Cloud(cloudSeconds, _values.Number("cloud.wind", 35));
        _cloud.Width = _area.Width; _cloud.Height = _area.Height;
        // Cloud title has ONE host silhouette. Host applies this scene's bounded pose to
        // both its paint and native sparse hit map; no duplicate background cloud.
        bool titleCloud = frame.TitleChrome == PosterTitleChromeKind.Cloud;
        _cloud.Visibility = !titleCloud && _area.Height > 0 && _area.Width > 0 ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(_cloud, _area.Left); Canvas.SetTop(_cloud, _area.Top);
        _cloudTransform.TranslateX = _pose.X; _cloudTransform.TranslateY = _pose.Y; _cloudTransform.Rotation = _pose.Angle;
        if (_root.Clip is not RectangleGeometry clip) _root.Clip = clip = new();
        clip.Rect = new(0, 0, frame.Width, frame.Height);
        if (_rain.Clip is not RectangleGeometry rainClip) _rain.Clip = rainClip = new();
        double rainTop = _area.Top + _area.Height - 8;
        rainClip.Rect = new(0, rainTop, frame.Width, Math.Max(0, _area.Bottom - rainTop));
        for (int i = 0; i < _drops.Length; i++)
        {
            var drop = _drops[i];
            drop.Visibility = i < _count && _area.Height > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (i >= _count) continue;
            var p = RainSceneMotion.Drop(i, seconds, _values.Number("rain.speed", 1), _values.Number("cloud.wind", 35), _area);
            drop.X1 = p.X; drop.X2 = p.X - p.Slant; drop.Y1 = p.Y; drop.Y2 = p.Y + p.Length;
        }
        var lightning = _lightningSchedule.Step(seconds, _running && _values.Boolean("rain.lightning", true),
            _values.Number("rain.lightningInterval",RainLightningSchedule.DefaultInterval));
        _lightning.Update(_area, lightning.Opacity,lightning.StrokeSeed);
    }

    public void PaintCard(SceneCardPaint paint)
    {
        if (paint.Card.Background is SolidColorBrush brush) brush.Color = _cardColor;
        else paint.Card.Background = new SolidColorBrush(_cardColor);
        if (!paint.Selected)
        {
            if (paint.Card.BorderBrush is SolidColorBrush border) border.Color = _edgeColor;
            else paint.Card.BorderBrush = new SolidColorBrush(_edgeColor);
        }
        // Never replace icons/labels, focus outlines, sizes, positions or hit targets.
    }
    internal RainSceneObservation Observe() => new(_drops.Length, _drops.Count(d => d.Visibility == Visibility.Visible),
        _cloud.Visibility == Visibility.Visible || _lastFrame.TitleChrome == PosterTitleChromeKind.Cloud, _running, _frames, _time.Seconds, _area, _pose,
        _values.Color("cloud.color", "#3F576D"), _values.Color("rain.color", "#93C9E9"), !_root.IsHitTestVisible,
        _root.Children.Count + _rain.Children.Count + _lightning.Nodes,
        _lastFrame.TitleChrome == PosterTitleChromeKind.Cloud, _pose.X, _lightning.Opacity,
        _values.Number("rain.lightningInterval",RainLightningSchedule.DefaultInterval))
        { CloudSize=TitleCloudPose.Size(_values.Number("cloud.size",1)) };
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; StopMotion(); _rain.Children.Clear(); _root.Children.Clear(); _lightning.Dispose();
    }

    // Fixed code-native paths adapted from our accepted UI C prototype; never parse imported markup.
    internal static Viewbox CloudArt(out LinearGradientBrush tone)
    {
        tone = new LinearGradientBrush { StartPoint = new(.2, 0), EndPoint = new(.7, 1),
            GradientStops = { new() { Offset = 0, Color = Parse("#697C8D") }, new() { Offset = .5, Color = Parse("#3F576D") }, new() { Offset = 1, Color = Parse("#304253") } } };
        var cloud = new Canvas { Width = 360, Height = 126, IsHitTestVisible = false };
        cloud.Children.Add(Path("M12 83C30 69 47 73 63 61C86 38 119 41 141 49C165 26 195 31 214 49C241 36 260 53 279 59C304 61 331 70 346 88C320 104 294 108 268 105C237 116 203 108 175 111C143 120 117 108 91 111C59 112 25 101 12 83Z", tone, .32));
        cloud.Children.Add(Path("M35 94C16 93 11 78 22 67C28 59 40 58 47 60C44 41 60 29 78 35C90 12 127 11 145 29C166 9 204 14 216 38C239 24 268 36 272 57C291 47 314 58 316 75C337 72 345 90 327 99C310 110 288 103 268 105C248 113 228 107 208 109C184 118 167 109 147 112C124 116 103 110 83 112C67 111 45 108 35 94Z", tone, 1));
        var mist = new LinearGradientBrush { StartPoint = new(.5, 0), EndPoint = new(.5, 1),
            GradientStops = { new() { Offset = 0, Color = Parse("#DFEAF1", 65) }, new() { Offset = 1, Color = Parse("#DFEAF1", 0) } } };
        cloud.Children.Add(Path("M47 76C72 75 77 49 103 50C127 54 142 31 162 34C186 32 198 53 215 49C238 43 249 69 270 67C284 65 303 75 312 86C282 80 267 85 246 83C216 84 207 78 180 86C157 84 143 91 120 87C97 91 81 80 47 76Z", mist, 1));
        cloud.Children.Add(Path("M35 95C87 91 104 108 152 102S232 98 277 101C244 115 215 112 181 116C124 120 74 109 35 95Z", tone, .24));
        return new Viewbox { Child = cloud, Stretch = Stretch.Fill, IsHitTestVisible = false };
    }
    private static Microsoft.UI.Xaml.Shapes.Path Path(string data, Brush fill, double opacity)
    {
        var result = (Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load("<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='" + data + "'/>");
        result.Fill = fill; result.Opacity = opacity; result.IsHitTestVisible = false; return result;
    }
    private static Color Parse(string text, byte alpha = 255) => Color.FromArgb(alpha,
        Convert.ToByte(text.Substring(1, 2), 16), Convert.ToByte(text.Substring(3, 2), 16), Convert.ToByte(text.Substring(5, 2), 16));
    private static Color Mix(Color c, double amount)
    {
        byte Channel(byte v) => (byte)Math.Round(amount > 0 ? v + (255 - v) * amount : v * (1 + amount));
        return Color.FromArgb(c.A, Channel(c.R), Channel(c.G), Channel(c.B));
    }
}
