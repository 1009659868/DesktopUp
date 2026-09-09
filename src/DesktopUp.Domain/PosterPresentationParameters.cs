using System.Globalization;
using System.Text.Json;

namespace DesktopUp.Domain;

public sealed record PosterContentParameters(double ItemWidthDip = 118, double ItemHeightDip = 82,
    double IconSizeDip = 48, double GapDip = 12, double LabelFontSizeDip = 10, double TitleFontSizeDip = 36)
{
    // A cell reserves the icon/card, a label, and padding. The inter-row gap is separate.
    public double LabelHeightDip => Math.Ceiling(LabelFontSizeDip * 1.6);
    public double RowHeightDip => ItemHeightDip + 7 + LabelHeightDip + 13;
}
public sealed record PosterThemeParameters(string Background = "#21242A", string Foreground = "#F2F2F2", string Accent = "#99CEFF");
public sealed record PosterPresentationParameters(PosterContentParameters Content, PosterThemeParameters Theme);
public sealed record ResolvedPosterPresentation(string EffectiveTemplateKey, PosterPresentationParameters Parameters,
    PosterContentMetrics Metrics, bool CanEdit, bool IsLegacy, string? Warning)
{
    public bool IsFallback => !CanEdit;
    public double MetaHeightDip => EffectiveTemplateKey == "basic-box" ? 0 : Math.Ceiling(81 + Parameters.Content.TitleFontSizeDip * 1.35);
}

public static class PosterPresentationPolicy
{
    public const int SchemaVersion = 1;
    public const int MaximumJsonLength = 32768;
    public const double HorizontalPaddingDip = 28;
    public static bool IsBuiltIn(string? key) => key is "night-ops" or "basic-box";
    public static PosterPresentationParameters Defaults(string template = "night-ops") => template == "basic-box"
        ? new(new(100, 72, 40, 12, 12, 24), new("#282B30", "#F2F2F2", "#68BDFF"))
        : new(new(), new());

    public static string? Validate(PosterPresentationParameters? value)
    {
        if (value?.Content is not { } c || value.Theme is not { } t) return "Missing content/theme parameters.";
        if (!In(c.ItemWidthDip, 72, 320) || !In(c.ItemHeightDip, 48, 240) ||
            !In(c.IconSizeDip, 16, 128) || !In(c.GapDip, 0, 40) ||
            !In(c.LabelFontSizeDip, 9, 24) || !In(c.TitleFontSizeDip, 18, 48))
            return "Content parameters exceed the v1 DIP ranges.";
        if (c.IconSizeDip > c.ItemHeightDip - 8 || c.IconSizeDip > c.ItemWidthDip - 16)
            return "Icon must fit inside the content cell with its padding.";
        if (!Color(t.Background) || !Color(t.Foreground) || !Color(t.Accent))
            return "Theme colors must use #RRGGBB; executable/resource expressions are not allowed.";
        return null;
    }

    public static ResolvedPosterPresentation Resolve(PosterDefinition poster)
    {
        if (!IsBuiltIn(poster.TemplateKey) || poster.LayoutKind != "adaptive-grid")
            return Fallback("Unknown template/layout; configuration retained.");
        try
        {
            using var doc = Parse(poster.LayoutParametersJson);
            var root = doc.RootElement;
            var parameters = Read(root, poster.TemplateKey, out bool legacy);
            var error = Validate(parameters);
            return error is null ? Resolved(poster.TemplateKey, parameters, true, legacy, null) : Fallback(error);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { return Fallback(e.Message); }
    }

    // Explicit writes must be current, understood, bounded, and preserve opaque old configuration.
    public static string? ValidateEdit(PosterDefinition previous, PosterDefinition desired)
    {
        if (!Resolve(previous).CanEdit) return "Unsupported stored presentation must be repaired explicitly before editing.";
        if (!IsBuiltIn(desired.TemplateKey) || desired.LayoutKind != "adaptive-grid") return "Unsupported template/layout.";
        try
        {
            using var oldDoc = Parse(previous.LayoutParametersJson);
            using var newDoc = Parse(desired.LayoutParametersJson);
            var value = Read(newDoc.RootElement, desired.TemplateKey, out bool legacy);
            if (legacy) return "New presentation writes require schemaVersion=1.";
            if (Validate(value) is { } error) return error;
            var expectedLegacy = LegacyPayload(oldDoc.RootElement);
            var actualLegacy = newDoc.RootElement.TryGetProperty("legacy", out var retained) ? retained : (JsonElement?)null;
            if (expectedLegacy is { } expected && (actualLegacy is null || !JsonElement.DeepEquals(expected, actualLegacy.Value)) ||
                expectedLegacy is null && actualLegacy is not null)
                return "Presentation edits must preserve the legacy payload unchanged.";
            return null;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { return e.Message; }
    }

    public static string Serialize(PosterPresentationParameters value, string? previousJson = null)
    {
        if (Validate(value) is { } error) throw new ArgumentException(error);
        using var previous = previousJson is null ? null : Parse(previousJson);
        if (previous is not null && previous.RootElement.TryGetProperty("schemaVersion", out var v) &&
            (!v.TryGetInt32(out int version) || version != SchemaVersion))
            throw new ArgumentException("Cannot overwrite an unknown presentation version.");
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WritePropertyName("content"); writer.WriteStartObject();
            writer.WriteNumber("itemWidthDip", value.Content.ItemWidthDip);
            writer.WriteNumber("itemHeightDip", value.Content.ItemHeightDip);
            writer.WriteNumber("iconSizeDip", value.Content.IconSizeDip);
            writer.WriteNumber("gapDip", value.Content.GapDip);
            writer.WriteNumber("labelFontSizeDip", value.Content.LabelFontSizeDip);
            writer.WriteNumber("titleFontSizeDip", value.Content.TitleFontSizeDip);
            writer.WriteEndObject();
            writer.WritePropertyName("theme"); writer.WriteStartObject();
            writer.WriteString("background", value.Theme.Background.ToUpperInvariant());
            writer.WriteString("foreground", value.Theme.Foreground.ToUpperInvariant());
            writer.WriteString("accent", value.Theme.Accent.ToUpperInvariant());
            writer.WriteEndObject();
            if (previous is not null && LegacyPayload(previous.RootElement) is { } retained)
            { writer.WritePropertyName("legacy"); retained.WriteTo(writer); }
            writer.WriteEndObject();
        }
        string result = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        if (result.Length > MaximumJsonLength) throw new ArgumentException("Presentation JSON is too large.");
        return result;
    }

    public static int ResolveColumns(double widthDip, PosterContentParameters content)
    {
        if (!double.IsFinite(widthDip) || widthDip <= 0 || Validate(new(content, new())) is not null) return 1;
        return (int)Math.Clamp(Math.Floor((Math.Max(1, widthDip - HorizontalPaddingDip * 2) + content.GapDip) /
            (content.ItemWidthDip + content.GapDip)), 1, 64);
    }

    public static double CellWidth(double widthDip, int columns, double gapDip) =>
        Math.Max(1, (widthDip - HorizontalPaddingDip * 2 - gapDip * (columns - 1)) / columns);

    private static ResolvedPosterPresentation Resolved(string template, PosterPresentationParameters parameters, bool edit, bool legacy, string? warning)
    {
        double top = PosterTitleBarLayoutPolicy.HeaderHeightDip + (template == "basic-box" ? 0 :
            Math.Ceiling(81 + parameters.Content.TitleFontSizeDip * 1.35)) + PosterContentMetrics.ItemsTopInsetDip;
        double row = parameters.Content.RowHeightDip;
        // Keep the legacy metric identity for identical geometry, independent of colors/title text.
        string version = top == PosterContentMetrics.Current.ReservedTopDip && row == PosterContentMetrics.Current.CompleteItemRowDip
            ? PosterContentMetrics.Current.Version
            : string.Create(CultureInfo.InvariantCulture, $"presentation-v1:{top:R}:{row:R}:{PosterContentMetrics.FooterHeightDip:R}");
        return new(template, parameters, new(version, top, row, PosterContentMetrics.FooterHeightDip), edit, legacy, warning);
    }
    private static ResolvedPosterPresentation Fallback(string warning) =>
        Resolved("basic-box", Defaults("basic-box"), false, false, warning);

    private static JsonDocument Parse(string? json)
    {
        if (json is null || json.Length > MaximumJsonLength) throw new ArgumentException("Missing or oversized presentation JSON.");
        var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
        document.Dispose(); throw new ArgumentException("Presentation parameters must be a JSON object.");
    }
    private static JsonElement? LegacyPayload(JsonElement root) => root.TryGetProperty("schemaVersion", out _)
        ? root.TryGetProperty("legacy", out var legacy) ? legacy : null : root;

    private static PosterPresentationParameters Read(JsonElement root, string template, out bool legacy)
    {
        var defaults = Defaults(template);
        legacy = !root.TryGetProperty("schemaVersion", out var version);
        if (legacy)
        {
            Unique(root);
            var content = defaults.Content with
            {
                GapDip = Number(root, "itemGapDip", defaults.Content.GapDip),
                ItemHeightDip = Number(root, "cardHeightDip", defaults.Content.ItemHeightDip)
            };
            // Legacy rowHeight is honored only if it is exactly reproducible by shared label/padding metrics.
            if (root.TryGetProperty("rowHeightDip", out _) && Number(root, "rowHeightDip", content.RowHeightDip) != content.RowHeightDip)
                throw new ArgumentException("Legacy rowHeight cannot be represented safely; original data retained.");
            return defaults with { Content = content };
        }
        Members(root, "schemaVersion", "content", "theme", "legacy");
        if (!version.TryGetInt32(out int v) || v != SchemaVersion) throw new ArgumentException("Unknown presentation schema version.");
        if (root.TryGetProperty("legacy", out var retained) && retained.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Legacy payload must be an object.");
        var c = defaults.Content; var t = defaults.Theme;
        if (root.TryGetProperty("content", out var contentNode))
        {
            Members(contentNode, "itemWidthDip", "itemHeightDip", "iconSizeDip", "gapDip", "labelFontSizeDip", "titleFontSizeDip");
            c = new(Number(contentNode, "itemWidthDip", c.ItemWidthDip), Number(contentNode, "itemHeightDip", c.ItemHeightDip),
                Number(contentNode, "iconSizeDip", c.IconSizeDip), Number(contentNode, "gapDip", c.GapDip),
                Number(contentNode, "labelFontSizeDip", c.LabelFontSizeDip), Number(contentNode, "titleFontSizeDip", c.TitleFontSizeDip));
        }
        if (root.TryGetProperty("theme", out var themeNode))
        {
            Members(themeNode, "background", "foreground", "accent");
            t = new(Text(themeNode, "background", t.Background), Text(themeNode, "foreground", t.Foreground), Text(themeNode, "accent", t.Accent));
        }
        return new(c, t);
    }
    private static void Unique(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new ArgumentException("Parameter section must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in node.EnumerateObject()) if (!names.Add(p.Name)) throw new ArgumentException("Duplicate parameter: " + p.Name);
    }
    private static void Members(JsonElement node, params string[] allowed)
    {
        Unique(node);
        foreach (var p in node.EnumerateObject()) if (!allowed.Contains(p.Name, StringComparer.Ordinal))
            throw new ArgumentException("Unknown parameter: " + p.Name);
    }
    private static double Number(JsonElement node, string key, double fallback) =>
        !node.TryGetProperty(key, out var value) ? fallback : value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number : throw new ArgumentException("Invalid numeric parameter: " + key);
    private static string Text(JsonElement node, string key, string fallback) =>
        !node.TryGetProperty(key, out var value) ? fallback : value.ValueKind == JsonValueKind.String ? value.GetString()! :
            throw new ArgumentException("Invalid text parameter: " + key);
    private static bool In(double value, double low, double high) => double.IsFinite(value) && value >= low && value <= high;
    private static bool Color(string? value) => value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") == false;
}
