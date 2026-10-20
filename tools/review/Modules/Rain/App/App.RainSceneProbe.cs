using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal RainSceneObservation? ReadRainForProbe() => (_sceneInstance as RainSceneInstance)?.Observe() is { } value
        ? value with { TitleNodes=_titleCloudGroup is null ? 0 : 1+_titleCloudGroup.Children.Count } : null;
    internal object RainSurfaceForProbe() => new { backdrop = BackdropColor().A, valid = VerifyWindowBackdrop(),
        metaOpacity = _metaPanel?.Opacity, contentTop = ContentTopDip, rootOpacity = _window?.Content.Opacity,
        module = ReadSceneModuleForProbe(), surface = inputRouter.ReadSurface(inputSession) is null ? null : ReadContentSurfaceForSmoke() };
    internal bool RainGeometryUnchangedForProbe(object? canvas, Dictionary<string, object> cards) =>
        ReferenceEquals(canvas, Presenter.Surface) && cards.Count == Presenter.Cards.Count &&
        cards.All(p => Presenter.Cards.TryGetValue(p.Key, out var card) && ReferenceEquals(card, p.Value));
    internal (object?, Dictionary<string, object>) RainReferencesForProbe() =>
        (Presenter.Surface, Presenter.Cards.ToDictionary(p => p.Key, p => (object)p.Value));
    internal bool RainSurfaceValidForProbe => BackdropColor().A == 0 && VerifyWindowBackdrop() &&
        _metaPanel?.Opacity == 0 && _window?.Content.Opacity == 1 && Presenter.Cards.Values.All(v => v.Opacity == 1);
    internal int CloudAlphaForProbe { get; private set; }
    internal bool CloudSizeAppliedForProbe(double size) => _titlePose.Scale==size && Math.Abs(_titleTransform.ScaleX-size)<1e-6 &&
        Math.Abs(_titleTransform.ScaleY-size)<1e-6 && _inputIsland?.VerifyTitleRegionForProbe((int)Math.Ceiling(84*(_monitor?.ScaleY ?? 1)))==true;

    internal async Task ProbeRainTitlePixelsAsync(string directory, bool lightning, bool wind = true)
    {
        if (_sceneInstance is not RainSceneInstance scene || _window?.Content is not FrameworkElement poster || _titleCanvas is null || _titleCloud is null ||
            _titleCanvas.Children.IndexOf(_titleCloudGroup!) != 0 || _titleCloudGroup!.Children.IndexOf(_titleCloud)!=0 || _titleCanvas.Children.IndexOf(_titleName!) < 1)
            throw new InvalidOperationException("Single moving cloud must remain below stable labels.");
        var refs = RainReferencesForProbe();
        spatial.SetSceneMotionDemand(this, false, inputRouter);
        try
        {
            async Task<(byte[] Pixels, int Width, int Height)> Pixels(string tag, bool cloudOnly = false)
            {
                // A transparent Canvas capture trims to its ink bounds. The poster root has
                // a fixed transparent background, so both samples use the SAME coordinates.
                double opacity=scene.Root.Opacity;
                try
                {
                    // Rain starts at y=56 and is allowed at the bottom of the
                    // title budget. Isolate host cloud ink for edge assertions.
                    if(cloudOnly) scene.Root.Opacity=0;
                    var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(poster);
                    var buffer = await bitmap.GetPixelsAsync();
                    await CapturePosterXamlForProbeAsync(Path.Combine(directory, "rain-title-" + tag + ".png"));
                    return (buffer.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
                }
                finally { scene.Root.Opacity=opacity; }
            }
            string prefix = (!wind ? "zero-wind-" : lightning ? "enabled-" : "disabled-")+
                scene.Observe().CloudSize.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-";
            async Task ApplyPose(double time)
            {
                var expected=scene.TitleSampleForProbe(time).Fit(poster.ActualWidth);
                for(int attempt=0;attempt<50;attempt++)
                {
                    TryPaintTitleMotion(expected);
                    if(_titlePose==expected && _inputIsland?.CanAnimateTitle==true) return;
                    await Task.Delay(40);
                }
                throw new InvalidOperationException($"Title pose not ready; guards retained. requested={expected}, actual={_titlePose}, nativeReady={_inputIsland?.CanAnimateTitle}.");
            }
            await ApplyPose(0);
            if(!CloudSizeAppliedForProbe(scene.Observe().CloudSize))
                throw new InvalidOperationException($"Cloud size failed to bind shared paint/input pose: requested={scene.Observe().CloudSize:R}, pose={_titlePose.Scale:R}, paint={_titleTransform.ScaleX:R}/{_titleTransform.ScaleY:R}.");
            var before = await Pixels(prefix + "start");
            CloudAlphaForProbe=0;
            for(int y=0;y<Math.Min(before.Height,(int)(60*before.Height/poster.ActualHeight));y++)
                for(int x=0;x<before.Width;x++)
                    if(before.Pixels[(y*before.Width+x)*4+3]>200) CloudAlphaForProbe++;
            var initial = inputRouter.ReadSurface(inputSession)!;
            await ApplyPose(9.5);
            var drift = await Pixels(prefix + "wind-peak");
            var moved = inputRouter.ReadSurface(inputSession)!;
            bool nativeRegionMatches = _inputIsland!.VerifyTitleRegionForProbe((int)Math.Ceiling(84 * _monitor!.ScaleY));
            bool gripsChanged = !initial.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.TitleBar)
                .SequenceEqual(moved.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.TitleBar));
            bool othersStable = initial.Layout.Regions.Where(r => r.Kind != PosterInputTargetKind.TitleBar)
                .SequenceEqual(moved.Layout.Regions.Where(r => r.Kind != PosterInputTargetKind.TitleBar));
            // Hold an original router lease; simulated capture is not OS mouse input.
            var button = moved.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.CollapseButton);
            var lease = inputRouter.Begin(new(inputSession, moved.Layout.Revision, moved.Frame, button));
            if (lease is null) throw new InvalidOperationException("Cannot establish hold check.");
            var held = _titlePose;
            try
            {
                TryPaintTitleMotion(scene.TitleSampleForProbe(14.25));
                TryPaintTitleMotion(scene.TitleSampleForProbe(14.25) with { Scale=held.Scale==.6 ? 1.1 : .6 });
                if (_titlePose != held || inputRouter.ReadSurface(inputSession)!.Layout.Revision != moved.Layout.Revision ||
                    !inputRouter.CanInvoke(lease, moved.Frame.X + button.Bounds.X + 2, moved.Frame.Y + button.Bounds.Y + 2))
                    throw new InvalidOperationException("Cloud motion invalidated a held action.");
            }
            finally { inputRouter.End(lease); }
            int edgeInk=0;
            foreach(double time in new[]{0.0,4.75,9.5,14.25,19.0})
            {
                await ApplyPose(time);
                var sample=await Pixels(prefix+"cycle-"+time.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),true);
                double sy=sample.Height/poster.ActualHeight, sx=sample.Width/poster.ActualWidth;
                for(int y=0;y<Math.Min(sample.Height,(int)(60*sy));y++)
                    for(int x=0;x<sample.Width;x++)
                        if((x<sx || x>sample.Width-1-sx || y<sy || y>=59*sy) && sample.Pixels[(y*sample.Width+x)*4+3]>20) edgeInk++;
                if(!_inputIsland!.VerifyTitleRegionForProbe((int)Math.Ceiling(84*_monitor!.ScaleY)))
                    throw new InvalidOperationException("Wind cycle native hit region diverged.");
            }
            if(edgeInk!=0) throw new InvalidOperationException("Wind cycle ink reaches title/frame clip edge: "+edgeInk);
            scene.PaintLightningSampleForProbe(0);
            var noFlash = await Pixels(prefix + "lightning-off");
            scene.PaintLightningSampleForProbe(17.35);
            var flash = await Pixels(prefix + "lightning-peak");
            scene.PaintLightningSampleForProbe(17.35,73);
            var relocated=await Pixels(prefix+"lightning-relocated");
            int randomPixels=Enumerable.Range(0,flash.Pixels.Length).Count(i=>Math.Abs(flash.Pixels[i]-relocated.Pixels[i])>3);
            if(lightning ? randomPixels<100 : randomPixels!=0)
                throw new InvalidOperationException("Lightning event relocation/disabled pixels differ unexpectedly.");
            int changed = 0, silhouette = 0;
            if (before.Pixels.Length != drift.Pixels.Length || before.Height <= 0)
                throw new InvalidOperationException($"Invalid title capture: {before.Width}x{before.Height} / {drift.Width}x{drift.Height}, actual={_titleCanvas.ActualWidth}x{_titleCanvas.ActualHeight}.");
            if (flash.Width != noFlash.Width || flash.Height != noFlash.Height || flash.Width != before.Width || flash.Height != before.Height)
                throw new InvalidOperationException("Poster capture coordinates changed between samples.");
            double scale = before.Height / poster.ActualHeight;
            for (int y = 0; y < before.Height; y++)
                for (int x = 0; x < before.Width; x++)
                {
                    int p = (y * before.Width + x) * 4;
                    if (y < 60 * scale && Math.Abs(before.Pixels[p] - drift.Pixels[p]) > 3) changed++;
                    if (y < _titleCanvas.ActualHeight * scale && Math.Abs(before.Pixels[p + 3] - drift.Pixels[p + 3]) > 20) silhouette++;
                }
            var bands = new int[3];
            double top = scene.Observe().Envelope.Top + scene.Observe().Envelope.Height - 5;
            double bottom = scene.Observe().Envelope.Bottom;
            for (int y = 0; y < flash.Height; y++)
                for (int x = 0; x < flash.Width; x++)
                {
                    double t = (y / scale - top) / (bottom - top);
                    int p = (y * flash.Width + x) * 4;
                    if (t >= 0 && t < 1 && Enumerable.Range(0, 4).Any(c => Math.Abs(flash.Pixels[p + c] - noFlash.Pixels[p + c]) > 3))
                        bands[Math.Min(2, (int)(t * 3))]++;
                }
            if ((wind ? changed < 50 || silhouette < 50 || !gripsChanged : changed != 0 || silhouette != 0 || gripsChanged) ||
                (lightning ? bands.Any(n => n < 20) : bands.Any(n => n != 0)) || !nativeRegionMatches || !othersStable || !VerifyTitleChromeForProbe() ||
                !RainGeometryUnchangedForProbe(refs.Item1, refs.Item2))
                throw new InvalidOperationException($"Weather pixels/geometry failed: wind={changed}, silhouette={silhouette}, grips={gripsChanged}, bands={string.Join(',', bands)}, enabled={lightning}.");
            log.Write("info", "rain.title_pixels_observed", new { changedWindPixels = changed, silhouette, gripsChanged, nativeRegionMatches, othersStable, edgeInk, randomPixels, cloudAlphaPixels=CloudAlphaForProbe, paintScale=_titleTransform.ScaleX, lightningBands = bands, lightning, wind,
                scene = scene.Observe(), deterministicRender = true, realTimeCadenceCertified = false });
        }
        finally { RefreshScene(); }
    }
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
        int smallCloudPixels=0;
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
            Require(scene.Pool == 64 && scene.Nodes == 70 && scene.TitleNodes==4 && scene.DecorativeOnly && scene.CloudVisible && scene.TitleWeatherVisible,
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
                { CheckPaint(); Require(center.VerifyTitleVisibilityControlForProbe(false) && View(a).VerifyTitleVisibilityForProbe(false) &&
                    View(b).VerifyTitleVisibilityForProbe(true), "Restart lost title visibility or peer isolation.");
                    Require(Scene(a).VisibleDrops == 28 && Scene(b).VisibleDrops == 12 && Scene(a).LightningInterval==41 && Scene(b).LightningInterval==23 &&
                    Scene(a).CloudSize==.75 && Scene(b).CloudSize==1, "Restart lost rain values."); capture = Capture("restart"); });
                return;
            }
            center.TemplateForProbe("rain");
            Step("built-in-schema-controls", () => center.RevealLampParametersForProbe());
            Step("transparent-background-cloud-gap-and-shared-frames", () =>
            {
                CheckPaint(); var fields = center.ReadTemplateRecipeUiForProbe().Fields;
                Require(fields.Length == 10 && fields.All(f => f.Connected) && fields.Any(f => f.Binding == "cloud.wind") && fields.Any(f=>f.Binding=="cloud.size") &&
                    fields.Any(f => f.Binding == "rain.lightningInterval") &&
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
            Step("small-cloud-control",()=>center.ChangeRecipeNumberForProbe("cloudSize",.6,true));
            Step("small-cloud-pixels",()=> { Require(Scene(a).CloudSize==.6,"Small size did not bind."); capture=View(a).ProbeRainTitlePixelsAsync(root,true); });
            Step("large-cloud-control",()=> { smallCloudPixels=View(a).CloudAlphaForProbe; center.ChangeRecipeNumberForProbe("cloudSize",1.1,true); });
            Step("large-cloud-pixels",()=> { Require(Scene(a).CloudSize==1.1,"Large size did not bind."); capture=View(a).ProbeRainTitlePixelsAsync(root,true); });
            Step("cloud-size-paint-input-and-target-isolation",()=>
            {
                Require(View(a).CloudAlphaForProbe>smallCloudPixels*1.7 && Current().PostersById[b].Poster.LayoutParametersJson==otherRaw &&
                    View(a).RainGeometryUnchangedForProbe(references.Canvas,references.Cards) && geometry==View(a).ReadContentSurfaceForSmoke().GeometryFingerprint,
                    "Size did not change visible cloud or crossed content/target boundaries.");
                center.ChangeRecipeNumberForProbe("cloudSize",1,true);
            });
            Step("lightning-interval-control",()=>center.ChangeRecipeNumberForProbe("lightningInterval",8,true));
            Step("lightning-interval-target-isolation",()=>
            { Require(Scene(a).LightningInterval==8 && Current().PostersById[b].Poster.LayoutParametersJson==otherRaw,"Interval control crossed target or did not bind."); });
            Step("visible-title-wind-and-lightning", () => capture = View(a).ProbeRainTitlePixelsAsync(root, true));
            Step("disable-title-lightning", () => center.ChangeRecipeBooleanForProbe("lightning", false));
            Step("disabled-lightning-has-no-pixels", () => capture = View(a).ProbeRainTitlePixelsAsync(root, false));
            Step("restore-title-lightning", () => center.ChangeRecipeBooleanForProbe("lightning", true));
            Step("rain-color-through-generated-control", () => center.ChangeRecipeColorForProbe("rainColor", "#C3E9D5"));
            Step("cloud-color-through-generated-control", () => center.ChangeRecipeColorForProbe("cloudColor", "#567A83"));
            Step("colors-and-other-poster-unchanged", () =>
            {
                CheckPaint(); Require(Scene(a).CloudColor == "#567A83" && Scene(a).RainColor == "#C3E9D5" &&
                    Current().PostersById[b].Poster.LayoutParametersJson == otherRaw, "Color binding or target isolation failed.");
                capture = Capture("custom"); center.ChangeRecipeNumberForProbe("amount", 0, true);
            });
            Step("zero-rain-keeps-cloud", () => { Require(Scene(a).VisibleDrops == 0 && Scene(a).CloudVisible, "Zero rain removed cloud."); center.ChangeRecipeNumberForProbe("wind", 0, true); });
            Step("zero-wind-no-title-drift", () => capture = View(a).ProbeRainTitlePixelsAsync(root, true, false));
            Step("disable-last-moving-feature", () => center.ChangeRecipeBooleanForProbe("lightning", false));
            Step("size-without-animation-clock",()=> { Require(Timers()==0,"Static size setup retained clock."); center.ChangeRecipeNumberForProbe("cloudSize",.8,true); });
            Step("static-size-binds-without-clock",()=> { Require(Timers()==0 && View(a).CloudSizeAppliedForProbe(.8),"Size needs animation clock or mismatched input."); });
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
            { Require(Timers() == 0 && !Scene(a).Running && !Scene(b).Running && Scene(a).LightningOpacity == 0 && Scene(b).LightningOpacity == 0, "Global pause failed."); ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); });
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
                Require(copy.Schema.RendererCapability == "rain.paint@1" && copy.Schema.Properties.Count == 10 && Current().PostersById[a].Poster.TemplateKey == "rain",
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
            Step("persist-lightning-interval",()=> { center.RevealLampParametersForProbe(); center.ChangeRecipeNumberForProbe("lightningInterval",41,true); });
            Step("persist-cloud-size",()=>center.ChangeRecipeNumberForProbe("cloudSize",.75,true));
            Step("hide-title-control", () =>
            {
                references = View(a).RainReferencesForProbe(); geometry = View(a).ReadContentSurfaceForSmoke().GeometryFingerprint;
                otherRaw = Current().PostersById[b].Poster.LayoutParametersJson;
                center.SetTitleVisibilityForProbe(false);
            });
            Step("hidden-title-keeps-cloud-input-and-peer", () =>
            {
                CheckPaint();
                Require(center.VerifyTitleVisibilityControlForProbe(false) && View(a).VerifyTitleVisibilityForProbe(false) &&
                    View(b).VerifyTitleVisibilityForProbe(true) && Current().PostersById[b].Poster.LayoutParametersJson == otherRaw &&
                    View(a).RainGeometryUnchangedForProbe(references.Canvas,references.Cards) &&
                    geometry == View(a).ReadContentSurfaceForSmoke().GeometryFingerprint, "Visibility changed cloud/content/peer or did not paint.");
                capture = Capture("title-hidden");
            });
            Step("hidden-title-switch-basic", () => center.TemplateForProbe("basic-box"));
            Step("hidden-title-basic-keeps-name-and-controls", () =>
            { Require(View(a).VerifyTitleVisibilityForProbe(false), "Basic template did not retain hidden title."); center.TemplateForProbe("rain"); });
            Step("hidden-title-collapse", () => Collapse(a, true));
            Step("hidden-title-compact-keeps-controls", () =>
            { Require(View(a).VerifyTitleVisibilityForProbe(false), "Compact title lost control visibility."); Collapse(a, false); });
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
