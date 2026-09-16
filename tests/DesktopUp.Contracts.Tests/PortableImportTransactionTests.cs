using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class PortableImportTransactionTests
{
    [Fact]
    public void Append_is_one_revision_preserves_existing_entities_and_publishes_one_projection()
    {
        using var f = new Fixture();
        var before = f.Repository.Read();
        var notices = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, change) => notices.Add(change);
        var plan = f.Plan(2, PortableImportMode.Append);
        var result = f.Coordinator.ExecutePortable(new("append", plan));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(before.Revision + 1, result.CommittedRevision);
        Assert.Single(notices);
        Assert.Equal(3, result.Projection!.Posters.Count);
        var original = result.Projection.PostersById[Fixture.Original];
        Assert.Equal(before.Catalog.Posters[0].Poster, original.Poster);
        Assert.Equal(before.Catalog.Posters[0].EntityVersion, original.EntityVersion);
        Assert.Equal(before.Settings.Settings, result.Projection.ControlCenter.Settings.Settings);
        Assert.Equal(before.Settings.EntityVersion, result.Projection.ControlCenter.Settings.EntityVersion);
        Assert.All(plan.Creates, p => Assert.Equal(p, result.Projection.PostersById[p.PosterId].Poster));
        Assert.Equal(result.Projection.Revision, result.Projection.Tray.Revision);
        Assert.All(result.Projection.Posters, p => Assert.Equal(result.Projection.Revision, p.Revision));
    }

    [Fact]
    public void Replace_is_one_revision_replaces_ids_and_keeps_local_startup_intent()
    {
        using var f = new Fixture();
        var settings = f.Repository.Read().Settings;
        Assert.True(new SqliteSettingsRepository(f.Store).Save(settings.Revision, "startup-local",
            settings.Settings with { LaunchAtStartupRequested = true }).Succeeded);
        f.Coordinator.Refresh();
        var before = f.Repository.Read();
        var plan = f.Plan(3, PortableImportMode.Replace, importSettings: true);
        var result = f.Coordinator.ExecutePortable(new("replace", plan));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(before.Revision + 1, result.CommittedRevision);
        Assert.Equal(3, result.Projection!.Posters.Count);
        Assert.DoesNotContain(Fixture.Original, result.Projection.PostersById.Keys);
        Assert.True(result.Projection.ControlCenter.Settings.Settings.DynamicEffectsPaused);
        Assert.Equal(DesktopUpPerformanceMode.Static, result.Projection.ControlCenter.Settings.Settings.PerformanceMode);
        Assert.True(result.Projection.ControlCenter.Settings.Settings.LaunchAtStartupRequested);
        Assert.Equal(3, result.Projection.Posters.Select(p => p.Poster.PosterId).Distinct().Count());
        Assert.Equal(3, result.Projection.Posters.Select(p => p.Poster.ThemeInstanceId).Distinct().Count());
        Assert.All(result.Projection.Posters, p => Assert.Null(p.Poster.CollectionId));
    }

    [Fact]
    public void Replace_empty_preserves_initialized_zero_on_restart_without_extra_default()
    {
        using var f = new Fixture();
        long before = f.Repository.Read().Revision;
        var result = f.Coordinator.ExecutePortable(new("empty", f.Plan(0, PortableImportMode.Replace)));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(before + 1, result.CommittedRevision);
        Assert.True(result.Projection!.ProfileInitialized);
        Assert.Empty(result.Projection.Posters);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var projection = new StateProjectionCoordinator(new SqliteStateCommandRepository(reopened))
            .EnsureInitialized(Fixture.Seed());
        Assert.True(projection.ProfileInitialized);
        Assert.Empty(projection.Posters);
        Assert.Equal(before + 1, projection.Revision);
    }

    [Theory]
    [InlineData("poster")] [InlineData("catalog")] [InlineData("settings")]
    public void Preview_stales_on_every_authoritative_read_scope_without_partial_import(string changed)
    {
        using var f = new Fixture();
        var plan = f.Plan(2, PortableImportMode.Append);
        var current = f.Coordinator.Refresh();
        StateCommandResult mutation = changed switch
        {
            "poster" => f.Coordinator.Execute(new PosterStateCommand("rename", StateCommandSource.ControlCenter,
                PosterStateCommandKind.Rename, current.Revision, Fixture.Original,
                current.PostersById[Fixture.Original].Poster with { Title = "User intervened" })
                { ReadSet = StateReadSet.ForPoster(Fixture.Original, current.PostersById[Fixture.Original].EntityVersion) }),
            "catalog" => f.Coordinator.Execute(new CreatePosterCommand("create-neighbor", StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), Fixture.Seed() with { Title = "New neighbor" })),
            _ => f.Coordinator.Execute(new SettingsStateCommand("pause", StateCommandSource.ControlCenter,
                SettingsStateCommandKind.SetDynamicEffectsPaused, current.Revision,
                current.ControlCenter.Settings.Settings with { DynamicEffectsPaused = true })
                { ReadSet = StateReadSet.ForSettings(current.ControlCenter.Settings.EntityVersion) })
        };
        Assert.True(mutation.Succeeded, mutation.Error);
        var before = f.Repository.Read();
        var result = f.Coordinator.ExecutePortable(new("stale", plan));
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal(before.Revision, f.Repository.Read().Revision);
        Assert.Equal(before.Catalog.Posters.Select(p => p.Poster), f.Repository.Read().Catalog.Posters.Select(p => p.Poster));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts WHERE request_id='stale';"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_or_expired_runtime_guard_rejects_without_writes(bool bind)
    {
        using var f = new Fixture(bindGuard: bind);
        f.GuardAllows = false;
        var before = f.Repository.Read();
        var result = f.Coordinator.ExecutePortable(new("guard", f.Plan(1, PortableImportMode.Replace)));
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal(before.Revision, f.Repository.Read().Revision);
        Assert.Single(f.Repository.Read().Catalog.Posters);
    }

    [Fact]
    public void Replay_is_exact_and_does_not_duplicate_even_after_runtime_environment_changes()
    {
        using var f = new Fixture();
        var command = new PortableImportCommand("repeat", f.Plan(2, PortableImportMode.Append));
        var first = f.Coordinator.ExecutePortable(command);
        Assert.True(first.Succeeded, first.Error);
        f.GuardAllows = false;
        var replay = f.Coordinator.ExecutePortable(command);
        Assert.Equal(StateCommandStatus.Replayed, replay.Status);
        Assert.Equal(first.CommittedRevision, replay.CommittedRevision);
        Assert.Equal(3, replay.Projection!.Posters.Count);
        Assert.Equal(first.CurrentRevision, replay.CurrentRevision);
        var changed = command with { Plan = command.Plan with { ImportSettings = true } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.ExecutePortable(changed).Status);
        Assert.Equal(first.CurrentRevision, f.Repository.Read().Revision);
    }

    [Theory]
    [InlineData("geometry")] [InlineData("identity-collision")] [InlineData("removal")]
    [InlineData("settings")] [InlineData("pending")]
    public void Tampered_plan_is_rejected_before_any_write(string tamper)
    {
        using var f = new Fixture();
        var plan = f.Plan(2, PortableImportMode.Append);
        plan = tamper switch
        {
            "geometry" => plan with { Creates = plan.Creates.Select((p, i) => i == 0 ? p with { WidthDip = 500 } : p).ToArray() },
            "identity-collision" => plan with { Creates = plan.Creates.Select((p, i) => i == 0 ? p with { PosterId = Fixture.Original } : p).ToArray() },
            "removal" => plan with { Removes = new[] { Fixture.Original } },
            "settings" => plan with { Settings = new(true, DesktopUpPerformanceMode.Static, true) },
            _ => plan with { PendingPosterIds = new[] { plan.Creates[0].PosterId } }
        };
        var before = f.Repository.Read();
        var result = f.Coordinator.ExecutePortable(new("tamper", plan));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(before.Revision, f.Repository.Read().Revision);
        Assert.Single(f.Repository.Read().Catalog.Posters);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts WHERE request_id='tamper';"));
    }

    [Theory]
    [InlineData("second-insert")] [InlineData("remove-original")] [InlineData("settings")]
    [InlineData("receipt")]
    public void SQL_fault_at_each_stage_rolls_back_all_rows_head_profile_and_receipt(string stage)
    {
        using var f = new Fixture();
        var before = f.Repository.Read();
        string body = stage switch
        {
            "second-insert" => "BEFORE INSERT ON posters WHEN NEW.title='Imported 2'",
            "remove-original" => "BEFORE DELETE ON posters",
            "settings" => "BEFORE UPDATE ON app_settings",
            _ => "BEFORE INSERT ON state_command_receipts WHEN NEW.request_id='fault'"
        };
        f.ExecuteSql("CREATE TRIGGER portable_fault " + body + " BEGIN SELECT RAISE(ABORT,'portable injected failure'); END;");
        var result = f.Coordinator.ExecutePortable(new("fault", f.Plan(3, PortableImportMode.Replace, importSettings: true)));
        Assert.Equal(StateCommandStatus.Failed, result.Status);
        var after = f.Repository.Read();
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Catalog.CatalogVersion, after.Catalog.CatalogVersion);
        Assert.Equal(before.Catalog.ProfileInitialized, after.Catalog.ProfileInitialized);
        Assert.Equal(before.Catalog.Posters.Select(p => p.Poster), after.Catalog.Posters.Select(p => p.Poster));
        Assert.Equal(before.Settings, after.Settings);
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts WHERE request_id='fault';"));
        Assert.Equal(0L, f.Scalar("SELECT COUNT(*) FROM state_operations WHERE operation_id='fault';"));
    }

    [Fact]
    public void Insufficient_space_commits_all_configuration_as_pending_then_retry_places_without_new_ids()
    {
        using var f = new Fixture();
        var small = Fixture.Topology(300, 360);
        var plan = f.Plan(2, PortableImportMode.Append, topology: small);
        Assert.Equal(2, plan.PendingPosterIds.Count);
        Assert.All(plan.Creates, p => { Assert.True(p.PlacementPending); Assert.True(p.IsVisible); });
        var first = f.Coordinator.ExecutePortable(new("pending", plan));
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(3, first.Projection!.Posters.Count);
        Assert.Equal(2, first.Projection.Tray.PendingPlacementPosterCount);
        Assert.Equal(1, first.Projection.Tray.VisiblePosterCount);
        var beforeRetry = f.Repository.Read();
        var noSpace = PortableProfilePlanner.TryPlacePending(beforeRetry, small);
        var noChange = f.Coordinator.ExecutePortable(new("retry-no-space", noSpace));
        Assert.Equal(StateCommandStatus.NoChange, noChange.Status);
        Assert.Equal(beforeRetry.Revision, noChange.CurrentRevision);
        var retry = PortableProfilePlanner.TryPlacePending(f.Repository.Read(), Fixture.Topology());
        var placed = f.Coordinator.ExecutePortable(new("retry-space", retry));
        Assert.Equal(StateCommandStatus.Committed, placed.Status);
        Assert.Equal(beforeRetry.Revision + 1, placed.CommittedRevision);
        Assert.Equal(0, placed.Projection!.Tray.PendingPlacementPosterCount);
        Assert.Equal(3, placed.Projection.Tray.VisiblePosterCount);
        Assert.Equal(first.Projection.PostersById.Keys.Order(), placed.Projection.PostersById.Keys.Order());
        Assert.Equal(beforeRetry.Catalog.Posters.Single(p => p.Poster.PosterId == Fixture.Original).Poster,
            placed.Projection.PostersById[Fixture.Original].Poster);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Original = "poster-original";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.Portable.Transactions.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; }
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public bool GuardAllows { get; set; } = true;
        public Fixture(bool bindGuard = true)
        {
            Paths = new(_root);
            Store = new(Paths);
            Assert.True(Store.Initialize().Succeeded);
            Repository = new(Store);
            Coordinator = new(Repository);
            Coordinator.EnsureInitialized(Seed());
            if (bindGuard) Coordinator.BindPortableRuntime(_ => GuardAllows);
        }
        public static PosterDefinition Seed() => PosterConfigurationFactory.NewBasicBox("Original", "DISPLAY1") with
        { PosterId = Original, ThemeInstanceId = "theme-original", HorizontalAnchor = 0, VerticalAnchor = 0 };
        public static DisplayTopologySnapshot Topology(int width = 1920, int height = 1080) =>
            new("display-" + width + "-" + height, new[] { new DisplayMonitorSnapshot("DISPLAY1", true,
                new(0, 0, width, height), new(0, 0, width, height), 96, 96) }, 1, DateTimeOffset.UnixEpoch);
        public PortableImportPlan Plan(int count, PortableImportMode mode, bool importSettings = false,
            DisplayTopologySnapshot? topology = null)
        {
            var template = PortableProfileCodec.Parse(PortableProfileCodec.Export(Repository.Read()));
            var poster = template.Posters.Single();
            var document = template with
            {
                Posters = Enumerable.Range(1, count).Select(i => poster with { Key = "p" + i,
                    Title = "Imported " + i, StackOrder = i - 1 }).ToArray(),
                Settings = new(true, DesktopUpPerformanceMode.Static)
            };
            int id = 0;
            return PortableProfilePlanner.Plan(document, Repository.Read(), topology ?? Topology(), mode, importSettings,
                () => "imported-" + ++id);
        }
        public void ExecuteSql(string sql) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        public object? Scalar(string sql) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
        private SqliteConnection Open()
        {
            var c = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(Paths.State, "desktopup.sqlite3"), Pooling = false }.ToString());
            c.Open(); return c;
        }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            Directory.Delete(_root, true); // Only this fixture's allocated temporary tree.
        }
    }
}
