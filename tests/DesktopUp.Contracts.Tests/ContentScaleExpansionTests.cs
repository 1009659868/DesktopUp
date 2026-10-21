using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    private static PosterDefinition ScaleSeed(ContentFlowAxis axis)
    {
        var poster = Seed() with
        {
            WidthDip = 500, ExpandedHeightDip = 500,
            ContentLayoutJson = ContentLayoutPolicy.Serialize(ContentLayoutSamples.Equal("A", 2) with { FlowAxis = axis }),
            LayoutParametersJson = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults("basic-box") with
                { Preferences = new(.4, 1) })
        };
        Assert.True(ContentFrameMinimumPolicy.TryForPoster(poster, out var minimum, out _));
        return axis == ContentFlowAxis.Vertical ? poster with { WidthDip = Math.Ceiling(minimum!.WidthDip) }
            : poster with { ExpandedHeightDip = Math.Ceiling(minimum!.HeightDip) };
    }

    private static SpatialSnapshot ScaleScene(PosterSurfaceProjection target, uint dpi = 96) =>
        new(new("DISPLAY1", new(0, 0, 1500, 1200), dpi, dpi, 1, 1, Guid.NewGuid()),
            [new(target.Poster.PosterId, new(0, 0, (int)Math.Ceiling(target.Poster.WidthDip * dpi / 96d),
                (int)Math.Ceiling(target.Poster.ExpandedHeightDip * dpi / 96d)),
                (int)Math.Ceiling(target.Poster.ExpandedHeightDip * dpi / 96d), true, target.Poster.IsLocked, false, target.LayoutGeneration)]);

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, 96u)]
    [InlineData(ContentFlowAxis.Vertical, 144u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 96u)]
    [InlineData(ContentFlowAxis.HorizontalLoop, 144u)]
    public void Scale_expansion_commits_geometry_and_scale_once_and_reopens(ContentFlowAxis axis, uint dpi)
    {
        using var f = new Fixture(ScaleSeed(axis));
        var before = f.Coordinator.Refresh();
        var target = before.PostersById["A"];
        var command = new PosterSettingEdit(target, PosterSetting.Scale, 1.173).Command("scale-grow");
        var scene = ScaleScene(target, dpi);
        var plan = ContentScaleExpansion.Plan(target.Poster, command.DesiredState, scene, out var error);
        Assert.NotNull(plan); Assert.Null(error);
        Assert.Single(plan.Changes);
        var expanded = command with { DesiredState = plan.Desired,
            ContentScaleExpansion = new(scene, command.DesiredState),
            ReadSet = new(before.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: before.CatalogVersion) };
        Assert.False(f.Coordinator.Execute(expanded with { RequestId = "missing-proof", ContentScaleExpansion = null }).Succeeded);
        Assert.False(f.Coordinator.Execute(expanded with { RequestId = "forged", DesiredState = expanded.DesiredState with { Title = "forged" } }).Succeeded);
        Assert.False(f.Coordinator.Execute(expanded with { RequestId = "stale-catalog",
            ReadSet = expanded.ReadSet! with { CatalogVersion = before.CatalogVersion + 1 } }).Succeeded);
        var result = f.Coordinator.Execute(expanded);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(before.Revision + 1, result.CurrentRevision);
        var after = f.Coordinator.Refresh().PostersById["A"];
        Assert.Equal(1.173, after.Presentation.Parameters.Preferences!.Scale);
        Assert.Equal(target.Poster.ContentLayoutJson, after.Poster.ContentLayoutJson);
        Assert.Equal(target.LayoutGeneration + 1, after.LayoutGeneration);
        Assert.True(axis == ContentFlowAxis.Vertical ? after.Poster.WidthDip > target.Poster.WidthDip :
            after.Poster.ExpandedHeightDip > target.Poster.ExpandedHeightDip);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(expanded).Status);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(expanded with { RequestId = "stale-scale" }).Status);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(after.Poster, new SqlitePosterRepository(reopened).Read("A")!.Poster);
    }

    [Theory]
    [InlineData("work-area")] [InlineData("neighbour")] [InlineData("locked")]
    public void Blocked_scale_discards_only_its_preview_and_does_not_own_the_manual_frame_menu(string blocker)
    {
        using var f = new Fixture(ScaleSeed(ContentFlowAxis.Vertical) with { IsLocked = blocker == "locked" });
        var before = f.Coordinator.Refresh(); var target = before.PostersById["A"];
        var scene = ScaleScene(target);
        var bounds = scene.Posters[0].Bounds;
        if (blocker == "work-area") scene = new(scene.Environment with { WorkArea = bounds }, scene.Posters);
        if (blocker == "neighbour") scene = new(scene.Environment,
            [scene.Posters[0], new("B", new(checked((int)bounds.Right), 0, 400, 600), 600, true, false, false, 1)]);
        var edit = new PosterSettingEdit(target, PosterSetting.Scale, 2);
        var plan = ContentScaleExpansion.Plan(target.Poster, edit.Command("candidate").DesiredState, scene, out var error);
        Assert.Null(plan); Assert.NotNull(error);
        var session = new ControlCenterSettingsSession();
        Assert.True(session.Update(edit));
        session.RejectPreview(error!, discard: true);
        session.Finish();
        Assert.False(session.OwnsPoster("A")); Assert.False(session.ProtectsInteraction);
        Assert.Null(session.Preview); Assert.Null(session.TakeNext()); Assert.Equal(error, session.Error);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.True(session.Update(edit with { Number = .4 })); // A failed attempt never wedges the next gesture.
    }
}
