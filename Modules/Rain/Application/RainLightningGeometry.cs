namespace DesktopUp.Application;

public readonly record struct RainLightningPoint(double X, double Y);

/// <summary>A bounded weather stroke from the cloud base to the content foot, not a flash sheet.</summary>
public static class RainLightningGeometry
{
    public static IReadOnlyList<IReadOnlyList<RainLightningPoint>> Resolve(RainSceneEnvelope area, uint strokeSeed = 0)
    {
        if (!double.IsFinite(area.Left) || !double.IsFinite(area.Top) || !double.IsFinite(area.Width) ||
            !double.IsFinite(area.Height) || !double.IsFinite(area.Bottom) || area.Width <= 0 || area.Height <= 0)
            return [];
        double top = area.Top + area.Height - 5, bottom = area.Bottom;
        if (bottom <= top) return [];
        double center=.28+.44*RainLightningSchedule.Unit(strokeSeed,17);
        double spread=.65+.35*RainLightningSchedule.Unit(strokeSeed,31);
        double direction=(strokeSeed&1)==0 ? 1 : -1;
        RainLightningPoint P(double x, double y) => new(area.Left + area.Width *
            (strokeSeed==0 ? x : Math.Clamp(center+(x-.53)*spread*direction,.025,.975)), top + (bottom - top) * y);
        return [
            new[] { P(.61, 0), P(.49, .16), P(.56, .24), P(.38, .44), P(.47, .50), P(.30, .72), P(.39, .80), P(.27, 1) },
            new[] { P(.56, .24), P(.72, .33), P(.68, .38), P(.79, .48) },
            new[] { P(.47, .50), P(.61, .65), P(.56, .70), P(.65, .84) }
        ];
    }
}
