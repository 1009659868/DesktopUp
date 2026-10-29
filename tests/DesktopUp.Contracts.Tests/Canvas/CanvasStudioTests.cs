using System.Security.Cryptography;
using System.Text;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasStudioTests
{
    private static CanvasNode Text(string id, double x) => new(id, "root", "draw.text@1", new(x, .15, .25, .2),
        new Dictionary<string, CanvasValue> { ["text"] = new(Text: id), ["color"] = new(Text: "#FFFFFF"), ["fontSize"] = new(Number: 18) });
    private static CanvasAuthoring Scene()
    { var doc = new CanvasAuthoring(CanvasAuthoring.Blank()); doc.Set(doc.WithNodes(doc.Design.Recipe.Nodes.Concat([Text("one", .1), Text("two", .5)]))); return doc; }
    [Fact]
    public void Semantic_slot_does_not_require_members_id_and_v1_remains_readable()
    {
        var blank = CanvasAuthoring.Blank(); Assert.NotEqual("members", blank.Recipe.Nodes.Single(n => n.Type == "content.slot@1").Id);
        CanvasDeclaration.Validate(blank); Assert.True(CanvasLayout.Resolve(blank, 540, 720).Content.Width > 400);
        foreach (var old in CanvasSamples.All) CanvasDeclaration.Validate(CanvasDeclaration.Read<CanvasDesign>(CanvasDeclaration.Serialize(old)));
    }
    [Fact]
    public void Group_preserves_geometry_and_one_undo_restores_the_complete_tree()
    {
        var doc = Scene(); var before = doc.Design; var first = CanvasLayout.Resolve(before, 540, 720);
        string group = doc.Group(["one", "two"]); var after = CanvasLayout.Resolve(doc.Design, 540, 720);
        foreach (string id in new[] { "one", "two" })
        { var a = first.Nodes.Single(n => n.Node.Id == id).Bounds; var b = after.Nodes.Single(n => n.Node.Id == id).Bounds; Assert.Equal(a.X, b.X, 7); Assert.Equal(a.Width, b.Width, 7); }
        Assert.Equal(group, doc.Design.Recipe.Nodes.Single(n => n.Id == "one").Parent);
        doc.Undo(); Assert.Equal(CanvasDeclaration.Digest(before), CanvasDeclaration.Digest(doc.Design)); doc.Redo(); Assert.Contains(doc.Design.Recipe.Nodes, n => n.Id == group);
    }
    [Fact]
    public void Combination_instances_have_independent_ids_and_do_not_copy_members()
    {
        var source = Scene(); string group = source.Group(["one", "two"]); var saved = source.Export(group);
        var target = new CanvasAuthoring(CanvasAuthoring.Blank()); string a = target.Insert(saved, "root"), b = target.Insert(saved, "root", .2, .4);
        Assert.NotEqual(a, b); Assert.Single(target.Design.Recipe.Nodes.Where(n => n.Type == "content.slot@1"));
        var child = target.Subtree([a]).First(n => n.Type == "draw.text@1"); var props = child.Properties.ToDictionary(x => x.Key, x => x.Value); props["text"] = new(Text: "Changed");
        target.Replace(child with { Properties = props });
        Assert.DoesNotContain(saved, n => n.Properties.GetValueOrDefault("text")?.Text == "Changed");
        Assert.DoesNotContain(target.Subtree([b]), n => n.Properties.GetValueOrDefault("text")?.Text == "Changed");
    }
    [Fact]
    public void Invalid_edits_preserve_history_and_protect_the_only_content_slot()
    {
        var doc = Scene(); string digest = CanvasDeclaration.Digest(doc.Design);
        Assert.Throws<ArgumentException>(() => doc.Delete([doc.Design.Recipe.Nodes.Single(n => n.Type == "content.slot@1").Id]));
        Assert.Throws<ArgumentException>(() => doc.Replace(doc.Design.Recipe.Nodes[1] with { Box = new(double.NaN, 0, 1, 1) }));
        Assert.Throws<ArgumentException>(() => doc.Insert(doc.Design.Recipe.Nodes, "root"));
        Assert.Equal(digest, CanvasDeclaration.Digest(doc.Design));
    }
    [Fact]
    public void Resource_paths_scripts_and_rotated_content_are_rejected()
    {
        var doc = Scene();
        var image = new CanvasNode("image", "root", "draw.image@1", new(), new Dictionary<string, CanvasValue> { ["asset"] = new(Text: "C:/private.png"), ["opacity"] = new(Number: 1) });
        Assert.Throws<ArgumentException>(() => doc.Set(doc.WithNodes(doc.Design.Recipe.Nodes.Append(image))));
        Assert.Throws<ArgumentException>(() => doc.Replace(doc.Design.Recipe.Nodes[1] with { Rotation = 30 }));
        Assert.Throws<ArgumentException>(() => CanvasDeclaration.Read<CanvasDesign>(CanvasDeclaration.Serialize(doc.Design).Replace("\"protocolVersion\":2", "\"protocolVersion\":2,\"script\":\"run()\"")));
    }
    [Fact]
    public void Samples_are_data_resources_and_images_and_combinations_survive_reopen()
    {
        using var fixture = new WorkspaceFixture(); var ws = fixture.Store;
        var works = ws.Works(); Assert.Equal(2, works.Count); var rain = works.Single(w => w.Design.Recipe.Nodes.Any(n => n.Type == "draw.image@1"));
        ws.Verify(rain.Design); var doc = new CanvasAuthoring(rain.Design); var nodes = doc.Export("note");
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanvasDeclaration.Serialize(nodes)))).ToLowerInvariant();
        ws.SaveCombination(new(id, "我的便签", nodes)); ws.SaveWork("saved", "我的作品", rain.Design);
        var reopened = new CanvasWorkspace(new UserDataPaths(fixture.Path));
        Assert.Contains(reopened.Combinations(), c => c.Id == id); Assert.Contains(reopened.Works(), w => w.Id == "saved"); reopened.Verify(rain.Design);
        Assert.Single(reopened.Assets()); Assert.False(File.Exists(System.IO.Path.Combine(fixture.Path, "state", "desktopup.db")));
    }
    [Fact]
    public void Import_is_deduplicated_and_hash_tampering_missing_and_oversized_resources_fail_closed()
    {
        using var fixture = new WorkspaceFixture(); var ws = fixture.Store; ws.Works();
        var asset = ws.Assets().Single(); byte[] bytes = ws.ReadAsset(asset.Id);
        Assert.Equal(asset.Id, ws.Import("second-name.jpg", bytes).Id); Assert.Single(ws.Assets());
        Assert.Throws<ArgumentException>(() => ws.Import("bad.svg", Encoding.UTF8.GetBytes("<svg><script>anything</script></svg>")));
        Assert.Throws<ArgumentException>(() => ws.Import("huge.jpg", new byte[CanvasWorkspace.MaxImageBytes + 1]));
        Assert.Throws<ArgumentException>(() => ws.ReadAsset("../escape"));
        Assert.Throws<FileNotFoundException>(() => ws.ReadAsset(new string('0', 64)));
        // Only a GUID-owned fixture resource is modified; the original image is not touched.
        File.WriteAllBytes(System.IO.Path.Combine(fixture.Path, "canvas", asset.Id + ".image"), new byte[25]);
        Assert.Throws<IOException>(() => ws.ReadAsset(asset.Id));
    }
    [Fact]
    public void Pixel_budget_is_checked_before_decode()
    {
        byte[] png = new byte[24]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0); "IHDR"u8.CopyTo(png.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), 8000); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), 8000);
        Assert.Throws<ArgumentException>(() => CanvasWorkspace.ImageSize(png));
    }
    [Fact]
    public void Content_ink_is_design_only_and_does_not_rewrite_the_original_theme()
    {
        var design = CanvasAuthoring.Blank() with { Recipe = CanvasAuthoring.Blank().Recipe with { ContentForeground = "#34393D" } };
        CanvasDeclaration.Validate(design);
        var original = PosterPresentationPolicy.Defaults();
        var view = new ResolvedPosterPresentation("basic-box", original with { Canvas = new(1, design, null, true, "test") }, PosterContentMetrics.Current, true, true, null);
        Assert.Equal("#34393D", view.EffectiveContentForeground);
        Assert.Equal(original.Theme.Foreground, view.Parameters.Theme.Foreground);
        Assert.Equal(original.Theme.Foreground, (view with { Parameters = original }).EffectiveContentForeground);
    }
    [Fact]
    public void Combination_digest_tampering_is_rejected_on_reopen()
    {
        using var fixture = new WorkspaceFixture(); var ws = fixture.Store; var doc = Scene(); string group = doc.Group(["one", "two"]); var nodes = doc.Export(group);
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanvasDeclaration.Serialize(nodes)))).ToLowerInvariant(); ws.SaveCombination(new(id, "test", nodes));
        string path = System.IO.Path.Combine(fixture.Path, "canvas", id + ".combo.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"fontSize\":{\"number\":18}", "\"fontSize\":{\"number\":20}"));
        Assert.Throws<ArgumentException>(() => ws.Combinations());
    }
    [Fact]
    public void Asset_semantics_are_bounded_revision_checked_and_do_not_mutate_pixels()
    {
        using var fixture = new WorkspaceFixture(); var ws = fixture.Store; ws.Works();
        var asset = ws.Assets().Single(); var bytes = ws.ReadAsset(asset.Id);
        Assert.Null(asset.Description);
        var next = ws.DescribeAsset(asset.Id, "雨夜窗边，蓝色城市，底部木桌；用户描述", 0);
        Assert.Equal(1, next.DescriptionRevision);
        Assert.Equal(next, fixture.Store.Assets().Single());
        Assert.Throws<ArgumentException>(() => ws.DescribeAsset(asset.Id, "旧版本", 0));
        Assert.Throws<ArgumentException>(() => ws.DescribeAsset(asset.Id, new string('x', 513), 1));
        Assert.Throws<ArgumentException>(() => ws.DescribeAsset("../outside", "x", 0));
        Assert.Equal(bytes, ws.ReadAsset(asset.Id)); Assert.Equal(next, ws.Assets().Single());
    }
    private sealed class WorkspaceFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopUp.Canvas.Studio.Tests", Guid.NewGuid().ToString("N"));
        public CanvasWorkspace Store => new(new UserDataPaths(Path));
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
