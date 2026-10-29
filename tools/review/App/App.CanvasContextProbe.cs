using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

public partial class App
{
    private async Task VerifyCanvasContextProbe(string directory, string id, PosterVisualWindow window,
        CancellationToken token, Func<string, Task> observe)
    {
        var state = _stateCoordinator!; var commands = _stateCommands!; var spatial = _spatialRuntime!;
        var workspace = _composition!.CanvasWorkspace; workspace.Works();
        var asset = workspace.Assets().Single(); workspace.DescribeAsset(asset.Id, "雨夜城市窗景，暖色木桌；隔离样例人工说明。", asset.DescriptionRevision);
        CanvasDesignSession Open() => new(state, commands, spatial, id, workspace, spatial.ReadCanvasObservation);
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        CanvasDesign saved;
        using (var session = Open())
        {
            var tools = new CanvasDraftTools(session); var first = tools.Read();
            var image = new CanvasNode("context-image", first.Design.Recipe.Nodes[0].Id, "draw.image@1", new(.5, .3, .25, .2),
                new Dictionary<string, CanvasValue> { ["asset"] = new(Text: asset.Id), ["opacity"] = new(Number: .35) }, Name: "行为样例");
            var behavior = new CanvasBehavior("custom", "#112233", .8, [
                new("expanded", "poster.expanded", "poster", image.Id, "rotation", 10, 0),
                new("enter", "pointer.enter", "poster", image.Id, "opacity", .8, 100),
                new("leave", "pointer.leave", "poster", image.Id, "opacity", .35, 100),
                new("click", "pointer.click", "poster", image.Id, "offsetX", 24, 0)]);
            var request = new CanvasDraftBatch("native-batch", tools.Scope, first.CapabilityRevision, first.TargetVersion,
                first.DraftRevision, first.DesignDigest, [new("add", image), new("behavior", Behavior: behavior)]);
            var receipt = tools.Execute(request); Require(tools.Execute(request) == receipt, "Native duplicate was not replayed.");
            tools.Preview(receipt.DraftRevision, receipt.Digest); await observe("context-preview");
            var packet = tools.Read();
            Require(packet.Content is { Status: "current", Observation.Items.Count: 2 }, "Anonymous published frame not current: " + packet.Content.Status);
            Require(packet.AssetDescriptions.Single().Description.Contains("雨夜"), "Asset semantics missing.");
            File.WriteAllText(Path.Combine(directory, "scene-packet.json"), CanvasSceneDescription.Serialize(packet));
            File.WriteAllText(Path.Combine(directory, "scene-guide.json"), CanvasDeclaration.Serialize(CanvasSceneDescription.Describe()));
            window.VerifyCanvasBehaviorForProbe(image.Id);
            saved = session.Design!;
            tools.CancelPreview(receipt.DraftRevision, receipt.Digest); await observe("context-cancelled");
            Require(tools.Read().Content.Status != "current", "Unpublished draft claimed current member geometry.");
            tools.Preview(session.DraftRevision, CanvasDeclaration.Digest(saved));
            Require(session.Apply(session.PreviewId!).Succeeded, "Host-owned apply failed.");
        }
        await observe("context-applied");
        var warm = window.CanvasWarmReadbackForProbe();
        spatial.ToggleCollapsed(window); while (spatial.IsSpatialBusy) await Task.Delay(40, token);
        window.VerifyCollapsedCanvasForProbe();
        await window.CapturePosterXamlForProbeAsync(Path.Combine(directory, "context-collapsed.png"));
        spatial.ToggleCollapsed(window); while (spatial.IsSpatialBusy) await Task.Delay(40, token);
        await observe("context-expanded"); warm();
        // Exercise authored sequential host calls, including same-payload state republication.
        using (var scripts = Open())
        {
            var behavior = CanvasBehaviorPrefabs.ForEditing(saved);
            saved = saved with { Recipe = saved.Recipe with { Behavior = behavior with {
                Prefabs = behavior.Prefabs!.Append(new("native-sequence", "收起等待再展开", "poster.collapse(); wait(120); poster.expand(); set(\"offsetX\", 32);")).ToArray(),
                Bindings = behavior.Bindings!.Append(new("native-click", "context-image", "pointer.click", "native-sequence", "context-image")).ToArray() } } };
            scripts.Revise(saved); scripts.Preview(); Require(scripts.Apply(scripts.PreviewId!).Succeeded, "Script apply failed.");
        }
        await observe("prefabs-applied");
        await window.VerifyCanvasPrefabSequenceForProbe(directory, token);
        await observe("prefabs-after-sequence");
        using (var undo = Open()) { undo.Preview(undo: true); Require(undo.Apply(undo.PreviewId!).Succeeded, "Behavior undo failed."); }
        await observe("context-undone");
        Require(state.Refresh().PostersById[id].Presentation.Parameters.Canvas?.Current?.Recipe.Behavior?.Prefabs is null, "Undo retained new script snapshot.");
        using (var restore = Open()) { restore.Revise(saved); restore.Preview(); Require(restore.Apply(restore.PreviewId!).Succeeded, "Behavior reapply failed."); }
        using (var reopened = Open()) Require(CanvasDeclaration.Digest(reopened.ReadCurrentDesign()) == CanvasDeclaration.Digest(saved), "Saved rules did not round-trip.");
        _controlCenter!.Show(); await Task.Delay(150, token);
        await _controlCenter.SettingsWindowForContractProbe!.VerifyContextEditorForProbe(directory, token);
    }
}
