using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ScopedStateCommandTests
{
    [Fact]
    public void UnrelatedPosterAndSettingsWritesDoNotInvalidateAnEntityCommand()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        DesktopUpProjection baseline = f.Current;
        PosterStateCommand aCommand = Lock(baseline, Main);
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(Lock(baseline, b)).Status);
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(Pause(baseline)).Status);
        StateCommandResult result = f.Coordinator.Execute(aCommand);
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(baseline.Revision + 3, result.CommittedRevision);
        Assert.True(result.Projection!.PostersById[Main].Poster.IsLocked);
        Assert.True(result.Projection.PostersById[b].Poster.IsLocked);
        Assert.True(result.Projection.ControlCenter.Settings.Settings.DynamicEffectsPaused);
        Assert.Equal(2, result.Projection.PostersById[Main].EntityVersion);
        Assert.Equal(2, result.Projection.ControlCenter.Settings.EntityVersion);
    }

    [Fact]
    public void SameEntityStaleWriteReportsTheConflictingTarget()
    {
        using var f = new Fixture();
        DesktopUpProjection initial = f.Current;
        f.Coordinator.Execute(Lock(initial, Main));
        var stale = Lock(initial, Main) with { Kind = PosterStateCommandKind.Rename,
            DesiredState = initial.PostersById[Main].Poster with { Title = "stale" } };
        StateCommandResult result = f.Coordinator.Execute(stale);
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        StateReadConflict conflict = Assert.Single(result.Conflicts);
        Assert.Equal(Main, conflict.Id);
        Assert.Equal(1, conflict.Expected);
        Assert.Equal(2, conflict.Actual);
        Assert.Equal("NIGHT OPS", f.Current.PostersById[Main].Poster.Title);
        Assert.Equal(2, f.Current.Revision);
    }

    [Fact]
    public void AdditionalReadDependenciesAreCheckedEvenWhenNotWritten()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        DesktopUpProjection initial = f.Current;
        var command = Lock(initial, Main) with { ReadSet = Reads(initial, Main, b) with { SettingsVersion = initial.ControlCenter.Settings.EntityVersion } };
        f.Coordinator.Execute(Lock(initial, b));
        f.Coordinator.Execute(Pause(initial));
        StateCommandResult result = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal(2, result.Conflicts.Count);
        Assert.False(f.Current.PostersById[Main].Poster.IsLocked);
    }

    [Fact]
    public void BatchCommitsOnceAndPublishesOnlyChangedIdsWithOneRevision()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        string c = f.Add("C");
        DesktopUpProjection initial = f.Current;
        var events = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, e) => events.Add(e);
        StateCommandResult result = f.Coordinator.Execute(Batch(initial, new[] { Main, b }, BatchPosterCommandKind.SetLocked, true));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(initial.Revision + 1, result.CommittedRevision);
        StateProjectionChangedEventArgs change = Assert.Single(events);
        Assert.Equal(new[] { Main, b }.Order(), change.ChangedPosterIds);
        Assert.False(change.CatalogChanged);
        Assert.False(change.SettingsChanged);
        DesktopUpProjection p = change.Projection;
        Assert.All(p.Posters, target => Assert.Equal(p.Revision, target.Revision));
        Assert.Equal(p.Revision, p.ControlCenter.Revision);
        Assert.Equal(p.Revision, p.ControlCenter.Settings.Revision);
        Assert.Equal(p.Revision, p.Tray.Revision);
        Assert.Equal(3, p.Tray.PosterCount);
        Assert.Equal(3, p.Tray.VisiblePosterCount);
        Assert.Equal(2, p.Tray.LockedPosterCount);
        Assert.Equal(PosterLockSummary.Some, p.Tray.LockSummary);
        Assert.False(p.Tray.AllPostersLocked);
        Assert.Equal(1, p.PostersById[c].EntityVersion);
        Assert.Equal(initial.PostersById[c].PosterRevision, p.PostersById[c].PosterRevision);
    }

    [Fact]
    public void BatchConflictReportsAllTargetsWithoutPartialWrite()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        DesktopUpProjection initial = f.Current;
        f.Coordinator.Execute(Lock(initial, b));
        var reads = Reads(initial, Main, b) with { Posters = new Dictionary<string, long> { [Main] = 1, [b] = 1, ["gone"] = 1 } };
        var batch = Batch(initial, new[] { Main, b }, BatchPosterCommandKind.SetVisible, false) with { ReadSet = reads, PosterIds = new[] { Main, b, "gone" } };
        long revision = f.Current.Revision;
        StateCommandResult result = f.Coordinator.Execute(batch);
        Assert.Equal(StateCommandStatus.NotFound, result.Status);
        Assert.Equal(2, result.Conflicts.Count);
        Assert.All(f.Current.Posters, p => Assert.True(p.Poster.IsVisible));
        Assert.Equal(revision, f.Current.Revision);
    }

    [Fact]
    public void SqlFailureAfterFirstBatchUpdateRollsBackHeadEntitiesAndReceipt()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        DesktopUpProjection initial = f.Current;
        string lastTarget = new[] { Main, b }.Order(StringComparer.Ordinal).Last();
        f.ExecuteSql($"CREATE TRIGGER fail_batch BEFORE UPDATE ON posters WHEN OLD.poster_id='{lastTarget}' BEGIN SELECT RAISE(ABORT,'injected'); END;");
        var command = Batch(initial, new[] { Main, b }, BatchPosterCommandKind.SetLocked, true);
        StateCommandResult result = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Failed, result.Status);
        Assert.Equal(initial, f.Current);
        Assert.Equal(initial.Revision, f.Store.ReadHead().Revision);
        Assert.All(f.Repository.Read().Catalog.Posters, p => { Assert.False(p.Poster.IsLocked); Assert.Equal(1, p.EntityVersion); });
        Assert.Equal(0L, f.Scalar($"SELECT COUNT(*) FROM state_command_receipts WHERE request_id='{command.RequestId}';"));
    }

    [Fact]
    public void AllTargetsRequiresCatalogVersionAndRejectsMembershipChanges()
    {
        using var f = new Fixture();
        DesktopUpProjection initial = f.Current;
        var command = Batch(initial, new[] { Main }, BatchPosterCommandKind.SetLocked, true, all: true);
        f.Add("B");
        StateCommandResult result = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal("catalog", Assert.Single(result.Conflicts).Kind);
        var unprotected = Batch(f.Current, new[] { Main }, BatchPosterCommandKind.SetLocked, true) with { AllPosters = true };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(unprotected).Status);
        Assert.All(f.Current.Posters, p => Assert.False(p.Poster.IsLocked));
    }

    [Fact]
    public void ExplicitSubsetDoesNotConflictWithUnrelatedCatalogCreation()
    {
        using var f = new Fixture();
        DesktopUpProjection initial = f.Current;
        var command = Batch(initial, new[] { Main }, BatchPosterCommandKind.SetVisible, false);
        string b = f.Add("B");
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(command).Status);
        Assert.False(f.Current.PostersById[Main].Poster.IsVisible);
        Assert.True(f.Current.PostersById[b].Poster.IsVisible);
        Assert.Equal(1, f.Current.Tray.VisiblePosterCount);
    }

    [Fact]
    public void SettingsOnlyChangeHasAnEmptyPosterDelta()
    {
        using var f = new Fixture();
        var events = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, e) => events.Add(e);
        f.Coordinator.Execute(Pause(f.Current));
        StateProjectionChangedEventArgs change = Assert.Single(events);
        Assert.Empty(change.ChangedPosterIds);
        Assert.True(change.SettingsChanged);
        Assert.False(change.CatalogChanged);
    }

    [Fact]
    public void MissingRequiredReadSetIsRejectedEvenForNoChange()
    {
        using var f = new Fixture();
        var missing = Lock(f.Current, Main) with { DesiredState = f.Current.PostersById[Main].Poster,
            ReadSet = StateReadSet.ForCatalog(f.Current.CatalogVersion) };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(missing).Status);
        Assert.Equal(1, f.Current.Revision);
    }

    [Fact]
    public void TargetCannotBeSubstitutedThroughDesiredState()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        var command = Lock(f.Current, Main) with { DesiredState = f.Current.PostersById[b].Poster with { IsLocked = true } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command).Status);
        Assert.All(f.Current.Posters, p => Assert.False(p.Poster.IsLocked));
    }

    [Fact]
    public void RequestIdPayloadMismatchIsRejectedIncludingSourceAndTarget()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        DesktopUpProjection initial = f.Current;
        var command = Lock(initial, Main);
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(command).Status);
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command with { Source = StateCommandSource.Poster }).Status);
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(Lock(initial, b) with { RequestId = command.RequestId }).Status);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(initial.Revision + 1, f.Current.Revision);
    }

    [Fact]
    public void CommittedAndNoChangeReceiptsSurviveCoordinatorRestart()
    {
        using var f = new Fixture();
        var command = Lock(f.Current, Main);
        StateCommandResult committed = f.Coordinator.Execute(command);
        var noChange = Lock(f.Current, Main);
        Assert.Equal(StateCommandStatus.NoChange, f.Coordinator.Execute(noChange).Status);
        using var secondStore = f.Open();
        var second = new StateProjectionCoordinator(new SqliteStateCommandRepository(secondStore));
        second.EnsureInitialized(Configuration());
        StateCommandResult replay = second.Execute(command);
        Assert.Equal(StateCommandStatus.Replayed, replay.Status);
        Assert.Equal(committed.CommittedRevision, replay.CommittedRevision);
        StateCommandResult noChangeReplay = second.Execute(noChange);
        Assert.Equal(StateCommandStatus.Replayed, noChangeReplay.Status);
        Assert.Null(noChangeReplay.CommittedRevision);
        Assert.Equal(2L, f.Scalar("SELECT COUNT(*) FROM state_command_receipts;"));
        Assert.Equal(2, secondStore.ReadHead().Revision);
    }

    [Fact]
    public void CancellationAndStaleConfirmationCannotRemoveTheTarget()
    {
        using var f = new Fixture();
        PosterRemovalPreview preview = f.Coordinator.PrepareRemoval(Main)!;
        Assert.True(preview.IsLastPoster);
        Assert.Contains("返回收纳前的目录", preview.Consequences);
        Assert.Contains("返回系统桌面", preview.Consequences);
        Assert.Contains("取消或归还失败时保留海报和剩余内容", preview.Consequences);
        var remove = new RemovePosterCommand(Id(), StateCommandSource.ControlCenter, preview.Revision, preview.ReadSet, preview.PosterId, false);
        Assert.Equal(StateCommandStatus.ConfirmationRequired, f.Coordinator.Execute(remove).Status);
        Assert.Equal(1, f.Current.Revision);
        f.Coordinator.Execute(Lock(f.Current, Main));
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(remove with { Confirmed = true }).Status);
        Assert.Single(f.Current.Posters);
    }

    [Fact]
    public void DeleteLastDoesNotReseedAndLateCommandsCannotResurrect()
    {
        using var f = new Fixture();
        var late = Lock(f.Current, Main);
        StateCommandResult removed = f.Remove(Main);
        Assert.Equal(StateCommandStatus.Committed, removed.Status);
        Assert.Empty(f.Current.Posters);
        Assert.Null(f.Current.Poster);
        Assert.Null(f.Current.ControlCenter.Poster);
        Assert.Equal(PosterLockSummary.Empty, f.Current.Tray.LockSummary);
        Assert.False(f.Current.Tray.AllPostersLocked);
        Assert.Equal(StateCommandStatus.NotFound, f.Coordinator.Execute(late).Status);
        using var otherStore = f.Open();
        var restarted = new StateProjectionCoordinator(new SqliteStateCommandRepository(otherStore));
        Assert.Empty(restarted.EnsureInitialized(Configuration()).Posters);
        Assert.Equal(2, otherStore.ReadHead().Revision);
        Assert.Equal(StateCommandStatus.Committed, restarted.Execute(Pause(restarted.Refresh())).Status);
        Assert.Empty(restarted.Refresh().Posters);
    }

    [Fact]
    public void CreateAndCloneAllocateIndependentIdentitiesAndReplaysNeverRecreateDeletedEntity()
    {
        using var f = new Fixture();
        DesktopUpProjection initial = f.Current;
        var create = new CreatePosterCommand(Id(), StateCommandSource.Tray, initial.Revision,
            StateReadSet.ForCatalog(initial.CatalogVersion), Configuration() with { Title = "Work", CollectionId = "do-not-share" });
        StateCommandResult created = f.Coordinator.Execute(create);
        Assert.Equal(StateCommandStatus.Committed, created.Status);
        string id = created.ResultPosterId!;
        Assert.NotEqual(Main, id);
        Assert.Null(f.Current.PostersById[id].Poster.CollectionId);
        var clone = new ClonePosterCommand(Id(), StateCommandSource.ControlCenter, f.Current.Revision,
            Reads(f.Current, id) with { CatalogVersion = f.Current.CatalogVersion }, id);
        StateCommandResult cloned = f.Coordinator.Execute(clone);
        Assert.Equal(StateCommandStatus.Committed, cloned.Status);
        Assert.Equal(3, f.Current.Posters.Count);
        Assert.Equal(3, f.Current.Posters.Select(p => p.Poster.ThemeInstanceId).Distinct().Count());
        Assert.Equal(3, f.Current.Posters.Select(p => p.Poster.PosterId).Distinct().Count());
        f.Remove(id);
        StateCommandResult replay = f.Coordinator.Execute(create);
        Assert.Equal(StateCommandStatus.Replayed, replay.Status);
        Assert.Equal(id, replay.ResultPosterId);
        Assert.Equal(created.CommittedRevision, replay.CommittedRevision);
        Assert.False(replay.Projection!.PostersById.ContainsKey(id));
    }

    [Fact]
    public void RemovalDeltaContainsRemovedIdAndDoesNotAffectOtherPosterVersion()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        var events = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, e) => events.Add(e);
        f.Remove(Main);
        StateProjectionChangedEventArgs change = Assert.Single(events);
        Assert.Equal(Main, Assert.Single(change.ChangedPosterIds));
        Assert.True(change.CatalogChanged);
        Assert.Equal(b, change.Projection.Poster!.Poster.PosterId);
        Assert.Equal(1, change.Projection.PostersById[b].EntityVersion);
    }

    [Fact]
    public void SubscribersAreIsolatedAndReentrantPublicationsRemainOrdered()
    {
        using var f = new Fixture();
        var delivered = new List<long>();
        f.Coordinator.ProjectionChanged += (_, _) => throw new InvalidOperationException("subscriber failure");
        f.Coordinator.ProjectionChanged += (_, e) =>
        {
            if (e.Projection.Revision == 2) f.Coordinator.Execute(Pause(e.Projection));
        };
        f.Coordinator.ProjectionChanged += (_, e) => delivered.Add(e.Projection.Revision);
        StateCommandResult result = f.Coordinator.Execute(Lock(f.Current, Main));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(new long[] { 2, 3 }, delivered);
        Assert.Equal(3, f.Current.Revision);
    }

    [Fact]
    public async Task ConcurrentStoresCanCommitUnrelatedTargetsFromSameGlobalRevision()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        using var secondStore = f.Open();
        var second = new StateProjectionCoordinator(new SqliteStateCommandRepository(secondStore));
        DesktopUpProjection initial = second.EnsureInitialized(Configuration());
        StateCommandResult[] results = await Task.WhenAll(
            Task.Run(() => f.Coordinator.Execute(Lock(initial, Main))), Task.Run(() => second.Execute(Lock(initial, b))));
        Assert.All(results, r => Assert.Equal(StateCommandStatus.Committed, r.Status));
        Assert.Equal(initial.Revision + 2, f.Coordinator.Refresh().Revision);
        Assert.All(f.Current.Posters, p => Assert.True(p.Poster.IsLocked));
    }

    [Fact]
    public async Task ConcurrentStoresCannotBothCommitSameEntityVersion()
    {
        using var f = new Fixture();
        using var secondStore = f.Open();
        var second = new StateProjectionCoordinator(new SqliteStateCommandRepository(secondStore));
        DesktopUpProjection initial = second.EnsureInitialized(Configuration());
        StateCommandResult[] results = await Task.WhenAll(
            Task.Run(() => f.Coordinator.Execute(Lock(initial, Main))), Task.Run(() => second.Execute(Lock(initial, Main))));
        Assert.Single(results.Where(r => r.Status == StateCommandStatus.Committed));
        Assert.Single(results.Where(r => r.Status == StateCommandStatus.Conflict));
        Assert.Equal(2, f.Store.ReadHead().Revision);
    }

    [Fact]
    public void RepositoryRejectsUncoveredWriteEvenIfAPolicyWouldAllowIt()
    {
        using var f = new Fixture();
        StateTransactionResult result = f.Repository.Execute(new(Id(), "test", f.Current.Revision, StateReadSet.ForCatalog(f.Current.CatalogVersion)),
            state => new() { Updates = new[] { state.Catalog.Posters[0].Poster with { IsLocked = true } } });
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Equal(1, f.Store.ReadHead().Revision);
    }

    [Fact]
    public void NoChangeReceiptCannotBeReusedByTheLegacyRepository()
    {
        using var f = new Fixture();
        var command = Lock(f.Current, Main) with { DesiredState = f.Current.PostersById[Main].Poster };
        Assert.Equal(StateCommandStatus.NoChange, f.Coordinator.Execute(command).Status);
        var legacy = new SqlitePosterRepository(f.Store).Save(f.Current.Revision, command.RequestId,
            f.Current.PostersById[Main].Poster with { IsLocked = true });
        Assert.Equal(RepositoryWriteStatus.Failed, legacy.Status);
        Assert.False(f.Repository.Read().Catalog.Posters[0].Poster.IsLocked);
        Assert.Equal(1, f.Store.ReadHead().Revision);
    }

    [Fact]
    public void LegacySettingsWriteParticipatesInSettingsEntityCas()
    {
        using var f = new Fixture();
        var command = Pause(f.Current);
        var legacy = new SqliteSettingsRepository(f.Store).Save(f.Current.Revision, Id(), command.DesiredState);
        Assert.True(legacy.Succeeded);
        StateCommandResult result = f.Coordinator.Execute(command);
        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal("settings", Assert.Single(result.Conflicts).Kind);
        Assert.Equal(2, f.Current.ControlCenter.Settings.EntityVersion);
    }

    [Fact]
    public void ZeroCatalogCanCreateAgainWithoutReusingTheDeletedIdentity()
    {
        using var f = new Fixture();
        f.Remove(Main);
        string id = f.Add("Fresh");
        Assert.NotEqual(Main, id);
        Assert.Equal(id, Assert.Single(f.Current.Posters).Poster.PosterId);
        Assert.Equal(1, f.Current.PostersById[id].EntityVersion);
        Assert.Equal(3, f.Current.Revision);
    }

    [Fact]
    public void ExistingThreePostersInitializeWithoutAddingAnotherDefault()
    {
        using var f = new Fixture();
        f.Add("B"); f.Add("C"); f.Remove(Main); f.Add("D");
        long revision = f.Current.Revision;
        using var secondStore = f.Open();
        var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(secondStore));
        DesktopUpProjection state = coordinator.EnsureInitialized(Configuration());
        Assert.Equal(3, state.Posters.Count);
        Assert.False(state.PostersById.ContainsKey(Main));
        Assert.Equal(revision, state.Revision);
    }

    [Fact]
    public void DuplicateBatchTargetAndWrongRemovalSourceAreRejected()
    {
        using var f = new Fixture();
        var batch = Batch(f.Current, new[] { Main }, BatchPosterCommandKind.SetLocked, true) with { PosterIds = new[] { Main, Main } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(batch).Status);
        var preview = f.Coordinator.PrepareRemoval(Main)!;
        Assert.Equal(StateCommandStatus.PermissionDenied, f.Coordinator.Execute(
            new RemovePosterCommand(Id(), StateCommandSource.Tray, preview.Revision, preview.ReadSet, Main, true)).Status);
        Assert.Equal(1, f.Current.Revision);
    }

    [Fact]
    public async Task ConcurrentScopedInitializersCreateExactlyOneDefault()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.Init.Tests", Id());
        try
        {
            var paths = new UserDataPaths(root);
            using var a = new SqliteStateStore(paths);
            Assert.True(a.Initialize().Succeeded);
            using var b = new SqliteStateStore(paths);
            Assert.True(b.Initialize().Succeeded);
            var results = await Task.WhenAll(
                Task.Run(() => new SqliteStateCommandRepository(a).EnsureInitialized(Configuration())),
                Task.Run(() => new SqliteStateCommandRepository(b).EnsureInitialized(Configuration())));
            Assert.All(results, s => { Assert.Equal(1, s.Revision); Assert.Single(s.Catalog.Posters); });
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private const string Main = BuiltInStateIdentities.PrimaryPosterId;
    private static string Id() => Guid.NewGuid().ToString("N");
    private static PosterDefinition Configuration() => new(Main, "NIGHT OPS", "TACTICAL ARCHIVE / 01", "DISPLAY1",
        1450, 24, 420, 720, 0.97, 0.02, 0.22, 0.7, false, false, false, "grid", "{}", "night-ops");
    private static StateReadSet Reads(DesktopUpProjection p, params string[] ids) => new(ids.ToDictionary(id => id, id => p.PostersById[id].EntityVersion));
    private static PosterStateCommand Lock(DesktopUpProjection p, string id) => new(Id(), StateCommandSource.ControlCenter,
        PosterStateCommandKind.SetLocked, p.Revision, id, p.PostersById[id].Poster with { IsLocked = true })
        { ReadSet = Reads(p, id) };
    private static SettingsStateCommand Pause(DesktopUpProjection p) => new(Id(), StateCommandSource.Tray,
        SettingsStateCommandKind.SetDynamicEffectsPaused, p.Revision, p.ControlCenter.Settings.Settings with { DynamicEffectsPaused = true })
        { ReadSet = StateReadSet.ForSettings(p.ControlCenter.Settings.EntityVersion) };
    private static BatchPosterCommand Batch(DesktopUpProjection p, string[] ids, BatchPosterCommandKind kind, bool value, bool all = false) =>
        new(Id(), StateCommandSource.Tray, p.Revision, Reads(p, ids) with { CatalogVersion = all ? p.CatalogVersion : null }, ids, kind, value, all);

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.ScopedState.Tests", Id());
        public Fixture()
        {
            Paths = new UserDataPaths(_root);
            Store = Open();
            Repository = new SqliteStateCommandRepository(Store);
            Coordinator = new StateProjectionCoordinator(Repository);
            Coordinator.EnsureInitialized(Configuration());
        }
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; }
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current { get { Assert.True(Coordinator.TryGetCurrent(out var p)); return p!; } }
        public SqliteStateStore Open()
        {
            var store = new SqliteStateStore(Paths);
            var result = store.Initialize();
            Assert.True(result.Succeeded, result.Error);
            return store;
        }
        public string Add(string title)
        {
            StateCommandResult result = Coordinator.Execute(new CreatePosterCommand(Id(), StateCommandSource.ControlCenter, Current.Revision,
                StateReadSet.ForCatalog(Current.CatalogVersion), Configuration() with { Title = title }));
            Assert.Equal(StateCommandStatus.Committed, result.Status);
            return result.ResultPosterId!;
        }
        public StateCommandResult Remove(string id)
        {
            PosterRemovalPreview preview = Coordinator.PrepareRemoval(id)!;
            return Coordinator.Execute(new RemovePosterCommand(Id(), StateCommandSource.ControlCenter, preview.Revision, preview.ReadSet, id, true));
        }
        public object? Scalar(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={Path.Combine(Paths.State, "desktopup.sqlite3")};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar();
        }
        public void ExecuteSql(string sql) => Scalar(sql);
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
