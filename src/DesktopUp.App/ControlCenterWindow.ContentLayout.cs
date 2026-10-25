using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using global::Windows.System;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Func<PosterSpatialRuntime?>? _contentRuntime;
    private bool _renderingContent, _contentFocused;
    private string? _contentTargetId, _contentActivationTargetId;
    private PosterSpatialRuntime? _contentObservedRuntime;
    private bool _contentRefreshQueued;
    internal void ConfigureContentLayout(Func<PosterSpatialRuntime?> runtime)
    {
        _contentRuntime = runtime;
        _contentObservedRuntime = runtime();
        if (_contentObservedRuntime is not null) _contentObservedRuntime.ContentEditorChanged += ContentEditorChanged;
        ContentFineTune.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, (_, _) => UpdateContentDraftPresentation());
        ContentLayoutPanel.GotFocus += (_, _) => { _contentFocused = true; SettingsInteractionChanged?.Invoke(); };
        ContentLayoutPanel.LostFocus += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_editingClosed || _contentRuntime is null || ContentLayoutPanel.XamlRoot is null) return;
                var focused = FocusManager.GetFocusedElement(ContentLayoutPanel.XamlRoot) as DependencyObject;
                bool inside = false;
                while (focused is not null)
                {
                    if (ReferenceEquals(focused, ContentLayoutPanel)) { inside = true; break; }
                    focused = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(focused);
                }
                _contentFocused = inside; SettingsInteractionChanged?.Invoke();
            });
        };
        Closed += (_, _) =>
        {
            ReleaseContentActivation();
            ClearContentFieldDraft();
            if (_contentObservedRuntime is not null) _contentObservedRuntime.ContentEditorChanged -= ContentEditorChanged;
            _contentObservedRuntime = null; _contentFocused = false;
            _contentRuntime = null; SettingsInteractionChanged?.Invoke();
        };
    }
    private void ContentEditorChanged(string id) { if (id == _view?.SelectedPosterId) QueueContentRefresh(); }
    private void QueueContentRefresh()
    {
        if (_contentRefreshQueued || _editingClosed) return;
        _contentRefreshQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _contentRefreshQueued = false;
            if (!_editingClosed && _contentRuntime is not null) { RenderContentLayout(); RenderEditing(); RenderSettings(); }
        });
    }
    private bool OtherContentOwner => _lampColorFlyout is not null || _settings.ProtectsInteraction || _editor.ProtectsInteraction || _portableBusy ||
        _catalogDialog is not null || _removalDialog is not null || _activityDialog is not null;
    private void RenderContentLayout()
    {
        if (ContentLayoutPanel is null || _editingClosed) return;
        var id = _view?.SelectedPosterId;
        if (id != _contentTargetId)
        {
            ReleaseContentActivation();
            ClearContentFieldDraft();
            SetActivityNotice(null);
            _contentTargetId = id;
        }
        var view = _contentRuntime?.Invoke()?.ReadContentEditor(id);
        _renderingContent = true;
        try
        {
            bool editable = view?.CanEdit == true && !OtherContentOwner;
            bool idle = editable && !IsContentFieldDraftActive;
            ActivitySettingsButton.IsEnabled = idle;
            ContentLayoutStatus.Text = view is null ? "请先显示海报；内容布局不会强行恢复隐藏窗口。" :
                $"{(view.Document.Mode == ContentLayoutMode.Manual ? "手动排布" : "自动排布")} · {view.Document.Placements.Count} 项";
            ContentModeSelector.IsEnabled = ContentFlowSelector.IsEnabled = ContentLoopSwitch.IsEnabled = idle;
            ContentGridSwitch.IsEnabled = ContentSpanSwitch.IsEnabled = idle;
            ContentItemSelector.IsEnabled = view is not null && !OtherContentOwner && !IsContentFieldDraftActive;
            ContentUndoButton.IsEnabled = idle && view?.CanUndo == true;
            ContentRedoButton.IsEnabled = idle && view?.CanRedo == true;
            foreach (var sample in ContentSamples.Children.OfType<Grid>().SelectMany(grid => grid.Children.OfType<Button>()))
                sample.IsEnabled = idle && sample.Tag as string != "landscape" && _readCommitted?.Invoke()?.ContentCatalog.Bindings.All(b => b.PosterId != id) != false;
            var placement = view?.Document.Placements.FirstOrDefault(p => p.ItemId == view.SelectedItemId);
            ContentFixedButton.IsEnabled = idle && placement is { FixedPosition: false } && view!.Document.Mode != ContentLayoutMode.Manual;
            ContentFixedButton.Content = placement?.FixedPosition == true ? "旧硬位置 · 保留" : placement?.PinnedIndex is not null ? "解除顺序图钉" : "固定顺序图钉";
            ContentAutoSizeButton.IsEnabled = idle && placement is { ManualSize: true, AutomaticSizeBasis: not null } && view!.Document.Mode != ContentLayoutMode.Manual;
            // Locked/read-only layouts may still select and activate content. Mutation commands
            // retain their own capability checks; disabling the whole host would hide that path.
            ContentKeyboardPad.IsEnabled = view is not null && !OtherContentOwner && !IsContentFieldDraftActive && placement is not null;
            ContentManageButton.IsEnabled = ContentKeyboardPad.IsEnabled;
            if (view is null)
            {
                ContentItemSelector.ItemsSource = null;
                ContentModeSelector.SelectedIndex = ContentFlowSelector.SelectedIndex = -1;
                ContentLoopSwitch.IsOn = false;
                ContentGridSwitch.IsChecked = ContentSpanSwitch.IsChecked = false;
                RenderContentFields(null);
                ContentLayoutNotice.Text = "选择已显示的海报后，可查看排布与所选卡片。";
                return;
            }
            ContentModeSelector.SelectedIndex = view.Document.Mode == ContentLayoutMode.Manual ? 1 : 0;
            ContentFlowSelector.SelectedIndex = view.Document.FlowAxis == ContentFlowAxis.HorizontalLoop ? 1 : 0;
            ContentLoopSwitch.IsOn = view.Document.LoopScroll;
            ContentGridSwitch.IsChecked = view.Document.ShowBaselineGrid;
            ContentSpanSwitch.IsChecked = view.Document.ShowSpanLabels;
            var items = DesktopUp.Application.ContentSourceProjection.Read(
                _contentRuntime?.Invoke()?.ContentSourceFor(view.Document.PosterId).Views ?? new DemoContentItemViewSource(), view.Document);
            if (ContentItemSelector.ItemsSource is not IReadOnlyList<ContentItemView> existing || !existing.SequenceEqual(items)) ContentItemSelector.ItemsSource = items;
            ContentItemSelector.SelectedItem = ((IReadOnlyList<ContentItemView>)ContentItemSelector.ItemsSource).FirstOrDefault(p => p.ItemId == view.SelectedItemId);
            var selectedView = ContentItemSelector.SelectedItem as ContentItemView;
            ToolTipService.SetToolTip(ContentItemSelector, selectedView is null ? null : selectedView.Name + "\n" + selectedView.TypeName +
                (selectedView.IsAvailable ? "" : "\n来源暂不可用，可刷新或修复引用。"));
            RenderContentFields(view);
            string? draftError = _contentFieldDraft is { } draft ? ValidateContentFields(draft, view, out _) : null;
            ContentLayoutNotice.Text = draftError ?? _contentFieldNotice ?? view.Notice ?? (view.Document.Mode == ContentLayoutMode.Manual && view.Document.SavedAutomaticMode is null
                ? "这份旧手动构图没有原自动配置，暂不能恢复自动；当前构图保留。"
                : view.Document.Mode == ContentLayoutMode.Anchored ? "旧锚定构图按兼容规则保留；顺序图钉与旧硬位置分别处理。"
                : "");
        }
        finally
        {
            _renderingContent = false;
            ContentFineTune.IsEnabled = IsContentFieldDraftActive || view?.SelectedSlot is not null;
            RenderWorkspaceChoices(); RefreshCenterItemMenu(view);
            UpdateContentDraftPresentation();
        }
    }
    private void ContentAction(Func<PosterSpatialRuntime, string, string?> action)
    {
        if (_renderingContent || _editingClosed || _view?.SelectedPosterId is not { } id || _contentRuntime?.Invoke() is not { } runtime) return;
        string? error;
        if (OtherContentOwner) error = "请先完成或取消当前外观、几何或配置操作。";
        else if (IsContentFieldDraftActive) error = "请先应用或取消当前卡片字段；原文已保留。";
        else error = action(runtime, id);
        RenderContentLayout();
        if (error is not null) ContentLayoutNotice.Text = error;
        UpdateContentDraftPresentation();
    }
    private void UpdateContentDraftPresentation()
    {
        if (ContentDraftSummary is null || _editingClosed) return;
        ContentDraftSummary.Visibility = IsContentFieldDraftActive && !ContentFineTune.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        bool duplicate = IsContentFieldDraftActive && ContentFineTune.IsExpanded && ContentLayoutNotice.Text == ContentFieldStatus.Text;
        ContentLayoutNotice.Visibility = duplicate || string.IsNullOrEmpty(ContentLayoutNotice.Text) ? Visibility.Collapsed : Visibility.Visible;
        CommonSettingsNotice.Visibility = IsContentFieldDraftActive && CommonSettingsNotice.Text.StartsWith("已保存", StringComparison.Ordinal)
            ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ContentDraftCancel_Click(object sender, RoutedEventArgs e) => CancelContentFieldDraft();
    private void ContentEdit(ContentLayoutEdit edit) => ContentAction((r, id) => edit.ItemId is { } selected && r.ReadContentEditor(id)?.SelectedItemId != selected
        ? "海报上的选择已变化；已刷新，请重新操作。" : r.ApplyContentEditor(id, edit));
    private string? SelectedContentId => (ContentItemSelector.SelectedItem as ContentItemView)?.ItemId;
    private void ContentMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_renderingContent || ContentModeSelector is null || ContentModeSelector.SelectedIndex < 0) return;
        ContentEdit(new(ContentLayoutEditKind.Mode) { Mode = ContentModeSelector.SelectedIndex == 1 ? ContentLayoutMode.Manual : ContentLayoutMode.Automatic });
    }
    private void ContentFlow_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_renderingContent || ContentFlowSelector is null || (ContentFlowSelector.SelectedItem as ComboBoxItem)?.Tag as string is not { } tag) return;
        ContentAction((r, id) => r.SetContentFlowAxis(id, tag == "horizontal" ? ContentFlowAxis.HorizontalLoop : ContentFlowAxis.Vertical));
    }
    private void ContentLoop_Changed(object sender, RoutedEventArgs e)
    {
        if (_renderingContent || ContentLoopSwitch is null) return;
        bool enabled = ContentLoopSwitch.IsOn;
        ContentAction((r, id) => r.SetContentLoopScroll(id, enabled));
    }
    private void ContentGuides_Changed(object sender, RoutedEventArgs e)
    {
        if (!_renderingContent && ContentGridSwitch is not null && ContentSpanSwitch is not null)
            ContentEdit(new(ContentLayoutEditKind.Guides) { ShowBaselineGrid = ContentGridSwitch.IsChecked == true, ShowSpanLabels = ContentSpanSwitch.IsChecked == true });
    }
    private void ContentItem_Changed(object sender, SelectionChangedEventArgs e)
    { if (!_renderingContent) { var item = SelectedContentId; ContentAction((r, id) => r.SelectContentEditor(id, item)); } }
    private void ContentFixed_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedContentId is not { } item) return;
        bool pinned = _contentRuntime?.Invoke()?.ReadContentEditor(_view?.SelectedPosterId)?.Document.Placements.FirstOrDefault(p => p.ItemId == item)?.PinnedIndex is not null;
        ContentEdit(new(ContentLayoutEditKind.Pinned, item) { Enabled = !pinned });
    }
    private void ContentAutoSize_Click(object sender, RoutedEventArgs e)
    { if (SelectedContentId is { } item) ContentEdit(new(ContentLayoutEditKind.SizeOverride, item) { Enabled = false }); }
    private void ContentUndo_Click(object sender, RoutedEventArgs e) => ContentAction((r, id) => r.UndoContentEditor(id, (sender as Button)?.Tag as string == "redo"));
    private void ContentSample_Click(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: string tag }) ContentAction((r, id) => r.ApplyContentSample(id, tag)); }
    private bool ContentKeyboardHasFocus => ContentKeyboardPad.XamlRoot is not null && ReferenceEquals(FocusManager.GetFocusedElement(ContentKeyboardPad.XamlRoot), ContentKeyboardPad);
    private void ContentKeyboard_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ContentKeyboardHasFocus) return;
        if (e.Key == VirtualKey.A && DesktopUp.Windows.NativeInputKeys.ControlDown)
        { e.Handled = true; ContentAction((r, id) => r.SelectAllRealContent(id)); return; }
        if (e.Key == VirtualKey.Delete && !e.KeyStatus.WasKeyDown)
        { e.Handled = true; _ = RemoveSelectedReference(); return; }
        if (e.Key == VirtualKey.F10 && DesktopUp.Windows.NativeInputKeys.ShiftDown)
        {
            e.Handled = true;
            if (ContentKeyboardPad.ContextFlyout is MenuFlyout menu) menu.ShowAt(ContentKeyboardPad);
            return;
        }
        int dx = e.Key == VirtualKey.Left ? -1 : e.Key == VirtualKey.Right ? 1 : 0;
        int dy = e.Key == VirtualKey.Up ? -1 : e.Key == VirtualKey.Down ? 1 : 0;
        if (dx != 0 || dy != 0)
        {
            e.Handled = true;
            var selected = SelectedContentId;
            ContentAction((r, id) => selected is null || r.ReadContentEditor(id)?.SelectedItemId != selected
                ? "海报上的选择已变化；已刷新，请重新操作。" : r.NudgeContentEditor(id, dx, dy, DesktopUp.Windows.NativeInputKeys.ShiftDown));
        }
        else if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ContentAction((r, id) => { _contentActivationTargetId = id; return r.ActivateContentEditor(id, e.KeyStatus.WasKeyDown); });
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            ReleaseContentActivation();
            CancelContentFieldDraft();
            if (_view?.SelectedPosterId is { } target) _contentRuntime?.Invoke()?.SelectAllRealContent(target, clear: true);
            ContentItemSelector.Focus(FocusState.Programmatic);
        }
    }
    private void ContentKeyboard_KeyUp(object sender, KeyRoutedEventArgs e)
    { if (e.Key == VirtualKey.Enter) { e.Handled = true; ReleaseContentActivation(); } }
    private void ContentKeyboard_LostFocus(object sender, RoutedEventArgs e) => ReleaseContentActivation();
    private void ReleaseContentActivation()
    {
        if (_contentActivationTargetId is not { } id) return;
        _contentActivationTargetId = null;
        _contentRuntime?.Invoke()?.ReleaseContentActivationKey(id);
    }
}
