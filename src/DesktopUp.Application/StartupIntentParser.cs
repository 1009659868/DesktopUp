using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class StartupIntentParser
{
    public StartupIntent Parse(IEnumerable<string> arguments)
    {
        string[] normalized = arguments
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();

        StartupIntentKind kind = normalized.Any(static value =>
            value.Equals("--exit", StringComparison.OrdinalIgnoreCase))
                ? StartupIntentKind.Exit
                : normalized.Any(static value =>
                    value.Equals("--show-control-center", StringComparison.OrdinalIgnoreCase))
                    ? StartupIntentKind.ShowControlCenter
                    : StartupIntentKind.StartBackground;

        return new StartupIntent(kind, normalized, DateTimeOffset.UtcNow);
    }
}
