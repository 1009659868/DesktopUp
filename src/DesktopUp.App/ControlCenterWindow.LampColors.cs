using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly Dictionary<string, (Button Button, Border Chip)> _lampSwatches = new(StringComparer.Ordinal);
    // Transient popup identity only. All color values and cancellation belong to _settings.
    private Flyout? _lampColorFlyout;
    private ColorPicker? _lampColorPicker;
    private string? _lampColorTarget, _lampColorKey;
    private static bool TryLampColor(string text, out global::Windows.UI.Color color)
    {
        color = default;
        if (text.Length != 7 || text[0] != '#' || text.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF")) return false;
        color = global::Windows.UI.Color.FromArgb(255, Convert.ToByte(text.Substring(1, 2), 16),
            Convert.ToByte(text.Substring(3, 2), 16), Convert.ToByte(text.Substring(5, 2), 16));
        return true;
    }
    private void OpenLampColorPicker(string key)
    {
        if (!LampInputAvailable() || _lampColorFlyout is not null || !_lampSwatches.ContainsKey(key) ||
            _settings.ProtectsInteraction ||
            DesktopInteractionConflicts(_lampRenderedPosterId!)) return;
        OpenPosterColorPicker(key, _lampSwatches[key].Button, _lampColors[key].Text,
            LampInputAvailable, color => _lampColors[key].Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    // One popup owner and the original settings draft/cancel/commit lifecycle for both
    // template-specific colors and the host's title colors. Never a second persistence path.
    private void OpenPosterColorPicker(string key, Button anchor, string initialColor,
        Func<bool> available, Action<global::Windows.UI.Color> changed)
    {
        var body = new StackPanel { Spacing = 10, Width = 240, RequestedTheme = UiCRoot.ActualTheme };
        // Popup content is outside the center's visual subtree. Give newly created controls
        // the same scoped resources, instead of relying on the application's system defaults.
        foreach (var entry in UiCRoot.Resources) body.Resources[entry.Key] = entry.Value;
        foreach (var theme in UiCRoot.Resources.ThemeDictionaries)
        {
            var local = new ResourceDictionary();
            foreach (var entry in (ResourceDictionary)theme.Value) local[entry.Key] = entry.Value;
            body.Resources.ThemeDictionaries[theme.Key] = local;
        }
        var picker = new ColorPicker { IsAlphaEnabled = false, IsMoreButtonVisible = false,
            IsColorSliderVisible = true, IsColorChannelTextInputVisible = false, IsHexInputVisible = false, Width = 240 };
        if (TryLampColor(initialColor, out var initial)) picker.Color = initial;
        var flyout = new Flyout { Content = body };
        _lampColorFlyout = flyout; _lampColorPicker = picker; _lampColorTarget = _view?.SelectedPosterId; _lampColorKey = key;
        _settingsHeld = true; _settingsTimer?.Stop();
        body.Children.Add(new TextBlock { Text = "当前海报 · 颜色", FontSize = 12 }); body.Children.Add(picker);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var apply = new Button { Content = "应用颜色", Style = (Style)UiCRoot.Resources["UiCAction"] };
        var cancel = new Button { Content = "取消", Style = (Style)UiCRoot.Resources["UiCAction"] };
        apply.Click += (_, _) => CompleteLampColorPicker(true);
        cancel.Click += (_, _) => CompleteLampColorPicker(false);
        actions.Children.Add(apply); actions.Children.Add(cancel); body.Children.Add(actions);
        body.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Escape) { e.Handled = true; CompleteLampColorPicker(false); }
        };
        picker.ColorChanged += (_, e) =>
        {
            if (!ReferenceEquals(_lampColorPicker, picker) || _lampColorTarget != _view?.SelectedPosterId || !available()) return;
            // TextChanging synchronously registers the same six-field session draft.
            changed(e.NewColor);
        };
        flyout.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_lampColorFlyout, flyout)) return;
            CompleteLampColorPicker(false);
        };
        SettingsInteractionChanged?.Invoke(); RenderTemplateSamples();
        flyout.ShowAt(anchor);
    }
    private void CompleteLampColorPicker(bool apply)
    {
        if (_lampColorFlyout is null) return;
        if (apply && _lampColorTarget == _view?.SelectedPosterId && _settings.Preview?.Setting is (PosterSetting.Lamp or PosterSetting.TemplateParameters or PosterSetting.TitleColors))
        {
            // Invalid text in another field remains protected; choosing a color cannot save it.
            if (!_settings.IsPreviewValid) { RenderSettings(); return; }
            CloseLampColorPicker(); FinishSettings();
        }
        else if (apply) { CloseLampColorPicker(); RenderSettings(); }
        else { CloseLampColorPicker(); CancelSettingsPreview(); }
    }
    private void CloseLampColorPicker()
    {
        var flyout = _lampColorFlyout;
        _lampColorFlyout = null; _lampColorPicker = null; _lampColorTarget = _lampColorKey = null;
        if (flyout is null) return;
        _settingsHeld = false; flyout.Hide();
        SettingsInteractionChanged?.Invoke();
    }
    private void ResetLampParameters()
    {
        if (!LampInputAvailable() || OtherContentOwner || IsContentFieldDraftActive)
        { if (_lampParameterNotice is not null) _lampParameterNotice.Text = "请先应用或取消当前修改。"; return; }
        ChangeLampSetting(TemplateParameterBindingAdapter.Reset(_templateParameterSchema,
            _view?.SelectedPoster?.Presentation.TemplateValues ?? new TemplateParameterValues([])), true);
    }
}
