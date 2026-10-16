using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private Action? _stopAutoSmoke;

    // Explicit isolated fixture only. No synthetic pointer, direct auto request or database write.
    // Policies use normal commands; native observation and the production scheduler do the work.
    private void RunAutoPostureSmoke()
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(200); timer.IsRepeating = true;
        var lease = _ledger!.Acquire("fixture.auto-posture", "isolated-runtime-check");
        long timeout = _autoClock.ElapsedMilliseconds + 32000, due = 0, stableRevision = -1;
        int step = 0; bool manualDone = false; string? manualError = null;
        string[] ids = [];
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "auto_posture.smoke_completed", new
            { passed, error, step, nativePointer = true, revision = stableRevision });
            ExitApplication();
        }
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Configure(string id, bool enabled)
        {
            var target = Current().PostersById[id];
            var result = _stateCommands!.Execute((PosterAutoPostureDraft.Open(target) with
                { Enabled = enabled, LeaveDelaySeconds = 1 }).ToCommand("smoke-policy-" + Guid.NewGuid().ToString("N")));
            Require(result.Succeeded, result.Error ?? result.Status.ToString());
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                Require(_autoClock.ElapsedMilliseconds < timeout, "Timed out; inspect actual presence/protection and runtime logs.");
                if (!_autoReady) return;
                var current = Current();
                if (step == 0)
                {
                    Require(current.Posters.Count == 3 && current.Posters.All(p => !p.Poster.IsCollapsed && !p.AutoPosture.Enabled),
                        "Requires three expanded, auto-disabled measurement fixture posters.");
                    ids = current.Posters.Select(p => p.Poster.PosterId).Order(StringComparer.Ordinal).ToArray();
                    foreach (string id in ids) Configure(id, true);
                    step = 1;
                }
                else if (step == 1 && current.Posters.All(p => p.Poster.IsCollapsed) && !_spatialRuntime!.IsSpatialBusy &&
                    _autoPosture!.State.Outstanding is null)
                {
                    Require(_autoTimer is not null && _ledger.ActiveResources.Count(r => r == "timer.auto-posture") == 1,
                        "Missing or duplicated shared timer.");
                    stableRevision = current.Revision; due = _autoClock.ElapsedMilliseconds + 1800; step = 2;
                }
                else if (step == 2 && _autoClock.ElapsedMilliseconds >= due)
                {
                    Require(current.Revision == stableRevision, "Stationary collapsed state is rewriting revisions.");
                    foreach (string id in ids) Configure(id, false);
                    Require(_autoTimer is null && !_inputRouter.HasPointerObservers, "Zero demand still holds input/timer work.");
                    var target = Current().PostersById[ids[0]];
                    _spatialRuntime!.SetCollapsed(new("smoke-manual-expand", StateCommandSource.ControlCenter,
                        PosterStateCommandKind.SetCollapsed, target.Revision, ids[0], target.Poster with { IsCollapsed = false })
                        { ReadSet = StateReadSet.ForPoster(ids[0], target.EntityVersion) }, result =>
                        { manualDone = true; if (!result.Succeeded) manualError = result.Error ?? result.Status.ToString(); });
                    step = 3;
                }
                else if (step == 3 && manualDone)
                {
                    Require(manualError is null, manualError ?? "");
                    Require(!current.PostersById[ids[0]].Poster.IsCollapsed, "Manual expansion failed.");
                    Configure(ids[0], true); Configure(ids[0], false); // invalidate a pending policy before deadline
                    stableRevision = Current().Revision; due = _autoClock.ElapsedMilliseconds + 1800; step = 4;
                }
                else if (step == 4 && _autoClock.ElapsedMilliseconds >= due)
                {
                    Require(current.Revision == stableRevision && !current.PostersById[ids[0]].Poster.IsCollapsed,
                        "Disabled policy executed a stale deadline.");
                    Require(_autoTimer is null && _autoPosture!.State.Outstanding is null, "Automatic work survived disabling.");
                    Configure(ids[0], true); step = 5;
                }
                else if (step == 5 && _autoPosture!.State.Outstanding is not null && _spatialRuntime!.IsSpatialBusy)
                {
                    Configure(ids[0], false); // Cancel a real animation, not just a pending deadline.
                    stableRevision = Current().Revision; due = _autoClock.ElapsedMilliseconds + 1200; step = 6;
                }
                else if (step == 6 && _autoClock.ElapsedMilliseconds >= due)
                {
                    Require(current.Revision == stableRevision && !current.PostersById[ids[0]].Poster.IsCollapsed,
                        "Cancelled animation partially committed posture.");
                    Require(!_spatialRuntime!.IsSpatialBusy && _autoPosture!.State.Outstanding is null && _autoTimer is null,
                        "Cancellation did not restore/release the flight.");
                    var frames = _spatialRuntime!.InspectPresentation(current);
                    Require(frames.Count == 3 && frames.All(f => f.XamlVerified), "Final XAML/native layout not restored.");
                    _log!.Write("info", "auto_posture.smoke_final", new { current.Revision, count = frames.Count,
                        timer = _autoTimer is not null, pointerObserver = _inputRouter.HasPointerObservers });
                    Finish(true, null);
                }
            }
            catch (Exception e) { Finish(false, e.Message); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
