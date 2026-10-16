using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    // Same guarded, isolated entry as the older appearance probe; no synthetic pointer input.
    internal async Task VerifyAppearanceLayoutForProbe(bool restart, string root)
    {
        void Check(bool passed, string message) { if (!passed) throw new IOException(message); }
        async Task Settle(Func<bool> ready)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!ready()) { if (clock.Elapsed.TotalSeconds > 5) throw new IOException("Appearance layout did not settle."); await Task.Delay(60); }
            await Task.Delay(160);
        }
        var colors = new PosterTitleColors("#E7EDF2", "#9FD1BA");
        async Task ClosePicker(Action action)
        {
            var flyout = _lampColorFlyout ?? throw new IOException("Picker not opened.");
            var closed = new TaskCompletionSource();
            void OnClosed(object? sender, object args) => closed.TrySetResult();
            flyout.Closed += OnClosed;
            try
            {
                action();
                await Task.WhenAny(closed.Task, Task.Delay(3000));
                Check(closed.Task.IsCompleted, "Picker did not finish closing.");
            }
            finally { flyout.Closed -= OnClosed; }
        }
        async Task ThemeCheck(string stage)
        {
            File.AppendAllText(Path.Combine(root, "theme-stages.txt"), stage + " starting\n");
            foreach (string theme in new[] { "ink", "light" })
            { _applyCenterTheme!(theme); await Settle(() => _renderedCenterTheme == theme); }
            File.AppendAllText(Path.Combine(root, "theme-stages.txt"), stage + " passed\n");
        }
        string a = _view!.Posters.First().PosterId;
        SelectSettingsTargetForProbe(a);
        await Settle(() => _view.SelectedPosterId == a);
        if (!restart)
        {
            LayoutFolder.IsExpanded = SurfaceFolder.IsExpanded = TitleColorsFolder.IsExpanded = true;
            PrepareUiCSampleForProbe(false, 480, 1000);
            await Task.Delay(450);
            foreach (string theme in new[] { "ink", "blue", "light" })
            {
                _applyCenterTheme!(theme);
                await Settle(() => _renderedCenterTheme == theme);
            }
        }
        await VerifyAppearanceCleanupForProbe(restart, root, capture: false); // Reuse preview/cancel/one-commit + old template fidelity checks.
        if (!restart)
        {
            await ThemeCheck("surface-and-templates");
            var basis = _readCommitted!.Invoke()!;
            _applyCatalog!.Invoke(new("appearance-second-" + Guid.NewGuid().ToString("N"), basis.Revision,
                new StateReadSet(basis.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: basis.CatalogVersion).Freeze(),
                null, "B / 外观对照"));
            await Settle(() => _view.Posters.Count == 2);
            SelectSettingsTargetForProbe(a);
            await Settle(() => _view.SelectedPosterId == a);
            RevealTitleColorsForProbe();
            await ThemeCheck("second-poster");
            long revision = _view.Projection.Revision;
            SetTitleVisibilityForProbe(false);
            await Settle(() => !_view.SelectedPoster!.Presentation.Parameters.IsTitleVisible);
            Check(_view.Projection.Revision == revision + 1 && TitleSummary.Text.StartsWith("标题隐藏"), "Title toggle/summary did not commit once.");
            await ThemeCheck("title-hidden");
            foreach (bool actions in new[] { false, true })
            {
                revision = _view.Projection.Revision;
                OpenTitleColorForProbe(actions);
                await Settle(() => _lampColorPicker?.IsLoaded == true && _lampColorPicker.ActualWidth > 0);
                SetTitleColorForProbe("#123456"); await ClosePicker(() => CompleteLampColorPicker(false));
                Check(_view.Projection.Revision == revision, "Title color cancel wrote state.");
                await ThemeCheck("title-cancel-" + actions);
                revision = _view.Projection.Revision;
                OpenTitleColorForProbe(actions);
                await Settle(() => _lampColorPicker?.IsLoaded == true && _lampColorPicker.ActualWidth > 0);
                SetTitleColorForProbe(actions ? colors.Actions : colors.Text); await ClosePicker(() => CompleteLampColorPicker(true));
                await Settle(() => !_settings.ProtectsInteraction);
                Check(_view.Projection.Revision == revision + 1, "Title color did not commit once.");
                await ThemeCheck("title-apply-" + actions);
            }
            await ThemeCheck("title-colors");
            string b = _view.Posters.Single(p => p.PosterId != a).PosterId;
            var beforeB = _view.Projection.PostersById[b].Poster.LayoutParametersJson;
            _settingsHeld = true; SurfaceOpacitySlider.Value = 61;
            Check(_settings.Preview?.Surface?.OpacityPercent == 61, "Surface draft missing before switch.");
            SelectSettingsTargetForProbe(b);
            await Settle(() => _view.SelectedPosterId == b && !_settings.ProtectsInteraction);
            Check(_view.Projection.PostersById[b].Poster.LayoutParametersJson == beforeB && TitleVisibilitySwitch.IsOn,
                "Switch applied A's draft to B.");
            SelectSettingsTargetForProbe(a);
            await Settle(() => _view.SelectedPosterId == a);
            Check(SurfaceOpacitySlider.Value == 25 && SurfaceOpacityValue.Text == "25%" && !TitleVisibilitySwitch.IsOn,
                "Switch did not restore A's authoritative controls.");
            SurfaceColor_Click(SurfaceColorButton, new RoutedEventArgs());
            await Settle(() => _lampColorPicker?.IsLoaded == true && _lampColorPicker.ActualWidth > 0);
            SetLampPickerColorForProbe("#654321");
            await ClosePicker(() => SelectSettingsTargetForProbe(b));
            await Settle(() => _view.SelectedPosterId == b && _lampColorFlyout is null && !_settings.ProtectsInteraction);
            Check(_view.Projection.PostersById[b].Poster.LayoutParametersJson == beforeB, "Color popup leaked into B.");
            SelectSettingsTargetForProbe(a);
            await Settle(() => _view.SelectedPosterId == a);
            await ThemeCheck("target-switch");
            foreach (string theme in new[] { "light", "ink", "blue" })
            {
                _applyCenterTheme!(theme);
                await Settle(() => _renderedCenterTheme == theme);
                foreach (int width in new[] { 980, 480 })
                {
                    PrepareUiCSampleForProbe(false, width, 1000);
                    LayoutFolder.IsExpanded = SurfaceFolder.IsExpanded = TitleColorsFolder.IsExpanded = true;
                    await Task.Delay(450); UiCRoot.UpdateLayout(); AppearanceScroll.ChangeView(null, 0, null, true);
                    bool Fits(FrameworkElement control, FrameworkElement parent)
                    {
                        double x = control.TransformToVisual(parent).TransformPoint(new(0, 0)).X;
                        return control.IsLoaded && control.ActualWidth > 0 && x >= -1 && x + control.ActualWidth <= parent.ActualWidth + 1;
                    }
                    Check(Fits(SurfaceCard, LayoutFolderPanel) && Fits(TitleCard, LayoutFolderPanel) &&
                        Fits(SurfaceColorButton, SurfaceCard) && Fits(SurfaceOpacitySlider, SurfaceCard) &&
                        Fits(TitleVisibilitySwitch, TitleCard) && _titleColors.Values.All(c => Fits(c.Button, TitleCard)), "Appearance controls overflow.");
                    Check(_titleColors.Values.All(c => c.Button.IsEnabled && c.Button.ActualWidth == SurfaceColorButton.ActualWidth), "Color actions are inconsistent or disabled.");
                    await CaptureSettingsForProbeAsync(Path.Combine(root, $"layout-{theme}-{width}.png"));
                }
            }
            _applyCenterTheme!("light"); PrepareUiCSampleForProbe(false, 980, 800);
            long foldedRevision = _view.Projection.Revision;
            SurfaceFolder.IsExpanded = TitleColorsFolder.IsExpanded = false;
            await Task.Delay(450); AppearanceScroll.ChangeView(null, 0, null, true);
            Check(_view.Projection.Revision == foldedRevision && SurfaceSummary.Text == "#375B69 · 不透明度 25%", "Folding reset state or summary is stale.");
            await CaptureSettingsForProbeAsync(Path.Combine(root, "layout-folded.png"));
        }
        Check(_view!.Posters.Count == 2 && _view.SelectedPoster!.Presentation.Parameters.TitleColors == colors &&
            !_view.SelectedPoster.Presentation.Parameters.IsTitleVisible && CurrentSurface == new PosterSurfaceAppearance("#375B69", 25),
            "Appearance state was not retained across selection/restart.");
    }

    // Called only by RunIncomingProbe after validating its owned isolated profile.
    internal async Task VerifyAppearanceCleanupForProbe(bool restart, string root, bool capture = true)
    {
        var desired = new PosterSurfaceAppearance("#375B69", 25);
        async Task Until(Func<bool> ready)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!ready())
            { if (timeout.Elapsed.TotalSeconds > 4) throw new IOException("Appearance UI did not settle."); await Task.Delay(60); }
            await Task.Delay(120);
        }
        void Check(bool passed, string message) { if (!passed) throw new IOException(message); }
        if (!restart)
        {
            TemplateForProbe("basic-box");
            await Until(() => _view?.SelectedPoster?.Poster.TemplateKey == "basic-box" && !_settings.ProtectsInteraction);
            Check(ContentHeadingFolder is null && TemplateComponentsSection.Children.Count == 0, "Basic storage mounted a heading editor.");
            ShowWorkspacePage(false); LayoutFolder.IsExpanded = SurfaceFolder.IsExpanded = true;
            SurfaceFolder.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            await Task.Delay(160);
            long revision = _view!.Projection.Revision;
            SurfaceColor_Click(SurfaceColorButton, new RoutedEventArgs());
            SetLampPickerColorForProbe(desired.Color);
            Check(_settings.Preview?.Surface?.Color == desired.Color, "Color picker did not preview common surface.");
            CompleteLampColorPicker(false);
            Check(_view.Projection.Revision == revision && !_settings.ProtectsInteraction, "Color cancel wrote state or retained a draft.");
            SurfaceColor_Click(SurfaceColorButton, new RoutedEventArgs());
            SetLampPickerColorForProbe(desired.Color); CompleteLampColorPicker(true);
            await Until(() => _view.SelectedPoster!.Presentation.Parameters.Surface?.Color == desired.Color && !_settings.ProtectsInteraction);
            Check(_view.Projection.Revision == revision + 1, "Color apply did not commit exactly once.");
            revision = _view.Projection.Revision;
            _settingsHeld = true; SurfaceOpacitySlider.Value = desired.OpacityPercent;
            Check(_settings.Preview?.Surface == desired, "Opacity slider did not preview common surface.");
            CancelSettingsPreview();
            Check(_view.Projection.Revision == revision, "Opacity cancellation wrote state.");
            _settingsHeld = true; SurfaceOpacitySlider.Value = desired.OpacityPercent; FinishSettingsForProbe();
            await Until(() => _view.SelectedPoster!.Presentation.Parameters.Surface == desired && !_settings.ProtectsInteraction);
            Check(_view.Projection.Revision == revision + 1, "Opacity commit was not atomic.");
            foreach (string template in new[] { "night-ops", "lamp", "rain", "basic-box" })
            {
                TemplateForProbe(template);
                await Until(() => _view.SelectedPoster!.Poster.TemplateKey == template && !_settings.ProtectsInteraction);
                Check(_view.SelectedPoster!.Presentation.Parameters.Surface == desired, "Template switch lost common surface.");
                Check((ContentHeadingFolder is not null) == (template is "night-ops" or "lamp"), "Component UI does not match mounted template.");
                Check(!_lampColors.ContainsKey("background") && !_lampNumbers.ContainsKey("backgroundOpacity"), "Private template controls duplicate common surface.");
                UiCTemplateFolder.IsExpanded = template is "night-ops" or "lamp";
                if (ContentHeadingFolder is not null) ContentHeadingFolder.IsExpanded = true;
                LayoutFolder.IsExpanded = SurfaceFolder.IsExpanded = true;
                await Task.Delay(120);
                if (capture) await CaptureSettingsForProbeAsync(Path.Combine(root, "appearance-" + template + ".png"));
            }
        }
        Check(_view?.SelectedPoster?.Presentation.Parameters.Surface == desired, "Restart lost surface configuration.");
        Check(_view?.SelectedPoster?.Poster.TemplateKey == "basic-box" && ContentHeadingFolder is null, "Basic storage changed after restart.");
        Check(SurfaceColorButton.IsEnabled && SurfaceOpacitySlider.IsEnabled, "Common appearance controls are unavailable.");
    }
}
