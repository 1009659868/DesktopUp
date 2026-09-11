using System.Diagnostics;
using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Measurement-only, behind the isolated-profile/one-probe guard. The same 250ms observer
    // runs in OFF and ON controls. It never injects a pointer or calls StartAutomatic directly.
    private void RunAutoMeasurement(string mode)
    {
        bool enabled = mode.StartsWith("on-", StringComparison.Ordinal);
        string workload = mode[(mode.IndexOf('-') + 1)..];
        bool initialCollapsed = workload == "collapsed";
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(250);
        var lease = _ledger!.Acquire("fixture.auto-measurement", "paired-resource-observer");
        long timeout = _autoClock.ElapsedMilliseconds + 35000, origin = 0, initialRevision = -1;
        long pointerBegin = 0, environment = -1; int step = 0, count = 0, manualIndex = 0;
        bool manualBusy = false; string? manualError = null; string[] ids = [];
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Marker(string phase) => _log!.Write("info", "auto_measurement.marker", new
        {
            phase, mode, count, qpc = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency,
            revision = Current().Revision, pointerSamples = _autoPointerSequence,
            autoTimer = _autoTimer is not null, pointerObserver = _inputRouter.HasPointerObservers,
            resources = _ledger!.ActiveResources.ToArray()
        });
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "auto_measurement.completed",
                new { passed, error, mode, count, step }); ExitApplication();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                Require(_autoClock.ElapsedMilliseconds < timeout, "Measurement timed out");
                if (step == 0)
                {
                    if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                    var state = Current(); count = state.Posters.Count;
                    Require(count is 1 or 3 or 6, "Requires 1/3/6 posters");
                    Require(state.Posters.All(p => p.Poster.IsVisible && p.Poster.IsCollapsed == initialCollapsed &&
                        !p.AutoPosture.Enabled), "Use a new auto-disabled measurement fixture with the matching posture");
                    ids = state.Posters.Select(p => p.Poster.PosterId).Order(StringComparer.Ordinal).ToArray();
                    foreach (string id in ids)
                    {
                        var draft = PosterAutoPostureDraft.Open(Current().PostersById[id]) with
                        { Enabled = enabled, LeaveDelaySeconds = workload == "trigger" ? 10 : 300 };
                        var result = _stateCommands!.Execute(draft.ToCommand("measure-policy-" + Guid.NewGuid().ToString("N")));
                        Require(result.Succeeded, result.Error ?? result.Status.ToString());
                    }
                    initialRevision = Current().Revision; environment = _autoEnvironment;
                    origin = _autoClock.ElapsedMilliseconds; Marker("ready"); step = 1; return;
                }
                long elapsed = _autoClock.ElapsedMilliseconds - origin;
                Require(_autoReady && _autoEnvironment == environment, "Desktop recovery invalidated sample");
                Require(_posterSessions!.Sessions.Count == count && _inputRouter.ReadyCount == count, "Session changed during sample");
                Require(_ledger!.ActiveResources.Count(r => r == "timer.auto-posture") == (enabled ? 1 : 0),
                    "Unexpected dedicated timer count");
                if (enabled && elapsed >= 1000 && !_spatialRuntime!.IsSpatialBusy)
                {
                    Require(_autoPosture!.State.Posters.Count == count &&
                        _autoPosture.State.Posters.Values.All(n => n.Last.Pointer.Presence == AutoPosturePresence.Outside &&
                            n.Last.Protection == AutoPostureProtection.None &&
                            n.Phase is not (AutoPosturePhase.Unavailable or AutoPosturePhase.Protected)),
                        "Input not neutral/readable; unlock desktop, keep pointer outside posters and release menus/buttons");
                }
                if (step == 1 && elapsed >= 8000)
                {
                    Require(Current().Revision == initialRevision && !_spatialRuntime!.IsSpatialBusy, "Unexpected action before sample window");
                    pointerBegin = _autoPointerSequence; Marker("begin"); step = 2;
                }
                // OFF trigger is an explicit programmatic collapse control, not an automatic or
                // physical-mouse result. ON trigger must be initiated only by the real scheduler.
                if (!enabled && workload == "trigger" && elapsed >= 10000 && manualIndex < count && !manualBusy)
                {
                    string id = ids[manualIndex++]; var target = Current().PostersById[id]; manualBusy = true;
                    var command = new PosterStateCommand("measure-manual-" + Guid.NewGuid().ToString("N"),
                        StateCommandSource.ControlCenter, PosterStateCommandKind.SetCollapsed, target.Revision, id,
                        target.Poster with { IsCollapsed = true }) { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) };
                    _spatialRuntime!.SetCollapsed(command, result => { manualBusy = false; if (!result.Succeeded) manualError = result.Error ?? result.Status.ToString(); });
                }
                Require(manualError is null, manualError ?? "");
                if (step == 2 && elapsed >= 18000)
                {
                    var state = Current();
                    Require(!_spatialRuntime!.IsSpatialBusy && !manualBusy && _autoPosture!.State.Outstanding is null, "Unfinished posture at window end");
                    Require(state.Revision == initialRevision + (workload == "trigger" ? count : 0), "Unexpected revision writes");
                    Require(state.Posters.All(p => p.Poster.IsCollapsed == (initialCollapsed || workload == "trigger")), "Unexpected final posture");
                    var frames = _spatialRuntime.InspectPresentation(state);
                    Require(frames.Count == count && frames.All(f => f.XamlVerified), "Final presentation layout not restored");
                    if (!enabled && workload != "trigger") Require(_autoPointerSequence == pointerBegin, "OFF steady state kept sampling");
                    Marker("end"); step = 3; Finish(true, null);
                }
            }
            catch (Exception e) { Finish(false, e.Message); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
