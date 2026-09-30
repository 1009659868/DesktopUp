using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal RainSceneObservation? ReadRainForProbe() => (_sceneInstance as RainSceneInstance)?.Observe();
    internal object RainSurfaceForProbe() => new { backdrop = BackdropColor().A, valid = VerifyWindowBackdrop(),
        metaOpacity = _metaPanel?.Opacity, contentTop = ContentTopDip, rootOpacity = _window?.Content.Opacity,
        module = ReadSceneModuleForProbe(), surface = inputRouter.ReadSurface(inputSession) is null ? null : ReadContentSurfaceForSmoke() };
    internal bool RainGeometryUnchangedForProbe(object? canvas, Dictionary<string, object> cards) =>
        ReferenceEquals(canvas, _contentCanvas) && cards.Count == _contentCards.Count &&
        cards.All(p => _contentCards.TryGetValue(p.Key, out var card) && ReferenceEquals(card, p.Value));
    internal (object?, Dictionary<string, object>) RainReferencesForProbe() =>
        (_contentCanvas, _contentCards.ToDictionary(p => p.Key, p => (object)p.Value));
    internal bool RainSurfaceValidForProbe => BackdropColor().A == 0 && VerifyWindowBackdrop() &&
        _metaPanel?.Opacity == 0 && _window?.Content.Opacity == 1 && _contentCards.Values.All(v => v.Opacity == 1);
}
internal sealed partial class PosterSpatialRuntime
{
    internal PosterVisualWindow RainWindowForProbe(string id) => _windows[id];
}
public sealed partial class ControlCenterWindow
{
    internal void ChangeRecipeColorForProbe(string key, string value)
    { _lampColors[key].Text = value; FinishSettingsForProbe(); }
}
public partial class App
{
    private void RunRainSceneProbe(string root, bool restart)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.rain-scene", "schema-binding-and-lifecycle");
        var operations = new Queue<(string Name, Action Action, Func<bool>? Ready)>(); var observations = new List<object>();
        bool initialized = false, stopped = false, ticking = false; string a = "", b = "", step = "initial";
        long readyAt = 0, deadline = _autoClock.ElapsedMilliseconds + 85000, frames = 0;
        string? contentA = null, contentB = null, otherRaw = null, geometry = null;
        (object? Canvas, Dictionary<string, object> Cards) references = (null, new());
        ControlCenterWindow? center = null; Task? capture = null;
        string expectedPath = Path.Combine(root, "rain-expected.json");
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        PosterVisualWindow View(string id) => _spatialRuntime!.RainWindowForProbe(id);
        RainSceneObservation Scene(string id) => View(id).ReadRainForProbe() ?? throw new InvalidOperationException("Rain renderer missing.");
        int Timers() => _ledger!.ActiveResources.Count(r => r == "timer.scene-motion");
        void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(step + ": " + error); }
        void Step(string name, Action action, Func<bool>? ready = null) => operations.Enqueue((name, action, ready));
        string Stored() => JsonSerializer.Serialize(new { catalog = Current().ControlCenter.Settings.Settings.TemplateCatalogJson,
            posters = Current().Posters.Select(p => new { p.Poster.PosterId, p.Poster.TemplateKey, p.Poster.LayoutParametersJson, p.Poster.ContentLayoutJson }) });
        void Stop() { if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "rain.scene_observed", new { passed, error, step, restart, observations,
                nativeInput = false, desktopPixelsCertified = false, captureIncludesBackdrop = false,
                pointer = _spatialRuntime?.ReadScenePointerDiagnosticsForProbe() }); ExitApplication();
        }
        async Task Capture(string name)
        {
            await center!.CaptureSettingsForProbeAsync(Path.Combine(root, "rain-" + name + "-center.png"));
            await _spatialRuntime!.CapturePosterXamlForProbeAsync(a, Path.Combine(root, "rain-" + name + "-xaml.png"));
        }
        void CheckPaint()
        {
            Require(View(a).VerifyTitleChromeForProbe(), "Single title paint/input geometry or action contrast differs.");
            Require(View(a).RainSurfaceValidForProbe, "Backdrop not transparent or content opacity changed.");
            var scene = Scene(a);
            Require(scene.Pool == 64 && scene.Nodes == 67 && scene.DecorativeOnly && scene.CloudVisible,
                "Missing cloud, unbounded particles or interactive scene.");
            Require(scene.Envelope.Top + scene.Envelope.Height + 8 <= Current().PostersById[a].Presentation.Metrics.ReservedTopDip - 20,
                "Cloud slot crowds content.");
            Require(Current().PostersById[a].Poster.ContentLayoutJson == contentA && Current().PostersById[b].Poster.ContentLayoutJson == contentB,
                "Decoration changed content authoring.");
            Require(Timers() <= 1, "More than one shared motion timer.");
        }
        void Collapse(string id, bool value)
        {
            var p = Current(); var target = p.PostersById[id];
            _spatialRuntime!.SetCollapsed(new("rain-probe-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed, p.Revision, id, target.Poster with { IsCollapsed = value })
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }, result =>
                    { if (!result.Succeeded) Finish(false, result.Error); });
        }
        void Initialize()
        {
            var current = Current(); Require(current.Posters.Count == 2, "Two-poster isolated fixture required.");
            a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId;
            contentA = current.PostersById[a].Poster.ContentLayoutJson; contentB = current.PostersById[b].Poster.ContentLayoutJson;
            center!.SelectSettingsTargetForProbe(a);
            double scale = center.Content.XamlRoot.RasterizationScale;
            center.PrepareUiCSampleForProbe(true, (int)(980 * scale), (int)(880 * scale));
            if (restart)
            {
                Require(File.ReadAllText(expectedPath) == Stored(), "Restart changed template or instance values.");
                center.RevealLampParametersForProbe();
                Step("restart-keeps-generic-parameters-and-independent-scenes", () =>
                { CheckPaint(); Require(Scene(a).VisibleDrops == 28 && Scene(b).VisibleDrops == 12, "Restart lost rain counts."); capture = Capture("restart"); });
                return;
            }
            center.TemplateForProbe("rain");
            Step("built-in-schema-controls", () => center.RevealLampParametersForProbe());
            Step("transparent-background-cloud-gap-and-shared-frames", () =>
            {
                CheckPaint(); var fields = center.ReadTemplateRecipeUiForProbe().Fields;
                Require(fields.Length == 8 && fields.All(f => f.Connected) && fields.Any(f => f.Binding == "cloud.wind") &&
                    fields.All(f => !f.Binding.StartsWith("lamp.")), "Actual controls still tied to Lamp schema.");
                references = View(a).RainReferencesForProbe(); geometry = View(a).ReadContentSurfaceForSmoke().GeometryFingerprint;
                otherRaw = Current().PostersById[b].Poster.LayoutParametersJson; frames = Scene(a).Frames;
                capture = Capture("default");
            });
            Step("shared-clock-advances", () =>
            {
                Require(_spatialRuntime!.SceneReducedMotion || Scene(a).Frames > frames, "Shared clock did not drive rain.");
                center.ChangeRecipeNumberForProbe("amount", 64, true);
            });
            Step("maximum-rain-preserves-item-objects", () =>
            {
                CheckPaint(); Require(Scene(a).VisibleDrops == 64 && View(a).RainGeometryUnchangedForProbe(references.Canvas, references.Cards) &&
                    geometry == View(a).ReadContentSurfaceForSmoke().GeometryFingerprint, "Rain changed content geometry/objects.");
                center.ChangeRecipeNumberForProbe("speed", 2, true);
            });
            Step("wind-bounded-and-independent", () => center.ChangeRecipeNumberForProbe("wind", 100, true));
            Step("rain-color-through-generated-control", () => center.ChangeRecipeColorForProbe("rainColor", "#C3E9D5"));
            Step("cloud-color-through-generated-control", () => center.ChangeRecipeColorForProbe("cloudColor", "#567A83"));
            Step("colors-and-other-poster-unchanged", () =>
            {
                CheckPaint(); Require(Scene(a).CloudColor == "#567A83" && Scene(a).RainColor == "#C3E9D5" &&
                    Current().PostersById[b].Poster.LayoutParametersJson == otherRaw, "Color binding or target isolation failed.");
                capture = Capture("custom"); center.ChangeRecipeNumberForProbe("amount", 0, true);
            });
            Step("zero-rain-keeps-cloud", () => { Require(Scene(a).VisibleDrops == 0 && Scene(a).CloudVisible, "Zero rain removed cloud."); center.ChangeRecipeNumberForProbe("wind", 0, true); });
            Step("disable-last-moving-feature", () => center.ChangeRecipeBooleanForProbe("lightning", false));
            Step("fully-static-scene-releases-shared-clock", () =>
            { Require(Timers() == 0 && !Scene(a).Running, "No-op scene retained clock."); center.ResetRecipeControlsForProbe(); });
            Step("reset-current-template-only", () =>
            {
                Require(Scene(a).VisibleDrops == 28 && Current().PostersById[b].Poster.LayoutParametersJson == otherRaw, "Reset crossed target boundary.");
                center.SelectSettingsTargetForProbe(b); center.TemplateForProbe("rain");
            });
            Step("second-generated-form", () => center.RevealLampParametersForProbe());
            Step("second-instance-own-value", () => center.ChangeRecipeNumberForProbe("amount", 12, true));
            Step("two-scenes-one-clock", () =>
            { Require(Scene(a).VisibleDrops == 28 && Scene(b).VisibleDrops == 12 && Timers() <= 1, "Instances or clock not isolated."); ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); });
            Step("global-pause", () =>
            { Require(Timers() == 0 && !Scene(a).Running && !Scene(b).Running, "Global pause failed."); ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); });
            Step("editing-pause-both", () =>
            { Require(_spatialRuntime!.SetContentEditing(a, true) is null && _spatialRuntime.SetContentEditing(b, true) is null, "Editing refused."); });
            Step("editing-has-no-motion-resources", () =>
            {
                Require(Timers() == 0 && !Scene(a).Running && !Scene(b).Running, "Editing kept motion.");
                Require(_spatialRuntime!.SetContentEditing(a, false) is null && _spatialRuntime.SetContentEditing(b, false) is null, "Editing exit refused.");
                Collapse(a, true);
            });
            Step("collapse-second", () => Collapse(b, true));
            Step("collapsed-no-motion-resources", () =>
            { Require(Timers() == 0 && !Scene(a).Running && !Scene(b).Running, "Collapsed retained clock.");
                Require(View(a).VerifyTitleChromeForProbe() && View(b).VerifyTitleChromeForProbe(), "Compact title geometry differs."); Collapse(a, false); });
            Step("expand-second", () => Collapse(b, false));
            Step("environment-suspend", () => _spatialRuntime!.Suspend());
            Step("environment-suspend-resources", () =>
            {
                Require(Timers() == 0, "Suspended retained clock.");
                // Resume must follow the real owner recovery/publication; Resume() alone is
                // only the final scheduler step and deliberately cannot revive stale input.
                Require(_spatialRuntime!.RecoverContentForSmoke(a) && _spatialRuntime.RecoverContentForSmoke(b), "Owner recovery refused.");
                _spatialRuntime.Resume();
            });
            for (int i = 0; i < 3; i++)
            {
                Step("switch-away-" + i, () => { center.SelectSettingsTargetForProbe(a); center.TemplateForProbe("basic-box"); });
                Step("switch-back-" + i, () => center.TemplateForProbe("rain"));
                Step("switch-retains-pool-and-values-" + i, () => { CheckPaint(); Require(Scene(a).VisibleDrops == 28, "Switch lost values."); });
            }
            Step("open-rain-copy-dialog", () => center.OpenTemplateDialogForProbe("copy"));
            Step("save-rain-copy", () =>
            { center.RenameTemplateCopyForProbe("雨云验证副本"); center.InvokeTemplateDialogForProbe("primary"); }, () => center.TemplateDialogReadyForProbe);
            Step("copy-does-not-replace-current", () =>
            {
                var copy = TemplateRecipeCatalog.All(Current().ControlCenter.Settings.Settings.TemplateCatalogJson).Single(r => r.Name == "雨云验证副本");
                Require(copy.Schema.RendererCapability == "rain.paint@1" && copy.Schema.Properties.Count == 8 && Current().PostersById[a].Poster.TemplateKey == "rain",
                    "Copy changed current or lost schema.");
                center.OpenTemplateDialogForProbe("import");
            }, () => center.TemplateDialogClosedForProbe);
            Step("prepare-different-rain-recipe", () =>
            {
                var basis = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "rain");
                var recipe = basis with { Schema = basis.Schema with { TemplateId = "rain-probe-drizzle", Name = "细雨验证",
                    Properties = [basis.Schema.Properties.Single(p => p.Binding == "rain.count") with
                    { Key = "drizzle", Label = "细雨滴数", DefaultValue = TemplateParameterValue.FromNumber(7) }] } };
                center.FillTemplateJsonForProbe(TemplateRecipeCodec.Serialize(recipe)); center.InvokeTemplateDialogForProbe("check");
            }, () => center.TemplateDialogReadyForProbe);
            Step("live-import-rain-recipe", () =>
            { Require(center.ReadTemplateRecipeUiForProbe().PrimaryEnabled, "Import validation rejected declared rain recipe."); center.InvokeTemplateDialogForProbe("primary"); });
            Step("show-new-rain-schema", () => center.RevealLampParametersForProbe(), () => center.TemplateDialogClosedForProbe);
            Step("new-schema-without-restart", () =>
            {
                var fields = center.ReadTemplateRecipeUiForProbe().Fields;
                Require(fields.Length == 1 && fields[0].Key == "drizzle" && fields[0].Binding == "rain.count" && fields[0].Connected &&
                    Scene(a).VisibleDrops == 7 && Scene(b).VisibleDrops == 12, "Hot imported schema/values did not bind independently.");
                capture = Capture("import"); center.ChangeRecipeNumberForProbe("drizzle", 19, true);
            });
            Step("new-schema-edit-restores-original-independently", () =>
            { Require(Scene(a).VisibleDrops == 19, "New field did not paint."); center.TemplateForProbe("rain"); });
            Step("save-restart-evidence", () =>
            {
                CheckPaint(); center.RevealLampParametersForProbe(); capture = Capture("final");
                Require(!File.Exists(expectedPath), "Refuse evidence overwrite."); File.WriteAllText(expectedPath, Stored());
            });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped || ticking) return; ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Probe timed out.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy || _autoClock.ElapsedMilliseconds < readyAt) return;
                center = _controlCenter!.SettingsWindowForContractProbe;
                if (center?.Content.XamlRoot is null) { _controlCenter.Show(); return; }
                if (capture is not null) { if (!capture.IsCompleted) return; capture.GetAwaiter().GetResult(); capture = null; }
                if (!initialized) { Initialize(); initialized = true; readyAt = _autoClock.ElapsedMilliseconds + 600; return; }
                if (operations.Count == 0) { Finish(true, null); return; }
                var operation = operations.Peek(); step = operation.Name;
                if (operation.Ready is not null && !operation.Ready()) return;
                operations.Dequeue(); operation.Action();
                observations.Add(new { name = step, revision = Current().Revision, a = View(a).ReadRainForProbe(),
                    b = View(b).ReadRainForProbe(), timers = Timers(), surface = View(a).RainSurfaceForProbe() });
                readyAt = _autoClock.ElapsedMilliseconds + 400;
            }
            catch (Exception e) { Finish(false, e.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
