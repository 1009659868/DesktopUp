using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using global::Windows.System;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly Dictionary<string, Button> _templateSamples = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _templateSampleStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _templateSamplePapers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _templateSampleTapes = new(StringComparer.Ordinal);
    private IReadOnlyList<TemplateRecipe> _templateCatalog = TemplateRecipeCatalog.BuiltIns;
    private string? _templateCatalogSource;
    private int _templateSamplePage;
    private bool IsAppearanceWorkspace => AppearanceScroll.Visibility == Visibility.Visible;
    private void InitializeUiCWorkspace()
    {
        _template.Visibility = Visibility.Collapsed; // Existing template command source; no second selected-template state.
        RebuildTemplateSamples();
        UiCRoot.ActualThemeChanged += (_, _) => { RenderTemplateSamples(); RenderWorkspaceChoices(); };
        UiCTemplateFolder.RegisterPropertyChangedCallback(Expander.IsExpandedProperty,
            (_, _) => UiCFolderAction.Text = UiCTemplateFolder.IsExpanded ? "收起样片" : "展开样片");
        RenderTemplateSamples(); ShowWorkspacePage(false);
        InitializeTemplateRecipe();
    }
    private void RefreshTemplateCatalog()
    {
        string source = _view?.Projection.Settings.Settings.TemplateCatalogJson ?? "[]";
        if (_templateCatalogSource == source) return;
        _templateCatalogSource = source;
        _templateCatalog = TemplateRecipeCatalog.All(source);
        bool wasRendering = _renderingEditor; _renderingEditor = true;
        try
        {
            _template.Items.Clear();
            foreach (var recipe in _templateCatalog) _template.Items.Add(new ComboBoxItem { Content = recipe.Name, Tag = recipe.Id });
            SelectTemplateKey(_view?.SelectedPoster?.Poster.TemplateKey);
        }
        finally { _renderingEditor = wasRendering; }
        _templateSamplePage = Math.Min(_templateSamplePage, (_templateCatalog.Count - 1) / 3);
        RebuildTemplateSamples();
    }
    private TemplateRecipe? SelectedRecipe => _templateCatalog.FirstOrDefault(r => r.Id == (_view?.SelectedPoster?.Poster.TemplateKey ?? SelectedTemplateKey));
    private void TemplatePrevious_Click(object sender, RoutedEventArgs e)
    { if (_templateSamplePage > 0) { _templateSamplePage--; RebuildTemplateSamples(); } }
    private void TemplateNext_Click(object sender, RoutedEventArgs e)
    { if ((_templateSamplePage + 1) * 3 < _templateCatalog.Count) { _templateSamplePage++; RebuildTemplateSamples(); } }
    private void RebuildTemplateSamples()
    {
        TemplateSampleDeck.Children.Clear(); _templateSamples.Clear(); _templateSampleStates.Clear(); _templateSamplePapers.Clear(); _templateSampleTapes.Clear();
        foreach (var recipe in _templateCatalog.Skip(_templateSamplePage * 3).Take(3))
        {
            string key = recipe.Id, label = recipe.Name;
            int index = _templateSamples.Count;
            var body = new StackPanel { Spacing = 9, HorizontalAlignment = HorizontalAlignment.Stretch };
            body.Children.Add(CreateTemplateSampleArt(recipe));
            var caption = new Grid { ColumnSpacing = 5 };
            caption.ColumnDefinitions.Add(new ColumnDefinition()); caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            caption.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
            var number = new TextBlock { Text = (_templateSamplePage * 3 + index + 1).ToString("00"), FontFamily = new FontFamily("Consolas"), FontSize = 10, Opacity = .55 };
            Grid.SetColumn(number, 1); caption.Children.Add(number); body.Children.Add(caption);
            var state = new TextBlock { FontSize = 10, Opacity = .7, Height = 14, TextTrimming = TextTrimming.CharacterEllipsis }; body.Children.Add(state);
            double angle = index == 0 ? -3 : index == 1 ? 2 : -1;
            var transform = new CompositeTransform { Rotation = angle, TranslateY = index == 1 ? 5 : 0 };
            var paperFrame = new Grid { Margin = new Thickness(3, 4, 3, 8), RenderTransformOrigin = new(.5, .5), RenderTransform = transform };
            // Static soft layers remain part of the same native XAML capture; no animation timer or scene owner.
            paperFrame.Children.Add(new Border { Margin = new Thickness(1, 3, -1, -3), Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(12, 0, 0, 0)), CornerRadius = new(4), IsHitTestVisible = false });
            paperFrame.Children.Add(new Border { Margin = new Thickness(2, 5, -2, -5), Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(8, 0, 0, 0)), CornerRadius = new(5), IsHitTestVisible = false });
            var paper = new Border { Child = body, Padding = new Thickness(7, 7, 7, 10), BorderThickness = new(1), CornerRadius = new(3, 5, 4, 2) };
            paperFrame.Children.Add(paper);
            var tape = new Border { Width = 30, Height = 11, Margin = new Thickness(0, -6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform { Angle = -8 },
                BorderThickness = new Thickness(1, 0, 1, 0), Opacity = .42, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            paperFrame.Children.Add(tape);
            var button = new Button { Content = paperFrame, Tag = key, Padding = new Thickness(0), MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0), VerticalAlignment = VerticalAlignment.Top };
            foreach (string resource in new[] { "ButtonBackgroundPointerOver", "ButtonBackgroundPressed", "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed" })
                button.Resources[resource] = new SolidColorBrush(Colors.Transparent);
            void RaiseSample(bool raised) { transform.Rotation = raised ? 0 : angle; transform.TranslateY = raised ? -3 : index == 1 ? 5 : 0; }
            button.PointerEntered += (_, _) => { if (button.IsEnabled) RaiseSample(true); };
            button.PointerExited += (_, _) => RaiseSample(button.FocusState != FocusState.Unfocused);
            button.GotFocus += (_, _) => RaiseSample(true); button.LostFocus += (_, _) => RaiseSample(false);
            AutomationProperties.SetAutomationId(button, "TemplateSample_" + key);
            AutomationProperties.SetName(button, label);
            ToolTipService.SetToolTip(button, label + " · " + recipe.Family + "；选择后应用到当前海报");
            button.Click += (_, _) => { SelectTemplateKey(key); RenderUiCTemplateSummary(); RenderTemplateSamples(); };
            Grid.SetColumn(button, _templateSamples.Count); TemplateSampleDeck.Children.Add(button);
            _templateSamples.Add(key, button); _templateSampleStates.Add(key, state);
            _templateSamplePapers.Add(key, paper); _templateSampleTapes.Add(key, tape);
        }
        TemplateSampleRange.Text = $"{_templateSamplePage * 3 + 1}–{Math.Min((_templateSamplePage + 1) * 3, _templateCatalog.Count)} / {_templateCatalog.Count}";
        TemplatePrevious.IsEnabled = _templateSamplePage > 0;
        TemplateNext.IsEnabled = (_templateSamplePage + 1) * 3 < _templateCatalog.Count;
        RenderTemplateSamples();
    }
    private string TemplateSampleFamily(string key) => _templateCatalog.FirstOrDefault(r => r.Id == key)?.Family ?? "我的样片";
    private static Grid CreateTemplateSampleArt(TemplateRecipe recipe)
    {
        if (SceneModuleRegistry.CreateSample(recipe) is { } sample) return sample;
        var art = new Grid { Height = 132, CornerRadius = new(2), Background = SampleBrush(recipe.Base.Background), IsHitTestVisible = false };
        art.Children.Add(new TextBlock { Text = recipe.Base.Heading ? "NIGHT\nPLAY." : "MAKE\nROOM.",
            Margin = new(10, 12, 8, 0), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Top,
            Foreground = SampleBrush("#F2F2F2") });
        var cards = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new(10, 0, 10, 10), VerticalAlignment = VerticalAlignment.Bottom };
        for (int i = 0; i < 3; i++) cards.Children.Add(new Border { Width = 28, Height = 32 - i * 5, Background = SampleBrush(recipe.Base.Card), CornerRadius = new(2) });
        art.Children.Add(cards); return art;
    }    private static SolidColorBrush SampleBrush(string hex) => new(global::Windows.UI.Color.FromArgb(255,
        Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
    private void RenderTemplateSamples()
    {
        if (UiCTemplateFolder is null) return;
        foreach (var (key, button) in _templateSamples)
        {
            bool selected = SelectedTemplateKey == key;
            var paper = _templateSamplePapers[key]; var tape = _templateSampleTapes[key];
            paper.BorderBrush = selected ? UiCFolderTab.Foreground : UiCTemplateFolder.BorderBrush;
            paper.BorderThickness = new(selected ? 1.5 : 1);
            paper.Background = UiCPaperSurface.Background;
            tape.Background = tape.BorderBrush = UiCFolderTab.Foreground;
            tape.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            button.IsEnabled = _template.IsEnabled && !OtherContentOwner && !IsContentFieldDraftActive;
            _templateSampleStates[key].Text = selected ? "✓ 使用中" : TemplateSampleFamily(key);
            _templateSampleStates[key].Foreground = selected ? UiCFolderTab.Foreground : PosterTitleText.Foreground;
        }
    }
    private void WorkspaceTab_Click(object sender, RoutedEventArgs e) => ShowWorkspacePage((sender as FrameworkElement)?.Tag as string == "behavior");
    private void ShowWorkspacePage(bool behavior)
    {
        if (AppearanceScroll is null || BehaviorScroll is null) return;
        AppearanceScroll.Visibility = behavior ? Visibility.Collapsed : Visibility.Visible;
        BehaviorScroll.Visibility = behavior ? Visibility.Visible : Visibility.Collapsed;
        AppearanceTab.IsChecked = !behavior; BehaviorTab.IsChecked = behavior;
        StyleWorkspaceChoice(AppearanceTab, !behavior); StyleWorkspaceChoice(BehaviorTab, behavior);
    }
    private void ContentModeChoice_Click(object sender, RoutedEventArgs e)
    {
        ContentModeSelector.SelectedIndex = (sender as FrameworkElement)?.Tag as string == "manual" ? 1 : 0;
        RenderWorkspaceChoices();
    }
    private void ContentFlowChoice_Click(object sender, RoutedEventArgs e)
    {
        ContentFlowSelector.SelectedIndex = (sender as FrameworkElement)?.Tag as string == "horizontal" ? 1 : 0;
        RenderWorkspaceChoices();
    }
    private void RenderWorkspaceChoices()
    {
        if (ContentAutomaticChoice is null) return;
        SetChoice(ContentAutomaticChoice, ContentModeSelector.SelectedIndex == 0, ContentModeSelector.IsEnabled);
        SetChoice(ContentManualChoice, ContentModeSelector.SelectedIndex == 1, ContentModeSelector.IsEnabled);
        SetChoice(ContentVerticalChoice, ContentFlowSelector.SelectedIndex == 0, ContentFlowSelector.IsEnabled);
        SetChoice(ContentHorizontalChoice, ContentFlowSelector.SelectedIndex == 1, ContentFlowSelector.IsEnabled);
        StyleWorkspaceChoice(AppearanceTab, IsAppearanceWorkspace); StyleWorkspaceChoice(BehaviorTab, !IsAppearanceWorkspace);
        ContentFineTune.Header = IsContentFieldDraftActive ? "精细调整 · 尚未应用" : "精细调整";
        void SetChoice(ToggleButton choice, bool selected, bool enabled)
        { choice.IsChecked = selected; choice.IsEnabled = enabled; StyleWorkspaceChoice(choice, selected); }
    }
    private void StyleWorkspaceChoice(ToggleButton choice, bool selected)
    {
        bool tab = ReferenceEquals(choice, AppearanceTab) || ReferenceEquals(choice, BehaviorTab);
        choice.BorderThickness = tab ? new Thickness(0, 0, 0, 2) : new Thickness(1);
        choice.BorderBrush = selected ? UiCFolderTab.Foreground : new SolidColorBrush(Colors.Transparent);
        choice.Foreground = selected ? UiCFolderTab.Foreground : PosterTitleText.Foreground;
        choice.Background = selected && !tab ? UiCTemplateFolder.Background : new SolidColorBrush(Colors.Transparent);
    }
    private void ContentOpen_Click(object sender, RoutedEventArgs e)
    {
        ContentAction((runtime, id) =>
        {
            try { return runtime.ActivateContentEditor(id, false); }
            finally { runtime.ReleaseContentActivationKey(id); }
        });
    }
    private void ContentManage_Click(object sender, RoutedEventArgs e)
    {
        if (ContentManageButton.IsEnabled) ContentKeyboardPad.ContextFlyout?.ShowAt(ContentManageButton);
    }
    private void RefreshCenterItemMenu(ContentLayoutEditorView? view)
    {
        var menu = new MenuFlyout(); var item = view?.SelectedItemId;
        // ContextFlyout is reused across openings; each actual opening gets one authority.
        var gate = new PosterMenuActionGate();
        gate.Close();
        menu.Opening += (_, _) => gate = new PosterMenuActionGate();
        menu.Closed += (_, _) => gate.Close();
        void Add(string text, Action action, bool enabled)
        {
            var entry = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
            entry.Click += (_, _) =>
            {
                if (!gate.TryConsume(ReferenceEquals(ContentKeyboardPad.ContextFlyout, menu) && entry.IsEnabled)) return;
                menu.Hide(); action();
            };
            menu.Items.Add(entry);
        }
        bool writable = view?.CanEdit == true && !OtherContentOwner && !IsContentFieldDraftActive;
        var placement = view?.Document.Placements.FirstOrDefault(p => p.ItemId == item);
        string? target = _view?.SelectedPosterId;
        void ForSameItem(Action action)
        {
            var current = _contentRuntime?.Invoke()?.ReadContentEditor(target);
            if (target == _view?.SelectedPosterId && item == SelectedContentId && current?.SelectedItemId == item &&
                current?.EntityVersion == view?.EntityVersion && current?.Document.DocumentVersion == view?.Document.DocumentVersion) action();
            else ContentLayoutNotice.Text = "所选项目已变化，请在当前项目重新打开菜单。";
        }
        Add("打开当前项", () => ForSameItem(() => ContentOpen_Click(ContentKeyboardPad, new RoutedEventArgs())), ContentKeyboardPad.IsEnabled);
        bool real = target is not null && _contentRuntime?.Invoke()?.ContentSourceFor(target).Open is DesktopUp.Application.IAsyncContentActions;
        if (real)
        {
            Add("在资源管理器中定位", () => ForSameItem(() => ContentAction((runtime, id) => runtime.RevealContentEditor(id))), !OtherContentOwner);
            Add("刷新来源状态与图标", () => ForSameItem(() => ContentAction((runtime, id) => runtime.RefreshRealContentEditor(id))), !OtherContentOwner);
            var icon = CaptureIconPreview();
            Add("更换卡片图标…", () => ForSameItem(() => _ = EditCardIcon(false)),
                writable && icon is not null && _applyIcon is not null);
            Add("恢复默认图标", () => ForSameItem(() => _ = EditCardIcon(true)),
                writable && icon?.HasCustomIcon == true && _applyIcon is not null);
            Add("修复所选引用…", () => ForSameItem(() => _ = RepairSelectedReference()),
                writable && item is not null && view?.SelectedIds.Count == 1 && _referenceRepair is not null);
        }
        if (placement is not null)
        {
            Add(placement.PinnedIndex is not null ? "解除顺序图钉" : "固定顺序图钉",
                () => ForSameItem(() => ContentEdit(new(ContentLayoutEditKind.Pinned, item) { Enabled = placement.PinnedIndex is null })),
                writable && !placement.FixedPosition && view!.Document.Mode != ContentLayoutMode.Manual);
            Add("恢复自动尺寸", () => ForSameItem(() => ContentEdit(new(ContentLayoutEditKind.SizeOverride, item) { Enabled = false })),
                writable && view!.Document.Mode != ContentLayoutMode.Manual && placement is { ManualSize: true, AutomaticSizeBasis: not null });
            Add("精细调整…", () => ForSameItem(() => { ContentFineTune.IsExpanded = true; ContentColumns.Focus(FocusState.Programmatic); }), writable);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("撤销", () => ForSameItem(() => ContentAction((runtime, id) => runtime.UndoContentEditor(id, false))), writable && view?.CanUndo == true);
        Add("重做", () => ForSameItem(() => ContentAction((runtime, id) => runtime.UndoContentEditor(id, true))), writable && view?.CanRedo == true);
        ContentKeyboardPad.ContextFlyout?.Hide();
        ContentKeyboardPad.ContextFlyout = menu;
    }
}
