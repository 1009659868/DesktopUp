using System.Diagnostics;
using DesktopUp.Application;
using Microsoft.UI.Dispatching;
namespace DesktopUp;

public partial class App
{
    private void RunRainResourceProbe()
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(500);
        var lease = _ledger!.Acquire("fixture.rain-resources", "bounded-pool-shared-clock");
        var samples = new List<object>(); var summaries = new List<object>(); string[] ids = [];
        int target = 0, stage = 0, sampled = 0; bool stopped = false;
        long deadline = _autoClock.ElapsedMilliseconds + 70000, origin = 0, allocated = 0, firstFrames = 0;
        double cpu = 0; string phase = "initialize";
        void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        void Stop() { if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "rain.resources_observed", new { passed, error,
                count = ids.Length, samples, summaries, nativeInput = false, fpsCertified = false }); ExitApplication();
        }
        RainSceneObservation[] Scenes() => ids.Select(id => _spatialRuntime!.RainWindowForProbe(id).ReadRainForProbe()
            ?? throw new InvalidOperationException("Missing resource-test scene.")).ToArray();
        void Begin(string name)
        {
            phase = name; sampled = 0; origin = _autoClock.ElapsedMilliseconds;
            using var p = Process.GetCurrentProcess(); cpu = p.TotalProcessorTime.TotalMilliseconds;
            allocated = GC.GetTotalAllocatedBytes(false); firstFrames = Scenes().Sum(s => s.Frames);
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped) return;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Rain resource probe timed out.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                if (stage == 0)
                {
                    var state = _stateCoordinator!.Refresh(); ids = state.Posters.Select(p => p.Poster.PosterId).ToArray();
                    Require(ids.Length is 1 or 3 or 6, "Use a new 1/3/6 measurement fixture.");
                    _controlCenter!.Show(); stage = 1; return;
                }
                if (stage == 1)
                {
                    var center = _controlCenter!.SettingsWindowForContractProbe;
                    if (center?.Content.XamlRoot is null) return;
                    center.SelectSettingsTargetForProbe(ids[target]); center.TemplateForProbe("rain"); stage = 2; return;
                }
                if (stage == 2)
                {
                    _controlCenter!.SettingsWindowForContractProbe!.ChangeRecipeNumberForProbe("amount", 64, true);
                    if (++target < ids.Length) { stage = 1; return; }
                    _controlCenter.CloseForContractProbe(); stage = 3; return;
                }
                if (stage == 3) { Begin("rain-max-active"); stage = 4; return; }
                if (stage == 5) { Begin("global-paused-baseline"); stage = 6; return; }
                var scenes = Scenes(); int timers = _ledger!.ActiveResources.Count(r => r == "timer.scene-motion");
                Require(scenes.All(s => s.Pool == 64 && s.Nodes == 67 && s.VisibleDrops == 64 && s.CloudVisible && s.DecorativeOnly),
                    "Particle/decoration bound failed.");
                Require(timers <= 1 && (stage != 6 || timers == 0 && scenes.All(s => !s.Running)), "Shared clock/pause bound failed.");
                using var process = Process.GetCurrentProcess();
                samples.Add(new { phase, sample = ++sampled, timers, nodes = scenes.Sum(s => s.Nodes),
                    drops = scenes.Sum(s => s.VisibleDrops), process.WorkingSet64, process.HandleCount,
                    allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocated, frames = scenes.Sum(s => s.Frames) - firstFrames });
                if (sampled < 12) return;
                long elapsed = _autoClock.ElapsedMilliseconds - origin;
                double cpuPercent = (process.TotalProcessorTime.TotalMilliseconds - cpu) / elapsed * 100 / Environment.ProcessorCount;
                Require(stage == 6 || _spatialRuntime.SceneReducedMotion || scenes.Sum(s => s.Frames) > firstFrames, "No shared animation frames.");
                summaries.Add(new { phase, elapsedMs = elapsed, cpuPercent, allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocated,
                    frames = scenes.Sum(s => s.Frames) - firstFrames, reducedMotion = _spatialRuntime.SceneReducedMotion });
                if (stage == 4) { ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); stage = 5; }
                else Finish(true, null);
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
