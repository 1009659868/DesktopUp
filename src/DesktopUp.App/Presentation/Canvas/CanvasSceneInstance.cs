using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal static class CanvasRegistration
{
    internal static void Register(CanvasImages images) => SceneModuleRegistry.Register("canvas.paint@1", () => new CanvasSceneInstance(images));
}

/// <summary>Stable-ID decorative nodes only. The host retains content, input and one shared clock.</summary>
internal sealed partial class CanvasSceneInstance(CanvasImages images) : IPosterSceneInstance
{
    private readonly Microsoft.UI.Xaml.Controls.Canvas root = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, FrameworkElement> visuals = new(StringComparer.Ordinal);
    private readonly CanvasLayoutCache cache = new();
    private CanvasDesign? design;
    private CanvasLayoutResult? layout;
    private double lastClock, activeSeconds;
    private bool running;
    private CanvasLayerState? layerState;
    public FrameworkElement Root => root;
    public bool WantsMotion => scripts?.Active == true || ObservesPointer || !ReadCollapsed() && design is not null && design.Recipe.Nodes.Any(n => n.Type == "effect.rain@1" && layerState?.Hidden.Contains(n.Id) != true && CanvasDeclaration.Property(design, n, "intensity").Number > 0);
    public void Update(SceneRenderParameters parameters, SceneFrameContext frame)
    {
        if (parameters.Canvas is not { } next || frame.Width <= 0 || frame.Height <= CanvasLayout.Header) return;
        if (!ReferenceEquals(scriptDesign, next))
        {
            // State publication may deserialize the same design after a posture commit. It must
            // not cancel a sequence waiting for that commit. Hash only on reference changes.
            if (scriptDesign is null || CanvasDeclaration.Digest(scriptDesign) != CanvasDeclaration.Digest(next))
            { scripts?.Stop(); scripts = new(next, () => ReadCollapsed(), value => RequestPosture(value)); behaviorVisible = false; behaviorCollapsed = ReadCollapsed(); }
            scriptDesign = next;
        }
        if (!frame.Visible)
        {
            running = false;
            if (ReadCollapsed()) { UpdateBehavior(frame); return; }
            scripts?.Stop(); behaviorVisible = false; return;
        }
        var resolved = cache.Resolve(next, frame.Width, frame.Height);
        if (!ReferenceEquals(design, next) || !ReferenceEquals(layout, resolved))
        {
            design = next; layout = resolved; layerState = CanvasLayerState.Resolve(next);
            var retained = new HashSet<string>(StringComparer.Ordinal);
            int order = 0;
            foreach (var p in resolved.Nodes)
            {
                var n = p.Node;
                if (layerState.Hidden.Contains(n.Id)) continue;
                if (n.Type is "layout.canvas@1" or "layout.stack@1" or "layout.group@1" or "content.slot@1" or "host.title@1") continue;
                retained.Add(n.Id);
                if (!visuals.TryGetValue(n.Id, out var element) || (string?)element.Tag != n.Type)
                {
                    if (element is not null) root.Children.Remove(element);
                    element = Create(n.Type); element.Tag = n.Type; element.IsHitTestVisible = false;
                    visuals[n.Id] = element; root.Children.Add(element);
                }
                var b = p.Bounds; element.Width = b.Width; element.Height = b.Height;
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(element, b.X); Microsoft.UI.Xaml.Controls.Canvas.SetTop(element, b.Y);
                Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(element, order++);
                Paint(element, n);
                element.RenderTransformOrigin = new(.5, .5);
                element.RenderTransform = p.Rotation == 0 ? null : new RotateTransform { Angle = p.Rotation };
                if (element is TextBlock text)
                {
                    text.Measure(new(b.Width, double.PositiveInfinity));
                    if (text.DesiredSize.Height > b.Height + 1)
                        throw new ArgumentException("文字超出当前布局预算，请扩大外框或缩短文字。");
                }
                if (element is Microsoft.UI.Xaml.Controls.Canvas rain)
                    rain.Clip = new RectangleGeometry { Rect = new(0, 0, b.Width, b.Height) };
            }
            foreach (var id in visuals.Keys.Where(id => !retained.Contains(id)).ToArray()) { root.Children.Remove(visuals[id]); visuals.Remove(id); }
            root.Clip = new RectangleGeometry { Rect = new(0, 0, frame.Width, frame.Height) };
            RebuildBehaviorGeometry();
        }
        UpdateBehavior(frame);
        bool animate = frame.CanAnimate && WantsMotion;
        if (animate && running) activeSeconds += Math.Clamp(frame.Seconds - lastClock, 0, .1);
        lastClock = frame.Seconds; running = animate;
        foreach (var p in resolved.Nodes.Where(p => p.Node.Type == "effect.rain@1" && layerState?.Hidden.Contains(p.Node.Id) != true))
        {
            var panel = (Microsoft.UI.Xaml.Controls.Canvas)visuals[p.Node.Id];
            int count = (int)Math.Round(CanvasDeclaration.Property(next, p.Node, "intensity").Number!.Value * panel.Children.Count);
            for (int i = 0; i < panel.Children.Count; i++)
            {
                var line = (Line)panel.Children[i]; line.Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
                if (i >= count) continue;
                double x = ((i * .61803398875) % 1) * p.Bounds.Width;
                double y = ((i * .38196601125 + activeSeconds * .22) % 1) * (p.Bounds.Height + 30) - 20;
                line.X1 = x; line.X2 = x - 5; line.Y1 = y; line.Y2 = y + 16 + i % 4 * 3;
            }
        }
    }
    private static FrameworkElement Create(string type) => type switch
    {
        "draw.panel@1" => new Border(), "draw.ellipse@1" => new Ellipse(),
        "draw.text@1" => new TextBlock { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None },
        "draw.image@1" => new Image { Stretch = Stretch.UniformToFill },
        "effect.rain@1" => Rain(), _ => throw new ArgumentException("Unregistered canvas renderer.")
    };
    private static Microsoft.UI.Xaml.Controls.Canvas Rain()
    {
        var panel = new Microsoft.UI.Xaml.Controls.Canvas();
        var ink = new SolidColorBrush(global::Windows.UI.Color.FromArgb(100, 147, 201, 233));
        for (int i = 0; i < 48; i++) panel.Children.Add(new Line { Stroke = ink, StrokeThickness = 1 });
        return panel;
    }
    private void Paint(FrameworkElement visual, CanvasNode node)
    {
        CanvasValue P(string key) => CanvasDeclaration.Property(design!, node, key);
        if (visual is Border panel) { panel.Background = Brush(P("fill").Text!); panel.Opacity = P("opacity").Number!.Value; panel.CornerRadius = new(P("radius").Number!.Value * layout!.Scale); }
        else if (visual is Ellipse ellipse) { ellipse.Fill = Brush(P("fill").Text!); ellipse.Opacity = P("opacity").Number!.Value; }
        else if (visual is TextBlock text) { text.Text = P("text").Text; text.Foreground = Brush(P("color").Text!);
            CanvasTypography.Apply(text, node.Typography is { } typography ? typography with { LetterSpacing = typography.LetterSpacing * layout!.Scale } : null, P("fontSize").Number!.Value * layout!.Scale); }
        else if (visual is Image image) { image.Source = images.Load(P("asset").Text!); image.Opacity = P("opacity").Number!.Value; }
    }
    private static SolidColorBrush Brush(string color) => new(global::Windows.UI.Color.FromArgb(255,
        Convert.ToByte(color.Substring(1, 2), 16), Convert.ToByte(color.Substring(3, 2), 16), Convert.ToByte(color.Substring(5, 2), 16)));
    public void PaintCard(SceneCardPaint paint) { }
    public void StopMotion() { running = false; scripts?.Stop(); PaintBehavior(); }
    public void Dispose() { running = false; scripts?.Stop(); root.Children.Clear(); visuals.Clear(); design = null; layout = null; }
}
