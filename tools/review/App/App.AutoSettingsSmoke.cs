using DesktopUp.Application;
using Microsoft.UI.Dispatching;
namespace DesktopUp;

public partial class App
{
    // Replaces the retired M08 Apply/Cancel UI fixture. Same isolated CLI guard.
    private void RunAutoSettingsSmoke(string root)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(150);
        var lease = _ledger!.Acquire("fixture.auto-settings", "ux-controls");
        long deadline = _autoClock.ElapsedMilliseconds + 25000, revision = 0;
        int step = 0; string a = ""; Task? capture = null;
        bool ticking = false;
        long captureAfter = 0;
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Require(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
        void Finish(bool passed, string? error)
        { Stop(); _log!.Write(passed ? "info" : "error", "auto_settings.smoke_completed", new { passed, error, step, revision, nativeMouse = false }); ExitApplication(); }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking) return;
            ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "UX timeout");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                switch (step)
                {
                    case 0:
                        _controlCenter.Show(); revision = current.Revision; step++; break;
                    case 1:
                        if (window?.Content.XamlRoot is null) return;
                        if (current.Posters.Count == 0)
                        { Require(!window.SettingsControlsEnabledForProbe, "empty controls enabled"); step = 20; break; }
                        a = current.Posters[0].Poster.PosterId; window.SelectSettingsTargetForProbe(a);
                        var basis = _spatialRuntime.ReadControlCenterGeometry(a);
                        Require(basis is not null && window.EditorForProbe.Geometry == basis.Text(current.PostersById[a].Poster), "actual geometry mismatch");
                        var nativeBefore = _spatialRuntime.ReadLocateForProbe(a);
                        window.LocateSelected();
                        Require(_spatialRuntime.HasLocateForProbe(a) && nativeBefore == _spatialRuntime.ReadLocateForProbe(a),
                            "locate changed native frame, owner, style, foreground or Z order");
                        Require(_stateCoordinator.Refresh().Revision == revision, "locate wrote state");
                        for (int i = 1; i <= 15; i++) window.PreviewScaleForProbe(1 + i * .02);
                        Require(window.CommonSettingsForProbe.Preview is not null && _stateCoordinator.Refresh().Revision == revision, "preview wrote SQLite");
                        step++; break;
                    case 2:
                        Require(current.Revision == revision, "held preview committed");
                        Require(window!.CommonSettingsForProbe.Preview is { } preview && _spatialRuntime.VerifyCenterPreviewForProbe(preview),
                            "slider preview did not reach actual XAML: " + System.Text.Json.JsonSerializer.Serialize(_spatialRuntime.ReadCenterPreviewForProbe(window!.CommonSettingsForProbe.Preview!)));
                        window!.FinishSettingsForProbe(); step++; break;
                    case 3:
                        Require(_spatialRuntime.InspectPresentation(current).All(p => p.XamlVerified), "committed XAML mismatch");
                        Require(current.Revision == revision + 1 && Math.Abs(current.PostersById[a].Presentation.Parameters.Preferences!.Scale - 1.3) < .001,
                            "scale not committed once");
                        Require(current.Posters.Where(p => p.Poster.PosterId != a).All(p => p.Presentation.Parameters.Preferences is null), "scale leaked target");
                        revision = current.Revision; window!.ToggleForProbe(true); step++; break;
                    case 4:
                        Require(current.Revision == revision + 1 && current.PostersById[a].AutoPosture.Enabled, "toggle required Apply");
                        revision = current.Revision; window!.DelayForProbe(1.5); window.FinishSettingsForProbe(); step++; break;
                    case 5:
                        Require(current.Revision == revision && window!.CommonSettingsForProbe.Error is not null, "invalid delay saved");
                        window!.DelayForProbe(19); window.FinishSettingsForProbe(); step++; break;
                    case 6:
                        Require(current.Revision == revision + 1 && current.PostersById[a].AutoPosture.LeaveDelaySeconds == 19, "delay not auto saved");
                        revision = current.Revision; window!.PreviewScaleForProbe(1.4);
                        var target = current.PostersById[a];
                        var external = _stateCommands!.Execute(new PosterStateCommand("ux-external", StateCommandSource.ControlCenter,
                            PosterStateCommandKind.Rename, current.Revision, a, target.Poster with { Title = target.Poster.Title + " / UX" })
                            { ReadSet = StateReadSet.ForPoster(a, target.EntityVersion) });
                        Require(external.Succeeded, "external fixture failed"); window.FinishSettingsForProbe(); step++; break;
                    case 7:
                        Require(current.Revision == revision + 1 && window!.CommonSettingsForProbe.Error is not null &&
                            Math.Abs(current.PostersById[a].Presentation.Parameters.Preferences!.Scale - 1.3) < .001, "stale preview overwrote state");
                        revision = current.Revision;
                        window!.PreviewScaleForProbe(1.45);
                        if (current.Posters.Count > 1)
                        {
                            window.SelectSettingsTargetForProbe(current.Posters[1].Poster.PosterId);
                            Require(window.CommonSettingsForProbe.Preview is null, "unfinished A preview leaked selection");
                            window.SelectSettingsTargetForProbe(a); window.PreviewScaleForProbe(1.45);
                        }
                        _controlCenter.CloseForContractProbe(); step++; break;
                    case 8:
                        Require(window is null && current.Revision == revision && !_controlCenter.IsSettingsInteractionActive, "close leaked state/protection");
                        _controlCenter.Show(); step++; break;
                    case 9:
                        if (window?.Content.XamlRoot is null) return;
                        Require(window.CommonSettingsForProbe.Preview is null && _controlCenter.VerifyProjection(current), "reopen not authoritative");
                        window.LocateSelected();
                        Require(_spatialRuntime.HasLocateForProbe(a), "repeated locate missing");
                        step = 10; break;
                    case 10:
                        Require(window!.VerifyPresetLayoutForProbe(), "compact native-track marks unavailable or too wide");
                        revision = current.Revision; window.PresetForProbe(false, 0); step++; break;
                    case 11:
                        Require(window!.VerifyPresetLayoutForProbe(), "minimum native thumb/mark alignment");
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.Preferences!.Scale == .8,
                            "small preset was not one exact commit");
                        revision = current.Revision; window!.PresetForProbe(false, 1); step++; break;
                    case 12:
                        Require(window!.VerifyPresetLayoutForProbe(), "standard native thumb/mark alignment");
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.Preferences!.Scale == 1,
                            "nonuniform standard preset missed");
                        revision = current.Revision; window!.PresetForProbe(true, 2); step++; break;
                    case 13:
                        Require(window!.VerifyPresetLayoutForProbe(), "spacing maximum native thumb/mark alignment");
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.Preferences!.Spacing == 1.5 &&
                            current.PostersById[a].Presentation.Parameters.Preferences!.Scale == 1, "spacing preset missed or changed scale");
                        Require(_spatialRuntime.InspectPresentation(current).All(p => p.XamlVerified), "preset committed XAML mismatch");
                        step = 20; break;
                    case 20:
                        window!.RevealSettingsForProbe(); captureAfter = _autoClock.ElapsedMilliseconds + 1000; step++; break;
                    case 21:
                        if (_autoClock.ElapsedMilliseconds < captureAfter) return;
                        capture = window!.CaptureSettingsForProbeAsync(Path.Combine(root, "control-center-ux.png")); step++; break;
                    case 22:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult();
                        window!.RevealBehaviorForProbe(); captureAfter = _autoClock.ElapsedMilliseconds + 1000; step++; break;
                    case 23:
                        if (_autoClock.ElapsedMilliseconds < captureAfter) return;
                        capture = window!.CaptureSettingsForProbeAsync(Path.Combine(root, "control-center-behavior.png")); step++; break;
                    case 24:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult();
                        Require(a.Length == 0 || !_spatialRuntime.HasLocateForProbe(a), "locate did not expire");
                        Finish(true, null); break;
                }
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
