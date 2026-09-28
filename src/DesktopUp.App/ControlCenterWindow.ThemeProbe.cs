using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    // Isolated recipe probe only: exercise the real templates, including a newly created popup.
    // VisualStateManager is not OS hover/click evidence. No production interaction is synthesized.
    internal async Task VerifyCenterThemeForProbeAsync(string directory, string tag)
    {
        var observations = new List<object>();
        var errors = new List<string>();
        string theme = UiCRoot.ActualTheme.ToString();
        string savedPoster = JsonSerializer.Serialize(_view!.SelectedPoster!.Poster);
        bool light = UiCRoot.ActualTheme == ElementTheme.Light;
        var expected = ((SolidColorBrush)UiCPaperSurface.Background).Color;
        var folder = ((SolidColorBrush)((ResourceDictionary)UiCRoot.Resources.ThemeDictionaries[light ? "Light" : "Default"])["UiCFolderBrush"]).Color;
        var controls = new Dictionary<string, Control>
        {
            ["more"] = MoreActionsButton, ["global"] = UiCGlobalSettings,
            ["create"] = CreatePosterButton, ["reset"] = _lampReset!
        };
        foreach (var pair in _lampSwatches) controls["swatch-" + pair.Key] = pair.Value.Button;
        static string Hex(Brush? brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.GetType().Name ?? "null";
        static double Brightness(global::Windows.UI.Color color) => (color.R + color.G + color.B) / 3d;
        try
        {
            RevealLampParametersForProbe();
            await Task.Delay(180);
            OpenLampColorPicker(_lampSwatches.Keys.First());
            await Task.Delay(180);
            if (_lampColorFlyout?.Content is not StackPanel body || !body.IsLoaded)
                throw new InvalidOperationException("The production color popup did not load.");
            foreach (var button in RecipeVisuals<Button>(body).Where(b => b.Content is string))
                controls["picker-" + button.Content] = button;
            if (!controls.ContainsKey("picker-应用颜色") || !controls.ContainsKey("picker-取消"))
                throw new InvalidOperationException("The production picker actions are missing.");
            await CaptureSettingsForProbeAsync(Path.Combine(directory, "center-theme-" + tag + ".png"));
            await CaptureThemeElementForProbeAsync(body, Path.Combine(directory, "center-theme-" + tag + "-picker.png"));
            foreach (string state in new[] { "Normal", "PointerOver", "Pressed", "Normal", "Disabled", "Normal" })
            {
                if (!body.IsLoaded || !ReferenceEquals(_lampColorFlyout?.Content, body))
                    throw new InvalidOperationException("Color popup was dismissed during theme observation; detached controls cannot certify visible theme states.");
                foreach (var control in controls.Values)
                {
                    control.IsEnabled = state != "Disabled";
                    if (!VisualStateManager.GoToState(control, state, false)) errors.Add("State unavailable: " + state);
                }
                await Task.Delay(120);
                foreach (var pair in controls)
                {
                    var control = pair.Value;
                    bool connected = control.IsLoaded && body.IsLoaded && ReferenceEquals(_lampColorFlyout?.Content, body);
                    var presenter = RecipeVisuals<ContentPresenter>(control).FirstOrDefault(p => p.Name == "ContentPresenter");
                    Brush? background = control is DropDownButton
                        ? RecipeVisuals<Grid>(control).FirstOrDefault(p => p.Name == "RootGrid")?.Background
                        : presenter?.Background;
                    var foreground = presenter?.Foreground ?? control.Foreground;
                    bool validBackground = background is SolidColorBrush fill &&
                        (light ? Brightness(fill.Color) > 150 : Brightness(fill.Color) < 125);
                    if (state == "Normal") validBackground &= Hex(background) == folder.ToString();
                    bool validText = foreground is SolidColorBrush ink &&
                        (light ? Brightness(ink.Color) < 150 : Brightness(ink.Color) > 140);
                    observations.Add(new { control = pair.Key, state, theme, background = Hex(background),
                        foreground = Hex(foreground), propertyBackground = Hex(control.Background), validBackground, validText,
                        connected, actualTheme = control.ActualTheme.ToString() });
                    if (!connected) errors.Add(pair.Key + "/" + state + " was disconnected during observation.");
                    if (!validBackground || !validText) errors.Add(pair.Key + "/" + state + " has mismatched theme brushes.");
                }
            }
            if (JsonSerializer.Serialize(_view.SelectedPoster!.Poster) != savedPoster || _settings.ProtectsInteraction)
                errors.Add("Inspecting color/theme visuals changed poster state or created a draft.");
        }
        finally
        {
            foreach (var control in controls.Values) { control.IsEnabled = true; VisualStateManager.GoToState(control, "Normal", false); }
            CompleteLampColorPicker(false);
            File.WriteAllText(Path.Combine(directory, "center-theme-" + tag + ".json"), JsonSerializer.Serialize(new
            {
                theme, expectedPanel = expected.ToString(), expectedFolder = folder.ToString(), observations, errors,
                nativeMouse = false, desktopCompositionCertified = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("; ", errors));
    }

    private static async Task CaptureThemeElementForProbeAsync(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(element);
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) throw new InvalidOperationException("Theme capture is empty.");
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
