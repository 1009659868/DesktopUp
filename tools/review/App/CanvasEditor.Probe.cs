using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

internal sealed partial class CanvasEditor
{
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private void Invoke(string label)
    {
        var button = Visuals<Button>(shell).First(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(b) == label || b.Content?.ToString() == label);
        if (!button.IsEnabled || FrameworkElementAutomationPeer.CreatePeerForElement(button)?.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            throw new InvalidOperationException("Canvas production button unavailable: " + label);
        invoke.Invoke();
    }
    internal async Task VerifyForProbe(string image, CancellationToken token, Func<string, Task> observe)
    {
        while (!shell.IsLoaded) await Task.Delay(40, token);
        Switch("builtin-basic");
        if (apply.IsEnabled) throw new InvalidOperationException("Apply enabled without preview.");
        Invoke("预览作品"); await Task.Delay(180, token);
        if (!apply.IsEnabled) throw new InvalidOperationException(notice.Text);
        Invoke("应用并保存"); await Task.Delay(180, token);
        if (apply.IsEnabled || session!.Design?.Recipe.Id != "basic-storage") throw new InvalidOperationException(notice.Text);
        await CaptureForProbe(Path.Combine(Path.GetDirectoryName(image)!, "editor-basic.png"));
        await observe("studio-basic");
        Invoke("撤销已应用设计"); await Task.Delay(180, token);
        if (session.Design?.Recipe.Id != "rainy-desk") throw new InvalidOperationException("Undo/editor recipe mismatch.");
        Switch("builtin-rainy");
        if (document.Design.Recipe.ProtocolVersion != 2) throw new InvalidOperationException("Studio did not load data-backed v2 sample.");
        string importPath = Path.Combine(Path.GetDirectoryName(image)!, "owned-import.jpg");
        File.WriteAllBytes(importPath, workspace.ReadAsset(workspace.Assets().Single().Id));
        try { selected.Clear(); await ImportFile(await StorageFile.GetFileFromPathAsync(importPath), .1, .1, current); }
        finally { File.Delete(importPath); }
        document.Undo(); Render(); workspace.Verify(document.Design);
        var stable = views.ToDictionary(p => p.Key, p => p.Value);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew(); long allocation = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 240; i++)
        {
            pending = document.WithNodes(document.Design.Recipe.Nodes.Select(n => n.Id == "heading" ? n with { Box = n.Box with { X = .07 + i % 2 * .001 } } : n));
            RenderGesture();
        }
        var measured = new { iterations = 240, elapsedMs = stopwatch.Elapsed.TotalMilliseconds, allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation, nativePointer = false };
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(image)!, "studio-cost.json"), System.Text.Json.JsonSerializer.Serialize(measured));
        pending = null; Render();
        if (stable.Any(p => !ReferenceEquals(views[p.Key], p.Value))) throw new InvalidOperationException("Gesture replaced a decorative visual.");
        Select("note"); Render(); SaveCombination(selected, "桌边便签");
        if (workspace.Combinations().Count == 0) throw new InvalidOperationException(notice.Text);
        var saved = workspace.Combinations().First();
        NewWork(); InsertCombination(saved, .5, .5); string inserted = selected.Single(); Render();
        if (saved.Size is { } size && Math.Abs(geometry!.Nodes.Single(n => n.Node.Id == inserted).Bounds.Width - size.Width) > .01)
            throw new InvalidOperationException("Cross-work combination changed its authored size.");
        var textNode = document.Subtree([inserted]).First(n => n.Type == "draw.text@1");
        SetProperty(textNode.Id, "text", new(Text: "独立实例"));
        if (saved.Nodes.Any(n => n.Properties.GetValueOrDefault("text")?.Text == "独立实例")) throw new InvalidOperationException("Instance changed its source.");
        SaveWork();
        Switch("builtin-rainy"); Invoke("预览作品"); await Task.Delay(180, token);
        if (!apply.IsEnabled) throw new InvalidOperationException(notice.Text);
        Invoke("应用并保存"); await Task.Delay(180, token);
        if (session.Design?.Recipe.ProtocolVersion != 2) throw new InvalidOperationException(notice.Text);
        context = "note"; Select("note-paper"); Render(); Fit(); await Task.Delay(180, token);
        await CaptureForProbe(image);
        window!.AppWindow.Resize(new(800, 980)); await Task.Delay(240, token);
        Fit(); await Task.Delay(180, token);
        await CaptureForProbe(Path.Combine(Path.GetDirectoryName(image)!, "editor-narrow.png"));
        NewWork();
        selected.Clear(); InsertAsset(workspace.Assets().Single().Id, .35, .52);
        var photo = Current(selected.Single());
        document.Replace(photo with { Rotation = -22, Name = "雨窗与桌面" });
        var paper = NewNode("draw.panel@1", new(337.1 / 720, 209.1 / 540, 220.0 / 720, 160.0 / 540)) with { Rotation = 38.1, Name = "暖色纸张" };
        document.Set(document.WithNodes(document.Design.Recipe.Nodes.Append(paper)));
        Select(paper.Id); Render(); Fit(); await Task.Delay(180, token);
        if (Hit(new(447.1, 289.1))?.Id != paper.Id) throw new InvalidOperationException("Rotated hit mismatch.");
        // These are the same authoring commands as the layer buttons; not simulated pointer input.
        document.Replace(paper with { Hidden = true }); Render();
        if (views.ContainsKey(paper.Id)) throw new InvalidOperationException("Hidden layer still rendered.");
        document.Undo(); Render();
        document.Replace(Current(paper.Id) with { Locked = true }); Render();
        if (Hit(new(447.1, 289.1))?.Id == paper.Id) throw new InvalidOperationException("Locked layer still hit.");
        document.Undo(); Select(paper.Id); Render();
        await CaptureForProbe(Path.Combine(Path.GetDirectoryName(image)!, "editor-blank-narrow.png"));
        window.AppWindow.Resize(new(1280, 900)); await Task.Delay(240, token);
        Fit(); await Task.Delay(180, token);
        await CaptureForProbe(Path.Combine(Path.GetDirectoryName(image)!, "editor-blank-wide.png"));
        selected.Clear(); selected.UnionWith(new[] { photo.Id, paper.Id }); GroupSelection();
        string group = selected.Single(); document.Ungroup(group); document.Undo();
        if (!document.Design.Recipe.Nodes.Any(n => n.Id == group)) throw new InvalidOperationException("Ungroup undo lost the subtree.");
        // Draft-only changes must not alter the already committed rainy host.
        if (session.Design?.Recipe.Id == "rainy-desk") throw new InvalidOperationException("Draft did not remain independent.");
        SaveWork();
        Dispose();
    }
    private async Task CaptureForProbe(string image)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(shell);
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(image)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(image), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
public sealed partial class ControlCenterWindow
{
    internal async Task VerifyCanvasEditorForProbe(string image, CancellationToken token, Func<string, Task> observe)
    {
        InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Single(b => b.Content?.ToString() == "画板设计 ↗"));
        while (_canvasEditor is null) await Task.Delay(40, token);
        await _canvasEditor.VerifyForProbe(image, token, observe);
    }
}
