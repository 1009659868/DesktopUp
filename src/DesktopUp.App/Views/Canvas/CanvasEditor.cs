using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

/// <summary>Workbench lifetime and use-case wiring; draft edits never own real content.</summary>
internal sealed partial class CanvasEditor(Func<CanvasDesignSession> open, Action<StateCommandResult> completed, CanvasImages? images = null,
    Func<CanvasContentSnapshot?>? readContent = null) : IDisposable
{
    private CanvasDesignSession? session;
    private ICanvasWorkspace workspace = null!;
    private CanvasImages pictures = null!;
    private CanvasContentSnapshot? contentSnapshot;
    private bool showContentReference = true;
    private readonly HashSet<string> designOnlyWorks = [];
    private Window? window;
    private bool closed, rebuildingProperties;
    private readonly TaskCompletionSource dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, CanvasAuthoring> documents = new();
    private readonly Dictionary<string, string> names = new();
    private string current = "", context = "root";
    private CanvasAuthoring document = null!;
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private readonly Grid shell = new() { RequestedTheme = ElementTheme.Dark };
    private readonly Microsoft.UI.Xaml.Controls.Canvas board = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), AllowDrop = true };
    private readonly ScrollViewer viewport = new()
    {
        ZoomMode = ZoomMode.Enabled, MinZoomFactor = .15f, MaxZoomFactor = 3,
        HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Enabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        IsTabStop = true
    };
    private readonly StackPanel layers = new() { Spacing = 2 };
    private readonly Grid library = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly StackPanel inspector = new() { Spacing = 12 };
    private readonly TextBlock notice = new() { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11, Foreground = Ink("#ADB1B0"), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button apply = new() { Content = "应用并保存", IsEnabled = false };
    private readonly Button back = new();
    public string? PosterId => session?.PosterId;

    public async Task ShowAsync(XamlRoot ownerRoot, double height)
    {
        session = open(); workspace = session.Workspace ?? throw new InvalidOperationException("设计资源库尚未就绪。");
        pictures = images ?? new CanvasImages(workspace); contentSnapshot = readContent?.Invoke();
        foreach (var work in workspace.Works())
        { documents[work.Id] = Authoring(work.Design); names[work.Id] = work.Name; if (!work.ShowContentReference) designOnlyWorks.Add(work.Id); }
        current = CanvasAuthoring.NewId(); names[current] = "当前海报";
        try { documents[current] = Authoring(session.ReadCurrentDesign()); }
        catch (ArgumentException error)
        {
            // No silent blank substitute for a native template we cannot faithfully convert.
            names.Remove(current); current = CanvasAuthoring.NewId(); names[current] = "空白创作";
            documents[current] = Authoring(CanvasAuthoring.NewArtwork()); designOnlyWorks.Add(current);
            showContentReference = false; notice.Text = error.Message;
        }
        document = documents[current]; context = document.Design.Recipe.Nodes[0].Id;
        board.Width = document.Design.Recipe.Artboard!.Width; board.Height = document.Design.Recipe.Artboard.Height;
        window = new Window { Title = "DesktopUp · 创作工作台", Content = shell };
        window.AppWindow.Resize(new(1280, 900));
        Build(); Bind(); RefreshWorks(); Render(); RefreshLibrary();
        window.Closed += (_, _) => Dispose();
        shell.Loaded += (_, _) => Fit();
        window.Activate();
        try { await dismissed.Task; } finally { Dispose(); }
    }

    private void RefreshWorks()
    {
        documentTabs.Children.Clear();
        foreach (var pair in names)
        {
            var button = Command(pair.Value, "layers", () => Try(() => Switch(pair.Key)));
            button.Padding = new(10, 8, 10, 8);
            SetActive(button, pair.Key == current);
            if (pair.Key == current) button.Loaded += (_, _) => button.StartBringIntoView();
            var underline = new Border { Height = 2, Background = pair.Key == current ? Ink("#7BC9AD") : null };
            var tab = new StackPanel(); tab.Children.Add(button); tab.Children.Add(underline); documentTabs.Children.Add(tab);
        }
        documentTabs.Children.Add(Command("空白创作", "plus", NewWork));
    }
    private CanvasAuthoring Authoring(CanvasDesign design)
    {
        design = CanvasSurfaceGeometry.ForEditing(design, session!.Width, session.Height);
        return new(design with { Recipe = design.Recipe with { Behavior = CanvasBehaviorPrefabs.ForEditing(design) } });
    }
    private void NewWork()
    {
        if (documents.Count >= 128) { notice.Text = "已达到本轮128份作品预算，请先整理已保存作品。"; return; }
        string id = CanvasAuthoring.NewId(); names[id] = "空白创作"; documents[id] = Authoring(CanvasAuthoring.NewArtwork()); designOnlyWorks.Add(id);
        Switch(id); Fit();
    }
    private void Switch(string id)
    {
        if (!documents.TryGetValue(id, out var next)) return;
        StopStudioRun(); CloseScriptEditor(); CancelInteraction(); current = id; document = next; context = next.Design.Recipe.Nodes[0].Id;
        showContentReference = !designOnlyWorks.Contains(id);
        selected.Clear(); Invalidate(); UpdateArtboard(); RefreshWorks(); Render(); Fit();
    }
    private void SaveWork() => Try(() =>
    {
        workspace.SaveWork(current, names[current], document.Design, showContentReference);
        notice.Text = "草稿已保存，未应用到海报；重开工作台可继续编辑。";
    });
    private void Preview() => Try(() =>
    {
        session!.Revise(document.Design); session.Preview(); contentSnapshot = readContent?.Invoke(); RefreshContentReadback(); apply.IsEnabled = true;
        notice.Text = "原海报正在预览 · 应用才保存，取消预览或关闭工作台恢复原设计。";
        previewCancel.Visibility = Visibility.Visible;
    });
    private void CancelPreview()
    {
        session?.Revise(document.Design); apply.IsEnabled = false;
        previewCancel.Visibility = Visibility.Collapsed; notice.Text = "已恢复原海报；当前草稿保留。";
    }
    private void Commit()
    {
        var result = session!.Apply(session.PreviewId ?? ""); completed(result);
        if (!result.Succeeded) throw new ArgumentException(result.Error ?? "应用失败，原设计保留。");
        session.Dispose(); session = open(); contentSnapshot = readContent?.Invoke(); RefreshContentReadback();
        // Keep the independent authoring history. Undoing a published design is a separate use case.
        document = Authoring(session.ReadCurrentDesign()); documents[current] = document; UpdateArtboard();
        selected.Clear(); context = document.Design.Recipe.Nodes[0].Id; apply.IsEnabled = false;
        previewCancel.Visibility = Visibility.Collapsed; Render(); notice.Text = "已应用并保存 · 真实文件、卡片身份与使用统计保持不变。";
    }
    private void Fit()
    {
        if (viewport.ActualWidth < 1 || viewport.ActualHeight < 1) return;
        float zoom = (float)Math.Clamp(Math.Min(Math.Max(100, viewport.ActualWidth - 80) / board.Width,
            Math.Max(100, viewport.ActualHeight - 80) / board.Height), .15, 3);
        viewport.ChangeView(0, 0, zoom, true); UpdateZoom();
    }
    private void Zoom(double factor)
    {
        float next = (float)Math.Clamp(viewport.ZoomFactor * factor, .15, 3);
        double ratio = next / viewport.ZoomFactor;
        viewport.ChangeView((viewport.HorizontalOffset + viewport.ActualWidth / 2) * ratio - viewport.ActualWidth / 2,
            (viewport.VerticalOffset + viewport.ActualHeight / 2) * ratio - viewport.ActualHeight / 2, next, true);
        UpdateZoom();
    }
    private float adornmentZoom;
    private void UpdateZoom()
    {
        zoomValue.Text = $"{viewport.ZoomFactor * 100:0}%";
        if (adornmentZoom == viewport.ZoomFactor) return;
        adornmentZoom = viewport.ZoomFactor;
        if (geometry is not null && selected.Count != 0) RenderGesture();
    }
    private void Invalidate()
    {
        apply.IsEnabled = false; previewCancel.Visibility = Visibility.Collapsed; session?.Revise(document.Design);
    }
    private void Edit(Action action) => Try(() => { StopStudioRun(); CancelGestureFrame(); action(); Invalidate(); UpdateArtboard(); Render(); });
    private void Try(Action action)
    {
        try { if (!closed) action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { apply.IsEnabled = false; notice.Text = e.Message; }
    }
    private static SolidColorBrush Ink(string hex) => new(global::Windows.UI.Color.FromArgb(255,
        Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
    public void Dispose()
    {
        if (closed) return; StopStudioRun(); closed = true; CancelGestureFrame(); session?.Dispose(); session = null;
        window?.Close(); window = null; views.Clear(); board.Children.Clear(); documents.Clear(); dismissed.TrySetResult();
    }
}
