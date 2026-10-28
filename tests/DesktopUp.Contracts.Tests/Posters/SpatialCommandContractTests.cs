using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class SpatialCommandContractTests
{
    private static SpatialSnapshot Physical() => Scene(Poster("A", 0, 0), Poster("B", 500, 0));
    private static PosterDefinition Definition(SpatialPoster p) => new(p.PosterId, p.PosterId, "spatial fixture", "display-1",
        p.Bounds.X, p.Bounds.Y, p.Bounds.Width, p.ExpandedHeight, 0, 0, 0.24, 0.24,
        p.IsLocked, p.IsCollapsed, false, "grid", "{}", "box") { IsVisible = p.IsVisible };
    private static DesktopStateSnapshot State(SpatialSnapshot scene) => new(
        new(10, 3, true, scene.Posters.Select(p => new PosterStateSnapshot(10, 1, Definition(p), DateTimeOffset.UnixEpoch, 1)).ToArray()),
        new(10, 1, new(false, DesktopUpPerformanceMode.Automatic, false), DateTimeOffset.UnixEpoch, 1));
    private static SpatialPreparedChange MoveA(SpatialSnapshot scene) => new(
        new("A", scene.Posters[0].Bounds, new(0, 300, 240, 240), false, false, SpatialChangeCause.Move), 0, 1);
    private static SpatialBatchCommand Command(DesktopStateSnapshot state, SpatialSnapshot physical,
        IEnumerable<SpatialPreparedChange>? changes = null, StateReadSet? reads = null) =>
        new("move-a", "A", StateCommandSource.Poster, state.Revision, physical.Environment,
            reads ?? SpatialCommandContract.CaptureReadSet(state), changes ?? new[] { MoveA(physical) });

    [Fact]
    public void CaptureIncludesHiddenAndOtherWorkspaceEntitiesButNotGlobalSettings()
    {
        var physical = Physical();
        var state = State(physical);
        var hidden = new PosterStateSnapshot(10, 1, Definition(Poster("C", 0, 0) with { IsVisible = false })
            with { MonitorDeviceName = "display-2" }, DateTimeOffset.UnixEpoch, 7);
        state = state with { Catalog = state.Catalog with { Posters = state.Catalog.Posters.Append(hidden).ToArray() } };
        var reads = SpatialCommandContract.CaptureReadSet(state);
        Assert.Equal(new[] { "A", "B", "C" }, reads.Posters.Keys);
        Assert.Equal(7, reads.Posters["C"]);
        Assert.Equal(3, reads.CatalogVersion);
        Assert.Null(reads.SettingsVersion);
    }

    [Fact]
    public void AnUnmovedObstacleChangingInvalidatesTheWholePlan()
    {
        var physical = Physical();
        var state = State(physical);
        var command = Command(state, physical);
        var changed = state with { Catalog = state.Catalog with
        {
            Revision = 11,
            Posters = state.Catalog.Posters.Select(p => p.Poster.PosterId == "B" ? p with { EntityVersion = 2 } : p).ToArray()
        } };
        var issues = SpatialCommandContract.Validate(command, changed, physical);
        Assert.Contains(issues, i => i.Reason == SpatialCommandFailure.ReadConflict && i.PosterId == "B");
        Assert.Equal(new SpatialRect(0, 0, 240, 240), physical.Posters[0].Bounds);
    }

    [Fact]
    public void CatalogInsertionAndRemovalArePhantomConflicts()
    {
        var physical = Physical();
        var state = State(physical);
        var command = Command(state, physical);
        foreach (bool insert in new[] { true, false })
        {
            var changed = state with { Catalog = state.Catalog with
            {
                Revision = 11, CatalogVersion = 4,
                Posters = insert ? state.Catalog.Posters.Append(new(11, 1, Definition(Poster("C", 500, 500)), DateTimeOffset.UnixEpoch)).ToArray()
                    : state.Catalog.Posters.Take(1).ToArray()
            } };
            Assert.Contains(SpatialCommandContract.Validate(command, changed, physical), i => i.Reason == SpatialCommandFailure.ReadConflict);
        }
    }

    [Fact]
    public void UnrelatedGlobalSettingsRevisionDoesNotInvalidateSpatialReadSet()
    {
        var physical = Physical();
        var state = State(physical);
        var command = Command(state, physical);
        var changed = state with { Catalog = state.Catalog with { Revision = 11 },
            Settings = state.Settings with { Revision = 11, EntityVersion = 2, Settings = state.Settings.Settings with { DynamicEffectsPaused = true } } };
        Assert.Empty(SpatialCommandContract.Validate(command, changed, physical));
    }

    [Fact]
    public void SelectedOnlyReadSetIsRejectedEvenWhenTargetStillMatches()
    {
        var physical = Physical();
        var state = State(physical);
        var command = Command(state, physical, reads: StateReadSet.ForPoster("A", 1) with { CatalogVersion = 3 });
        Assert.Contains(SpatialCommandContract.Validate(command, state, physical), i => i.Reason == SpatialCommandFailure.IncompleteReadSet);
    }

    [Fact]
    public void CommandFreezesCallerCollectionsAndTargetIdentity()
    {
        var physical = Physical();
        var state = State(physical);
        var reads = new Dictionary<string, long> { ["A"] = 1, ["B"] = 1 };
        var changes = new List<SpatialPreparedChange> { MoveA(physical) };
        var command = Command(state, physical, changes, new(reads, CatalogVersion: 3));
        reads.Clear(); changes.Clear();
        Assert.Equal(2, command.ReadSet.Posters.Count);
        Assert.Single(command.Changes);
        Assert.Equal("A", command.SourcePosterId);
        Assert.Throws<NotSupportedException>(() => ((IList<SpatialPreparedChange>)command.Changes).Clear());
        Assert.Empty(SpatialCommandContract.Validate(command, state, physical));
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("display")]
    [InlineData("dpi")]
    [InlineData("session")]
    [InlineData("area")]
    public void AllPhysicalEnvironmentChangesCancelOldPlans(string kind)
    {
        var physical = Physical();
        var state = State(physical);
        var command = Command(state, physical);
        var environment = kind switch
        {
            "shell" => TestEnvironment with { ShellGeneration = 2 },
            "display" => TestEnvironment with { DisplayGeneration = 2 },
            "dpi" => TestEnvironment with { DpiY = 120 },
            "session" => TestEnvironment with { RuntimeSessionId = Guid.NewGuid() },
            _ => TestEnvironment with { WorkArea = new(0, 0, 1000, 900) }
        };
        Assert.Contains(SpatialCommandContract.Validate(command, state, new(environment, physical.Posters)),
            i => i.Reason == SpatialCommandFailure.StaleEnvironment);
    }

    [Fact]
    public void DuplicateTargetsStaleLayoutGenerationAndFinalOverlapAreRejected()
    {
        var physical = Physical();
        var state = State(physical);
        var change = MoveA(physical);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [change, change]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCommand);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [change with { ExpectedLayoutGeneration = 1 }]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCandidate);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical,
            [change with { Geometry = change.Geometry with { After = new(500, 0, 240, 240) } }]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCandidate);
    }

    [Fact]
    public void LockedNeighborCannotBePushedAndNeighborSizeCannotChange()
    {
        var physical = Scene(Poster("A", 0, 0), Poster("B", 500, 0) with { IsLocked = true });
        var state = State(physical);
        var push = new SpatialPreparedChange(new("B", physical.Posters[1].Bounds, new(500, 300, 240, 240), false, false, SpatialChangeCause.Push), 0, 1);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [MoveA(physical), push]), state, physical),
            i => i.Reason == SpatialCommandFailure.PermissionDenied);
        var resize = push with { Geometry = push.Geometry with { After = new(500, 300, 260, 240) } };
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [MoveA(physical), resize]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCandidate);
    }

    [Fact]
    public void ASourceOperationCannotDisguiseResizingOrAuthorizeDragPush()
    {
        var physical = Physical();
        var state = State(physical);
        var move = MoveA(physical);
        var disguised = move with { Geometry = move.Geometry with { After = new(0, 300, 250, 240) } };
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [disguised]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCandidate);
        var push = new SpatialPreparedChange(new("B", physical.Posters[1].Bounds, new(500, 300, 240, 240), false, false, SpatialChangeCause.Push), 0, 1);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [move, push]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCandidate);
        Assert.Contains(SpatialCommandContract.Validate(Command(state, physical, [push]), state, physical),
            i => i.Reason == SpatialCommandFailure.InvalidCommand);
    }

    [Fact]
    public void FullPreparedExpansionBatchAllowsDifferentDestinationsButNoLockedPeer()
    {
        var a = Poster("A", 0, 0, 240, 60) with { IsCollapsed = true, ExpandedHeight = 600 };
        var b = Poster("B", 0, 100, 240, 240);
        var physical = Scene(a, b);
        var state = State(physical);
        var expand = new SpatialPreparedChange(new("A", a.Bounds, new(0, 0, 240, 600), true, false, SpatialChangeCause.Expand), 0, 1);
        var push = new SpatialPreparedChange(new("B", b.Bounds, new(0, 600, 240, 240), false, false, SpatialChangeCause.Push), 0, 1);
        var command = Command(state, physical, [push, expand]);
        Assert.Empty(SpatialCommandContract.Validate(command, state, physical));
        Assert.Equal(new[] { "A", "B" }, command.Changes.Select(c => c.Geometry.PosterId));
        // This only validates a caller-supplied endpoint batch; no push solver or DB execution exists yet.
    }
}
