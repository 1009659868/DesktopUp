namespace DesktopUp.Domain;

// Semantic, per-poster preferences; never heat scores, viewport dimensions or tick indices.
public sealed record PosterContentPreferences(double Scale = 1, double Spacing = 1, int MappingVersion = 1);
public sealed record PosterItemVisualInput(string ItemId, double RelativeScale, int Order, bool Pinned = false);
public sealed record PosterItemVisualMetrics(PosterItemVisualInput Item, PosterContentParameters Content);

public static class PosterContentPreferencePolicy
{
    public const double MinimumScale = .8, MaximumScale = 1.5;
    public const double MinimumSpacing = .5, MaximumSpacing = 1.5;
    public const int KeyboardSettleMilliseconds = 350;
    public static IReadOnlyList<double> ScalePresets { get; } = Array.AsReadOnly(new[] { .8, 1d, 1.5 });
    public static IReadOnlyList<double> SpacingPresets { get; } = Array.AsReadOnly(new[] { .5, 1d, 1.5 });

    public static string? Validate(PosterContentPreferences? value) => value is null ? null :
        value.MappingVersion != 1 ? "Unknown content preference mapping version." :
        !In(value.Scale, MinimumScale, MaximumScale) || !In(value.Spacing, MinimumSpacing, MaximumSpacing)
            ? "Content scale must be 0.8–1.5 and spacing 0.5–1.5." : null;

    public static PosterContentParameters Resolve(PosterContentParameters basis, PosterContentPreferences? preferences,
        double relativeScale = 1)
    {
        if (Validate(preferences) is { } error) throw new ArgumentException(error);
        if (!In(relativeScale, .5, 3)) throw new ArgumentOutOfRangeException(nameof(relativeScale));
        var p = preferences ?? new();
        double scale = p.Scale * relativeScale;
        return basis with
        {
            ItemWidthDip = basis.ItemWidthDip * scale,
            ItemHeightDip = basis.ItemHeightDip * scale,
            IconSizeDip = basis.IconSizeDip * scale,
            LabelFontSizeDip = Math.Clamp(basis.LabelFontSizeDip * Math.Sqrt(scale), 9, 24),
            GapDip = basis.GapDip * p.Spacing
        };
    }

    // Accepts already-ranked heat/manual output. This adapter never sorts or computes activity.
    // A relative scale of 1 is neutral; a future heat engine normalizes its own default once.
    public static IReadOnlyList<PosterItemVisualMetrics> ResolveItems(PosterPresentationParameters parameters,
        IReadOnlyList<PosterItemVisualInput> items)
    {
        if (items.Any(i => string.IsNullOrWhiteSpace(i.ItemId)) || items.Select(i => i.ItemId).Distinct().Count() != items.Count)
            throw new ArgumentException("Unique stable item identities are required.");
        return items.Select(i => new PosterItemVisualMetrics(i, Resolve(parameters.Content, parameters.Preferences, i.RelativeScale))).ToArray();
    }
    private static bool In(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
}
