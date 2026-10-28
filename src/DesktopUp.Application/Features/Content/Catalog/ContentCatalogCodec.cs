using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentCatalogReadStatus { Supported, Unsupported, Invalid }
public sealed record ContentCatalogReadResult(ContentCatalogReadStatus Status, string Original,
    ContentCatalogSnapshot? Catalog = null, string? Error = null);

// A pure contract fixture format, not a new on-disk production store or portable-profile format.
public static class ContentCatalogCodec
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Write(ContentCatalogSnapshot catalog)
    {
        var text = JsonSerializer.Serialize(ContentCatalogPolicy.Freeze(catalog), Options);
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes)
            throw new ArgumentException("Content contract exceeds the byte budget.", nameof(catalog));
        return text;
    }

    public static ContentCatalogReadResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes)
            return new(ContentCatalogReadStatus.Invalid, text, Error: "Content contract exceeds the byte budget.");
        try
        {
            using var document = JsonDocument.Parse(text, new() { MaxDepth = 32 });
            if (HasDuplicateProperties(document.RootElement))
                return new(ContentCatalogReadStatus.Invalid, text, Error: "Duplicate JSON property.");
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("formatVersion", out var version) ||
                !version.TryGetInt32(out int number))
                return new(ContentCatalogReadStatus.Invalid, text, Error: "Missing format version.");
            if (number != 1)
                return new(ContentCatalogReadStatus.Unsupported, text, Error: "Unknown contract format; retain the original.");
            if (document.RootElement.TryGetProperty("posterItems", out var posterItems) &&
                posterItems.ValueKind == JsonValueKind.Array &&
                posterItems.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("layoutItemId", out _)))
                return new(ContentCatalogReadStatus.Unsupported, text, Error: "Derived layout identity is not an importable field.");
            ContentCatalogSnapshot? catalog;
            try { catalog = JsonSerializer.Deserialize<ContentCatalogSnapshot>(text, Options); }
            catch (JsonException failure)
            {
                return new(ContentCatalogReadStatus.Unsupported, text, Error: failure.Message);
            }
            if (catalog is null) return new(ContentCatalogReadStatus.Invalid, text, Error: "Missing catalog.");
            return new(ContentCatalogReadStatus.Supported, text, ContentCatalogPolicy.Freeze(catalog));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            return new(ContentCatalogReadStatus.Invalid, text, Error: error.Message);
        }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!keys.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var entry in element.EnumerateArray())
                if (HasDuplicateProperties(entry)) return true;
        return false;
    }
}
