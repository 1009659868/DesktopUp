using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private Action? _continuityProbeStop;
    internal void RunContinuitySmoke(DispatcherQueue dispatcher, bool arrangeOnly)
    {
        var timer = dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(300); timer.IsRepeating = true;
        DateTime deadline = DateTime.UtcNow.AddSeconds(24);
        bool busy = false, finished = false;
        string[]? ids = null; SpatialSnapshot? baseline = null;
        int index = 0;
        (int Index, bool Collapsed)[] steps = [(0, false), (1, false), (0, true), (1, true),
            (0, false), (1, false), (1, true), (0, true)];
        _measurePostureFrames = !arrangeOnly;
        void Finish(bool passed, string? error)
        {
            if (finished) return;
            finished = true; timer.Stop(); timer.Tick -= Tick; _continuityProbeStop = null; _measurePostureFrames = false;
            log.Write(passed ? "info" : "error", arrangeOnly ? "continuity.fixture_ready" : "continuity.smoke_completed",
                new { passed, error, commands = index, baseline = baseline?.Posters.Select(p => new { p.PosterId, p.Bounds }) });
        }
        _continuityProbeStop = () => Finish(false, "surface-removed");
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
                if (busy || _suspended || _windows.Count != 3 || !state.TryGetCurrent(out var current) || current is null) return;
                if (ids is null)
                {
                    var targets = current.Posters.OrderByDescending(p => p.Poster.Title, StringComparer.Ordinal).ToArray();
                    if (targets.Length != 3 || targets.Any(p => !p.Poster.IsCollapsed || p.Poster.IsLocked))
                    { Finish(false, "requires-three-unlocked-compact-fixtures"); return; }
                    ids = targets.Select(p => p.Poster.PosterId).ToArray(); var owner = _windows[ids[0]];
                    if (!TrySnapshot(owner, out var scene, out _)) { ids = null; return; }
                    var area = scene!.Environment.WorkArea; int height = scene.Posters[0].Bounds.Height;
                    int width = scene.Posters[0].Bounds.Width;
                    if (scene.Posters.Take(2).Sum(p => p.ExpandedHeight) + height + 24 > area.Height)
                    { Finish(false, "work-area-too-small-for-full-native-fixture"); return; }
                    var bounds = ids.Select((id, i) => new SpatialRect(checked((int)(area.Right - width - 24)), area.Y + 24 + i * height, width, height)).ToArray();
                    var prepared = ids.Select((id, i) => _windows[id].PrepareSpatialFrame(Pixels(bounds[i]))).ToArray();
                    if (prepared.Any(p => p is null) || !owner.CommitSpatialFrames(prepared.Select(p => p!).ToArray()))
                    { Finish(false, "arrange-native-batch-failed"); return; }
                    for (int i = 0; i < ids.Length; i++) _effective.Set(targets[i], bounds[i], scene.Environment);
                    EffectiveLayoutChanged?.Invoke();
                    if (!TrySnapshot(owner, out baseline, out _) || PosterSpatialSolver.ValidateLayout(baseline!).Count != 0)
                    { Finish(false, "arrange-readback-failed"); return; }
                    if (arrangeOnly) Finish(true, null);
                    return;
                }
                if (index == steps.Length) { Finish(true, null); return; }
                var step = steps[index]; var target = current.PostersById[ids[step.Index]]; var window = _windows[target.Poster.PosterId];
                busy = true;
                SetCollapsed(new("continuity-probe-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                    PosterStateCommandKind.SetCollapsed, current.Revision, target.Poster.PosterId,
                    target.Poster with { IsCollapsed = step.Collapsed })
                    { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) }, result =>
                {
                    try
                    {
                        busy = false; if (finished) return;
                        if (!result.Succeeded || !TrySnapshot(window, out var actual, out var projection) ||
                            PosterSpatialSolver.ValidateLayout(actual!).Count != 0)
                        { Finish(false, result.Error ?? "physical-snapshot-failed"); return; }
                        var proof = projection!.SpatialContinuities.SingleOrDefault(s => s.Environment == actual!.Environment);
                        bool consistent = proof is not null && actual!.Posters.All(p => proof.Nodes.Any(n => n.Expected == p));
                        bool closure = index is not (3 or 7) || actual!.Posters.All(p => p.Bounds == baseline!.Posters.Single(b => b.PosterId == p.PosterId).Bounds);
                        bool nested = index != 2 || (!actual!.Posters.Single(p => p.PosterId == ids[1]).IsCollapsed &&
                            actual.Posters.Single(p => p.PosterId == ids[1]).Bounds.Y == baseline!.Posters.Single(p => p.PosterId == ids[1]).Bounds.Y &&
                            actual.Posters.Single(p => p.PosterId == ids[2]).Bounds.Y == actual.Posters.Single(p => p.PosterId == ids[1]).Bounds.Bottom);
                        log.Write("info", "continuity.probe_step", new { index, result.RequestId, result.CurrentRevision,
                            consistent, closure, nested, bounds = actual!.Posters.Select(p => new { p.PosterId, p.Bounds, p.IsCollapsed }) });
                        if (!consistent || !closure || !nested) { Finish(false, "continuity-geometry-or-revision-mismatch"); return; }
                        index++;
                    }
                    catch (Exception e) { Finish(false, e.Message); }
                });
            }
            catch (Exception e) { Finish(false, e.Message); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
