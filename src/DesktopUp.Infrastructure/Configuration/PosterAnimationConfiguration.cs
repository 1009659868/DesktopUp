using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed record PosterAnimationConfiguration(int SchemaVersion = 1, PosterAnimationOptions? Posture = null)
{
    public static PosterAnimationOptions Read(string path, bool required = false)
    {
        if (!File.Exists(path))
        {
            if (required) throw new FileNotFoundException("Animation configuration not found.", path);
            return new();
        }
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 16 * 1024) throw new InvalidDataException("Animation configuration exceeds 16 KiB.");
        using var document = JsonDocument.Parse(input);
        CheckObject(document.RootElement);
        if (document.RootElement.TryGetProperty("posture", out var posture)) CheckObject(posture);
        var config = document.Deserialize<PosterAnimationConfiguration>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("Animation configuration is null.");
        if (config.SchemaVersion != 1) throw new InvalidDataException("Unsupported animation configuration schema.");
        var options = config.Posture ?? new(); options.Validate(); return options;
    }

    private static void CheckObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidDataException("Animation configuration must contain objects without duplicate properties.");
    }
}
