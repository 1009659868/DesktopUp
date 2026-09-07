using DesktopUp.Application;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow : Window
{
    private readonly Action<string> _selectPoster;
    private readonly Action<PosterStateCommand> _posterCommand;
    private readonly Action _toggleDynamicEffectsPaused;
    private ControlCenterView? _view;
    private bool _applyingView;

    public ControlCenterWindow(Action<string> selectPoster, Action<PosterStateCommand> posterCommand,
        Action toggleDynamicEffectsPaused)
    {
        _selectPoster = selectPoster;
        _posterCommand = posterCommand;
        _toggleDynamicEffectsPaused = toggleDynamicEffectsPaused;
    }

    internal void InitializeView()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(980, 640));
        AppWindow.IsShownInSwitchers = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }
    }

    public void ApplyView(ControlCenterView view, string? commandError = null)
    {
        _view = view;
        _applyingView = true;
        try
        {
            // Keep selection keyed by identity, never by title or the old list index.
            var rows = PosterSelector.ItemsSource as IReadOnlyList<ControlCenterPosterRow>;
            if (rows is null || !rows.SequenceEqual(view.Posters))
            {
                rows = view.Posters;
                PosterSelector.ItemsSource = rows;
            }
            // A selection-only change must not rebuild the list or lose keyboard focus.
            PosterSelector.SelectedItem = rows.FirstOrDefault(p => p.PosterId == view.SelectedPosterId);
        }
        finally { _applyingView = false; }
        CatalogCountText.Text = $"海报 · {view.Posters.Count}";
        SettingsProjection settings = view.Projection.Settings;
        SettingsDetailText.Text = $"动态效果：{(settings.Settings.DynamicEffectsPaused ? "已暂停" : "运行中")} · 性能模式：{settings.Settings.PerformanceMode}";
        PauseButton.Content = settings.Settings.DynamicEffectsPaused ? "恢复动态效果" : "暂停动态效果";
        PauseButton.IsEnabled = true;
        RevisionText.Text = $"committed revision {view.Projection.Revision} · settings {settings.EntityVersion}";
        StateInfoBar.Severity = commandError is null ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        if (view.SelectedPoster is not { } poster)
        {
            StateInfoBar.Title = view.Posters.Count == 0 ? "暂无海报" : "请选择海报";
            StateInfoBar.Message = commandError ?? view.Notice ?? "控制中心与托盘继续运行；新增入口将在后续任务接入。";
            PosterTitleText.Text = view.Posters.Count == 0 ? "暂无海报" : "请选择海报";
            PosterIdentityText.Text = string.Empty;
            PosterDetailText.Text = "没有选中的目标，海报操作已禁用。";
            RuntimeDetailText.Text = string.Empty;
            LockButton.IsEnabled = CollapseButton.IsEnabled = VisibilityButton.IsEnabled = false;
            return;
        }
        StateInfoBar.Title = commandError is null ? "状态已同步" : "操作未应用";
        StateInfoBar.Message = commandError ?? $"仅操作所选海报；目录共 {view.Posters.Count} 张。";
        PosterTitleText.Text = poster.Poster.Title;
        PosterIdentityText.Text = poster.Poster.PosterId;
        RevisionText.Text += $" · poster entity {poster.EntityVersion}";
        PosterDetailText.Text = $"{poster.Poster.WidthDip:0.#} × {poster.Poster.ExpandedHeightDip:0.#} DIP · " +
            $"{(poster.Poster.IsLocked ? "已锁定" : "未锁定")} · {(poster.Poster.IsCollapsed ? "已收起" : "已展开")}";
        RuntimeDetailText.Text = "会话：" + view.Posters.First(p => p.PosterId == poster.Poster.PosterId).RuntimeStatus;
        LockButton.Content = poster.Poster.IsLocked ? "解除锁定" : "锁定";
        CollapseButton.Content = poster.Poster.IsCollapsed ? "展开" : "收起";
        VisibilityButton.Content = poster.Poster.IsVisible ? "隐藏" : "显示";
        LockButton.IsEnabled = VisibilityButton.IsEnabled = true;
        CollapseButton.IsEnabled = poster.CanToggleCollapsed;
    }

    private void PosterSelector_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_applyingView && PosterSelector.SelectedItem is ControlCenterPosterRow row)
            _selectPoster(row.PosterId);
    }

    private void Send(PosterStateCommandKind kind)
    {
        if (_view?.CreatePosterCommand(kind, $"controlcenter-{kind}-{Guid.NewGuid():N}") is { } command)
            _posterCommand(command);
    }

    private void LockButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetLocked);
    private void CollapseButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetCollapsed);
    private void VisibilityButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetVisible);
    private void PauseButton_Click(object sender, RoutedEventArgs args) => _toggleDynamicEffectsPaused();
}
