using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Domain;
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
    internal async Task VerifyContextForProbe(string directory, CancellationToken token)
    {
        while (geometry is null || shell.ActualWidth < 1) await Task.Delay(40, token);
        Select("context-image"); Render(); await Task.Delay(80, token);
        if (!Visuals<TextBox>(shell).Any(t => t.Text.Contains("雨夜城市窗景"))) throw new InvalidOperationException("Asset meaning editor missing.");
        await CaptureForProbe(Path.Combine(directory, "editor-context.png"));
        selected.Clear(); Render(); ShowBehaviorEditor(); await Task.Delay(250, token);
        if (!behaviorTree || !layers.Children.OfType<Button>().Any()) throw new InvalidOperationException("Prefab tree unavailable.");
        // Inspect initial assignment, not just code assigned after AcceptsReturn was enabled.
        foreach (var template in CanvasBehaviorPrefabs.Templates)
        {
            OpenScript(template); await Task.Delay(80, token);
            var initial = Visuals<TextBox>(scriptPanel!).Single(t => t.AcceptsReturn);
            if (CanvasScript.NormalizeLineEndings(initial.Text) != template.Script)
                throw new InvalidOperationException("Initial multiline template was truncated.");
        }
        CloseScriptEditor();
        Invoke("＋ 新建脚本行为"); await Task.Delay(100, token);
        var code = Visuals<TextBox>(scriptPanel!).Single(t => t.AcceptsReturn);
        string digest = CanvasDeclaration.Digest(document.Design);
        code.Text = "System.IO.File.Delete(\"x\");"; Invoke("校验并保存");
        if (CanvasDeclaration.Digest(document.Design) != digest || !Visuals<TextBlock>(scriptPanel!).Any(t => t.Text.Contains("行")))
            throw new InvalidOperationException("Invalid code was not isolated with a line diagnostic.");
        // A real TextBox edit/roundtrip uses CR line breaks; a leading comment must
        // not swallow the actual program, including when saved and reopened.
        code.Text = "// 用户输入的说明\rif (expanded) { animate(\"offsetX\", 24, 180, \"smooth\"); }\rwait(100);\rposter.collapse();\rwait(120);\rposter.expand();";
        string expectedScript = CanvasScript.NormalizeLineEndings(code.Text);
        File.WriteAllText(Path.Combine(directory, "script-newlines.json"), CanvasDeclaration.Serialize(new {
            carriageReturns = code.Text.Count(c => c == '\r'), lineFeeds = code.Text.Count(c => c == '\n'),
            canonicalInstructions = CanvasScript.Compile(code.Text).Instructions.Count }));
        Invoke("存入行为库"); await Task.Delay(120, token);
        var prefab = Behavior.Prefabs!.Single(p => p.Script == expectedScript);
        if (!workspace.Behaviors().Any(p => p == prefab) || !workspace.Works().Any(w => w.Id == current && w.Design.Recipe.Behavior!.Prefabs!.Any(p => p == prefab)))
            throw new InvalidOperationException("Authoring did not persist code and work.");
        CloseScriptEditor(); OpenScript(workspace.Behaviors().Single(p => p.Id == prefab.Id)); await Task.Delay(80, token);
        if (CanvasScript.NormalizeLineEndings(Visuals<TextBox>(scriptPanel!).Single(t => t.AcceptsReturn).Text) != expectedScript)
            throw new InvalidOperationException("Reopening the saved script lost a line.");
        await CaptureForProbe(Path.Combine(directory, "editor-script.png")); CloseScriptEditor();
        Edit(() => SetBehavior(Behavior with { Bindings = Behavior.Bindings!.Append(new(CanvasAuthoring.NewId(), "context-image", "pointer.click", prefab.Id, "context-image")).ToArray() }));
        Select("context-image"); Render(); await Task.Delay(100, token);
        await CaptureForProbe(Path.Combine(directory, "editor-bindings.png"));
        // Parent title is not the editable behavior source. The v child is selected
        // independently both through the layer tree and precise canvas hit geometry.
        behaviorTree = false; Select(document.Design.Recipe.Nodes.Single(n => n.Type == "host.title@1").Id); Render();
        if (Visuals<TextBlock>(inspector).Any(t => t.Text.Contains("按顺序执行"))) throw new InvalidOperationException("Whole title still shows toggle bindings.");
        Invoke("收展按钮 v");
        if (SelectedTitleControl is not { Role: "toggle" } || !Visuals<TextBlock>(inspector).Any(t => t.Text == "v 按钮行为 · 按顺序执行"))
            throw new InvalidOperationException("Independent v inspector unavailable.");
        var controls = TitleControls(Current(selected.Single()));
        foreach (var control in controls)
        {
            var b = control.BoundsDip;
            if (!SelectTitleControlAt(new(b.X + b.Width / 2, b.Y + b.Height / 2)) || SelectedTitleControl?.Id != control.Id)
                throw new InvalidOperationException("Title subcontrol hit mismatch.");
            if (!control.CanBind && Visuals<TextBlock>(inspector).Any(t => t.Text.Contains("按顺序执行")))
                throw new InvalidOperationException("Name or more incorrectly offered script binding.");
        }
        Invoke("收展按钮 v"); await Task.Delay(100, token);
        var explicitToggle = CanvasBehaviorPrefabs.ToggleExpanded;
        Edit(() => SetBehavior(Behavior with {
            Prefabs = Behavior.Prefabs!.Where(p => p.Id != explicitToggle.Id).Append(explicitToggle).ToArray(),
            Bindings = Behavior.Bindings!.Select(b => b.Source == CanvasBehaviorPrefabs.ToggleSource(document.Design)
                ? b with { Prefab = explicitToggle.Id } : b).ToArray() }));
        await CaptureForProbe(Path.Combine(directory, "editor-v-control.png"));
        var saved = CanvasDeclaration.Digest(document.Design);
        Invoke("运行画板"); await Task.Delay(120, token);
        long version = session!.DraftRevision;
        if (runScene is null) throw new InvalidOperationException("Canvas run unavailable: " + notice.Text);
        var title = CanvasSurfaceGeometry.Title(document.Design, board.Width, board.Height, false, PosterTitleChromeKind.Basic);
        RunClick(new(title.Collapse.X + 12, title.Collapse.Y + 12));
        for (int i = 0; !runCollapsed && i < 60; i++) await Task.Delay(20, token);
        if (!runCollapsed) throw new InvalidOperationException("Canvas v did not collapse: " + notice.Text);
        await CaptureForProbe(Path.Combine(directory, "editor-running-collapsed.png"));
        title = CanvasSurfaceGeometry.Title(document.Design, board.Width, board.Height, true, PosterTitleChromeKind.Basic);
        RunClick(new(title.Collapse.X + 12, title.Collapse.Y + 12));
        for (int i = 0; runCollapsed && i < 60; i++) await Task.Delay(20, token);
        if (runCollapsed) throw new InvalidOperationException("Canvas v did not expand: " + notice.Text);
        // Name and more do not execute the v binding.
        title = CanvasSurfaceGeometry.Title(document.Design, board.Width, board.Height, false, PosterTitleChromeKind.Basic);
        RunClick(new(title.Name.X + 6, title.Name.Y + 6)); RunClick(new(title.More.X + 8, title.More.Y + 8));
        await Task.Delay(100, token);
        if (runCollapsed || runPosture is not null) throw new InvalidOperationException("Title text or more triggered collapse.");
        var box = geometry!.Nodes.Single(n => n.Node.Id == "context-image").Bounds;
        RunClick(new(box.X + box.Width / 2, box.Y + box.Height / 2));
        bool sawCollapse = false;
        for (int i = 0; i < 140; i++)
        { await Task.Delay(20, token); sawCollapse |= runCollapsed; if (sawCollapse && !runCollapsed && runScene?.HasActiveScripts == false) break; }
        if (!sawCollapse || runCollapsed || runScene?.ScriptError is not null) throw new InvalidOperationException("Authored canvas sequence failed: " + notice.Text);
        await CaptureForProbe(Path.Combine(directory, "editor-running-expanded.png"));
        Invoke("运行画板");
        if (runScene is not null || CanvasDeclaration.Digest(document.Design) != saved || session.DraftRevision != version)
            throw new InvalidOperationException("Local run changed saved design or leaked runtime.");
        SaveCombination(["context-image"], "行为与图片组合");
        if (workspace.Combinations().Last().Behavior?.Bindings?.Count < 1) throw new InvalidOperationException("Combination omitted behavior snapshot.");
        SaveWork();
        if (!workspace.Works().Any(w => CanvasDeclaration.Digest(w.Design) == saved)) throw new InvalidOperationException("Script draft did not save.");
        Dispose();
    }
    internal async Task VerifyCurrentForProbe(string directory, CancellationToken token, Func<string, Task> observe)
    {
        while (geometry is null || shell.ActualWidth < 1) await Task.Delay(40, token);
        if (names[current] != "当前海报") throw new InvalidOperationException(notice.Text);
        var plateNode = document.Design.Recipe.Nodes.Single(n => n.Name == "海报背景");
        if (views[plateNode.Id] is not Border plate || plate.Background is not SolidColorBrush ink || ink.Color.R != 0 ||
            Math.Abs(plate.Opacity - Math.Round(43 * 255d / 100) / 255d) > .00001)
            throw new InvalidOperationException("Editor current poster omitted its native background.");
        File.WriteAllText(Path.Combine(directory, "scene-packet.json"), DesktopUp.Application.CanvasSceneDescription.Serialize(session!.DescribeScene()));
        File.WriteAllText(Path.Combine(directory, "scene-guide.json"), DesktopUp.Domain.CanvasDeclaration.Serialize(DesktopUp.Application.CanvasSceneDescription.Describe()));
        await CaptureForProbe(Path.Combine(directory, "editor-current.png"));
        Preview(); if (!apply.IsEnabled) throw new InvalidOperationException(notice.Text);
        await observe("current-preview");
        CancelPreview(); await observe("current-cancelled");
        Preview(); Commit(); await observe("current-applied");
        session!.Preview(true); Commit(); await observe("current-undone");
        Dispose();
    }
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
        // The real editor draft and actual HWNDs share title/content coordinates and the outer shape.
        document.Set(document.Design with { Recipe = document.Design.Recipe with { Surface = new("ellipse") } });
        document.Set(document.WithNodes(document.Design.Recipe.Nodes.Select(n => n.Type switch {
            "host.title@1" => n with { Box = new(.25, .12, .5, .1) },
            "content.slot@1" => n with { Parent = document.Design.Recipe.Nodes[0].Id, Box = new(.2, .3, .6, .48) }, _ => n })));
        Render(); Preview(); await observe("studio-ellipse-preview");
        await CaptureForProbe(Path.Combine(Path.GetDirectoryName(image)!, "editor-ellipse.png"));
        CancelPreview(); await observe("studio-ellipse-cancelled");
        document.Undo(); document.Undo(); Render();
        Invoke("撤销已应用设计"); await Task.Delay(180, token);
        if (session.Design?.Recipe.Id != "rainy-desk") throw new InvalidOperationException("Undo/editor recipe mismatch.");
        Switch("builtin-rainy");
        if (document.Design.Recipe.ProtocolVersion != 3) throw new InvalidOperationException("Studio did not upgrade the sample to a whole-poster surface.");
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
        if (session.Design?.Recipe.ProtocolVersion != 3) throw new InvalidOperationException(notice.Text);
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
    internal async Task VerifyContextEditorForProbe(string directory, CancellationToken token)
    {
        InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Single(b => b.Content?.ToString() == "画板设计 ↗"));
        for (int attempt = 0; _canvasEditor is null && attempt < 30; attempt++) await Task.Delay(40, token);
        if (_canvasEditor is null) throw new InvalidOperationException("Context editor unavailable.");
        await _canvasEditor.VerifyContextForProbe(directory, token);
    }
    internal async Task VerifyCurrentCanvasEditorForProbe(string directory, CancellationToken token, Func<string, Task> observe)
    {
        InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Single(b => b.Content?.ToString() == "画板设计 ↗"));
        for (int attempt = 0; _canvasEditor is null && attempt < 30; attempt++) await Task.Delay(40, token);
        if (_canvasEditor is null) throw new InvalidOperationException("Current editor unavailable: " + CommonSettingsNotice.Text);
        await _canvasEditor.VerifyCurrentForProbe(directory, token, observe);
    }
    internal async Task VerifyCanvasEditorForProbe(string image, CancellationToken token, Func<string, Task> observe)
    {
        InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Single(b => b.Content?.ToString() == "画板设计 ↗"));
        for (int attempt = 0; _canvasEditor is null && attempt < 30; attempt++) await Task.Delay(40, token);
        if (_canvasEditor is null) throw new InvalidOperationException($"Editor did not open. Ready={TemplateOperationReady()}; notice={CommonSettingsNotice.Text}; collapsed={_view?.SelectedPoster?.Poster.IsCollapsed}");
        await _canvasEditor.VerifyForProbe(image, token, observe);
    }
}
