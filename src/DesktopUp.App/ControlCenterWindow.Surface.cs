using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private bool _surfacePointerHeld;
    private PosterSurfaceAppearance CurrentSurface
    {
        get
        {
            if (_settings.Preview is { Setting: PosterSetting.Surface } edit && edit.PosterId == _view?.SelectedPosterId)
                return edit.Surface!;
            if (_view?.SelectedPoster?.Presentation is not { } presentation) return new("#282B30", 100);
            if (presentation.Parameters.Surface is { } saved) return saved;
            var paint = ScenePresentationAdapter.Resolve(presentation).Surface;
            return new(paint.Background, paint.BackgroundAlpha * 100d / 255);
        }
    }

    private bool SurfaceInputAvailable() => !_renderingSettings && !_editingClosed &&
        _view?.SelectedPoster?.Presentation.CanEdit == true && !_editor.ProtectsInteraction && !IsContentFieldDraftActive;

    private void InitializeSurfaceControls()
    {
        var slider = SurfaceOpacitySlider;
        slider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (!SurfaceInputAvailable() || !slider.IsEnabled || !e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
            _surfacePointerHeld = _settingsHeld = true; _settingsTimer?.Stop();
        }), true);
        slider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => FinishSurfacePointer()), true);
        slider.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) => CancelSettingsPreview()), true);
        slider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, e) =>
        {
            if (!_surfacePointerHeld) return;
            if (e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) CancelSettingsPreview();
            else FinishSurfacePointer();
        }), true);
        slider.ValueChanged += (_, _) =>
        {
            if (SurfaceInputAvailable() && slider.IsEnabled) ChangeSurface(CurrentSurface with { OpacityPercent = slider.Value });
        };
        slider.LostFocus += (_, _) =>
        {
            FinishSurfacePointer();
            if (_settings.Preview?.Setting == PosterSetting.Surface && _lampColorFlyout is null) FinishSettings();
        };
        RenderSurfaceControls();
    }

    private void FinishSurfacePointer()
    {
        if (!_surfacePointerHeld) return;
        _surfacePointerHeld = _settingsHeld = false;
        if (_settings.Preview?.Setting == PosterSetting.Surface) FinishSettings();
    }

    private void SurfaceColor_Click(object sender, RoutedEventArgs args)
    {
        if (!SurfaceInputAvailable() || _lampColorFlyout is not null || _settings.ProtectsInteraction ||
            DesktopInteractionConflicts(_view!.SelectedPosterId!)) return;
        OpenPosterColorPicker("@surface", SurfaceColorButton, CurrentSurface.Color, SurfaceInputAvailable,
            color => ChangeSurface(CurrentSurface with { Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}" }));
    }

    private void ChangeSurface(PosterSurfaceAppearance value)
    {
        if (!SurfaceInputAvailable() || _view?.SelectedPoster is not { } target || DesktopInteractionConflicts(target.Poster.PosterId)) return;
        bool valid = _settings.Update(new(target, PosterSetting.Surface, Surface: value));
        if (valid && _settings.Preview is { } edit && _contentRuntime?.Invoke()?.ValidateControlCenterSetting(edit) is { } error)
        { _settings.RejectPreview(error); valid = false; }
        if (valid) QueueVisualPreview();
        if (valid && !_settingsHeld) { _settingsTimer?.Stop(); _settingsTimer?.Start(); }
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }

    private void RenderSurfaceControls()
    {
        var value = CurrentSurface;
        SurfaceColorValue.Text = value.Color;
        if (TryLampColor(value.Color, out var color)) SurfaceColorChip.Background = new SolidColorBrush(color);
        SurfaceOpacityLabel.Text = $"背景不透明度 · {value.OpacityPercent:0}%";
        SurfaceOpacitySlider.Value = value.OpacityPercent;
        bool available = _view?.SelectedPoster?.Presentation.CanEdit == true && !_editor.ProtectsInteraction && !IsContentFieldDraftActive;
        SurfaceColorButton.IsEnabled = available && ((!_settings.ProtectsInteraction && _lampColorFlyout is null) ||
            _lampColorKey == "@surface" && _lampColorTarget == _view?.SelectedPosterId);
        SurfaceOpacitySlider.IsEnabled = available && _lampColorFlyout is null &&
            (!_settings.ProtectsInteraction || _settings.Preview?.Setting == PosterSetting.Surface);
    }
}
