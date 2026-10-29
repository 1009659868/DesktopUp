namespace DesktopUp.Domain;

/// <summary>Finite RAM-only values and tweens. Event dispatch belongs to CanvasScriptRuntime.</summary>
public sealed class CanvasBehaviorRuntime(CanvasDesign design)
{
    private sealed record Tween(double From, double To, double Duration, string Easing)
    { public double Elapsed { get; set; } }
    private readonly Dictionary<(string Target, string Action), double> values = [];
    private readonly Dictionary<(string Target, string Action), Tween> tweens = [];
    private readonly Dictionary<string, double> opacity = design.Recipe.Nodes.ToDictionary(n => n.Id,
        n => n.Properties.ContainsKey("opacity") ? CanvasDeclaration.Property(design, n, "opacity").Number!.Value : 1d);
    public bool Animating => tweens.Count != 0;
    public double Value(string target, string action)
    {
        if (values.TryGetValue((target, action), out double value)) return value;
        return action == "opacity" ? opacity.GetValueOrDefault(target, 1) : 0;
    }
    public void Assign(string target, string action, double value, int duration, string easing, bool animate)
    {
        var key = (target, action);
        if (animate && duration > 0) tweens[key] = new(Value(target, action), value, duration / 1000d, easing);
        else { tweens.Remove(key); values[key] = value; }
    }
    public void Advance(double deltaSeconds, bool animate)
    {
        foreach (var (key, tween) in tweens.ToArray())
        {
            tween.Elapsed += double.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0, .1) : 0;
            double t = animate ? Math.Clamp(tween.Elapsed / tween.Duration, 0, 1) : 1;
            double eased = tween.Easing == "smooth" ? t * t * (3 - 2 * t) : t;
            values[key] = t == 1 ? tween.To : tween.From + (tween.To - tween.From) * eased;
            if (t == 1) tweens.Remove(key);
        }
    }
    public void Reset() { values.Clear(); tweens.Clear(); }
}
