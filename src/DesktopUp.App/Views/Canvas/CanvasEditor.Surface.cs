using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private readonly Dictionary<string, FrameworkElement> views = new();
    private readonly List<FrameworkElement> adorners = [];
    private readonly List<(string Id, FrameworkElement View, int X, int Y, string Kind)> selectionVisuals = [];
    private CanvasLayoutResult? geometry;
    private CanvasLayerState layerState = new(new HashSet<string>(), new HashSet<string>());
    private CanvasDesign? pending;
    private string? contentInk;
    private bool gestureFrameQueued, handleGesture;
    private void Render(bool properties = true)
    {
        var design = Shown; geometry = CanvasLayout.ForAuthoring(design, board.Width, board.Height);
        layerState = CanvasLayerState.Resolve(design);
        if (!design.Recipe.Nodes.Any(n => n.Id == context)) context = design.Recipe.Nodes[0].Id;
        ClearAdorners();
        var retained = new HashSet<string>(); int z = 0;
        foreach (var p in geometry.Nodes)
        {
            var n = p.Node;
            if (n.Parent is null || n.Type is "layout.group@1" or "layout.stack@1" || layerState.Hidden.Contains(n.Id)) continue;
            retained.Add(n.Id);
            if (!views.TryGetValue(n.Id, out var e) || (string?)e.Tag != n.Type)
            {
                if (e is not null) board.Children.Remove(e);
                e = n.Type switch
                {
                    "draw.text@1" => new TextBlock { TextWrapping = TextWrapping.Wrap },
                    "draw.ellipse@1" => new Ellipse(),
                    "draw.image@1" => new Image { Stretch = Stretch.UniformToFill },
                    "content.slot@1" => ContentGuide(),
                    _ => new Border()
                };
                e.Tag = n.Type; e.IsHitTestVisible = false; views[n.Id] = e; board.Children.Add(e);
            }
            CanvasValue P(string key) => CanvasDeclaration.Property(design, n, key);
            if (n.Type == "draw.panel@1" && e is Border panel)
            { panel.Background = Ink(P("fill").Text!); panel.CornerRadius = new(P("radius").Number!.Value); panel.Opacity = P("opacity").Number!.Value; }
            if (e is Ellipse ellipse) { ellipse.Fill = Ink(P("fill").Text!); ellipse.Opacity = P("opacity").Number!.Value; }
            if (e is TextBlock text) { text.Text = P("text").Text; text.Foreground = Ink(P("color").Text!); CanvasTypography.Apply(text, n.Typography, P("fontSize").Number!.Value); }
            if (e is Image image) { image.Source = pictures.Load(P("asset").Text!); image.Opacity = P("opacity").Number!.Value; }
            if (n.Type == "content.slot@1" && e is Border guide)
            {
                // An outline is an editor guide, not a painted rectangle in the saved design.
                bool active = selected.Contains(n.Id) || tool == "content.slot@1";
                guide.Visibility = showContentReference || active ? Visibility.Visible : Visibility.Collapsed;
                guide.BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(active ? (byte)210 : (byte)45, 123, 201, 173));
            }
            Place(e, p.Bounds); RotateView(e, p.Rotation); Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(e, z++);
        }
        foreach (var id in views.Keys.Where(id => !retained.Contains(id)).ToArray()) { board.Children.Remove(views[id]); views.Remove(id); }
        foreach (var id in selected.ToArray())
        {
            var p = geometry.Nodes.FirstOrDefault(p => p.Node.Id == id);
            if (p is null) { selected.Remove(id); continue; }
            if (layerState.Hidden.Contains(id)) continue;
            var outline = new Border { BorderBrush = Ink(layerState.Locked.Contains(id) ? "#ADB1B0" : "#438CFF"), BorderThickness = new(1.5), IsHitTestVisible = false };
            Adorn(outline, p.Bounds); RotateView(outline, p.Rotation); selectionVisuals.Add((id, outline, 0, 0, "outline"));
            if (selected.Count == 1 && pending is null && !layerState.Locked.Contains(id)) Handles(p);
        }
        if (contentInk != design.Recipe.ContentForeground) { contentInk = design.Recipe.ContentForeground; RefreshContentReadback(); }
        artboardLabel.Text = names[current] + $" · {board.Width:0} × {board.Height:0}";
        undoButton.IsEnabled = document.CanUndo; redoButton.IsEnabled = document.CanRedo;
        RefreshContext();
        if (properties) { RefreshLayers(); Properties(); }
    }
    private CanvasDesign Shown => pending ?? document.Design;
    private Border ContentGuide() => new() { BorderThickness = new(1), Background = null, Child = ReadOnlyContent() };
    private UIElement ReadOnlyContent()
    {
        if (!showContentReference || contentSnapshot is not { } snapshot || snapshot.Items.Count == 0)
            return new TextBlock { Text = "收纳区域", FontSize = 11, Foreground = Ink("#7BC9AD"), Margin = new(10), VerticalAlignment = VerticalAlignment.Top };
        var content = new Microsoft.UI.Xaml.Controls.Canvas { Width = snapshot.Width, Height = snapshot.Height,
            Clip = new RectangleGeometry { Rect = new(0, 0, snapshot.Width, snapshot.Height) } };
        string foreground = document.Design.Recipe.ContentForeground ?? snapshot.Foreground;
        foreach (var item in snapshot.Items)
        {
            var tile = new Grid();
            tile.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); tile.RowDefinitions.Add(new() { Height = new(25) });
            FrameworkElement glyph = item.Image is not null ? new Image { Source = item.Image, Stretch = Stretch.Uniform, Margin = new(8) }
                : new TextBlock { Text = item.Monogram, FontSize = 20, Foreground = Ink(foreground), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            tile.Children.Add(glyph);
            var title = new TextBlock { Text = item.Name, Foreground = Ink(foreground), FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(title, 1); tile.Children.Add(title); Place(tile, item.Box); content.Children.Add(tile);
        }
        return new Viewbox { Child = content, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    }
    private void RefreshContentReadback()
    {
        foreach (var pair in views.Where(p => (string?)p.Value.Tag == "content.slot@1"))
            if (pair.Value is Border border) border.Child = ReadOnlyContent();
    }
    private static void Place(FrameworkElement e, CanvasRect b)
    { e.Width = Math.Max(.1, b.Width); e.Height = Math.Max(.1, b.Height); Microsoft.UI.Xaml.Controls.Canvas.SetLeft(e, b.X); Microsoft.UI.Xaml.Controls.Canvas.SetTop(e, b.Y); }
    private static void RotateView(FrameworkElement e, double angle)
    {
        e.RenderTransformOrigin = new(.5, .5);
        if (e.RenderTransform is RotateTransform rotate) rotate.Angle = angle;
        else e.RenderTransform = new RotateTransform { Angle = angle };
    }
    private void Adorn(FrameworkElement e, CanvasRect b)
    { Place(e, b); Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(e, 1000); board.Children.Add(e); adorners.Add(e); }
    private void ClearAdorners()
    { foreach (var e in adorners) board.Children.Remove(e); adorners.Clear(); selectionVisuals.Clear(); }
    private void RefreshLayers()
    {
        layers.Children.Clear();
        var nodes = document.Design.Recipe.Nodes.Where(n => n.Parent == context).Reverse().ToArray();
        layerCount.Text = nodes.Length.ToString();
        foreach (var n in nodes)
        {
            var row = new Grid { CornerRadius = new(5), Background = selected.Contains(n.Id) ? Ink("#334D47") : null, Opacity = n.Hidden ? .5 : 1 };
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new(24) }); row.ColumnDefinitions.Add(new() { Width = new(24) });
            var select = Command(n.Name ?? Label(n.Type), TypeIcon(n.Type), () =>
            {
                if (!Down(global::Windows.System.VirtualKey.Shift)) selected.Clear();
                if (!selected.Add(n.Id)) selected.Remove(n.Id);
                Render();
            });
            select.HorizontalAlignment = HorizontalAlignment.Stretch; select.HorizontalContentAlignment = HorizontalAlignment.Left; select.Padding = new(3);
            if (select.Content is StackPanel content && content.Children.LastOrDefault() is TextBlock caption)
            { caption.MaxWidth = narrow ? 48 : 76; caption.TextTrimming = TextTrimming.CharacterEllipsis; caption.FontSize = 11; }
            row.Children.Add(select);
            var visibility = Command(n.Hidden ? "显示图层" : "隐藏图层", n.Hidden ? "eye-off" : "eye", () => Edit(() => document.Replace(Current(n.Id) with { Hidden = !Current(n.Id).Hidden })), true);
            visibility.Padding = new(3); visibility.IsEnabled = !document.Subtree([n.Id]).Any(p => p.Type == "content.slot@1");
            Grid.SetColumn(visibility, 1); row.Children.Add(visibility);
            var locked = Command(n.Locked ? "解锁图层" : "锁定图层", n.Locked ? "lock" : "unlock", () => Edit(() => document.Replace(Current(n.Id) with { Locked = !Current(n.Id).Locked })), true);
            locked.Padding = new(3); Grid.SetColumn(locked, 2); row.Children.Add(locked); layers.Children.Add(row);
        }
        back.Content = "画布 / " + (document.Design.Recipe.Nodes.First(n => n.Id == context).Name ?? "组合") + "  ↑";
        back.Visibility = context == document.Design.Recipe.Nodes[0].Id ? Visibility.Collapsed : Visibility.Visible;
    }
    private static string TypeIcon(string type) => type switch { "draw.text@1" => "text", "draw.image@1" => "image", "draw.ellipse@1" => "circle", "layout.group@1" => "group", "content.slot@1" => "grid", _ => "square" };
    private static string Label(string type) => type switch { "draw.text@1" => "文字", "draw.panel@1" => "矩形", "draw.ellipse@1" => "椭圆", "draw.image@1" => "图片", "content.slot@1" => "收纳区域", "layout.group@1" => "组合", _ => type };
    private void Select(string id) { selected.Clear(); selected.Add(id); }
    private void RefreshContext()
    {
        contextTools.Children.Clear(); contextBar.Visibility = selected.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        bool editable = selected.Count > 0 && !selected.Any(layerState.Locked.Contains);
        bool decor = editable && !document.Subtree(selected).Any(n => n.Type == "content.slot@1");
        void Add(string label, string icon, Action action, bool enabled)
        { var b = Command(label, icon, action, label is "复制" or "删除"); b.IsEnabled = enabled; contextTools.Children.Add(b); }
        Add("复制", "copy", Duplicate, decor);
        if (selected.Count > 1) Add("组合", "group", GroupSelection, decor);
        Add("存入组合库", "group", SaveCombinationPrompt, decor);
        Add("删除", "trash", DeleteSelection, decor);
    }
    private void Handles(CanvasPlacement p)
    {
        foreach (var (x, y) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) })
        {
            var handle = new Thumb { Width = 9, Height = 9, Background = Ink("#FFFFFF"), BorderBrush = Ink("#438CFF"), BorderThickness = new(1), CornerRadius = new(1) };
            double dx = 0, dy = 0; var parent = geometry!.Nodes.First(n => n.Node.Id == p.Node.Parent).Bounds;
            handle.DragStarted += (_, _) => { handleGesture = true; dx = dy = 0; };
            handle.DragDelta += (_, e) => Try(() =>
            {
                dx += e.HorizontalChange; dy += e.VerticalChange;
                var delta = CanvasEditingGeometry.Rotate(dx, dy, -geometry!.Nodes.First(n => n.Node.Id == p.Node.Parent).Rotation);
                var box = CanvasEditingGeometry.Resize(p.Node.Box, parent, p.Node.Rotation, x, y, delta.X, delta.Y,
                    Down(global::Windows.System.VirtualKey.Shift) || p.Node.Type == "layout.group@1");
                pending = document.WithNodes(document.Design.Recipe.Nodes.Select(n => n.Id == p.Node.Id ? n with { Box = box } : n));
                QueueGestureFrame();
            });
            handle.DragCompleted += (_, e) => CompleteHandle(!e.Canceled);
            Adorn(handle, HandleBounds(p, x, y, "handle")); selectionVisuals.Add((p.Node.Id, handle, x, y, "handle"));
        }
        if (p.Node.Type.StartsWith("draw.", StringComparison.Ordinal) || p.Node.Type == "layout.group@1")
        {
            var rotate = new Thumb { Width = 10, Height = 10, CornerRadius = new(5), Background = Ink("#FFFFFF"), BorderBrush = Ink("#438CFF"), BorderThickness = new(1) };
            double dx = 0, dy = 0; var start = CanvasEditingGeometry.Rotate(0, -p.Bounds.Height / 2 - 25 / viewport.ZoomFactor, p.Rotation);
            rotate.DragStarted += (_, _) => { handleGesture = true; dx = dy = 0; };
            rotate.DragDelta += (_, e) => Try(() =>
            {
                dx += e.HorizontalChange; dy += e.VerticalChange;
                double angle = Math.Atan2(start.Y + dy, start.X + dx) * 180 / Math.PI + 90;
                if (Down(global::Windows.System.VirtualKey.Shift)) angle = Math.Round(angle / 15) * 15;
                angle = (angle - geometry!.Nodes.First(n => n.Node.Id == p.Node.Parent).Rotation + 540) % 360 - 180;
                pending = document.WithNodes(document.Design.Recipe.Nodes.Select(n => n.Id == p.Node.Id ? n with { Rotation = angle } : n)); QueueGestureFrame();
            });
            rotate.DragCompleted += (_, e) => CompleteHandle(!e.Canceled);
            Adorn(rotate, HandleBounds(p, 0, -1, "rotate")); selectionVisuals.Add((p.Node.Id, rotate, 0, -1, "rotate"));
        }
        var size = new Border { Background = Ink("#438CFF"), CornerRadius = new(3), Child = new TextBlock { FontSize = 10, Foreground = Ink("#FFFFFF"), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, IsHitTestVisible = false };
        Adorn(size, HandleBounds(p, 0, 1, "size")); selectionVisuals.Add((p.Node.Id, size, 0, 1, "size")); UpdateSize(size, p);
    }
    private void CompleteHandle(bool commit)
    {
        if (!handleGesture) return; handleGesture = false; CancelGestureFrame(); var next = pending; pending = null;
        if (commit && next is not null) Edit(() => document.Set(next)); else Render();
    }
    private CanvasRect HandleBounds(CanvasPlacement p, int x, int y, string kind)
    {
        double pixel = 1 / viewport.ZoomFactor;
        double extra = (kind == "rotate" ? 25 : kind == "size" ? 18 : 0) * pixel;
        var offset = CanvasEditingGeometry.Rotate(x * p.Bounds.Width / 2, y * (p.Bounds.Height / 2 + extra), p.Rotation);
        double w = (kind == "size" ? 90 : 10) * pixel, h = (kind == "size" ? 20 : 10) * pixel;
        return new(p.Bounds.X + p.Bounds.Width / 2 + offset.X - w / 2, p.Bounds.Y + p.Bounds.Height / 2 + offset.Y - h / 2, w, h);
    }
    private void UpdateSize(Border b, CanvasPlacement p)
    { if (b.Child is TextBlock t) { t.Text = $"{p.Bounds.Width:0.#} × {p.Bounds.Height:0.#}"; t.FontSize = 10 / viewport.ZoomFactor; } }
    private void RenderGesture()
    {
        var layout = CanvasLayout.ForAuthoring(Shown, board.Width, board.Height);
        foreach (var p in layout.Nodes)
        {
            if (views.TryGetValue(p.Node.Id, out var e)) { Place(e, p.Bounds); RotateView(e, p.Rotation); }
            foreach (var a in selectionVisuals.Where(a => a.Id == p.Node.Id))
            {
                if (a.Kind == "outline") { Place(a.View, p.Bounds); RotateView(a.View, p.Rotation); }
                else { Place(a.View, HandleBounds(p, a.X, a.Y, a.Kind)); if (a.Kind == "size") UpdateSize((Border)a.View, p); }
            }
        }
    }
    private void QueueGestureFrame()
    { if (gestureFrameQueued || closed) return; gestureFrameQueued = true; CompositionTarget.Rendering += GestureFrame; }
    private void GestureFrame(object? sender, object e) { CancelGestureFrame(); Try(RenderGesture); }
    private void CancelGestureFrame()
    { if (!gestureFrameQueued) return; CompositionTarget.Rendering -= GestureFrame; gestureFrameQueued = false; }
}
