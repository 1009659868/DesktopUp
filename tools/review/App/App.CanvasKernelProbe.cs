using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

public partial class App
{
    private async void RunCanvasKernelProbe(string dataRoot)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        IDisposable? lease = null;
        string stage = "guard";
        var measurements = new List<object>();
        try
        {
            string repo = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repo, "DesktopUp.sln"))) repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository required.");
            string root = Path.GetDirectoryName(Path.GetFullPath(dataRoot))!;
            if (Path.GetDirectoryName(root) != Path.Combine(repo, "out", "drop-d3") || Path.GetFileName(dataRoot) != "profile" ||
                Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") != dataRoot || !File.Exists(Path.Combine(root, ".canvas-fixture")))
                throw new IOException("Owned isolated canvas fixture required.");
            lease = _ledger!.Acquire("fixture.canvas", "native-canvas-composition"); _stopAutoSmoke = () => cancellation.Cancel();
            await _transferDiscovery;
            while (!_autoReady || _spatialRuntime!.IsSpatialBusy) await Task.Delay(50, cancellation.Token);
            var state = _stateCoordinator!; var commands = _stateCommands!; var spatial = _spatialRuntime!;
            var basis = state.Refresh(); string id = basis.Posters.Single().Poster.PosterId;
            var window = spatial.CanvasWindowForProbe(id);
            string? content = basis.Posters[0].Poster.ContentLayoutJson;
            void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
            async Task Observe(string name)
            {
                await Task.Delay(180, cancellation.Token);
                var native = window.ReadContentSurfaceForSmoke();
                Require(native.Items == 2 && native.CanvasMatches && native.InputMatches && native.Clipped && native.LastItemReachable, "Content/native mismatch: " + name);
                Require(state.Refresh().PostersById[id].Poster.ContentLayoutJson == content, "Design rewrote content authoring.");
                window.VerifyCanvasOriginForProbe();
                await window.CapturePosterXamlForProbeAsync(Path.Combine(dataRoot, name + ".png"));
            }
            stage = "restart";
            if (_startupIntent.Arguments.Contains("--canvas-restart"))
            {
                Require(basis.Posters[0].Presentation.Parameters.Canvas?.Current?.Recipe.Id == "rainy-desk", "Saved design was not restored.");
                await Observe("restarted-rainy");
            }
            else
            {
                stage = "basic";
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                {
                    session.Revise(CanvasSamples.All[0]); long revision = state.Refresh().Revision;
                    session.Preview(); Require(state.Refresh().Revision == revision, "Preview persisted state.");
                    Require(session.Apply(session.PreviewId!).Succeeded, "Basic apply failed.");
                }
                await Observe("basic");
                var retained = window.CaptureCanvasCardsForProbe();
                stage = "cancel-rain";
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                {
                    long revision = state.Refresh().Revision;
                    session.Revise(CanvasSamples.All[1]); session.Preview(); await Observe("rain-preview");
                    Require(state.Refresh().Revision == revision, "Rain preview persisted state.");
                }
                Require(state.Refresh().PostersById[id].Presentation.Parameters.Canvas?.Current?.Recipe.Id == "basic-storage", "Cancel did not retain basic.");
                await Observe("cancelled");
                stage = "apply-rain";
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                { session.Revise(CanvasSamples.All[1]); session.Preview(); Require(session.Apply(session.PreviewId!).Succeeded, "Rain apply failed."); }
                await Observe("rain-applied"); window.RequireCanvasCardsForProbe(retained);
                stage = "paint-only";
                var rain = CanvasSamples.All[1] with { Overrides = new Dictionary<string, CanvasValue> { ["background"] = new(Text: "#243346") } };
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                {
                    long bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
                    session.Revise(rain); session.Preview(); Require(session.Apply(session.PreviewId!).Succeeded, "Paint apply failed.");
                    measurements.Add(new { operation = "paint-preview-commit", elapsedMs = watch.Elapsed.TotalMilliseconds, allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes });
                }
                window.RequireCanvasCardsForProbe(retained); await Observe("paint-only");
                stage = "long-text-and-assembly";
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                {
                    session.Revise(rain with { Overrides = new Dictionary<string, CanvasValue> { ["caption"] = new(Text: new string('雨', 90)) } });
                    session.Preview(); await Observe("long-text");
                    session.Revise(CanvasSamples.Without(rain, "caption")); session.Preview(); await Observe("without-caption");
                }
                stage = "undo";
                using (var session = new CanvasDesignSession(state, commands, spatial, id))
                { session.Preview(undo: true); Require(session.Apply(session.PreviewId!).Succeeded, "Undo failed."); }
                await Observe("undo");
                stage = "translation";
                await window.VerifyDragPresentationForProbe(cancellation.Token);
                window.RequireCanvasCardsForProbe(retained);
                stage = "collapse-expand";
                var verifyWarm = window.CanvasWarmReadbackForProbe();
                spatial.ToggleCollapsed(window);
                while (spatial.IsSpatialBusy) await Task.Delay(50, cancellation.Token);
                Require(state.Refresh().PostersById[id].Poster.IsCollapsed, "Canvas collapse rejected.");
                spatial.ToggleCollapsed(window);
                while (spatial.IsSpatialBusy) await Task.Delay(50, cancellation.Token);
                Require(!state.Refresh().PostersById[id].Poster.IsCollapsed, "Canvas expansion rejected.");
                await Observe("expanded"); verifyWarm();
            }
            stage = "center"; _controlCenter!.Show();
            await Task.Delay(150, cancellation.Token);
            Require(_controlCenter.SettingsWindowForContractProbe is not null, "Control center unavailable.");
            if (!_startupIntent.Arguments.Contains("--canvas-restart"))
            {
                await _controlCenter.SettingsWindowForContractProbe!.VerifyCanvasEditorForProbe(Path.Combine(dataRoot, "editor.png"), cancellation.Token, Observe);
                await Observe("studio-rainy-final");
            }
            _log!.Write("info", "canvas.kernel_observed", new { passed = true, stage, measurements,
                persistent = state.Refresh().PostersById[id].Presentation.Parameters.Canvas?.Current?.Recipe.Id,
                nativeMouse = false, nativeKeyboard = false, FilesUnchanged = true });
        }
        catch (Exception error) { _log!.Write("error", "canvas.kernel_observed", new { passed = false, stage, error = error.ToString(), measurements }); }
        finally { lease?.Dispose(); _stopAutoSmoke = null; ExitApplication(); }
    }
}
