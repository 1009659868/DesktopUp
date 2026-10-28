namespace DesktopUp.Domain;

/// <summary>One parent-first pass per scene revision; never traverse ancestors per pointer sample.</summary>
public sealed record CanvasLayerState(IReadOnlySet<string> Hidden, IReadOnlySet<string> Locked)
{
    public static CanvasLayerState Resolve(CanvasDesign design)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        var locked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in design.Recipe.Nodes)
        {
            if (n.Hidden || n.Parent is not null && hidden.Contains(n.Parent)) hidden.Add(n.Id);
            if (n.Locked || n.Parent is not null && locked.Contains(n.Parent)) locked.Add(n.Id);
        }
        return new(hidden, locked);
    }
}
