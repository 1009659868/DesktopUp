using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class StateProjectionCoordinatorTests
{
    [Fact]
    public void InitializationBuildsThreeProjectionsFromOneCommittedRevision()
    {
        using var fixture = new ProjectionFixture();

        DesktopUpProjection projection = fixture.Coordinator.EnsureInitialized(DefaultPoster());

        Assert.Equal(1, projection.Revision);
        Assert.Equal(projection.Revision, projection.Poster!.Revision);
        Assert.Equal(projection.Revision, projection.ControlCenter.Revision);
        Assert.Equal(projection.Revision, projection.ControlCenter.Poster!.Revision);
        Assert.Equal(projection.Revision, projection.ControlCenter.Settings.Revision);
        Assert.Equal(projection.Revision, projection.Tray.Revision);
        Assert.Equal(1, projection.Poster!.PosterRevision);
        Assert.Equal(0, projection.ControlCenter.Settings.SettingsRevision);
        Assert.Equal(projection.Poster!.Poster, projection.ControlCenter.Poster!.Poster);
    }

    [Fact]
    public void ControlCenterLockCommandPublishesOneRevisionToAllSurfaces()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        var changes = new List<StateProjectionChangedEventArgs>();
        fixture.Coordinator.ProjectionChanged += (_, args) => changes.Add(args);
        PosterDefinition desired = initial.Poster!.Poster with { IsLocked = true };

        StateCommandResult result = fixture.Coordinator.Execute(new PosterStateCommand(
            "control-lock-1",
            StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked,
            initial.Revision,
            desired.PosterId,
            desired));

        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(2, result.CurrentRevision);
        Assert.True(result.Projection?.Poster!.Poster.IsLocked);
        Assert.True(result.Projection?.ControlCenter.Poster!.Poster.IsLocked);
        Assert.True(result.Projection?.Tray.AllPostersLocked);
        StateProjectionChangedEventArgs change = Assert.Single(changes);
        Assert.Equal(2, change.Projection.Revision);
        Assert.Equal(StateCommandSource.ControlCenter, change.Source);
        Assert.Equal("control-lock-1", change.RequestId);
    }

    [Fact]
    public void TrayPauseCommandAdvancesSettingsWithoutRevisingPosterRow()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        DesktopUpSettings desired = initial.ControlCenter.Settings.Settings with
        {
            DynamicEffectsPaused = true
        };

        StateCommandResult result = fixture.Coordinator.Execute(new SettingsStateCommand(
            "tray-pause-1",
            StateCommandSource.Tray,
            SettingsStateCommandKind.SetDynamicEffectsPaused,
            initial.Revision,
            desired));

        DesktopUpProjection projection = Assert.IsType<DesktopUpProjection>(result.Projection);
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Equal(2, projection.Revision);
        Assert.Equal(1, projection.Poster!.PosterRevision);
        Assert.Equal(2, projection.ControlCenter.Settings.SettingsRevision);
        Assert.True(projection.Tray.DynamicEffectsPaused);
        Assert.Contains("已暂停", projection.Tray.ToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public void StaleCommandReturnsCurrentProjectionWithoutOverwritingIt()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        PosterDefinition locked = initial.Poster!.Poster with { IsLocked = true };
        _ = fixture.Coordinator.Execute(new PosterStateCommand(
            "lock-before-stale",
            StateCommandSource.Tray,
            PosterStateCommandKind.SetLocked,
            initial.Revision,
            locked.PosterId,
            locked));
        PosterDefinition staleCollapse = initial.Poster!.Poster with { IsCollapsed = true };

        StateCommandResult result = fixture.Coordinator.Execute(new PosterStateCommand(
            "stale-collapse",
            StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetCollapsed,
            initial.Revision,
            staleCollapse.PosterId,
            staleCollapse));

        Assert.Equal(StateCommandStatus.Conflict, result.Status);
        Assert.Equal(2, result.CurrentRevision);
        Assert.True(result.Projection?.Poster!.Poster.IsLocked);
        Assert.False(result.Projection?.Poster!.Poster.IsCollapsed);
        Assert.Equal(2, fixture.Store.ReadHead().Revision);
    }

    [Fact]
    public void DuplicateRequestIsReplayedWithoutSecondCommitOrBroadcast()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        PosterDefinition desired = initial.Poster!.Poster with { IsCollapsed = true };
        var command = new PosterStateCommand(
            "collapse-idempotent",
            StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetCollapsed,
            initial.Revision,
            desired.PosterId,
            desired);
        int changes = 0;
        fixture.Coordinator.ProjectionChanged += (_, _) => changes++;

        StateCommandResult first = fixture.Coordinator.Execute(command);
        StateCommandResult replay = fixture.Coordinator.Execute(command);

        Assert.Equal(StateCommandStatus.Committed, first.Status);
        Assert.Equal(StateCommandStatus.Replayed, replay.Status);
        Assert.Equal(2, replay.CurrentRevision);
        Assert.Equal(2, fixture.Store.ReadHead().Revision);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void LockedPosterRejectsResizeWithoutAdvancingRevision()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        PosterDefinition locked = initial.Poster!.Poster with { IsLocked = true };
        StateCommandResult lockResult = fixture.Coordinator.Execute(new PosterStateCommand(
            "lock-first",
            StateCommandSource.Poster,
            PosterStateCommandKind.SetLocked,
            initial.Revision,
            locked.PosterId,
            locked));
        DesktopUpProjection lockedProjection = lockResult.Projection!;
        PosterDefinition resized = lockedProjection.Poster!.Poster with { WidthDip = 640 };

        StateCommandResult resizeResult = fixture.Coordinator.Execute(new PosterStateCommand(
            "resize-while-locked",
            StateCommandSource.ControlCenter,
            PosterStateCommandKind.Resize,
            lockedProjection.Revision,
            resized.PosterId,
            resized));

        Assert.Equal(StateCommandStatus.PermissionDenied, resizeResult.Status);
        Assert.Equal(2, fixture.Store.ReadHead().Revision);
        Assert.Equal(420, resizeResult.Projection?.Poster!.Poster.WidthDip);
    }

    [Fact]
    public void AggregateRepositoryReadsPosterAndSettingsInOneSnapshot()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        DesktopUpSettings paused = initial.ControlCenter.Settings.Settings with
        {
            DynamicEffectsPaused = true
        };
        _ = fixture.Coordinator.Execute(new SettingsStateCommand(
            "pause-before-aggregate",
            StateCommandSource.Tray,
            SettingsStateCommandKind.SetDynamicEffectsPaused,
            initial.Revision,
            paused));

        AuthoritativeStateSnapshot aggregate = fixture.Aggregates.Read(
            BuiltInStateIdentities.PrimaryPosterId);

        Assert.Equal(2, aggregate.Revision);
        Assert.Equal(1, aggregate.PosterRevision);
        Assert.Equal(2, aggregate.SettingsRevision);
        Assert.True(aggregate.Settings.DynamicEffectsPaused);
    }

    [Fact]
    public void CollapseAndExpandKeepTheSameNormalizedTop()
    {
        using var fixture = new ProjectionFixture();
        DesktopUpProjection initial = fixture.Coordinator.EnsureInitialized(DefaultPoster());
        double initialTop = NormalizedTop(initial.Poster!.Poster);
        PosterDefinition collapsed = initial.Poster!.Poster with { IsCollapsed = true };

        StateCommandResult collapsedResult = fixture.Coordinator.Execute(
            new PosterStateCommand(
                "collapse-preserve-top",
                StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed,
                initial.Revision,
                collapsed.PosterId,
                collapsed));
        PosterDefinition collapsedState = collapsedResult.Projection!.Poster!.Poster;
        double collapsedTop = NormalizedTop(collapsedState);
        PosterDefinition expanded = collapsedState with { IsCollapsed = false };
        StateCommandResult expandedResult = fixture.Coordinator.Execute(
            new PosterStateCommand(
                "expand-preserve-top",
                StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed,
                collapsedResult.CurrentRevision,
                expanded.PosterId,
                expanded));

        Assert.Equal(initialTop, collapsedTop, precision: 10);
        Assert.Equal(initialTop, NormalizedTop(expandedResult.Projection!.Poster!.Poster), precision: 10);
    }

    private static double NormalizedTop(PosterDefinition poster)
    {
        double workHeightDip = poster.ExpandedHeightDip / poster.NormalizedExpandedHeight;
        double visibleHeight = poster.IsCollapsed
            ? PosterFrameGeometryPolicy.CollapsedHeightDip / workHeightDip
            : poster.NormalizedExpandedHeight;
        return poster.VerticalAnchor * (1 - visibleHeight);
    }

    private static PosterDefinition DefaultPoster() => new(
        BuiltInStateIdentities.PrimaryPosterId,
        "NIGHT OPS",
        "TACTICAL ARCHIVE / 01",
        "\\\\.\\DISPLAY1",
        1450,
        24,
        420,
        720,
        0.97,
        0.02,
        0.22,
        0.7,
        false,
        false,
        false,
        "adaptive-grid",
        "{}",
        "night-ops");

    private sealed class ProjectionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "DesktopUp.V3.Projections.Tests",
            Guid.NewGuid().ToString("N"));

        public ProjectionFixture()
        {
            var paths = new UserDataPaths(_root);
            Store = new SqliteStateStore(paths);
            StateStoreInitializationResult initialized = Store.Initialize();
            Assert.True(initialized.Succeeded, initialized.Error);
            var posters = new SqlitePosterRepository(Store);
            var settings = new SqliteSettingsRepository(Store);
            Aggregates = new SqliteStateProjectionRepository(Store);
            Coordinator = new StateProjectionCoordinator(
                new SqliteStateCommandRepository(Store));
        }

        public SqliteStateStore Store { get; }
        public SqliteStateProjectionRepository Aggregates { get; }
        public StateProjectionCoordinator Coordinator { get; }

        public void Dispose()
        {
            Store.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
