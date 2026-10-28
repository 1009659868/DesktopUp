using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Expander? ContentHeadingFolder;
    private ToggleSwitch? ContentHeadingSwitch;
    private TextBox? ContentHeadingSubtitle;
    private Button? ContentHeadingRename;

    private void MountContentHeadingControls(bool mounted)
    {
        if (!mounted)
        {
            TemplateComponentsSection.Children.Clear();
            ContentHeadingFolder = null; ContentHeadingSwitch = null;
            ContentHeadingSubtitle = null; ContentHeadingRename = null;
            return;
        }
        if (ContentHeadingFolder is not null) return;
        ContentHeadingSwitch = new() { Header = "显示内容介绍", OnContent = "显示", OffContent = "隐藏" };
        ContentHeadingSwitch.Toggled += ContentHeading_Changed;
        AutomationProperties.SetAutomationId(ContentHeadingSwitch, "ContentHeadingVisibility");
        ToolTipService.SetToolTip(ContentHeadingSwitch, "仅当前模板已装配的介绍区；隐藏后收回占位，顶部标题栏不变。");
        ContentHeadingSubtitle = new() { Header = "副标题", MaxLength = 512 };
        ContentHeadingSubtitle.TextChanging += ContentHeadingSubtitle_Changing;
        ContentHeadingSubtitle.LostFocus += ContentHeadingSubtitle_LostFocus;
        ContentHeadingSubtitle.KeyDown += ContentHeadingSubtitle_KeyDown;
        AutomationProperties.SetAutomationId(ContentHeadingSubtitle, "ContentHeadingSubtitle");
        ToolTipService.SetToolTip(ContentHeadingSubtitle, "可留空；Enter或离开输入框保存，Esc取消当前输入。");
        ContentHeadingRename = new() { Content = "修改海报名称…", Style = (Style)UiCRoot.Resources["UiCAction"] };
        ContentHeadingRename.Click += RenamePoster_Click;
        ToolTipService.SetToolTip(ContentHeadingRename, "介绍区大标题使用海报名称；数量由内容自动更新。");
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(ContentHeadingSwitch); panel.Children.Add(ContentHeadingSubtitle); panel.Children.Add(ContentHeadingRename);
        ContentHeadingFolder = new() { Header = "组件与参数 · 内容介绍", Content = panel,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        TemplateComponentsSection.Children.Add(ContentHeadingFolder);
    }
    private void RenderContentHeading()
    {
        var target = _view?.SelectedPoster;
        var active = _settings.Preview?.PosterId == target?.Poster.PosterId ? _settings.Preview : null;
        bool supported = target?.Presentation.CanShowContentHeading == true;
        MountContentHeadingControls(supported);
        if (ContentHeadingSwitch is null || ContentHeadingSubtitle is null || ContentHeadingRename is null) return;
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
        if (!_renderingSettings && ContentHeadingSwitch is { } control) ChangeSetting(PosterSetting.ContentHeadingVisibility, control.IsOn ? 1 : 0, finish: true);
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
    internal void SetContentHeadingForProbe(bool visible)
    {
        if (ContentHeadingSwitch is null) throw new InvalidOperationException("Template has no heading component.");
        ContentHeadingSwitch.IsOn = visible;
    }
    internal bool ContentHeadingControlsForProbe(bool visible) => ContentHeadingFolder?.Visibility == Visibility.Visible &&
        ContentHeadingSwitch?.IsEnabled == true && ContentHeadingSwitch.IsOn == visible;
}
