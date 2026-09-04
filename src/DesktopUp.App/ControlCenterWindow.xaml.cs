using DesktopUp.Application;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public sealed partial class ControlCenterWindow : Window
{
    private readonly Action _toggleLocked;
    private readonly Action _toggleCollapsed;
    private readonly Action _toggleDynamicEffectsPaused;

    public ControlCenterWindow(
        Action toggleLocked,
        Action toggleCollapsed,
        Action toggleDynamicEffectsPaused)
    {
        _toggleLocked = toggleLocked;
        _toggleCollapsed = toggleCollapsed;
        _toggleDynamicEffectsPaused = toggleDynamicEffectsPaused;
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(840, 560));
        AppWindow.IsShownInSwitchers = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }
    }

    public void ApplyProjection(ControlCenterProjection projection)
    {
        PosterSurfaceProjection poster = projection.Poster;
        SettingsProjection settings = projection.Settings;
        StateInfoBar.Title = "状态已同步";
        StateInfoBar.Message =
            $"Control Center projection 与 revision {projection.Revision} 对齐。";
        PosterTitleText.Text = poster.Poster.Title;
        RevisionText.Text =
            $"committed revision {projection.Revision} · poster {poster.PosterRevision} · settings {settings.SettingsRevision}";
        PosterDetailText.Text =
            $"{poster.Poster.WidthDip:0.#} × {poster.Poster.ExpandedHeightDip:0.#} DIP · " +
            $"{(poster.Poster.IsLocked ? "已锁定" : "未锁定")} · " +
            $"{(poster.Poster.IsCollapsed ? "已收起" : "已展开")}";
        SettingsDetailText.Text =
            $"动态效果：{(settings.Settings.DynamicEffectsPaused ? "已暂停" : "运行中")} · " +
            $"性能模式：{settings.Settings.PerformanceMode}";
        LockButton.Content = poster.Poster.IsLocked ? "解除锁定" : "锁定";
        CollapseButton.Content = poster.Poster.IsCollapsed ? "展开" : "收起";
        PauseButton.Content = settings.Settings.DynamicEffectsPaused
            ? "恢复动态效果"
            : "暂停动态效果";
        LockButton.IsEnabled = true;
        CollapseButton.IsEnabled = poster.CanToggleCollapsed;
        PauseButton.IsEnabled = true;
    }

    private void LockButton_Click(object sender, RoutedEventArgs args) =>
        _toggleLocked();

    private void CollapseButton_Click(object sender, RoutedEventArgs args) =>
        _toggleCollapsed();

    private void PauseButton_Click(object sender, RoutedEventArgs args) =>
        _toggleDynamicEffectsPaused();
}
