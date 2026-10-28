using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DesktopShellLocatorTests
{
    private static readonly DesktopShellHostIdentity First = new(101, 201, 301);
    private static readonly DesktopShellHostIdentity Second = new(102, 202, 302);

    [Fact]
    public void StabilityRequiresConsecutiveMatchingHosts()
    {
        var tracker = new DesktopShellStabilityTracker(3);

        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(First, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(First, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(Second, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(Second, DateTimeOffset.UtcNow).State);
        DesktopShellProbeResult stable = tracker.Observe(Second, DateTimeOffset.UtcNow);

        Assert.Equal(DesktopShellProbeState.Stable, stable.State);
        Assert.Equal(1, stable.Snapshot?.Generation);
        Assert.Equal(Second, stable.Snapshot?.Identity);
    }

    [Fact]
    public void MissingHostResetsCandidateProgress()
    {
        var tracker = new DesktopShellStabilityTracker(2);

        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(First, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Missing, tracker.Observe(null, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Candidate, tracker.Observe(First, DateTimeOffset.UtcNow).State);
        Assert.Equal(DesktopShellProbeState.Stable, tracker.Observe(First, DateTimeOffset.UtcNow).State);
    }

    [Fact]
    public void SameStableHostDoesNotAdvanceGeneration()
    {
        var tracker = new DesktopShellStabilityTracker(1);

        DesktopShellProbeResult stable = tracker.Observe(First, DateTimeOffset.UtcNow);
        DesktopShellProbeResult unchanged = tracker.Observe(First, DateTimeOffset.UtcNow);

        Assert.Equal(1, stable.Snapshot?.Generation);
        Assert.Equal(DesktopShellProbeState.Unchanged, unchanged.State);
        Assert.Equal(1, unchanged.Snapshot?.Generation);
    }

    [Fact]
    public void InvalidationRequiresRestabilizationAndAdvancesGeneration()
    {
        var tracker = new DesktopShellStabilityTracker(1);
        Assert.Equal(1, tracker.Observe(First, DateTimeOffset.UtcNow).Snapshot?.Generation);

        tracker.Invalidate();
        DesktopShellProbeResult restabilized = tracker.Observe(First, DateTimeOffset.UtcNow);

        Assert.Equal(DesktopShellProbeState.Stable, restabilized.State);
        Assert.Equal(2, restabilized.Snapshot?.Generation);
    }

    [Fact]
    public void InvalidCurrentHandleIsNeverReturnedFromCache()
    {
        var api = new FakeExplorerDesktopApi(First);
        var locator = new ExplorerDesktopLocator(api, ImmediateOptions(requiredSamples: 1));
        Assert.NotNull(locator.Probe().Snapshot);

        api.Valid = false;

        Assert.Equal(DesktopShellProbeState.Invalidated, locator.Probe().State);
        Assert.Null(locator.Current);
    }

    [Fact]
    public async Task WaitForStableHostUsesBoundedConsecutiveSampling()
    {
        var api = new FakeExplorerDesktopApi(null, First, First);
        var locator = new ExplorerDesktopLocator(api, ImmediateOptions(requiredSamples: 2));

        DesktopShellHostSnapshot? snapshot = await locator.WaitForStableHostAsync();

        Assert.NotNull(snapshot);
        Assert.Equal(First, snapshot.Identity);
        Assert.Equal(1, snapshot.Generation);
        Assert.Equal(3, api.FindCalls);
    }

    [Fact]
    public void NativeLocatorNeverForcesWorkerWCreation()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "ExplorerDesktopNativeApi.cs"));

        Assert.Contains("SHELLDLL_DefView", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x052C", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendMessageTimeout", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SpawnWorker", source, StringComparison.OrdinalIgnoreCase);
    }

    private static ExplorerDesktopLocatorOptions ImmediateOptions(int requiredSamples) => new()
    {
        RequiredStableSamples = requiredSamples,
        MaximumProbeAttempts = 8,
        ProbeInterval = TimeSpan.Zero
    };

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("DesktopUp repository root was not found.");
    }

    private sealed class FakeExplorerDesktopApi : IExplorerDesktopApi
    {
        private readonly Queue<DesktopShellHostIdentity?> _samples;
        private DesktopShellHostIdentity? _last;

        public FakeExplorerDesktopApi(params DesktopShellHostIdentity?[] samples)
        {
            _samples = new Queue<DesktopShellHostIdentity?>(samples);
            _last = samples.LastOrDefault();
        }

        public bool Valid { get; set; } = true;
        public int FindCalls { get; private set; }

        public bool TryFindExistingHost(out DesktopShellHostIdentity identity)
        {
            FindCalls++;
            if (_samples.Count > 0)
            {
                _last = _samples.Dequeue();
            }
            identity = _last ?? default;
            return Valid && _last.HasValue;
        }

        public bool IsValid(DesktopShellHostIdentity identity) =>
            Valid && _last == identity;
    }
}
