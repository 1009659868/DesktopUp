using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Manual_scale_uses_uncapped_heat_target_and_rejects_blocked_growth(ContentFlowAxis axis)
    {
        var author = ContentLayoutSamples.Equal("A", 1) with { AllowHeatDrivenSize = true, FlowAxis = axis };
        using var f = new Fixture(Seed() with { WidthDip = 200, ExpandedHeightDip = 250,
            ContentLayoutJson = ContentLayoutPolicy.Serialize(author) });
        var before = f.Coordinator.Refresh(); var target = before.PostersById["A"];
        var scene = ScaleScene(target);
        var bounds = scene.Posters[0].Bounds;
        var obstacle = axis == ContentFlowAxis.Vertical ? new SpatialRect((int)bounds.Right, 0, 400, 800)
            : new SpatialRect(0, (int)bounds.Bottom, 1000, 500);
        scene = new(scene.Environment, [scene.Posters[0], new("B", obstacle, obstacle.Height, true, true, false, 1)]);
        var desired = new PosterSettingEdit(target, PosterSetting.Scale, 1.2).Command("scale-with-heat").DesiredState;
        var heat = new ContentHeatProposal("A", author.ItemSetVersion, 1,
            [new(author.Placements[0].ItemId, 0, 2.7)], ContentLayoutProposals.ContinuousMappingVersion)
            { AuthorDocumentVersion = author.DocumentVersion };
        Assert.NotNull(ContentScaleExpansion.Plan(target.Poster, desired, scene, out _));
        Assert.Null(ContentScaleExpansion.Plan(target.Poster, desired, scene, out var error, heat));
        Assert.NotNull(error);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Equal(target.Poster, f.Coordinator.Refresh().PostersById["A"].Poster);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Activity_grows_with_same_space_planner_then_caps_between_locked_neighbours(ContentFlowAxis axis)
    {
        var author = ContentLayoutSamples.Equal("A", 2) with { AllowHeatDrivenSize = true, FlowAxis = axis };
        var seed = Seed() with { WidthDip = 200, ExpandedHeightDip = 250,
            ContentLayoutJson = ContentLayoutPolicy.Serialize(author) };
        using var f = new Fixture(seed);
        var before = f.Coordinator.Refresh(); var entry = before.PostersById["A"];
        var proposal = new ContentHeatProposal("A", author.ItemSetVersion, 1,
            author.Placements.Select(p => new ContentHeatItem(p.ItemId, p.Order, 2.7)).ToArray(),
            ContentLayoutProposals.ContinuousMappingVersion) { AuthorDocumentVersion = author.DocumentVersion };
        var scene = ScaleScene(entry);
        var plan = ContentHeatExpansion.Plan(entry.Poster, proposal, scene, out var error);
        Assert.NotNull(plan); Assert.Null(error); Assert.Single(plan.Changes);
        var command = new PosterStateCommand("heat-grow", StateCommandSource.System, PosterStateCommandKind.Resize,
            before.Revision, "A", plan.Desired) { ContentHeatExpansion = new(scene, proposal),
            ReadSet = new(before.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: before.CatalogVersion) };
        Assert.False(f.Coordinator.Execute(command).Succeeded); // Ordinary callers cannot authorize system geometry.
        Assert.False(f.Coordinator.ExecuteContentHeatExpansion(command with { RequestId = "heat-stale", ContentHeatExpansion = new(scene, proposal with { AuthorDocumentVersion = 99 }) }).Succeeded);
        Assert.False(f.Coordinator.ExecuteContentHeatExpansion(command with { RequestId = "heat-forged", DesiredState = plan.Desired with { Title = "changed" } }).Succeeded);
        var committed = f.Coordinator.ExecuteContentHeatExpansion(command); Assert.True(committed.Succeeded, committed.Error);
        Assert.Equal(seed.ContentLayoutJson, committed.Projection!.PostersById["A"].Poster.ContentLayoutJson);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.ExecuteContentHeatExpansion(command).Status);
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(plan.Desired, new SqlitePosterRepository(reopened).Read("A")!.Poster);

        // A poster between two locked neighbours has no legal growth path. Its derived
        // display fits the current frame instead, without changing either neighbour or author.
        var source = scene.Posters[0];
        var middle = axis == ContentFlowAxis.Vertical ? source with { Bounds = source.Bounds with { X = 300 } }
            : source with { Bounds = source.Bounds with { Y = 300 } };
        var left = axis == ContentFlowAxis.Vertical ? new SpatialRect(0, 0, 300, 900) : new SpatialRect(0, 0, 1000, 300);
        var right = axis == ContentFlowAxis.Vertical ? new SpatialRect((int)middle.Bounds.Right, 0, 300, 900)
            : new SpatialRect(0, (int)middle.Bounds.Bottom, 1000, 300);
        var blocked = new SpatialSnapshot(scene.Environment, [middle, new("B", left, left.Height, true, true, false, 1), new("C", right, right.Height, true, true, false, 1)]);
        Assert.Null(ContentHeatExpansion.Plan(entry.Poster, proposal, blocked, out _));
        var viewport = new ContentLayoutViewport(160, 110);
        var display = ContentHeatSizing.Project(author, proposal, viewport);
        Assert.False(ContentLayoutSolver.Resolve(display, viewport).IsLayoutReadOnly);
        Assert.Equal(author.Placements, ContentLayoutSamples.Equal("A", 2).Placements);
    }
}
