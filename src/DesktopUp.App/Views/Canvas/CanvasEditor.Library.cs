using DesktopUp.Domain;
using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private readonly Dictionary<string, CanvasCombination> combinations = new();
    private void RefreshLibrary()
    {
        library.Children.Clear(); library.RowDefinitions.Clear();
        SetActive(assetsButton, !groupsLibrary); SetActive(groupsButton, groupsLibrary);
        combinations.Clear();
        foreach (var combo in workspace.Combinations()) combinations.Add(combo.Id, combo);
        int index = 0;
        if (!groupsLibrary) foreach (var asset in workspace.Assets())
        {
            LibraryTile(asset.Name, "asset:" + asset.Id, new Image { Source = pictures.Thumbnail(asset.Id), Stretch = Stretch.UniformToFill },
                () => Edit(() => InsertAsset(asset.Id, .5, .5)), index++);
        }
        else foreach (var combo in combinations.Values)
        {
            LibraryTile(combo.Name, "combo:" + combo.Id, Icon("group"), () => Edit(() => InsertCombination(combo, .5, .5)), index++);
        }
        if (index == 0)
        {
            var empty = new TextBlock { Text = groupsLibrary ? "选中装饰 → 存入组合库\n再拖入其他作品复用" : "导入 PNG / JPEG\n也可从资源管理器拖入", FontSize = 11, Foreground = Ink("#ADB1B0"), TextWrapping = TextWrapping.Wrap, Margin = new(2, 8, 2, 8) };
            Grid.SetColumnSpan(empty, 2); library.Children.Add(empty);
        }
    }
    private void LibraryTile(string label, string payload, FrameworkElement visual, Action action, int index)
    {
        if (library.RowDefinitions.Count <= index / 2) library.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var button = new Button { CanDrag = true, Tag = payload, Padding = new(0), Background = null, BorderThickness = new(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
        var tile = new StackPanel { Spacing = 5 };
        tile.Children.Add(new Border { Height = 56, CornerRadius = new(6), BorderThickness = new(1), BorderBrush = Ink("#414448"), Background = Ink("#34373B"), Child = visual });
        tile.Children.Add(new TextBlock { Text = label, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
        button.Content = tile; button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, label + " · 拖入画布");
        DragSource(button); Grid.SetRow(button, index / 2); Grid.SetColumn(button, index % 2); library.Children.Add(button);
    }
    private static void DragSource(FrameworkElement element)
    { element.DragStarting += (_, e) => { e.Data.SetText((string)element.Tag); e.Data.RequestedOperation = DataPackageOperation.Copy; }; }
    private void BindDrop()
    {
        board.DragOver += (_, e) => { e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Text) ? DataPackageOperation.Copy : DataPackageOperation.None; e.Handled = true; };
        board.Drop += async (_, e) =>
        {
            var deferral = e.GetDeferral(); var point = InContext(e.GetPosition(board)); string targetWork = current, targetContext = context;
            try
            {
                var parent = ContextBounds; double x = Math.Clamp((point.X - parent.X) / parent.Width, 0, .99), y = Math.Clamp((point.Y - parent.Y) / parent.Height, 0, .99);
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                { var items = await e.DataView.GetStorageItemsAsync(); if (!closed && targetWork == current && targetContext == context) foreach (var file in items.OfType<StorageFile>().Take(8)) await ImportFile(file, x, y, targetWork); }
                else if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string data = await e.DataView.GetTextAsync(); if (closed || targetWork != current || targetContext != context) return;
                    Edit(() => { if (data.StartsWith("asset:", StringComparison.Ordinal)) InsertAsset(data[6..], x, y); else if (data.StartsWith("combo:", StringComparison.Ordinal) && combinations.TryGetValue(data[6..], out var combo)) InsertCombination(combo, x, y); });
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException) { if (!closed) notice.Text = error.Message; }
            finally { deferral.Complete(); }
        };
    }
    private async void ImportPicker()
    {
        if (window is null) return; string target = current;
        try
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".png"); picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            var file = await picker.PickSingleFileAsync(); if (file is not null && !closed && target == current) await ImportFile(file, .12, .12, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException) { if (!closed) notice.Text = e.Message; }
    }
    private async Task ImportFile(StorageFile file, double x, double y, string target)
    {
        string targetContext = context;
        using var input = await file.OpenStreamForReadAsync();
        if (input.Length is < 24 or > 8 * 1024 * 1024) throw new ArgumentException("PNG/JPEG 图片需小于 8 MB。");
        var bytes = new byte[(int)input.Length]; await input.ReadExactlyAsync(bytes);
        if (closed || target != current || targetContext != context) return;
        // Platform decode must succeed before the resource is persisted. Header quota is checked by the port too.
        CanvasImages.Decode(bytes, workspace.ValidateImage(bytes));
        var asset = workspace.Import(file.Name.Length <= 64 ? file.Name : file.Name[..64], bytes);
        Edit(() => InsertAsset(asset.Id, x, y)); groupsLibrary = false; RefreshLibrary();
    }
    private void InsertAsset(string id, double x, double y)
    {
        if (layerState.Locked.Contains(context)) throw new ArgumentException("请先解锁组合。");
        var image = pictures.Load(id); var parent = ContextBounds;
        double w = Math.Min(340, parent.Width * .55), h = w * image.PixelHeight / image.PixelWidth;
        if (h > parent.Height * .8) { w *= parent.Height * .8 / h; h = parent.Height * .8; }
        var box = CanvasEditingGeometry.Constrain(new(x - w / parent.Width / 2, y - h / parent.Height / 2, w / parent.Width, h / parent.Height));
        var node = new CanvasNode(CanvasAuthoring.NewId(), context, "draw.image@1", box,
            new Dictionary<string, CanvasValue> { ["asset"] = new(Text: id), ["opacity"] = new(Number: 1) }, Name: "图片");
        document.Set(document.WithNodes(document.Design.Recipe.Nodes.Append(node))); Select(node.Id);
    }
    private void InsertCombination(CanvasCombination combo, double x, double y)
    {
        if (layerState.Locked.Contains(context)) throw new ArgumentException("请先解锁组合。");
        var nodes = combo.Nodes.ToArray(); var parent = ContextBounds;
        if (combo.Size is { } size)
        {
            double scale = Math.Min(1, Math.Min(parent.Width / size.Width, parent.Height / size.Height));
            nodes[0] = nodes[0] with { Box = nodes[0].Box with { Width = size.Width * scale / parent.Width, Height = size.Height * scale / parent.Height } };
        }
        Select(document.Insert(nodes, context, x - nodes[0].Box.Width / 2, y - nodes[0].Box.Height / 2, combo.Behavior));
    }
}
