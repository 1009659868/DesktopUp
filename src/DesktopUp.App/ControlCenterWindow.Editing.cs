using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private readonly ControlCenterPosterEditor _editor = new(requireActualGeometry: true);
    private Func<DesktopUpProjection?>? _readCommitted;
    private Func<string?, ControlCenterGeometryBasis?>? _readGeometry;
    private Action<ControlCenterGeometryRequest>? _applyGeometry;
    private Action<ControlCenterCatalogRequest>? _applyCatalog;
    private readonly Dictionary<string, TextBox> _geometryFields = new(), _styleFields = new();
    private readonly InfoBar _editNotice = new() { IsOpen = true, IsClosable = false };
    private readonly TextBlock _geometryStatus = new() { TextWrapping = TextWrapping.Wrap }, _styleStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _template = new() { Header = "内置模板", HorizontalAlignment = HorizontalAlignment.Stretch };
    private string? SelectedTemplateKey => (_template.SelectedItem as ComboBoxItem)?.Tag as string;
    private void SelectTemplateKey(string? key) => _template.SelectedItem = _template.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == key);
    private readonly Button _geometryApply = new() { Content = "应用位置/尺寸" }, _geometryCancel = new() { Content = "取消几何修改" },
        _styleApply = new() { Content = "应用内容/主题" }, _styleCancel = new() { Content = "取消参数修改" };
    private bool _renderingEditor, _editingProtection, _editingClosed;
    private ContentDialog? _catalogDialog;

    internal void ConfigureEditing(Func<DesktopUpProjection?> committed, Func<string?, ControlCenterGeometryBasis?> geometryRead,
        Action<ControlCenterGeometryRequest> geometry, Action<ControlCenterCatalogRequest> catalog)
    { _readCommitted = committed; _readGeometry = geometryRead; _applyGeometry = geometry; _applyCatalog = catalog; }

    private void InitializeEditing()
    {
        var geometry = Section("位置与尺寸 · 当前显示器工作区内 DIP");
        foreach (var (key, label) in new[] { ("X", "X 位置"), ("Y", "Y 位置"), ("Width", "宽度（下限随内容）"), ("Height", "保存展开高度（下限随内容）") })
            AddField(geometry, _geometryFields, key, label, false);
        geometry.Children.Add(new TextBlock { Text = "收起时只可移动，锁定时不可编辑几何。受限的当前高度不覆盖保存高度；小数按物理像素取整。", TextWrapping = TextWrapping.Wrap });
        geometry.Children.Add(_geometryStatus);
        AddActions(geometry, _geometryApply, _geometryCancel);
        var style = Section("精细基准参数", true);
        foreach (var (key, label) in TemplateRecipeCatalog.BuiltIns.Select(r => (r.Id, r.Name)))
            _template.Items.Add(new ComboBoxItem { Content = label, Tag = key });
        TemplateSection.Children.Add(_template);
        _template.SelectionChanged += (_, _) => { if (!_renderingEditor && !_renderingSettings && SelectedTemplateKey is { } key) ChangeSetting(PosterSetting.Template, text: key, finish: true); };
        foreach (var (key, label) in new[] { ("Subtitle", "副标题"), ("ItemWidth", "单元宽 72～320 DIP"), ("ItemHeight", "图标区高 48～240 DIP"),
            ("Icon", "图标大小 16～128 DIP"), ("Gap", "间距 0～40 DIP"), ("Label", "标签字号 9～24 DIP"), ("Title", "大标题字号 18～48 DIP"),
            ("Background", "背景色 #RRGGBB"), ("Foreground", "文字色 #RRGGBB"), ("Accent", "强调色 #RRGGBB") })
            AddField(style, _styleFields, key, label, true);
        style.Children.Add(_styleStatus);
        AddActions(style, _styleApply, _styleCancel);
        EditorSections.Children.Insert(0, _editNotice);
        EditorSections.Children.Add(new TextBlock { Text = "分区独立应用；切换海报或关闭窗口会丢弃未应用的修改。应用中切换目标不会重定向已提交的请求。", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        _geometryApply.Click += (_, _) =>
        {
            CaptureGeometry(); var request = _editor.BeginGeometryApply(); RenderEditing();
            if (request is not null) _applyGeometry?.Invoke(request);
        };
        _styleApply.Click += (_, _) =>
        {
            CaptureStyle(); var command = _editor.BeginPresentationApply(); RenderEditing();
            if (command is not null) _posterCommand(command);
        };
        _geometryCancel.Click += (_, _) => { _editor.Cancel(); RenderEditing(); };
        _styleCancel.Click += (_, _) => { _editor.Cancel(); RenderEditing(); };
        AutomationProperties.SetAutomationId(_geometryApply, "ApplyGeometry");
        AutomationProperties.SetAutomationId(_styleApply, "ApplyPresentation");
        Closed += (_, _) => { _editingClosed = true; _catalogDialog?.Hide(); _editor.Close(); };
    }

    private StackPanel Section(string title, bool collapsed = false)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (collapsed) EditorSections.Children.Add(new Expander { Header = title, Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch });
        else EditorSections.Children.Add(panel);
        return panel;
    }
    private static void AddActions(StackPanel panel, Button apply, Button cancel)
    { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; row.Children.Add(apply); row.Children.Add(cancel); panel.Children.Add(row); }
    private void AddField(StackPanel panel, Dictionary<string, TextBox> fields, string key, string label, bool style)
    {
        var box = new TextBox { Header = label, MaxLength = key == "Subtitle" ? 512 : 64, MaxWidth = 300, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 210 };
        fields[key] = box; panel.Children.Add(box);
        AutomationProperties.SetAutomationId(box, (style ? "Presentation" : "Geometry") + key);
        box.TextChanging += (_, _) => { if (!_renderingEditor) { if (style) CaptureStyle(); else CaptureGeometry(); } };
        box.TextChanged += (_, _) => { if (!_renderingEditor) RenderEditing(); };
    }
    private void CaptureGeometry()
    {
        if (_renderingEditor || _geometryFields.Count != 4) return;
        if (_lampColorFlyout is not null || _settings.ProtectsInteraction || IsContentFieldDraftActive || _view?.SelectedPosterId is { } id && DesktopInteractionConflicts(id))
        { _geometryStatus.Text = "请先完成或取消当前海报操作；原草稿已保留。"; return; }
        _editor.EditGeometry(new(_geometryFields["X"].Text, _geometryFields["Y"].Text, _geometryFields["Width"].Text, _geometryFields["Height"].Text));
    }
    private void CaptureStyle()
    {
        if (_renderingEditor || _styleFields.Count != 10 || _editor.Presentation is not { } draft) return;
        if (_lampColorFlyout is not null || _settings.ProtectsInteraction || IsContentFieldDraftActive || _view?.SelectedPosterId is { } id && DesktopInteractionConflicts(id))
        { _styleStatus.Text = "请先完成或取消当前海报操作；原草稿已保留。"; return; }
        double Number(string key) => double.TryParse(_styleFields[key].Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out double n) && double.IsFinite(n) ? n : double.NaN;
        _editor.EditPresentation(draft.Title, _styleFields["Subtitle"].Text, SelectedTemplateKey ?? draft.TemplateKey,
            draft.Parameters with { Content = new(Number("ItemWidth"), Number("ItemHeight"), Number("Icon"), Number("Gap"), Number("Label"), Number("Title")),
                Theme = new(_styleFields["Background"].Text, _styleFields["Foreground"].Text, _styleFields["Accent"].Text) });
    }
    private void SynchronizeEditing(ControlCenterView view)
    {
        if (_readCommitted?.Invoke() is { } current)
            _editor.Synchronize(current, view.SelectedPosterId, view.SelectedPoster is { } target ? _effectiveLayout(target) : null,
                _readGeometry?.Invoke(view.SelectedPosterId));
        RenderEditing();
    }
    private void RenderEditing()
    {
        if (_editingClosed || _geometryFields.Count != 4) return;
        _renderingEditor = true;
        try
        {
            bool target = _editor.PosterId is not null, pending = _editor.IsPending;
            bool geometry = _editor.Group != ControlCenterEditGroup.Presentation;
            bool style = _editor.Group != ControlCenterEditGroup.Geometry;
            if (!target)
            {
                foreach (var field in _geometryFields.Values.Concat(_styleFields.Values)) if (field.Text.Length != 0) field.Text = "";
                _template.SelectedIndex = -1;
            }
            if (_editor.Geometry is { } g)
            {
                Set(_geometryFields, "X", g.X); Set(_geometryFields, "Y", g.Y); Set(_geometryFields, "Width", g.Width); Set(_geometryFields, "Height", g.ExpandedHeight);
            }
            else foreach (var field in _geometryFields.Values) field.Text = "";
            if ((!_editor.IsDirty || !style) && _editor.Presentation is { } p)
            {
                SelectTemplateKey(p.TemplateKey);
                Set(_styleFields, "Subtitle", p.Subtitle);
                var c = p.Parameters.Content; var t = p.Parameters.Theme;
                Set(_styleFields, "ItemWidth", N(c.ItemWidthDip)); Set(_styleFields, "ItemHeight", N(c.ItemHeightDip));
                Set(_styleFields, "Icon", N(c.IconSizeDip)); Set(_styleFields, "Gap", N(c.GapDip));
                Set(_styleFields, "Label", N(c.LabelFontSizeDip)); Set(_styleFields, "Title", N(c.TitleFontSizeDip));
                Set(_styleFields, "Background", t.Background); Set(_styleFields, "Foreground", t.Foreground); Set(_styleFields, "Accent", t.Accent);
            }
            bool movable = target && _editor.HasActualGeometry && _editor.LatestCommitted?.CanMove == true && _editor.LatestCommitted.Poster.IsVisible;
            bool otherDraft = _lampColorFlyout is not null || _settings.ProtectsInteraction || IsContentFieldDraftActive || _editor.PosterId is { } targetId && DesktopInteractionConflicts(targetId);
            foreach (var (key, field) in _geometryFields) field.IsEnabled = geometry && !pending && !otherDraft && movable &&
                (key is "X" or "Y" || _editor.LatestCommitted?.CanResize == true);
            foreach (var field in _styleFields.Values) field.IsEnabled = style && !pending && !otherDraft && _editor.Presentation is not null;
            foreach (string key in new[] { "ItemWidth", "ItemHeight", "Gap" })
            {
                _styleFields[key].IsEnabled=false;
                ToolTipService.SetToolTip(_styleFields[key],"旧等格兼容值，只读保留。当前卡片由内容布局的跨度/基线控制；宏观疏密在外观页调整。");
            }
            _template.IsEnabled = style && !pending && !otherDraft && !_editor.IsDirty && _editor.Presentation is not null;
            _geometryApply.IsEnabled = geometry && _editor.CanApply;
            _styleApply.IsEnabled = style && _editor.CanApply;
            _geometryCancel.IsEnabled = geometry && _editor.CanCancel;
            _styleCancel.IsEnabled = style && _editor.CanCancel;
            CreatePosterButton.IsEnabled = !pending && _applyCatalog is not null;
            ClonePosterButton.IsEnabled = RenamePosterButton.IsEnabled = target && !pending && _catalogDialog is null;
            if (pending) LockButton.IsEnabled = CollapseButton.IsEnabled = VisibilityButton.IsEnabled = RemoveButton.IsEnabled = false;
            string? error = _editor.ValidationError ?? _editor.Error;
            _editNotice.IsOpen = error is not null || _editor.IsDirty || pending;
            _editNotice.Severity = error is not null || _editor.IsStale ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            _editNotice.Title = !target ? "未选择海报" : pending ? "正在呈现并提交" : _editor.IsStale ? "草稿已过期" : _editor.IsDirty ? "尚未应用" : "已提交值";
            _editNotice.Message = error ?? (_editor.IsStale ? "同一海报已变化；应用将执行 CAS。可取消载入最新值。" :
                "外框几何不改变图标大小和字号；修改只作用于当前目标。未知参数版本只读，不覆盖原文。");
            _geometryStatus.Text = geometry ? _editNotice.Title + " · " + (error ?? "位置和尺寸按一次事务应用。") : "请先应用或取消内容参数草稿。";
            _styleStatus.Text = style ? _editNotice.Title + " · " + (error ?? "旧单元宽高/间距只读保留；图标基准仅用于 contain 卡片，封面采用 cover。") : "请先应用或取消几何草稿。";
        }
        finally { _renderingEditor = false; }
        if (_editingProtection != IsSettingsInteractionActive)
        { _editingProtection = IsSettingsInteractionActive; SettingsInteractionChanged?.Invoke(); }
        static string N(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        static void Set(Dictionary<string, TextBox> fields, string key, string value) { if (fields[key].Text != value) fields[key].Text = value; }
    }

    internal bool CompleteEditing(StateCommandResult result)
    {
        if (!_editor.Complete(result)) return false;
        RenderEditing();
        if (_view is { } view) ApplyView(view);
        return true;
    }
    private void CreatePoster_Click(object sender, RoutedEventArgs e)
    {
        if (_portableBusy || _editingClosed) return;
        var menu = new MenuFlyout();
        var normal = new MenuFlyoutItem { Text = "普通海报" };
        normal.Click += (_, _) => ShowCatalogDialog("create");
        var mapped = new MenuFlyoutItem { Text = "目录映射…", IsEnabled = _contentRuntime?.Invoke()?.TransfersAvailable == true };
        mapped.Click += async (_, _) => await CreateMappedPosterFromCenter();
        menu.Items.Add(normal); menu.Items.Add(mapped);
        menu.ShowAt(CreatePosterButton);
    }

    private Task CreateMappedPosterFromCenter(Func<Task<string?>>? choose = null) => PortableAction(async () =>
    {
        var runtime = _contentRuntime?.Invoke();
        if (runtime is null || !runtime.FileServiceReady) return;
        string? path = await (choose?.Invoke() ?? PickPortableFile(false, purpose: DesktopUp.Windows.NativePickerPurpose.MappedDirectory));
        if (path is null || _editingClosed || _portableStopping) return;
        string id = await runtime.CreateMappedPoster(path, _transferStopping.Token);
        if (!_editingClosed && !_portableStopping) _selectPoster(id);
    });
    private void ClonePoster_Click(object sender, RoutedEventArgs e) => ShowCatalogDialog("clone");
    private void RenamePoster_Click(object sender, RoutedEventArgs e) => ShowCatalogDialog("rename");
    private async void ShowCatalogDialog(string kind)
    {
        if (_catalogDialog is not null || _portableBusy || _editor.IsPending || _readCommitted?.Invoke() is not { } basis) return;
        if (_lampColorFlyout is not null || _settings.ProtectsInteraction || _editor.ProtectsInteraction || IsContentFieldDraftActive || _view?.SelectedPosterId is { } selectedId && DesktopInteractionConflicts(selectedId))
        { CommonSettingsNotice.Text = "请先完成或取消当前海报操作。"; return; }
        var target = _view?.SelectedPoster;
        if (kind != "create" && target is null) return;
        var input = new TextBox { Header = "海报名称", MaxLength = 256,
            Text = kind == "create" ? "新海报" : target!.Poster.Title + (kind == "clone" ? " 副本" : "") };
        var body = new StackPanel { Spacing = 10 }; body.Children.Add(input);
        body.Children.Add(new TextBlock { Text = kind == "clone" ? "仅复制配置，不复制文件/运行状态；在主显示器寻找空位。锁定、收起及自动设置按源配置保留。" :
            kind == "create" ? "创建普通 Box，在主显示器寻找完整空位。不会移动真实文件。" : "仅修改当前目标的名称，不应用其他草稿。", TextWrapping = TextWrapping.Wrap });
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = kind == "create" ? "新增海报" : kind == "clone" ? "复制配置" : "重命名",
            Content = body, PrimaryButtonText = "确认", CloseButtonText = "取消",
            DefaultButton = kind == "create" ? ContentDialogButton.Primary : ContentDialogButton.Close };
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);
        ApplyPaperDialogStyle(dialog);
        _catalogDialog = dialog; SettingsInteractionChanged?.Invoke();
        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _editingClosed) return;
            string name = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) { _editNotice.Message = "名称不能为空；未执行。"; return; }
            string id = "controlcenter-catalog-" + Guid.NewGuid().ToString("N");
            if (kind == "rename") _posterCommand(new(id, StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
                basis.Revision, target!.Poster.PosterId, target.Poster with { Title = name }) { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) });
            else _applyCatalog?.Invoke(new(id, basis.Revision, new StateReadSet(basis.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
                CatalogVersion: basis.CatalogVersion).Freeze(), kind == "clone" ? target!.Poster.PosterId : null, name));
        }
        catch (Exception ex) { if (!_editingClosed) { _editNotice.Title = "未执行"; _editNotice.Message = ex.Message; } }
        finally { _catalogDialog = null; RenderEditing(); SettingsInteractionChanged?.Invoke(); }
    }
}
