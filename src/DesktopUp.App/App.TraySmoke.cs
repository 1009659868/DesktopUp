using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private sealed record TrayRestartProof(long Revision, string Fingerprint, int Count);
    private void RunTraySmoke(string root)
    {
        bool restart = _startupIntent.Arguments.Contains("--tray-restart", StringComparer.Ordinal);
        string proofPath = Path.Combine(root, "tray-restart-proof.json");
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.tray", "isolated-real-menu-commands");
        int step = 0, initialCount = 0, rounds = 0; long revision = 0;
        long deadline = _autoClock.ElapsedMilliseconds + 65000;
        string individuallyHidden = "", createdId = ""; bool ticking = false; object? center = null;
        var cases = new List<string>();
        string Fingerprint(DesktopUpProjection p) => StateRequestFingerprint.Compute(new
            { Posters = p.Posters.Select(t => t.Poster).ToArray(), Settings = p.ControlCenter.Settings.Settings });
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool pass, string? error)
        {
            Stop();
            _log!.Write(pass ? "info" : "error", "tray.smoke_completed", new
                { passed = pass, error, step, initialCount, restart, cases, nativeMouse = false, nativeSystemLifecycle = false });
            if (pass) _tray!.InvokeForProbe(TrayAction.Exit); else ExitApplication();
        }
        void Invoke(TrayAction action) => _tray!.InvokeForProbe(action);
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking) return; ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "tray process timeout");
                // Step 12 deliberately waits under a simulated system lock, where environment remains pending.
                if (_controlCenterEnvironmentPending && step != 12) return;
                if (!_stateCoordinator!.TryGetCurrent(out var state) || state is null) return;
                var p = state;
                Require(_tray!.VerifyProjection(p.Tray), "six-menu projection mismatch");
                switch (step)
                {
                    case 0:
                        Require(_controlCenter!.SettingsWindowForContractProbe is null, "background startup opened center");
                        Require(!_posterSessions!.GloballyHidden, "runtime hidden flag persisted");
                        Require(_inputRouter.ReadyCount == p.Posters.Count(t => t.Poster.IsVisible), "initial input count");
                        if (restart)
                        {
                            var proof = JsonSerializer.Deserialize<TrayRestartProof>(File.ReadAllText(proofPath))!;
                            Require(p.Revision == proof.Revision && p.Posters.Count == proof.Count && Fingerprint(p) == proof.Fingerprint,
                                "restart changed catalog/settings");
                            cases.Add("restart-restores-individual-intent-not-global-hide"); Finish(true, null); return;
                        }
                        initialCount = p.Posters.Count; revision = p.Revision;
                        Require(initialCount is 0 or 1 or 3, "isolated 0/1/3 fixture required");
                        Require(p.Posters.All(t => !t.AutoPosture.Enabled), "auto-OFF fixture required");
                        Invoke(TrayAction.Create); step++; break;
                    case 1:
                        Require(p.Posters.Count == initialCount + 1 && p.Revision == revision + 1, "normal create not one revision");
                        createdId = p.Posters.Last().Poster.PosterId;
                        Require(p.PostersById[createdId].Presentation.CanEdit, "new poster appearance is read-only");
                        Require(_controlCenter!.SettingsWindowForContractProbe is null, "tray create forced center open");
                        cases.Add("normal-create-with-center-closed");
                        Invoke(TrayAction.OpenCenter); step++; break;
                    case 2:
                        center = _controlCenter!.SettingsWindowForContractProbe;
                        if (center is null) return;
                        _controlCenter.SettingsWindowForContractProbe!.SelectSettingsTargetForProbe(p.Posters.Last().Poster.PosterId);
                        step = 20; break;
                    case 20:
                        var appearance = _controlCenter!.SettingsWindowForContractProbe!;
                        if (appearance.Content is not Microsoft.UI.Xaml.FrameworkElement { IsLoaded: true }) return;
                        appearance.RevealSettingsForProbe();
                        Require(appearance.AppearanceControlsEnabledForProbe, "new poster appearance controls disabled");
                        revision = p.Revision; appearance.TemplateForProbe("night-ops"); step++; break;
                    case 21:
                        Require(p.Revision == revision + 1 && p.PostersById[createdId].Poster.TemplateKey == "night-ops", "new template not committed");
                        revision = p.Revision;
                        _controlCenter!.SettingsWindowForContractProbe!.PreviewScaleForProbe(1.2);
                        _controlCenter.SettingsWindowForContractProbe.FinishSettingsForProbe(); step++; break;
                    case 22:
                        Require(p.Revision == revision + 1 && p.PostersById[createdId].Presentation.Parameters.Preferences?.Scale == 1.2, "new scale not committed");
                        revision = p.Revision;
                        _controlCenter!.SettingsWindowForContractProbe!.SpacingForProbe(1.3);
                        _controlCenter.SettingsWindowForContractProbe.FinishSettingsForProbe(); step++; break;
                    case 23:
                        Require(p.Revision == revision + 1 && p.PostersById[createdId].Presentation.Parameters.Preferences?.Spacing == 1.3, "new spacing not committed");
                        cases.Add("new-poster-template-scale-spacing-real-controls");
                        revision = p.Revision; Invoke(TrayAction.LockAll); step = 3; break;
                    case 3:
                        Require(p.Tray.AllPostersLocked && p.Revision == revision + 1, "global lock");
                        Invoke(TrayAction.OpenCenter); Invoke(TrayAction.OpenCenter);
                        revision = p.Revision; Invoke(TrayAction.LockAll); step++; break;
                    case 4:
                        Require(p.Posters.All(t => !t.Poster.IsLocked) && p.Revision == revision + 1, "global unlock");
                        Require(ReferenceEquals(center, _controlCenter!.SettingsWindowForContractProbe), "duplicate center");
                        cases.Add("selection-independent-global-lock-and-unique-center");
                        revision = p.Revision; Invoke(TrayAction.Motion); step++; break;
                    case 5:
                        Require(p.Tray.DynamicEffectsPaused && p.Revision == revision + 1, "motion pause setting");
                        revision = p.Revision; Invoke(TrayAction.Motion); step++; break;
                    case 6:
                        Require(!p.Tray.DynamicEffectsPaused && p.Revision == revision + 1, "motion resume setting");
                        cases.Add("motion-setting-only-not-full-animation");
                        var last = p.Posters.Last(); individuallyHidden = last.Poster.PosterId;
                        var hiddenResult = _stateCommands!.Execute(new PosterStateCommand("tray-probe-individual-hide",
                            StateCommandSource.ControlCenter, PosterStateCommandKind.SetVisible, p.Revision, individuallyHidden,
                            last.Poster with { IsVisible = false }) { ReadSet = StateReadSet.ForPoster(individuallyHidden, last.EntityVersion) });
                        Require(hiddenResult.Succeeded, hiddenResult.Error ?? "individual hide");
                        step++; break;
                    case 7:
                        revision = p.Revision; Invoke(TrayAction.Visibility); step++; break;
                    case 8:
                        Require(_posterSessions!.GloballyHidden && _posterSessions.Sessions.Count == 0 &&
                            _inputRouter.ReadyCount == 0 && _autoTimer is null && p.Revision == revision, "hide leaked session/input/timer or persisted");
                        Require(_controlCenter!.VerifyProjection(p), "center hidden projection");
                        cases.Add("global-hide-without-state-write");
                        Invoke(TrayAction.Create); step++; break;
                    case 9:
                        Require(p.Posters.Count == initialCount + 2 && p.Revision == revision + 1, "hidden create not one revision");
                        Require(_posterSessions!.GloballyHidden && _posterSessions.Sessions.Count == 0 && _inputRouter.ReadyCount == 0,
                            "hidden create exposed a window");
                        revision = p.Revision; cases.Add("create-while-hidden-remains-hidden");
                        QueueDesktopRecovery(DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
                            "tray-probe-hidden-recovery", invalidateShell: true);
                        step++; break;
                    case 10:
                        Require(_posterSessions!.GloballyHidden && _posterSessions.Sessions.Count == 0 && p.Revision == revision, "recovery exposed hidden windows");
                        cases.Add("shared-shell-display-reprobe-preserves-hide");
                        OnSystemLifecycleChanged(SystemLifecycleSignal.SessionLock);
                        SetTrayGlobalHidden(false); step = 12; break;
                    case 12:
                        Require(_systemLifecycle.IsSuspended && _posterSessions!.Sessions.All(s => s.WindowHandle == "0x0" &&
                            s.Status == PosterSessionStatus.Starting) && _inputRouter.ReadyCount == 0,
                            "restore bypassed system suspension");
                        cases.Add("simulated-system-suspend-blocks-restore");
                        OnSystemLifecycleChanged(SystemLifecycleSignal.SessionUnlock); step++; break;
                    case 13:
                        Require(!_posterSessions!.GloballyHidden && _posterSessions.Find(individuallyHidden) is null &&
                            _inputRouter.ReadyCount == p.Posters.Count(t => t.Poster.IsVisible), "restore ignored individual intent");
                        var actual = p.Posters.Where(t => t.Poster.IsVisible)
                            .Select(t => _spatialRuntime!.ReadControlCenterGeometry(t.Poster.PosterId)).ToArray();
                        Require(actual.All(x => x is not null), "missing restored physical readback");
                        for (int i = 0; i < actual.Length; i++)
                        for (int j = i + 1; j < actual.Length; j++)
                            Require(!actual[i]!.Bounds.Overlaps(actual[j]!.Bounds), "restored posters overlap");
                        Require(p.Revision == revision, "restore wrote state");
                        cases.Add("restore-native-occupancy-no-overlap");
                        step++; break;
                    case 14: Invoke(TrayAction.Visibility); step++; break;
                    case 15:
                        Require(_posterSessions!.Sessions.Count == 0 && _inputRouter.ReadyCount == 0, "repeat hide");
                        Invoke(TrayAction.Visibility); step++; break;
                    case 16:
                        Require(p.Revision == revision && _inputRouter.ReadyCount == p.Posters.Count(t => t.Poster.IsVisible), "repeat restore");
                        if (++rounds < 3) { step = 14; break; }
                        cases.Add("three-hide-restore-rounds");
                        _controlCenter!.CloseForContractProbe();
                        step++; break;
                    case 17:
                        Require(_controlCenter!.SettingsWindowForContractProbe is null && !_controlCenter.IsSettingsInteractionActive, "center close leaked");
                        // Interrupt the existing R4 posture path via the real queued tray binding.
                        var target = p.Posters.First(t => t.Poster.IsVisible);
                        _spatialRuntime!.SetCollapsed(new("tray-probe-posture", StateCommandSource.ControlCenter,
                            PosterStateCommandKind.SetCollapsed, p.Revision, target.Poster.PosterId,
                            target.Poster with { IsCollapsed = !target.Poster.IsCollapsed })
                            { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) }, _ => { });
                        Require(_spatialRuntime.IsSpatialBusy, "in-flight R4 posture not exercised");
                        Invoke(TrayAction.Visibility); step++; break;
                    case 18:
                        Require(_posterSessions!.GloballyHidden && !_spatialRuntime!.IsSpatialBusy && _inputRouter.ReadyCount == 0 &&
                            _autoTimer is null, "hide did not drain in-flight posture/input");
                        Require(p.Revision == revision, "interrupted posture committed unexpectedly");
                        cases.Add("hide-cancels-inflight-r4-through-shared-restore");
                        File.WriteAllText(proofPath, JsonSerializer.Serialize(new TrayRestartProof(p.Revision, Fingerprint(p), p.Posters.Count)));
                        cases.Add("exit-via-real-tray-command");
                        Finish(true, null); break;
                }
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
