using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentFrameResizeTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 96u)]
    [InlineData(ContentFlowAxis.Vertical, 144u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 96u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 144u)]
    public void Populated_cross_axis_can_resize_below_legacy_240_without_clipping(ContentFlowAxis axis, uint dpi)
    {
        var doc = ContentLayoutSamples.Equal("A", 2) with { FlowAxis = axis };
        doc = doc with { Placements = doc.Placements.Select(p => p with
            { ColumnSpan = 2.125, RowSpan = 2.375 }).ToArray() };
        var poster = Seed() with { ContentLayoutJson = ContentLayoutPolicy.Serialize(doc) };
        Assert.True(ContentFrameMinimumPolicy.TryForPoster(poster, out var minimum, out var error), error);
        var pixels = minimum!.Pixels(dpi, dpi);
        double width = pixels.Width * 96d / dpi, height = pixels.Height * 96d / dpi;
        Assert.True((axis == ContentFlowAxis.Vertical ? width : height) < 240);
        var plan = ControlCenterSpatial.Plan(poster, Scene(poster, dpi), new(0, 0, width, height), out error);
        Assert.NotNull(plan);
        Assert.Null(error);
        Assert.Equal(poster.ContentLayoutJson, plan.Desired.ContentLayoutJson);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 96u)] [InlineData(ContentFlowAxis.Vertical, 120u)]
    [InlineData(ContentFlowAxis.Vertical, 144u)] [InlineData(ContentFlowAxis.HorizontalLoop, 96u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 120u)] [InlineData(ContentFlowAxis.HorizontalLoop, 144u)]
    public void Center_rejects_cross_axis_smaller_than_complete_item(ContentFlowAxis axis, uint dpi)
    {
        var poster = LargeItem(axis); var scene = Scene(poster, dpi);
        // Largest complete item = 12 * (24 + 6) - 6 = 354 DIP, before any chrome.
        var intent = new PosterGeometryIntent(0, 0, axis == ContentFlowAxis.Vertical ? 300 : 600,
            axis == ContentFlowAxis.Vertical ? 700 : 300);
        Assert.Null(ControlCenterSpatial.Plan(poster, scene, intent, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(PosterGestureKind.ResizeRight)] [InlineData(PosterGestureKind.ResizeLeft)]
    [InlineData(PosterGestureKind.ResizeBottom)] [InlineData(PosterGestureKind.ResizeTop)]
    public void Impossible_anchored_minimum_retains_frame_instead_of_throwing_or_shrinking_item(PosterGestureKind gesture)
    {
        var initial = new DesktopPixelRect(100, 100, 300, 300);
        var work = new DesktopPixelRect(0, 0, 500, 500);
        Assert.Equal(initial, PosterFrameResizePolicy.Resolve(gesture, initial, -100, -100, work, 450, 450));
    }

    internal static PosterDefinition LargeItem(ContentFlowAxis axis)
    {
        var d = ContentLayoutSamples.Equal("A", 3) with { FlowAxis = axis };
        d = d with { Placements = d.Placements.Select((p,i) => i == 0 ? p with { ColumnSpan = 12, RowSpan = 12 } : p).ToArray() };
        return Seed() with { WidthDip = 600, ExpandedHeightDip = 700, ContentLayoutJson = ContentLayoutPolicy.Serialize(d) };
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 96u)] [InlineData(ContentFlowAxis.Vertical, 120u)] [InlineData(ContentFlowAxis.Vertical, 144u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 96u)] [InlineData(ContentFlowAxis.HorizontalLoop, 120u)] [InlineData(ContentFlowAxis.HorizontalLoop, 144u)]
    public void Exact_outward_pixel_minimum_passes_and_one_pixel_less_fails(ContentFlowAxis axis, uint dpi)
    {
        var p = LargeItem(axis); var scene = Scene(p, dpi);
        Assert.True(ContentFrameMinimumPolicy.TryForPoster(p, out var minimum, out var error), error);
        var pixels = minimum!.Pixels(dpi, dpi);
        double width = pixels.Width * 96d / dpi, height = pixels.Height * 96d / dpi;
        Assert.NotNull(ControlCenterSpatial.Plan(p, scene, new(0,0,width,height), out error));
        Assert.Null(error);
        Assert.Null(ControlCenterSpatial.Plan(p, scene, new(0,0,
            width - (axis == ContentFlowAxis.Vertical ? 96d / dpi : 0),
            height - (axis == ContentFlowAxis.HorizontalLoop ? 96d / dpi : 0)), out _));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Main_axis_can_shrink_without_scaling_items_or_mutating_authored_intent(ContentFlowAxis axis)
    {
        var p = LargeItem(axis); string raw = p.ContentLayoutJson!;
        var plan = ControlCenterSpatial.Plan(p, Scene(p), new(0,0,
            axis == ContentFlowAxis.Vertical ? 600 : 240, axis == ContentFlowAxis.Vertical ? 240 : 700), out var error);
        Assert.NotNull(plan); Assert.Null(error); Assert.Equal(raw, plan.Desired.ContentLayoutJson);
        ContentLayoutPolicy.TryRead(raw, "A", out var doc, out _);
        var before = ContentLayoutSolver.Resolve(doc!, new(542,586));
        var after = ContentLayoutSolver.Resolve(doc!, new(plan.Desired.WidthDip - 58, plan.Desired.ExpandedHeightDip - 114));
        Assert.Equal(ContentLayoutSpaceStatus.Normal, after.SpaceStatus);
        Assert.Equal(before.Items.Select(p => (p.ItemId,p.Bounds.Width,p.Bounds.Height)), after.Items.Select(p => (p.ItemId,p.Bounds.Width,p.Bounds.Height)));
    }

    [Fact]
    public void Empty_content_has_only_shell_minimum_and_scaling_uses_solver_readable_minimum()
    {
        var p = LargeItem(ContentFlowAxis.Vertical);
        var empty = p with { ContentLayoutJson = ContentLayoutPolicy.Serialize(ContentLayoutSamples.Equal("A",0)) };
        Assert.True(ContentFrameMinimumPolicy.TryForPoster(empty, out var min, out _));
        Assert.Equal(new ContentFrameMinimum(240,240), min);
        var d = new ContentLayoutDocument("A", [new("A:small",0,1,1)]) { BaseCellDip = 16, BaseGapDip = 3 };
        var view = new ContentLayoutViewport(100,100,Scale:.8,Spacing:1.5);
        var solved = ContentLayoutSolver.Resolve(d,view);
        var resolved = ContentFrameMinimumPolicy.FromSnapshot(solved,300,100);
        Assert.Equal(solved.Items[0].Bounds.Width + 300, resolved.WidthDip);
        Assert.Equal(240,resolved.HeightDip);
    }

    [Theory] [InlineData("{\"schemaVersion\":999}")] [InlineData("broken")]
    public void Unknown_raw_cannot_be_replaced_by_a_fake_empty_minimum_but_move_still_works(string raw)
    {
        var p = LargeItem(ContentFlowAxis.Vertical) with { ContentLayoutJson = raw };
        Assert.False(ContentFrameMinimumPolicy.TryForPoster(p,out var minimum,out _)); Assert.Null(minimum);
        Assert.Null(ControlCenterSpatial.Plan(p,Scene(p),new(0,0,580,700),out _));
        var moved = ControlCenterSpatial.Plan(p,Scene(p),new(5,0,600,700),out _);
        Assert.NotNull(moved); Assert.Equal(raw,moved.Desired.ContentLayoutJson);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Constrained_frame_can_still_move_without_converting_to_active_resize(ContentFlowAxis axis)
    {
        var p = LargeItem(axis); var original = Scene(p); var one = original.Posters[0];
        var scene = new SpatialSnapshot(original.Environment,[one with { Bounds = one.Bounds with { Width=240,Height=240 } }]);
        var moved = ControlCenterSpatial.Plan(p,scene,new(10,20,p.WidthDip,p.ExpandedHeightDip),out _);
        Assert.NotNull(moved); Assert.Equal(240,moved.Changes[0].After.Width); Assert.Equal(240,moved.Changes[0].After.Height);
        Assert.Equal(p.ContentLayoutJson,moved.Desired.ContentLayoutJson);
    }

    internal static SpatialSnapshot Scene(PosterDefinition poster, uint dpi = 96)
    {
        var env = new SpatialEnvironment("display", new(-1800, -100, 2600, 1800), dpi, dpi, 1, 1, Guid.NewGuid());
        var bounds = new SpatialRect(env.WorkArea.X, env.WorkArea.Y,
            (int)Math.Round(poster.WidthDip * dpi / 96d), (int)Math.Round(poster.ExpandedHeightDip * dpi / 96d));
        return new(env, [new(poster.PosterId, bounds, bounds.Height, true, false, false, 1)]);
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Forged_old_240_pixel_trace_cannot_bypass_final_transaction_minimum(ContentFlowAxis axis)
    {
        var p = LargeItem(axis); var scene = Scene(p); var initial = scene.Posters[0].Bounds;
        var final = axis == ContentFlowAxis.Vertical ? initial with { Width=300 } : initial with { Height=300 };
        var trace = new SpatialGestureCommit(scene,final,[new("A",initial,final)],PosterGestureKind.ResizeBottomRight,true);
        var state = new DesktopStateSnapshot(new(1,1,true,[new(1,1,p,DateTimeOffset.UnixEpoch){LayoutGeneration=1}]),
            new(1,1,new(false,DesktopUpPerformanceMode.Automatic,false),DateTimeOffset.UnixEpoch));
        var command = new PosterStateCommand("bad-size",StateCommandSource.Poster,PosterStateCommandKind.Resize,1,"A",
            SpatialGestureCommitPolicy.Capture(p,final,scene.Environment,final.Height))
            { ReadSet = new(new Dictionary<string,long>{{"A",1}},CatalogVersion:1), SpatialGesture=trace };
        Assert.Contains("largest complete",SpatialGestureCommitPolicy.Validate(command,state));
    }
}
