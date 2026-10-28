using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DisplayTopologyTests
{
    private static readonly DisplayMonitorSnapshot Primary100 = new(
        "\\\\.\\DISPLAY1",
        true,
        new DesktopPixelRect(0, 0, 1920, 1080),
        new DesktopPixelRect(0, 0, 1920, 1040),
        96,
        96);

    private static readonly DisplayMonitorSnapshot Primary125 = new(
        "\\\\.\\DISPLAY1",
        true,
        new DesktopPixelRect(0, 0, 1920, 1080),
        new DesktopPixelRect(0, 0, 1920, 1040),
        120,
        120);

    [Fact]
    public void TopologyRequiresConsecutiveMatchingSignatures()
    {
        var tracker = new DisplayTopologyStabilityTracker(2);

        Assert.Equal(
            DisplayTopologyProbeState.Candidate,
            tracker.Observe("A", [Primary100], DateTimeOffset.UtcNow).State);
        Assert.Equal(
            DisplayTopologyProbeState.Candidate,
            tracker.Observe("B", [Primary125], DateTimeOffset.UtcNow).State);
        DisplayTopologyProbeResult stable =
            tracker.Observe("B", [Primary125], DateTimeOffset.UtcNow);

        Assert.Equal(DisplayTopologyProbeState.Stable, stable.State);
        Assert.Equal(1, stable.Snapshot?.Generation);
        Assert.Equal("B", stable.Snapshot?.Signature);
    }

    [Fact]
    public void SameStableTopologyDoesNotAdvanceGeneration()
    {
        var tracker = new DisplayTopologyStabilityTracker(1);

        DisplayTopologyProbeResult first =
            tracker.Observe("A", [Primary100], DateTimeOffset.UtcNow);
        DisplayTopologyProbeResult unchanged =
            tracker.Observe("A", [Primary100], DateTimeOffset.UtcNow);

        Assert.Equal(1, first.Snapshot?.Generation);
        Assert.Equal(DisplayTopologyProbeState.Unchanged, unchanged.State);
        Assert.Equal(1, unchanged.Snapshot?.Generation);
    }

    [Fact]
    public async Task ServiceWaitsForStableChangedTopology()
    {
        var api = new FakeDisplayTopologyApi(
            ("A", new[] { Primary100 }),
            ("A", new[] { Primary100 }),
            ("B", new[] { Primary125 }),
            ("B", new[] { Primary125 }));
        var service = new DisplayTopologyService(api, new DisplayTopologyOptions
        {
            RequiredStableSamples = 2,
            MaximumProbeAttempts = 4,
            ProbeInterval = TimeSpan.Zero
        });

        DisplayTopologySnapshot? initial =
            await service.WaitForStableTopologyAsync();
        DisplayTopologySnapshot? changed =
            await service.WaitForStableTopologyAsync();

        Assert.NotNull(initial);
        Assert.NotNull(changed);
        Assert.Equal("A", initial.Signature);
        Assert.Equal(1, initial.Generation);
        Assert.Equal("B", changed.Signature);
        Assert.Equal(2, changed.Generation);
    }

    [Fact]
    public void CurrentVerificationUsesFreshNativeSignature()
    {
        var api = new FakeDisplayTopologyApi(("A", new[] { Primary100 }));
        var service = new DisplayTopologyService(api, new DisplayTopologyOptions
        {
            RequiredStableSamples = 1,
            MaximumProbeAttempts = 1,
            ProbeInterval = TimeSpan.Zero
        });
        DisplayTopologySnapshot snapshot = service.Probe().Snapshot!;

        api.SetCurrent("B", [Primary125]);

        Assert.False(service.IsCurrent(snapshot));
    }

    [Fact]
    public void GeometryUsesLogicalSizeAndTargetDpi()
    {
        var placement = new PosterPlacementState(420, 240, 0.5, 0.5);

        DesktopPixelRect at100 = PosterGeometryProjector.Project(placement, Primary100);
        DesktopPixelRect at125 = PosterGeometryProjector.Project(placement, Primary125);

        Assert.Equal(new DesktopPixelRect(750, 400, 420, 240), at100);
        Assert.Equal(new DesktopPixelRect(698, 370, 525, 300), at125);
    }

    [Theory]
    [InlineData(96, 750, 400, 420, 240)]
    [InlineData(120, 698, 370, 525, 300)]
    [InlineData(144, 645, 340, 630, 360)]
    [InlineData(192, 540, 280, 840, 480)]
    public void GeometryProjectsLogicalSizeAcrossFourDpiTiers(
        uint dpi,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var monitor = Primary100 with { DpiX = dpi, DpiY = dpi };
        var placement = new PosterPlacementState(420, 240, 0.5, 0.5);

        DesktopPixelRect actual = PosterGeometryProjector.Project(placement, monitor);

        Assert.Equal(
            new DesktopPixelRect(expectedX, expectedY, expectedWidth, expectedHeight),
            actual);
    }

    [Fact]
    [Trait("Contract", "LegacyClampProjection-NotSpatialRecoveryAcceptance")]
    public void GeometryPreservesRightBottomAnchorAndClampsToWorkArea()
    {
        var rightBottom = new PosterPlacementState(420, 240, 1, 1);
        var oversized = new PosterPlacementState(4000, 3000, 0.5, 0.5);

        Assert.Equal(
            new DesktopPixelRect(1500, 800, 420, 240),
            PosterGeometryProjector.Project(rightBottom, Primary100));
        Assert.Equal(
            Primary100.WorkArea,
            PosterGeometryProjector.Project(oversized, Primary100));
    }

    [Fact]
    public void RecoveryGateRejectsStaleRequestAndGenerationPair()
    {
        var gate = new DesktopRecoveryVersionGate();
        long first = gate.Signal();
        long second = gate.Signal();
        var expected = new DesktopRecoveryGenerationPair(3, 5);

        Assert.False(gate.CanCommit(first, expected, expected));
        Assert.True(gate.CanCommit(second, expected, expected));
        Assert.False(gate.CanCommit(
            second,
            expected,
            new DesktopRecoveryGenerationPair(4, 5)));
    }

    private sealed class FakeDisplayTopologyApi : IDisplayTopologyApi
    {
        private readonly Queue<(string Signature, IReadOnlyList<DisplayMonitorSnapshot> Monitors)> _samples;
        private (string Signature, IReadOnlyList<DisplayMonitorSnapshot> Monitors) _current;

        public FakeDisplayTopologyApi(
            params (string Signature, IReadOnlyList<DisplayMonitorSnapshot> Monitors)[] samples)
        {
            _samples = new Queue<(string, IReadOnlyList<DisplayMonitorSnapshot>)>(samples);
            _current = samples.Last();
        }

        public void SetCurrent(
            string signature,
            IReadOnlyList<DisplayMonitorSnapshot> monitors)
        {
            _samples.Clear();
            _current = (signature, monitors);
        }

        public bool TryCapture(
            out string signature,
            out IReadOnlyList<DisplayMonitorSnapshot> monitors)
        {
            if (_samples.Count > 0)
            {
                _current = _samples.Dequeue();
            }
            signature = _current.Signature;
            monitors = _current.Monitors;
            return true;
        }
    }
}
