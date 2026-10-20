using DesktopUp.Application;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private StartupRegistrationService? _startupRegistration;
    private Func<bool, StateCommandResult>? _saveStartupPreference;
    private bool _renderingStartup;
    internal void ConfigureStartup(StartupRegistrationService registration, Func<bool, StateCommandResult> save)
    {
        _startupRegistration = registration; _saveStartupPreference = save;
        UiCGlobalSettings.Flyout.Opening += (_, _) => RefreshStartup();
        Activated += (_, args) => { if (args.WindowActivationState != WindowActivationState.Deactivated) RefreshStartup(); };
        RefreshStartup();
    }
    private void RefreshStartup(StartupRegistrationSnapshot? snapshot = null, string? error = null)
    {
        if (_startupRegistration is null) return;
        snapshot ??= _startupRegistration.Read();
        _renderingStartup = true;
        try
        {
            StartupToggle.IsOn = snapshot.RegisteredHere;
            StartupToggle.IsEnabled = snapshot.CanChange;
            StartupStatusText.Text = error is null ? snapshot.Message : error + "\n" + snapshot.Message;
        }
        finally { _renderingStartup = false; }
    }
    private void StartupToggle_Toggled(object sender, RoutedEventArgs args)
    {
        if (_renderingStartup || _startupRegistration is null) return;
        var result = _startupRegistration.SetEnabled(StartupToggle.IsOn);
        string? error = result.Error;
        if (result.Succeeded && _saveStartupPreference is not null)
        {
            try
            {
                if (!_saveStartupPreference(result.Snapshot.RegisteredHere).Succeeded)
                    error = "系统登记已改变，但偏好保存未完成；以实际读回状态为准。";
            }
            catch (Exception) { error = "偏好保存未完成；以系统登记的实际状态为准。"; }
        }
        RefreshStartup(result.Snapshot, error);
    }
    private async void StartupSettings_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            if (!await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")))
                StartupStatusText.Text = "无法打开系统设置，请在 Windows 设置 → 应用 → 启动中查看。";
        }
        catch (Exception) { StartupStatusText.Text = "无法打开系统设置，请在 Windows 设置 → 应用 → 启动中查看。"; }
    }
}
