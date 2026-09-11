using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private void RunAutoSettingsSmoke(string root)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(250);
        var lease = _ledger!.Acquire("fixture.auto-settings", "isolated-xaml-check");
        long deadline = _autoClock.ElapsedMilliseconds + 25000, revision = -1;
        int step = 0; string a = "", b = ""; Task? capture = null;
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "auto_settings.smoke_completed", new { passed, error, step, revision });
            ExitApplication();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "settings fixture timed out");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                var editor = window?.SettingsEditorForProbe;
                switch (step)
                {
                    case 0:
                        Require(current.Posters.Count is 0 or 3, "Requires zero or three isolated posters.");
                        revision = current.Revision; _controlCenter.Show(); step++; break;
                    case 1:
                        if (window?.Content.XamlRoot is null) return;
                        if (current.Posters.Count == 0)
                        {
                            Require(!window.SettingsControlsEnabledForProbe && !editor!.CanApply && !_controlCenter.IsSettingsInteractionActive,
                                "Empty selection enabled settings/protection.");
                            window.RevealSettingsForProbe(); step = 20; break;
                        }
                        a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId;
                        window.SelectSettingsTargetForProbe(a);
                        Require(!editor!.IsDirty && !editor.Enabled && editor.DelayText == "3" && !_controlCenter.IsSettingsInteractionActive,
                            "Opening changed committed defaults or protected interaction.");
                        window.EditSettingsForProbe(true, "1.5");
                        Require(editor.DelayText == "1.5" && !editor.CanApply, "Raw text was not synchronously captured.");
                        _controlCenter.ApplyProjection(current.ControlCenter);
                        Require(editor.DelayText == "1.5" && !editor.CanApply, "An intervening projection discarded new raw input.");
                        _log!.Write("info", "auto_settings.input_assigned", new { editor.DelayText, editor.CanApply, protection = _controlCenter.IsSettingsInteractionActive });
                        step = 13; break;
                    case 13:
                        Require(!editor!.CanApply && editor.DelayText == "1.5" && window!.SettingsWarningForProbe && _controlCenter.IsSettingsInteractionActive,
                            $"Invalid UI input state: delay={editor!.DelayText}, apply={editor.CanApply}, protected={_controlCenter.IsSettingsInteractionActive}.");
                        Require(ReadPassivePointer(++_autoPointerSequence, _autoEnvironment).SharedProtection.HasFlag(AutoPostureProtection.Editing), "Settings protection not sampled.");
                        window!.InvokeSettingsForProbe(false); step = 2; break;
                    case 2:
                        Require(current.Revision == revision && !editor!.IsDirty && editor.DelayText == "3", "Cancel saved/reset incorrectly.");
                        window!.EditSettingsForProbe(true, "7"); step = 14; break;
                    case 14:
                        Require(editor!.IsDirty && current.Revision == revision, "A draft unexpectedly saved.");
                        window!.SelectSettingsTargetForProbe(b);
                        Require(editor!.PosterId == b && !editor.IsDirty && editor.DelayText == "3", "Target switch retained A draft.");
                        window.EditSettingsForProbe(true, "300"); step = 15; break;
                    case 15:
                        window!.InvokeSettingsForProbe(true); step = 3; break;
                    case 3:
                        Require(current.PostersById[b].AutoPosture is { Enabled: true, LeaveDelaySeconds: 300 } &&
                            !current.PostersById[a].AutoPosture.Enabled && !editor!.IsDirty && !editor.IsPending, "B Apply failed or wrote A.");
                        window!.SelectSettingsTargetForProbe(a); window.EditSettingsForProbe(false, "15");
                        step = 16; break;
                    case 16:
                        var target = current.PostersById[a];
                        var external = _stateCommands!.Execute((PosterAutoPostureDraft.Open(target) with { LeaveDelaySeconds = 19 }).ToCommand("settings-smoke-external"));
                        Require(external.Succeeded, "External conflict fixture failed."); revision = external.CurrentRevision;
                        Require(editor!.IsStale && editor.DelayText == "15" && window!.SettingsWarningForProbe, "Dirty text rebased on projection.");
                        window!.InvokeSettingsForProbe(true); step = 4; break;
                    case 4:
                        Require(current.Revision == revision && editor!.Error is not null && editor.DelayText == "15" && !editor.IsPending,
                            "Stale Apply did not surface conflict or overwrote committed value.");
                        window!.InvokeSettingsForProbe(false); step++; break;
                    case 5:
                        Require(editor!.DelayText == "19" && !editor.IsDirty && !editor.IsStale, "Cancel did not load latest value.");
                        window!.EditSettingsForProbe(true, "300"); step = 17; break;
                    case 17:
                        window!.InvokeSettingsForProbe(true); step = 6; break;
                    case 6:
                        Require(current.PostersById[a].AutoPosture is { Enabled: true, LeaveDelaySeconds: 300 } && !editor!.IsDirty, "A Apply failed.");
                        revision = current.Revision; window!.EditSettingsForProbe(false, "8");
                        step = 18; break;
                    case 18:
                        Require(editor!.IsDirty && _controlCenter.IsSettingsInteractionActive, "Close test had no dirty draft.");
                        _controlCenter.CloseForContractProbe(); step = 7; break;
                    case 7:
                        Require(window is null && !_controlCenter.IsSettingsInteractionActive && current.Revision == revision, "Close saved draft or retained protection.");
                        _controlCenter.Show(); step++; break;
                    case 8:
                        if (window?.Content.XamlRoot is null) return;
                        Require(editor is { Enabled: true, DelayText: "300", IsDirty: false } && !_controlCenter.IsSettingsInteractionActive,
                            "Reopen failed to restore committed values.");
                        window.RevealSettingsForProbe(); step++; break;
                    case 9:
                        capture = window!.CaptureSettingsForProbeAsync(Path.Combine(root, "control-center-settings.png")); step = 12; break;
                    case 12:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult();
                        Require(current.Revision == revision && _controlCenter.VerifyProjection(current), "Unexpected commit or stale center projection.");
                        Finish(true, null); break;
                    case 20:
                        capture = window!.CaptureSettingsForProbeAsync(Path.Combine(root, "control-center-empty.png")); step = 12; break;
                }
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
