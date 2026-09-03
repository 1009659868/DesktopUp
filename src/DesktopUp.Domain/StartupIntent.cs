namespace DesktopUp.Domain;

public enum StartupIntentKind
{
    StartBackground = 0,
    ShowControlCenter = 1,
    Exit = 2
}

public sealed record StartupIntent(
    StartupIntentKind Kind,
    string[] Arguments,
    DateTimeOffset RequestedAtUtc)
{
    public static StartupIntent Background(string[] arguments) =>
        new(StartupIntentKind.StartBackground, arguments, DateTimeOffset.UtcNow);

    public StartupIntent ForExistingInstance() => Kind == StartupIntentKind.StartBackground
        ? this with { Kind = StartupIntentKind.ShowControlCenter }
        : this;
}
