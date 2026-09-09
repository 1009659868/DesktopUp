using DesktopUp.Testing;

namespace DesktopUp.Contracts.Tests;

public sealed class MeasurementContractTests
{
    [Fact] public void QuantileUsesNearestRankAndDoesNotMutate()
    {
        double[] values = [4, 1, 3, 2];
        Assert.Equal(2, DesktopUpMeasurementMath.Quantile(values, .5));
        Assert.Equal(4, DesktopUpMeasurementMath.Quantile(values, .95));
        Assert.Equal(1, DesktopUpMeasurementMath.Quantile(values, 0));
        Assert.Equal(new double[] { 4, 1, 3, 2 }, values);
        Assert.Throws<ArgumentException>(() => DesktopUpMeasurementMath.Quantile([], .5));
        Assert.Throws<ArgumentException>(() => DesktopUpMeasurementMath.Quantile([double.NaN], .5));
    }
    [Fact] public void CpuUsesElapsedDeltaAndExplicitNormalization()
    {
        Assert.Equal(1, DesktopUpMeasurementMath.CpuPercent(100, 10000, 1));
        Assert.Equal(.0625, DesktopUpMeasurementMath.CpuPercent(100, 10000, 16));
        Assert.Throws<ArgumentException>(() => DesktopUpMeasurementMath.CpuPercent(-1, 1, 1));
        Assert.Throws<ArgumentException>(() => DesktopUpMeasurementMath.CpuPercent(1, 0, 1));
        Assert.Throws<ArgumentException>(() => DesktopUpMeasurementMath.CpuPercent(1, 1, 0));
    }
    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(6)]
    public void FixtureUsesStableParametersAndGridAcrossPostures(int count)
    {
        var positions = new HashSet<(double, double)>();
        for (int i = 0; i < count; i++)
        {
            var expanded = MeasurementFixtureData.Create(i, count, "expanded");
            Assert.True(positions.Add((expanded.HorizontalAnchor, expanded.VerticalAnchor)));
            Assert.False(expanded.IsLocked); Assert.True(expanded.IsVisible);
            Assert.Equal(expanded with { IsCollapsed = true }, MeasurementFixtureData.Create(i, count, "collapsed"));
            Assert.Equal(expanded with { IsVisible = false }, MeasurementFixtureData.Create(i, count, "hidden"));
        }
    }
    [Fact] public void FixtureRejectsUnsupportedCapacityAndState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MeasurementFixtureData.Create(0, 4, "expanded"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeasurementFixtureData.Create(6, 6, "expanded"));
        Assert.Throws<ArgumentException>(() => MeasurementFixtureData.Create(0, 1, "invalid"));
    }
}
