using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

// Isolated probe drivers set actual dependency properties and use their production handlers.
// They do not synthesize a native mouse gesture, keyboard event, or a routed Escape event.
public sealed partial class ControlCenterWindow
{
    internal void OpenLampColorForProbe(string key) => OpenLampColorPicker(key);
    internal void SetLampPickerColorForProbe(string hex)
    {
        if (_lampColorPicker is null || !TryLampColor(hex, out var color)) throw new InvalidOperationException("No color picker or invalid color.");
        _lampColorPicker.Color = color;
    }
    internal void FinishLampColorForProbe(bool apply) => CompleteLampColorPicker(apply);
    internal void ResetLampParametersForProbe() => ResetLampParameters();
    internal void RevealLampParametersForProbe()
    {
        ShowWorkspacePage(false);
        UiCTemplateFolder.IsExpanded = true;
        if (_lampParameterFolder is null) throw new InvalidOperationException("Lamp controls are not initialized.");
        _lampParameterFolder.IsExpanded = true;
        _lampParameterFolder.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
    }

    internal void PreviewLampControlsForProbe(LampTemplateParameters value)
    {
        if (!LampInputAvailable()) throw new InvalidOperationException("Lamp input is not available for the selected target.");
        _settingsHeld = true; _settingsTimer?.Stop();
        _lampColors["lightColor"].Text = value.LightColor;
        _lampNumbers["softness"].Value = value.Softness;
        _lampNumbers["intensity"].Value = value.Intensity;
        _lampColors["background"].Text = value.Background;
        _lampColors["card"].Text = value.Card;
        _lampNumbers["backgroundOpacity"].Value = value.BackgroundOpacity;
        _lampBooleans["motionEnabled"].IsOn = value.MotionEnabled;
        _lampBooleans["followPointer"].IsOn = value.FollowPointer;
        _lampNumbers["idleSway"].Value = value.IdleSway;
        _lampNumbers["motionSpeed"].Value = value.MotionSpeed;
    }

    internal void PreviewInvalidLampColorForProbe()
    {
        if (!LampInputAvailable()) throw new InvalidOperationException("Lamp input is not available.");
        _settingsHeld = true; _settingsTimer?.Stop();
        _lampColors["lightColor"].Text = "#GGGGGG";
    }

    // Same cancellation method called by production Escape and pointer cancellation handlers.
    internal void CancelLampDraftForProbe() => CancelSettingsPreview();

    // Token/callback regression only; still not a synthesized native pointer event.
    internal void PreviewHeldLampIntensityForProbe(double value)
    {
        _lampHeldSlider = _lampNumbers["intensity"];
        _settingsHeld = true; _settingsTimer?.Stop();
        _lampHeldSlider.Value = value;
    }
    internal void CompleteCancelledLampCallbacksForProbe()
    {
        FinishLampPointer(_lampNumbers["intensity"]);
        CommitLampColor();
    }
}
