using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterSessionRegistryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void PersistedCatalogRestoresExactlyZeroOneOrThreeWithoutExtraDefault(int count)
    {
        using var f = new Fixture();
        if (count == 0) f.Remove(Main);
        for (int i = 1; i < count; i++) f.Add("Box " + i);
        f.Registry.ApplyProjection(Change(f.Coordinator.EnsureInitialized(Configuration())));
        Assert.True(f.Recover());
        Assert.Equal(count, f.Registry.Sessions.Count);
        Assert.Equal(count, f.Factory.Created.Count);
        f.Registry.ApplyProjection(Change(f.Current));
        Assert.True(f.Recover());
        Assert.Equal(count, f.Factory.Created.Count);
    }

    [Fact]
    public void FreshProfileSeedsOnceAndConcurrentUiActivationDoesNotDuplicateSessions()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        for (int i = 0; i < 5; i++) f.Registry.ApplyProjection(Change(f.Coordinator.EnsureInitialized(Configuration())));
        Assert.Single(f.Registry.Sessions);
        Assert.Single(f.Factory.Created);
        Assert.Single(f.Current.Posters);
    }

    [Fact]
    public void PerPosterProjectionDoesNotMutateNeighborsAndDeleteCancelsOnlyTarget()
    {
        using var f = new Fixture();
        string b = f.Add("B"), c = f.Add("C");
        Assert.True(f.Recover());
        FakeSession first = f.Factory.For(Main), second = f.Factory.For(b), third = f.Factory.For(c);
        Assert.True(f.Set(b, p => p with { IsLocked = true }, PosterStateCommandKind.SetLocked).Succeeded);
        Assert.True(second.Projection!.Poster.IsLocked);
        Assert.False(first.Projection!.Poster.IsLocked);
        Assert.False(third.Projection!.Poster.IsLocked);
        f.Remove(b);
        Assert.True(second.Token.IsCancellationRequested);
        Assert.True(second.Disposed);
        Assert.False(first.Disposed);
        Assert.False(third.Disposed);
        Assert.Equal(2, f.Registry.Sessions.Count);
    }

    [Fact]
    public void HiddenPosterReleasesAndUnhideCreatesNewEpochWithCommittedState()
    {
        using var f = new Fixture();
        f.Set(Main, p => p with { IsLocked = true }, PosterStateCommandKind.SetLocked);
        Assert.True(f.Recover());
        FakeSession previous = f.Factory.For(Main);
        long epoch = Assert.Single(f.Registry.Sessions).Epoch;
        f.Set(Main, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        Assert.Empty(f.Registry.Sessions);
        Assert.True(previous.Token.IsCancellationRequested);
        f.Set(Main, p => p with { IsVisible = true }, PosterStateCommandKind.SetVisible);
        Assert.True(Assert.Single(f.Registry.Sessions).Epoch > epoch);
        Assert.True(f.Factory.For(Main).Projection!.Poster.IsLocked);
        Assert.Equal(2, f.Factory.Created.Count);
    }

    [Fact]
    public void FailureAfterPartialCreationIsIsolatedAndRetryDoesNotRecreateHealthySessions()
    {
        using var f = new Fixture();
        string b = f.Add("B"), c = f.Add("C");
        f.Factory.FailStart.Add(b);
        Assert.False(f.Recover());
        Assert.Equal(2, f.Registry.Sessions.Count(s => s.Status == PosterSessionStatus.Ready));
        Assert.Equal(PosterSessionStatus.Degraded, f.Registry.Sessions.Single(s => s.PosterId == b).Status);
        Assert.True(f.Factory.For(b).Disposed);
        FakeSession first = f.Factory.For(Main), third = f.Factory.For(c);
        f.Factory.FailStart.Clear();
        Assert.True(f.Recover());
        Assert.Same(first, f.Registry.Find(Main));
        Assert.Same(third, f.Registry.Find(c));
        Assert.Equal(4, f.Factory.Created.Count);
        Assert.True(f.Factory.Created.First(s => s.PosterId == b).Token.IsCancellationRequested);
    }

    [Fact]
    public void SharedRecoveryReadbackIsCheckedPerSessionAndFailureDoesNotEraseCatalog()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        Assert.True(f.Recover());
        long revision = f.Current.Revision;
        FakeSession first = f.Factory.For(Main), second = f.Factory.For(b);
        second.FailRecover = true;
        Assert.False(f.Recover(2));
        Assert.Equal(2, first.CurrentGenerationPair!.Value.DesktopShellGeneration);
        Assert.True(second.Disposed);
        Assert.Equal(revision, f.Current.Revision);
        Assert.True(f.Recover(2));
        Assert.Same(first, f.Registry.Find(Main));
        Assert.Equal(1, first.Recoveries);
        Assert.Equal(2, f.Registry.Sessions.Count);
    }

    [Fact]
    public async Task DeleteAndHideWhileDiscoveryWaitsCannotBeResurrectedByDelayedSnapshot()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        Assert.True(f.Recover());
        f.Registry.SuspendEnvironment(2);
        var releaseDiscovery = new TaskCompletionSource<PosterSessionEnvironment>();
        Task<bool> recovery = FinishAsync();
        f.Remove(Main);
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        releaseDiscovery.SetResult(Environment(2));
        Assert.True(await recovery);
        Assert.Empty(f.Registry.Sessions);
        Assert.Equal(2, f.Factory.Created.Count);
        async Task<bool> FinishAsync() => f.Registry.Recover(await releaseDiscovery.Task, 2, () => true);
    }

    [Fact]
    public void DeleteReentrantlyDuringCreateCancelsAndCleansTheUnpublishedIncarnation()
    {
        using var f = new Fixture();
        f.Factory.OnStart = s => { f.Remove(s.PosterId); Assert.True(s.Token.IsCancellationRequested); };
        Assert.True(f.Recover());
        Assert.Empty(f.Registry.Sessions);
        Assert.True(Assert.Single(f.Factory.Created).Disposed);
    }

    [Fact]
    public void SupersededGenerationAndOldProjectionCannotRestoreRemovedWindow()
    {
        using var f = new Fixture();
        DesktopUpProjection old = f.Current;
        f.Registry.SuspendEnvironment(3);
        Assert.False(f.Registry.Recover(Environment(2), 2, () => true));
        Assert.Empty(f.Factory.Created);
        Assert.False(f.Registry.Recover(Environment(3), 3, () => false));
        f.Remove(Main);
        f.Registry.ApplyProjection(Change(old));
        Assert.True(f.Recover(3));
        Assert.Empty(f.Registry.Sessions);
        Assert.Empty(f.Factory.Created);
    }

    [Fact]
    public void ShutdownCancelsAllSessionsAndRejectsDelayedWork()
    {
        using var f = new Fixture();
        f.Add("B"); f.Add("C");
        Assert.True(f.Recover());
        f.Registry.Dispose();
        Assert.All(f.Factory.Created, s => { Assert.True(s.Disposed); Assert.True(s.Token.IsCancellationRequested); });
        Assert.Empty(f.Registry.Sessions);
        Assert.False(f.Recover(2));
        f.Registry.ApplyProjection(Change(f.Current));
        Assert.Equal(3, f.Factory.Created.Count);
    }

    [Fact]
    public void IdenticalNativeResourceNamesAreScopedByPosterAndIncarnation()
    {
        using var f = new Fixture();
        var shared = new ResourceLedger(f.Paths);
        var a = new PosterSessionLedger(shared, "A", 1);
        var b = new PosterSessionLedger(shared, "B", 2);
        using (a.Acquire("window.poster-visual", "test"))
        using (b.Acquire("window.poster-visual", "test"))
        {
            Assert.Equal(2, shared.ActiveResources.Count);
            Assert.Single(a.ActiveResources); Assert.Single(b.ActiveResources);
        }
        Assert.Empty(shared.ActiveResources);
    }

    [Fact]
    public void NativeReadbackSnapshotsCanBeWrittenToJsonEvidence()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        new JsonLineLog(f.Paths).Write("info", "test.sessions", new { sessions = f.Registry.Sessions });
        Assert.Contains("0x64", File.ReadAllText(Path.Combine(f.Paths.Logs, "desktopup.jsonl")));
    }

    [Fact]
    public void CleanupFailureIsRetainedForRetryAndNeverReusedAsAReadyIncarnation()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        Assert.True(f.Recover());
        FakeSession failed = f.Factory.For(Main), healthy = f.Factory.For(b);
        failed.FailDispose = true;
        f.Remove(Main);
        Assert.Null(f.Registry.Find(Main));
        Assert.Equal(PosterSessionStatus.Degraded, f.Registry.Sessions.Single(s => s.PosterId == Main).Status);
        Assert.True(f.Registry.NeedsRecovery(Environment(1).Shell));
        failed.FailDispose = false;
        Assert.True(f.Recover());
        Assert.Single(f.Registry.Sessions);
        Assert.Same(healthy, f.Registry.Find(b));
    }

    [Fact]
    public void FailedSessionShutdownDoesNotPreventOtherSessionsFromClosing()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        Assert.True(f.Recover());
        f.Factory.For(Main).FailDispose = true;
        f.Registry.Dispose();
        Assert.True(f.Factory.For(b).Disposed);
        Assert.Single(f.Registry.Sessions);
        f.Factory.For(Main).FailDispose = false;
        f.Registry.Dispose();
        Assert.Empty(f.Registry.Sessions);
    }

    private const string Main = BuiltInStateIdentities.PrimaryPosterId;
    private static string Id() => Guid.NewGuid().ToString("N");
    private static PosterDefinition Configuration() => new(Main, "A", "S", "DISPLAY1",
        0, 0, 420, 720, 0.1, 0.1, 0.22, 0.7, false, false, false, "grid", "{}", "night-ops");
    private static StateProjectionChangedEventArgs Change(DesktopUpProjection p) => new(p, StateCommandSource.System, Id());
    private static PosterSessionEnvironment Environment(long generation) => new(
        new(new(1, 2, 3), generation, DateTimeOffset.UtcNow),
        new("display-" + generation, new[] { new DisplayMonitorSnapshot("DISPLAY1", true,
            new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 96, 96) }, generation, DateTimeOffset.UtcNow));

    private sealed class FakeLog : IStructuredLog { public void Write(string level, string name, object? data = null) { } }
    private sealed class Factory : IPosterSessionFactory
    {
        public List<FakeSession> Created { get; } = new();
        public HashSet<string> FailStart { get; } = new();
        public Action<FakeSession>? OnStart { get; set; }
        public FakeSession For(string id) => Created.Last(s => s.PosterId == id);
        public IPosterSession Create(string id, long epoch)
        {
            var session = new FakeSession(id, this);
            Created.Add(session);
            return session;
        }
    }
    private sealed class FakeSession(string id, Factory factory) : IPosterSession
    {
        public string PosterId => id;
        public nint WindowHandle => Disposed ? 0 : 100;
        public bool IsEditing { get; private set; }
        public DesktopRecoveryGenerationPair? CurrentGenerationPair { get; private set; }
        public PosterSurfaceProjection? Projection { get; private set; }
        public CancellationToken Token { get; private set; }
        public bool Disposed { get; private set; }
        public bool FailRecover { get; set; }
        public bool FailDispose { get; set; }
        public int Recoveries { get; private set; }
        public bool Start(PosterSurfaceProjection p, PosterSessionEnvironment e, CancellationToken lifetime)
        {
            Token = lifetime; Projection = p; CurrentGenerationPair = e.Generations;
            factory.OnStart?.Invoke(this);
            return !factory.FailStart.Contains(id);
        }
        public bool ApplyProjection(StateProjectionChangedEventArgs e) { Projection = e.Projection.PostersById[id]; return true; }
        public bool Recover(PosterSessionEnvironment e, long request)
        {
            Recoveries++;
            if (FailRecover) return false;
            CurrentGenerationPair = e.Generations; return true;
        }
        public bool IsAttached(DesktopShellHostSnapshot shell) => !Disposed && CurrentGenerationPair?.DesktopShellGeneration == shell.Generation;
        public bool SetEditing(bool value) { IsEditing = value; return true; }
        public void Dispose()
        {
            if (FailDispose) throw new InvalidOperationException("fake cleanup failure");
            Disposed = true;
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.Sessions.Tests", Id());
        private readonly SqliteStateStore _store;
        public Fixture()
        {
            Paths = new UserDataPaths(_root);
            _store = new(Paths); Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            Registry = new(Factory, new FakeLog());
            Coordinator.ProjectionChanged += (_, args) => Registry.ApplyProjection(args);
            Registry.ApplyProjection(Change(Coordinator.EnsureInitialized(Configuration())));
        }
        public UserDataPaths Paths { get; }
        public Factory Factory { get; } = new();
        public PosterSessionRegistry Registry { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current { get { Assert.True(Coordinator.TryGetCurrent(out var p)); return p!; } }
        public bool Recover(long generation = 1) => Registry.Recover(Environment(generation), generation, () => true);
        public string Add(string title)
        {
            var result = Coordinator.Execute(new CreatePosterCommand(Id(), StateCommandSource.ControlCenter, Current.Revision,
                StateReadSet.ForCatalog(Current.CatalogVersion), Configuration() with { Title = title }));
            Assert.True(result.Succeeded, result.Error); return result.ResultPosterId!;
        }
        public void Remove(string id)
        {
            var preview = Coordinator.PrepareRemoval(id)!;
            Assert.True(Coordinator.Execute(new RemovePosterCommand(Id(), StateCommandSource.ControlCenter,
                preview.Revision, preview.ReadSet, id, true)).Succeeded);
        }
        public StateCommandResult Set(string id, Func<PosterDefinition, PosterDefinition> mutate, PosterStateCommandKind kind) =>
            Coordinator.Execute(new PosterStateCommand(Id(), StateCommandSource.ControlCenter, kind, Current.Revision, id,
                mutate(Current.PostersById[id].Poster)) { ReadSet = StateReadSet.ForPoster(id, Current.PostersById[id].EntityVersion) });
        public void Dispose()
        {
            Registry.Dispose(); _store.Dispose(); SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
