using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ControlCenterEditTransactionTests
{
    private static SpatialEnvironment EnvironmentForCenter(uint dpi = 96) => new("display", new(-1920, -100, 1600, 1200), dpi, dpi, 1, 1, Guid.NewGuid());
    private static SpatialSnapshot SceneForCenter(DesktopUpProjection p, uint dpi = 96, int? effectiveHeight = null)
    {
        var env = EnvironmentForCenter(dpi);
        return new(env, p.Posters.Select(t =>
        {
            var d = t.Poster;
            SpatialPixelProjection.TryProjectLocalDip(d.FallbackXDip, d.FallbackYDip, d.WidthDip,
                d.IsCollapsed ? 60 : effectiveHeight ?? d.ExpandedHeightDip, env.WorkArea.X, env.WorkArea.Y, dpi, dpi, out var bounds);
            return new SpatialPoster(d.PosterId, bounds, (int)Math.Round(d.ExpandedHeightDip * dpi / 96d), d.IsVisible, d.IsLocked, d.IsCollapsed, t.LayoutGeneration);
        }));
    }
    private static StateReadSet AllReads(DesktopUpProjection p) => new(p.Posters.ToDictionary(t => t.Poster.PosterId, t => t.EntityVersion), CatalogVersion: p.CatalogVersion);
    private static PosterStateCommand CenterCommand(DesktopUpProjection p, SpatialSnapshot scene, PosterGeometryIntent intent)
    {
        var target = p.PostersById["A"];
        var plan = ControlCenterSpatial.Plan(target.Poster, scene, intent, out var error);
        Assert.NotNull(plan);
        bool resize = intent.Width != target.Poster.WidthDip || intent.ExpandedHeight != target.Poster.ExpandedHeightDip;
        return new(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, resize ? PosterStateCommandKind.Resize : PosterStateCommandKind.Move,
            p.Revision, "A", plan.Desired) { ReadSet = AllReads(p), ControlCenterGeometry = new(scene, intent) };
    }


    [Theory] [InlineData(96u)] [InlineData(120u)] [InlineData(144u)]
    public void Center_combined_move_resize_is_one_revision_and_restarts(uint dpi)
    {
        using var f = new Fixture(); var before = f.Current; var scene = SceneForCenter(before, dpi);
        var command = CenterCommand(before, scene, new(40, 20, 360, 480));
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
        Assert.Equal(result.CurrentRevision, result.Projection!.Tray.Revision);
        Assert.Equal(command.DesiredState, result.Projection.PostersById["A"].Poster);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(command.DesiredState, new StateProjectionCoordinator(new SqliteStateCommandRepository(f.Store)).Refresh().PostersById["A"].Poster);
    }
    [Fact]
    public void Center_move_preserves_constrained_height_intent()
    {
        using var f = new Fixture(); var before = f.Current; var scene = SceneForCenter(before, effectiveHeight: 300);
        var command = CenterCommand(before, scene, new(25, 30, 300, 600));
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(600, result.Projection!.PostersById["A"].Poster.ExpandedHeightDip);
        Assert.Equal(300, ControlCenterSpatial.Plan(before.PostersById["A"].Poster, scene, new(25, 30, 300, 600), out _)!.Changes[0].After.Height);
    }
    [Fact]
    public void Center_collision_is_rejected_not_clamped()
    {
        using var f = new Fixture(); f.Add(); var before = f.Current;
        Assert.Null(ControlCenterSpatial.Plan(before.PostersById["A"].Poster, SceneForCenter(before), new(600, 0, 300, 600), out var error));
        Assert.NotNull(error); Assert.Equal(before.Revision, f.Current.Revision);
    }
    [Theory] [InlineData(-10, 0, 300, 600)] [InlineData(0, 1000, 300, 600)] [InlineData(0, 0, 200, 600)] [InlineData(0, 0, 300, 1201)]
    public void Center_invalid_or_outside_intent_is_rejected(double x, double y, double w, double h)
    {
        using var f = new Fixture(); Assert.Null(ControlCenterSpatial.Plan(f.Current.PostersById["A"].Poster, SceneForCenter(f.Current), new(x,y,w,h), out _));
    }
    [Fact]
    public void Center_stale_obstacle_read_rejects_whole_combined_edit()
    {
        using var f = new Fixture(); string b = f.Add(); var before = f.Current;
        var command = CenterCommand(before, SceneForCenter(before), new(10, 10, 300, 480));
        f.Rename(b, "changed"); long revision = f.Current.Revision;
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(command).Status);
        Assert.Equal(revision, f.Current.Revision); Assert.Equal(before.PostersById["A"].Poster, f.Current.PostersById["A"].Poster);
    }
    [Fact]
    public void Center_proof_rejects_partial_reads_tampered_result_and_mixed_proof()
    {
        using var f = new Fixture(); f.Add(); var command = CenterCommand(f.Current, SceneForCenter(f.Current), new(20, 10, 300, 480));
        Assert.False(f.Coordinator.Execute(command with { ReadSet = StateReadSet.ForPoster("A", f.Current.PostersById["A"].EntityVersion) }).Succeeded);
        Assert.False(f.Coordinator.Execute(command with { RequestId = "tamper", DesiredState = command.DesiredState with { Title = "wrong" } }).Succeeded);
        Assert.False(f.Coordinator.Execute(command with { RequestId = "mixed", Source = StateCommandSource.Tray }).Succeeded);
    }
    [Fact]
    public void Center_geometry_sql_failure_is_atomic()
    {
        using var f = new Fixture(); var before = f.Current;
        f.Sql("CREATE TRIGGER fail_center BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT, 'injected'); END;");
        Assert.False(f.Coordinator.Execute(CenterCommand(before, SceneForCenter(before), new(20,30,360,480))).Succeeded);
        Assert.Equal(before.Revision, f.Current.Revision); Assert.Equal(before.PostersById["A"].Poster, f.Current.PostersById["A"].Poster);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Catalog_safe_placement_is_atomic_and_config_only(bool clone)
    {
        using var f = new Fixture(); var before = f.Current; var scene = SceneForCenter(before);
        var result = clone ? f.Coordinator.Execute(new ClonePosterCommand("clone-safe", StateCommandSource.ControlCenter, before.Revision,
            AllReads(before), "A") { Placement = new(scene), Title = "copy" }) : f.Coordinator.Execute(new CreatePosterCommand("create-safe",
                StateCommandSource.ControlCenter, before.Revision, AllReads(before), Seed("new")) { Placement = new(scene) });
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision+1, result.CurrentRevision);
        var added = result.Projection!.PostersById[result.ResultPosterId!].Poster;
        Assert.NotEqual("A", added.PosterId); Assert.NotEqual(before.PostersById["A"].Poster.ThemeInstanceId, added.ThemeInstanceId);
        Assert.Null(added.CollectionId); Assert.Equal(300, added.FallbackXDip); Assert.Equal(0, added.FallbackYDip);
        Assert.Equal(before.PostersById["A"].Poster, result.Projection.PostersById["A"].Poster);
        if (clone) Assert.Equal("copy", added.Title);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Catalog_no_space_leaves_no_partial_entity(bool clone)
    {
        using var f = new Fixture(); var p = f.Current; var original = SceneForCenter(p);
        var scene = new SpatialSnapshot(original.Environment with { WorkArea = original.Posters[0].Bounds }, original.Posters);
        var r = clone ? f.Coordinator.Execute(new ClonePosterCommand("clone-full", StateCommandSource.ControlCenter, p.Revision, AllReads(p), "A") { Placement = new(scene) }) :
            f.Coordinator.Execute(new CreatePosterCommand("create-full", StateCommandSource.ControlCenter, p.Revision, AllReads(p), Seed()) { Placement = new(scene) });
        Assert.Equal(StateCommandStatus.InsufficientSpace, r.Status); Assert.Equal(p.Revision, f.Current.Revision); Assert.Single(f.Current.Posters);
    }
    [Fact]
    public void Catalog_zero_can_create_again_with_fresh_identity()
    {
        using var f = new Fixture(); f.Remove("A"); var p = f.Current;
        var r = f.Coordinator.Execute(new CreatePosterCommand("empty-create", StateCommandSource.ControlCenter, p.Revision, AllReads(p), Seed())
            { Placement = new(new(EnvironmentForCenter(), [])) });
        Assert.True(r.Succeeded, r.Error); Assert.Single(r.Projection!.Posters); Assert.NotEqual("A", r.ResultPosterId);
    }

    [Fact]
    public void Center_cannot_omit_same_workspace_obstacle_from_proof()
    {
        using var f = new Fixture(); f.Add(); var p = f.Current; var scene = SceneForCenter(p);
        var forged = new SpatialSnapshot(scene.Environment, scene.Posters.Where(s => s.PosterId == "A"));
        var command = CenterCommand(p, forged, new(20, 0, 300, 600));
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.False(f.Coordinator.Execute(new ClonePosterCommand("omit-clone", StateCommandSource.ControlCenter, p.Revision, AllReads(p), "A")
            { Placement = new(forged) }).Succeeded);
        Assert.Equal(p.Revision, f.Current.Revision);
    }
    [Fact]
    public void Center_cannot_use_old_layout_generation()
    {
        using var f = new Fixture(); var p = f.Current; var scene = SceneForCenter(p);
        var forged = new SpatialSnapshot(scene.Environment, scene.Posters.Select(s => s with { LayoutGeneration = s.LayoutGeneration + 1 }));
        Assert.False(f.Coordinator.Execute(CenterCommand(p, forged, new(20,0,300,600))).Succeeded);
    }
    [Fact]
    public void Center_locked_rejects_geometry_and_collapsed_accepts_only_position()
    {
        using var f = new Fixture(); var p = f.Current.PostersById["A"];
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("lock", StateCommandSource.ControlCenter, PosterStateCommandKind.SetLocked,
            f.Current.Revision, "A", p.Poster with { IsLocked = true }) { ReadSet = StateReadSet.ForPoster("A", p.EntityVersion) }).Succeeded);
        Assert.Null(ControlCenterSpatial.Plan(f.Current.PostersById["A"].Poster, SceneForCenter(f.Current), new(20,0,300,600), out _));
        var compact = p.Poster with { IsCollapsed = true };
        var scene = SceneForCenter(f.Current);
        scene = new(scene.Environment, scene.Posters.Select(s => s with { Bounds = s.Bounds with { Height = 60 }, IsCollapsed = true, IsLocked = false }));
        Assert.NotNull(ControlCenterSpatial.Plan(compact, scene, new(20,0,300,600), out _));
        Assert.Null(ControlCenterSpatial.Plan(compact, scene, new(20,0,320,600), out _));
    }
    [Fact]
    public void Center_try_resize_first_when_move_first_has_no_room()
    {
        var poster = Seed();
        var env = EnvironmentForCenter() with { WorkArea = new(0,0,600,600) };
        var scene = new SpatialSnapshot(env, new[] { new SpatialPoster("A", new(0,0,300,600), 600,true,false,false,0) });
        var plan = ControlCenterSpatial.Plan(poster, scene, new(0,100,300,480), out var error);
        Assert.NotNull(plan); Assert.Null(error); Assert.Equal(2, plan.Path.Count);
        Assert.Equal(480, plan.Path[0].To.Height); Assert.Equal(0, plan.Path[0].To.Y); Assert.Equal(100, plan.Path[1].To.Y);
    }

    [Theory] [InlineData("poster")] [InlineData("create")] [InlineData("clone")]
    public void New_optional_fields_preserve_legacy_receipt_fingerprint_when_absent(string kind)
    {
        using var f = new Fixture(); var p = f.Current; var reads = AllReads(p);
        object command, legacy, changed;
        if (kind == "poster")
        {
            var c = new PosterStateCommand("old", StateCommandSource.ControlCenter, PosterStateCommandKind.Rename, p.Revision, "A", Seed()) { ReadSet = reads };
            command = c;
            legacy = new { c.RequestId, c.Source, c.Kind, c.ExpectedRevision, c.PosterId, c.DesiredState, c.ReadSet, c.SpatialGesture, c.SpatialPosture };
            changed = c with { ControlCenterGeometry = new(SceneForCenter(p), new(0,0,300,600)) };
        }
        else if (kind == "create")
        {
            var c = new CreatePosterCommand("old", StateCommandSource.ControlCenter, p.Revision, reads, Seed());
            command = c; legacy = new { c.RequestId, c.Source, c.ExpectedRevision, c.ReadSet, c.Configuration };
            changed = c with { Placement = new(SceneForCenter(p)) };
        }
        else
        {
            var c = new ClonePosterCommand("old", StateCommandSource.ControlCenter, p.Revision, reads, "A");
            command = c; legacy = new { c.RequestId, c.Source, c.ExpectedRevision, c.ReadSet, c.SourcePosterId };
            changed = c with { Title = "changed" };
        }
        Assert.Equal(StateRequestFingerprint.Compute(legacy), StateRequestFingerprint.Compute(command));
        Assert.NotEqual(StateRequestFingerprint.Compute(command), StateRequestFingerprint.Compute(changed));
    }
}
