namespace DesktopUp.Domain;

// One bounded built-in scene. These are per-poster values, never global theme state.
public sealed record LampTemplateParameters(string LightColor = "#FFDC99", double Softness = 60,
    double Intensity = 70, string Background = "#17221E", string Card = "#354237",
    double BackgroundOpacity = 100, bool MotionEnabled = true, bool FollowPointer = true,
    double IdleSway = 35, double MotionSpeed = 40);

public static class LampTemplatePolicy
{
    public const string TemplateKey = "lamp";
    public static LampTemplateParameters Defaults => new();
    public static string? Validate(LampTemplateParameters? value)
    {
        if (value is null) return null;
        if (!Color(value.LightColor) || !Color(value.Background) || !Color(value.Card))
            return "Lamp colors must use #RRGGBB.";
        if (!Percent(value.Softness) || !Percent(value.Intensity) || !Percent(value.BackgroundOpacity) ||
            !Percent(value.IdleSway) || !Percent(value.MotionSpeed))
            return "Lamp percentages must be finite numbers in 0..100.";
        return null;
    }

    private static bool Percent(double value) => double.IsFinite(value) && value is >= 0 and <= 100;
    private static bool Color(string? value) => value is { Length: 7 } && value[0] == '#' &&
        !value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF");
}
