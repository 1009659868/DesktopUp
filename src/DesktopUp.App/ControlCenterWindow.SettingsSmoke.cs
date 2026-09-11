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
    internal ControlCenterAutoPostureEditor SettingsEditorForProbe => _autoEditor;
    internal bool SettingsControlsEnabledForProbe => AutoPostureSwitch.IsEnabled && AutoPostureDelay.IsEnabled;
    internal bool SettingsWarningForProbe => AutoPostureInfo.Severity == InfoBarSeverity.Warning;
    internal void SelectSettingsTargetForProbe(string id) => PosterSelector.SelectedItem =
        ((IReadOnlyList<ControlCenterPosterRow>)PosterSelector.ItemsSource).Single(p => p.PosterId == id);
    internal void EditSettingsForProbe(bool enabled, string text)
    {
        AutoPostureSwitch.IsOn = enabled; AutoPostureDelay.Text = text;
    }
    internal void InvokeSettingsForProbe(bool apply)
    {
        Button button = apply ? AutoPostureApply : AutoPostureCancel;
        if (!button.IsEnabled) throw new InvalidOperationException("Settings button disabled.");
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button);
        if (peer?.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            throw new InvalidOperationException("Missing native button invoke provider.");
        invoke.Invoke(); // Real Button automation/Click path, not the draft command directly.
    }
    internal void RevealSettingsForProbe() => AutoPostureApply.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
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
