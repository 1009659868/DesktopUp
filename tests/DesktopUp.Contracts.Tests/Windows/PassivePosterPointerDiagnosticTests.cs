using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    [Fact]
    public void Real_acceptance_sample_reports_its_occluder_without_requerying_or_mutating_input()
    {
        var fixture = new Fixture(); fixture.Native.Point = new(-250, 300); fixture.Native.Cover.Add(99);
        var before = fixture.Router.ReadSurface(A);
        var result = fixture.Sample();
        var diagnostic = fixture.Observer.LastDiagnostics!;
        Assert.Equal(result.Sequence, diagnostic.Sequence);
        Assert.Equal(fixture.Native.Point, diagnostic.Position);
        Assert.Equal("native-window-occludes", diagnostic.Posters.Single().Reason);
        Assert.Equal("0x63", diagnostic.Posters.Single().OccludingWindow);
        Assert.Equal(AutoPosturePresence.Outside, diagnostic.Posters.Single().Presence);
        Assert.Equal(1, fixture.Native.CursorReads); Assert.Equal(1, fixture.Native.OrderReads);
        Assert.Equal(before, fixture.Router.ReadSurface(A)); Assert.Null(fixture.Router.Capture);
    }

    [Fact]
    public void Transparent_blank_inside_reports_acceptance_without_an_input_hit()
    {
        var fixture = new Fixture(); fixture.Native.Point = new(-250, 300); fixture.Native.Receiver = 90;
        fixture.Sample();
        Assert.Equal("inside-unoccluded-published-frame", fixture.Observer.LastDiagnostics!.Posters.Single().Reason);
        Assert.Equal(AutoPosturePresence.Inside, fixture.Observer.LastDiagnostics.Posters.Single().Presence);
        Assert.Null(fixture.Router.Resolve(-250, 300, [A]));
    }

    [Theory]
    [InlineData("surface", "native-surface-mismatch")]
    [InlineData("order", "surface-missing-from-z-order")]
    [InlineData("cursor", "native-sample-unreliable")]
    [InlineData("desktop", "desktop-identity-stale")]
    [InlineData("receiver", "receiver-not-poster-or-verified-desktop")]
    public void Diagnostics_distinguish_rejection_reasons_without_granting_hover(string fault, string reason)
    {
        var fixture = new Fixture(); fixture.Native.Point = new(-250, 300);
        switch (fault)
        {
            case "surface": fixture.Native.SurfaceOk = false; break;
            case "order": fixture.Native.Order = [99, 90]; break;
            case "cursor": fixture.Native.CursorOk = false; break;
            case "desktop": fixture.Native.DesktopOk = false; break;
            case "receiver": fixture.Native.Receiver = 99; break;
        }
        fixture.Sample();
        Assert.Equal(reason, fixture.Observer.LastDiagnostics!.Posters.Single().Reason);
        Assert.NotEqual(AutoPosturePresence.Inside, fixture.Observer.LastDiagnostics.Posters.Single().Presence);
    }

    [Fact]
    public void Stale_sequence_keeps_the_prior_diagnostic_and_does_not_read_native_again()
    {
        var fixture = new Fixture(); var sample = fixture.Sample(); var diagnostic = fixture.Observer.LastDiagnostics;
        fixture.Native.Point = new(600, 700);
        fixture.Observer.Sample(sample.Sequence, sample.EnvironmentEpoch, fixture.Targets);
        Assert.Same(diagnostic, fixture.Observer.LastDiagnostics);
        Assert.Equal(1, fixture.Native.CursorReads);
    }
}
