using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>One local immutable layout, bounded to its design and size. Position and time are not keys.</summary>
public sealed class CanvasLayoutCache
{
    private CanvasDesign? design;
    private CanvasLayoutResult? layout;
    public CanvasLayoutResult Resolve(CanvasDesign current, double width, double height)
    {
        if (!ReferenceEquals(design, current) || layout?.Width != width || layout.Height != height)
        { layout = CanvasLayout.Resolve(current, width, height); design = current; }
        return layout;
    }
}
