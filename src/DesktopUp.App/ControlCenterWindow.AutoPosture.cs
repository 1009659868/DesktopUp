using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly ControlCenterAutoPostureEditor _autoEditor = new();
    private bool _renderingAutoPosture, _autoProtection;
    internal bool IsSettingsInteractionActive => _autoEditor.ProtectsInteraction;
    internal event Action? SettingsInteractionChanged;

    private void RenderAutoPosture()
    {
        _renderingAutoPosture = true;
        try
        {
            AutoPostureSwitch.IsOn = _autoEditor.Enabled;
            // Do not rewrite a text field/caret during the frequent session projections.
            if (AutoPostureDelay.Text != _autoEditor.DelayText) AutoPostureDelay.Text = _autoEditor.DelayText;
            AutoPostureSwitch.IsEnabled = AutoPostureDelay.IsEnabled = _autoEditor.PosterId is not null && !_autoEditor.IsPending;
            AutoPostureApply.IsEnabled = _autoEditor.CanApply;
            AutoPostureCancel.IsEnabled = _autoEditor.CanCancel;
            string? error = _autoEditor.ValidationError ?? _autoEditor.Error;
            AutoPostureInfo.Severity = error is not null || _autoEditor.IsStale ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            AutoPostureInfo.Title = _autoEditor.PosterId is null ? "未选择海报" : _autoEditor.IsPending ? "正在应用" : error is not null ? "未应用" :
                _autoEditor.IsStale ? "草稿已过期" : _autoEditor.IsDirty ? "尚未应用" : "已提交设置";
            AutoPostureInfo.Message = error ?? (_autoEditor.PosterId is null ? "请选择海报。" : _autoEditor.IsStale ?
                "此海报已被其他操作修改；应用会校验版本。请取消以载入最新值。" : _autoEditor.ProtectsInteraction ?
                "草稿仅在当前窗口中，自动收展暂时受保护；应用或取消后重新判定。" :
                "控件显示已提交值；仅打开此窗口不会改变姿态或暂停自动收展。");
        }
        finally { _renderingAutoPosture = false; }
        if (_autoProtection != _autoEditor.ProtectsInteraction)
        {
            _autoProtection = _autoEditor.ProtectsInteraction;
            SettingsInteractionChanged?.Invoke();
        }
    }

    private void AutoPosture_Changed(object sender, RoutedEventArgs args) => EditAutoPosture();
    private void AutoPostureDelay_Changing(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        // Capture raw input synchronously so an intervening projection cannot overwrite it.
        // TextChanging can run during layout: only touch the model, not the visual tree.
        if (!_renderingAutoPosture) _autoEditor.Edit(AutoPostureSwitch.IsOn, sender.Text);
    }
    private void AutoPostureDelay_Changed(object sender, TextChangedEventArgs args) => RenderAutoPosture();
    private void EditAutoPosture()
    {
        if (_renderingAutoPosture || AutoPostureDelay is null) return;
        _autoEditor.Edit(AutoPostureSwitch.IsOn, AutoPostureDelay.Text);
        RenderAutoPosture();
    }
    private void AutoPostureApply_Click(object sender, RoutedEventArgs args)
    {
        EditAutoPosture(); // Validate the actual controls even if a text notification was queued.
        if (_autoEditor.BeginApply($"controlcenter-auto-{Guid.NewGuid():N}") is not { } command) return;
        RenderAutoPosture(); // Freeze before synchronous projection/result callbacks can re-enter.
        _posterCommand(command);
    }
    private void AutoPostureCancel_Click(object sender, RoutedEventArgs args)
    {
        _autoEditor.Cancel(); RenderAutoPosture();
    }
    internal bool CompleteAutoPosture(StateCommandResult result)
    {
        if (!_autoEditor.Complete(result)) return false;
        RenderAutoPosture(); return true;
    }
}
