namespace DesktopUp.Domain;

// Strategy settings are independent of the poster's overall content scale and of raw usage facts.
public sealed record ContentActivityOptions(double MinimumScale = .5, double MaximumScale = 3,
    double DefaultScale = 1.5, double SmoothingStrength = 20)
{
    public string? Validate() =>
        !double.IsFinite(MinimumScale) || !double.IsFinite(MaximumScale) ||
        !double.IsFinite(DefaultScale) || !double.IsFinite(SmoothingStrength) ||
        MinimumScale < .5 || MaximumScale > 3 || MinimumScale > DefaultScale ||
        DefaultScale > MaximumScale || SmoothingStrength < 0 || SmoothingStrength > 10000
            ? "Invalid activity scale settings." : null;
}

public sealed record ContentActivityUsage(Guid ContentItemId, DateTimeOffset AddedAt,
    long TotalLaunches, DateTimeOffset? LastLaunchedAt);
public sealed record ContentActivityInput(string ItemId, int PreviousOrder, ContentActivityUsage Usage);
public sealed record ContentActivityScore(string ItemId, int Rank, double Score,
    double EffectiveLaunches, double RelativeScale);
public sealed record ContentActivityDecayResult(double EffectiveLaunches, double Multiplier,
    double HalfLifeDays, double DecayDays);

// Pure, replayable policy. It never changes stored counts, author sizes or the wall clock.
public static class ContentActivityPolicy
{
    public const int Version = 1;

    public static ContentActivityDecayResult Decay(ContentActivityUsage usage, DateTimeOffset now)
    {
        if (usage.ContentItemId == Guid.Empty || usage.TotalLaunches < 0 ||
            (usage.TotalLaunches == 0) != (usage.LastLaunchedAt is null))
            throw new ArgumentException("Invalid activity statistics.", nameof(usage));
        if (usage.TotalLaunches == 0) return new(0, 1, 7, 0);
        double resistance = Math.Min(1, Math.Log(1d + usage.TotalLaunches) / Math.Log(51));
        double halfLife = 7 + 21 * resistance * resistance;
        // The decay clock starts only after BOTH protections expire. Fractional days keep
        // the day-30/day-14 transitions continuous; future dates cannot create negative age.
        double elapsed = Math.Max(0, Math.Min(Age(usage.AddedAt, now) - 30,
            Age(usage.LastLaunchedAt!.Value, now) - 14));
        double multiplier = Math.Max(.1, Math.Pow(2, -elapsed / halfLife));
        return new(usage.TotalLaunches * multiplier, multiplier, halfLife, elapsed);
    }

    public static double Scale(double effective, double maximum, ContentActivityOptions options)
    {
        if (options.Validate() is { } error) throw new ArgumentException(error, nameof(options));
        if (!double.IsFinite(effective) || !double.IsFinite(maximum) || effective < 0 || maximum < effective)
            throw new ArgumentOutOfRangeException(nameof(effective));
        if (maximum == 0) return options.DefaultScale;
        double relative = options.MinimumScale + (options.MaximumScale - options.MinimumScale) *
            LogOnePlus(effective) / LogOnePlus(maximum);
        double weight = maximum / (maximum + options.SmoothingStrength);
        return Math.Clamp(options.DefaultScale + weight * (relative - options.DefaultScale),
            options.MinimumScale, options.MaximumScale);
    }

    public static IReadOnlyList<ContentActivityScore> Evaluate(IReadOnlyList<ContentActivityInput> items,
        ContentActivityOptions options, DateTimeOffset now)
    {
        if (options.Validate() is { } error) throw new ArgumentException(error, nameof(options));
        if (items.Count > ContentLayoutPolicy.MaximumItems ||
            items.Any(x => string.IsNullOrWhiteSpace(x.ItemId) || x.PreviousOrder < 0) ||
            items.Select(x => x.ItemId).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new ArgumentException("Invalid activity membership snapshot.", nameof(items));
        if (items.Count == 0) return Array.Empty<ContentActivityScore>();
        var values = items.Select(x => (Input: x, Effective: Decay(x.Usage, now).EffectiveLaunches,
            Age: Age(x.Usage.AddedAt, now))).ToArray();
        double maxEffective = values.Max(x => x.Effective), maxAge = Math.Max(1, values.Max(x => x.Age));
        var ranked = values.Select(x =>
        {
            double recency = x.Input.Usage.LastLaunchedAt is { } last ? 1 - Math.Min(Age(last, now) / 7, 1) : 0;
            double frequency = maxEffective == 0 ? 0 : LogOnePlus(x.Effective) / LogOnePlus(maxEffective);
            double newness = 1 - Math.Min(x.Age / maxAge, 1);
            return (Value: x, Score: .4 * recency + .35 * frequency + .25 * newness);
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Value.Input.PreviousOrder)
            .ThenBy(x => x.Value.Input.Usage.AddedAt).ThenBy(x => x.Value.Input.ItemId, StringComparer.Ordinal);
        return Array.AsReadOnly(ranked.Select((x, rank) => new ContentActivityScore(x.Value.Input.ItemId,
            rank, x.Score, x.Value.Effective, Scale(x.Value.Effective, maxEffective, options))).ToArray());
    }

    private static double Age(DateTimeOffset date, DateTimeOffset now) => Math.Max(0, (now - date).TotalDays);
    private static double LogOnePlus(double value)
    {
        double sum = 1 + value;
        // Preserve very small positive inputs even on runtimes whose LogP1 rounds 1+x to 1.
        return sum == 1 ? value : Math.Log(sum) * (value / (sum - 1));
    }
}
