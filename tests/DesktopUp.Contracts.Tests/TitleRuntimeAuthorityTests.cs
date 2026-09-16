using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleRuntimeAuthorityTests
{
    [Fact]
    public void DisconnectedMonitorAndConstrainedLayoutRequireOpaqueLiveHostWitness()
    {
        using var f = new Fixture();
        var command = f.Command();
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(command).Status);
        var host = new Readback(command.Trace.Start); f.Coordinator.BindTitleRuntime(host);
        using var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(command.Trace.Start, command.ReadSet));
        int events = 0; f.Coordinator.ProjectionChanged += (_, _) => events++;
        long revision = f.Coordinator.Refresh().Revision;
        var result = f.Coordinator.ExecuteTitle(command, lease);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(revision + 1, result.CurrentRevision); Assert.Equal(1, events); Assert.Equal(2, host.Checks);
        Assert.Equal("test", result.Projection!.Posters.Single().Poster.MonitorDeviceName);
        Assert.Equal(360, result.Projection.Posters.Single().Poster.ExpandedHeightDip); // no temporary height persistence
        Assert.Equal(1, result.Projection.Posters.Single().LayoutGeneration);
        Assert.Equal(result.CurrentRevision, result.Projection.Tray.Revision);
        Assert.Equal(result.CurrentRevision, result.Projection.ControlCenter.Revision);
    }

    [Fact]
    public void NoRegisteredHostCannotIssueAStartWitness()
    {
        using var f = new Fixture(); var c = f.Command();
        Assert.Null(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
    }

    [Theory] [InlineData("expired")] [InlineData("disposed")] [InlineData("issuer")]
    [InlineData("operation")] [InlineData("scene")] [InlineData("reads")] [InlineData("system")]
    public void ExpiredForeignOrTamperedWitnessCannotWrite(string failure)
    {
        using var f = new Fixture(); var c = f.Command(); var host = new Readback(c.Trace.Start);
        f.Coordinator.BindTitleRuntime(host);
        var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
        using (lease)
        {
            StateProjectionCoordinator receiver = f.Coordinator;
            if (failure == "expired") host.Current = false;
            if (failure == "disposed") lease.Dispose();
            if (failure == "issuer")
            {
                receiver = new(f.Repository); receiver.Refresh(); receiver.BindTitleRuntime(host);
            }
            if (failure == "operation") c = c with { RequestId = "forged" };
            if (failure == "system") c = c with { Source = StateCommandSource.System };
            if (failure == "reads") c = c with { ReadSet = c.ReadSet with { CatalogVersion = null } };
            if (failure == "scene") c = c with { Trace = c.Trace with { Start = c.Trace.Start with
                { Initial = new(c.Trace.Start.Initial.Environment, c.Trace.Start.Initial.Posters.Select(p =>
                    p with { Bounds = p.Bounds with { X = p.Bounds.X + 1 } })) } } };
            long revision = f.Coordinator.Refresh().Revision;
            var result = receiver.ExecuteTitle(c, lease);
            Assert.False(result.Succeeded);
            Assert.Equal(revision, f.Coordinator.Refresh().Revision);
        }
    }

    [Fact]
    public void WitnessDoesNotBypassReplayOrEndpointValidation()
    {
        using var f = new Fixture(); var c = f.Command(); f.Coordinator.BindTitleRuntime(new Readback(c.Trace.Start));
        using var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
        c = c with { Trace = c.Trace with { Presented = new(c.Trace.Presented.Environment, c.Trace.Presented.Posters.Select(p =>
            p with { Bounds = p.Bounds with { X = 800 } })) } };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.ExecuteTitle(c, lease).Status);
        Assert.Equal(0, f.Coordinator.Refresh().Posters.Single().LayoutGeneration);
    }

    [Fact]
    public void WitnessDoesNotBypassCompleteEntityCas()
    {
        using var f = new Fixture(); var c = f.Command(); f.Coordinator.BindTitleRuntime(new Readback(c.Trace.Start));
        using var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
        var before = f.Coordinator.Refresh(); var source = before.Posters.Single();
        var change = f.Coordinator.Execute(new PosterStateCommand("lock-now", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, before.Revision, source.Poster.PosterId, source.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(source.Poster.PosterId, source.EntityVersion) });
        Assert.True(change.Succeeded);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.ExecuteTitle(c, lease).Status);
        Assert.Equal(change.CurrentRevision, f.Coordinator.Refresh().Revision);
    }

    [Fact]
    public void RuntimeBindingCannotBeReplacedByAnotherCaller()
    {
        using var f = new Fixture(); var c = f.Command(); var host = new Readback(c.Trace.Start);
        f.Coordinator.BindTitleRuntime(host); f.Coordinator.BindTitleRuntime(host);
        Assert.Throws<InvalidOperationException>(() => f.Coordinator.BindTitleRuntime(new Readback(c.Trace.Start)));
    }

    [Fact]
    public void RuntimeReadFailureIsRejectedWithoutReceiptOrWrite()
    {
        using var f = new Fixture(); var c = f.Command(); var host = new Readback(c.Trace.Start);
        f.Coordinator.BindTitleRuntime(host);
        using var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
        host.Throw = true;
        long revision = f.Coordinator.Refresh().Revision;
        Assert.Equal(StateCommandStatus.Failed, f.Coordinator.ExecuteTitle(c, lease).Status);
        Assert.Equal(revision, f.Coordinator.Refresh().Revision);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void InterruptedTerminalStillRequiresLiveWitnessInsideTransaction(bool current)
    {
        using var f = new Fixture(); var c = f.Command(interrupted: true);
        var host = new Readback(c.Trace.Start); f.Coordinator.BindTitleRuntime(host);
        using var lease = Assert.IsType<TitleRuntimeLease>(f.Coordinator.OpenTitle(c.Trace.Start, c.ReadSet));
        host.Current = current;
        long revision = f.Coordinator.Refresh().Revision;
        int events = 0; f.Coordinator.ProjectionChanged += (_, _) => events++;
        var result = f.Coordinator.ExecuteTitle(c, lease);
        Assert.Equal(current, result.Succeeded);
        Assert.Equal(revision + (current ? 1 : 0), f.Coordinator.Refresh().Revision);
        Assert.Equal(current ? 1 : 0, events); Assert.Equal(2, host.Checks);
    }

    private sealed class Readback(TitleSpatialStart accepted) : ITitleRuntimeReadback
    {
        public bool Current = true, Throw;
        public int Checks;
        public bool ValidateTitleStart(TitleSpatialStart start, StateReadSet reads)
        {
            Checks++;
            if (Throw) throw new InvalidOperationException("native readback unavailable");
            return Current && start == accepted;
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.TitleRuntime.Tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            var paths = new UserDataPaths(_root); paths.EnsureCreated(); _store = new(paths);
            Assert.True(_store.Initialize().Succeeded); Repository = new(_store); Coordinator = new(Repository);
            Coordinator.EnsureInitialized(new("C", "C", "", "disconnected-monitor", 100, 200, 240, 360,
                .1, .25, .2, .36, false, false, true, "grid", "{}", "basic-box"));
        }
        public TitleSpatialCommand Command(bool interrupted = false)
        {
            var current = Coordinator.Refresh(); var target = current.Posters.Single();
            // Readback of a runtime-rebuilt effective height; deliberately not the durable anchor rectangle.
            var scene = new SpatialSnapshot(TitleSpatialTestData.Env,
                [new("C", new(100, 200, 240, 220), 360, true, false, false,
                    target.LayoutGeneration, target.LastLayoutOperationId)]);
            var start = TitleSpatialTestData.Start(scene);
            var s = new TitleSpatialSession(start, target.Presentation.Metrics);
            TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, 30));
            if (interrupted) TitleSpatialTestData.Accept(s, TitleSpatialTestData.Input(s, TitlePointerEventKind.Interrupt, 900));
            else TitleSpatialTestData.Release(s, 30);
            return new(start.Press.OperationId, StateCommandSource.Poster, current.Revision,
                new(new Dictionary<string, long> { ["C"] = target.EntityVersion }, CatalogVersion: current.CatalogVersion), s.Freeze());
        }
        public void Dispose()
        {
            _store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(_root, true);
        }
    }
}
