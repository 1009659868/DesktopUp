using DesktopUp.Domain;

namespace DesktopUp.Application;

// Trusted host -> dedicated helper. No arbitrary verb, argument line, DLL or provider execution.
public enum LocalContentAction { Inspect, Icon, Open, Reveal, DecodeIcon }
public sealed record LocalContentRequest(Guid RequestId, LocalContentAction Action, ContentItem Item);
public sealed record LocalContentResponse(Guid RequestId, ContentAvailability Availability, string Message,
    string? Name = null, string? TypeName = null, byte[]? Pixels = null, int Width = 0, int Height = 0,
    ContentOpenEvidence Evidence = ContentOpenEvidence.None, bool Cancelled = false)
{
    public ContentOpenOutcome Outcome { get; init; }
}

public interface ILocalContentWorker
{
    ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellationToken);
}

public static class LocalContentProtocol
{
    public const int MaximumRequestCharacters = 65536;
    public const int MaximumResponseCharacters = 400000;
    public const int MaximumIconDimension = 256;
    public static bool IsSupported(ContentItem item) => item is { Source: not null, Target: not null } &&
        item.Source.ProviderId == "local-file-reference-v1" && item.Source.Key == item.Target.Locator &&
        item.Target.Arguments is { Length: 0 } && item.Target.WorkingDirectory is { Length: 0 } &&
        (item.Kind is ContentKind.File or ContentKind.Folder && item.Target.Kind == ContentTargetKind.FileSystem ||
         item.Kind == ContentKind.Shortcut && item.Target.Kind == ContentTargetKind.Shortcut);
    public static bool ValidPixels(byte[]? pixels, int width, int height) => pixels is not null &&
        width is > 0 and <= MaximumIconDimension && height is > 0 and <= MaximumIconDimension &&
        pixels.Length == width * height * 4;
    public static bool Validate(LocalContentRequest request, LocalContentResponse result) =>
        result.RequestId == request.RequestId && Enum.IsDefined(result.Availability) && Enum.IsDefined(result.Evidence) && Enum.IsDefined(result.Outcome) &&
        result.Message is { Length: <= 1024 } && result.Name?.Length is not > 1024 && result.TypeName?.Length is not > 256 &&
        (result.Pixels is null ? result.Width == 0 && result.Height == 0 :
            request.Action is LocalContentAction.Icon or LocalContentAction.DecodeIcon && ValidPixels(result.Pixels, result.Width, result.Height)) &&
        (result.Evidence == ContentOpenEvidence.None || request.Action is LocalContentAction.Open or LocalContentAction.Reveal &&
            result.Availability == ContentAvailability.Available && !result.Cancelled &&
            result.Outcome is ContentOpenOutcome.None or ContentOpenOutcome.Succeeded) &&
        (result.Outcome != ContentOpenOutcome.Succeeded || result.Evidence != ContentOpenEvidence.None);
}
