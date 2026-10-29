using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal async Task VerifyCanvasPrefabSequenceForProbe(string directory, CancellationToken token)
    {
        if (_sceneInstance is not CanvasSceneInstance scene) throw new InvalidOperationException("Missing script scene.");
        scene.VerifyTitleSourcesForProbe();
        var samples = new List<object>(); string? failure = null; bool sawCollapse = false;
        void Frame(object? sender, object args)
        {
            if (_titleName is null || CanvasFor(_bounds) is not { } canvas || _geometry.IsCollapsed) return;
            var expected = CanvasSurfaceGeometry.Title(_presentation.Parameters.Canvas!.Current, canvas.Width, canvas.Height, false, _presentation.TitleChrome);
            double y = Microsoft.UI.Xaml.Controls.Canvas.GetTop(_titleName);
            samples.Add(new { Height = _bounds.Height, ExpandedHeight = canvas.Height, TitleY = y, ExpectedY = expected.Name.Y });
            if (Math.Abs(y - expected.Name.Y) > .01) failure = $"Title moved to {y}; expected {expected.Name.Y}; frame {_bounds.Height}.";
        }
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += Frame;
        try
        {
            scene.InvokeSource("context-image", true); UpdateScene();
            for (int i = 0; i < 250; i++)
            {
                await Task.Delay(20, token); sawCollapse |= _authoritativePoster?.IsCollapsed == true;
                if (sawCollapse && _authoritativePoster?.IsCollapsed == false && !scene.HasActiveScripts && !spatial.IsSpatialBusy) break;
            }
            if (!sawCollapse || _authoritativePoster?.IsCollapsed != false || scene.HasActiveScripts || scene.ScriptError is not null)
                throw new InvalidOperationException("Native authored sequence failed: " + scene.ScriptError);
            var image = ((Microsoft.UI.Xaml.Controls.Canvas)scene.Root).Children.OfType<Microsoft.UI.Xaml.Controls.Image>().Single();
            if (image.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform pose || Math.Abs(pose.TranslateX - 32 * CanvasFor(_bounds)!.Scale) > .01)
                throw new InvalidOperationException("Native sequence was reset across posture publication.");
            if (failure is not null) throw new InvalidOperationException(failure);
            if (samples.Count < 2) throw new InvalidOperationException("No native title frame evidence.");
            File.WriteAllText(Path.Combine(directory, "prefab-title-frames.json"), System.Text.Json.JsonSerializer.Serialize(samples));
            if (!InvokeCanvasToggle()) throw new InvalidOperationException("Default native v binding missing.");
            for (int i = 0; _authoritativePoster?.IsCollapsed != true && i < 80; i++) await Task.Delay(20, token);
            if (_authoritativePoster?.IsCollapsed != true) throw new InvalidOperationException("Native v did not collapse.");
            if (!InvokeCanvasToggle()) throw new InvalidOperationException("Collapsed native v lost its binding.");
            for (int i = 0; _authoritativePoster?.IsCollapsed != false && i < 80; i++) await Task.Delay(20, token);
            if (_authoritativePoster?.IsCollapsed != false) throw new InvalidOperationException("Native v did not expand.");
        }
        finally { Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= Frame; }
    }
    internal void VerifyCollapsedCanvasForProbe()
    {
        var color = BackdropColor();
        if (!_geometry.IsCollapsed || !VerifyWindowBackdrop() || color.R != 0x11 || color.G != 0x22 || color.B != 0x33 || color.A != 204)
            throw new InvalidOperationException("Collapsed canvas dropped its declared state paint.");
    }
    internal void VerifyCanvasBehaviorForProbe(string target)
    {
        if (_sceneInstance is not CanvasSceneInstance scene || CanvasFor(_bounds) is not { } layout) throw new InvalidOperationException("Canvas not published.");
        var root = (Microsoft.UI.Xaml.Controls.Canvas)scene.Root;
        var image = root.Children.OfType<Microsoft.UI.Xaml.Controls.Image>().Single();
        void Tick(double seconds, DesktopUp.Application.ScenePointer? pointer, bool reduced = false) =>
            scene.Update(CurrentSceneParameters, DesktopUp.Application.SceneFrameContext.Create(layout.Width, layout.Height, seconds, pointer, true,
                reduced ? DesktopUp.Application.ScenePauseReason.SharedPolicy : DesktopUp.Application.ScenePauseReason.None));
        Tick(10, null); Tick(10.1, null); Tick(10.2, new(20, 100)); Tick(10.3, new(20, 100));
        if (Math.Abs(image.Opacity - .8) > .00001) throw new InvalidOperationException("Pointer enter did not paint declared opacity.");
        scene.Click(new(20, 100), false);
        if (image.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform pose || Math.Abs(pose.TranslateX - 24 * layout.Scale) > .01)
            throw new InvalidOperationException("Blank click did not update decorative transform.");
        Tick(10.4, null, true);
        if (Math.Abs(image.Opacity - .35) > .00001) throw new InvalidOperationException("Reduced motion did not reach leave state.");
    }
    internal void VerifyCurrentBackgroundForProbe(bool canvas)
    {
        if (!VerifyWindowBackdrop()) throw new InvalidOperationException("Backdrop readback failed.");
        var color = BackdropColor();
        if (!canvas)
        {
            if (color.A != (byte)Math.Round(43 * 255d / 100) || color.R != 0 || color.G != 0 || color.B != 0)
                throw new InvalidOperationException("Original black/translucent backdrop changed.");
            return;
        }
        if (color.A != 0 || _sceneInstance?.Root is not Microsoft.UI.Xaml.Controls.Canvas root)
            throw new InvalidOperationException("Canvas retained a second native backdrop.");
        var plate = root.Children.OfType<Microsoft.UI.Xaml.Controls.Border>().Single();
        if (plate.Background is not Microsoft.UI.Xaml.Media.SolidColorBrush ink || ink.Color.R != 0 || ink.Color.G != 0 || ink.Color.B != 0 ||
            Math.Abs(plate.Opacity - Math.Round(43 * 255d / 100) / 255d) > .00001)
            throw new InvalidOperationException("Native canvas omitted or doubled the inherited background.");
    }
    internal Dictionary<string, FrameworkElement> CaptureCanvasCardsForProbe() => Presenter.Cards.ToDictionary(p => p.Key, p => p.Value);
    internal Action CanvasWarmReadbackForProbe()
    {
        var warm = Presenter.Bitmaps.ToArray();
        if (warm.Length == 0) throw new InvalidOperationException("No warm raster to verify.");
        return () =>
        {
            // A zero-height viewport may virtualize cards away. Their ready bitmaps must survive.
            if (warm.Any(p => !ReferenceEquals(Presenter.Bitmaps.GetValueOrDefault(p.Key), p.Value)))
                throw new InvalidOperationException("Posture discarded warm canvas rasters.");
        };
    }
    internal void RequireCanvasCardsForProbe(Dictionary<string, FrameworkElement> previous)
    {
        if (previous.Count != Presenter.Cards.Count || previous.Any(p => !ReferenceEquals(Presenter.Cards.GetValueOrDefault(p.Key), p.Value)))
            throw new InvalidOperationException("Canvas design replaced stable content controls.");
    }
    internal void VerifyCanvasOriginForProbe()
    {
        if (_presentation.Parameters.Canvas?.Current is null) return;
        if (_window?.Content is not FrameworkElement root || Presenter.Surface is null) throw new InvalidOperationException("Missing actual canvas visual.");
        var point = Presenter.Surface.TransformToVisual(root).TransformPoint(new(0, 0));
        if (Math.Abs(point.X - ContentLeftDip) > .1 || Math.Abs(point.Y - ContentTopDip) > .1)
            throw new InvalidOperationException($"Canvas visual/input origin mismatch: {point} vs {ContentLeftDip},{ContentTopDip}");
        if (_sceneInstance is not CanvasSceneInstance) throw new InvalidOperationException("Canvas renderer is missing.");
        if (CanvasSurfaceGeometry.IsWholePoster(_presentation.Parameters.Canvas?.Current))
        {
            if (!VerifyTitleChromeForProbe()) throw new InvalidOperationException("Editable title visual/input mismatch.");
            var layout = CanvasFor(_bounds)!;
            if (layout.Nodes[0].Bounds != new CanvasRect(0, 0, layout.Width, layout.Height))
                throw new InvalidOperationException("Whole poster acquired a reserved top/fit offset.");
            foreach (nint handle in new[] { _windowHandle, _inputIsland!.WindowHandle })
            {
                nint region = CanvasRegionProbe.CreateRectRgn(0, 0, 0, 0);
                try
                {
                    if (CanvasRegionProbe.GetWindowRgn(handle, region) == 0) throw new InvalidOperationException("Native shape not installed.");
                    var design = _presentation.Parameters.Canvas!.Current!;
                    var surface = design.Recipe.Surface!;
                    foreach (var sample in new[] { (1, 1), (_bounds.Width / 2, _bounds.Height / 2), (_bounds.Width / 2, 4), (4, _bounds.Height / 2) })
                    {
                        bool expected = CanvasSurfaceGeometry.Contains(surface, _bounds.Width, _bounds.Height, sample.Item1 + .5, sample.Item2 + .5,
                            Math.Min(_bounds.Width / design.Recipe.Artboard!.Width, _bounds.Height / design.Recipe.Artboard.Height));
                        if (CanvasRegionProbe.PtInRegion(region, sample.Item1, sample.Item2) != expected)
                            throw new InvalidOperationException("Visual/input HWND disagrees with declared outline.");
                    }
                }
                finally { CanvasRegionProbe.DeleteObject(region); }
            }
        }
    }
}
internal static class CanvasRegionProbe
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int l, int t, int r, int b);
    [System.Runtime.InteropServices.DllImport("user32.dll")] internal static extern int GetWindowRgn(nint window, nint region);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] internal static extern bool PtInRegion(nint region, int x, int y);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint value);
}
internal sealed partial class PosterSpatialRuntime
{
    internal PosterVisualWindow CanvasWindowForProbe(string id) => _windows[id];
}

internal sealed partial class CanvasSceneInstance
{
    internal void VerifyTitleSourcesForProbe()
    {
        foreach (var control in CanvasTitleControls.Resolve(design!, layout!.Width, layout.Height))
        {
            var b = control.BoundsDip;
            var sources = Sources(new(b.X + b.Width / 2, b.Y + b.Height / 2));
            bool hasToggle = sources.Contains(CanvasBehaviorPrefabs.ToggleSource(design!));
            if (hasToggle != (control.Role == "toggle") || sources.Any(s => s != "poster" && s != control.Id))
                throw new InvalidOperationException("Title pointer source did not isolate v from name/more/decorations.");
        }
    }
}
