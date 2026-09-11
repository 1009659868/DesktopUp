using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopUp.Contracts.Tests;

public sealed partial class AutoPostureSettingsTests
{
    private static PosterDefinition Seed(string id = "A") => new(id, "Box", "Keep", "DISPLAY1",
        10, 20, 300, 600, .1, .2, .3, .6, false, true, false, "grid", "{}", "basic-box");

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.M08.Settings.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public string DatabasePath => Path.Combine(Paths.State, "desktopup.sqlite3");
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current => Coordinator.Refresh();
        public Fixture(bool initialize = true, PosterDefinition? seed = null)
        {
            Paths = new(_root); Paths.EnsureCreated(); Store = new(Paths);
            Coordinator = new(new SqliteStateCommandRepository(Store));
            if (initialize) { Assert.True(Store.Initialize().Succeeded); Coordinator.EnsureInitialized(seed ?? Seed()); }
        }
        public SqliteConnection Open(string? path = null)
        {
            var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path ?? DatabasePath, Pooling = false }.ToString());
            c.Open(); return c;
        }
        public object? Scalar(string sql, string? path = null)
        {
            using var c = Open(path); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar();
        }
        public void Execute(string sql)
        {
            using var c = Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery();
        }
        public string Add()
        {
            var s = Current;
            var r = Coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                s.Revision, StateReadSet.ForCatalog(s.CatalogVersion), Seed("input")));
            Assert.True(r.Succeeded, r.Error); return r.ResultPosterId!;
        }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            // Only this fixture's uniquely allocated temporary directory, never a user data root.
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void Default_is_off_three_seconds_and_projection_reuses_the_pure_model_policy()
    {
        using var f = new Fixture(); var s = f.Current;
        Assert.False(s.PostersById["A"].AutoPosture.Enabled);
        Assert.Equal(3, s.PostersById["A"].AutoPosture.LeaveDelaySeconds);
        Assert.Equal(0, s.Tray.AutoPostureEnabledPosterCount);
        Assert.Equal(new AutoPosturePolicy(), s.PostersById["A"].AutoPosture);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(300)]
    public void Settings_commit_uses_one_revision_and_does_not_change_posture_or_geometry(int delay)
    {
        using var f = new Fixture(); string b = f.Add(); var before = f.Current;
        var observed = new List<StateProjectionChangedEventArgs>(); f.Coordinator.ProjectionChanged += (_, e) => observed.Add(e);
        var draft = PosterAutoPostureDraft.Open(before.PostersById["A"]) with { Enabled = true, LeaveDelaySeconds = delay };
        var result = f.Coordinator.Execute(draft.ToCommand("configure"));
        Assert.Equal(StateCommandStatus.Committed, result.Status); var after = result.Projection!;
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(after.Revision, after.ControlCenter.Revision); Assert.Equal(after.Revision, after.Tray.Revision);
        Assert.All(after.Posters, p => Assert.Equal(after.Revision, p.Revision));
        Assert.Same(after.PostersById["A"], after.ControlCenter.PostersById["A"]);
        Assert.Equal(before.PostersById["A"].Poster with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = delay }, after.PostersById["A"].Poster);
        Assert.Equal(before.PostersById["A"].EntityVersion + 1, after.PostersById["A"].EntityVersion);
        Assert.Equal(before.PostersById["A"].LayoutGeneration, after.PostersById["A"].LayoutGeneration);
        Assert.Equal(before.PostersById[b].Poster, after.PostersById[b].Poster);
        Assert.Equal(before.PostersById[b].EntityVersion, after.PostersById[b].EntityVersion);
        Assert.Equal(before.ControlCenter.Settings.EntityVersion, after.ControlCenter.Settings.EntityVersion);
        Assert.Equal(before.CatalogVersion, after.CatalogVersion);
        Assert.Equal(1, after.Tray.AutoPostureEnabledPosterCount);
        var e = Assert.Single(observed); Assert.Equal(new[] { "A" }, e.ChangedPosterIds);
        Assert.False(e.SettingsChanged); Assert.False(e.CatalogChanged);
        using var reopened = new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        var reload = new StateProjectionCoordinator(new SqliteStateCommandRepository(reopened)).Refresh();
        Assert.Equal(after.Revision, reload.Revision); Assert.Equal(after.PostersById["A"].Poster, reload.PostersById["A"].Poster);
        Assert.Equal(after.Tray.AutoPostureEnabledPosterCount, reload.Tray.AutoPostureEnabledPosterCount);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(301)] [InlineData(int.MaxValue)]
    public void Invalid_delay_is_rejected_even_when_disabled_in_command_and_repository(int delay)
    {
        using var f = new Fixture(); var before = f.Current;
        var command = (PosterAutoPostureDraft.Open(before.PostersById["A"]) with { LeaveDelaySeconds = delay }).ToCommand("invalid");
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command).Status);
        Assert.Equal(RepositoryWriteStatus.ValidationFailed, new SqlitePosterRepository(f.Store).Save(before.Revision, "raw-invalid", command.DesiredState).Status);
        Assert.Equal(before.Revision, f.Current.Revision);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts;"));
    }

    [Theory]
    [InlineData("1.5")] [InlineData("\"3\"")] [InlineData("null")]
    public void Json_does_not_round_or_coerce_non_integer_delay(string value)
    {
        var json = JsonSerializer.SerializeToNode(Seed())!.AsObject();
        json[nameof(PosterDefinition.AutoCollapseDelaySeconds)] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => json.Deserialize<PosterDefinition>());
    }

    [Fact]
    public void Older_serialized_definition_defaults_only_the_missing_delay()
    {
        var p = Seed() with { AutoCollapseEnabled = true };
        var json = JsonSerializer.SerializeToNode(p)!.AsObject(); json.Remove(nameof(PosterDefinition.AutoCollapseDelaySeconds));
        Assert.Equal(p, json.Deserialize<PosterDefinition>());
        Assert.Contains("AutoCollapseDelaySeconds", JsonSerializer.Serialize(p)); // storage/export serialization is not fingerprint serialization
    }

    [Theory]
    [InlineData("source")] [InlineData("reads")] [InlineData("geometry")] [InlineData("title")]
    [InlineData("posture")] [InlineData("theme")] [InlineData("wrong-target")]
    public void Setting_command_requires_target_cas_and_rejects_unrelated_edits(string fault)
    {
        using var f = new Fixture(); var s = f.Current;
        var good = (PosterAutoPostureDraft.Open(s.PostersById["A"]) with { Enabled = true }).ToCommand("invalid");
        var bad = fault switch
        {
            "source" => good with { Source = StateCommandSource.Tray },
            "reads" => good with { ReadSet = null },
            "geometry" => good with { DesiredState = good.DesiredState with { FallbackXDip = 100 } },
            "title" => good with { DesiredState = good.DesiredState with { Title = "other" } },
            "posture" => good with { DesiredState = good.DesiredState with { IsCollapsed = false } },
            "theme" => good with { DesiredState = good.DesiredState with { TemplateKey = "night-ops" } },
            _ => good with { PosterId = "missing" }
        };
        Assert.False(f.Coordinator.Execute(bad).Succeeded); Assert.Equal(s.Revision, f.Current.Revision);
    }

    [Fact]
    public void Disabling_retains_saved_delay_and_current_posture()
    {
        using var f = new Fixture(seed: Seed() with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 300 });
        var before = f.Current;
        var result = f.Coordinator.Execute((PosterAutoPostureDraft.Open(before.PostersById["A"]) with { Enabled = false }).ToCommand("disable"));
        Assert.True(result.Succeeded); Assert.Equal(before.PostersById["A"].Poster with { AutoCollapseEnabled = false }, result.Projection!.PostersById["A"].Poster);
        Assert.Equal(0, result.Projection.Tray.AutoPostureEnabledPosterCount);
    }

    [Fact]
    public void Detached_draft_stays_bound_to_original_target_and_other_target_write_does_not_conflict()
    {
        using var f = new Fixture(); string b = f.Add(); var s = f.Current;
        var aDraft = PosterAutoPostureDraft.Open(s.PostersById["A"]) with { Enabled = true, LeaveDelaySeconds = 1 };
        var bDraft = PosterAutoPostureDraft.Open(s.PostersById[b]) with { Enabled = true, LeaveDelaySeconds = 300 };
        Assert.True(f.Coordinator.Execute(bDraft.ToCommand("b")).Succeeded);
        Assert.True(f.Coordinator.Execute(aDraft.ToCommand("a")).Succeeded);
        Assert.Equal(1, f.Current.PostersById["A"].AutoPosture.LeaveDelaySeconds);
        Assert.Equal(300, f.Current.PostersById[b].AutoPosture.LeaveDelaySeconds);
    }

    [Fact]
    public void Two_connections_same_target_conflict_while_nochange_replay_is_idempotent()
    {
        using var f = new Fixture(); var s = f.Current;
        using var other = new SqliteStateStore(f.Paths); Assert.True(other.Initialize().Succeeded);
        var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(other)); coordinator.Refresh();
        var draft = PosterAutoPostureDraft.Open(s.PostersById["A"]);
        var nochange = draft.ToCommand("nochange");
        Assert.Equal(StateCommandStatus.NoChange, f.Coordinator.Execute(nochange).Status);
        Assert.Equal(s.Revision, f.Current.Revision);
        var changed = (draft with { Enabled = true, LeaveDelaySeconds = 9 }).ToCommand("changed");
        Assert.Equal(StateCommandStatus.Committed, coordinator.Execute(changed).Status);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute((draft with { LeaveDelaySeconds = 4 }).ToCommand("stale")).Status);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(nochange).Status);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(changed).Status);
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(changed with { DesiredState = changed.DesiredState with { AutoCollapseDelaySeconds = 10 } }).Status);
        Assert.Equal(s.Revision + 1, f.Current.Revision);
    }

    [Fact]
    public void Receipt_failure_rolls_back_policy_entity_revision_and_all_publications()
    {
        using var f = new Fixture(); var before = f.Current; int events = 0;
        f.Coordinator.ProjectionChanged += (_, _) => events++;
        f.Execute("CREATE TRIGGER fail_policy_receipt BEFORE INSERT ON state_command_receipts BEGIN SELECT RAISE(ABORT,'fixture failure'); END;");
        var command = (PosterAutoPostureDraft.Open(before.PostersById["A"]) with { Enabled = true, LeaveDelaySeconds = 9 }).ToCommand("retry");
        Assert.Equal(StateCommandStatus.Failed, f.Coordinator.Execute(command).Status);
        Assert.Equal(before.PostersById["A"], f.Current.PostersById["A"]); Assert.Equal(0, events);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts;"));
        f.Execute("DROP TRIGGER fail_policy_receipt;");
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(command).Status); Assert.Equal(1, events);
    }

    [Fact]
    public void Clone_copies_policy_while_a_new_default_stays_disabled()
    {
        using var f = new Fixture(seed: Seed() with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 9 });
        var before = f.Current;
        var clone = f.Coordinator.Execute(new ClonePosterCommand("clone", StateCommandSource.ControlCenter, before.Revision,
            StateReadSet.ForPoster("A", before.PostersById["A"].EntityVersion) with { CatalogVersion = before.CatalogVersion }, "A"));
        Assert.True(clone.Succeeded, clone.Error);
        Assert.Equal(before.PostersById["A"].AutoPosture, clone.Projection!.PostersById[clone.ResultPosterId!].AutoPosture);
        Assert.NotEqual(before.PostersById["A"].Poster.ThemeInstanceId, clone.Projection.PostersById[clone.ResultPosterId!].Poster.ThemeInstanceId);
        string created = f.Add(); Assert.Equal(new AutoPosturePolicy(), f.Current.PostersById[created].AutoPosture);
    }

    [Fact]
    public void Deleted_draft_target_does_not_recreate_a_poster_or_mutate_the_empty_catalog()
    {
        using var f = new Fixture();
        var draft = PosterAutoPostureDraft.Open(f.Current.PostersById["A"]) with { Enabled = true };
        var preview = f.Coordinator.PrepareRemoval("A")!;
        Assert.True(f.Coordinator.Execute(new RemovePosterCommand("remove", StateCommandSource.ControlCenter,
            preview.Revision, preview.ReadSet, "A", true)).Succeeded);
        long revision = f.Current.Revision;
        Assert.Equal(StateCommandStatus.NotFound, f.Coordinator.Execute(draft.ToCommand("stale-target")).Status);
        Assert.Empty(f.Current.Posters); Assert.Equal(revision, f.Current.Revision);
    }

    [Fact]
    public void Legacy_catalog_create_clone_and_repository_save_retain_nondefault_policy()
    {
        using var f = new Fixture(); var catalog = new SqlitePosterCatalogRepository(f.Store);
        var create = catalog.Create(f.Current.Revision, "legacy-create", Seed() with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 12 });
        Assert.True(create.Succeeded, create.Error);
        var clone = catalog.CloneConfiguration(create.CurrentRevision, "legacy-clone", create.PosterId!);
        Assert.True(clone.Succeeded, clone.Error);
        var cloned = clone.Snapshot!.Posters.Single(p => p.Poster.PosterId == clone.PosterId).Poster;
        Assert.True(cloned.AutoCollapseEnabled); Assert.Equal(12, cloned.AutoCollapseDelaySeconds);
        var save = new SqlitePosterRepository(f.Store).Save(clone.CurrentRevision, "legacy-save", cloned with { AutoCollapseDelaySeconds = 300 });
        Assert.True(save.Succeeded, save.Error); Assert.Equal(300, new SqlitePosterRepository(f.Store).Read(clone.PosterId!)!.Poster.AutoCollapseDelaySeconds);
    }
}
