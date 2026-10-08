using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

internal sealed record LampNativeReadback(LampSceneObservation Scene, ContentSurfaceProbe Surface,
    LampTemplateParameters Parameters, bool BackdropValid, string BackdropColor, double RootOpacity,
    bool ContentOpaque, [property: JsonIgnore] object? CanvasReference,
    [property: JsonIgnore] IReadOnlyDictionary<string, object> CardReferences);

internal sealed partial class PosterVisualWindow
{
    // Called only by the isolated probe below; not an import/module execution entry.
    private sealed class FailingSceneForProbe(IPosterSceneInstance inner) : IPosterSceneInstance
    {
        public FrameworkElement Root => inner.Root;
        public bool WantsMotion => inner.WantsMotion;
        public int Releases { get; private set; }
        public void Update(SceneRenderParameters parameters, SceneFrameContext frame) =>
            throw new InvalidOperationException("isolated-scene-frame-fault");
        public void PaintCard(SceneCardPaint paint) => inner.PaintCard(paint);
        public void StopMotion() => inner.StopMotion();
        public void Dispose() { Releases++; inner.Dispose(); }
    }
    internal bool FailSceneForProbe()
    {
        if (_sceneInstance is null) return false;
        var failing = new FailingSceneForProbe(_sceneInstance);
        _sceneInstance = failing;
        PaintSceneMotion(0, null, false);
        RefreshScene(); RefreshScene();
        return _sceneInstance is null && failing.Releases == 1 &&
            _sceneFailure.Failure?.Message == "isolated-scene-frame-fault";
    }

    internal LampNativeReadback ReadLampNativeForProbe()
    {
        var color = _windowBackdrop?.TintColor;
        return new(ReadLampSceneForProbe(), ReadContentSurfaceForSmoke(), _presentation.TemplateParameters, VerifyWindowBackdrop(),
            color is { } c ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}" : "", _window?.Content.Opacity ?? 0,
            _contentCards.Count > 0 && _contentCards.Values.All(cell => cell.Opacity == 1 &&
                cell is Grid grid && grid.Children.All(child => child.Opacity == 1)), _contentCanvas,
            _contentCards.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal));
    }

    internal LampSceneObservation ReadLampSceneForProbe()
    {
        var cards = _contentCards.Values.OfType<Grid>().Select(cell =>
            cell.Children.OfType<Border>().FirstOrDefault()?.Background)
            .OfType<Microsoft.UI.Xaml.Media.RadialGradientBrush>();
        var observation = _sceneInstance is LampSceneInstance lamp ? lamp.ReadObservation(cards) :
            new LampSceneObservation(false, false, false, true, true, 0, 0, 0, 0, 0, 0, "", 0, 0, "", "");
        return observation with
        {
            ReducedMotion = spatial.SceneReducedMotion, GlobalMotionPaused = spatial.SceneGlobalMotionPaused
        };
    }

    // Deliberately a production paint-handler driver, not native pointer evidence. The normal
    // shared sampler remains the only production source of physical pointer observations.
    internal LampNativeReadback PaintLampMotionForProbe(double seconds, LampScenePoint? point)
    {
        PointerScreenPoint? physical = point is { } p ? new(
            _bounds.X + (int)Math.Round(p.X * (_monitor?.ScaleX ?? 1)),
            _bounds.Y + (int)Math.Round(p.Y * (_monitor?.ScaleY ?? 1))) : null;
        PaintSceneMotion(seconds, physical, !spatial.SceneReducedMotion);
        return ReadLampNativeForProbe();
    }

    internal void VerifyHeldLampFollowForProbe()
    {
        // Same synchronous handler turn: no native cursor injection or timer race.
        var before = ReadCompositionForProbe();
        void Follow(double x)
        {
            var size = ReadLampSceneForProbe();
            var point = new LampScenePoint(size.Width * x, size.Height * .55);
            var painted = PaintLampMotionForProbe(1, point).Scene;
            RefreshScene(); // the old implementation reset to the default light here
            var refreshed = ReadLampSceneForProbe();
            if (!painted.FollowingPointer || !refreshed.FollowingPointer ||
                painted.LightX != refreshed.LightX || painted.LightY != refreshed.LightY ||
                Math.Abs(refreshed.LightX - point.X) > 2)
                throw new InvalidOperationException("A repaint or held gesture reset the lamp pointer.");
        }
        Follow(.22);
        try
        {
            _ = BeginIndependentItemDragForProbe(); // original published-input protocol; never release/commit
            if (!HasContentGesture) throw new InvalidOperationException("Expected held Item gesture.");
            Follow(.3); Follow(.7); Follow(.2);
        }
        finally { CancelContentGesture("lamp-follow-probe"); }
        Follow(.6);
        if (HasContentGesture || ReadCompositionForProbe().StoredJson != before.StoredJson ||
            ReadCompositionForProbe().Revision != before.Revision)
            throw new InvalidOperationException("Read-only lamp follow wrote or retained a gesture.");
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal bool FailSceneForProbe(string posterId) => _windows[posterId].FailSceneForProbe();
    internal LampNativeReadback ReadLampNativeForProbe(string posterId) => _windows[posterId].ReadLampNativeForProbe();
    internal LampNativeReadback PaintLampMotionForProbe(string posterId, double seconds, LampScenePoint? point) =>
        _windows[posterId].PaintLampMotionForProbe(seconds, point);
}

public sealed partial class ControlCenterWindow
{
    internal async Task CaptureLampPickerForProbeAsync(string path)
    {
        var content = _lampColorFlyout?.Content ?? throw new InvalidOperationException("Color picker popup is closed.");
        if (content.XamlRoot is null) throw new InvalidOperationException("Color picker popup is not connected.");
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(content);
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) throw new InvalidOperationException("Color picker popup has no visible size.");
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}

public partial class App
{
    // Root entry point must apply the same exclusive isolated-profile guard as the other probes.
    // The script creates two expanded DEMO posters. Only this isolated database is written.
    private void RunLampParameterProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.lamp-parameters", "isolated-native-parameter-bindings");
        var observations = new List<object>(); var captures = new List<string>();
        var aValue = new LampTemplateParameters("#E8B66A", 32, 43, "#20372D", "#4C5B43", 24,
            MotionEnabled: true, FollowPointer: true, IdleSway: 60, MotionSpeed: 22);
        var bValue = new LampTemplateParameters("#98CCFF", 75, 88, "#15263B", "#334E68", 67,
            MotionEnabled: true, FollowPointer: false, IdleSway: 80, MotionSpeed: 75);
        var zeroValue = aValue with { Softness = 0, Intensity = 0, BackgroundOpacity = 0 };
        string a = "", b = "";
        string? aContent = null, bContent = null;
        PosterDefinition? initialB = null;
        LampNativeReadback? baseline = null, baselineB = null;
        long revision = -1, readyAfter = 0, deadline = _autoClock.ElapsedMilliseconds + 80000;
        long motionFrames = 0;
        bool pointerHandlerObserved = false, idleHandlerObserved = false, timerAdvanceObserved = false;
        string? postureError = null;
        int step = 0; bool ticking = false, stopped = false; Task? capture = null;
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop();
            _log!.Write(passed ? "info" : "error", "lamp.parameters_observed", new
            {
                passed, error, step, revision, observations, captures,
                inputDriver = "actual-control-dependency-properties-and-shared-production-handlers",
                cancellationDriver = "shared-CancelSettingsPreview-not-routed-native-Escape",
                nativeMouse = false, nativeKeyboard = false, nativeWheel = false,
                desktopPixelVisualCertified = false, captureIncludesSystemBackdrop = false,
                pointerFollowingImplemented = true, pointerHandlerObserved, idleHandlerObserved, timerAdvanceObserved,
                motionDriver = "shared-production-paint-handler-and-real-dispatcher-timer; not-native-pointer-injection",
                processRestartTested = false,
                uiCProductAccepted = false, milestoneAccepted = false
            });
            ExitApplication();
        }
        void Next(int next, int delay = 400) { step = next; readyAfter = _autoClock.ElapsedMilliseconds + delay; }
        void SameGeometry(LampNativeReadback before, LampNativeReadback after, bool sameObjects)
        {
            Require(after.Surface.CanvasMatches && after.Surface.InputMatches && after.Surface.Clipped && after.Surface.Items > 0,
                "Native content canvas/input/clip did not agree.");
            Require(before.Surface.Handle == after.Surface.Handle && before.Surface.GeometryFingerprint == after.Surface.GeometryFingerprint &&
                before.Surface.HitFingerprint == after.Surface.HitFingerprint && before.Surface.ViewWidth == after.Surface.ViewWidth &&
                before.Surface.ViewHeight == after.Surface.ViewHeight && before.Surface.ScrollX == after.Surface.ScrollX &&
                before.Surface.ScrollY == after.Surface.ScrollY, "Lamp paint changed native geometry, item identity, hit regions, or scroll.");
            if (sameObjects)
                Require(ReferenceEquals(before.CanvasReference, after.CanvasReference) && before.CardReferences.Count == after.CardReferences.Count &&
                    before.CardReferences.All(pair => after.CardReferences.TryGetValue(pair.Key, out var card) && ReferenceEquals(pair.Value, card)),
                    "Lamp paint replaced the production content canvas or cards.");
        }
        LampNativeReadback Bound(string id, LampTemplateParameters expected)
        {
            var read = _spatialRuntime!.ReadLampNativeForProbe(id); var scene = read.Scene;
            Require(read.Parameters == expected && read.BackdropValid && read.RootOpacity == 1 && read.ContentOpaque,
                "Lamp state/backdrop or content opacity is incorrect.");
            Require(scene.Active && scene.Connected && scene.Visible && scene.DecorativeOnly &&
                scene.SceneChildren == 3 && scene.Width > 0 && scene.Height > 0 && scene.PaintedCards == read.Surface.Controls,
                "Native lamp layer/card brushes are missing, hidden, interactive, or unbounded.");
            Require(Math.Abs(scene.PoolDiameter - (140 + expected.Softness * 3)) < .001 &&
                Math.Abs(scene.PoolOpacity - expected.Intensity / 100) < .0001 &&
                Math.Abs(scene.ConeOpacity - expected.Intensity / 100) < .0001 &&
                Math.Abs(scene.CardLightRadius - (.5 + expected.Softness / 100)) < .0001,
                "Native light radius, strength, or card softness binding is incorrect.");
            Require(scene.PoolColor == "#26" + expected.LightColor[1..] && scene.CardBaseColor == "#FF" + expected.Card[1..] &&
                read.BackdropColor == "#" + ((byte)Math.Round(expected.BackgroundOpacity * 255 / 100)).ToString("X2") + expected.Background[1..],
                "Native lamp/card/background color or background-only alpha binding is incorrect.");
            return read;
        }
        int MotionTimers() => _ledger!.ActiveResources.Count(resource => resource == "timer.scene-motion");
        void Collapse(string id)
        {
            var p = _stateCoordinator!.Refresh(); var target = p.PostersById[id];
            _spatialRuntime!.SetCollapsed(new("lamp-collapse-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed, p.Revision, id, target.Poster with { IsCollapsed = true })
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }, result =>
                    { if (!result.Succeeded) postureError = result.Error ?? "Lamp collapse failed."; });
        }
        void Observe(string name, ControlCenterWindow window, LampNativeReadback read, DesktopUpProjection current)
        {
            Require(current.PostersById[a].Poster.ContentLayoutJson == aContent && current.PostersById[b].Poster.ContentLayoutJson == bContent,
                "Parameter/template edits changed persisted content authoring.");
            observations.Add(new { name, current.Revision, controls = window.ReadLampParameterControlsForProbe(), native = read });
        }
        async Task Capture(ControlCenterWindow window, string id, string name)
        {
            string center = Path.Combine(dataRoot, "lamp-" + name + "-center.png");
            string poster = Path.Combine(dataRoot, "lamp-" + name + "-poster-xaml.png");
            await window.CaptureSettingsForProbeAsync(center); captures.Add(center);
            await _spatialRuntime!.CapturePosterXamlForProbeAsync(id, poster); captures.Add(poster);
            if (name == "color-picker")
            {
                string popup = Path.Combine(dataRoot, "lamp-color-picker-popup.png");
                await window.CaptureLampPickerForProbeAsync(popup); captures.Add(popup);
            }
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking || stopped) return;
            ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Lamp parameter observation timed out.");
                Require(postureError is null, postureError ?? "");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy || _autoClock.ElapsedMilliseconds < readyAfter) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                if (step != 0 && step != 17 && step != 18 && window?.Content.XamlRoot is null) return;
                switch (step)
                {
                    case 0:
                        Require(current.Posters.Count == 2 && current.Posters.All(p => !p.Poster.IsCollapsed && p.Poster.IsVisible),
                            "Lamp probe requires two visible expanded isolated DEMO posters.");
                        a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId;
                        aContent = current.PostersById[a].Poster.ContentLayoutJson; bContent = current.PostersById[b].Poster.ContentLayoutJson;
                        initialB = current.PostersById[b].Poster;
                        _controlCenter.Show(); Next(1); break;
                    case 1:
                        window!.SelectSettingsTargetForProbe(a); revision = current.Revision;
                        window.TemplateForProbe("lamp"); Next(2); break;
                    case 2:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Poster.TemplateKey == "lamp", "Initial template selection did not commit once.");
                        window!.PrepareUiCSampleForProbe(true, (int)Math.Ceiling(980 * window.Content.XamlRoot.RasterizationScale),
                            (int)Math.Ceiling(1040 * window.Content.XamlRoot.RasterizationScale));
                        window.RevealLampParametersForProbe(); Next(3, 600); break;
                    case 3:
                        baseline = Bound(a, LampTemplatePolicy.Defaults); revision = current.Revision;
                        Require(current.PostersById[b].Poster == initialB, "Selecting A's lamp changed B.");
                        var controls = window!.ReadLampParameterControlsForProbe();
                        Require(controls.Visible && controls.Enabled && controls.ControlCount == 10 && controls.PosterId == a &&
                            controls.VisualColorCount == 3 && controls.HasTemplateReset, "Ten real lamp controls, swatches or reset are unavailable.");
                        var motionBindings = TemplateParameterSchemas.ForTemplate("lamp")!.Properties.Select(p => p.Binding).ToHashSet();
                        Require(new[] { "scene.motion", "lamp.follow", "lamp.sway", "lamp.speed" }.All(motionBindings.Contains),
                            "The four declared motion bindings are missing.");
                        window.PreviewLampControlsForProbe(aValue); Next(4); break;
                    case 4:
                        Require(current.Revision == revision && window!.CommonSettingsForProbe.Preview?.TemplateValues == LegacyLampValues.ToValues(aValue) && window.HasPosterDraft(a),
                            "Held lamp preview wrote state or did not own its target.");
                        var preview = Bound(a, aValue); SameGeometry(baseline!, preview, true);
                        Require(current.PostersById[b].Poster == initialB, "A's preview leaked into B.");
                        Observe("ten-control-preview-without-write", window!, preview, current);
                        capture = Capture(window!, a, "preview"); Next(5, 0); break;
                    case 5:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult(); window!.FinishSettingsForProbe(); Next(6); break;
                    case 6:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.Lamp == aValue &&
                            !window!.HasPosterDraft(a), "Ten fields were not saved as one completed revision.");
                        var saved = Bound(a, aValue); SameGeometry(baseline!, saved, true);
                        Observe("ten-fields-single-revision-save", window!, saved, current);
                        revision = current.Revision; window!.PreviewLampControlsForProbe(zeroValue); Next(7); break;
                    case 7:
                        Require(current.Revision == revision, "Zero-light/transparent preview committed early.");
                        var zero = Bound(a, zeroValue); SameGeometry(baseline!, zero, true);
                        Observe("zero-light-transparent-background-preview", window!, zero, current);
                        capture = Capture(window!, a, "transparent"); Next(8, 0); break;
                    case 8:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult(); window!.CancelLampDraftForProbe(); Next(9); break;
                    case 9:
                        Require(current.Revision == revision && window!.CommonSettingsForProbe.Preview is null && !window.HasPosterDraft(a),
                            "Shared cancellation wrote state or retained ownership.");
                        var cancelled = Bound(a, aValue); SameGeometry(baseline!, cancelled, true);
                        Observe("shared-cancel-restores-without-write", window!, cancelled, current);
                        // Queue a different valid preview, then replace it with an invalid color in
                        // the same dispatcher turn. The queued callback must not render that invalid draft.
                        window!.PreviewLampControlsForProbe(aValue with { Intensity = 91 });
                        window.PreviewInvalidLampColorForProbe(); window.SpacingForProbe(1.4); window.FinishSettingsForProbe(); Next(10, 600); break;
                    case 10:
                        observations.Add(new { name = "invalid-draft-readback", current.Revision,
                            ownsTarget = window!.HasPosterDraft(a), preview = window.CommonSettingsForProbe.Preview,
                            window.CommonSettingsForProbe.Error, controls = window.ReadLampParameterControlsForProbe(),
                            stored = current.PostersById[a].Presentation.Parameters.Lamp });
                        Require(current.Revision == revision && window!.HasPosterDraft(a) &&
                            window.CommonSettingsForProbe.Preview is { Setting: PosterSetting.TemplateParameters, TemplateValues: { } invalidValues } && invalidValues.Color("lamp.color", "") == "#GGGGGG" &&
                            window.CommonSettingsForProbe.Error is not null && window.ReadLampParameterControlsForProbe().Values.LightColor == "#GGGGGG",
                            "Invalid lamp draft was lost, committed, or replaced by the spacing entry.");
                        var retained = Bound(a, aValue); SameGeometry(baseline!, retained, true);
                        Observe("queued-valid-then-invalid-retains-last-good-and-rejects-spacing-entry", window!, retained, current);
                        window!.CancelLampDraftForProbe(); window.TemplateForProbe("basic-box"); Next(11); break;
                    case 11:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Poster.TemplateKey == "basic-box" &&
                            current.PostersById[a].Presentation.Parameters.Lamp == aValue && !_spatialRuntime.ReadLampNativeForProbe(a).Scene.Active,
                            "Switching away discarded parameters or retained a lamp scene.");
                        revision = current.Revision; window!.TemplateForProbe("lamp"); Next(12); break;
                    case 12:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.Lamp == aValue,
                            "Switching back did not restore saved lamp parameters.");
                        var restored = Bound(a, aValue); SameGeometry(baseline!, restored, false);
                        Observe("template-away-back-restores-parameters", window!, restored, current);
                        revision = current.Revision; window!.SelectSettingsTargetForProbe(b); window.TemplateForProbe("lamp"); Next(13); break;
                    case 13:
                        Require(current.Revision == revision + 1 && current.PostersById[b].Poster.TemplateKey == "lamp" &&
                            current.PostersById[b].Presentation.Parameters.Lamp is null, "B inherited A's lamp override.");
                        baselineB = Bound(b, LampTemplatePolicy.Defaults); revision = current.Revision;
                        window!.RevealLampParametersForProbe(); window.PreviewLampControlsForProbe(bValue); Next(14); break;
                    case 14:
                        Require(current.Revision == revision, "B preview committed early.");
                        var bPreview = Bound(b, bValue); SameGeometry(baselineB!, bPreview, true); Bound(a, aValue);
                        Observe("second-poster-independent-preview", window!, bPreview, current);
                        window!.FinishSettingsForProbe(); Next(15); break;
                    case 15:
                        Require(current.Revision == revision + 1 && current.PostersById[b].Presentation.Parameters.Lamp == bValue &&
                            current.PostersById[a].Presentation.Parameters.Lamp == aValue, "Independent B save modified A or did not commit once.");
                        var bSaved = Bound(b, bValue); SameGeometry(baselineB!, bSaved, true); Bound(a, aValue);
                        Observe("second-poster-independent-save", window!, bSaved, current);
                        revision = current.Revision; capture = Capture(window!, b, "second-poster"); Next(16, 0); break;
                    case 16:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult();
                        _controlCenter.CloseForContractProbe(); Next(17); break;
                    case 17:
                        Require(window is null && current.Revision == revision && !_controlCenter.IsSettingsInteractionActive,
                            "Closing center retained settings resources/protection or wrote state.");
                        _controlCenter.Show(); Next(18); break;
                    case 18:
                        if (window?.Content.XamlRoot is null) return;
                        window.SelectSettingsTargetForProbe(a); window.RevealLampParametersForProbe(); Next(19); break;
                    case 19:
                        Require(current.Revision == revision && window!.ReadLampParameterControlsForProbe().Values == aValue &&
                            window.CommonSettingsForProbe.Preview is null, "Reopened center did not read A's saved values.");
                        Bound(b, bValue); Observe("center-reopen-restores-target-values", window!, Bound(a, aValue), current);
                        window!.PreviewHeldLampIntensityForProbe(19); Next(20); break;
                    case 20:
                        Require(current.Revision == revision && window!.HasPosterDraft(a), "Held slider did not retain a preview.");
                        window!.CancelLampDraftForProbe(); window.CompleteCancelledLampCallbacksForProbe(); Next(21, 600); break;
                    case 21:
                        Require(current.Revision == revision && window!.CommonSettingsForProbe.Preview is null &&
                            !window.HasPosterDraft(a), "Cancelled slider/idle color callback restarted a settings draft.");
                        Observe("cancelled-slider-late-callbacks-stay-idle", window!, Bound(a, aValue), current);
                        window!.OpenLampColorForProbe("lightColor"); window.SetLampPickerColorForProbe("#A4C6E8"); Next(22); break;
                    case 22:
                        Require(current.Revision == revision && window!.ReadLampParameterControlsForProbe().ColorPickerOpen &&
                            window.HasPosterDraft(a), "Actual color picker did not retain a noncommitted preview.");
                        var colorPreview = Bound(a, aValue with { LightColor = "#A4C6E8" });
                        SameGeometry(baseline!, colorPreview, false); Bound(b, bValue);
                        Observe("visual-color-picker-preview", window!, colorPreview, current);
                        capture = Capture(window!, a, "color-picker"); Next(23, 0); break;
                    case 23:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult(); window!.FinishLampColorForProbe(false); Next(24); break;
                    case 24:
                        Require(current.Revision == revision && !window!.HasPosterDraft(a) &&
                            !window.ReadLampParameterControlsForProbe().ColorPickerOpen,
                            "Cancelling color selection wrote state or retained popup/draft ownership.");
                        Observe("visual-color-picker-cancel", window!, Bound(a, aValue), current);
                        window!.OpenLampColorForProbe("lightColor"); window.SetLampPickerColorForProbe("#A4C6E8");
                        window.FinishLampColorForProbe(true); Next(25); break;
                    case 25:
                        Require(current.Revision == revision + 1 && !window!.HasPosterDraft(a) &&
                            current.PostersById[a].Presentation.Parameters.Lamp == (aValue with { LightColor = "#A4C6E8" }),
                            "Applying chosen color did not commit exactly the selected target once.");
                        Observe("visual-color-picker-apply-once", window!, Bound(a, aValue with { LightColor = "#A4C6E8" }), current);
                        revision = current.Revision; window!.ResetLampParametersForProbe(); Next(26); break;
                    case 26:
                        Require(current.Revision == revision + 1 && !window!.HasPosterDraft(a) &&
                            current.PostersById[a].Poster.ContentLayoutJson == aContent &&
                            current.PostersById[b].Poster.ContentLayoutJson == bContent &&
                            current.PostersById[b].Presentation.Parameters.Lamp == bValue,
                            "Current template reset touched content, another poster or more than one revision.");
                        Observe("current-template-reset-only", window!, Bound(a, LampTemplatePolicy.Defaults), current);
                        motionFrames = _spatialRuntime.ReadLampNativeForProbe(a).Scene.MotionFrames;
                        revision = current.Revision; window!.RevealTitleColorsForProbe(); Next(260, 650); break;
                    case 260:
                        baseline = Bound(a, LampTemplatePolicy.Defaults);
                        Require(_spatialRuntime.BackdropWindowForProbe(a).VerifyTitleChromeForProbe(), "Default title slots are not transparent/aligned.");
                        window!.OpenTitleColorForProbe(false); window.SetTitleColorForProbe("#85B9ED"); Next(261); break;
                    case 261:
                        Require(current.Revision == revision && window!.CommonSettingsForProbe.Preview?.TitleColors?.Text == "#85B9ED",
                            "Title preview did not retain its original target without writing.");
                        Require(_spatialRuntime.BackdropWindowForProbe(a).VerifyTitleChromeForProbe(), "Title preview did not paint bare glyphs.");
                        SameGeometry(baseline!, Bound(a, LampTemplatePolicy.Defaults), true);
                        window!.FinishLampColorForProbe(false); Next(262); break;
                    case 262:
                        Require(current.Revision == revision && current.PostersById[a].Presentation.Parameters.TitleColors is null,
                            "Cancelling title color wrote state.");
                        window!.OpenTitleColorForProbe(false); window.SetTitleColorForProbe("#85B9ED");
                        window.FinishLampColorForProbe(true); Next(263); break;
                    case 263:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.TitleColors == new PosterTitleColors("#85B9ED"),
                            "Title text color did not save once.");
                        Require(_spatialRuntime.BackdropWindowForProbe(a).VerifyTitleChromeForProbe(), "Saved title color mismatch.");
                        SameGeometry(baseline!, Bound(a, LampTemplatePolicy.Defaults), true);
                        window!.OpenTitleColorForProbe(true); window.SetTitleColorForProbe("#E4BF7A");
                        window.FinishLampColorForProbe(true); Next(264); break;
                    case 264:
                        Require(current.Revision == revision + 2 &&
                            current.PostersById[a].Presentation.Parameters.TitleColors == new PosterTitleColors("#85B9ED", "#E4BF7A") &&
                            current.PostersById[b].Presentation.Parameters.TitleColors is null,
                            "Action color lost the title color or leaked to peer.");
                        Require(_spatialRuntime.BackdropWindowForProbe(a).VerifyTitleChromeForProbe(), "Saved action ink mismatch.");
                        SameGeometry(baseline!, Bound(a, LampTemplatePolicy.Defaults), true);
                        Observe("transparent-title-colors-preview-cancel-single-commit-peer-isolation", window!, Bound(a, LampTemplatePolicy.Defaults), current);
                        revision = current.Revision;
                        capture = Capture(window!, a, "title-ink"); Next(265, 0); break;
                    case 265:
                        if (capture?.IsCompleted != true) break;
                        capture.GetAwaiter().GetResult();
                        baseline = Bound(a, LampTemplatePolicy.Defaults);
                        window!.SetTitleVisibilityForProbe(false); Next(266); break;
                    case 266:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.Parameters.ShowTitle == false &&
                            current.PostersById[b].Presentation.Parameters.ShowTitle is null &&
                            window!.VerifyTitleVisibilityControlForProbe(false) &&
                            _spatialRuntime.BackdropWindowForProbe(a).VerifyTitleVisibilityForProbe(false),
                            "Title toggle did not hide both name copies/save once, or changed peer/action visibility.");
                        SameGeometry(baseline!, Bound(a, LampTemplatePolicy.Defaults), true);
                        Observe("title-visibility-immediate-single-commit-keeps-name-geometry-and-peer", window!, Bound(a, LampTemplatePolicy.Defaults), current);
                        capture = Capture(window!, a, "title-hidden"); Next(267, 0); break;
                    case 267:
                        if (capture?.IsCompleted != true) break;
                        capture.GetAwaiter().GetResult();
                        window!.SetTitleVisibilityForProbe(true); Next(268); break;
                    case 268:
                        Require(current.Revision == revision + 2 && window!.VerifyTitleVisibilityControlForProbe(true) &&
                            _spatialRuntime.BackdropWindowForProbe(a).VerifyTitleVisibilityForProbe(true),
                            "Showing title again did not retain name/controls or save once.");
                        SameGeometry(baseline!, Bound(a, LampTemplatePolicy.Defaults), true);
                        revision = current.Revision;
                        window!.RevealLampParametersForProbe();
                        motionFrames = _spatialRuntime.ReadLampNativeForProbe(a).Scene.MotionFrames;
                        Next(27, 650); break;
                    case 27:
                        var moving = Bound(a, LampTemplatePolicy.Defaults);
                        observations.Add(new { name = "motion-gate-readback", previousFrames = motionFrames, moving.Scene,
                            timers = MotionTimers(), runtimeTimerPresent = _spatialRuntime.SceneMotionRunning,
                            motionResources = _ledger!.ActiveResources.Where(resource => resource.Contains("scene-motion", StringComparison.Ordinal)).ToArray() });
                        Require(current.Revision == revision && MotionTimers() <= 1, "Decorative frames wrote state or allocated per-poster timers.");
                        if (moving.Scene.ReducedMotion || moving.Scene.GlobalMotionPaused)
                            Require(moving.Scene.StaticOnly && MotionTimers() == 0, "Reduced/global-paused motion retained a frame timer.");
                        else
                        {
                            Require(MotionTimers() == 1 && _spatialRuntime.SceneMotionRunning,
                                "Expected exactly one application-owned motion timer, matching the running scheduler.");
                            Require(moving.Scene.MotionFrames > motionFrames,
                                $"Real shared dispatcher frames did not advance: before={motionFrames}, after={moving.Scene.MotionFrames}.");
                            timerAdvanceObserved = true;
                            var pointer = new LampScenePoint(moving.Scene.Width * .2, moving.Scene.Height * .6);
                            var followed = _spatialRuntime.PaintLampMotionForProbe(a, 1, pointer);
                            Require(followed.Scene.FollowingPointer && Math.Abs(followed.Scene.LightX - pointer.X) < 2 &&
                                Math.Abs(followed.Scene.LightY - pointer.Y) < 2, "Production paint handler did not follow the local pointer position.");
                            SameGeometry(moving, followed, true); pointerHandlerObserved = true;
                            var idle0 = _spatialRuntime.PaintLampMotionForProbe(a, 0, null);
                            var idle1 = _spatialRuntime.PaintLampMotionForProbe(a, 1, null);
                            Require(!idle1.Scene.FollowingPointer && idle0.Scene.FixtureAngle != idle1.Scene.FixtureAngle &&
                                idle0.Scene.LightX != idle1.Scene.LightX, "Pointer leave did not restore lamp and light idle sway.");
                            SameGeometry(moving, idle1, true); idleHandlerObserved = true;
                            _spatialRuntime.BackdropWindowForProbe(a).VerifyHeldLampFollowForProbe();
                            observations.Add(new { name = "held-item-and-refresh-keep-lamp-following", nativePointer = false, persisted = false });
                            observations.Add(new { name = "pointer-follow-and-leave-shared-paint-handler", followed = followed.Scene,
                                idleStart = idle0.Scene, idleLater = idle1.Scene, nativePointer = false });
                        }
                        observations.Add(new { name = "real-shared-motion-frames", moving.Scene, timers = MotionTimers(), timerAdvanceObserved });
                        Require(_spatialRuntime.SetContentEditing(a, true) is null && _spatialRuntime.SetContentEditing(b, true) is null,
                            "Could not enter production editing modes.");
                        Next(28); break;
                    case 28:
                        Require(_spatialRuntime.ReadLampNativeForProbe(a).Scene.StaticOnly &&
                            _spatialRuntime.ReadLampNativeForProbe(b).Scene.StaticOnly && MotionTimers() == 0 &&
                            !_ledger.ActiveResources.Contains("subscription.scene-motion-settings"), "Editing kept motion or its resources alive.");
                        observations.Add(new { name = "editing-stops-shared-motion", timers = MotionTimers(), current.Revision });
                        Require(_spatialRuntime.SetContentEditing(a, false) is null && _spatialRuntime.SetContentEditing(b, false) is null,
                            "Could not leave production editing modes.");
                        window!.PreviewLampControlsForProbe(LampTemplatePolicy.Defaults with { MotionEnabled = false }); Next(29); break;
                    case 29:
                        var disabled = Bound(a, LampTemplatePolicy.Defaults with { MotionEnabled = false });
                        Require(disabled.Scene.StaticOnly && !disabled.Scene.FollowingPointer && current.Revision == revision,
                            "Motion toggle did not stop only the preview target.");
                        Bound(b, bValue);
                        Observe("per-poster-motion-disabled-preview", window!, disabled, current);
                        window!.FinishSettingsForProbe(); Next(30); break;
                    case 30:
                        Require(current.Revision == revision + 1 && current.PostersById[a].Presentation.TemplateParameters.MotionEnabled == false &&
                            current.PostersById[b].Presentation.TemplateParameters == bValue, "Motion switch did not persist independently.");
                        ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); Next(31); break;
                    case 31:
                        Require(current.Tray.DynamicEffectsPaused && MotionTimers() == 0 &&
                            _spatialRuntime.ReadLampNativeForProbe(b).Scene.StaticOnly, "Global pause retained scene motion.");
                        observations.Add(new { name = "global-pause-stops-shared-motion", current.Revision, timers = MotionTimers() });
                        ToggleDynamicEffectsPaused(StateCommandSource.ControlCenter); Collapse(a); Next(32); break;
                    case 32:
                        Require(current.PostersById[a].Poster.IsCollapsed && !_spatialRuntime.ReadLampNativeForProbe(a).Scene.Visible,
                            "Collapsed poster retained a visible lamp scene.");
                        Collapse(b); Next(33); break;
                    case 33:
                        Require(current.PostersById[b].Poster.IsCollapsed && !_spatialRuntime.ReadLampNativeForProbe(b).Scene.Visible &&
                            MotionTimers() == 0 && !_ledger.ActiveResources.Contains("subscription.scene-motion-settings"),
                            "All collapsed scenes retained motion resources.");
                        observations.Add(new { name = "all-collapsed-motion-resources-zero", timers = MotionTimers(), current.Revision });
                        var beforeFault = _spatialRuntime.ReadLampNativeForProbe(a);
                        var unaffected = _spatialRuntime.ReadLampNativeForProbe(b);
                        Require(_spatialRuntime.FailSceneForProbe(a), "Failed scene was not detached/disposed once or retried on refresh.");
                        var afterFault = _spatialRuntime.ReadLampNativeForProbe(a);
                        SameGeometry(beforeFault, afterFault, true);
                        Require(!afterFault.Scene.Active && afterFault.BackdropValid &&
                            afterFault.BackdropColor == beforeFault.BackdropColor &&
                            afterFault.Parameters == beforeFault.Parameters &&
                            _spatialRuntime.ReadLampNativeForProbe(b).Scene == unaffected.Scene &&
                            MotionTimers() == 0 && _stateCoordinator.Refresh().Revision == current.Revision,
                            "Scene failure damaged base paint, another instance, state or resources.");
                        observations.Add(new { name = "scene-host-failure-detaches-only-decoration", resources = MotionTimers(),
                            afterFault.BackdropValid, stateUnchanged = true, geometryUnchanged = true, releaseCount = 1 });
                        _spatialRuntime.Suspend();
                        Require(MotionTimers() == 0, "Environment suspension retained motion resources.");
                        observations.Add(new { name = "environment-suspend-motion-resources-zero", timers = MotionTimers() });
                        Finish(true, null); break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
