using DesktopUp.Domain;

namespace DesktopUp.Application;

public readonly record struct LampScenePoint(double X, double Y);
public readonly record struct LampScenePose(double X, double Y, double FixtureAngle, bool FollowingPointer);

/// <summary>Bounded decorative motion only. It never changes item geometry, hit regions or saved values.</summary>
public static class LampSceneMotion
{
    public static LampScenePose Resolve(double width, double height, double seconds,
        LampTemplateParameters parameters, bool canAnimate, LampScenePoint? pointer)
    {
        width = FiniteSize(width); height = FiniteSize(height);
        bool animate = canAnimate && parameters.MotionEnabled;
        bool follow = animate && parameters.FollowPointer && pointer is { } point &&
            double.IsFinite(point.X) && double.IsFinite(point.Y) &&
            point.X >= 0 && point.X <= width && point.Y >= 0 && point.Y <= height;
        double x = width * .55, y = height * .47, angle = 0;
        if (follow)
        {
            x = pointer!.Value.X; y = pointer.Value.Y;
            angle = Math.Clamp((x - width * .75) / 15, -13, 13);
        }
        else if (animate && parameters.IdleSway > 0)
        {
            double time = double.IsFinite(seconds) ? Math.Max(0, seconds) : 0;
            double strength = Percent(parameters.IdleSway) / 100;
            double phase = time * (.35 + Percent(parameters.MotionSpeed) * .0145);
            double sway = Math.Sin(phase);
            angle = sway * 4 * strength;
            x += sway * Math.Min(24, width * .06) * strength;
            y += Math.Sin(phase * 2) * 6 * strength;
        }
        return new(ClampInset(x, width, 8, 8), ClampInset(y, height, 66, 16), angle, follow);
    }

    private static double ClampInset(double value, double size, double start, double end) =>
        Math.Clamp(value, Math.Min(start, size / 2), Math.Max(Math.Min(start, size / 2), size - Math.Min(end, size / 2)));
    private static double FiniteSize(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    private static double Percent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
}
