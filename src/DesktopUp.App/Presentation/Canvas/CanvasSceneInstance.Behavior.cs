using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class CanvasSceneInstance
{
    private CanvasScriptRuntime? scripts;
    private CanvasDesign? scriptDesign;
    private Func<bool> ReadCollapsed = () => false;
    private Func<bool, Task<bool>> RequestPosture = _ => Task.FromResult(false);
    private bool behaviorCollapsed;
    private bool behaviorVisible;
    private double behaviorClock;
    private CanvasPlacement[] eventGeometry = [];
    private PosterTitleChromeLayout? eventTitle;
    private string? toggleEventSource;
    private (CanvasPlacement Placed, Microsoft.UI.Xaml.FrameworkElement View, CompositeTransform Transform)[] behaviorVisuals = [];
    internal bool ObservesPointer => !ReadCollapsed() && scripts?.ObservesPointer == true;
    internal bool HasActiveScripts => scripts?.Active == true;
    internal string? ScriptError => scripts?.Error;
    internal void ConfigureHost(Func<bool> collapsed, Func<bool, Task<bool>> request)
    { ReadCollapsed = collapsed; RequestPosture = request; }
    private void RebuildBehaviorGeometry()
    {
        var title = layout!.Nodes.SingleOrDefault(p => p.Node.Type == "host.title@1");
        eventTitle = title is null ? null : CanvasSurfaceGeometry.TitleBox(title.Bounds, layout.Width, layout.Height);
        toggleEventSource = title?.Node.Id + "/toggle";
        eventGeometry = layout!.Nodes.Where(p => CanvasBehaviorPrefabs.CanBind(p.Node) && layerState?.Hidden.Contains(p.Node.Id) != true).ToArray();
        var targets = scripts!.Targets;
        behaviorVisuals = eventGeometry.Where(p => targets.Contains(p.Node.Id) && visuals.ContainsKey(p.Node.Id)).Select(p =>
        {
            var view = visuals[p.Node.Id]; var transform = new CompositeTransform { Rotation = p.Rotation };
            view.RenderTransform = transform; return (p, view, transform);
        }).ToArray();
    }
    private HashSet<string> Sources(ScenePointer? pointer)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal);
        if (pointer is not { } point || layout is null) return sources;
        sources.Add("poster");
        if (eventTitle is { } title && title.HitTest(point.X, point.Y) is var target && target != PosterTitleChromeTarget.Desktop)
        {
            if (target == PosterTitleChromeTarget.Collapse) sources.Add(toggleEventSource!);
            return sources; // Title controls take priority over decorative layers underneath.
        }
        // Reference hit geometry deliberately stays static: animation cannot chase the pointer
        // or change native file/chrome input ownership. Topmost decorative leaf wins.
        var hit = eventGeometry.LastOrDefault(p => CanvasEditingGeometry.Hit(p.Bounds, p.Rotation, point.X, point.Y));
        if (hit is not null)
        {
            sources.Add(hit.Node.Id);
            var parent = hit.Node.Parent;
            while (parent is not null)
            { var node = design!.Recipe.Nodes.First(n => n.Id == parent); if (CanvasBehaviorPrefabs.CanBind(node)) sources.Add(node.Id); parent = node.Parent; }
        }
        return sources;
    }
    private void UpdateBehavior(SceneFrameContext frame)
    {
        if (scripts is null) return;
        bool collapsed = ReadCollapsed();
        if (!behaviorVisible || behaviorCollapsed != collapsed)
        { scripts.Fire(collapsed ? "poster.collapsed" : "poster.expanded", "poster", frame.CanAnimate); behaviorClock = frame.Seconds; }
        behaviorCollapsed = collapsed;
        behaviorVisible = true;
        if (ObservesPointer) scripts.Observe(Sources(frame.Pointer), frame.CanAnimate);
        scripts.Advance(frame.Seconds - behaviorClock, frame.CanAnimate); behaviorClock = frame.Seconds;
        PaintBehavior();
    }
    internal void Click(ScenePointer point, bool animate)
    {
        if (!behaviorVisible || scripts is null) return;
        foreach (var source in Sources(point)) scripts.Fire("pointer.click", source, animate);
        PaintBehavior();
    }
    internal bool InvokeToggle(bool animate)
    {
        if (scripts is null || design is null || !CanvasSurfaceGeometry.IsWholePoster(design)) return false;
        return scripts.Fire("pointer.click", CanvasBehaviorPrefabs.ToggleSource(design), animate);
    }
    internal void InvokeSource(string source, bool animate)
    {
        if (scripts is null) return;
        scripts.Fire("pointer.click", "poster", animate);
        while (source != "poster")
        {
            scripts.Fire("pointer.click", source, animate);
            var parent = design?.Recipe.Nodes.FirstOrDefault(n => n.Id == source)?.Parent;
            if (parent is null) break;
            source = parent;
        }
        PaintBehavior();
    }
    private void PaintBehavior()
    {
        if (scripts is null || layout is null) return;
        var behavior = scripts.Values;
        foreach (var (placed, view, transform) in behaviorVisuals)
        {
            string target = placed.Node.Id;
            double rotation = placed.Rotation + behavior.Value(target, "rotation"),
                x = behavior.Value(target, "offsetX") * layout.Scale, y = behavior.Value(target, "offsetY") * layout.Scale,
                opacity = behavior.Value(target, "opacity");
            if (transform.Rotation != rotation) transform.Rotation = rotation;
            if (transform.TranslateX != x) transform.TranslateX = x;
            if (transform.TranslateY != y) transform.TranslateY = y;
            if (view.Opacity != opacity) view.Opacity = opacity;
        }
    }
}
