namespace DesktopUp.Domain;

/// <summary>Stable, separately selectable host controls. Their geometry is derived from
/// the title layout, not another editable rectangle that can drift from native input.</summary>
public sealed record CanvasTitleControl(string Id, string Parent, string Role, CanvasRect BoundsDip, bool CanBind);

public static class CanvasTitleControls
{
    public static IReadOnlyList<CanvasTitleControl> Resolve(CanvasDesign design, double width, double height)
    {
        var placed = CanvasLayout.ForAuthoring(design, width, height).Nodes.SingleOrDefault(p => p.Node.Type == "host.title@1");
        return placed is null ? [] : Resolve(placed.Node.Id, placed.Bounds, width, height);
    }

    public static IReadOnlyList<CanvasTitleControl> Resolve(string parent, CanvasRect box, double width, double height)
    {
        var title = CanvasSurfaceGeometry.TitleBox(box, width, height);
        CanvasTitleControl Part(string role, TitleChromeRect b, bool canBind = false) =>
            new(parent + "/" + role, parent, role, new(b.X, b.Y, b.Width, b.Height), canBind);
        return [Part("name", title.Name), Part("toggle", title.Collapse, true), Part("more", title.More)];
    }
}
