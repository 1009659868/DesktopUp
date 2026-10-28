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
        // Use the native caption once; the workspace does not draw a second application titlebar.
        ExtendsContentIntoTitleBar = false;
        Title = DistributionBuild.DisplayName + " · 控制中心";
        // Closed notices must release their layout space as well as hide their content.
        StateInfoBar.Visibility = Visibility.Collapsed;
        StateInfoBar.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (_, _) =>
            StateInfoBar.Visibility = StateInfoBar.IsOpen ? Visibility.Visible : Visibility.Collapsed);
        LocateNotice.Visibility = Visibility.Collapsed;
        LocateNotice.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) =>
            LocateNotice.Visibility = string.IsNullOrEmpty(LocateNotice.Text) ? Visibility.Collapsed : Visibility.Visible);
        InitializeEditing();
        InitializeSettings();
        InitializeUiCSample();
        InitializeTemplateParameters();
        InitializeTitleColors();
        InitializeSurfaceControls();
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
        if (_view?.SelectedPosterId != view.SelectedPosterId) CancelSettingsPreview();
        _view = view;
        RefreshTemplateCatalog();
        RenderCenterTheme();
        SynchronizeEditing(view);
        RenderSettings();
        RenderUiCTemplateSummary();
        RenderContentLayout();
        QueueContentRefresh();
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
            : $"模板：{presentation.EffectiveTemplateKey} · 图标基准 {presentation.Parameters.Content.IconSizeDip:0.#} DIP（contain）\n" +
              "卡片几何由独立跨格布局提供；旧等格宽高/间距保留兼容，不再控制卡片排版。";
        SettingsProjection settings = view.Projection.Settings;
        SettingsDetailText.Text = $"动态效果：{(settings.Settings.DynamicEffectsPaused ? "已暂停" : "运行中")} · 性能模式：{settings.Settings.PerformanceMode}";
        PauseButton.Content = settings.Settings.DynamicEffectsPaused ? "恢复提示动效" : "暂停提示动效";
        SettingsDetailText.Text += " · 场景装饰与提示；不改变自动收展设置";
        PauseButton.IsEnabled = true;
        RevisionText.Text = $"committed revision {view.Projection.Revision} · settings {settings.EntityVersion}";
        StateInfoBar.Severity = commandError is null ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        StateInfoBar.IsOpen = commandError is not null || view.SelectedPoster is null || view.GloballyHidden;
        if (view.SelectedPoster is not { } poster)
        {
            StateInfoBar.Title = view.Posters.Count == 0 ? "暂无海报" : "请选择海报";
            StateInfoBar.Message = commandError ?? view.Notice ?? "控制中心与托盘继续运行；可使用左侧新增海报。";
            PosterTitleText.Text = view.Posters.Count == 0 ? "暂无海报" : "请选择海报";
            PosterIdentityText.Text = string.Empty;
            PosterDetailText.Text = "没有选中的目标，海报操作已禁用。";
            RuntimeDetailText.Text = string.Empty;
            LockButton.IsEnabled = CollapseButton.IsEnabled = VisibilityButton.IsEnabled = RemoveButton.IsEnabled = false;
            return;
        }
        StateInfoBar.Title = commandError is null ? "状态已同步" : "操作未应用";
        StateInfoBar.Message = commandError ?? (view.GloballyHidden ? "托盘已临时隐藏全部海报；新增和设置仍保存，恢复显示后生效。" : $"仅操作所选海报；目录共 {view.Posters.Count} 张。");
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
        CollapseButton.IsEnabled = poster.CanToggleCollapsed && !view.GloballyHidden;
        if (_editor.IsPending) LockButton.IsEnabled = CollapseButton.IsEnabled = VisibilityButton.IsEnabled = RemoveButton.IsEnabled = false;
    }

    private void PosterSelector_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_applyingView && PosterSelector.SelectedItem is ControlCenterPosterRow row)
            _selectPoster(row.PosterId);
    }

    private void Send(PosterStateCommandKind kind)
    {
        if (_view?.SelectedPosterId is { } id && (HasPosterDraft(id) || DesktopInteractionConflicts(id)))
        { CommonSettingsNotice.Text = "请先完成或取消当前海报操作。"; return; }
        if (_view?.CreatePosterCommand(kind, $"controlcenter-{kind}-{Guid.NewGuid():N}") is { } command)
            _posterCommand(command);
    }

    private void LockButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetLocked);
    private void CollapseButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetCollapsed);
    private void VisibilityButton_Click(object sender, RoutedEventArgs args) => Send(PosterStateCommandKind.SetVisible);
    private void PauseButton_Click(object sender, RoutedEventArgs args) => _toggleDynamicEffectsPaused();

    private void RemoveButton_Click(object sender, RoutedEventArgs args)
    {
        if (_view?.SelectedPosterId is { } id && (HasPosterDraft(id) || DesktopInteractionConflicts(id)))
        { CommonSettingsNotice.Text = "请先完成或取消当前海报操作。"; return; }
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
        ApplyPaperDialogStyle(dialog);
        _removalDialog = dialog;
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        finally { if (ReferenceEquals(_removalDialog, dialog)) _removalDialog = null; }
    }
    internal void CancelRemovalDialog() => _removalDialog?.Hide();
    internal bool IsRemovalDialogLoaded => _removalDialog?.IsLoaded == true;
}
