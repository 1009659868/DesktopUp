using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class LampSceneMotionTests
{
    [Fact]
    public void Pointer_moves_light_and_clamps_fixture_angle_without_changing_parameters()
    {
        var value = new LampTemplateParameters();
        var left = LampSceneMotion.Resolve(400, 600, 1, value, true, new(20, 180));
        var right = LampSceneMotion.Resolve(400, 600, 2, value, true, new(390, 520));
        Assert.Equal(20, left.X); Assert.Equal(180, left.Y); Assert.Equal(-13, left.FixtureAngle);
        Assert.Equal(390, right.X); Assert.Equal(520, right.Y);
        Assert.True(left.FollowingPointer && right.FollowingPointer);
        Assert.Equal(new LampTemplateParameters(), value);
    }

    [Fact]
    public void Leaving_poster_returns_to_bounded_idle_sway()
    {
        var value = new LampTemplateParameters(IdleSway: 100, MotionSpeed: 100);
        var zero = LampSceneMotion.Resolve(400, 600, 0, value, true, null);
        var idle = LampSceneMotion.Resolve(400, 600, .5, value, true, new(-1, 400));
        Assert.False(idle.FollowingPointer); Assert.NotEqual(zero.X, idle.X);
        Assert.InRange(idle.X, 196, 244); Assert.InRange(idle.Y, 276, 288);
        Assert.InRange(idle.FixtureAngle, -4, 4);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Reduced_motion_or_disabled_effect_remains_static(bool canAnimate, bool enabled)
    {
        var value = new LampTemplateParameters(MotionEnabled: enabled);
        var expected = LampSceneMotion.Resolve(400, 600, 0, value, false, null);
        Assert.Equal(expected, LampSceneMotion.Resolve(400, 600, 123, value, canAnimate, new(380, 450)));
    }

    [Fact]
    public void Follow_and_idle_controls_are_independent()
    {
        var value = new LampTemplateParameters(FollowPointer: false, IdleSway: 0);
        var a = LampSceneMotion.Resolve(400, 600, 3, value, true, new(350, 510));
        Assert.False(a.FollowingPointer); Assert.Equal(220, a.X, 6); Assert.Equal(282, a.Y, 6);
        Assert.Equal(0, a.FixtureAngle);
        var follow = LampSceneMotion.Resolve(400, 600, 3, value with { FollowPointer = true }, true, new(350, 510));
        Assert.True(follow.FollowingPointer); Assert.Equal(350, follow.X);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 25)]
    [InlineData(double.NaN, double.PositiveInfinity)]
    public void Tiny_or_invalid_surface_never_produces_invalid_geometry(double width, double height)
    {
        var pose = LampSceneMotion.Resolve(width, height, double.NaN, new(), true, new(5, 7));
        Assert.True(double.IsFinite(pose.X) && double.IsFinite(pose.Y) && double.IsFinite(pose.FixtureAngle));
        Assert.InRange(pose.X, 0, double.IsFinite(width) ? width : 0);
        Assert.InRange(pose.Y, 0, double.IsFinite(height) ? height : 0);
    }
}
