namespace DesktopUp.Domain;

/// <summary>One whole-poster coordinate space. No implicit header, fit box or offset.
/// Legacy declarations are upgraded only in the editor; saved designs are not rewritten on read.</summary>
public static class CanvasSurfaceGeometry
{
    public static bool IsWholePoster(CanvasDesign? design) => design?.Recipe.Surface is not null;

    public static CanvasDesign ForEditing(CanvasDesign design, double width, double height)
    {
        if (IsWholePoster(design)) return design;
        var art = design.Recipe.Artboard ?? new(Math.Clamp(width, 240, 2048), Math.Clamp(height, 240, 2048));
        double contentTop = CanvasLayout.ForAuthoring(design, art.Width, art.Height).Content.Y;
        double titleHeight = Math.Clamp(contentTop - 8, 28, 40);
        var title = new CanvasNode(CanvasAuthoring.NewId(), design.Recipe.Nodes[0].Id, "host.title@1",
            new(.03, 4 / art.Height, .94, titleHeight / art.Height),
            new Dictionary<string, CanvasValue> { ["text"] = new(Text: design.Recipe.Name), ["color"] = new(Text: design.Recipe.ContentForeground ?? "#F2F2F2"),
                ["actions"] = new(Text: design.Recipe.ContentForeground ?? "#FFFFFF"), ["fontSize"] = new(Number: 14) }, Name: "标题与操作区");
        return CanvasDeclaration.Freeze(design with { Recipe = design.Recipe with { ProtocolVersion = 3,
            Artboard = art, Surface = new(), Nodes = design.Recipe.Nodes.Append(title).ToArray() } });
    }

    public static bool Contains(CanvasSurface surface, double width, double height, double x, double y, double scale = 1)
    {
        if (x < 0 || y < 0 || x > width || y > height || width <= 0 || height <= 0) return false;
        if (surface.Shape == "ellipse")
        { double dx = (x - width / 2) / (width / 2), dy = (y - height / 2) / (height / 2); return dx * dx + dy * dy <= 1.0000001; }
        double r = surface.Shape == "rounded" ? Math.Min(surface.Radius * scale, Math.Min(width, height) / 2) : 0;
        double a = Math.Max(Math.Max(r - x, x - (width - r)), 0), b = Math.Max(Math.Max(r - y, y - (height - r)), 0);
        return a * a + b * b <= r * r + .0000001;
    }

    public static void ValidateHosting(CanvasDesign design, CanvasLayoutResult layout)
    {
        if (design.Recipe.Surface is not { } surface) return;
        var titleNode = layout.Nodes.Single(p => p.Node.Type == "host.title@1");
        var title = titleNode.Bounds;
        if (title.Width < 120 || title.Height < Math.Max(28, CanvasDeclaration.Property(design, titleNode.Node, "fontSize").Number!.Value * layout.Scale * 1.5 + 4))
            throw new ArgumentException("标题操作区至少需要 120 × 28 DIP，保留拖动、收起与菜单空间。");
        foreach (var box in new[] { title, layout.Content })
            if (!Contains(surface, layout.Width, layout.Height, box.X, box.Y, layout.Scale) ||
                !Contains(surface, layout.Width, layout.Height, box.Right, box.Y, layout.Scale) ||
                !Contains(surface, layout.Width, layout.Height, box.X, box.Bottom, layout.Scale) ||
                !Contains(surface, layout.Width, layout.Height, box.Right, box.Bottom, layout.Scale))
                throw new ArgumentException("标题操作区或收纳区域超出海报外形；请在画板中移动或缩小该区域。");
        if (title.X < layout.Content.Right && title.Right > layout.Content.X &&
            title.Y < layout.Content.Bottom && title.Bottom > layout.Content.Y)
            throw new ArgumentException("标题操作区与收纳区域不能重叠，以免遮挡文件操作。");
    }

    public static ContentFrameMinimum Minimum(CanvasLayoutResult layout, ContentLayoutSnapshot snapshot)
    {
        var content = ContentFrameMinimumPolicy.FromSnapshot(snapshot, 2, 2);
        var title = layout.Nodes.Single(p => p.Node.Type == "host.title@1").Bounds;
        return new(Math.Max(content.WidthDip * layout.Width / layout.Content.Width, 120 * layout.Width / title.Width),
            Math.Max(content.HeightDip * layout.Height / layout.Content.Height, 28 * layout.Height / title.Height));
    }

    public static PosterTitleChromeLayout Title(CanvasDesign? design, double width, double height, bool collapsed, PosterTitleChromeKind legacy)
    {
        if (!IsWholePoster(design) || collapsed) return PosterTitleChromePolicy.Resolve(width, collapsed, legacy);
        var box = CanvasLayout.Resolve(design!, width, height, false).Nodes.Single(p => p.Node.Type == "host.title@1").Bounds;
        return TitleBox(box, width, height);
    }

    public static PosterTitleChromeLayout TitleBox(CanvasRect box, double width, double height)
    {
        const double button = 24;
        double y = box.Y + (box.Height - button) / 2;
        return new(PosterTitleChromeKind.Basic, width, height,
            new(box.X + 6, box.Y + 2, Math.Max(1, box.Width - 70), Math.Max(1, box.Height - 4)),
            new(box.Right - 30 - button - PosterTitleBarLayoutPolicy.ActionGapDip, y, button, button), new(box.Right - 30, y, button, button),
            new[] { new TitleChromeRect(box.X, box.Y, box.Width, box.Height) });
    }
}
