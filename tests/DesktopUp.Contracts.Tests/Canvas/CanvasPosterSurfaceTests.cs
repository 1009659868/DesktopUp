using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasPosterSurfaceTests
{
    [Fact]
    public void Outer_resize_preserves_composition_aspect_and_has_no_legacy_width_cap()
    {
        var art = new CanvasArtboard(860, 620); var initial = new DesktopPixelRect(100, 100, 860, 620);
        var work = new DesktopPixelRect(0, 0, 1920, 1080);
        var grown = CanvasFrameResize.Resolve(art, PosterGestureKind.ResizeRight, initial, 100, 0, work, 300, 300, 1, 1);
        Assert.Equal(960, grown.Width); Assert.InRange(Math.Abs(grown.Width / (double)grown.Height - 860.0 / 620), 0, .002);
        var smaller = CanvasFrameResize.Resolve(art, PosterGestureKind.ResizeRight, grown, -100, 0, work, 300, 300, 1, 1);
        Assert.Equal(initial, smaller);
        var minimum = CanvasFrameResize.Enclose(art, 900, 600, 1, 1);
        Assert.True(minimum.Width >= 900 && minimum.Height >= 600);
    }
    [Fact]
    public void Whole_poster_has_one_origin_and_identical_authoring_and_runtime_geometry()
    {
        var legacy = CanvasAuthoring.Blank(); string raw = CanvasDeclaration.Serialize(legacy);
        var scene = CanvasSurfaceGeometry.ForEditing(legacy, 860, 620);
        var editor = CanvasLayout.ForAuthoring(scene, 860, 620); var runtime = CanvasLayout.Resolve(scene, 860, 620);
        Assert.Equal(new CanvasRect(0, 0, 860, 620), runtime.Nodes[0].Bounds);
        Assert.Equal(editor.Content, runtime.Content);
        Assert.Equal(editor.Nodes.Select(p => p.Bounds), runtime.Nodes.Select(p => p.Bounds));
        Assert.Equal(raw, CanvasDeclaration.Serialize(legacy));
        Assert.Single(scene.Recipe.Nodes.Where(n => n.Type == "host.title@1"));
        Assert.Equal(1, runtime.Scale);
        CanvasSurfaceGeometry.ValidateHosting(scene, runtime);
    }

    [Theory]
    [InlineData("rectangle", true)] [InlineData("rounded", false)] [InlineData("ellipse", false)]
    public void Visual_shape_and_input_geometry_use_the_same_surface(string kind, bool corner)
    {
        var shape = new CanvasSurface(kind, 40);
        var layout = new PosterInputLayoutSnapshot(1, 720, 540, []) { Surface = shape };
        Assert.Equal(corner, PosterInputGeometry.InsideRoundedFrame(1, 1, layout));
        Assert.True(PosterInputGeometry.InsideRoundedFrame(360, 270, layout));
        Assert.False(PosterInputGeometry.InsideRoundedFrame(-1, 270, layout));
        Assert.False(PosterInputGeometry.InsideRoundedFrame(720, 270, layout));
    }

    [Fact]
    public void Ellipse_requires_real_content_and_title_to_be_inside_and_not_overlap()
    {
        var doc = new CanvasAuthoring(CanvasAuthoring.NewArtwork());
        doc.Set(doc.Design with { Recipe = doc.Design.Recipe with { Surface = new("ellipse") } });
        Assert.Throws<ArgumentException>(() => CanvasSurfaceGeometry.ValidateHosting(doc.Design, CanvasLayout.Resolve(doc.Design, 720, 540)));
        doc.Set(doc.WithNodes(doc.Design.Recipe.Nodes.Select(n => n.Type switch {
            "host.title@1" => n with { Box = new(.25, .12, .5, .1) },
            "content.slot@1" => n with { Box = new(.2, .3, .6, .48) }, _ => n })));
        CanvasSurfaceGeometry.ValidateHosting(doc.Design, CanvasLayout.Resolve(doc.Design, 720, 540));
        var title = doc.Design.Recipe.Nodes.Single(n => n.Type == "host.title@1");
        Assert.Throws<ArgumentException>(() => doc.Delete([title.Id]));
        Assert.Throws<ArgumentException>(() => doc.Export(title.Id));
        Assert.Throws<ArgumentException>(() => doc.Replace(title with { Hidden = true }));
        doc.Replace(title with { Box = new(.25, .35, .5, .1) });
        Assert.Throws<ArgumentException>(() => CanvasSurfaceGeometry.ValidateHosting(doc.Design, CanvasLayout.Resolve(doc.Design, 720, 540)));
    }

    [Fact]
    public void Title_actions_follow_the_editable_box_not_the_legacy_top_bar()
    {
        var doc = new CanvasAuthoring(CanvasAuthoring.NewArtwork());
        var title = doc.Design.Recipe.Nodes.Single(n => n.Type == "host.title@1");
        doc.Replace(title with { Box = new(.25, .12, .5, .1) });
        var resolved = CanvasSurfaceGeometry.Title(doc.Design, 720, 540, false, PosterTitleChromeKind.Cloud);
        Assert.Equal(64.8, resolved.GripBands[0].Y, 6);
        Assert.Equal(PosterTitleChromeTarget.More, resolved.HitTest(resolved.More.X + 12, resolved.More.Y + 12));
        Assert.Equal(PosterTitleChromeTarget.Desktop, resolved.HitTest(10, 10));
        var collapsed = CanvasSurfaceGeometry.Title(doc.Design, 720, 46, true, PosterTitleChromeKind.Basic);
        Assert.Equal(46, collapsed.Height);
    }
}
