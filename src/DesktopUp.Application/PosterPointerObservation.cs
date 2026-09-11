namespace DesktopUp.Application;

public readonly record struct PointerScreenPoint(int X, int Y);
public sealed record PosterPointerReading(PosterInputSessionKey Session, PosterInputSurfaceSnapshot? Surface,
    AutoPosturePresence Presence, AutoPostureProtection Protection = AutoPostureProtection.None);
public sealed record PosterPointerObserved(PosterInputSessionKey Session, AutoPosturePointer Pointer,
    AutoPostureProtection Protection);
public sealed record PosterPointerSnapshot(long Sequence, long EnvironmentEpoch,
    IReadOnlyList<PosterPointerObserved> Posters, AutoPostureProtection SharedProtection);

// Dispatcher-owned, bounded to the current complete session set. No clocks, subscriptions,
// commands, persistence or native calls. Geometry/occlusion changes alone never create intent.
public sealed class PosterPointerTracker
{
    private sealed record Previous(PosterPointerReading Reading, AutoPosturePointer Pointer);
    private Dictionary<string, Previous> _previous = new(StringComparer.Ordinal);
    private PointerScreenPoint? _position;
    public PosterPointerSnapshot Current { get; private set; } = new(0, 0, [], AutoPostureProtection.None);

    public PosterPointerSnapshot Observe(long sequence, long environmentEpoch, PointerScreenPoint? position,
        IReadOnlyList<PosterPointerReading> readings, AutoPostureProtection sharedProtection)
    {
        if (sequence <= Current.Sequence || environmentEpoch < Current.EnvironmentEpoch) return Current;
        if (environmentEpoch < 1) throw new ArgumentOutOfRangeException(nameof(environmentEpoch));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reading in readings)
            if (string.IsNullOrWhiteSpace(reading.Session.PosterId) || reading.Session.Epoch < 1 ||
                !ids.Add(reading.Session.PosterId) || !Enum.IsDefined(reading.Presence) ||
                (reading.Surface is not null && reading.Surface.Session != reading.Session))
                throw new ArgumentException("Invalid or duplicate pointer session.");

        var next = new Dictionary<string, Previous>(StringComparer.Ordinal);
        bool moved = position is { } now && _position is { } before && now != before;
        foreach (var reading in readings.OrderBy(r => r.Session.PosterId, StringComparer.Ordinal))
        {
            _previous.TryGetValue(reading.Session.PosterId, out var old);
            bool sameLifetime = old?.Reading.Session == reading.Session;
            long enter = sameLifetime ? old!.Pointer.EnterVersion : 0;
            long exit = sameLifetime ? old!.Pointer.ExitVersion : 0;
            // Keep counters across environment recovery, but reset the comparison baseline.
            // Unknown/native gaps are not evidence of leaving or re-entering.
            if (sameLifetime && Current.EnvironmentEpoch == environmentEpoch && moved &&
                old!.Reading.Presence != AutoPosturePresence.Unknown && reading.Presence != AutoPosturePresence.Unknown &&
                old.Reading.Surface is { } previousSurface && reading.Surface is { } surface &&
                SameGeometry(previousSurface, surface))
            {
                bool wasInside = PosterInputGeometry.Contains(surface, _position!.Value);
                bool isInside = PosterInputGeometry.Contains(surface, position!.Value);
                if (wasInside && !isInside) exit = checked(exit + 1);
                if (reading.Presence == AutoPosturePresence.ExpansionZone &&
                    !PosterInputGeometry.IsExpansionZone(surface, _position.Value)) enter = checked(enter + 1);
            }
            var stableReading = reading.Surface is { } published ? reading with
            {
                Surface = published with { Layout = published.Layout with
                    { Regions = Array.AsReadOnly(published.Layout.Regions.ToArray()) } }
            } : reading;
            next.Add(reading.Session.PosterId, new(stableReading, new(reading.Presence, enter, exit)));
        }
        _previous = next;
        _position = position;
        return Current = new(sequence, environmentEpoch,
            Array.AsReadOnly(next.Values.Select(p => new PosterPointerObserved(p.Reading.Session, p.Pointer,
                p.Reading.Protection)).ToArray()), sharedProtection);
    }

    private static bool SameGeometry(PosterInputSurfaceSnapshot a, PosterInputSurfaceSnapshot b) =>
        a.Frame == b.Frame && a.Layout.OuterCornerRadius == b.Layout.OuterCornerRadius &&
        a.Layout.Regions.SequenceEqual(b.Layout.Regions);
}
