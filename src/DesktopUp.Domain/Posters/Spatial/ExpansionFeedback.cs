namespace DesktopUp.Domain;

// Visual-only feedback; deliberately has no rectangle, input, focus or state-write port.
public sealed class ExpansionFeedback
{
    public const int DurationMilliseconds = 280;
    private long? _started;
    private bool _animate;
    public bool TryStart(long now, bool animationsEnabled)
    {
        if (_started is { } start && now - start < DurationMilliseconds) return false;
        _started = now; _animate = animationsEnabled; return true;
    }
    public (bool Active, double OffsetDip) Sample(long now)
    {
        if (_started is not { } start || now - start >= DurationMilliseconds) { Reset(); return (false, 0); }
        int phase = (int)Math.Clamp((now - start) / 40, 0, 6);
        double[] offsets = [0, -2, 2, -1.5, 1, -0.5, 0];
        return (true, _animate ? offsets[phase] : 0);
    }
    public void Reset() => _started = null;
}
