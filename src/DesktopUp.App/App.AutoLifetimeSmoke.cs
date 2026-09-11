using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Isolated fixture: local messages to OUR hidden HWND exercise native classification and
    // production recovery. No OS lock/sleep, Explorer restart or synthetic mouse input.
    private void RunAutoLifetimeSmoke(string mode)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(200);
        var lease = _ledger!.Acquire("fixture.auto-lifetime", "isolated-lifecycle-check");
        long deadline = _autoClock.ElapsedMilliseconds + 23000, revision = -1, epoch = -1, environment = -1;
        int step = 0, count = -1; string[] ids = []; bool observedOutside = false;
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "auto_lifetime.smoke_completed", new { passed, error, step, mode, count, observedOutside });
            ExitApplication();
        }
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Configure(string id)
        {
            var target = Current().PostersById[id];
            var result = _stateCommands!.Execute((PosterAutoPostureDraft.Open(target) with { Enabled = true, LeaveDelaySeconds = 300 })
                .ToCommand("lifetime-policy-" + Guid.NewGuid().ToString("N")));
            Require(result.Succeeded, result.Error ?? result.Status.ToString());
        }
        void Visible(string id, bool visible)
        {
            var target = Current().PostersById[id];
            var result = _stateCommands!.Execute(new PosterStateCommand("lifetime-visible-" + Guid.NewGuid().ToString("N"),
                StateCommandSource.ControlCenter, PosterStateCommandKind.SetVisible, target.Revision, id, target.Poster with { IsVisible = visible })
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) });
            Require(result.Succeeded, result.Error ?? result.Status.ToString());
        }
        void Remove(string id)
        {
            var preview = _stateCoordinator!.PrepareRemoval(id)!;
            Require(_stateCommands!.Execute(new RemovePosterCommand("lifetime-remove-" + Guid.NewGuid().ToString("N"),
                StateCommandSource.ControlCenter, preview.Revision, preview.ReadSet, id, true)).Succeeded, "Remove failed");
        }
        void Signal(uint message, nuint value)
        {
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_desktopEnvironmentAnchor!);
            _ = SendLifetimeMessage(hwnd, message, value, System.Diagnostics.Process.GetCurrentProcess().SessionId);
        }
        void Snapshot(string phase)
        {
            var s = Current(); var frames = _spatialRuntime!.InspectPresentation(s);
            int visible = s.Posters.Count(p => p.Poster.IsVisible);
            Require(_posterSessions!.Sessions.Count == visible && _inputRouter.ReadyCount == visible, "Session/input count mismatch: " + phase);
            Require(frames.Count == visible && frames.All(f => f.XamlVerified), "Layout restoration mismatch: " + phase);
            Require(_ledger!.ActiveResources.Count(r => r == "timer.auto-posture") <= 1, "Duplicated auto timer");
            _log!.Write("info", "auto_lifetime.snapshot", new { phase, s.Revision, mode,
                posters = s.Posters.Select(p => p.Poster).ToArray(), sessions = _posterSessions.Sessions,
                autoTimer = _autoTimer is not null, pointerObserver = _inputRouter.HasPointerObservers,
                outstanding = _autoPosture!.State.Outstanding is not null, frameCount = frames.Count });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "lifecycle fixture timed out");
                if (step == 0 && (!_autoReady || _spatialRuntime!.IsSpatialBusy)) return;
                var current = Current();
                switch (step)
                {
                    case 0:
                        count = current.Posters.Count; Require(count is 0 or 1 or 3 or 6, "Wrong fixture count");
                        ids = current.Posters.Select(p => p.Poster.PosterId).ToArray();
                        Require(_desktopEnvironmentWatcher!.SessionNotificationsRegistered, "Session notifications not registered");
                        if (mode == "restart")
                        {
                            Require(current.Posters.All(p => p.AutoPosture is { Enabled: true, LeaveDelaySeconds: 300 }), "Restart lost policy");
                            _controlCenter!.Show(); step = 20; break;
                        }
                        Require(current.Posters.All(p => !p.AutoPosture.Enabled), "Use a fresh auto-disabled fixture");
                        foreach (string id in ids) Configure(id);
                        step++; break;
                    case 1:
                        Snapshot("enabled");
                        Require((_autoTimer is not null) == (count > 0), "Wrong timer demand");
                        observedOutside = _autoPosture!.State.Posters.Values.Any(p => p.Last.Pointer.Presence == AutoPosturePresence.Outside);
                        if (mode == "enabled-exit" || count == 0) { Finish(true, null); break; }
                        revision = current.Revision; environment = _autoEnvironment;
                        epoch = _posterSessions!.Sessions.Single(s => s.PosterId == ids[0]).Epoch;
                        Signal(DesktopLifecycleSignals.SessionChange, 7); // local lock signal
                        Signal(DesktopLifecycleSignals.PowerBroadcast, 4);
                        step++; break;
                    case 2:
                        Require(_systemLifecycle.IsSuspended && !_autoReady && _autoTimer is null && !_inputRouter.HasPointerObservers,
                            "Suspend left automatic work alive");
                        Require(current.Revision == revision && !_spatialRuntime!.IsSpatialBusy, "Suspend wrote posture");
                        Signal(DesktopLifecycleSignals.PowerBroadcast, 18); // must not unlock
                        Require(_systemLifecycle.IsSuspended && !_autoReady, "Power resume unlocked session");
                        QueueDesktopRecovery(DesktopEnvironmentChangeKind.DisplayTopology, "fixture-display-while-locked", false);
                        step++; break;
                    case 3:
                        Require(_systemLifecycle.IsSuspended && _autoTimer is null && !_autoReady, "Recovery bypassed session lock");
                        Signal(DesktopLifecycleSignals.SessionChange, 8);
                        step++; break;
                    case 4:
                        if (!_autoReady || _controlCenterEnvironmentPending) return;
                        Require(_autoEnvironment > environment && current.Revision == revision, "Recovery replayed old deadline or persisted state");
                        Snapshot("recovered");
                        foreach (var node in _autoPosture!.State.Posters.Values.Where(n => n.DeadlineMs is not null))
                            Require(node.DeadlineMs > _autoClock.ElapsedMilliseconds + 295000, "Recovery reused old deadline");
                        Visible(ids[0], false); step++; break;
                    case 5:
                        Snapshot("hidden-one");
                        Require(_posterSessions!.Find(ids[0]) is null, "Hidden incarnation survived");
                        foreach (string id in ids.Skip(1)) Visible(id, false);
                        step++; break;
                    case 6:
                        Snapshot("hidden-all"); Require(_autoTimer is null && !_inputRouter.HasPointerObservers, "All hidden kept polling");
                        foreach (string id in ids) Visible(id, true);
                        step++; break;
                    case 7:
                        Snapshot("reshown");
                        Require(_posterSessions!.Sessions.Single(s => s.PosterId == ids[0]).Epoch > epoch, "Old incarnation reused");
                        Remove(ids[0]); step++; break;
                    case 8:
                        Snapshot("removed-one");
                        Require(!current.PostersById.ContainsKey(ids[0]) && !_autoPosture!.State.Posters.ContainsKey(ids[0]), "Deleted automatic node survived");
                        foreach (string id in ids.Skip(1)) Remove(id);
                        step++; break;
                    case 9:
                        Snapshot("zero"); Require(_autoTimer is null && !_inputRouter.HasPointerObservers, "Zero catalog kept polling");
                        _controlCenter!.Show(); step = 20; break;
                    case 20:
                        Require(_controlCenter!.VerifyProjection(current), "Control center unavailable after lifetime change");
                        Snapshot("final"); Finish(true, null); break;
                }
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendLifetimeMessage(nint window, uint message, nuint wParam, nint lParam);
}
