using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasEditingAlignmentTests
{
    private static CanvasAuthoring Scene()
    {
        var doc = new CanvasAuthoring(CanvasAuthoring.Blank());
        doc.Set(doc.WithNodes(doc.Design.Recipe.Nodes.Concat(new[] { Shape("one", .1), Shape("two", .5) })));
        return doc;
    }
    private static CanvasNode Shape(string id, double x) => new(id, "root", "draw.panel@1", new(x, .2, .25, .3),
        new Dictionary<string, CanvasValue> { ["fill"] = new(Text: "#F1E3B9"), ["opacity"] = new(Number: .8), ["radius"] = new(Number: 3) }, Rotation: 25);
    [Fact]
    public void Layer_flags_persist_undo_and_inherit_without_hiding_real_content()
    {
        var doc = Scene(); string group = doc.Group(["one", "two"]);
        var node = doc.Design.Recipe.Nodes.Single(n => n.Id == group);
        doc.Replace(node with { Hidden = true, Locked = true });
        var state = CanvasLayerState.Resolve(CanvasDeclaration.Read<CanvasDesign>(CanvasDeclaration.Serialize(doc.Design)));
        Assert.Contains("one", state.Hidden); Assert.Contains("two", state.Locked);
        Assert.Throws<ArgumentException>(() => doc.Delete([group]));
        Assert.Throws<ArgumentException>(() => doc.Ungroup(group));
        doc.Undo(); Assert.Empty(CanvasLayerState.Resolve(doc.Design).Hidden);
        Assert.Throws<ArgumentException>(() => doc.Replace(doc.Design.Recipe.Nodes.Single(n => n.Type == "content.slot@1") with { Hidden = true }));
        Assert.DoesNotContain("\"hidden\":false", CanvasDeclaration.Serialize(doc.Design));
        Assert.DoesNotContain("\"locked\":false", CanvasDeclaration.Serialize(doc.Design));
    }
    [Fact]
    public void Ungroup_preserves_rotated_children_geometry_and_one_undo_restores_group()
    {
        var doc = Scene(); var initial = CanvasLayout.Resolve(doc.Design, 640, 800);
        string group = doc.Group(["one", "two"]); var before = doc.Design;
        Assert.Equal(2, doc.Ungroup(group).Count);
        var after = CanvasLayout.Resolve(doc.Design, 640, 800);
        foreach (string id in new[] { "one", "two" })
        {
            var a = initial.Nodes.Single(n => n.Node.Id == id); var b = after.Nodes.Single(n => n.Node.Id == id);
            Assert.Equal(a.Bounds.X, b.Bounds.X, 6); Assert.Equal(a.Bounds.Y, b.Bounds.Y, 6);
            Assert.Equal(a.Bounds.Width, b.Bounds.Width, 6); Assert.Equal(a.Node.Rotation, b.Node.Rotation);
        }
        doc.Undo(); Assert.Equal(CanvasDeclaration.Digest(before), CanvasDeclaration.Digest(doc.Design));
    }
    [Fact]
    public void Rotated_hit_and_resize_use_the_same_local_axes_and_keep_opposite_edge()
    {
        var rect = new CanvasRect(100, 100, 200, 80);
        Assert.True(CanvasEditingGeometry.Hit(rect, 90, 200, 225));
        Assert.False(CanvasEditingGeometry.Hit(rect, 90, 105, 140));
        var parent = new CanvasRect(0, 0, 500, 500); var original = new CanvasBox(.2, .2, .4, .16);
        var resized = CanvasEditingGeometry.Resize(original, parent, 90, 1, 0, 0, 40, false);
        Assert.Equal(.48, resized.Width, 6); Assert.Equal(.16, resized.Height, 6);
        Assert.Equal(.16, resized.X, 6); Assert.Equal(.24, resized.Y, 6);
        // The left edge in the object's rotated coordinates stays at the same world point.
        Assert.Equal(200, (resized.X + resized.Width / 2) * 500, 6);
        Assert.Equal(40, (resized.Y + resized.Height / 2 - resized.Width / 2) * 500, 6);
    }
    [Fact]
    public void Authored_aspect_is_preserved_in_the_host_and_group_rotation_reaches_children()
    {
        var doc = Scene(); doc.Set(doc.Design with { Recipe = doc.Design.Recipe with { Artboard = new(860, 620) } });
        string group = doc.Group(["one", "two"]);
        doc.Replace(doc.Design.Recipe.Nodes.Single(n => n.Id == group) with { Rotation = -6 });
        var editor = CanvasLayout.ForAuthoring(doc.Design, 860, 620);
        var host = CanvasLayout.Resolve(doc.Design, 540, 720);
        Assert.Equal(540.0 / 860, host.Scale, 8);
        Assert.Equal(19, host.Nodes.Single(p => p.Node.Id == "one").Rotation, 8);
        Assert.Equal(editor.Content.Width * host.Scale, host.Content.Width, 6);
        Assert.Equal(editor.Content.Height * host.Scale, host.Content.Height, 6);
        var old = doc.Design; doc.Ungroup(group); doc.Undo();
        Assert.Equal(CanvasDeclaration.Digest(old), CanvasDeclaration.Digest(doc.Design));
        var slot = doc.Design.Recipe.Nodes.Single(n => n.Type == "content.slot@1");
        Assert.Throws<ArgumentException>(() => doc.Replace(slot with { Parent = group }));
    }
    [Fact]
    public void Combination_size_is_part_of_the_identity_and_legacy_identity_is_unchanged()
    {
        var doc = Scene(); string group = doc.Group(["one", "two"]); var nodes = doc.Export(group);
        string legacy = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(CanvasDeclaration.Serialize(nodes)))).ToLowerInvariant();
        Assert.Equal(legacy, CanvasCombination.Digest(nodes));
        Assert.NotEqual(CanvasCombination.Digest(nodes, new(224, 190)), CanvasCombination.Digest(nodes, new(225, 190)));
    }
}
