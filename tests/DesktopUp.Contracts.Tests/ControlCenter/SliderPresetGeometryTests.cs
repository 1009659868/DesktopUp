using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class SliderPresetGeometryTests
{
    [Theory]
    [InlineData(.8, 0)] [InlineData(1, 2d / 7)] [InlineData(1.5, 1)]
    public void Scale_marks_follow_value_not_equal_thirds(double value, double ratio)
    {
        Assert.Equal(20 + 12 + (320 - 24) * ratio, SliderPresetGeometry.Center(.8, 1.5, value, 20, 320, 24), 8);
    }
    [Theory]
    [InlineData(160, 16)] [InlineData(320, 24)] [InlineData(280, 32)]
    public void Spacing_standard_and_endpoints_follow_native_thumb_centers(double width, double thumb)
    {
        Assert.Equal(thumb / 2, SliderPresetGeometry.Center(.5, 1.5, .5, 0, width, thumb));
        Assert.Equal(width / 2, SliderPresetGeometry.Center(.5, 1.5, 1, 0, width, thumb));
        Assert.Equal(width - thumb / 2, SliderPresetGeometry.Center(.5, 1.5, 1.5, 0, width, thumb));
    }
    [Theory]
    [InlineData(100, 16, 0, true)] [InlineData(103, 18, 3, true)]
    [InlineData(105, 16, 5, false)] [InlineData(100, 16, 30, false)]
    [InlineData(double.NaN, 16, 0, false)]
    public void Only_stationary_clicks_snap_not_drag_or_returned_drag(double endX, double endY, double travel, bool expected) =>
        Assert.Equal(expected, SliderPresetGeometry.IsPresetClick(100, 16, endX, endY, travel, 100, 16));
    [Theory]
    [InlineData(108, 16)] [InlineData(100, 29)] [InlineData(150, 16)]
    public void Non_marker_track_clicks_remain_native_fine_values(double x, double y) =>
        Assert.False(SliderPresetGeometry.IsPresetClick(x, y, x, y, 0, 100, 16));
    [Theory]
    [InlineData(24, 24)] [InlineData(0, 24)] [InlineData(320, 0)] [InlineData(double.NaN, 24)]
    public void Unready_track_cannot_invent_hit_coordinates(double width, double thumb) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SliderPresetGeometry.Center(.8, 1.5, 1, 0, width, thumb));
}
