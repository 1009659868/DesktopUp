using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using global::Windows.Storage;
using global::Windows.Storage.Pickers;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private bool _templateReferenceOpen;
    private Func<string, string?, long, StateCommandResult>? _registerTemplate;
    internal void ConfigureTemplateRegistration(Func<string, string?, long, StateCommandResult> register) => _registerTemplate = register;
    private void InitializeTemplateRecipe()
    {
        Add(TemplateActionsHost, "装入模板 ↗", () => ShowTemplateImport());
        Add(TemplateActionsHost, "＋ 留一份自己的", ShowSaveTemplate);
        Add(TemplateLibraryActionsHost, "打开模板册 ↗", ShowTemplateBook);
        void Add(Panel host, string label, Action action)
        {
            var button = new Button { Content = label, Style = (Style)UiCRoot.Resources["UiCAction"],
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
            button.Click += (_, _) => action(); host.Children.Add(button);
        }
    }
    private bool TemplateOperationReady() => !_editingClosed && _view?.SelectedPoster is { Presentation.CanEdit: true } target &&
        !OtherContentOwner && !IsContentFieldDraftActive && !_settings.ProtectsInteraction && !_editor.ProtectsInteraction &&
        !DesktopInteractionConflicts(target.Poster.PosterId);
    private void ShowTemplateDefinition() => ShowTemplateImport(SelectedRecipe is { } recipe ? TemplateRecipeCodec.Serialize(recipe) : null);

    private async void ShowTemplateImport(string? initial = null)
    {
        if (_templateReferenceOpen || Content.XamlRoot is null) return;
        if (!TemplateOperationReady()) { CommonSettingsNotice.Text = "请先完成或取消当前修改。"; return; }
        _templateReferenceOpen = true;
        bool live = true; int generation = 0;
        try
        {
            string target = _view!.SelectedPosterId!; long revision = _view.Projection.Revision;
            string? checkedSource = null;
            var body = new StackPanel { Spacing = 16 };
            body.Children.Add(new TextBlock { Text = "RECIPE / LIVE LOAD", FontFamily = new FontFamily("Consolas"), FontSize = 11, CharacterSpacing = 180, Opacity = .65 });
            body.Children.Add(new TextBlock { Text = "参数随模板而来。检查通过后，直接用于当前海报，无需刷新。", TextWrapping = TextWrapping.Wrap });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var example = new Button { Content = "填入「暖夜书桌」示例" };
            var file = new Button { Content = "选择 JSON" };
            var inspect = new Button { Content = "查看当前配方", Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
            actions.Children.Add(example); actions.Children.Add(file); actions.Children.Add(inspect); body.Children.Add(actions);
            var source = new TextBox { Text = initial ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"), FontSize = 12, MinHeight = 180,
                Height = Math.Max(180, Math.Min(340, UiCRoot.ActualHeight - 340)), MaxLength = 65536,
                PlaceholderText = "粘贴模板 JSON，或选择上方示例", Padding = new Thickness(16), IsSpellCheckEnabled = false };
            AutomationProperties.SetName(source, "模板 JSON 配方"); body.Children.Add(source);
            var notice = new TextBlock { Text = "先填入示例或选择 JSON，再检查配方。", TextWrapping = TextWrapping.Wrap }; body.Children.Add(notice);
            body.Children.Add(new Expander { Header = "配方怎样工作？", Content = new TextBlock
            { Text = "参数声明生成控件，绑定连接到宿主支持的效果。装入使用新的模板 ID；已有模板和其他海报保持不变。", TextWrapping = TextWrapping.Wrap } });
            body.Children.Add(new TextBlock { Text = "用于 " + _view.SelectedPoster!.Poster.Title, FontSize = 11, Opacity = .65 });
            var dialog = PaperDialog("装入一张新样片", new ScrollViewer { Content = body, MaxHeight = Math.Max(300, UiCRoot.ActualHeight - 150) }, "取消", "装入并使用", "检查配方");
            dialog.IsPrimaryButtonEnabled = false;
            source.TextChanging += (_, _) => { checkedSource = null; generation++; dialog.IsPrimaryButtonEnabled = false; notice.Text = "配方已变化，请检查后再装入。"; };
            example.Click += (_, _) =>
            {
                var recipe = TemplateRecipeCatalog.BuiltIns.FirstOrDefault(r => r.Schema.Properties.Count > 0) ?? TemplateRecipeCatalog.BuiltIns[0];
                source.Text = TemplateRecipeCodec.Serialize(recipe with { Schema = recipe.Schema with { TemplateId = "my-warm-desk", Name = "暖夜书桌" } });
            };
            inspect.Click += (_, _) => { if (SelectedRecipe is { } recipe) source.Text = TemplateRecipeCodec.Serialize(recipe); };
            file.Click += async (_, _) =>
            {
                int ticket = ++generation; checkedSource = null; dialog.IsPrimaryButtonEnabled = false;
                try
                {
                    var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".json");
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    var selected = await picker.PickSingleFileAsync();
                    if (selected is null || !live || ticket != generation) return;
                    if ((await selected.GetBasicPropertiesAsync()).Size > TemplateRecipeCodec.MaximumBytes)
                    { if (live && ticket == generation) notice.Text = "配方文件不得超过 64 KiB。"; return; }
                    string text = await FileIO.ReadTextAsync(selected);
                    if (live && ticket == generation) source.Text = text;
                }
                catch (Exception error) when (error is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
                { if (live && ticket == generation) notice.Text = "无法读取所选配方：" + error.Message; }
            };
            dialog.SecondaryButtonClick += (_, args) =>
            {
                args.Cancel = true;
                try
                {
                    var recipe = TemplateRecipeCodec.Parse(source.Text);
                    TemplateRecipeCatalog.Add(_view?.Projection.Settings.Settings.TemplateCatalogJson ?? "[]", recipe);
                    if (_view?.SelectedPosterId != target || _view.Projection.Revision != revision || !TemplateOperationReady())
                        throw new ArgumentException("当前海报或设置已变化，请重新打开装入窗口。");
                    checkedSource = source.Text; dialog.IsPrimaryButtonEnabled = true;
                    notice.Text = $"检查通过 · {recipe.Name} · {recipe.Schema.Properties.Count} 项专属设置";
                }
                catch (ArgumentException error) { checkedSource = null; dialog.IsPrimaryButtonEnabled = false; notice.Text = error.Message; }
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                args.Cancel = true;
                if (checkedSource != source.Text || _view?.SelectedPosterId != target || _view.Projection.Revision != revision || !TemplateOperationReady())
                { checkedSource = null; dialog.IsPrimaryButtonEnabled = false; notice.Text = "配方或目标已变化，请关闭后重新检查。"; return; }
                var result = _registerTemplate?.Invoke(checkedSource!, target, revision);
                if (result?.Succeeded != true) { notice.Text = result?.Error ?? "装入未完成，原海报已保留。"; return; }
                args.Cancel = false;
            };
            dialog.Closed += (_, _) => { live = false; generation++; };
            await dialog.ShowAsync();
        }
        catch (InvalidOperationException) { CommonSettingsNotice.Text = "当前无法打开装入窗口，请先关闭其他对话框。"; }
        finally { live = false; generation++; _templateReferenceOpen = false; }
    }

    private async void ShowSaveTemplate()
    {
        if (_templateReferenceOpen || Content.XamlRoot is null || SelectedRecipe is not { } selected) return;
        if (!TemplateOperationReady()) { CommonSettingsNotice.Text = "请先完成或取消当前修改。"; return; }
        _templateReferenceOpen = true;
        try
        {
            string target = _view!.SelectedPosterId!; long revision = _view.Projection.Revision;
            var currentPaint = _view.SelectedPoster!.Presentation.TemplateValues;
            selected = selected with { Base = selected.Base with { Background = currentPaint.Color("surface.background", selected.Base.Background), Card = currentPaint.Color("item.color", selected.Base.Card) } };
            var values = TemplateParameterBindingAdapter.Read(selected.Schema, _view.SelectedPoster!.Presentation.TemplateValues)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var body = new StackPanel { Spacing = 16 }; body.Children.Add(CreateTemplateSampleArt(selected));
            var name = new TextBox { Header = "模板名称", Text = selected.Name + " · 我的", MaxLength = 40 }; body.Children.Add(name);
            body.Children.Add(CreateRecipeDraftFields(selected.Schema, values));
            var notice = new TextBlock { Text = "参数成为新模板的默认值。保存到模板册后，点选才会使用；取消不改变当前海报。", TextWrapping = TextWrapping.Wrap }; body.Children.Add(notice);
            var dialog = PaperDialog("留一份自己的", new ScrollViewer { Content = body, MaxHeight = Math.Max(280, UiCRoot.ActualHeight - 180) }, "取消", "保存到“我的”");
            dialog.PrimaryButtonClick += (_, args) =>
            {
                args.Cancel = true;
                try
                {
                    if (!TemplateOperationReady() || _view?.SelectedPosterId != target || _view.Projection.Revision != revision)
                        throw new ArgumentException("当前海报或设置已变化，请重新打开。未保存副本。");
                    var recipe = TemplateRecipeCopy.Create(selected, "mine-" + Guid.NewGuid().ToString("N"), name.Text.Trim(), values);
                    var result = _registerTemplate?.Invoke(TemplateRecipeCodec.Serialize(recipe), null, revision);
                    if (result?.Succeeded != true) throw new ArgumentException(result?.Error ?? "副本未保存，请重试。");
                    args.Cancel = false;
                }
                catch (ArgumentException error) { notice.Text = error.Message; }
            };
            await dialog.ShowAsync();
        }
        catch (InvalidOperationException) { CommonSettingsNotice.Text = "当前无法打开副本窗口，请先关闭其他对话框。"; }
        finally { _templateReferenceOpen = false; }
    }

    private StackPanel CreateRecipeDraftFields(TemplateParameterSchema schema, Dictionary<string, TemplateParameterValue> values)
    {
        var panel = new StackPanel { Spacing = 14 };
        foreach (var group in schema.Properties.GroupBy(p => p.Group))
        {
            panel.Children.Add(new TextBlock { Text = group.Key, Opacity = .65, FontSize = 11 });
            foreach (var property in group)
            {
                if (property.Kind == TemplateParameterKind.Boolean)
                {
                    var field = new ToggleSwitch { Header = property.Label, IsOn = values[property.Key].Boolean!.Value, OnContent = "开启", OffContent = "关闭" };
                    field.Toggled += (_, _) => values[property.Key] = TemplateParameterValue.FromBoolean(field.IsOn); panel.Children.Add(field);
                }
                else if (property.Kind == TemplateParameterKind.Range)
                {
                    var field = new Slider { Header = property.Label, Minimum = property.Minimum!.Value, Maximum = property.Maximum!.Value,
                        StepFrequency = property.Step!.Value, Value = values[property.Key].Number!.Value };
                    field.ValueChanged += (_, _) => values[property.Key] = TemplateParameterValue.FromNumber(field.Value); panel.Children.Add(field);
                }
                else
                {
                    var row = new StackPanel { Spacing = 6 };
                    var field = new TextBox { Header = property.Label, Text = values[property.Key].Color!, MaxLength = 7 };
                    var chip = new Border { Width = 32, Height = 20, Background = SampleBrush(field.Text), CornerRadius = new(3) };
                    var pick = new Button { Content = chip, HorizontalAlignment = HorizontalAlignment.Left };
                    var picker = new ColorPicker { IsAlphaEnabled = false, IsMoreButtonVisible = false, Width = 230, Color = SampleBrush(field.Text).Color };
                    pick.Flyout = new Flyout { Content = picker };
                    picker.ColorChanged += (_, e) => field.Text = $"#{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}";
                    field.TextChanging += (_, _) => { values[property.Key] = TemplateParameterValue.FromColor(field.Text); if (TryLampColor(field.Text, out var color)) chip.Background = new SolidColorBrush(color); };
                    row.Children.Add(field); row.Children.Add(pick); panel.Children.Add(row);
                }
            }
        }
        return panel;
    }

    private async void ShowTemplateBook()
    {
        if (_templateReferenceOpen || _view?.SelectedPoster is not { } target || Content.XamlRoot is null) return;
        _templateReferenceOpen = true;
        try
        {
            string id = target.Poster.PosterId; long revision = _view.Projection.Revision; string? choice = null;
            var body = new StackPanel { Spacing = 14 };
            var search = new TextBox { PlaceholderText = "查找模板" }; body.Children.Add(search);
            var filters = new ComboBox { ItemsSource = new[] { "全部", "内置", "我的" }, SelectedIndex = 0, MinWidth = 100 }; body.Children.Add(filters);
            var grid = new Grid { ColumnSpacing = 16, RowSpacing = 18 }; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var count = new TextBlock { FontSize = 11, Opacity = .65 }; body.Children.Add(grid); body.Children.Add(count);
            var dialog = PaperDialog("海报模板册", new ScrollViewer { Content = body, MaxHeight = Math.Max(280, UiCRoot.ActualHeight - 180) });
            void RenderBook()
            {
                grid.Children.Clear(); grid.RowDefinitions.Clear();
                var recipes = _templateCatalog.Where(r => r.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase) &&
                    (filters.SelectedIndex == 0 || PosterPresentationPolicy.IsBuiltIn(r.Id) == (filters.SelectedIndex == 1))).ToArray();
                foreach (var recipe in recipes)
                {
                    int n = grid.Children.Count; if (n % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var card = new StackPanel { Spacing = 9 }; card.Children.Add(CreateTemplateSampleArt(recipe));
                    card.Children.Add(new TextBlock { Text = recipe.Name + (target.Poster.TemplateKey == recipe.Id ? "  ✓ 使用中" : "") });
                    var button = new Button { Content = card, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsEnabled = TemplateOperationReady() };
                    button.Click += (_, _) => { choice = recipe.Id; dialog.Hide(); };
                    Grid.SetColumn(button, n % 2); Grid.SetRow(button, n / 2); grid.Children.Add(button);
                }
                count.Text = $"{recipes.Length} 张样片 · 点选即使用 · 仅当前海报";
            }
            search.TextChanged += (_, _) => RenderBook(); filters.SelectionChanged += (_, _) => RenderBook(); RenderBook();
            await dialog.ShowAsync();
            if (choice is null) return;
            if (_view?.SelectedPosterId != id || _view.Projection.Revision != revision || !TemplateOperationReady())
            { CommonSettingsNotice.Text = "当前海报或修改状态已变化，请重新打开模板册。"; return; }
            SelectTemplateKey(choice); RenderUiCTemplateSummary(); RenderTemplateSamples();
        }
        catch (InvalidOperationException) { CommonSettingsNotice.Text = "当前无法打开模板册，请先关闭其他对话框。"; }
        finally { _templateReferenceOpen = false; }
    }
}
