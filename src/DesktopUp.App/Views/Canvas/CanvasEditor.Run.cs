using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace DesktopUp;

/// <summary>Canvas-local host. Same scene interpreter, no state commands or file interaction.</summary>
internal sealed partial class CanvasEditor
{
    private CanvasSceneInstance? runScene;
    private Grid? runRoot;
    private Canvas? runTitle;
    private UIElement? runContent;
    private Border? runSurface;
    private bool runCollapsed, runRendering, runAnimate;
    private TaskCompletionSource<bool>? runPosture;
    private bool runDesiredCollapsed;
    private double runFrom, runTo, runPostureStarted;
    private ScenePointer? runPointer;
    private readonly Stopwatch runClock = new();
    private Button runButton = null!;
    private Microsoft.UI.Xaml.Shapes.Path? runMask;
    private SceneRenderParameters? runParameters;
    private double runMaskHeight = -1;
    private bool? runTitleCompact;
    private RectangleGeometry? runClip;
    private bool IsStudioRunning => runScene is not null;
    private void ToggleStudioRun() => Try(() =>
    {
        if (IsStudioRunning) { StopStudioRun(); Render(); return; }
        CancelInteraction(); CloseScriptEditor(); CancelPreview();
        CanvasDeclaration.Validate(document.Design);
        runAnimate = new UISettings().AnimationsEnabled;
        runCollapsed = false; runPointer = null; runClock.Restart();
        runScene = new(pictures); runScene.ConfigureHost(() => runCollapsed, RunPosture);
        runParameters = new("canvas.paint@1", new("#282B30", 0, null), new Dictionary<string, TemplateParameterValue>()) { Canvas = document.Design };
        runRoot = new Grid { Width = board.Width, Height = board.Height, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        runRoot.Children.Add(runScene.Root);
        var content = new Canvas { IsHitTestVisible = false };
        var slot = geometry!.Content; var reference = new Border { Child = ReadOnlyContent(), IsHitTestVisible = false };
        Place(reference, slot); content.Children.Add(reference); runContent = content; runRoot.Children.Add(content);
        runTitle = new Canvas { IsHitTestVisible = false };
        for (int i = 0; i < 3; i++) runTitle.Children.Add(new Border { Child = new TextBlock { MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center } });
        runRoot.Children.Add(runTitle);
        runSurface = new Border { Width = board.Width, Height = board.Height, Child = runRoot, Margin = board.Margin,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Background = Ink("#282B30") };
        world.Children.Add(runSurface); Canvas.SetZIndex(runSurface, 3000); board.Visibility = Visibility.Collapsed;
        runMask = new() { Fill = Ink("#191D20"), IsHitTestVisible = false };
        runRoot.Children.Add(runMask); runMaskHeight = -1;
        runTitleCompact = null; runClip = new(); runRoot.Clip = runClip;
        runSurface.PointerMoved += (_, e) => { var point = e.GetCurrentPoint(runSurface).Position; runPointer = new(point.X, point.Y); PaintStudioRun(); };
        runSurface.PointerExited += (_, _) => { runPointer = null; PaintStudioRun(); };
        runSurface.Tapped += (_, e) =>
        {
            var point = e.GetPosition(runSurface); RunClick(point); e.Handled = true;
        };
        SetActive(runButton, true); notice.Text = "画板运行中 · 直接点击v或绑定UI；再次点运行或Esc停止，不修改桌面海报。";
        contextBar.Visibility = Visibility.Collapsed; back.Visibility = Visibility.Collapsed;
        PaintStudioRun();
    });
    private Task<bool> RunPosture(bool collapsed)
    {
        if (runPosture is not null || runSurface is null) return Task.FromResult(false);
        if (collapsed == runCollapsed) return Task.FromResult(true);
        runDesiredCollapsed = collapsed; runFrom = runSurface.Height; runTo = collapsed ? PosterFrameGeometryPolicy.CollapsedHeightDip : board.Height;
        runPostureStarted = runClock.Elapsed.TotalSeconds;
        runPosture = new(TaskCreationOptions.RunContinuationsAsynchronously); return runPosture.Task;
    }
    private void RunClick(Point point)
    {
        if (runScene is null || runPosture is not null) return;
        if (!runCollapsed && !CanvasSurfaceGeometry.Contains(document.Design.Recipe.Surface!, board.Width, board.Height, point.X, point.Y, 1)) return;
        var title = CanvasSurfaceGeometry.Title(document.Design, board.Width, board.Height, runCollapsed, PosterTitleChromeKind.Basic);
        var hit = title.HitTest(point.X, point.Y);
        if (hit == PosterTitleChromeTarget.Collapse)
        { if (!runScene.InvokeToggle(runAnimate)) _ = RunPosture(!runCollapsed); }
        else if (!runCollapsed && hit == PosterTitleChromeTarget.Desktop)
            runScene.Click(new(point.X, point.Y), runAnimate);
        PaintStudioRun();
    }
    private void PaintStudioRun()
    {
        if (runScene is null || runSurface is null || runRoot is null || runTitle is null) return;
        double clock = runClock.Elapsed.TotalSeconds;
        if (runPosture is { } transition)
        {
            double progress = runAnimate ? Math.Clamp((clock - runPostureStarted) / .18, 0, 1) : 1;
            double eased = progress * progress * (3 - 2 * progress);
            runSurface.Height = runFrom + (runTo - runFrom) * eased;
            if (progress == 1) { runCollapsed = runDesiredCollapsed; runPosture = null; transition.TrySetResult(true); }
        }
        bool compact = runCollapsed && runPosture is null;
        if (runClip!.Rect.Height != runSurface.Height) runClip.Rect = new(0, 0, board.Width, runSurface.Height);
        runScene.Root.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (runContent is not null) runContent.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        runScene.Update(runParameters!, SceneFrameContext.Create(board.Width, board.Height, clock, runPointer, !compact,
            runAnimate ? ScenePauseReason.None : ScenePauseReason.SharedPolicy));
        if (runMask is not null && runMaskHeight != runSurface.Height)
        {
            runMaskHeight = runSurface.Height;
            var shape = document.Design.Recipe.Surface ?? new();
            var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };
            mask.Children.Add(new RectangleGeometry { Rect = new(0, 0, board.Width, runSurface.Height) });
            if (!compact && shape.Shape == "ellipse") mask.Children.Add(new EllipseGeometry { Center = new(board.Width / 2, runSurface.Height / 2), RadiusX = board.Width / 2, RadiusY = runSurface.Height / 2 });
            else mask.Children.Add(RoundedOutline(board.Width, runSurface.Height, compact ? 10 : shape.Shape == "rounded" ? shape.Radius * Math.Min(1, runSurface.Height / board.Height) : 0));
            runMask.Data = mask;
        }
        if (runTitleCompact != compact)
        {
        runTitleCompact = compact;
        var paint = CanvasBehaviorPolicy.Collapsed(document.Design, "#282B30", .96);
        runSurface.Background = compact ? new SolidColorBrush(Ink(paint.Color).Color with { A = (byte)Math.Round(paint.Opacity * 255) }) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var title = CanvasSurfaceGeometry.Title(document.Design, board.Width, board.Height, compact, PosterTitleChromeKind.Basic);
        var node = document.Design.Recipe.Nodes.Single(n => n.Type == "host.title@1");
        var boxes = new[] { title.Name, title.Collapse, title.More };
        var texts = new[] { CanvasDeclaration.Property(document.Design, node, "text").Text!, compact ? "⌃" : "⌄", "⋯" };
        for (int i = 0; i < 3; i++)
        {
            var border = (Border)runTitle.Children[i]; var text = (TextBlock)border.Child; var box = boxes[i];
            Place(border, new(box.X, box.Y, box.Width, box.Height)); text.Text = texts[i];
            text.FontSize = i == 0 ? compact ? 14 : CanvasDeclaration.Property(document.Design, node, "fontSize").Number!.Value : 12;
            text.TextAlignment = i == 0 ? TextAlignment.Left : TextAlignment.Center;
            text.Foreground = Ink(CanvasDeclaration.Property(document.Design, node, i == 0 ? "color" : "actions").Text!);
        }
        }
        if (runScene.ScriptError is { } error) notice.Text = error;
        bool needed = runPosture is not null || runScene.HasActiveScripts || document.Design.Recipe.Nodes.Any(n => n.Type == "effect.rain@1" && !n.Hidden && !compact);
        if (needed && !runRendering) { CompositionTarget.Rendering += RunFrame; runRendering = true; }
        else if (!needed && runRendering) { CompositionTarget.Rendering -= RunFrame; runRendering = false; }
    }
    private void RunFrame(object? sender, object args)
    {
        try { PaintStudioRun(); }
        catch (Exception error) { StopStudioRun(); Render(); notice.Text = "画板运行已停止：" + error.Message; }
    }
    private void StopStudioRun()
    {
        if (runRendering) { CompositionTarget.Rendering -= RunFrame; runRendering = false; }
        runPosture?.TrySetResult(false); runPosture = null; runClock.Stop();
        runScene?.Dispose(); runScene = null;
        if (runSurface is not null) world.Children.Remove(runSurface);
        runSurface = null; runRoot = null; runTitle = null; runContent = null; board.Visibility = Visibility.Visible;
        runParameters = null; runMask = null;
        if (runButton is not null) SetActive(runButton, false);
    }
}
