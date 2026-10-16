namespace DesktopUp.Domain;

public enum StartupIntentKind
{
    StartBackground = 0,
    ShowControlCenter = 1,
    Exit = 2,
    AutoStart = 3
}

public sealed record StartupIntent(
    StartupIntentKind Kind,
    string[] Arguments,
    DateTimeOffset RequestedAtUtc)
{
    public static StartupIntent Background(string[] arguments) =>
        new(StartupIntentKind.StartBackground, arguments, DateTimeOffset.UtcNow);

    public bool ShouldShowControlCenter(bool hasPosters) =>
        Kind == StartupIntentKind.ShowControlCenter ||
        (Kind == StartupIntentKind.StartBackground && !hasPosters);

    // Preserve an ordinary launch; the running instance decides from its current catalog.
    public StartupIntent ForExistingInstance() => this;
}
