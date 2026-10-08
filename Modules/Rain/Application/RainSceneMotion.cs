namespace DesktopUp.Application;

public readonly record struct RainSceneEnvelope(double Left, double Top, double Width, double Height, double Bottom);
public readonly record struct RainCloudPose(double X, double Y, double Angle, double Vapor = 0, double Wisp = 0);
public readonly record struct RainDropPose(double X, double Y, double Length, double Slant);

/// <summary>Bounded decorative math, not an item layout solver or timer.</summary>
public static class RainSceneMotion
{
    public const int PoolSize = 64;
    private static double Finite(double value, double fallback = 0) => double.IsFinite(value) ? value : fallback;
    public static RainSceneEnvelope Envelope(SceneFrameContext frame)
    {
        double width = Math.Max(0, Finite(frame.Width)), height = Math.Max(0, Finite(frame.Height));
        if (frame.TitleChrome == DesktopUp.Domain.PosterTitleChromeKind.Cloud)
            return new(Math.Min(12, width / 2), 0, Math.Max(0, width - 24), Math.Min(64, height),
                Math.Clamp(Finite(frame.FooterTop, height), 0, height));
        double top = Math.Clamp(Finite(frame.HeaderBottom) + 4, 0, height);
        double room = Math.Max(0, Math.Min(height, Finite(frame.ContentTop)) - top - 28);
        return new(Math.Min(28, width / 2), top, Math.Max(0, width - 56), Math.Min(104, room),
            Math.Clamp(Finite(frame.FooterTop, height), top, height));
    }
    public static RainCloudPose Cloud(double seconds, double wind)
    {
        double strength = Math.Clamp(Finite(wind), 0, 100) / 100;
        double time = Math.Max(0, Finite(seconds));
        // Slow wind: rise/fall and rocking carry more weight than lateral drift.
        // Rock and lift are a quarter-cycle apart, so their peaks do not compete
        // for the compact title's entire vertical budget at the same instant.
        static double Ease(double t, double period) => (1-Math.Cos(t%period/period*Math.Tau))/2;
        double phase = time%19/19*Math.Tau;
        return new(5*Math.Sin(phase)*strength, 6*Math.Sin(phase)*strength, 3.2*Math.Cos(phase)*strength,
            (Ease(time,27)*2-1)*strength, (1-Ease(time+9,23)*2)*strength);
    }
    public static RainDropPose Drop(int index, double seconds, double speed, double wind, RainSceneEnvelope area)
    {
        if (index is < 0 or >= PoolSize) throw new ArgumentOutOfRangeException(nameof(index));
        speed = Math.Clamp(Finite(speed, 1), .5, 2);
        double length = 17 + index * 7 % 23, start = area.Top + area.Height - 12;
        double travel = Math.Max(1, area.Bottom - start + length);
        double period = (1.5 + index % 7 * .17) / speed;
        double phase = (Math.Max(0, Finite(seconds)) / period + index * .173) % 1;
        return new(area.Left + area.Width * (.06 + index * 37 % 89 / 100.0),
            start + phase * travel - length, length, Math.Clamp(Finite(wind), 0, 100) * .025);
    }
    // One soft pulse per 23 seconds, not a full-window flash or repeated strobe.
    public static double Lightning(double seconds, bool enabled)
    {
        if (!enabled) return 0;
        double phase = Math.Max(0, Finite(seconds)) % 23;
        return phase is >= 17 and <= 17.7 ? .24 * Math.Sin((phase - 17) / .7 * Math.PI) : 0;
    }
}

/// <summary>Tracks active shared-clock time; pauses and clock resets never cause catch-up jumps.</summary>
public sealed class SceneActiveTime
{
    private double? _last;
    public double Seconds { get; private set; }
    public void Stop() => _last = null;
    public double Step(double seconds, bool running)
    {
        if (!running || !double.IsFinite(seconds)) { Stop(); return Seconds; }
        if (_last is { } last && seconds >= last) Seconds += Math.Min(.1, seconds - last);
        _last = seconds; return Seconds;
    }
}
