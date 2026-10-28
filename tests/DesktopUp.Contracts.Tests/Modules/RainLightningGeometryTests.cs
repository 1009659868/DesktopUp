using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class RainLightningGeometryTests
{
    [Theory]
    [InlineData(240, 180)] [InlineData(300, 600)] [InlineData(1600, 2400)]
    public void Main_stroke_spans_whole_weather_area_with_bounded_branches(double width, double bottom)
    {
        var area = new RainSceneEnvelope(12, 0, width - 24, 64, bottom);
        var paths = RainLightningGeometry.Resolve(area);
        Assert.Equal(3, paths.Count); Assert.Equal(16, paths.Sum(p => p.Count));
        Assert.Equal(59, paths[0][0].Y); Assert.Equal(bottom, paths[0][^1].Y);
        Assert.All(paths.SelectMany(p => p), p => { Assert.InRange(p.X, area.Left, area.Left + area.Width); Assert.InRange(p.Y, 59, bottom); });
        Assert.Equal(paths[0][2], paths[1][0]); Assert.Equal(paths[0][4], paths[2][0]);
        Assert.Equal(paths.SelectMany(p => p), RainLightningGeometry.Resolve(area).SelectMany(p => p));
    }

    [Theory]
    [InlineData(double.NaN, 600)] [InlineData(300, double.PositiveInfinity)] [InlineData(0, 600)] [InlineData(300, 50)]
    public void Invalid_or_no_weather_space_has_no_stroke(double width, double bottom) =>
        Assert.Empty(RainLightningGeometry.Resolve(new(12, 0, width, 64, bottom)));
}
