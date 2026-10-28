using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    [Fact]
    public void Decorative_position_is_from_the_same_completed_sample_without_extra_native_reads()
    {
        var fixture = new Fixture();
        fixture.Native.Point = new(-250, 300);
        var sample = fixture.Sample();
        Assert.Equal(AutoPosturePresence.Inside, sample.Posters.Single().Pointer.Presence);
        Assert.Equal(fixture.Native.Point, fixture.Observer.LastPosition);
        Assert.Equal(1, fixture.Native.CursorReads);
        fixture.Native.Point = new(-240, 310);
        Assert.Same(sample, fixture.Observer.Sample(sample.Sequence, sample.EnvironmentEpoch, fixture.Targets));
        Assert.Equal(new PointerScreenPoint(-250, 300), fixture.Observer.LastPosition);
        Assert.Equal(1, fixture.Native.CursorReads);
        fixture.Sample();
        Assert.Equal(fixture.Native.Point, fixture.Observer.LastPosition);
        Assert.Equal(2, fixture.Native.CursorReads);
    }

    [Fact]
    public void Unreliable_or_zero_demand_sample_clears_the_decorative_position()
    {
        var fixture = new Fixture(); fixture.Sample();
        Assert.NotNull(fixture.Observer.LastPosition);
        fixture.Native.CursorOk = false; fixture.Sample();
        Assert.Null(fixture.Observer.LastPosition);
        fixture.Native.CursorOk = true; fixture.Sample();
        Assert.NotNull(fixture.Observer.LastPosition);
        fixture.Targets = fixture.Targets.Select(target => target with { Enabled = false }).ToList();
        fixture.Sample(); Assert.Null(fixture.Observer.LastPosition);
        Assert.Null(fixture.Router.Capture);
    }
}
