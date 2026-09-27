using DesktopUp.Domain;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Action<string>? _applyCenterTheme;
    private string? _renderedCenterTheme;
    private ContentDialog? _activeTemplateDialog;
    private void TrackTemplateDialog(ContentDialog dialog)
    {
        _activeTemplateDialog = dialog;
        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeTemplateDialog, dialog)) _activeTemplateDialog = null;
        };
    }
    internal void ConfigureCenterTheme(Action<string> apply) => _applyCenterTheme = apply;

    private static (string Name, string Background, string Accent, bool Light) CenterPalette(string key) => key switch
    {
        "ink" => ("墨色", "#202326", "#E9B871", false),
        "blue" => ("深海", "#192738", "#93B9E7", false),
        _ => ("纸白", "#F4F1E9", "#755023", true)
    };

    private void RenderCenterTheme()
    {
        string key = _view?.Projection.Settings.Settings.CenterTheme ?? "light";
        if (_renderedCenterTheme == key) return;
        _renderedCenterTheme = key;
        foreach (string dictionaryKey in new[] { "Light", "Default" })
        {
            var palette = CenterPalette(key == "system" ? dictionaryKey == "Light" ? "light" : "ink" : key);
            var dictionary = (ResourceDictionary)UiCRoot.Resources.ThemeDictionaries[dictionaryKey];
            var bg = SampleBrush(palette.Background).Color;
            var text = SampleBrush(palette.Light ? "#111416" : "#F3F4F0").Color;
            void Set(string name, global::Windows.UI.Color color)
            {
                // ResourceDictionary lookup can fall back to framework resources. Never mutate
                // a brush obtained that way: native styles may share it across windows/themes.
                var owned = dictionary.FirstOrDefault(entry => Equals(entry.Key, name)).Value;
                if (owned is SolidColorBrush brush) brush.Color = color;
                else dictionary[name] = new SolidColorBrush(color);
            }
            static global::Windows.UI.Color Mix(global::Windows.UI.Color a, global::Windows.UI.Color b, double ratio) =>
                global::Windows.UI.Color.FromArgb(255, (byte)Math.Round(a.R + (b.R - a.R) * ratio),
                    (byte)Math.Round(a.G + (b.G - a.G) * ratio), (byte)Math.Round(a.B + (b.B - a.B) * ratio));
            Set("UiCPanelBrush", bg); Set("UiCTextBrush", text);
            Set("UiCSidebarBrush", Mix(bg, Colors.Black, palette.Light ? .055 : .19));
            Set("UiCFolderBrush", Mix(bg, palette.Light ? Colors.Black : Colors.White, palette.Light ? .035 : .045));
            Set("UiCBorderBrush", Mix(bg, text, .2)); Set("UiCAccentBrush", SampleBrush(palette.Accent).Color);
            foreach (string name in new[] { "ExpanderHeaderBackground", "ExpanderContentBackground", "ComboBoxBackground", "TextControlBackground", "ButtonBackground", "ComboBoxDropDownBackground" })
                Set(name, Mix(bg, palette.Light ? Colors.Black : Colors.White, palette.Light ? .035 : .045));
            foreach (string name in new[] { "ExpanderHeaderBorderBrush", "ExpanderContentBorderBrush", "TextControlBorderBrush", "ComboBoxBorderBrush", "ButtonBorderBrush" })
                Set(name, Mix(bg, text, .2));
            foreach (string name in new[] { "TextControlBorderBrushFocused", "SliderTrackValueFill", "SliderThumbBackground", "ToggleSwitchFillOn", "CheckBoxCheckBackgroundFillChecked",
                "ToggleButtonForegroundChecked", "ToggleButtonForegroundCheckedPointerOver", "ToggleButtonForegroundCheckedPressed", "ToggleButtonBorderBrushChecked" })
                Set(name, SampleBrush(palette.Accent).Color);
            foreach (string name in new[] { "ToggleButtonBackgroundChecked", "ToggleButtonBackgroundCheckedPointerOver", "ToggleButtonBackgroundCheckedPressed" })
                Set(name, Mix(bg, SampleBrush(palette.Accent).Color, .12));
            foreach (string state in new[] { "", "PointerOver", "Pressed", "Disabled" })
            {
                double shade = state switch { "PointerOver" => .08, "Pressed" => .12, _ => palette.Light ? .035 : .045 };
                Set("ButtonBackground" + state, Mix(bg, palette.Light ? Colors.Black : Colors.White, shade));
                Set("ButtonForeground" + state, state == "Disabled" ? Mix(bg, text, .55) : text);
                Set("ButtonBorderBrush" + state, Mix(bg, text, state == "Disabled" ? .12 : .2));
            }
            foreach (string state in new[] { "", "PointerOver", "Pressed" }) Set("DropDownButtonForegroundSecondary" + state, text);
        }
        UiCRoot.RequestedTheme = key == "system" ? ElementTheme.Default : CenterPalette(key).Light ? ElementTheme.Light : ElementTheme.Dark;
        if (_activeTemplateDialog is { } dialog) dialog.RequestedTheme = UiCRoot.RequestedTheme;
        UiCThemeButton.Content = "◧ " + (key == "system" ? "跟随系统" : CenterPalette(key).Name) + " ⌄";
        RenderTemplateSamples(); RenderWorkspaceChoices();
    }

    private ContentDialog PaperDialog(string title, object content, string close = "关闭", string? primary = null, string? secondary = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot, Title = title, Content = content,
            CloseButtonText = close, PrimaryButtonText = primary ?? "", SecondaryButtonText = secondary ?? "",
            RequestedTheme = UiCRoot.ActualTheme, Background = UiCPaperSurface.Background,
            Foreground = PosterTitleText.Foreground, BorderBrush = UiCTemplateFolder.BorderBrush,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12)
        };
        ApplyPaperDialogStyle(dialog);
        TrackTemplateDialog(dialog);
        return dialog;
    }

    private void ApplyPaperDialogStyle(ContentDialog dialog)
    {
        dialog.RequestedTheme = UiCRoot.RequestedTheme;
        dialog.Background = UiCPaperSurface.Background;
        dialog.Foreground = PosterTitleText.Foreground;
        dialog.BorderBrush = UiCTemplateFolder.BorderBrush;
        dialog.CornerRadius = new CornerRadius(12);
        var primaryStyle = new Style(typeof(Button)) { BasedOn = (Style)UiCRoot.Resources["UiCAction"] };
        primaryStyle.Setters.Add(new Setter(Control.BackgroundProperty, UiCFolderTab.Foreground));
        primaryStyle.Setters.Add(new Setter(Control.ForegroundProperty, UiCPaperSurface.Background));
        primaryStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        dialog.PrimaryButtonStyle = primaryStyle;
        dialog.SecondaryButtonStyle = dialog.CloseButtonStyle = (Style)UiCRoot.Resources["UiCAction"];
        foreach (var entry in UiCRoot.Resources) dialog.Resources[entry.Key] = entry.Value;
        dialog.Resources["ContentDialogBackground"] = UiCPaperSurface.Background;
        dialog.Resources["ContentDialogTopOverlay"] = UiCPaperSurface.Background;
        dialog.Resources["ContentDialogMaxWidth"] = Math.Max(320d, Math.Min(820d, UiCRoot.ActualWidth - 40));
        dialog.Resources["ContentDialogMinWidth"] = Math.Max(280d, Math.Min(680d, UiCRoot.ActualWidth - 60));
    }

    private async void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (_templateReferenceOpen || Content.XamlRoot is null) return;
        _templateReferenceOpen = true;
        try
        {
            var body = new StackPanel { Spacing = 18 };
            body.Children.Add(new TextBlock { Text = "控制中心", FontSize = 11, Opacity = .65 });
            var cards = new Grid { ColumnSpacing = 12 };
            foreach (string key in new[] { "light", "ink", "blue" })
            {
                var palette = CenterPalette(key);
                int index = cards.Children.Count; cards.ColumnDefinitions.Add(new ColumnDefinition());
                var preview = new StackPanel { Spacing = 15, Padding = new Thickness(14), MinHeight = 112 };
                preview.Children.Add(new TextBlock { Text = "DesktopUp", FontSize = 14, Foreground = SampleBrush(palette.Light ? "#111416" : "#F3F4F0") });
                preview.Children.Add(new Border { Height = 5, Width = 55, HorizontalAlignment = HorizontalAlignment.Left, Background = SampleBrush(palette.Accent), CornerRadius = new(2) });
                preview.Children.Add(new TextBlock { Text = palette.Name, Foreground = SampleBrush(palette.Accent) });
                var button = new Button { Content = preview, Background = SampleBrush(palette.Background), Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    BorderThickness = new(_renderedCenterTheme == key ? 2 : 1), BorderBrush = SampleBrush(palette.Accent) };
                button.Click += (_, _) => _applyCenterTheme?.Invoke(key);
                Grid.SetColumn(button, index); cards.Children.Add(button);
            }
            body.Children.Add(cards);
            var system = new Button { Content = "跟随系统", Style = (Style)UiCRoot.Resources["UiCAction"] };
            system.Click += (_, _) => _applyCenterTheme?.Invoke("system"); body.Children.Add(system);
            body.Children.Add(new TextBlock { Text = "只换控制中心的颜色，海报样式不变。", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
            await PaperDialog("主题", body).ShowAsync();
        }
        catch (InvalidOperationException) { CommonSettingsNotice.Text = "请先关闭当前对话框。"; }
        finally { _templateReferenceOpen = false; }
    }
}
