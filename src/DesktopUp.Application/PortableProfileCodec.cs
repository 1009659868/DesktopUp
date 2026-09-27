using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record PortableSettings(bool DynamicEffectsPaused, DesktopUpPerformanceMode PerformanceMode);
public sealed record PortablePosterProfile(string Key, string Title, string Subtitle, string Purpose,
    string MonitorHint, double WidthDip, double ExpandedHeightDip, double HorizontalAnchor, double VerticalAnchor,
    long StackOrder, bool IsVisible, bool IsLocked, bool IsCollapsed, bool AutoCollapseEnabled,
    int AutoCollapseDelaySeconds, string LayoutKind, JsonElement LayoutParameters, string TemplateKey)
{
    [JsonIgnore] public string LayoutParametersJson => LayoutParameters.GetRawText();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PortableContentLayout? ContentLayout { get; init; }
}
public sealed record PortableProfileDocument(string Format, int Version, IReadOnlyList<PortablePosterProfile> Posters,
    PortableSettings Settings);

// A deliberately small, versioned data format. This codec has no file, shell, native or database effects.
public static class PortableProfileCodec
{
    public const string Format = "DesktopUp.PortableProfile";
    /// <summary>v4 carries content schema three, including manual mode's retained automatic state.
    /// v1-v3 archives remain readable; unknown members and new semantics under old versions reject.</summary>
    public const int Version = 4, MinimumVersion = 1, MaximumBytes = 4 * 1024 * 1024, MaximumPosters = 128,
        MaximumDepth = 32, MaximumLayoutBytes = 32768;
    public const double MaximumExtentDip = 32768;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = MaximumDepth
    };

    public static byte[] Export(DesktopStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Catalog.Revision != state.Settings.Revision)
            throw new ArgumentException("Export requires one committed state snapshot.");
        if (TemplateRecipeCatalog.Read(state.Settings.Settings.TemplateCatalogJson).Count != 0 || state.Settings.Settings.CenterTheme != "light" ||
            state.Catalog.Posters.Any(p => PosterPresentationPolicy.Resolve(p.Poster).Parameters.RecipeInstances?.Count > 0))
            throw new ArgumentException("当前便携格式尚不能携带自定义模板或中心主题；请使用完整数据库备份，配置未被修改。");
        var posters = state.Catalog.Posters.OrderBy(p => p.Poster.StackOrder)
            .ThenBy(p => p.Poster.PosterId, StringComparer.Ordinal).Select((p, index) => FromPoster(p.Poster, $"p{index + 1}")).ToArray();
        var document = new PortableProfileDocument(Format, Version, Array.AsReadOnly(posters),
            new(state.Settings.Settings.DynamicEffectsPaused, state.Settings.Settings.PerformanceMode));
        return Encode(document);
    }

    public static byte[] Encode(PortableProfileDocument document)
    {
        Validate(document);
        return JsonSerializer.SerializeToUtf8Bytes(document, Options);
    }
    public static PortableProfileDocument Freeze(PortableProfileDocument document) => Parse(Encode(document));

    public static PortableProfileDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes) throw new ArgumentException("Portable configuration must be 1 byte to 4 MiB.");
        try
        {
            // Reject invalid UTF-8 rather than replacing bytes in a title or a parameter value.
            string json = new UTF8Encoding(false, true).GetString(bytes.Span);
            if (json.Length > 0 && json[0] == '\uFEFF') json = json[1..];
            using var parsed = JsonDocument.Parse(json, new() { MaxDepth = MaximumDepth });
            CheckDuplicateMembers(parsed.RootElement);
            var document = JsonSerializer.Deserialize<PortableProfileDocument>(parsed.RootElement, Options)
                ?? throw new ArgumentException("Portable configuration is null.");
            Validate(document);
            return document with { Posters = Array.AsReadOnly(document.Posters.Select(p => p with
            {
                LayoutParameters = p.LayoutParameters.Clone(),
                ContentLayout = p.ContentLayout is { } content ? content with
                { Regions = Array.AsReadOnly(content.Regions.ToArray()), Placements = Array.AsReadOnly(content.Placements.ToArray()),
                    ReservedSpaces = Array.AsReadOnly(content.ReservedSpaces.ToArray()) } : null
            }).ToArray()) };
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException or OverflowException)
        { throw new ArgumentException("Invalid portable configuration: " + error.Message, error); }
    }

    public static void Validate(PortableProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Format != Format || document.Version is < MinimumVersion or > Version)
            throw new ArgumentException("Unknown portable format or version.");
        if (document.Settings is null || !Enum.IsDefined(document.Settings.PerformanceMode)) throw new ArgumentException("Invalid portable settings.");
        if (document.Posters is null || document.Posters.Count > MaximumPosters) throw new ArgumentException("Portable poster count exceeds 128.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in document.Posters)
        {
            if (p is null || !Token(p.Key, 64) || !keys.Add(p.Key) || !Text(p.Title, 256) || !Text(p.Subtitle, 512, true) ||
                !Text(p.Purpose, 256) || !Text(p.MonitorHint, 256) || !Token(p.TemplateKey, 128) ||
                p.LayoutKind != PosterPresentationPolicy.AdaptiveGridLayout || p.StackOrder is < 0 or > int.MaxValue ||
                !Extent(p.WidthDip, PosterFrameGeometryPolicy.MinimumWidthDip) ||
                !Extent(p.ExpandedHeightDip, PosterFrameGeometryPolicy.MinimumExpandedHeightDip) ||
                !Unit(p.HorizontalAnchor) || !Unit(p.VerticalAnchor) ||
                PosterAutoPosturePolicy.ValidateDelay(p.AutoCollapseDelaySeconds) is not null)
                throw new ArgumentException("Invalid or unsupported portable poster fields.");
            ValidateParameters(p.LayoutParameters, p.TemplateKey);
            if (p.ContentLayout is { } content)
            {
                if (document.Version == 1) throw new ArgumentException("Content layout requires portable format version two.");
                // Version three promises the axis: an archive that claims it without stating one is
                // rejected instead of being silently interpreted as vertical.
                if (document.Version >= 3 && content.FlowAxis is null)
                    throw new ArgumentException("Content layout requires an explicit flow axis from portable format version three.");
                if (document.Version < 4 && (content.SchemaVersion >= 3 || content.SavedAutomaticMode is not null ||
                    content.Placements?.Any(p => p?.SavedAutomaticState is not null || p?.AutomaticSizeBasis is not null) == true))
                    throw new ArgumentException("Content schema three and retained automatic state require portable format version four.");
                var documentLayout = PortableContentLayoutCodec.Import(content, p.Key);
                var appearance = PosterConfigurationFactory.NewBasicBox("portable-content-validation", "portable-content-validation") with
                { LayoutParametersJson = p.LayoutParametersJson, TemplateKey = PosterPresentationPolicy.IsBuiltIn(p.TemplateKey) ? p.TemplateKey : "basic-box" };
                var preferences = PosterPresentationPolicy.Resolve(appearance).Parameters.Preferences ?? new();
                _ = ContentLayoutCapabilities.Inspect(documentLayout, preferences.Scale, preferences.Spacing);
            }
        }
        // This also bounds documents constructed programmatically, not only parsed byte streams.
        if (JsonSerializer.SerializeToUtf8Bytes(document, Options).Length > MaximumBytes)
            throw new ArgumentException("Portable configuration exceeds 4 MiB.");
    }

    internal static PortablePosterProfile FromPoster(PosterDefinition poster, string key)
    {
        if (Encoding.UTF8.GetByteCount(poster.LayoutParametersJson) > MaximumLayoutBytes)
            throw new ArgumentException("Stored layout parameters exceed the portable limit.");
        using var parsed = JsonDocument.Parse(poster.LayoutParametersJson, new() { MaxDepth = MaximumDepth });
        JsonElement parameters = parsed.RootElement.Clone();
        // Known pre-versioned numeric layout fields are converted losslessly to the supported parameter schema.
        // Arbitrary retained metadata is not exported: it can contain private source paths or future semantics.
        if (!parameters.TryGetProperty("schemaVersion", out _))
        {
            Members(parameters, "itemGapDip", "rowHeightDip", "cardHeightDip");
            var resolved = PosterPresentationPolicy.Resolve(poster with { TemplateKey = PosterPresentationPolicy.IsBuiltIn(poster.TemplateKey) ? poster.TemplateKey : "basic-box" });
            if (!resolved.CanEdit) throw new ArgumentException("Legacy presentation cannot be exported safely.");
            using var converted = JsonDocument.Parse(PosterPresentationPolicy.Serialize(resolved.Parameters));
            parameters = converted.RootElement.Clone();
        }
        if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var content, out var contentError))
            throw new ArgumentException("Cannot export unsupported content layout for " + poster.Title + ": " + contentError);
        return new(key, poster.Title, poster.Subtitle, poster.Purpose, poster.MonitorDeviceName,
            poster.WidthDip, poster.ExpandedHeightDip, poster.HorizontalAnchor, poster.VerticalAnchor,
            poster.StackOrder, poster.IsVisible, poster.IsLocked, poster.IsCollapsed, poster.AutoCollapseEnabled,
            poster.AutoCollapseDelaySeconds, poster.LayoutKind, parameters, poster.TemplateKey)
        { ContentLayout = PortableContentLayoutCodec.Export(content!) };
    }

    private static void ValidateParameters(JsonElement parameters, string template)
    {
        if (parameters.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(parameters.GetRawText()) > MaximumLayoutBytes)
            throw new ArgumentException("Portable layout parameters must be a bounded object.");
        CheckDuplicateMembers(parameters);
        if (!parameters.TryGetProperty("schemaVersion", out var schema) || !schema.TryGetInt32(out int version) || version is not (1 or 2))
            throw new ArgumentException("Unknown layout parameter version.");
        if (version == 1) Members(parameters, "schemaVersion", "content", "theme", "legacy");
        else Members(parameters, "schemaVersion", "content", "theme", "legacy", "preferences");
        if (parameters.TryGetProperty("legacy", out var legacy))
        {
            Members(legacy, "itemGapDip", "rowHeightDip", "cardHeightDip");
            foreach (var field in legacy.EnumerateObject())
                if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetDouble(out double n) || !double.IsFinite(n) || n < 0 || n > MaximumExtentDip)
                    throw new ArgumentException("Unsupported retained legacy data.");
        }
        var basis = PosterConfigurationFactory.NewBasicBox("portable-validation", "portable-validation") with
        { TemplateKey = PosterPresentationPolicy.IsBuiltIn(template) ? template : "basic-box", LayoutParametersJson = parameters.GetRawText() };
        var resolved = PosterPresentationPolicy.Resolve(basis);
        if (!resolved.CanEdit) throw new ArgumentException("Unsupported portable layout parameters: " + resolved.Warning);
    }

    private static void Members(JsonElement node, params string[] allowed)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new ArgumentException("Parameter section must be an object.");
        CheckDuplicateMembers(node);
        foreach (var member in node.EnumerateObject())
            if (!allowed.Contains(member.Name, StringComparer.Ordinal)) throw new ArgumentException("Unknown parameter field: " + member.Name);
    }
    private static void CheckDuplicateMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in element.EnumerateObject())
            {
                if (!names.Add(member.Name)) throw new ArgumentException("Duplicate field: " + member.Name);
                CheckDuplicateMembers(member.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicateMembers(item);
    }
    private static bool Extent(double value, double minimum) => double.IsFinite(value) && value >= minimum && value <= MaximumExtentDip;
    private static bool Unit(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
    private static bool Text(string? value, int maximum, bool empty = false) => value is not null && value.Length <= maximum &&
        (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl);
    private static bool Token(string? value, int maximum) => Text(value, maximum) && value!.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
