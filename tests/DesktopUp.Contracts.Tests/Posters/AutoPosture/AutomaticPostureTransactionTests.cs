using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class SpatialContinuityIntegrationTests
{
    [Fact]
    public void AutomaticPosture_nested_push_restore_uses_full_CAS_and_one_committed_revision()
    {
        using var f = new Fixture(automatic: true);
        foreach (var (id, collapse) in new[] { ("C", false), ("B", false), ("C", true), ("B", true) })
        {
            var command = f.Command(id, collapse) with { Source = StateCommandSource.System };
            var before = f.Coordinator.Refresh();
            var result = f.Coordinator.ExecuteAutomaticPosture(command);
            Assert.Equal(StateCommandStatus.Committed, result.Status);
            Assert.Equal(before.Revision + 1, result.CurrentRevision);
            Assert.Equal(command.ReadSet!.Posters.Count, before.Posters.Count);
            Assert.All(result.Projection!.Posters, p => Assert.Equal(result.CurrentRevision, p.Revision));
            Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.ExecuteAutomaticPosture(command).Status);
        }
        Assert.Equal(0, f.Find("C").Poster.FallbackYDip);
        Assert.Equal(60, f.Find("B").Poster.FallbackYDip);
        Assert.Equal(120, f.Find("A").Poster.FallbackYDip);
    }

    [Theory]
    [InlineData("general-port")] [InlineData("disabled")] [InlineData("wrong-source")]
    [InlineData("rename")] [InlineData("no-spatial")] [InlineData("partial-read")]
    [InlineData("no-continuity")] [InlineData("unrelated-field")]
    public void AutomaticPosture_port_does_not_enable_other_system_mutations(string kind)
    {
        using var f = new Fixture(automatic: kind != "disabled");
        var command = f.Command("C", false) with { Source = StateCommandSource.System };
        command = kind switch
        {
            "wrong-source" => command with { Source = StateCommandSource.Poster },
            "rename" => command with { Kind = PosterStateCommandKind.Rename },
            "no-spatial" => command with { SpatialPosture = null },
            "partial-read" => command with { ReadSet = StateReadSet.ForPoster(command.PosterId, command.ReadSet!.Posters[command.PosterId]) },
            "no-continuity" => command with { SpatialPosture = command.SpatialPosture! with { UseContinuity = false } },
            "unrelated-field" => command with { DesiredState = command.DesiredState with { Title = "escape" } },
            _ => command
        };
        var before = f.Coordinator.Refresh();
        var result = kind == "general-port" ? f.Coordinator.Execute(command) : f.Coordinator.ExecuteAutomaticPosture(command);
        Assert.False(result.Succeeded); Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Empty(f.Coordinator.Refresh().SpatialContinuities);
    }

    [Fact]
    public void AutomaticPosture_conflict_and_SQL_failure_leave_no_half_state()
    {
        using var f = new Fixture(automatic: true); var command = f.Command("C", false) with { Source = StateCommandSource.System };
        var b = f.Find("B");
        var rename = new PosterStateCommand("rename", StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
            command.ExpectedRevision, b.Poster.PosterId, b.Poster with { Title = "B2" })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) };
        Assert.True(f.Coordinator.Execute(rename).Succeeded);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.ExecuteAutomaticPosture(command).Status);
        command = f.Command("C", false) with { Source = StateCommandSource.System };
        var before = f.Coordinator.Refresh();
        f.Execute("CREATE TRIGGER reject_auto BEFORE INSERT ON spatial_continuity BEGIN SELECT RAISE(ABORT, 'injected'); END;");
        Assert.False(f.Coordinator.ExecuteAutomaticPosture(command).Succeeded);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Equal(before.Posters.Select(p => p.Poster), f.Coordinator.Refresh().Posters.Select(p => p.Poster));
    }

    [Fact]
    public void AutomaticPosture_evidence_distinguishes_manual_posture_from_automatic_and_metadata()
    {
        using var f = new Fixture(automatic: true); var evidence = new AutoPostureEvidence();
        evidence.Apply(new(f.Coordinator.Refresh(), StateCommandSource.System, "initial"));
        f.Coordinator.ProjectionChanged += (_, e) => evidence.Apply(e);
        var c = f.Find("C"); var command = f.Command("C", false) with { Source = StateCommandSource.System };
        Assert.True(f.Coordinator.ExecuteAutomaticPosture(command).Succeeded);
        Assert.Equal(0, evidence.SpaceVersion); Assert.Equal(0, evidence.ManualVersion(c.Poster.PosterId));
        var b = f.Find("B"); var before = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("rename", StateCommandSource.ControlCenter,
            PosterStateCommandKind.Rename, before.Revision, b.Poster.PosterId, b.Poster with { Title = "B2" })
            { ReadSet = StateReadSet.ForPoster(b.Poster.PosterId, b.EntityVersion) }).Succeeded);
        Assert.Equal(0, evidence.SpaceVersion);
        Assert.True(f.Coordinator.Execute(f.Command("C", true)).Succeeded);
        Assert.Equal(1, evidence.SpaceVersion); Assert.Equal(1, evidence.ManualVersion(c.Poster.PosterId));
        Assert.Equal(0, evidence.ManualVersion(b.Poster.PosterId)); // returned neighbour is not manual intent
    }

    [Fact]
    public void AutomaticPosture_lock_allows_own_posture_not_neighbour_push()
    {
        using var f = new Fixture(automatic: true); var c = f.Find("C"); var before = f.Coordinator.Refresh();
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("lock-c", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, before.Revision, c.Poster.PosterId, c.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(c.Poster.PosterId, c.EntityVersion) }).Succeeded);
        Assert.True(f.Coordinator.ExecuteAutomaticPosture(f.Command("C", false) with { Source = StateCommandSource.System }).Succeeded);
        Assert.True(f.Find("C").Poster.IsLocked);
    }
}
