using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed class ExplorerDesktopLocator : IDesktopShellLocator
{
    private readonly object _gate = new();
    private readonly IExplorerDesktopApi _api;
    private readonly ExplorerDesktopLocatorOptions _options;
    private readonly DesktopShellStabilityTracker _tracker;

    public ExplorerDesktopLocator()
        : this(new ExplorerDesktopNativeApi(), new ExplorerDesktopLocatorOptions())
    {
    }

    internal ExplorerDesktopLocator(
        IExplorerDesktopApi api,
        ExplorerDesktopLocatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _api = api;
        _options = options;
        _tracker = new DesktopShellStabilityTracker(options.RequiredStableSamples);
    }

    public DesktopShellHostSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                DesktopShellHostSnapshot? current = _tracker.Current;
                if (current is not null && !_api.IsValid(current.Identity))
                {
                    _tracker.Invalidate();
                    return null;
                }
                return current;
            }
        }
    }

    public DesktopShellProbeResult Probe()
    {
        lock (_gate)
        {
            bool invalidated = false;
            DesktopShellHostSnapshot? current = _tracker.Current;
            if (current is not null && !_api.IsValid(current.Identity))
            {
                _tracker.Invalidate();
                invalidated = true;
            }

            if (!_api.TryFindExistingHost(out DesktopShellHostIdentity candidate) ||
                !_api.IsValid(candidate))
            {
                DesktopShellProbeResult missing =
                    _tracker.Observe(null, DateTimeOffset.UtcNow);
                return invalidated
                    ? missing with { State = DesktopShellProbeState.Invalidated }
                    : missing;
            }

            return _tracker.Observe(candidate, DateTimeOffset.UtcNow);
        }
    }

    public async ValueTask<DesktopShellHostSnapshot?> WaitForStableHostAsync(
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= _options.MaximumProbeAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DesktopShellProbeResult result = Probe();
            if (result.State is DesktopShellProbeState.Stable or DesktopShellProbeState.Unchanged)
            {
                return result.Snapshot;
            }

            if (attempt < _options.MaximumProbeAttempts && _options.ProbeInterval > TimeSpan.Zero)
            {
                await Task.Delay(_options.ProbeInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _tracker.Invalidate();
        }
    }
}
