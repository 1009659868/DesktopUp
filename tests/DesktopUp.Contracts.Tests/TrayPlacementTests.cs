using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ControlCenterEditTransactionTests
{
    [Theory] [InlineData(96u)] [InlineData(120u)] [InlineData(144u)]
    public void TrayLatentPlacementPreservesAllPeersAndCommitsOnce(uint dpi)
    {
        using var f = new Fixture(); f.Add(); var before = f.Current;
        // The same recovery occupancy is used while all HWNDs are absent. Individual IsVisible stays true.
        var projected = SceneForCenter(before, dpi);
        // At 150%, A/B consume through x=1200 and another 450px cannot fit a 1600px area.
        // This is the success case; the separate full-workspace case asserts rejection.
        var scene = new SpatialSnapshot(projected.Environment with
            { WorkArea = projected.Environment.WorkArea with { Width = 2000 } }, projected.Posters);
        var request = TrayManagement.Create(before, "tray-latent");
        var command = new CreatePosterCommand(request.RequestId, request.Source, request.Revision, request.Reads, Seed("latent"))
            { Placement = new(scene) };
        var r = f.Coordinator.Execute(command);
        Assert.True(r.Succeeded, r.Error); Assert.Equal(before.Revision + 1, r.CurrentRevision);
        var next = r.Projection!.PostersById[r.ResultPosterId!].Poster;
        Assert.True(next.IsVisible);
        SpatialPixelProjection.TryProjectLocalDip(next.FallbackXDip, next.FallbackYDip, next.WidthDip, next.ExpandedHeightDip,
            scene.Environment.WorkArea.X, scene.Environment.WorkArea.Y, dpi, dpi, out var bounds);
        Assert.All(scene.Posters, p => Assert.False(p.Bounds.Overlaps(bounds)));
        Assert.All(before.Posters, p => Assert.Equal(p.Poster, r.Projection.PostersById[p.Poster.PosterId].Poster));
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
    }

    [Fact]
    public void TrayLatentPlacementCannotPretendHiddenRuntimeIsEmpty()
    {
        using var f = new Fixture(); var before = f.Current; var scene = SceneForCenter(before);
        var request = TrayManagement.Create(before, "tray-omitted");
        var r = f.Coordinator.Execute(new CreatePosterCommand(request.RequestId, request.Source, request.Revision, request.Reads, Seed())
            { Placement = new(new(scene.Environment, [])) });
        Assert.False(r.Succeeded); Assert.Equal(before.Revision, f.Current.Revision);
    }

    [Fact]
    public void TrayLatentFullWorkspaceRejectsWithoutCreating()
    {
        using var f = new Fixture(); var before = f.Current; var scene = SceneForCenter(before);
        var request = TrayManagement.Create(before, "tray-full");
        var full = new SpatialSnapshot(scene.Environment with { WorkArea = scene.Posters[0].Bounds }, scene.Posters);
        var r = f.Coordinator.Execute(new CreatePosterCommand(request.RequestId, request.Source, request.Revision, request.Reads, Seed())
            { Placement = new(full) });
        Assert.Equal(StateCommandStatus.InsufficientSpace, r.Status); Assert.Equal(before.Revision, f.Current.Revision);
    }

    [Fact]
    public void TrayPlacementSqlFailureLeavesCatalogUntouched()
    {
        using var f = new Fixture(); var before = f.Current;
        f.Sql("CREATE TRIGGER fail_tray BEFORE INSERT ON posters BEGIN SELECT RAISE(ABORT, 'tray injected'); END;");
        var request = TrayManagement.Create(before, "tray-sql-fail");
        var r = f.Coordinator.Execute(new CreatePosterCommand(request.RequestId, request.Source, request.Revision, request.Reads, Seed())
            { Placement = new(SceneForCenter(before)) });
        Assert.False(r.Succeeded); Assert.Equal(before.Revision, f.Current.Revision); Assert.Single(f.Current.Posters);
    }
}
