using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Isolated R3 observation only. Uses the actual center and poster surfaces; does not send
    // content commands, emulate native input, or certify GPU desktop composition.
    private void RunUiCSampleProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.ui-c-sample", "isolated-center-and-poster");
        var cases = new[] { ("wide-folded", false, 980, 720, false, false), ("wide-open", true, 980, 720, false, false),
            ("narrow-folded", false, 680, 820, true, false), ("narrow-open", true, 680, 820, true, false),
            ("behavior", false, 980, 720, false, true) };
        var observations = new List<object>();
        long deadline = _autoClock.ElapsedMilliseconds + 30000, readyAfter = 0, revision = -1;
        string? authoringFingerprint = null;
        int step = 0, index = 0;
        Task? capture = null;
        bool ticking = false, stopped = false;
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Require(bool value, string reason)
        { if (!value) throw new InvalidOperationException(reason); }
        void Finish(bool passed, string? error)
        {
            Stop();
            _log!.Write(passed ? "info" : "error", "ui_c.sample_observed", new
            {
                passed, error, step, revision, observations,
                nativeMouse = false, nativeKeyboard = false, nativeWheel = false,
                desktopPixelVisualCertified = false, uiCProductAccepted = false
            });
            ExitApplication();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking || stopped) return;
            ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "UI C observation timed out.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                if (step == 0) { _controlCenter.Show(); step = 1; return; }
                if (window?.Content.XamlRoot is null) return;
                Require(current.Posters.Count == 1, "Use a one-poster isolated sample fixture.");
                var fingerprint = StateRequestFingerprint.Compute(current.Posters.Select(p => p.Poster).ToArray());
                if (authoringFingerprint is null) { authoringFingerprint = fingerprint; revision = current.Revision; }
                Require(current.Revision == revision && fingerprint == authoringFingerprint,
                    "View-only sample changed persisted poster configuration.");
                var sample = cases[index];
                if (step == 1)
                {
                    double rasterScale = window.Content.XamlRoot.RasterizationScale;
                    window.PrepareUiCSampleForProbe(sample.Item2,
                        checked((int)Math.Ceiling(sample.Item3 * rasterScale)),
                        checked((int)Math.Ceiling(sample.Item4 * rasterScale)), sample.Item6);
                    readyAfter = _autoClock.ElapsedMilliseconds + 600; step = 2; return;
                }
                if (_autoClock.ElapsedMilliseconds < readyAfter) return;
                if (step == 2)
                {
                    var center = window.ReadUiCSampleForProbe();
                    Require(center.ControlsConnected && center.MacrosOutsideFolder && center.HorizontalBoundsValid,
                        "Center controls or horizontal bounds failed: " + System.Text.Json.JsonSerializer.Serialize(center));
                    Require(center.VisualSampleCount == 3 && center.ParallelMacros && center.BrowsingInAppearance &&
                        center.FineTuneCollapsed && center.AdvancedCollapsed && center.GlobalControlsSeparated,
                        "The actual UI C workspace lost visual samples, common controls or progressive disclosure: " + System.Text.Json.JsonSerializer.Serialize(center));
                    Require(center.BehaviorActionsGrouped && center.NativeTitleWithoutBrandHeader && center.RecipeEntrypointsConnected && center.BehaviorSelected == sample.Item6,
                        "The UI C correction lost behavior grouping, its single title or supported recipe entrypoints: " + System.Text.Json.JsonSerializer.Serialize(center));
                    Require(center.FolderExpanded == sample.Item2 && center.Compact == sample.Item5 && center.Revision == revision,
                        "Folder state, expected wide/compact coverage or center projection is stale.");
                    var poster = _spatialRuntime.ReadContentSurfaceForSmoke(current.Posters[0].Poster.PosterId);
                    Require(poster.Items > 0 && poster.CanvasMatches && poster.InputMatches && poster.Clipped,
                        "Actual poster canvas/input/clip disagree.");
                    string path = Path.Combine(dataRoot, "ui-c-" + sample.Item1 + ".png");
                    observations.Add(new { name = sample.Item1, center, poster, capturePath = path });
                    capture = window.CaptureSettingsForProbeAsync(path);
                    step = 3; return;
                }
                if (capture is null || !capture.IsCompleted) return;
                capture.GetAwaiter().GetResult();
                if (++index == cases.Length) { Finish(true, null); return; }
                step = 1;
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick;
        timer.Start();
    }
}
