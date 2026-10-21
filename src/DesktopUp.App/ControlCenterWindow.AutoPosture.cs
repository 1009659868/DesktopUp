using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly ControlCenterSettingsSession _settings = new();
    private bool _renderingSettings, _settingsHeld, _previewScheduled, _drainingSettings;
    private DispatcherQueueTimer? _settingsTimer;
    private IDisposable? _settingsTimerLease;
    private Func<PosterSettingEdit, bool>? _previewSetting;
    private Action? _cancelVisuals;
    private Func<string, string>? _locate;
    private SliderPresetMarks? _scaleMarks, _spacingMarks;
    internal bool IsSettingsInteractionActive => _lampColorFlyout is not null || _portableBusy || _settings.ProtectsInteraction || _editor.ProtectsInteraction || _catalogDialog is not null || IsContentFieldDraftActive;
    internal bool HasPosterDraft(string id) => _lampColorTarget == id || _portableBusy || _settings.OwnsPoster(id) || HasContentFieldDraft(id) ||
        _editor.ProtectsInteraction && _editor.PosterId == id || _catalogDialog is not null && _view?.SelectedPosterId == id;
    private bool DesktopInteractionConflicts(string id) => _contentRuntime?.Invoke()?.IsSpatialBusy == true;
    internal event Action? SettingsInteractionChanged;

    internal void ConfigureUsability(Func<PosterSettingEdit, bool> preview, Action cancel, Func<string, string> locate, IResourceLedger ledger)
    {
        _previewSetting = preview; _cancelVisuals = cancel; _locate = locate;
        _settingsTimerLease = ledger.Acquire("timer.control-center-settle", "window-session");
    }

    private void InitializeSettings()
    {
        SettingsTabs.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key != global::Windows.System.VirtualKey.Escape || _settings.Preview is null) return;
            CancelSettingsPreview(); e.Handled = true;
        }), true);
        // XBF can compress decimal literals to float32. Assign semantic bounds as doubles,
        // otherwise the exact 0.8 preset is less than XAML's 0.800000011920929 minimum.
        ScaleSlider.Minimum = PosterContentPreferencePolicy.MinimumScale;
        ScaleSlider.Maximum = PosterContentPreferencePolicy.MaximumScale;
        SpacingSlider.Minimum = PosterContentPreferencePolicy.MinimumSpacing;
        SpacingSlider.Maximum = PosterContentPreferencePolicy.MaximumSpacing;
        ScaleSlider.StepFrequency = .001;
        SpacingSlider.StepFrequency = .01;
        _settingsTimer = DispatcherQueue.CreateTimer();
        _settingsTimer.IsRepeating = false;
        _settingsTimer.Interval = TimeSpan.FromMilliseconds(PosterContentPreferencePolicy.KeyboardSettleMilliseconds);
        _settingsTimer.Tick += SettingsSettled;
        _scaleMarks = new(ScaleSlider, ScaleMarks, ScaleLabels, new[] { "小", "标准", "大" }.Zip(PosterContentPreferencePolicy.ScalePresets, (label, value) => (label, value)).ToArray());
        _spacingMarks = new(SpacingSlider, SpacingMarks, SpacingLabels, new[] { "紧凑", "标准", "宽松" }.Zip(PosterContentPreferencePolicy.SpacingPresets, (label, value) => (label, value)).ToArray());
        ConnectSlider(ScaleSlider, PosterSetting.Scale, _scaleMarks);
        ConnectSlider(SpacingSlider, PosterSetting.Spacing, _spacingMarks);
        // Let equal star columns measure their children at the current available width. Feeding
        // ActualWidth back into child Width can preserve the old wide measure during a shrink.
        ScaleHost.MaxWidth = SpacingHost.MaxWidth = 320;
        AutoPostureDelay.LostFocus += (_, _) => FinishSettings();
        Closed += (_, _) =>
        {
            _settingsTimer.Stop(); _settingsTimer.Tick -= SettingsSettled; _settingsTimer = null;
            _settingsTimerLease?.Dispose(); _settingsTimerLease = null;
            _settings.Close(); _cancelVisuals?.Invoke(); DrainSettings();
        };
    }

    private void ConnectSlider(Slider slider, PosterSetting setting, SliderPresetMarks marks)
    {
        slider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            var point = e.GetCurrentPoint(slider);
            if (point.Properties.IsLeftButtonPressed)
            { _settingsHeld = true; _settingsTimer?.Stop(); marks.Begin(e.Pointer.PointerId, point.Position); }
        }), true);
        slider.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, e) =>
            marks.Move(e.Pointer.PointerId, e.GetCurrentPoint(slider).Position)), true);
        void FinishPointer(PointerRoutedEventArgs e)
        {
            if (marks.Complete(e.Pointer.PointerId, e.GetCurrentPoint(slider).Position) is { } preset)
                slider.Value = preset;
            _settingsHeld = false; FinishSettings();
        }
        slider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, e) => FinishPointer(e)), true);
        slider.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) => CancelSettingsPreview()), true);
        slider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, e) =>
        {
            if (!_settingsHeld) return;
            if (e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) CancelSettingsPreview();
            else FinishPointer(e);
        }), true);
        slider.ValueChanged += (_, _) =>
        {
            if (!_renderingSettings) ChangeSetting(setting, slider.Value);
        };
    }

    private void ChangeSetting(PosterSetting setting, double number = 0, string text = "", bool finish = false)
    {
        if (_renderingSettings || _editingClosed || _view?.SelectedPoster is not { } target) return;
        if (_lampColorFlyout is not null)
        { RenderSettings(); CommonSettingsNotice.Text = "请先应用或取消颜色选择。"; return; }
        if (DesktopInteractionConflicts(target.Poster.PosterId))
        { RenderSettings(); CommonSettingsNotice.Text = "海报正在操作，请先完成或取消；当前调节未开始。"; return; }
        if (_editor.ProtectsInteraction || IsContentFieldDraftActive)
        { RenderSettings(); CommonSettingsNotice.Text = "请先应用或取消当前成组修改。"; return; }
        bool valid = _settings.Update(new(target, setting, number, text, Recipe: setting == PosterSetting.Template
            ? _templateCatalog.FirstOrDefault(r => r.Id == text) : null));
        if (valid && _settings.Preview is { } preview && _contentRuntime?.Invoke()?.ValidateControlCenterSetting(preview) is { } error)
        { RejectSettingsPreview(error); valid = false; }
        if (valid && setting is not (PosterSetting.AutoEnabled or PosterSetting.AutoDelay)) QueueVisualPreview();
        if (valid && finish) FinishSettings();
        else if (valid && !_settingsHeld) { _settingsTimer?.Stop(); _settingsTimer?.Start(); }
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }

    private void ChangeLampSetting(TemplateParameterValues value, bool finish)
    {
        if (_renderingSettings || _editingClosed || _view?.SelectedPoster is not { } target) return;
        if (_editor.ProtectsInteraction || IsContentFieldDraftActive || DesktopInteractionConflicts(target.Poster.PosterId))
        { RenderSettings(); CommonSettingsNotice.Text = "请先完成或取消当前海报操作。"; return; }
        bool valid = _settings.Update(new(target, PosterSetting.TemplateParameters, TemplateValues: value));
        if (valid && _settings.Preview is { } edit && _contentRuntime?.Invoke()?.ValidateControlCenterSetting(edit) is { } error)
        { _settings.RejectPreview(error); valid = false; }
        if (valid) QueueVisualPreview();
        if (valid && finish) FinishSettings();
        else if (valid && !_settingsHeld) { _settingsTimer?.Stop(); _settingsTimer?.Start(); }
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }

    private void QueueVisualPreview()
    {
        if (_previewScheduled) return;
        _previewScheduled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _previewScheduled = false;
            if (_editingClosed || !_settings.IsPreviewValid || _settings.Preview is not { } edit) return;
            if (_previewSetting?.Invoke(edit) != true)
            {
                if (edit.Setting is PosterSetting.Scale or PosterSetting.Spacing)
                {
                    RejectSettingsPreview("当前宿主无法安全呈现这次调整，已恢复原值。");
                    RenderSettings(); SettingsInteractionChanged?.Invoke();
                }
                else CommonSettingsNotice.Text = "当前宿主未就绪或正在交互；结束输入后仍会校验并保存配置。";
            }
        });
    }
    private void SettingsSettled(DispatcherQueueTimer sender, object args) { if (!_settingsHeld) FinishSettings(); }
    private void FinishSettings()
    {
        _settingsTimer?.Stop();
        if (_settings.Preview is { } edit && _contentRuntime?.Invoke()?.ValidateControlCenterSetting(edit) is { } error)
            RejectSettingsPreview(error);
        _settings.Finish();
        DrainSettings();
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }
    private void RejectSettingsPreview(string error)
    {
        // Sliders have no editable invalid text to preserve. A rejected value must not keep
        // owning the poster and silently disable its manual frame menu.
        bool discard = _settings.Preview?.Setting is PosterSetting.Scale or PosterSetting.Spacing;
        _settings.RejectPreview(error, discard);
        if (discard) { _settingsTimer?.Stop(); _cancelVisuals?.Invoke(); }
    }
    private void DrainSettings()
    {
        if (_drainingSettings) return;
        _drainingSettings = true;
        try { while (!_settings.IsPending && _settings.TakeNext() is { } command) _posterCommand(command); }
        finally { _drainingSettings = false; }
    }
    private void CancelSettingsPreview()
    {
        _surfacePointerHeld = false;
        CloseLampColorPicker();
        _scaleMarks?.Cancel(); _spacingMarks?.Cancel();
        _lampHeldSlider = null;
        _settingsHeld = false; _settingsTimer?.Stop();
        _settings.CancelPreview(); _cancelVisuals?.Invoke();
        RenderSettings(); SettingsInteractionChanged?.Invoke();
    }
    internal bool CompleteAutoPosture(StateCommandResult result)
    {
        if (!_settings.Complete(result)) return false;
        _cancelVisuals?.Invoke();
        if (_settings.Preview is not null) QueueVisualPreview();
        DrainSettings(); RenderSettings(); SettingsInteractionChanged?.Invoke(); return true;
    }

    private void RenderSettings()
    {
        if (_editingClosed || ScaleSlider is null) return;
        _renderingSettings = true;
        try
        {
            var target = _view?.SelectedPoster;
            var active = _settings.Preview?.PosterId == target?.Poster.PosterId ? _settings.Preview : null;
            SelectTemplateKey(active?.Setting == PosterSetting.Template ? active.Text : target?.Poster.TemplateKey);
            var prefs = target?.Presentation.Parameters.Preferences ?? new();
            ScaleSlider.Value = active?.Setting == PosterSetting.Scale ? active.Number : prefs.Scale;
            SpacingSlider.Value = active?.Setting == PosterSetting.Spacing ? active.Number : prefs.Spacing;
            ScaleLabel.Text = $"内容尺度 · {ScaleSlider.Value:0.###}×";
            SpacingLabel.Text = $"内容疏密 · 间距 {SpacingSlider.Value:0.00}×";
            AutoPostureSwitch.IsOn = active?.Setting == PosterSetting.AutoEnabled ? active.Number == 1 : target?.AutoPosture.Enabled == true;
            if (active?.Setting != PosterSetting.AutoDelay)
                AutoPostureDelay.Value = target?.AutoPosture.LeaveDelaySeconds ?? 3;
            bool enabled = target is not null && !_editor.ProtectsInteraction && !IsContentFieldDraftActive;
            TitleVisibilitySwitch.IsOn = active?.Setting == PosterSetting.TitleVisibility ? active.Number == 1 :
                target?.Presentation.Parameters.IsTitleVisible ?? true;
            TitleVisibilitySwitch.IsEnabled = enabled && target?.Presentation.CanEdit == true &&
                !_settings.ProtectsInteraction && _lampColorFlyout is null;
            AutoPostureSwitch.IsEnabled = AutoPostureDelay.IsEnabled = enabled;
            ScaleSlider.IsEnabled = SpacingSlider.IsEnabled = _template.IsEnabled = enabled && target?.Presentation.CanEdit == true;
            _scaleMarks?.Refresh(); _spacingMarks?.Refresh();
            LocateButton.IsEnabled = target is not null;
            string? error = _settings.ErrorPosterId == target?.Poster.PosterId ? _settings.Error : null;
            CommonSettingsNotice.Text = error ?? (target is null ? "请先新增或选择海报。" :
                !target.Presentation.CanEdit ? "外观只读：布局或参数不受支持，原配置已保留。" :
                _lampColorFlyout is not null ? "颜色预览 · 应用或取消" :
                _settings.ProtectsInteraction ? "调整中 · 完成后自动保存 · Esc 取消当前修改" : "已保存 · 仅当前海报");
            AutoPostureInfo.IsOpen = error is not null;
            AutoPostureInfo.Severity = InfoBarSeverity.Warning; AutoPostureInfo.Message = error ?? "";
            RenderTemplateParameters(active?.Setting is (PosterSetting.Lamp or PosterSetting.TemplateParameters) ? active.TemplateValues ?? (active.Lamp is { } oldValues ? LegacyLampValues.ToValues(oldValues) : null) : null);
            RenderTemplateSamples();
            RenderTitleColors();
            RenderContentHeading();
            RenderSurfaceControls();
            UpdateContentDraftPresentation();
        }
        finally { _renderingSettings = false; }
    }
    private void AutoPosture_Changed(object sender, RoutedEventArgs args)
    { if (!_renderingSettings) ChangeSetting(PosterSetting.AutoEnabled, AutoPostureSwitch.IsOn ? 1 : 0, finish: true); }
    private void TitleVisibility_Changed(object sender, RoutedEventArgs args)
    { if (!_renderingSettings) ChangeSetting(PosterSetting.TitleVisibility, TitleVisibilitySwitch.IsOn ? 1 : 0, finish: true); }
    private void AutoPostureDelay_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_renderingSettings) ChangeSetting(PosterSetting.AutoDelay, text: sender.Value.ToString("R", CultureInfo.InvariantCulture));
    }
    private void Locate_Click(object sender, RoutedEventArgs args) => LocateSelected();
    internal void LocateSelected()
    {
        if (_view?.SelectedPosterId is { } id) LocateNotice.Text = _locate?.Invoke(id) ?? "桌面尚未就绪。";
    }
}
