using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    // Called only by RunIncomingProbe after validating its owned isolated profile.
    internal async Task VerifyAppearanceCleanupForProbe(bool restart, string root)
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
                await CaptureSettingsForProbeAsync(Path.Combine(root, "appearance-" + template + ".png"));
            }
        }
        Check(_view?.SelectedPoster?.Presentation.Parameters.Surface == desired, "Restart lost surface configuration.");
        Check(_view?.SelectedPoster?.Poster.TemplateKey == "basic-box" && ContentHeadingFolder is null, "Basic storage changed after restart.");
        Check(SurfaceColorButton.IsEnabled && SurfaceOpacitySlider.IsEnabled, "Common appearance controls are unavailable.");
    }
}
