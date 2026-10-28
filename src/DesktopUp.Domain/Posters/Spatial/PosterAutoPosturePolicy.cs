namespace DesktopUp.Domain;

// Per-poster settings only. Pointer state, deadlines and pending requests are never persisted.
public static class PosterAutoPosturePolicy
{
    public const int DefaultDelaySeconds = 3;
    public const int MinimumDelaySeconds = 1;
    public const int MaximumDelaySeconds = 300;

    public static string? ValidateDelay(int seconds) => seconds is >= MinimumDelaySeconds and <= MaximumDelaySeconds
        ? null : "Auto-collapse leave delay must be an integer from 1 to 300 seconds.";
}
