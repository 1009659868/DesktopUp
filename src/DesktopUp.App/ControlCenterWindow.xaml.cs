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
    private readonly Action<PosterRemovalRequest> _requestRemoval;
    private ContentDialog? _removalDialog;
    private readonly Func<PosterSurfaceProjection, EffectivePosterLayout?> _effectiveLayout;

    public ControlCenterWindow(Action<string> selectPoster, Action<PosterStateCommand> posterCommand,
        Action toggleDynamicEffectsPaused, Action<PosterRemovalRequest> requestRemoval,
        Func<PosterSurfaceProjection, EffectivePosterLayout?> effectiveLayout)
    {
        _selectPoster = selectPoster;
        _posterCommand = posterCommand;
        _toggleDynamicEffectsPaused = toggleDynamicEffectsPaused;
        _requestRemoval = requestRemoval;
        _effectiveLayout = effectiveLayout;
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

    internal bool VerifyProjection(DesktopUpProjection expected) => _view?.Projection.Revision == expected.Revision &&
        PosterSelector.ItemsSource is IReadOnlyList<ControlCenterPosterRow> rows &&
        rows.Select(row => row.PosterId).SequenceEqual(expected.Posters.Select(p => p.Poster.PosterId)) &&
        PosterTitleText.Text == (_view.SelectedPoster?.Poster.Title ?? "暂无海报");

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
        var presentation = view.SelectedPoster?.Presentation;
        PresentationDetailText.Text = presentation is null ? "" : presentation.IsFallback
            ? "参数安全回退：" + presentation.Warning
            : $"模板：{presentation.EffectiveTemplateKey} · 图标 {presentation.Parameters.Content.IconSizeDip:0.#} DIP · " +
              $"单元最小宽 {presentation.Parameters.Content.ItemWidthDip:0.#} DIP · 间距 {presentation.Parameters.Content.GapDip:0.#} DIP\n" +
              $"完整行高 {presentation.Metrics.CompleteItemRowDip:0.#} DIP · 参数{(presentation.IsLegacy ? "兼容读取，原文保留" : "v1")}（编辑面板后续接入）";
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
            LockButton.IsEnabled = CollapseButton.IsEnabled = VisibilityButton.IsEnabled = RemoveButton.IsEnabled = false;
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
        if (_effectiveLayout(poster) is { IsConstrained: true } effective)
            RuntimeDetailText.Text += $" · 空间受限，当前临时高度 {effective.HeightDip:0.#} DIP；保存高度 {poster.Poster.ExpandedHeightDip:0.#} DIP 未改变";
        LockButton.Content = poster.Poster.IsLocked ? "解除锁定" : "锁定";
        CollapseButton.Content = poster.Poster.IsCollapsed ? "展开" : "收起";
        VisibilityButton.Content = poster.Poster.IsVisible ? "隐藏" : "显示";
        LockButton.IsEnabled = VisibilityButton.IsEnabled = true;
        RemoveButton.IsEnabled = true;
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

    private void RemoveButton_Click(object sender, RoutedEventArgs args)
    {
        if (_view?.SelectedPoster is { } target)
            _requestRemoval(new(target.Poster.PosterId, target.EntityVersion, StateCommandSource.ControlCenter));
    }
    internal async Task<bool> ShowRemovalDialogAsync(PosterRemovalTicket ticket)
    {
        if (_removalDialog is not null || Content.XamlRoot is null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot, Title = "删除海报？", PrimaryButtonText = "删除海报",
            CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer { MaxHeight = 360, Content = new TextBlock
                { Text = ticket.Message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } }
        };
        _removalDialog = dialog;
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        finally { if (ReferenceEquals(_removalDialog, dialog)) _removalDialog = null; }
    }
    internal void CancelRemovalDialog() => _removalDialog?.Hide();
    internal bool IsRemovalDialogLoaded => _removalDialog?.IsLoaded == true;
}
