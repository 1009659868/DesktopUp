namespace DesktopUp.Domain;

public sealed record CanvasRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
public sealed record CanvasPlacement(CanvasNode Node, CanvasRect Bounds, double Rotation = 0);
public sealed record CanvasLayoutResult(CanvasRect Content, IReadOnlyList<CanvasPlacement> Nodes, double Width, double Height, double Scale = 1);

/// <summary>Pure, bounded geometry. Text uses conservative line allocation; the platform may
/// reject a measured overflow, never publish a different content/input rectangle.</summary>
public static class CanvasLayout
{
    public const double Header = PosterTitleBarLayoutPolicy.HeaderHeightDip;
    public static CanvasLayoutResult Resolve(CanvasDesign design, double width, double height, bool requireReadableContent = true)
        => ResolveCore(design, width, height, Header, requireReadableContent);
    public static CanvasLayoutResult ForAuthoring(CanvasDesign design, double width, double height)
        => ResolveCore(design, width, height, 0, false);
    private static CanvasLayoutResult ResolveCore(CanvasDesign design, double width, double height, double header, bool requireReadableContent)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height <= header)
            throw new ArgumentException("画板尺寸不足。");
        var nodes = design.Recipe.Nodes;
        var children = nodes.Where(n => n.Parent is not null).GroupBy(n => n.Parent!).ToDictionary(g => g.Key, g => g.ToArray());
        var placed = new Dictionary<string, CanvasRect>(StringComparer.Ordinal);
        var output = new List<CanvasPlacement>(nodes.Count);
        double Number(CanvasNode n, string key) => CanvasDeclaration.Property(design, n, key).Number!.Value;
        double scale = design.Recipe.Artboard is { } art ? Math.Min(width / art.Width, (height - header) / art.Height) : 1;
        void Place(CanvasNode node, CanvasRect box, double rotation)
        {
            if (box.Width <= 0 || box.Height <= 0) throw new ArgumentException("画板内容空间不足，请先扩大外框或减少组件。");
            placed.Add(node.Id, box); output.Add(new(node, box, rotation));
            if (!children.TryGetValue(node.Id, out var descendants)) return;
            if (node.Type == "layout.stack@1")
            {
                double padding = Number(node, "padding") * scale, gap = Number(node, "gap") * scale, y = box.Y + padding, w = box.Width - padding * 2;
                foreach (var child in descendants)
                {
                    double h = child.Type == "content.slot@1" ? box.Bottom - padding - y :
                        TextHeight(CanvasDeclaration.Property(design, child, "text").Text!, Number(child, "fontSize") * scale, w);
                    Place(child, new(box.X + padding, y, w, h), child.Rotation); y += h + gap;
                }
            }
            else foreach (var child in descendants)
            {
                double w = child.Box.Width * box.Width, h = child.Box.Height * box.Height;
                var center = CanvasEditingGeometry.Rotate((child.Box.X + child.Box.Width / 2 - .5) * box.Width,
                    (child.Box.Y + child.Box.Height / 2 - .5) * box.Height, rotation);
                Place(child, new(box.X + box.Width / 2 + center.X - w / 2, box.Y + box.Height / 2 + center.Y - h / 2, w, h),
                    (rotation + child.Rotation + 540) % 360 - 180);
            }
        }
        double rootWidth = design.Recipe.Artboard?.Width * scale ?? width, rootHeight = design.Recipe.Artboard?.Height * scale ?? height - header;
        Place(nodes[0], new((width - rootWidth) / 2, header + (height - header - rootHeight) / 2, rootWidth, rootHeight), 0);
        var content = placed[nodes.Single(n => n.Type == "content.slot@1").Id];
        if (requireReadableContent && (content.Width < ContentLayoutPolicy.MinimumReadableCardDip || content.Height < ContentLayoutPolicy.MinimumReadableCardDip))
            throw new ArgumentException("画板未保留可读内容空间，请先扩大外框。");
        return new(content, output.AsReadOnly(), width, height, scale);
    }
    public static double TextHeight(string text, double size, double width)
    {
        if (width <= 0) throw new ArgumentException("文字没有可用宽度。");
        // At most one font-size per UTF-16 unit (including CJK); an intentionally generous
        // line box keeps the same deterministic layout at every persistence boundary.
        int capacity = Math.Max(1, (int)Math.Floor(width / (size * 1.1)));
        int lines = text.Split('\n').Sum(s => Math.Max(1, (s.Length + capacity - 1) / capacity));
        return lines * size * 1.5 + 4;
    }
}
