using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopUp.Domain;

public sealed record PosterDefinition(
    string PosterId,
    string Title,
    string Subtitle,
    string MonitorDeviceName,
    double FallbackXDip,
    double FallbackYDip,
    double WidthDip,
    double ExpandedHeightDip,
    double HorizontalAnchor,
    double VerticalAnchor,
    double NormalizedWidth,
    double NormalizedExpandedHeight,
    bool IsLocked,
    bool IsCollapsed,
    bool AutoCollapseEnabled,
    string LayoutKind,
    string LayoutParametersJson,
    string TemplateKey)
{
    public int AutoCollapseDelaySeconds { get; init; } = PosterAutoPosturePolicy.DefaultDelaySeconds;
    public string Purpose { get; init; } = "general";
    public bool IsVisible { get; init; } = true;
    // Local placement availability is separate from the user's visibility intent.
    public bool PlacementPending { get; init; }
    // Independent content intent; null is an empty, unauthored layout. Reading never seeds demo members or writes a migration.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentLayoutJson { get; init; }
    public long StackOrder { get; init; }
    public string? CollectionId { get; init; }
    // A reserved instance identity, not a loaded theme or executable theme package.
    public string ThemeInstanceId { get; init; } = $"theme-{PosterId}";
}

public enum DesktopUpPerformanceMode
{
    Automatic = 0,
    Static = 1,
    FramesPerSecond30 = 2
}

public sealed record DesktopUpSettings(
    bool DynamicEffectsPaused,
    DesktopUpPerformanceMode PerformanceMode,
    bool LaunchAtStartupRequested)
{
    public string CenterTheme { get; init; } = "light";
    public string TemplateCatalogJson { get; init; } = "[]";
}

public static class AuthoritativeStateValidationPolicy
{
    public static string? Validate(PosterDefinition poster)
    {
        ArgumentNullException.ThrowIfNull(poster);
        if (PosterAutoPosturePolicy.ValidateDelay(poster.AutoCollapseDelaySeconds) is { } delayError)
            return delayError;
        if (!IsRequiredText(poster.PosterId, 128) ||
            !IsRequiredText(poster.Title, 256) ||
            !IsBoundedText(poster.Subtitle, 512) ||
            !IsRequiredText(poster.MonitorDeviceName, 256) ||
            !IsRequiredText(poster.LayoutKind, 64) ||
            !IsRequiredText(poster.TemplateKey, 128) ||
            !IsRequiredText(poster.Purpose, 256) ||
            !IsRequiredText(poster.ThemeInstanceId, 256) ||
            (poster.CollectionId is not null && !IsRequiredText(poster.CollectionId, 128)) ||
            poster.StackOrder < 0)
        {
            return "Poster identity or text fields are invalid.";
        }
        if (!double.IsFinite(poster.FallbackXDip) ||
            !double.IsFinite(poster.FallbackYDip) ||
            !double.IsFinite(poster.WidthDip) ||
            !double.IsFinite(poster.ExpandedHeightDip) ||
            poster.WidthDip < PosterFrameGeometryPolicy.MinimumWidthDip ||
            poster.ExpandedHeightDip < PosterFrameGeometryPolicy.MinimumExpandedHeightDip)
        {
            return "Poster logical geometry is invalid.";
        }
        if (!IsUnitInterval(poster.HorizontalAnchor) ||
            !IsUnitInterval(poster.VerticalAnchor) ||
            !IsPositiveUnitInterval(poster.NormalizedWidth) ||
            !IsPositiveUnitInterval(poster.NormalizedExpandedHeight))
        {
            return "Poster normalized geometry is invalid.";
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(poster.LayoutParametersJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "Poster layout parameters must be a JSON object.";
            }
        }
        catch (JsonException)
        {
            return "Poster layout parameters are not valid JSON.";
        }
        return null;
    }

    public static string? Validate(DesktopUpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.CenterTheme is not ("light" or "ink" or "blue" or "system")) return "Unknown control-center theme.";
        try { TemplateRecipeCatalog.Read(settings.TemplateCatalogJson, stored: true); }
        catch (ArgumentException e) { return e.Message; }
        return Enum.IsDefined(settings.PerformanceMode)
            ? null
            : "Performance mode is invalid.";
    }

    private static bool IsRequiredText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    private static bool IsBoundedText(string? value, int maximumLength) =>
        value is not null && value.Length <= maximumLength;

    private static bool IsUnitInterval(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool IsPositiveUnitInterval(double value) =>
        double.IsFinite(value) && value is > 0 and <= 1;
}
