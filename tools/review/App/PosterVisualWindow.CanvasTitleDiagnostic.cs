using System.Diagnostics;
using System.Runtime.CompilerServices;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

// Review-only observation. Never fixes layout or changes animation timing to obtain a PASS.
internal sealed partial class PosterVisualWindow
{
    internal async Task ObserveCanvasTitleTransitionForProbe(string directory, string name, CancellationToken token, bool captureStills = true)
    {
        var root = _window?.Content as FrameworkElement ?? throw new InvalidOperationException("Missing root.");
        var title = _titleName ?? throw new InvalidOperationException("Missing title.");
        var samples = new List<object>(); var captures = new List<Task>(); var captureErrors = new List<string>();
        var clock = Stopwatch.StartNew(); string phase = "expanded-before";
        int render = 0, loaded = 0, unloaded = 0; bool capturing = false;
        var nativeFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? nativeCapture = null;
        double? Y(FrameworkElement? element)
        {
            if (element?.XamlRoot is null) return null;
            try { return element.TransformToVisual(root).TransformPoint(new(0, 0)).Y; }
            catch { return null; }
        }
        object? Clip(FrameworkElement? element) => element?.Clip is RectangleGeometry c
            ? new { c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height } : null;
        void Sample(string kind)
        {
            if (samples.Count >= 512) return;
            var text = title.Child as TextBlock;
            samples.Add(new { phase, kind, ms = clock.Elapsed.TotalMilliseconds, qpc = Stopwatch.GetTimestamp(), render, loaded, unloaded,
                collapsed = _geometry.IsCollapsed, committedCollapsed = _authoritativePoster?.IsCollapsed,
                height = _bounds.Height, nativeHeight = CurrentNativeBounds(_bounds).Height,
                rootHeight = root.ActualHeight, structureHeight = _contentStructure?.ActualHeight,
                transition = _transitionLayout is not null, titleConnected = title.XamlRoot is not null,
                structureY = Y(_contentStructure), headerY = Y(_titleHeader), canvasY = Y(_titleCanvas),
                localY = Canvas.GetTop(title), worldY = Y(title), actualHeight = title.ActualHeight,
                headerHeight = _titleHeader?.ActualHeight, rowSpan = _titleHeader is null ? 0 : Grid.GetRowSpan(_titleHeader),
                titleIdentity = RuntimeHelpers.GetHashCode(title), text = text?.Text, fontSize = text?.FontSize,
                foreground = (text?.Foreground as SolidColorBrush)?.Color.ToString(), opacity = text?.Opacity,
                titleClip = Clip(_titleCanvas), structureClip = Clip(_contentStructure), rootClip = Clip(root) });
        }
        async Task Capture(string tag)
        {
            capturing = true; Sample("capture-start:" + tag);
            try { if (captureStills) await CapturePosterXamlForProbeAsync(Path.Combine(directory, name + "-" + tag + ".png")); }
            catch (Exception error) { captureErrors.Add(tag + ": " + error.Message); }
            finally { Sample("capture-end:" + tag); capturing = false; }
        }
        void Frame(object? sender, object args)
        {
            render++; Sample("render");
            if (captureStills && phase == "expanding" && !capturing && render is 1 or 5)
                captures.Add(Capture("expand-render-" + render));
        }
        void Loaded(object sender, RoutedEventArgs args) { loaded++; Sample("loaded"); }
        void Unloaded(object sender, RoutedEventArgs args) { unloaded++; Sample("unloaded"); }
        async Task WaitFor(bool collapsed)
        {
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(20, token);
                if (!spatial.IsSpatialBusy && _authoritativePoster?.IsCollapsed == collapsed) return;
            }
            throw new InvalidOperationException("Title diagnostic posture did not finish.");
        }
        title.Loaded += Loaded; title.Unloaded += Unloaded; CompositionTarget.Rendering += Frame;
        try
        {
            Sample("before"); await Capture("expanded-before");
            nativeCapture = BackdropCaptureProbe.CaptureSequenceAsync(_windowHandle, Path.Combine(directory, name + "-native"),
                nativeFinished.Task, token, startsAfterFirstCommit: false);
            await Task.Delay(80, token); // Diagnostic capture warm-up only; never holds the production animation.
            phase = "collapsing"; render = 0;
            if (!InvokeCanvasToggle()) throw new InvalidOperationException("Default binding missing.");
            Sample("after-request"); await WaitFor(true); await Task.Delay(80, token);
            phase = "collapsed"; Sample("settled"); await Capture("collapsed");
            phase = "expanding"; render = 0;
            if (!InvokeCanvasToggle()) throw new InvalidOperationException("Collapsed binding missing.");
            Sample("after-request"); await WaitFor(false); await Task.Delay(100, token);
            phase = "expanded-after"; Sample("settled"); await Task.WhenAll(captures); await Capture("expanded-after");
        }
        finally
        {
            CompositionTarget.Rendering -= Frame; title.Loaded -= Loaded; title.Unloaded -= Unloaded;
            nativeFinished.TrySetResult(true);
            if (nativeCapture is not null)
                try { await nativeCapture; } catch (Exception error) { captureErrors.Add("native: " + error.Message); }
            await Task.WhenAll(captures);
            File.WriteAllText(Path.Combine(directory, name + "-title-trace.json"), CanvasDeclaration.Serialize(new
            {
                diagnosisOnly = true, productFixVerified = false, stillsAreXaml = captureStills, nativeSequenceIsOwnWindowOnly = true,
                desktopPresentationCertified = false, captureMayAffectTiming = true, captureErrors, samples
            }));
        }
    }
}

public partial class App
{
    private async Task DiagnoseCanvasTitleForProbe(string directory, string id, PosterVisualWindow window, CancellationToken token)
    {
        var state = _stateCoordinator!; var commands = _stateCommands!; var spatial = _spatialRuntime!;
        CanvasDesignSession Open() => new(state, commands, spatial, id, _composition!.CanvasWorkspace);
        using (var current = Open())
        {
            var design = current.ReadCurrentDesign();
            current.Revise(design with { Recipe = design.Recipe with { Behavior = CanvasBehaviorPrefabs.ForEditing(design) } });
            current.Preview();
            if (!current.Apply(current.PreviewId!).Succeeded) throw new InvalidOperationException("Diagnostic setup failed.");
        }
        await Task.Delay(180, token);
        await window.ObserveCanvasTitleTransitionForProbe(directory, "current-default", token);
        await window.ObserveCanvasTitleTransitionForProbe(directory, "default-native-only", token, captureStills: false);
        using (var authored = Open())
        {
            var design = authored.ReadCurrentDesign();
            var nodes = design.Recipe.Nodes.Select(n => n.Type == "content.slot@1" ? n with { Box = n.Box with { Y = .14, Height = .8 } } : n.Type != "host.title@1" ? n : n with
            {
                Box = new(.07, .04, .86, .055),
                Properties = new Dictionary<string, CanvasValue>
                {
                    ["text"] = new(Text: "TITLE-SENTINEL"), ["fontSize"] = new(Number: 20),
                    ["color"] = new(Text: "#00FFCC"), ["actions"] = new(Text: "#00FFCC")
                }
            }).ToArray();
            authored.Revise(design with { Recipe = design.Recipe with { Nodes = nodes } });
            authored.Preview();
            if (!authored.Apply(authored.PreviewId!).Succeeded) throw new InvalidOperationException("Authored setup failed.");
        }
        await Task.Delay(180, token);
        await window.ObserveCanvasTitleTransitionForProbe(directory, "authored-title", token);
    }
}
