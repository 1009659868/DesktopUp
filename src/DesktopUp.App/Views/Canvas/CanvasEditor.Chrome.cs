using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private readonly StackPanel documentTabs = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly Grid body = new(), stage = new();
    private readonly Grid world = new();
    private FrameworkElement? checkerboard;
    private readonly Border leftPanel = new(), propertyPanel = new();
    private readonly ScrollViewer propertyScroll = new();
    private readonly StackPanel contextTools = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly Border contextBar = new();
    private readonly TextBlock zoomValue = new() { FontSize = 11, MinWidth = 38, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock artboardLabel = new() { FontSize = 11, Foreground = Ink("#ADB1B0") };
    private readonly TextBlock layerCount = new() { FontSize = 11, Foreground = Ink("#ADB1B0"), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button previewCancel = new() { Content = "取消预览", Visibility = Visibility.Collapsed };
    private readonly Dictionary<string, Button> toolButtons = new();
    private Button undoButton = null!, redoButton = null!, snapButton = null!, assetsButton = null!, groupsButton = null!;
    private bool narrow, snapEnabled = true, groupsLibrary, focusCanvas;

    private void Build()
    {
        shell.Background = Ink("#202225");
        foreach (var height in new[] { new GridLength(50), GridLength.Auto, new GridLength(44), new GridLength(1, GridUnitType.Star), new GridLength(32) })
            shell.RowDefinitions.Add(new() { Height = height });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border { Width = 27, Height = 27, CornerRadius = new(8), Background = Ink("#7BC9AD"), Child = Icon("layers", "#172D27") });
        brand.Children.Add(new TextBlock { Text = "DesktopUp  创作工作台", FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Foreground = Ink("#E6E7E7") });
        brand.Children.Add(new TextBlock { Text = "｜  设计草稿", FontSize = 11, Foreground = Ink("#ADB1B0"), VerticalAlignment = VerticalAlignment.Center });
        var title = new Grid { Padding = new(16, 0, 140, 0), Background = Ink("#202225") }; title.Children.Add(brand);
        shell.Children.Add(title);
        if (window is not null)
        {
            window.ExtendsContentIntoTitleBar = true; window.SetTitleBar(title);
            window.AppWindow.TitleBar.ButtonBackgroundColor = global::Windows.UI.Color.FromArgb(255, 32, 34, 37);
            window.AppWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            window.AppWindow.TitleBar.ButtonInactiveBackgroundColor = global::Windows.UI.Color.FromArgb(255, 32, 34, 37);
        }
        var docs = TwoColumns();
        docs.Children.Add(new ScrollViewer { Content = documentTabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var publish = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        publish.Children.Add(Command("预览作品", "play", Preview));
        StyleButton(apply); apply.Background = Ink("#334D47"); apply.Foreground = Ink("#7BC9AD"); apply.Click += (_, _) => Try(Commit); publish.Children.Add(apply);
        StyleButton(previewCancel); previewCancel.Click += (_, _) => CancelPreview(); publish.Children.Add(previewCancel);
        Grid.SetColumn(publish, 1); docs.Children.Add(publish); RowFrame(docs, 1, new(10, 5, 10, 5));

        var toolbar = TwoColumns();
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        Tool(tools, "选择", "pointer", null, "选择／框选 · V");
        Tool(tools, "平移", "hand", "hand", "平移 · H / 空格拖动");
        tools.Children.Add(Separator());
        Tool(tools, "形状", "square", "draw.panel@1", "绘制矩形 · R");
        Tool(tools, "椭圆", "circle", "draw.ellipse@1", "绘制椭圆 · O");
        Tool(tools, "文字", "text", "draw.text@1", "添加文字 · T");
        Tool(tools, "收纳区", "grid", "content.slot@1", "重新绘制唯一收纳区域 · C；不会复制真实文件");
        tools.Children.Add(Command("导入素材", "image", ImportPicker));
        toolbar.Children.Add(tools);
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        undoButton = Command("撤销", "undo", () => Edit(document.Undo), true); history.Children.Add(undoButton);
        redoButton = Command("重做", "redo", () => Edit(document.Redo), true); history.Children.Add(redoButton);
        history.Children.Add(Separator());
        var help = Command("快捷操作", "keyboard", () => { }, true);
        help.Flyout = new Flyout { Content = new TextBlock { Text = "V 选择／框选   H 平移   空格 临时平移\nR 矩形   O 椭圆   T 文字   C 收纳区域\n滚轮缩放   Shift 多选／保持比例\nAlt 临时关闭吸附   Esc 取消手势\nCtrl+Z 撤销   Ctrl+Shift+Z 重做\nCtrl+G 组合   Ctrl+Shift+G 拆组\nCtrl+D 复制   Delete 删除装饰\nCtrl+S 保存草稿（不应用）", FontSize = 12, LineHeight = 25 } };
        history.Children.Add(help); Grid.SetColumn(history, 1); toolbar.Children.Add(history); RowFrame(toolbar, 2, new(10, 4, 10, 4));

        body.ColumnDefinitions.Add(new() { Width = new(178) }); body.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new(224) });
        body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var left = new Grid();
        left.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); left.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var layerArea = new Grid { Padding = new(8) };
        layerArea.RowDefinitions.Add(new() { Height = new(28) }); layerArea.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var layerHeader = new Grid(); layerHeader.Children.Add(new TextBlock { Text = "图层", FontSize = 11, Foreground = Ink("#ADB1B0") }); layerHeader.Children.Add(layerCount); layerArea.Children.Add(layerHeader);
        var layerScroll = new ScrollViewer { Content = layers }; Grid.SetRow(layerScroll, 1); layerArea.Children.Add(layerScroll); left.Children.Add(layerArea);
        var libArea = new Grid { Padding = new(8), BorderBrush = Ink("#414448"), BorderThickness = new(0, 1, 0, 0) };
        libArea.RowDefinitions.Add(new() { Height = GridLength.Auto }); libArea.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var libTabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new(0, 0, 0, 8) };
        assetsButton = Command("素材", "", () => { groupsLibrary = false; RefreshLibrary(); }); assetsButton.Width = 70;
        groupsButton = Command("组合库", "", () => { groupsLibrary = true; RefreshLibrary(); }); groupsButton.Width = 70;
        libTabs.Children.Add(assetsButton); libTabs.Children.Add(groupsButton); libArea.Children.Add(libTabs);
        library.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); library.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var libScroll = new ScrollViewer { Content = library }; Grid.SetRow(libScroll, 1); libArea.Children.Add(libScroll); Grid.SetRow(libArea, 1); left.Children.Add(libArea);
        leftPanel.Child = left; leftPanel.Background = Ink("#292B2F"); leftPanel.BorderBrush = Ink("#414448"); leftPanel.BorderThickness = new(0, 0, 1, 0); body.Children.Add(leftPanel);

        stage.Background = Ink("#191D20"); stage.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); stage.RowDefinitions.Add(new() { Height = new(40) });
        world.Width = board.Width + 96; world.Height = board.Height + 96;
        board.Margin = new(48); board.HorizontalAlignment = HorizontalAlignment.Left; board.VerticalAlignment = VerticalAlignment.Top;
        world.Children.Add(board); artboardLabel.Margin = new(48, 20, 0, 0); artboardLabel.VerticalAlignment = VerticalAlignment.Top; world.Children.Add(artboardLabel);
        viewport.Content = world; stage.Children.Add(viewport); BuildCheckerboard();
        contextBar.Child = contextTools; contextBar.Background = Ink("#34373B"); contextBar.BorderBrush = Ink("#414448"); contextBar.BorderThickness = new(1);
        contextBar.CornerRadius = new(8); contextBar.Padding = new(4); contextBar.Margin = new(10, 12, 10, 0); contextBar.HorizontalAlignment = HorizontalAlignment.Center; contextBar.VerticalAlignment = VerticalAlignment.Top;
        stage.Children.Add(contextBar);
        StyleButton(back); back.Background = Ink("#34373B"); back.HorizontalAlignment = HorizontalAlignment.Left; back.VerticalAlignment = VerticalAlignment.Bottom; back.Margin = new(12);
        back.Click += (_, _) => LeaveGroup(); stage.Children.Add(back);
        var zoom = TwoColumns(); zoom.Background = Ink("#292B2F"); zoom.Padding = new(8, 2, 8, 2);
        var zoomCommands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        zoomCommands.Children.Add(Command("缩小", "minus", () => Zoom(1 / 1.15), true)); zoomCommands.Children.Add(zoomValue);
        zoomCommands.Children.Add(Command("放大", "plus", () => Zoom(1.15), true)); zoomCommands.Children.Add(Command("适应", "", Fit)); zoom.Children.Add(zoomCommands);
        var viewCommands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        snapButton = Command("吸附", "magnet", () => { snapEnabled = !snapEnabled; SetActive(snapButton, snapEnabled); });
        SetActive(snapButton, true); viewCommands.Children.Add(snapButton);
        viewCommands.Children.Add(Command("收纳内容参考（仅编辑器）", "eye", () =>
        {
            showContentReference = !showContentReference;
            if (showContentReference) designOnlyWorks.Remove(current); else designOnlyWorks.Add(current);
            Render(); RefreshContentReadback();
        }, true));
        viewCommands.Children.Add(Command("专注画布", "sidebar", () => { focusCanvas = !focusCanvas; Responsive(); }, true));
        Grid.SetColumn(viewCommands, 1); zoom.Children.Add(viewCommands); Grid.SetRow(zoom, 1); stage.Children.Add(zoom);
        Grid.SetColumn(stage, 1); body.Children.Add(stage);
        propertyScroll.Content = inspector; propertyScroll.Padding = new(12); propertyPanel.Child = propertyScroll;
        propertyPanel.Background = Ink("#202225"); propertyPanel.BorderBrush = Ink("#414448"); propertyPanel.BorderThickness = new(1, 0, 0, 0);
        Grid.SetColumn(propertyPanel, 2); body.Children.Add(propertyPanel); Grid.SetRow(body, 3); shell.Children.Add(body);
        var status = TwoColumns(); status.Children.Add(notice);
        var save = Command("保存草稿", "save", SaveWork); save.MinHeight = 26; save.Padding = new(6, 2, 6, 2); Grid.SetColumn(save, 1); status.Children.Add(save);
        RowFrame(status, 4, new(12, 0, 12, 0));
        notice.Text = "拖动只改设计草稿 · 真实文件由海报管理";
        shell.SizeChanged += (_, _) => Responsive(); viewport.ViewChanged += (_, _) => UpdateZoom();
        Responsive();
    }
    private void Responsive()
    {
        bool next = shell.ActualWidth > 0 && shell.ActualWidth <= 850, changed = next != narrow;
        narrow = next;
        body.ColumnDefinitions[0].Width = new(focusCanvas ? 0 : narrow ? 150 : 178);
        body.ColumnDefinitions[2].Width = new(focusCanvas || narrow ? 0 : 224);
        leftPanel.Visibility = propertyPanel.Visibility = focusCanvas ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRow(propertyPanel, narrow ? 1 : 0); Grid.SetColumn(propertyPanel, narrow ? 0 : 2); Grid.SetColumnSpan(propertyPanel, narrow ? 3 : 1);
        propertyPanel.BorderThickness = narrow ? new(0, 1, 0, 0) : new(1, 0, 0, 0);
        propertyScroll.MaxHeight = narrow ? 210 : double.PositiveInfinity;
        if (changed && document is not null) Properties();
    }
    private static Grid TwoColumns()
    {
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); return grid;
    }
    private void RowFrame(UIElement child, int row, Thickness padding)
    {
        var frame = new Border { Child = child, Padding = padding, BorderBrush = Ink("#414448"), BorderThickness = new(0, 0, 0, 1) };
        Grid.SetRow(frame, row); shell.Children.Add(frame);
    }
    private static void StyleButton(Button b)
    {
        b.Padding = new(7, 5, 7, 5); b.MinWidth = 0; b.MinHeight = 30; b.FontSize = 12; b.BorderThickness = new(0);
        b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); b.Foreground = Ink("#E6E7E7"); b.CornerRadius = new(6);
    }
    private static Button Command(string label, string icon, Action action, bool iconOnly = false)
    {
        var b = new Button(); StyleButton(b);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (icon.Length != 0) content.Children.Add(Icon(icon));
        if (!iconOnly) content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        b.Content = content; AutomationProperties.SetName(b, label); ToolTipService.SetToolTip(b, label);
        b.Click += (_, _) => action(); return b;
    }
    private static void SetActive(Button b, bool active)
    { b.Background = active ? Ink("#334D47") : new SolidColorBrush(Microsoft.UI.Colors.Transparent); b.Foreground = active ? Ink("#7BC9AD") : Ink("#E6E7E7"); }
    private void Tool(Panel row, string label, string icon, string? type, string tip)
    {
        var button = Command(label, icon, () => SetTool(type), true); ToolTipService.SetToolTip(button, tip);
        toolButtons[type ?? "select"] = button; row.Children.Add(button); SetActive(button, type is null);
    }
    private void SetTool(string? type)
    {
        CancelInteraction(); tool = type;
        foreach (var pair in toolButtons) SetActive(pair.Value, pair.Key == (tool ?? "select"));
    }
    private void LeaveGroup()
    {
        context = document.Design.Recipe.Nodes.First(n => n.Id == context).Parent ?? document.Design.Recipe.Nodes[0].Id;
        selected.Clear(); Render();
    }
    private static Border Separator() => new() { Width = 1, Height = 20, Margin = new(4, 0, 4, 0), Background = Ink("#414448") };
    private void BuildCheckerboard()
    {
        if (checkerboard is not null) board.Children.Remove(checkerboard);
        var data = new System.Text.StringBuilder();
        for (int y = 0; y < board.Height; y += 10)
            for (int x = 0; x < board.Width; x += 10)
                if ((x / 10 + y / 10) % 2 == 0) data.Append(System.FormattableString.Invariant($"M{x},{y}h10v10h-10Z "));
        var checks = (Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load("<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Fill='#363C3E' Data='" + data + "'/>");
        var backdrop = new Border { Width = board.Width, Height = board.Height, Background = Ink("#303638"), Child = checks, IsHitTestVisible = false };
        Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(backdrop, -100); board.Children.Add(backdrop); checkerboard = backdrop;
    }
    private void UpdateArtboard()
    {
        double width = document.Design.Recipe.Artboard?.Width ?? session!.Width, height = document.Design.Recipe.Artboard?.Height ?? session!.Height - CanvasLayout.Header;
        if (board.Width == width && board.Height == height) return;
        board.Width = width; board.Height = height; world.Width = width + 96; world.Height = height + 96; BuildCheckerboard();
    }
    private static Viewbox Icon(string name, string color = "#C9CFCC")
    {
        string path = name switch
        {
            "pointer" => "M3,2 L20,10 12,12 9,20 Z", "hand" => "M6,12 V6 Q8,3 10,6 V10 M10,6 V3 Q12,1 14,3 V10 M14,5 Q16,3 18,5 V11 M18,8 Q21,6 21,10 V15 Q20,22 12,22 Q8,22 5,17 L2,13 Q2,10 5,11 L8,14",
            "square" => "M4,4 H20 V20 H4 Z", "circle" => "M12,3 A9,9 0 1 1 11.99,3",
            "text" => "M4,4 H20 M12,4 V21 M8,21 H16",
            "grid" => "M3,3 H21 V21 H3 Z M12,3 V21 M3,12 H21",
            "layers" => "M3,3 H21 V21 H3 Z M3,9 H21 M9,9 V21",
            "image" => "M3,5 H21 V21 H3 Z M3,17 L9,11 14,16 18,12 21,15 M15,7 H19 M17,5 V9",
            "plus" => "M12,4 V20 M4,12 H20", "minus" => "M4,12 H20",
            "play" => "M6,3 L21,12 6,21 Z", "undo" => "M8,4 L3,9 8,14 M3,9 H15 Q23,9 21,17 Q19,22 12,20",
            "redo" => "M16,4 L21,9 16,14 M21,9 H9 Q1,9 3,17 Q5,22 12,20",
            "keyboard" => "M2,5 H22 V19 H2 Z M5,9 H6 M9,9 H10 M13,9 H14 M17,9 H18 M5,12 H6 M9,12 H10 M13,12 H14 M17,12 H18 M7,16 H17",
            "eye" => "M2,12 Q12,-2 22,12 Q12,26 2,12 Z M9,12 A3,3 0 1 1 15,12 A3,3 0 1 1 9,12",
            "eye-off" => "M2,2 L22,22 M2,12 Q7,5 12,5 M22,12 Q17,19 12,19",
            "lock" => "M5,10 H19 V22 H5 Z M8,10 V6 Q12,-1 16,6 V10",
            "unlock" => "M5,10 H19 V22 H5 Z M8,10 V6 Q12,-1 16,6",
            "group" => "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z",
            "copy" => "M8,8 H21 V21 H8 Z M4,16 H2 V2 H16 V4",
            "trash" => "M3,5 H21 M8,5 V2 H16 V5 M6,5 V22 H18 V5 M10,9 V18 M14,9 V18",
            "save" => "M3,2 H18 L22,6 V22 H3 Z M7,2 V9 H17 V2 M7,22 V14 H18 V22",
            "magnet" => "M3,3 H8 V12 Q12,20 16,12 V3 H21 V12 Q12,30 3,12 Z M3,8 H8 M16,8 H21",
            "sidebar" => "M3,3 H21 V21 H3 Z M15,3 V21 M10,8 L6,12 10,16",
            "up" => "M4,15 L12,7 20,15", "down" => "M4,9 L12,17 20,9",
            _ => "M3,3 H21 V21 H3 Z"
        };
        var graphic = (Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load("<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Stroke='" + color + "' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Data='" + path + "'/>");
        return new Viewbox { Width = 16, Height = 16, Child = graphic, Stretch = Stretch.Uniform };
    }
}
