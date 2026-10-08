using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly Dictionary<bool, (Button Button, Border Chip, TextBlock Value)> _titleColors = new();

    private void InitializeTitleColors()
    {
        foreach (bool actions in new[] { false, true })
        {
            var line = new StackPanel { Spacing = 5 };
            string label = actions ? "动作符号" : "标题文字";
            line.Children.Add(new TextBlock { Text = label, FontSize = 12 });
            var swatch = new Border { Width = 24, Height = 18, CornerRadius = new(2),
                BorderThickness = new(1), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
            var value = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(swatch); content.Children.Add(value);
            var button = new Button { Content = content, Style = (Style)UiCRoot.Resources["UiCAction"], Padding = new(8, 6, 8, 6) };
            AutomationProperties.SetName(button, label + "颜色");
            AutomationProperties.SetAutomationId(button, actions ? "TitleActionColor" : "TitleTextColor");
            ToolTipService.SetToolTip(button, "仅当前海报；标题无背景底板，可按桌面背景调整颜色。不会改变 Item 文字或控制中心主题。");
            button.Click += (_, _) => OpenTitleColorPicker(actions);
            line.Children.Add(button); TitleColorsPanel.Children.Add(line);
            _titleColors.Add(actions, (button, swatch, value));
        }
        RenderTitleColors();
    }

    private bool TitleColorInputAvailable() => !_renderingSettings && !_editingClosed &&
        _view?.SelectedPoster?.Presentation.CanEdit == true && !_editor.ProtectsInteraction && !IsContentFieldDraftActive;

    private PosterTitleColors CurrentTitleColors => _settings.Preview is { Setting: PosterSetting.TitleColors } draft &&
        draft.PosterId == _view?.SelectedPosterId ? draft.TitleColors! : _view?.SelectedPoster?.Presentation.Parameters.TitleColors ?? new();

    private void OpenTitleColorPicker(bool actions)
    {
        if (!TitleColorInputAvailable() || _lampColorFlyout is not null || _settings.ProtectsInteraction ||
            DesktopInteractionConflicts(_view!.SelectedPosterId!)) return;
        var current = CurrentTitleColors;
        OpenPosterColorPicker(actions ? "@title-actions" : "@title-text", _titleColors[actions].Button,
            actions ? current.Actions : current.Text, TitleColorInputAvailable, color =>
            {
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                var value = CurrentTitleColors;
                ChangeTitleColors(actions ? value with { Actions = hex } : value with { Text = hex });
            });
    }

    private void ChangeTitleColors(PosterTitleColors colors)
    {
        if (!TitleColorInputAvailable() || _view?.SelectedPoster is not { } target || DesktopInteractionConflicts(target.Poster.PosterId)) return;
        bool valid = _settings.Update(new(target, PosterSetting.TitleColors, TitleColors: colors));
        if (valid && _settings.Preview is { } edit && _contentRuntime?.Invoke()?.ValidateControlCenterSetting(edit) is { } error)
        { _settings.RejectPreview(error); valid = false; }
        if (valid) QueueVisualPreview();
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }

    private void RenderTitleColors()
    {
        var ink = CurrentTitleColors;
        foreach (var (actions, controls) in _titleColors)
        {
            string hex = actions ? ink.Actions : ink.Text;
            controls.Value.Text = hex;
            if (TryLampColor(hex, out var color)) controls.Chip.Background = new SolidColorBrush(color);
            controls.Button.IsEnabled = _view?.SelectedPoster?.Presentation.CanEdit == true &&
                !_editor.ProtectsInteraction && !IsContentFieldDraftActive &&
                ((!_settings.ProtectsInteraction && _lampColorFlyout is null) ||
                 (_lampColorFlyout is not null && _lampColorTarget == _view?.SelectedPosterId &&
                  _lampColorKey == (actions ? "@title-actions" : "@title-text")));
        }
    }

    internal void RevealTitleColorsForProbe()
    {
        ShowWorkspacePage(false); UiCTemplateFolder.IsExpanded = false;
        LayoutFolder.IsExpanded = true;
        TitleColorsFolder.IsExpanded = true;
        UiCRoot.UpdateLayout();
        TitleColorsFolder.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
    }
    internal void OpenTitleColorForProbe(bool actions) => OpenTitleColorPicker(actions);
    internal void SetTitleColorForProbe(string value)
    {
        if (_lampColorKey is not ("@title-text" or "@title-actions") || _lampColorPicker is null || !TryLampColor(value, out var color))
            throw new InvalidOperationException("Open the real title color picker first.");
        _lampColorPicker.Color = color;
    }
}
