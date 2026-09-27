using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

// Only called by --auto-settings-smoke with the isolated-profile guard in App.
public sealed partial class ControlCenterWindow
{
    internal bool SettingsControlsEnabledForProbe => AutoPostureSwitch.IsEnabled && AutoPostureDelay.IsEnabled;
    internal bool AppearanceControlsEnabledForProbe => _template.IsEnabled && ScaleSlider.IsEnabled && SpacingSlider.IsEnabled;
    internal void TemplateForProbe(string key) => SelectTemplateKey(key);
    internal void SpacingForProbe(double value) { _settingsHeld = true; SpacingSlider.Value = value; }
    internal object PresetDiagnosticsForProbe() => new { scaleMinimum = ScaleSlider.Minimum, scaleMaximum = ScaleSlider.Maximum,
        scaleValue = ScaleSlider.Value, spacingMinimum = SpacingSlider.Minimum, spacingValue = SpacingSlider.Value };
    internal ControlCenterSettingsSession CommonSettingsForProbe => _settings;
    internal void SelectSettingsTargetForProbe(string id) => PosterSelector.SelectedItem =
        ((IReadOnlyList<ControlCenterPosterRow>)PosterSelector.ItemsSource).Single(p => p.PosterId == id);
    internal void PreviewScaleForProbe(double value) { _settingsHeld = true; ScaleSlider.Value = value; }
    internal void FinishSettingsForProbe() { _settingsHeld = false; FinishSettings(); }
    internal void DelayForProbe(double value) => AutoPostureDelay.Value = value;
    internal void ToggleForProbe(bool value) => AutoPostureSwitch.IsOn = value;
    internal void RevealSettingsForProbe() => ShowWorkspacePage(false);
    internal void RevealBehaviorForProbe() => ShowWorkspacePage(true);
    internal void PresetForProbe(bool spacing, int index)
    {
        var marks = spacing ? _spacingMarks! : _scaleMarks!;
        var slider = spacing ? SpacingSlider : ScaleSlider;
        var point = marks.CenterForProbe(index);
        _settingsHeld = true;
        marks.Begin(1, point);
        slider.Value = marks.Complete(1, point) ?? throw new InvalidOperationException("Preset click missed its native track mark");
        FinishSettingsForProbe();
    }
    internal bool VerifyPresetLayoutForProbe() =>
        ScaleHost.ActualWidth > 0 && ScaleHost.ActualWidth <= 320 && SpacingHost.ActualWidth == ScaleHost.ActualWidth &&
        _scaleMarks?.Ready == true && _spacingMarks?.Ready == true &&
        _scaleMarks.CenterForProbe(0).X < _scaleMarks.CenterForProbe(1).X &&
        _scaleMarks.CenterForProbe(1).X < _scaleMarks.CenterForProbe(2).X &&
        ScaleMarks.IsHitTestVisible == false && SpacingMarks.IsHitTestVisible == false &&
        _scaleMarks.ThumbAlignedForProbe() && _spacingMarks.ThumbAlignedForProbe();
    internal async Task CaptureSettingsForProbeAsync(string path)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(Content);
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
