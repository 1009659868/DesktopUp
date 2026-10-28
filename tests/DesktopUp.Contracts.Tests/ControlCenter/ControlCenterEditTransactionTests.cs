using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Presentation_apply_is_one_revision_with_reentrant_projection_and_no_geometry_write()
    {
        using var f = new Fixture(); string b = f.Add(); var before = f.Current;
        var e = f.Open(); var p = e.Presentation!;
        Assert.True(e.EditPresentation("Games", "saved together", "night-ops", PosterPresentationPolicy.Defaults("night-ops")));
        f.Coordinator.ProjectionChanged += (_, a) => e.Synchronize(a.Projection, "A");
        var command = e.BeginPresentationApply()!;
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.True(e.IsPending);
        Assert.True(e.Complete(result)); Assert.False(e.IsDirty); Assert.False(e.IsPending);
        Assert.Equal(before.Revision + 1, result.CommittedRevision);
        Assert.Equal(result.Projection!.Revision, result.Projection.ControlCenter.Revision);
        Assert.Equal(result.Projection.Revision, result.Projection.Tray.Revision);
        var saved = f.Current.PostersById["A"].Poster;
        Assert.Equal("Games", saved.Title); Assert.Equal("saved together", saved.Subtitle);
        Assert.Equal("night-ops", saved.TemplateKey);
        Assert.True(EffectivePosterLayout.SameGeometry(before.PostersById["A"].Poster, saved));
        Assert.Equal(before.PostersById[b].Poster, f.Current.PostersById[b].Poster);
        Assert.Equal(before.PostersById[b].EntityVersion, f.Current.PostersById[b].EntityVersion);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(before.Revision + 1, f.Current.Revision);
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(f.Store)).Refresh();
        Assert.Equal(saved, reopened.PostersById["A"].Poster);
    }

    [Fact]
    public void Unrelated_poster_and_settings_changes_do_not_conflict_with_presentation()
    {
        using var f = new Fixture(); string b = f.Add(); var e = f.Open(); Title(e, "A draft");
        f.Rename(b, "B new");
        var settings = f.Current.ControlCenter.Settings;
        Assert.True(f.Coordinator.Execute(new SettingsStateCommand("settings", StateCommandSource.ControlCenter,
            SettingsStateCommandKind.SetDynamicEffectsPaused, f.Current.Revision, settings.Settings with { DynamicEffectsPaused = true })
            { ReadSet = StateReadSet.ForSettings(settings.EntityVersion) }).Succeeded);
        e.Synchronize(f.Current, "A"); Assert.False(e.IsStale);
        var r = f.Coordinator.Execute(e.BeginPresentationApply()!); Assert.True(r.Succeeded, r.Error);
        e.Complete(r); Assert.Equal("A draft", f.Current.PostersById["A"].Poster.Title);
        Assert.Equal("B new", f.Current.PostersById[b].Poster.Title);
    }

    [Theory]
    [InlineData(PosterStateCommandKind.Rename)] [InlineData(PosterStateCommandKind.SetLocked)]
    [InlineData(PosterStateCommandKind.SetVisible)]
    public void Same_target_immediate_action_conflicts_without_erasing_the_draft(PosterStateCommandKind kind)
    {
        using var f = new Fixture(); var e = f.Open(); Title(e, "unapplied");
        Assert.True(e.CanRunImmediateAction);
        var p = f.Current.PostersById["A"];
        var desired = kind switch
        {
            PosterStateCommandKind.Rename => p.Poster with { Title = "external" },
            PosterStateCommandKind.SetLocked => p.Poster with { IsLocked = true },
            _ => p.Poster with { IsVisible = false }
        };
        Assert.True(f.Coordinator.Execute(new PosterStateCommand("immediate", StateCommandSource.ControlCenter,
            kind, f.Current.Revision, "A", desired) { ReadSet = StateReadSet.ForPoster("A", p.EntityVersion) }).Succeeded);
        e.Synchronize(f.Current, "A"); long before = f.Current.Revision;
        var r = f.Coordinator.Execute(e.BeginPresentationApply()!);
        Assert.Equal(StateCommandStatus.Conflict, r.Status); e.Complete(r);
        Assert.Equal(before, f.Current.Revision); Assert.Equal("unapplied", e.Presentation!.Title);
        e.Cancel(); Assert.Equal(desired.Title, e.Presentation!.Title); Assert.False(e.IsDirty);
    }

    [Fact]
    public void Nochange_result_releases_pending_without_new_revision()
    {
        using var f = new Fixture(); var e = f.Open(); long before = f.Current.Revision;
        e.EditGeometry(e.Geometry! with { X = "00" });
        var request = e.BeginGeometryApply()!;
        var nochange = f.Coordinator.Execute(PrepareMoveForTest(request, f.Current));
        Assert.Equal(StateCommandStatus.NoChange, nochange.Status);
        Assert.True(e.Complete(nochange)); Assert.False(e.IsPending); Assert.False(e.IsDirty);
        Assert.Equal("0", e.Geometry!.X); Assert.Equal(before, f.Current.Revision);
    }

    [Fact]
    public void Sql_failure_has_no_partial_fields_revision_or_receipt_and_keeps_draft()
    {
        using var f = new Fixture(); var e = f.Open(); Title(e, "must not save"); var before = f.Current;
        f.Sql("CREATE TRIGGER fail_edit BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected'); END;");
        var command = e.BeginPresentationApply()!; var r = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Failed, r.Status); e.Complete(r);
        Assert.True(e.IsDirty); Assert.NotNull(e.Error);
        Assert.Equal(before.Revision, f.Store.ReadHead().Revision);
        Assert.Equal(before.PostersById["A"].Poster, f.Coordinator.Refresh().PostersById["A"].Poster);
        using var connection = f.Connection(); using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM state_command_receipts WHERE request_id=$id";
        query.Parameters.AddWithValue("$id", command.RequestId);
        Assert.Equal(0L, query.ExecuteScalar());
    }

    [Fact]
    public void Delete_without_separate_ui_event_invalidates_target_from_failed_result()
    {
        using var f = new Fixture(); var e = f.Open(); Title(e, "old"); var c = e.BeginPresentationApply()!;
        f.Remove("A"); var r = f.Coordinator.Execute(c); Assert.Equal(StateCommandStatus.NotFound, r.Status);
        e.Complete(r);
        Assert.Null(e.PosterId); Assert.False(e.CanApply); Assert.False(e.ProtectsInteraction);
        Assert.Empty(f.Current.Posters); Assert.Empty(f.Coordinator.EnsureInitialized(Seed()).Posters);
    }

    [Fact]
    public void Switch_or_close_does_not_cancel_an_already_dispatched_original_target_command()
    {
        using var f = new Fixture(); string b = f.Add(); var e = f.Open(); Title(e, "A applied");
        var command = e.BeginPresentationApply()!;
        e.Synchronize(f.Current, b); Title(e, "B unsaved"); e.Close();
        var result = f.Coordinator.Execute(command); Assert.True(result.Succeeded);
        Assert.False(e.Complete(result)); Assert.Equal("A applied", f.Current.PostersById["A"].Poster.Title);
        Assert.Equal("B", f.Current.PostersById[b].Poster.Title);
    }

    [Fact]
    public void Pure_geometry_request_can_use_existing_space_proof_and_single_transaction()
    {
        using var f = new Fixture(); string b = f.Add(); var e = f.Open();
        e.EditGeometry(e.Geometry! with { X = "40", Y = "50" });
        var request = e.BeginGeometryApply()!; long before = f.Current.Revision;
        Assert.True(request.ReadsCurrent(f.Current));
        var command = PrepareMoveForTest(request, f.Current);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); e.Complete(result);
        Assert.Equal(before + 1, result.CommittedRevision); Assert.Equal("40", e.Geometry!.X);
        Assert.Equal(600, f.Current.PostersById["A"].Poster.ExpandedHeightDip);
        Assert.Equal(500, f.Current.PostersById[b].Poster.FallbackXDip);
    }

    [Fact]
    public void Geometry_obstacle_change_after_preparation_fails_full_cas_without_moving_source()
    {
        using var f = new Fixture(); string b = f.Add(); var e = f.Open();
        e.EditGeometry(e.Geometry! with { X = "40" });
        var request = e.BeginGeometryApply()!; var command = PrepareMoveForTest(request, f.Current);
        f.Rename(b, "obstacle changed"); long before = f.Current.Revision;
        Assert.False(request.ReadsCurrent(f.Current));
        var result = f.Coordinator.Execute(command); Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Contains(result.Conflicts, c => c.Id == b);
        e.Complete(result); Assert.True(e.IsDirty); Assert.NotNull(e.Error);
        Assert.Equal(before, f.Current.Revision); Assert.Equal(0, f.Current.PostersById["A"].Poster.FallbackXDip);
    }

    [Fact]
    public void Geometry_submission_failure_keeps_all_four_fields_uncommitted()
    {
        using var f = new Fixture(); var e = f.Open(); var before = f.Current.Revision;
        e.EditGeometry(new("12", "23", "345", "650")); var request = e.BeginGeometryApply()!;
        e.Complete(new(StateCommandStatus.InsufficientSpace, request.RequestId, request.ObservedRevision,
            f.Current.Revision, f.Current, "no complete placement"));
        Assert.Equal(before, f.Current.Revision); Assert.Equal("12", e.Geometry!.X);
        Assert.Equal("650", e.Geometry.ExpandedHeight); Assert.NotNull(e.Error);
        e.Cancel(); Assert.Equal("0", e.Geometry.X); Assert.Equal("600", e.Geometry.ExpandedHeight);
    }

    // Pure bridge fixture: no HWND/animation acceptance is claimed. Production adapter is 04-04-03.
    private static PosterStateCommand PrepareMoveForTest(ControlCenterGeometryRequest request, DesktopUpProjection projection)
    {
        var environment = new SpatialEnvironment("display", new(0, 0, 1000, 1000), 96, 96, 1, 1, Guid.NewGuid());
        var scene = new SpatialSnapshot(environment, projection.Posters.Select(p => new SpatialPoster(p.Poster.PosterId,
            new((int)p.Poster.FallbackXDip, (int)p.Poster.FallbackYDip, (int)p.Poster.WidthDip, (int)p.Poster.ExpandedHeightDip),
            (int)p.Poster.ExpandedHeightDip, p.Poster.IsVisible, p.Poster.IsLocked, p.Poster.IsCollapsed, p.LayoutGeneration)));
        var session = new SpatialGestureSession("A", scene, projection, PosterGestureKind.Move, PosterOperatingMode.Editing, 240, 240);
        var plan = session.Preview(new((int)request.Desired.X, (int)request.Desired.Y, 300, 600));
        Assert.True(plan.Succeeded); Assert.True(session.Accept(plan));
        var desired = SpatialGestureCommitPolicy.Capture(request.Basis.Poster, session.Presented, environment, request.Desired.ExpandedHeight);
        return new(request.RequestId, StateCommandSource.ControlCenter, PosterStateCommandKind.Move, request.ObservedRevision,
            request.Basis.Poster.PosterId, desired) { ReadSet = request.ReadSet, SpatialGesture = session.Freeze() };
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _parent = Path.Combine(Path.GetTempPath(), "DesktopUp.CC01.Tests");
        private readonly string _root;
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current => Coordinator.TryGetCurrent(out var p) ? p! : throw new InvalidOperationException();
        public Fixture()
        {
            _root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
            Paths = new(_root); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store)); Coordinator.EnsureInitialized(Seed());
        }
        public ControlCenterPosterEditor Open() { var e = new ControlCenterPosterEditor(); e.Synchronize(Current, "A"); return e; }
        public string Add()
        {
            var r = Coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), Seed("B") with { FallbackXDip = 500 }));
            Assert.True(r.Succeeded); return r.ResultPosterId!;
        }
        public void Rename(string id, string title)
        {
            var p = Current.PostersById[id]; Assert.True(Coordinator.Execute(new PosterStateCommand(Guid.NewGuid().ToString("N"),
                StateCommandSource.ControlCenter, PosterStateCommandKind.Rename, Current.Revision, id, p.Poster with { Title = title })
                { ReadSet = StateReadSet.ForPoster(id, p.EntityVersion) }).Succeeded);
        }
        public void Remove(string id)
        {
            var p = Current.PostersById[id];
            Assert.True(Coordinator.Execute(new RemovePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForPoster(id, p.EntityVersion) with { CatalogVersion = Current.CatalogVersion }, id, true)).Succeeded);
        }
        public SqliteConnection Connection()
        {
            var c = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(Paths.State, "desktopup.sqlite3"), Pooling = false }.ToString()); c.Open(); return c;
        }
        public void Sql(string sql) { using var c = Connection(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            var resolved = Path.GetFullPath(_root);
            if (Path.GetDirectoryName(resolved) != Path.GetFullPath(_parent) ||
                !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _)) throw new InvalidOperationException("Unsafe fixture cleanup.");
            Directory.Delete(resolved, true);
        }
    }
}
