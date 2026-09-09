using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp.Testing;

// Shared by contract tests and the on-disk fixture generator, never production defaults.
public static class MultiPosterFixtureData
{
    public static PosterDefinition Create(int index)
    {
        if (index is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(index));
        string template = index == 0 ? "night-ops" : "basic-box";
        var defaults = PosterPresentationPolicy.Defaults(template);
        var parameters = defaults with
        {
            Content = defaults.Content with { IconSizeDip = 24 + index * 8, GapDip = 8 + index * 4 },
            Theme = defaults.Theme with { Background = new[] { "#182B36", "#282B30", "#243329" }[index] }
        };
        return new(BuiltInStateIdentities.PrimaryPosterId,
            new[] { "A / Games", "B / Documents", "C / Applications" }[index],
            "M06: six demo placeholders, no files collected", "DISPLAY1",
            0, 0, 300, new double[] { 480, 420, 520 }[index],
            new double[] { .05, .42, .78 }[index], .1, .15, .45,
            index == 1, index == 2, false, "adaptive-grid", PosterPresentationPolicy.Serialize(parameters), template);
    }
}
