using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly Dictionary<bool, (Button Button, Border Chip, TextBlock Value)> _titleColors = new();

    private void InitializeTitleColors()
    {
        _titleColors.Add(false, (TitleTextColorButton, TitleTextColorChip, TitleTextColorValue));
        _titleColors.Add(true, (TitleActionColorButton, TitleActionColorChip, TitleActionColorValue));
        TitleTextColorButton.Click += (_, _) => OpenTitleColorPicker(false);
        TitleActionColorButton.Click += (_, _) => OpenTitleColorPicker(true);
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
        TitleSummary.Text = TitleVisibilitySwitch.IsOn ? "标题显示 · 文字与符号独立配色" : "标题隐藏 · 收展与更多按钮保留";
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

}
