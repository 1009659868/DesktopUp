namespace DesktopUp.Domain;

/// <summary>One bounded scheduler shared by studio and native adapters. Host effects are explicit
/// requests, never state writes. Stopping a script prevents future steps, not an accepted host command.</summary>
public sealed class CanvasScriptRuntime
{
    private sealed class Lane((CanvasBehaviorBinding Binding, CanvasProgram Code)[] steps)
    {
        public readonly (CanvasBehaviorBinding Binding, CanvasProgram Code)[] Steps = steps;
        public int Binding, Instruction, Executed;
        public double Delay, Elapsed;
        public Task<bool>? Host;
    }
    private readonly CanvasBehavior behavior;
    private readonly Dictionary<string, CanvasProgram> programs;
    private readonly Dictionary<string, Lane> lanes = new(StringComparer.Ordinal);
    private readonly HashSet<string> hovering = new(StringComparer.Ordinal);
    private readonly Func<bool> collapsed;
    private readonly Func<bool, Task<bool>> requestPosture;
    public CanvasBehaviorRuntime Values { get; }
    public string? Error { get; private set; }
    public bool Active => lanes.Count != 0 || Values.Animating;
    public bool ObservesPointer => behavior.Bindings!.Any(b => b.Enabled && b.Event is "pointer.enter" or "pointer.leave");
    public IReadOnlySet<string> Targets { get; }
    public CanvasScriptRuntime(CanvasDesign design, Func<bool> readCollapsed, Func<bool, Task<bool>> request)
    {
        collapsed = readCollapsed; requestPosture = request;
        behavior = CanvasBehaviorPrefabs.ForEditing(design);
        CanvasBehaviorPrefabs.Validate(behavior, design.Recipe.Nodes);
        programs = behavior.Prefabs!.ToDictionary(p => p.Id, p => CanvasScript.Compile(p.Script));
        Values = new(design);
        Targets = behavior.Bindings!.Where(b => programs[b.Prefab].ChangesDecoration).Select(b => b.Target).ToHashSet();
    }
    public bool Fire(string trigger, string source, bool animate)
    {
        var steps = behavior.Bindings!.Where(b => b.Enabled && b.Source == source && b.Event == trigger).Select(b => (b, programs[b.Prefab])).ToArray();
        if (steps.Length == 0) return false;
        lanes.Remove(source);
        if (steps.Sum(s => s.Item2.Instructions.Count) > 256 || lanes.Count >= 8)
        { Error = "本次触发超过256步或8条并发通道，未执行。"; return true; }
        Error = null; var lane = new Lane(steps); lanes[source] = lane;
        Pump(source, lane, 0, animate); return true;
    }
    public void Observe(IReadOnlySet<string> sources, bool animate)
    {
        foreach (var source in hovering.Where(s => !sources.Contains(s)).ToArray()) Fire("pointer.leave", source, animate);
        foreach (var source in sources) if (!hovering.Contains(source)) Fire("pointer.enter", source, animate);
        hovering.Clear(); hovering.UnionWith(sources);
    }
    public void Advance(double delta, bool animate)
    {
        double seconds = double.IsFinite(delta) ? Math.Clamp(delta, 0, .1) : 0;
        Values.Advance(seconds, animate);
        foreach (var (source, lane) in lanes.ToArray()) Pump(source, lane, seconds, animate);
    }
    private void Pump(string source, Lane lane, double delta, bool animate)
    {
        if (lanes.GetValueOrDefault(source) != lane) return;
        lane.Elapsed += delta;
        if (lane.Elapsed > 30) { Fail(source, "行为超过30秒，已停止后续动作。"); return; }
        if (lane.Host is { } host)
        {
            if (!host.IsCompleted) return;
            if (!host.IsCompletedSuccessfully || !host.Result) { Fail(source, "宿主未完成收展，后续动作已停止；原安全保护保持。"); return; }
            lane.Host = null;
        }
        lane.Delay = Math.Max(0, lane.Delay - delta);
        if (lane.Delay > 0) return;
        while (lane.Binding < lane.Steps.Length)
        {
            var (binding, code) = lane.Steps[lane.Binding];
            if (lane.Instruction == 0 && (binding.Condition == "collapsed" && !collapsed() || binding.Condition == "expanded" && collapsed()))
            { lane.Binding++; continue; }
            if (lane.Instruction >= code.Instructions.Count) { lane.Binding++; lane.Instruction = 0; continue; }
            if (++lane.Executed > 256) { Fail(source, "行为超过执行步数预算。"); return; }
            var step = code.Instructions[lane.Instruction++];
            switch (step.Op)
            {
                case "if": if (collapsed() != (step.Property == "collapsed")) lane.Instruction = step.Jump; break;
                case "jump": lane.Instruction = step.Jump; break;
                case "wait": lane.Delay = step.Duration / 1000d; break;
                case "set": case "animate":
                    Values.Assign(binding.Target, step.Property!, step.Value, step.Duration, step.Easing, animate);
                    lane.Delay = animate ? step.Duration / 1000d : 0; break;
                default:
                    if (lanes.Values.Any(l => l != lane && l.Host is not null)) { Fail(source, "另一个行为正在请求收展，请稍后再试。"); return; }
                    bool next = step.Op == "poster.toggle" ? !collapsed() : step.Op == "poster.collapse";
                    try { lane.Host = requestPosture(next); }
                    catch (Exception) { Fail(source, "宿主拒绝收展请求。"); return; }
                    if (lane.Host.IsCompleted)
                    {
                        if (!lane.Host.IsCompletedSuccessfully || !lane.Host.Result) { Fail(source, "宿主拒绝收展，后续动作已停止。"); return; }
                        lane.Host = null;
                    }
                    else return;
                    break;
            }
            if (lane.Delay > 0) return;
        }
        lanes.Remove(source);
    }
    private void Fail(string source, string message) { lanes.Remove(source); Error = message; }
    public void Stop() { lanes.Clear(); hovering.Clear(); Values.Reset(); Error = null; }
}
