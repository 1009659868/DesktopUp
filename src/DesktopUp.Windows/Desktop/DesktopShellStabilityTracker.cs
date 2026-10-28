using DesktopUp.Application;

namespace DesktopUp.Windows;

internal sealed class DesktopShellStabilityTracker(int requiredStableSamples)
{
    private readonly int _requiredStableSamples = Math.Max(1, requiredStableSamples);
    private DesktopShellHostIdentity? _candidate;
    private int _consecutiveSamples;
    private long _generation;

    public DesktopShellHostSnapshot? Current { get; private set; }

    public DesktopShellProbeResult Observe(
        DesktopShellHostIdentity? observed,
        DateTimeOffset observedAtUtc)
    {
        if (observed is null || observed.Value.IsEmpty)
        {
            ResetCandidate();
            return new DesktopShellProbeResult(
                DesktopShellProbeState.Missing,
                Current,
                null,
                0);
        }

        DesktopShellHostIdentity identity = observed.Value;
        if (Current?.Identity == identity)
        {
            ResetCandidate();
            return new DesktopShellProbeResult(
                DesktopShellProbeState.Unchanged,
                Current,
                identity,
                _requiredStableSamples);
        }

        if (_candidate != identity)
        {
            _candidate = identity;
            _consecutiveSamples = 1;
        }
        else
        {
            _consecutiveSamples++;
        }

        if (_consecutiveSamples < _requiredStableSamples)
        {
            return new DesktopShellProbeResult(
                DesktopShellProbeState.Candidate,
                Current,
                identity,
                _consecutiveSamples);
        }

        Current = new DesktopShellHostSnapshot(
            identity,
            checked(++_generation),
            observedAtUtc);
        ResetCandidate();
        return new DesktopShellProbeResult(
            DesktopShellProbeState.Stable,
            Current,
            identity,
            _requiredStableSamples);
    }

    public void Invalidate()
    {
        Current = null;
        ResetCandidate();
    }

    private void ResetCandidate()
    {
        _candidate = null;
        _consecutiveSamples = 0;
    }
}
