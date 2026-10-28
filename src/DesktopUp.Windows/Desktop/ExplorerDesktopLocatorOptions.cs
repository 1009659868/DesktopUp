namespace DesktopUp.Windows;

public sealed class ExplorerDesktopLocatorOptions
{
    public int RequiredStableSamples { get; init; } = 5;
    public int MaximumProbeAttempts { get; init; } = 48;
    public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    internal void Validate()
    {
        if (RequiredStableSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredStableSamples));
        }
        if (MaximumProbeAttempts < RequiredStableSamples)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumProbeAttempts));
        }
        if (ProbeInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ProbeInterval));
        }
    }
}
