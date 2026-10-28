using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed class DesktopRecoveryVersionGate
{
    private long _currentRequest;

    public long CurrentRequest => Interlocked.Read(ref _currentRequest);

    public long Signal() => Interlocked.Increment(ref _currentRequest);

    public bool IsCurrent(long request) => request == CurrentRequest;

    public bool CanCommit(
        long request,
        DesktopRecoveryGenerationPair expected,
        DesktopRecoveryGenerationPair observed) =>
        IsCurrent(request) && expected == observed;
}
