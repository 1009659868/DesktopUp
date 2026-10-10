using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private void RenderContentHeading()
    {
        var target = _view?.SelectedPoster;
        var active = _settings.Preview?.PosterId == target?.Poster.PosterId ? _settings.Preview : null;
        bool supported = target?.Presentation.CanShowContentHeading == true;
        ContentHeadingFolder.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        ContentHeadingSwitch.IsOn = active?.Setting == PosterSetting.ContentHeadingVisibility ? active.Number == 1 :
            target?.Presentation.IsContentHeadingVisible == true;
        bool available = supported && target?.Presentation.CanEdit == true && !_editor.ProtectsInteraction &&
            !IsContentFieldDraftActive && _lampColorFlyout is null;
        ContentHeadingSwitch.IsEnabled = ContentHeadingRename.IsEnabled = available && !_settings.ProtectsInteraction;
        ContentHeadingSubtitle.IsEnabled = available && ContentHeadingSwitch.IsOn &&
            (!_settings.ProtectsInteraction || active?.Setting == PosterSetting.Subtitle);
        string subtitle = active?.Setting == PosterSetting.Subtitle ? active.Text : target?.Poster.Subtitle ?? "";
        if (ContentHeadingSubtitle.Text != subtitle) ContentHeadingSubtitle.Text = subtitle;
    }
    private void ContentHeading_Changed(object sender, RoutedEventArgs args)
    {
        if (!_renderingSettings) ChangeSetting(PosterSetting.ContentHeadingVisibility, ContentHeadingSwitch.IsOn ? 1 : 0, finish: true);
    }
    private void ContentHeadingSubtitle_Changing(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        if (_renderingSettings || !sender.IsEnabled) return;
        ChangeSetting(PosterSetting.Subtitle, text: sender.Text);
        // Text edits commit at a deliberate focus/Enter boundary, not in the middle of typing.
        _settingsTimer?.Stop();
    }
    private void ContentHeadingSubtitle_LostFocus(object sender, RoutedEventArgs args)
    { if (_settings.Preview?.Setting == PosterSetting.Subtitle) FinishSettings(); }
    private void ContentHeadingSubtitle_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (_settings.Preview?.Setting != PosterSetting.Subtitle) return;
        if (args.Key == global::Windows.System.VirtualKey.Enter) { FinishSettings(); args.Handled = true; }
        else if (args.Key == global::Windows.System.VirtualKey.Escape) { CancelSettingsPreview(); args.Handled = true; }
    }
}
