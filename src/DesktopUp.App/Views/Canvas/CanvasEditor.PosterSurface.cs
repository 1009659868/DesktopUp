using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private Microsoft.UI.Xaml.Shapes.Path? surfaceMask;
    private (CanvasSurface? Surface, double Width, double Height) maskKey;

    private void SurfaceProperties()
    {
        var surface = document.Design.Recipe.Surface ?? new();
        var choice = new ComboBox { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (id, label) in new[] { ("rectangle", "矩形"), ("rounded", "圆角"), ("ellipse", "椭圆") })
            choice.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        choice.SelectedIndex = surface.Shape switch { "rounded" => 1, "ellipse" => 2, _ => 0 };
        choice.SelectionChanged += (_, _) =>
        {
            if (rebuildingProperties || !choice.IsLoaded || choice.SelectedItem is not ComboBoxItem item) return;
            Edit(() => document.Set(document.Design with { Recipe = document.Design.Recipe with {
                Surface = surface with { Shape = (string)item.Tag } } }));
        };
        AddField("海报外形", choice, true);
        AddField("安全操作空间", Command("操作区置入外形", "", () => Edit(() => document.Set(document.WithNodes(document.Design.Recipe.Nodes.Select(n => n.Type switch
        {
            "host.title@1" => n with { Box = new(.25, .12, .5, .1) },
            "content.slot@1" => n with { Parent = document.Design.Recipe.Nodes[0].Id, Box = new(.2, .3, .6, .48) },
            _ => n
        }))))), true);
        if (surface.Shape == "rounded") Number("外形圆角", surface.Radius, 0, 512, null,
            radius => document.Set(document.Design with { Recipe = document.Design.Recipe with { Surface = surface with { Radius = radius } } }));
    }

    private void PaintSurfaceMask(CanvasDesign design)
    {
        var key = (design.Recipe.Surface, board.Width, board.Height);
        if (surfaceMask is not null && maskKey == key) return;
        maskKey = key;
        surfaceMask ??= new Microsoft.UI.Xaml.Shapes.Path { Fill = Ink("#191D20"), IsHitTestVisible = false };
        if (!board.Children.Contains(surfaceMask)) board.Children.Add(surfaceMask);
        Canvas.SetZIndex(surfaceMask, 900);
        // Editor-only negative-space mask. Native HWNDs use the same declared shape, not alpha hit testing.
        var shape = design.Recipe.Surface ?? new();
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry { Rect = new(0, 0, board.Width, board.Height) });
        if (shape.Shape == "ellipse") group.Children.Add(new EllipseGeometry { Center = new(board.Width / 2, board.Height / 2), RadiusX = board.Width / 2, RadiusY = board.Height / 2 });
        else group.Children.Add(RoundedOutline(board.Width, board.Height, shape.Shape == "rounded" ? shape.Radius : 0));
        surfaceMask.Data = group;
    }

    private static Geometry RoundedOutline(double width, double height, double radius)
    {
        double r = Math.Min(radius, Math.Min(width, height) / 2);
        if (r <= 0) return new RectangleGeometry { Rect = new(0, 0, width, height) };
        var figure = new PathFigure { StartPoint = new(r, 0), IsClosed = true, IsFilled = true };
        void Line(double x, double y) => figure.Segments.Add(new LineSegment { Point = new(x, y) });
        void Arc(double x, double y) => figure.Segments.Add(new ArcSegment { Point = new(x, y), Size = new(r, r), SweepDirection = SweepDirection.Clockwise });
        Line(width - r, 0); Arc(width, r); Line(width, height - r); Arc(width - r, height);
        Line(r, height); Arc(0, height - r); Line(0, r); Arc(r, 0);
        var geometry = new PathGeometry(); geometry.Figures.Add(figure); return geometry;
    }

    private static Canvas TitleGuide() => new();
    private void PaintTitleGuide(Canvas view, CanvasDesign design, CanvasNode node, CanvasRect box)
    {
        var title = CanvasSurfaceGeometry.TitleBox(box, board.Width, board.Height);
        int index = 0;
        void Add(string text, TitleChromeRect rect, string color, double size, bool center)
        {
            if (view.Children.Count <= index) view.Children.Add(new Border { Child = new TextBlock { Foreground = Ink(color), TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1, Padding = center ? new(0) : new(3, 1, 3, 0), TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center } });
            var slot = (Border)view.Children[index++]; var label = (TextBlock)slot.Child;
            label.Text = text; label.FontSize = size;
            var ink = Ink(color).Color;
            if (label.Foreground is SolidColorBrush brush) brush.Color = ink; else label.Foreground = new SolidColorBrush(ink);
            Place(slot, new(rect.X - box.X, rect.Y - box.Y, rect.Width, rect.Height));
        }
        Add(CanvasDeclaration.Property(design, node, "text").Text!, title.Name,
            CanvasDeclaration.Property(design, node, "color").Text!, CanvasDeclaration.Property(design, node, "fontSize").Number!.Value, false);
        string actions = CanvasDeclaration.Property(design, node, "actions").Text!;
        Add("⌄", title.Collapse, actions, 12, true); Add("⋯", title.More, actions, 12, true);
    }
}
