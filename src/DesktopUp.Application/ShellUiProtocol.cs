namespace DesktopUp.Application;

// Interactive Shell extensions run in a disposable STA helper, never in the UI process.
// No arbitrary executable/verb or imported script is accepted by this protocol.
public enum ShellUiAction { ItemMenu, DirectoryMenu, Properties }
public sealed record ShellUiRequest(Guid Id, ShellUiAction Action, string Path, int X, int Y)
{
    public string? Identity { get; init; }
}
public sealed record ShellUiResult(Guid Id, bool Invoked, bool Cancelled, string? Error = null);
public static class ShellUiProtocol
{
    public const int MaximumCharacters = 16384;
    public static bool Valid(ShellUiRequest request) => request.Id != Guid.Empty && Enum.IsDefined(request.Action) &&
        request.Path is { Length: >= 4 and <= 8192 } path && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
        path[2] is '\\' or '/' && !path.Any(char.IsControl) && !path.AsSpan(2).Contains(':') &&
        path.IndexOfAny(['*', '?', '"', '<', '>', '|']) < 0 &&
        (request.Identity is null || request.Identity.Length == 48 && request.Identity.All(char.IsAsciiHexDigit));
    public static bool Valid(ShellUiRequest request, ShellUiResult result) => result.Id == request.Id &&
        !(result.Invoked && result.Cancelled) && result.Error?.Length is not > 1024 &&
        (!result.Invoked || result.Error is null);
}
