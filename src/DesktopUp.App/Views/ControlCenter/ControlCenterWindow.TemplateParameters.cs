using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopUp;

internal sealed record LampParameterControlsObservation(bool Visible, bool Enabled, int ControlCount,
    string? PosterId, LampTemplateParameters Values, string Notice)
{
    public int VisualColorCount { get; init; }
    public bool HasTemplateReset { get; init; }
    public bool ColorPickerOpen { get; init; }
}

public sealed partial class ControlCenterWindow
{
    private TemplateParameterSchema _templateParameterSchema = TemplateParameterSchemas.ForTemplate("basic-box")!;
    private Expander? _lampParameterFolder;
    private readonly Dictionary<string, TextBox> _lampColors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Slider> _lampNumbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleSwitch> _lampBooleans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _lampLabels = new(StringComparer.Ordinal);
    private TextBlock? _lampParameterNotice;
    private bool _renderingLampParameters;
    private string? _lampRenderedPosterId;
    private Slider? _lampHeldSlider;
    private Button? _lampReset;
    private IEnumerable<TemplateParameterDefinition> PrivateTemplateProperties => _templateParameterSchema.Properties
        .Where(p => p.Binding is not ("surface.background" or "surface.opacity"));

    private void InitializeTemplateParameters()
    {
        if (_lampParameterFolder is not null) return;
        var fields = new StackPanel { Spacing = 14 };
        // The currently supported template describes its own groups, controls and bounds.
        // Only the capability adapter knows how a binding maps to the stored Lamp section.
        var panels = new List<StackPanel>();
        foreach (var group in PrivateTemplateProperties.GroupBy(p => p.Group))
        {
            var panel = new StackPanel { Spacing = 9 };
            panel.Children.Add(new TextBlock { Text = group.Key, FontSize = 11, Opacity = .65 });
            foreach (var property in group)
            {
                if (property.Kind == TemplateParameterKind.Color) AddLampColor(panel, property);
                else if (property.Kind == TemplateParameterKind.Range) AddLampNumber(panel, property);
                else if (property.Kind == TemplateParameterKind.Boolean) AddLampBoolean(panel, property);
            }
            panels.Add(panel);
        }
        var groups = new Grid { ColumnSpacing = 22, RowSpacing = 14 };
        groups.ColumnDefinitions.Add(new ColumnDefinition()); groups.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        for (int i = 0; i < panels.Count; i++)
        {
            groups.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(panels[i], i); groups.Children.Add(panels[i]);
        }
        fields.Children.Add(groups);
        groups.SizeChanged += (_, _) =>
        {
            bool paired = groups.ActualWidth >= 420;
            groups.ColumnDefinitions[1].Width = paired ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            for (int i = 0; i < panels.Count; i++)
            { Grid.SetColumn(panels[i], paired ? i % 2 : 0); Grid.SetRow(panels[i], paired ? i / 2 : i); }
        };
        _lampReset = new Button { Content = "复位当前模板", Style = (Style)UiCRoot.Resources["UiCAction"], Padding = new Thickness(10, 5, 10, 5) };
        _lampReset.Click += (_, _) => ResetLampParameters();
        AutomationProperties.SetAutomationId(_lampReset, "LampTemplateReset");
        ToolTipService.SetToolTip(_lampReset, "恢复" + _templateParameterSchema.Name + "的声明默认值；保留排布、尺度与疏密。");
        fields.Children.Add(_lampReset);
        var inspect = new Button { Content = "查看配方 { }", Style = (Style)UiCRoot.Resources["UiCAction"],
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
        inspect.Click += (_, _) => ShowTemplateDefinition(); fields.Children.Add(inspect);
        _lampParameterNotice = new TextBlock { FontSize = 12, Opacity = .75, TextWrapping = TextWrapping.Wrap };
        fields.Children.Add(_lampParameterNotice);
        _lampParameterFolder = new Expander { Header = $"动效与参数 · {PrivateTemplateProperties.Count()} 项专属设置", IsExpanded = false, Content = fields,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(_lampParameterFolder, "LampParameters");
        TemplateSection.Children.Add(_lampParameterFolder);
        RenderTemplateParameters();
    }

    private void AddLampBoolean(StackPanel fields, TemplateParameterDefinition property)
    {
        var toggle = new ToggleSwitch { Header = property.Label, OnContent = "开启", OffContent = "关闭" };
        if (property.Hint.Length > 0) ToolTipService.SetToolTip(toggle, property.Hint);
        AutomationProperties.SetAutomationId(toggle, "Lamp_" + property.Key);
        _lampBooleans.Add(property.Key, toggle); fields.Children.Add(toggle);
        toggle.Toggled += (_, _) => { if (LampInputAvailable()) ChangeLampSetting(ReadLampControls(), !_settingsHeld); };
    }

    private void AddLampColor(StackPanel fields, TemplateParameterDefinition property)
    {
        string key = property.Key, label = property.Label;
        var field = new StackPanel { Spacing = 5 };
        field.Children.Add(new TextBlock { Text = label });
        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        var color = new Border { Width = 35, Height = 20, CornerRadius = new CornerRadius(2) };
        var swatch = new Button { Content = color, Style = (Style)UiCRoot.Resources["UiCAction"], Padding = new Thickness(4), MinWidth = 0, VerticalAlignment = VerticalAlignment.Stretch };
        swatch.Click += (_, _) => OpenLampColorPicker(key);
        AutomationProperties.SetAutomationId(swatch, "LampColorChoice_" + key);
        AutomationProperties.SetName(swatch, label + "选择");
        ToolTipService.SetToolTip(swatch, property.Hint.Length == 0 ? "选择" + label : property.Hint);
        _lampSwatches.Add(key, (swatch, color)); line.Children.Add(swatch);
        var box = new TextBox { MaxLength = 7, PlaceholderText = "#RRGGBB", HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        AutomationProperties.SetAutomationId(box, "Lamp_" + key);
        AutomationProperties.SetName(box, label);
        _lampColors.Add(key, box); Grid.SetColumn(box, 1); line.Children.Add(box); field.Children.Add(line); fields.Children.Add(field);
        // Even an unfinished hex value belongs to the existing settings session. Invalid input
        // stays a protected draft there; it is never sent to the renderer as a valid color.
        // TextChanging is synchronous: a later control or FinishSettings in the same
        // dispatcher turn must see this draft, including incomplete/invalid input.
        // TextChanged can arrive after RenderSettings has replaced the text.
        box.TextChanging += (_, _) =>
        {
            if (LampInputAvailable()) ChangeLampSetting(ReadLampControls(), false);
        };
        box.LostFocus += (_, _) => CommitLampColor();
        box.KeyDown += (_, e) =>
        {
            if (e.Key != global::Windows.System.VirtualKey.Enter) return;
            CommitLampColor(); e.Handled = true;
        };
    }

    private void AddLampNumber(StackPanel fields, TemplateParameterDefinition property)
    {
        string key = property.Key, label = property.Label;
        var stack = new StackPanel { Spacing = 0 };
        var text = new TextBlock { Text = label }; _lampLabels.Add(key, text); stack.Children.Add(text);
        var slider = new Slider { Minimum = property.Minimum!.Value, Maximum = property.Maximum!.Value, StepFrequency = property.Step!.Value,
            TickPlacement = Microsoft.UI.Xaml.Controls.Primitives.TickPlacement.None,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        if (property.Hint.Length > 0) ToolTipService.SetToolTip(slider, property.Hint);
        AutomationProperties.SetAutomationId(slider, "Lamp_" + key); AutomationProperties.SetName(slider, label);
        _lampNumbers.Add(key, slider); stack.Children.Add(slider); fields.Children.Add(stack);
        slider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed || !LampInputAvailable()) return;
            _lampHeldSlider = slider; _settingsHeld = true; _settingsTimer?.Stop();
        }), true);
        slider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => FinishLampPointer(slider)), true);
        slider.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) =>
        { _lampHeldSlider = null; CancelSettingsPreview(); }), true);
        slider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, e) =>
        {
            if (!ReferenceEquals(_lampHeldSlider, slider)) return;
            if (e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed)
            { _lampHeldSlider = null; CancelSettingsPreview(); }
            else FinishLampPointer(slider);
        }), true);
        slider.LostFocus += (_, _) => FinishLampPointer(slider);
        slider.ValueChanged += (_, _) =>
        {
            if (LampInputAvailable()) ChangeLampSetting(ReadLampControls(), false);
        };
    }

    private void FinishLampPointer(Slider slider)
    {
        if (!ReferenceEquals(_lampHeldSlider, slider)) return;
        _lampHeldSlider = null; _settingsHeld = false;
        if (LampInputAvailable()) ChangeLampSetting(ReadLampControls(), true);
    }

    private bool LampInputAvailable() => !_renderingLampParameters && !_renderingSettings && !_editingClosed &&
        _lampParameterFolder?.IsEnabled == true && _view?.SelectedPoster?.Poster.PosterId == _lampRenderedPosterId &&
        _view?.SelectedPoster?.Poster.TemplateKey == _templateParameterSchema.TemplateId;

    private void CommitLampColor()
    {
        if (_lampColorFlyout is not null || !LampInputAvailable() || _settings.Preview?.Setting is not (PosterSetting.Lamp or PosterSetting.TemplateParameters)) return;
        ChangeLampSetting(ReadLampControls(), true);
    }

    private TemplateParameterValues ReadLampControls()
    {
        var current = _settings.Preview?.TemplateValues ?? _view?.SelectedPoster?.Presentation.TemplateValues ?? new TemplateParameterValues([]);
        var values = PrivateTemplateProperties.ToDictionary(p => p.Key, p => p.Kind == TemplateParameterKind.Color
            ? TemplateParameterValue.FromColor(_lampColors[p.Key].Text) : p.Kind == TemplateParameterKind.Boolean
            ? TemplateParameterValue.FromBoolean(_lampBooleans[p.Key].IsOn) : TemplateParameterValue.FromNumber(_lampNumbers[p.Key].Value), StringComparer.Ordinal);
        foreach (var p in _templateParameterSchema.Properties.Except(PrivateTemplateProperties)) values[p.Key] = current.Get(p.Binding);
        return TemplateParameterBindingAdapter.ComposeDraft(_templateParameterSchema, values,
            current);
    }

    private void RenderTemplateParameters(TemplateParameterValues? preview = null)
    {
        if (_lampParameterFolder is null || _lampParameterNotice is null) return;
        if (SelectedRecipe is { } unavailable && !SceneCapabilityRegistry.Available(unavailable.Schema.RendererCapability))
        {
            _lampParameterFolder.IsEnabled = false;
            _lampParameterFolder.Visibility = Visibility.Collapsed;
            _lampParameterNotice.Text = "模板模块未安装；原设置保留，恢复模块后可调整。";
            return;
        }
        if (SelectedRecipe is { } recipe && _templateParameterSchema.TemplateId != recipe.Id)
        {
            CloseLampColorPicker(); _lampHeldSlider = null;
            TemplateSection.Children.Remove(_lampParameterFolder); _lampParameterFolder = null;
            _lampColors.Clear(); _lampNumbers.Clear(); _lampLabels.Clear(); _lampSwatches.Clear(); _lampBooleans.Clear();
            _templateParameterSchema = recipe.Schema;
            InitializeTemplateParameters(); return;
        }
        _renderingLampParameters = true;
        try
        {
            var target = _view?.SelectedPoster;
            bool lamp = target?.Poster.TemplateKey == _templateParameterSchema.TemplateId;
            if (_lampRenderedPosterId != target?.Poster.PosterId) { _lampHeldSlider = null; CloseLampColorPicker(); }
            _lampRenderedPosterId = target?.Poster.PosterId;
            _lampParameterFolder.Visibility = lamp && PrivateTemplateProperties.Any() ? Visibility.Visible : Visibility.Collapsed;
            _lampParameterFolder.IsEnabled = lamp && target?.Presentation.CanEdit == true && !_editor.ProtectsInteraction && !IsContentFieldDraftActive;
            var value = preview ?? target?.Presentation.TemplateValues ?? new TemplateParameterValues([]);
            var values = TemplateParameterBindingAdapter.Read(_templateParameterSchema, value);
            foreach (var property in PrivateTemplateProperties)
            {
                var field = values[property.Key];
                if (property.Kind == TemplateParameterKind.Color)
                { if (_lampColors[property.Key].Text != field.Color) _lampColors[property.Key].Text = field.Color!; }
                else if (property.Kind == TemplateParameterKind.Boolean) _lampBooleans[property.Key].IsOn = field.Boolean!.Value;
                else
                {
                    _lampNumbers[property.Key].Value = field.Number!.Value;
                    _lampLabels[property.Key].Text = $"{property.Label} · {field.Number.Value:0.#}{property.Unit}";
                }
            }
            foreach (var (key, swatch) in _lampSwatches)
            {
                if (TryLampColor(_lampColors[key].Text, out var color)) swatch.Chip.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
                swatch.Button.IsEnabled = _lampParameterFolder.IsEnabled && (!_settings.ProtectsInteraction || _lampColorKey == key);
            }
            if (_lampReset is not null) _lampReset.IsEnabled = _lampParameterFolder.IsEnabled && !_settings.ProtectsInteraction && _lampColorFlyout is null;
            string? error = _settings.ErrorPosterId == target?.Poster.PosterId ? _settings.Error : null;
            _lampParameterNotice.Text = error ?? "仅当前海报 · 完成调节后保存";
        }
        finally { _renderingLampParameters = false; }
    }

    internal LampParameterControlsObservation ReadLampParameterControlsForProbe() => new(
        _lampParameterFolder?.Visibility == Visibility.Visible, _lampParameterFolder?.IsEnabled == true,
        _lampColors.Count + _lampNumbers.Count + _lampBooleans.Count, _lampRenderedPosterId,
        _lampParameterFolder is null ? LampTemplatePolicy.Defaults : LegacyLampValues.FromValues(ReadLampControls()), _lampParameterNotice?.Text ?? "")
    { VisualColorCount = _lampSwatches.Count, HasTemplateReset = _lampReset is not null, ColorPickerOpen = _lampColorFlyout is not null };
}
