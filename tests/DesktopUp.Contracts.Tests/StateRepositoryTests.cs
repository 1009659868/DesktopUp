using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class StateRepositoryTests
{
    [Fact]
    public void SchemaV2CreatesDefaultSettingsWithoutCreatingAPoster()
    {
        using var fixture = new RepositoryFixture();
        using var store = fixture.CreateInitializedStore();
        var posters = new SqlitePosterRepository(store);
        var settings = new SqliteSettingsRepository(store);

        Assert.Null(posters.Read(BuiltInStateIdentities.PrimaryPosterId));
        SettingsStateSnapshot snapshot = settings.Read();
        Assert.Equal(0, snapshot.Revision);
        Assert.Equal(0, snapshot.SettingsRevision);
        Assert.False(snapshot.Settings.DynamicEffectsPaused);
        Assert.Equal(DesktopUpPerformanceMode.Automatic, snapshot.Settings.PerformanceMode);
        Assert.False(snapshot.Settings.LaunchAtStartupRequested);
    }

    [Fact]
    public void DefaultPosterCreationUsesTheGlobalCommittedRevision()
    {
        using var fixture = new RepositoryFixture();
        using var store = fixture.CreateInitializedStore();
        var repository = new SqlitePosterRepository(store);

        PosterStateSnapshot created = repository.GetOrCreateDefault(DefaultPoster());
        PosterStateSnapshot repeated = repository.GetOrCreateDefault(
            DefaultPoster() with { Title = "SHOULD NOT REPLACE" });

        Assert.Equal(1, created.Revision);
        Assert.Equal(1, created.PosterRevision);
        Assert.Equal("NIGHT OPS", created.Poster.Title);
        Assert.Equal(created, repeated);
        Assert.Equal(1, store.ReadHead().Revision);
    }

    [Fact]
    public void PosterSavePersistsGeometryLockAndCollapseAcrossRepositoryRestart()
    {
        using var fixture = new RepositoryFixture();
        PosterStateSnapshot saved;
        using (var store = fixture.CreateInitializedStore())
        {
            var repository = new SqlitePosterRepository(store);
            PosterStateSnapshot initial = repository.GetOrCreateDefault(DefaultPoster());
            PosterDefinition changed = initial.Poster with
            {
                FallbackXDip = 64,
                FallbackYDip = 72,
                WidthDip = 512,
                ExpandedHeightDip = 680,
                HorizontalAnchor = 0.35,
                VerticalAnchor = 0.2,
                NormalizedWidth = 0.28,
                NormalizedExpandedHeight = 0.7,
                IsLocked = true,
                IsCollapsed = true
            };
            RepositoryWriteResult<PosterStateSnapshot> result = repository.Save(
                initial.Revision,
                "poster-save-restart",
                changed);
            Assert.Equal(RepositoryWriteStatus.Committed, result.Status);
            saved = result.Snapshot!;
        }

        using var reopenedStore = fixture.CreateInitializedStore();
        var reopenedRepository = new SqlitePosterRepository(reopenedStore);
        PosterStateSnapshot restored = reopenedRepository.Read(
            BuiltInStateIdentities.PrimaryPosterId)!;

        Assert.Equal(saved, restored);
        Assert.Equal(2, restored.Revision);
        Assert.Equal(2, restored.PosterRevision);
        Assert.True(restored.Poster.IsLocked);
        Assert.True(restored.Poster.IsCollapsed);
        Assert.Equal(512, restored.Poster.WidthDip);
        Assert.Equal(680, restored.Poster.ExpandedHeightDip);
    }

    [Fact]
    public void PosterConflictReturnsLatestSnapshotAndReplayDoesNotWriteTwice()
    {
        using var fixture = new RepositoryFixture();
        using var store = fixture.CreateInitializedStore();
        var repository = new SqlitePosterRepository(store);
        PosterStateSnapshot initial = repository.GetOrCreateDefault(DefaultPoster());
        PosterDefinition locked = initial.Poster with { IsLocked = true };

        RepositoryWriteResult<PosterStateSnapshot> committed = repository.Save(
            initial.Revision,
            "poster-lock-once",
            locked);
        RepositoryWriteResult<PosterStateSnapshot> conflict = repository.Save(
            initial.Revision,
            "poster-stale-collapse",
            initial.Poster with { IsCollapsed = true });
        RepositoryWriteResult<PosterStateSnapshot> replay = repository.Save(
            initial.Revision,
            "poster-lock-once",
            locked);

        Assert.Equal(RepositoryWriteStatus.Committed, committed.Status);
        Assert.Equal(RepositoryWriteStatus.Conflict, conflict.Status);
        Assert.True(conflict.Snapshot?.Poster.IsLocked);
        Assert.False(conflict.Snapshot?.Poster.IsCollapsed);
        Assert.Equal(RepositoryWriteStatus.Replayed, replay.Status);
        Assert.Equal(2, replay.Snapshot?.Revision);
        Assert.Equal(2, store.ReadHead().Revision);
    }

    [Fact]
    public void SettingsAndPosterShareOneGlobalRevision()
    {
        using var fixture = new RepositoryFixture();
        using var store = fixture.CreateInitializedStore();
        var posters = new SqlitePosterRepository(store);
        var settings = new SqliteSettingsRepository(store);
        PosterStateSnapshot poster = posters.GetOrCreateDefault(DefaultPoster());
        SettingsStateSnapshot initialSettings = settings.Read();

        RepositoryWriteResult<SettingsStateSnapshot> settingsWrite = settings.Save(
            poster.Revision,
            "settings-pause",
            initialSettings.Settings with { DynamicEffectsPaused = true });
        PosterStateSnapshot posterAfterSettings = posters.Read(poster.Poster.PosterId)!;
        RepositoryWriteResult<PosterStateSnapshot> posterWrite = posters.Save(
            settingsWrite.Snapshot!.Revision,
            "poster-collapse-after-settings",
            poster.Poster with { IsCollapsed = true });
        SettingsStateSnapshot settingsAfterPoster = settings.Read();

        Assert.Equal(2, settingsWrite.CommittedRevision);
        Assert.Equal(2, posterAfterSettings.Revision);
        Assert.Equal(1, posterAfterSettings.PosterRevision);
        Assert.Equal(3, posterWrite.CommittedRevision);
        Assert.Equal(3, settingsAfterPoster.Revision);
        Assert.Equal(2, settingsAfterPoster.SettingsRevision);
        Assert.True(settingsAfterPoster.Settings.DynamicEffectsPaused);
    }

    [Fact]
    public void InvalidPosterDoesNotAdvanceRevisionOrOverwriteAcceptedState()
    {
        using var fixture = new RepositoryFixture();
        using var store = fixture.CreateInitializedStore();
        var repository = new SqlitePosterRepository(store);
        PosterStateSnapshot initial = repository.GetOrCreateDefault(DefaultPoster());

        RepositoryWriteResult<PosterStateSnapshot> result = repository.Save(
            initial.Revision,
            "poster-invalid",
            initial.Poster with { WidthDip = double.NaN });

        Assert.Equal(RepositoryWriteStatus.ValidationFailed, result.Status);
        Assert.Equal(1, store.ReadHead().Revision);
        Assert.Equal(initial, repository.Read(initial.Poster.PosterId));
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
        "grid",
        "{}",
        "night-ops");

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "DesktopUp.V3.Repositories.Tests",
            Guid.NewGuid().ToString("N"));

        public RepositoryFixture()
        {
            Paths = new UserDataPaths(_root);
        }

        public UserDataPaths Paths { get; }

        public SqliteStateStore CreateInitializedStore()
        {
            var store = new SqliteStateStore(Paths);
            StateStoreInitializationResult result = store.Initialize();
            Assert.True(result.Succeeded, result.Error);
            return store;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
