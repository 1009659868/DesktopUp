using DesktopUp.Domain;

namespace DesktopUp.Application;

// Refines the already accepted path, never chooses new endpoints or changes return eligibility.
public sealed record SpatialAnimationSchedule(IReadOnlyList<SpatialBatchFrame> Frames,
    IReadOnlyList<double> DueMilliseconds, double NominalDurationMs)
{
    public SpatialSnapshot? Initial { get; init; }
    public IReadOnlySet<int> SegmentEnds { get; init; } = new HashSet<int>();
    public bool CoalescePresentation { get; init; }

    public static SpatialAnimationSchedule CreatePresentation(SpatialSnapshot initial,
        IReadOnlyList<SpatialBatchFrame> keyframes, PosterAnimationOptions options)
    {
        var proof = Create(initial, keyframes, options);
        if (proof.Frames.Count == 0) return proof;
        var distances = proof.Frames.Select(f => f.Changes.Max(c => EdgeDistance(c.From, c.To))).ToArray();
        double total = distances.Sum(), travelled = 0;
        var due = new double[distances.Length];
        for (int i = 0; i < due.Length; i++)
        {
            travelled += distances[i]; double progress = travelled / total;
            due[i] = options.DurationMs * (options.Easing == "linear" ? progress : Math.Acos(1 - 2 * progress) / Math.PI);
        }
        return proof with { DueMilliseconds = Array.AsReadOnly(due), NominalDurationMs = options.DurationMs, CoalescePresentation = true };
    }

    public static SpatialAnimationSchedule Create(SpatialSnapshot initial,
        IReadOnlyList<SpatialBatchFrame> keyframes, PosterAnimationOptions options)
    {
        options.Validate();
        const int maximumFrames = 16384;
        const long maximumChecks = 4_000_000;
        var frames = new List<SpatialBatchFrame>();
        var distances = new List<long>();
        var ends = new HashSet<int>();
        var positions = initial.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        var visible = initial.Posters.Where(p => p.IsVisible).Select(p => p.PosterId).ToArray();
        long checks = 0, distance = 0;
        foreach (var key in keyframes)
        {
            if (key.Changes.Select(c => c.PosterId).Distinct(StringComparer.Ordinal).Count() != key.Changes.Count ||
                key.Changes.Any(c => !positions.TryGetValue(c.PosterId, out var r) || r != c.From))
                throw new ArgumentException("Animation path is discontinuous.");
            long extent = key.Changes.Select(c => EdgeDistance(c.From, c.To)).DefaultIfEmpty().Max();
            if (extent == 0) continue;
            long subdivisions = (extent + options.MaximumStepPixels - 1) / options.MaximumStepPixels;
            if (subdivisions > maximumFrames - frames.Count ||
                (checks += subdivisions * visible.Length * (long)visible.Length) > maximumChecks)
                throw new ArgumentException("Animation refinement budget exceeded.");
            for (int i = 1; i <= subdivisions; i++)
            {
                var next = new Dictionary<string, SpatialRect>(positions, StringComparer.Ordinal);
                foreach (var leg in key.Changes) next[leg.PosterId] = Interpolate(leg.From, leg.To, i, subdivisions);
                var changes = key.Changes.Where(c => positions[c.PosterId] != next[c.PosterId])
                    .Select(c => new SpatialPathLeg(c.PosterId, positions[c.PosterId], next[c.PosterId])).ToArray();
                if (changes.Length == 0) continue;
                if (changes.Any(c => !initial.Environment.WorkArea.Contains(c.To) ||
                        EdgeDistance(c.From, c.To) > options.MaximumStepPixels))
                    throw new ArgumentException("Unsafe animation subdivision.");
                for (int a = 0; a < visible.Length; a++)
                for (int b = a + 1; b < visible.Length; b++)
                    if (SpatialPathPolicy.CrossesMoving(positions[visible[a]], next[visible[a]],
                        positions[visible[b]], next[visible[b]]))
                        throw new ArgumentException("Animation subdivision crosses a surface.");
                distance += changes.Max(c => EdgeDistance(c.From, c.To));
                frames.Add(new(Array.AsReadOnly(changes))); distances.Add(distance); positions = next;
            }
            ends.Add(frames.Count);
        }
        var due = new List<double>(); double previous = 0;
        foreach (long d in distances)
        {
            double progress = d / (double)distance;
            double time = options.Easing == "linear" ? progress : Math.Acos(1 - 2 * progress) / Math.PI;
            previous = Math.Max(previous + options.FrameIntervalMs, time * options.DurationMs);
            due.Add(previous);
        }
        return new(frames.AsReadOnly(), due.AsReadOnly(), previous) { Initial = initial, SegmentEnds = ends };
    }

    public static long EdgeDistance(SpatialRect a, SpatialRect b) =>
        new[] { Math.Abs((long)b.X - a.X), Math.Abs((long)b.Y - a.Y),
            Math.Abs(b.Right - a.Right), Math.Abs(b.Bottom - a.Bottom) }.Max();

    private static SpatialRect Interpolate(SpatialRect a, SpatialRect b, long i, long count)
    {
        // Floor shared absolute edges, including negative monitor coordinates. Coincident edges
        // remain identical; interpolating each rectangle's height separately can introduce seams.
        long Blend(long x, long y)
        {
            long n = (y - x) * i, q = Math.DivRem(n, count, out long remainder);
            return x + q - (remainder < 0 ? 1 : 0);
        }
        long x = Blend(a.X, b.X), y = Blend(a.Y, b.Y);
        return new(checked((int)x), checked((int)y), checked((int)(Blend(a.Right, b.Right) - x)),
            checked((int)(Blend(a.Bottom, b.Bottom) - y)));
    }
}

public sealed class SpatialAnimationCursor(SpatialAnimationSchedule schedule, PosterAnimationOptions options)
{
    public int Index { get; private set; }
    public bool Complete => Index == schedule.Frames.Count;
    private double _lastSubmission = double.NegativeInfinity;
    private readonly Dictionary<string, SpatialRect>? _positions = schedule.Initial?.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
    public bool TryTake(double elapsedMs, out SpatialBatchFrame? frame)
    {
        frame = null;
        if (!double.IsFinite(elapsedMs) || elapsedMs < 0 || Complete ||
            elapsedMs < schedule.DueMilliseconds[Index] || elapsedMs - _lastSubmission < options.FrameIntervalMs) return false;
        frame = schedule.Frames[Index++];
        if (schedule.CoalescePresentation && _positions is not null && schedule.Initial is { } initial)
        {
            var merged = frame.Changes.ToDictionary(c => c.PosterId, StringComparer.Ordinal);
            while (!Complete && !schedule.SegmentEnds.Contains(Index) && schedule.DueMilliseconds[Index] <= elapsedMs)
            {
                var candidate = new Dictionary<string, SpatialPathLeg>(merged, StringComparer.Ordinal);
                foreach (var change in schedule.Frames[Index].Changes)
                    candidate[change.PosterId] = new(change.PosterId, _positions[change.PosterId], change.To);
                var visible = initial.Posters.Where(p => p.IsVisible).Select(p => p.PosterId).ToArray();
                bool safe = true;
                for (int a = 0; a < visible.Length && safe; a++)
                    for (int b = a + 1; b < visible.Length && safe; b++)
                        safe = !SpatialPathPolicy.CrossesMoving(_positions[visible[a]], candidate.GetValueOrDefault(visible[a])?.To ?? _positions[visible[a]],
                            _positions[visible[b]], candidate.GetValueOrDefault(visible[b])?.To ?? _positions[visible[b]]);
                if (!safe) break;
                merged = candidate; Index++;
            }
            frame = new(Array.AsReadOnly(merged.Values.ToArray()));
        }
        if (_positions is not null) foreach (var change in frame.Changes) _positions[change.PosterId] = change.To;
        _lastSubmission = elapsedMs;
        return true; // One native group submission; never cross an accepted path turn or burst.
    }
}
