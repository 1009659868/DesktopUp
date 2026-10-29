using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Windows.System;
using Windows.Foundation;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private string? tool, drawingId;
    private Point press, panPress;
    private bool dragging, drawing, panning, marquee;
    private double panX, panY;
    private readonly HashSet<string> initialSelection = [];
    private static bool Down(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
    private void Bind()
    {
        board.PointerPressed += PointerDown; board.PointerMoved += PointerMove; board.PointerReleased += (_, e) => Finish(e, true);
        board.PointerCanceled += (_, e) => Finish(e, false);
        board.PointerCaptureLost += (_, e) => { if (dragging) Finish(e, false); };
        viewport.PointerPressed += (_, e) =>
        {
            if (dragging || !(Down(VirtualKey.Space) || tool == "hand" || e.GetCurrentPoint(viewport).Properties.IsMiddleButtonPressed)) return;
            BeginPan(e); viewport.CapturePointer(e.Pointer); e.Handled = true;
        };
        viewport.PointerMoved += (_, e) => { if (panning) Pan(e); };
        viewport.PointerReleased += (_, e) => { if (panning && !dragging) { panning = false; viewport.ReleasePointerCaptures(); e.Handled = true; } };
        viewport.PointerCanceled += (_, _) => { panning = false; viewport.ReleasePointerCaptures(); };
        viewport.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((_, e) =>
        {
            if (dragging || handleGesture) return;
            Zoom(Math.Exp(e.GetCurrentPoint(viewport).Properties.MouseWheelDelta * .0015)); e.Handled = true;
        }), true);
        board.DoubleTapped += (_, e) => Try(() =>
        {
            var node = Hit(e.GetPosition(board)); if (node is null) return;
            if (node.Type == "layout.group@1") { context = node.Id; selected.Clear(); Render(); }
            else if (node.Type == "draw.text@1") InlineText(node);
            e.Handled = true;
        });
        shell.KeyDown += (_, e) =>
        {
            if (IsTextInput(e.OriginalSource)) return;
            if (IsStudioRunning) { if (e.Key == VirtualKey.Escape) { StopStudioRun(); Render(); e.Handled = true; } return; }
            bool ctrl = Down(VirtualKey.Control), shift = Down(VirtualKey.Shift);
            if (e.Key == VirtualKey.Escape)
            {
                bool activeGesture = dragging || panning || handleGesture;
                CancelInteraction(); if (!activeGesture) selected.Clear(); SetTool(null); Render();
            }
            else if (ctrl && e.Key == VirtualKey.Z) Edit(shift ? document.Redo : document.Undo);
            else if (ctrl && e.Key == VirtualKey.Y) Edit(document.Redo);
            else if (ctrl && e.Key == VirtualKey.G) { if (shift) UngroupSelection(); else GroupSelection(); }
            else if (ctrl && e.Key == VirtualKey.D) Duplicate();
            else if (ctrl && e.Key == VirtualKey.S) SaveWork();
            else if (e.Key == VirtualKey.Delete) DeleteSelection();
            else if (!ctrl && e.Key == VirtualKey.V) SetTool(null);
            else if (!ctrl && e.Key == VirtualKey.H) SetTool("hand");
            else if (!ctrl && e.Key == VirtualKey.R) SetTool("draw.panel@1");
            else if (!ctrl && e.Key == VirtualKey.O) SetTool("draw.ellipse@1");
            else if (!ctrl && e.Key == VirtualKey.T) SetTool("draw.text@1");
            else if (!ctrl && e.Key == VirtualKey.C) SetTool("content.slot@1");
            else if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
            {
                double amount = shift ? 10 : 1;
                Nudge(e.Key == VirtualKey.Left ? -amount : e.Key == VirtualKey.Right ? amount : 0,
                    e.Key == VirtualKey.Up ? -amount : e.Key == VirtualKey.Down ? amount : 0);
            }
            else return;
            e.Handled = true;
        };
        BindDrop();
    }
    private CanvasNode? Hit(Point point) => geometry?.Nodes.Reverse().FirstOrDefault(p => p.Node.Parent == context
        && !layerState.Hidden.Contains(p.Node.Id) && !layerState.Locked.Contains(p.Node.Id)
        && (showContentReference || p.Node.Type != "content.slot@1")
        && CanvasEditingGeometry.Hit(p.Bounds, p.Rotation, point.X, point.Y))?.Node;
    private CanvasRect ContextBounds => geometry!.Nodes.First(n => n.Node.Id == context).Bounds;
    private double ContextRotation => geometry!.Nodes.First(n => n.Node.Id == context).Rotation;
    private Point InContext(Point point)
    {
        var parent = ContextBounds;
        var local = CanvasEditingGeometry.Rotate(point.X - parent.X - parent.Width / 2, point.Y - parent.Y - parent.Height / 2, -ContextRotation);
        return new(parent.X + parent.Width / 2 + local.X, parent.Y + parent.Height / 2 + local.Y);
    }
    private void PointerDown(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(board).Properties;
        if (InputControl(e.OriginalSource) || !(properties.IsLeftButtonPressed || properties.IsMiddleButtonPressed)) return;
        Try(() =>
        {
            viewport.Focus(FocusState.Programmatic); press = e.GetCurrentPoint(board).Position;
            panning = Down(VirtualKey.Space) || tool == "hand" || properties.IsMiddleButtonPressed;
            drawing = tool is not null && !panning; marquee = false;
            if (panning) BeginPan(e);
            else if (drawing)
            {
                if (tool == "content.slot@1") { context = document.Design.Recipe.Nodes[0].Id; Render(); }
                press = InContext(press); var parent = ContextBounds;
                double x = Math.Clamp((press.X - parent.X) / parent.Width, 0, .98), y = Math.Clamp((press.Y - parent.Y) / parent.Height, 0, .98);
                var box = new CanvasBox(x, y, Math.Min(.3, 1 - x), Math.Min(.15, 1 - y));
                var node = tool == "content.slot@1" ? document.Design.Recipe.Nodes.Single(n => n.Type == "content.slot@1") with { Parent = context, Box = box } : NewNode(tool!, box);
                if (layerState.Locked.Contains(node.Id)) throw new ArgumentException("请先在图层列表解锁收纳区域。");
                drawingId = node.Id;
                pending = document.WithNodes(tool == "content.slot@1" ? document.Design.Recipe.Nodes.Select(n => n.Id == node.Id ? node : n) : document.Design.Recipe.Nodes.Append(node));
                Select(node.Id); Render(false);
            }
            else
            {
                if (SelectTitleControlAt(press)) { e.Handled = true; return; }
                selectedTitleControl = null;
                var hit = Hit(press); marquee = hit is null;
                if (!Down(VirtualKey.Shift) && (hit is null || !selected.Contains(hit.Id))) selected.Clear();
                initialSelection.Clear(); initialSelection.UnionWith(selected);
                if (hit is not null)
                {
                    if (Down(VirtualKey.Shift)) { if (!selected.Add(hit.Id)) selected.Remove(hit.Id); }
                    else selected.Add(hit.Id);
                }
                Render();
            }
            dragging = board.CapturePointer(e.Pointer); e.Handled = true;
        });
    }
    private void BeginPan(PointerRoutedEventArgs e)
    { panning = true; panPress = e.GetCurrentPoint(viewport).Position; panX = viewport.HorizontalOffset; panY = viewport.VerticalOffset; }
    private void Pan(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(viewport).Position;
        viewport.ChangeView(panX - point.X + panPress.X, panY - point.Y + panPress.Y, null, true); e.Handled = true;
    }
    private void PointerMove(object sender, PointerRoutedEventArgs e)
    {
        if (!dragging) return;
        Try(() =>
        {
            var point = e.GetCurrentPoint(board).Position;
            if (panning) { Pan(e); return; }
            var parent = ContextBounds;
            if (drawing)
            {
                point = InContext(point);
                // A text click retains its default readable box. Dragging draws its actual box.
                if (Math.Abs(point.X - press.X) + Math.Abs(point.Y - press.Y) < 4) return;
                double x = Math.Clamp((Math.Min(press.X, point.X) - parent.X) / parent.Width, 0, .99), y = Math.Clamp((Math.Min(press.Y, point.Y) - parent.Y) / parent.Height, 0, .99);
                var box = new CanvasBox(x, y, Math.Clamp(Math.Abs(point.X - press.X) / parent.Width, .01, 1 - x), Math.Clamp(Math.Abs(point.Y - press.Y) / parent.Height, .01, 1 - y));
                pending = pending! with { Recipe = pending.Recipe with { Nodes = pending.Recipe.Nodes.Select(n => n.Id == drawingId ? n with { Box = box } : n).ToArray() } };
                QueueGestureFrame(); return;
            }
            if (marquee)
            {
                double x = Math.Min(press.X, point.X), y = Math.Min(press.Y, point.Y), w = Math.Abs(point.X - press.X), h = Math.Abs(point.Y - press.Y);
                selected.Clear(); selected.UnionWith(initialSelection);
                foreach (var n in geometry!.Nodes.Where(n => n.Node.Parent == context && !layerState.Locked.Contains(n.Node.Id) && !layerState.Hidden.Contains(n.Node.Id)
                    && n.Bounds.X >= x && n.Bounds.Y >= y && n.Bounds.Right <= x + w && n.Bounds.Bottom <= y + h)) selected.Add(n.Node.Id);
                Render(false);
                Adorn(new Border { BorderBrush = Ink("#438CFF"), BorderThickness = new(1),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(24, 67, 140, 255)), IsHitTestVisible = false }, new(x, y, Math.Max(1, w), Math.Max(1, h)));
                return;
            }
            if (Math.Abs(point.X - press.X) + Math.Abs(point.Y - press.Y) < 2) return;
            double dx = point.X - press.X, dy = point.Y - press.Y;
            if (Down(VirtualKey.Shift)) { if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0; }
            pending = MoveSelection(dx, dy, snapEnabled && !Down(VirtualKey.Menu)); QueueGestureFrame();
        });
        e.Handled = true;
    }
    private CanvasDesign MoveSelection(double x, double y, bool snap)
    {
        if (SelectedTitleControl is not null) return document.Design;
        var chosen = document.Design.Recipe.Nodes.Where(n => selected.Contains(n.Id) && !layerState.Locked.Contains(n.Id)).ToArray();
        if (chosen.Length == 0) return document.Design;
        var parent = ContextBounds;
        var local = CanvasEditingGeometry.Rotate(x, y, -ContextRotation);
        double dx = local.X / parent.Width, dy = local.Y / parent.Height;
        double left = chosen.Min(n => n.Box.X), top = chosen.Min(n => n.Box.Y);
        double right = chosen.Max(n => n.Box.X + n.Box.Width), bottom = chosen.Max(n => n.Box.Y + n.Box.Height);
        if (snap)
        {
            var targets = document.Design.Recipe.Nodes.Where(n => n.Parent == context && !selected.Contains(n.Id) && !layerState.Hidden.Contains(n.Id)).Select(n => n.Box).Append(new CanvasBox()).ToArray();
            dx = SnapDelta(left, right, dx, targets.SelectMany(b => new[] { b.X, b.X + b.Width / 2, b.X + b.Width }), 5 / (parent.Width * viewport.ZoomFactor));
            dy = SnapDelta(top, bottom, dy, targets.SelectMany(b => new[] { b.Y, b.Y + b.Height / 2, b.Y + b.Height }), 5 / (parent.Height * viewport.ZoomFactor));
        }
        dx = Math.Clamp(dx, -left, 1 - right); dy = Math.Clamp(dy, -top, 1 - bottom);
        return document.WithNodes(document.Design.Recipe.Nodes.Select(n => selected.Contains(n.Id) && !layerState.Locked.Contains(n.Id)
            ? n with { Box = n.Box with { X = n.Box.X + dx, Y = n.Box.Y + dy } } : n));
    }
    private static double SnapDelta(double start, double end, double delta, IEnumerable<double> targets, double threshold)
    {
        double correction = 0, best = threshold;
        foreach (double target in targets)
            foreach (double source in new[] { start, (start + end) / 2, end })
            {
                double distance = target - source - delta;
                if (Math.Abs(distance) < best) { best = Math.Abs(distance); correction = distance; }
            }
        return delta + correction;
    }
    private static bool InputControl(object original)
    {
        for (var element = original as DependencyObject; element is not null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (element is TextBox or NumberBox or Button or Microsoft.UI.Xaml.Controls.Primitives.Thumb) return true;
        return false;
    }
    private static bool IsTextInput(object original)
    {
        for (var element = original as DependencyObject; element is not null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (element is TextBox or NumberBox) return true;
        return false;
    }
    private void CancelInteraction()
    {
        dragging = drawing = panning = handleGesture = marquee = false; pending = null; CancelGestureFrame();
        board.ReleasePointerCaptures(); viewport.ReleasePointerCaptures();
    }
    private void Finish(PointerRoutedEventArgs e, bool commit)
    {
        if (!dragging) return;
        bool wasDrawing = drawing, wasText = drawing && tool == "draw.text@1"; var next = pending;
        dragging = drawing = panning = false; pending = null; CancelGestureFrame(); board.ReleasePointerCaptures();
        if (commit && next is not null) Edit(() => document.Set(next)); else Render();
        if (wasDrawing)
        {
            SetTool(null); Render();
            if (commit && wasText && drawingId is not null && document.Design.Recipe.Nodes.Any(n => n.Id == drawingId)) InlineText(Current(drawingId));
        }
        e.Handled = true;
    }
    private CanvasNode NewNode(string type, CanvasBox box)
    {
        var props = new Dictionary<string, CanvasValue>();
        if (type == "draw.text@1") { props["text"] = new(Text: "双击编辑文字"); props["color"] = new(Text: "#F2E9D9"); props["fontSize"] = new(Number: 18); }
        else { props["fill"] = new(Text: "#F1E3B9"); props["opacity"] = new(Number: 1); if (type == "draw.panel@1") props["radius"] = new(Number: 3); }
        return new(CanvasAuthoring.NewId(), context, type, box, props, Name: Label(type));
    }
    private void InlineText(CanvasNode node)
    {
        if (layerState.Locked.Contains(node.Id)) return;
        var rect = geometry!.Nodes.First(p => p.Node.Id == node.Id).Bounds;
        var text = new TextBox { Text = CanvasDeclaration.Property(document.Design, node, "text").Text, AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MaxLength = 120, FontSize = CanvasDeclaration.Property(document.Design, node, "fontSize").Number!.Value };
        Adorn(text, rect); RotateView(text, geometry!.Nodes.First(p => p.Node.Id == node.Id).Rotation); bool done = false;
        void End(bool commit)
        {
            if (done) return; done = true; board.Children.Remove(text); adorners.Remove(text);
            if (commit) Edit(() => SetProperty(node.Id, "text", new(Text: text.Text))); else Render();
        }
        text.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape || e.Key == VirtualKey.Enter && !Down(VirtualKey.Shift)) { e.Handled = true; End(e.Key == VirtualKey.Enter); } };
        text.LostFocus += (_, _) => { if (!closed && !rebuildingProperties) End(true); };
        text.Focus(FocusState.Programmatic); text.SelectAll();
    }
    private void DeleteSelection() => Edit(() => { RequireWholeLayerSelection(); document.Delete(selected); selected.Clear(); });
    private void GroupSelection() => Edit(() => { RequireWholeLayerSelection(); Select(document.Group(selected)); });
    private void UngroupSelection() => Edit(() =>
    {
        RequireWholeLayerSelection(); if (selected.Count != 1) return; var children = document.Ungroup(selected.Single()); selected.Clear(); selected.UnionWith(children);
    });
    private void Nudge(double dx, double dy) => Edit(() => document.Set(MoveSelection(dx, dy, false)));
    private void Duplicate() => Edit(() =>
    {
        if (selected.Count == 0) return;
        RequireWholeLayerSelection();
        if (selected.Any(layerState.Locked.Contains)) throw new ArgumentException("请先解锁对象。");
        // Compose all copies before publishing one immutable edit / one undo boundary.
        var next = new CanvasAuthoring(document.Design); var copies = new List<string>();
        foreach (string id in selected)
        {
            var source = document.Export(id); copies.Add(next.Insert(source, context, source[0].Box.X + .03, source[0].Box.Y + .03, document.ExportBehavior(id)));
        }
        document.Set(next.Design); selected.Clear(); selected.UnionWith(copies);
    });
}
