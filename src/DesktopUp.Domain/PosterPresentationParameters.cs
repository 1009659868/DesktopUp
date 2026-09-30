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
public sealed record PosterPresentationParameters(PosterContentParameters Content, PosterThemeParameters Theme,
    PosterContentPreferences? Preferences = null, LampTemplateParameters? Lamp = null,
    IReadOnlyDictionary<string, TemplateRecipeInstance>? RecipeInstances = null);
public sealed record ResolvedPosterPresentation(string EffectiveTemplateKey, PosterPresentationParameters Parameters,
    PosterContentMetrics Metrics, bool CanEdit, bool IsLegacy, string? Warning)
{
    public string? SelectedRecipeId { get; init; }
    public TemplateParameterValues TemplateValues => TemplateRecipeInstancePolicy.CurrentValues(Parameters, SelectedRecipeId ?? EffectiveTemplateKey);
    // Compatibility readback for pre-module callers.
    public LampTemplateParameters TemplateParameters => TemplateRecipeInstancePolicy.Current(Parameters, SelectedRecipeId ?? EffectiveTemplateKey);
    public bool IsFallback => !CanEdit;
    public PosterContentParameters EffectiveContent => PosterContentPreferencePolicy.Resolve(Parameters.Content, Parameters.Preferences);
    public SceneCapabilityDefinition? SceneCapability => SceneCapabilityRegistry.Find(
        TemplateRecipeInstancePolicy.RecipeFor(Parameters, SelectedRecipeId ?? EffectiveTemplateKey)?.Schema.RendererCapability ?? "");
    public PosterTitleChromeKind TitleChrome => SceneCapability?.TitleChrome ?? PosterTitleChromeKind.Basic;
    public double HeaderHeightDip => TitleChrome == PosterTitleChromeKind.Cloud
        ? PosterTitleChromePolicy.ExpandedCloudHeightDip : PosterTitleBarLayoutPolicy.HeaderHeightDip;
    public double MetaHeightDip => TitleChrome == PosterTitleChromeKind.Cloud || EffectiveTemplateKey == "basic-box"
        ? 0 : Math.Ceiling(81 + Parameters.Content.TitleFontSizeDip * 1.35);
}

public static class PosterPresentationPolicy
{
    public const string AdaptiveGridLayout = "adaptive-grid";
    public const int SchemaVersion = 4;
    public const int MaximumJsonLength = 2 * 1024 * 1024;
    public const double HorizontalPaddingDip = 28;
    public static bool IsBuiltIn(string? key) => key is "night-ops" or "basic-box" or LampTemplatePolicy.TemplateKey || SceneCapabilityRegistry.Templates.Any(r => r.Id == key);
    public static PosterPresentationParameters Defaults(string template = "night-ops") => template == "basic-box"
        ? new(new(100, 72, 40, 12, 12, 24), new("#282B30", "#F2F2F2", "#68BDFF"))
        : new(new(), new(Background: "#202E2A"));

    public static string? Validate(PosterPresentationParameters? value)
    {
        if (value?.Content is not { } c || value.Theme is not { } t) return "Missing content/theme parameters.";
        if (PosterContentPreferencePolicy.Validate(value.Preferences) is { } preferenceError) return preferenceError;
        if (LampTemplatePolicy.Validate(value.Lamp) is { } lampError) return lampError;
        if (TemplateRecipeInstancePolicy.Validate(value.RecipeInstances) is { } recipeError) return recipeError;
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
        if (poster.LayoutKind != AdaptiveGridLayout)
            return Fallback("Unknown template/layout; configuration retained.");
        try
        {
            using var doc = Parse(poster.LayoutParametersJson);
            var root = doc.RootElement;
            var parameters = Read(root, poster.TemplateKey, out bool legacy);
            string effectiveKey = poster.TemplateKey;
            if (parameters.RecipeInstances?.ContainsKey(effectiveKey) == true || !IsBuiltIn(effectiveKey))
            {
                if (parameters.RecipeInstances is not { } instances || !instances.TryGetValue(effectiveKey, out var instance))
                    return Fallback("Unknown template; configuration retained.");
                effectiveKey = instance.Recipe.EffectiveTemplateKey;
            }
            var error = Validate(parameters);
            bool missing = (parameters.RecipeInstances?.Values.Any(i => !SceneCapabilityRegistry.Available(i.Recipe.Schema.RendererCapability)) ?? false) ||
                IsBuiltIn(poster.TemplateKey) && TemplateParameterSchemas.ForTemplate(poster.TemplateKey) is null;
            return error is null ? Resolved(effectiveKey, parameters, !missing, legacy,
                missing ? "模板模块未安装；原配置完整保留，当前只读显示。恢复模块后可继续调整。" : null, poster.TemplateKey) : Fallback(error);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        { return Fallback(e.Message); }
    }

    // Explicit writes must be current, understood, bounded, and preserve opaque old configuration.
    public static string? ValidateEdit(PosterDefinition previous, PosterDefinition desired)
    {
        if (!Resolve(previous).CanEdit) return "Unsupported stored presentation must be repaired explicitly before editing.";
        if (!Resolve(desired).CanEdit || desired.LayoutKind != AdaptiveGridLayout) return "Unsupported template/layout.";
        try
        {
            using var oldDoc = Parse(previous.LayoutParametersJson);
            using var newDoc = Parse(desired.LayoutParametersJson);
            var value = Read(newDoc.RootElement, desired.TemplateKey, out bool legacy);
            if (legacy) return "New presentation writes require a supported schemaVersion.";
            if (Validate(value) is { } error) return error;
            var previousRecipes = Read(oldDoc.RootElement, previous.TemplateKey, out _).RecipeInstances;
            if (previousRecipes is not null)
                foreach (var (id, prior) in previousRecipes)
                    if (value.RecipeInstances is not { } retainedRecipes || !retainedRecipes.TryGetValue(id, out var retainedRecipe) ||
                        TemplateRecipeCodec.Serialize(prior.Recipe) != TemplateRecipeCodec.Serialize(retainedRecipe.Recipe))
                        return "Presentation edits must preserve existing recipe definitions and instances.";
            if (oldDoc.RootElement.TryGetProperty("templates", out _) &&
                Read(oldDoc.RootElement, previous.TemplateKey, out _).Lamp is not null && value.Lamp is null)
                return "Presentation edits must retain the saved lamp override; reset by writing its defaults explicitly.";
            var expectedLegacy = LegacyPayload(oldDoc.RootElement);
            var actualLegacy = newDoc.RootElement.TryGetProperty("legacy", out var retained) ? retained : (JsonElement?)null;
            if (expectedLegacy is { } expected && (actualLegacy is null || !JsonElement.DeepEquals(expected, actualLegacy.Value)) ||
                expectedLegacy is null && actualLegacy is not null)
                return "Presentation edits must preserve the legacy payload unchanged.";
            return null;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        { return e.Message; }
    }

    public static string Serialize(PosterPresentationParameters value, string? previousJson = null)
    {
        if (Validate(value) is { } error) throw new ArgumentException(error);
        using var previous = previousJson is null ? null : Parse(previousJson);
        if (previous is not null)
        {
            // Older callers cannot erase a new understood section while changing an unrelated value.
            // Validate the complete versioned source before retaining it: copying only the known child
            // would silently drop an unknown sibling. Unversioned legacy objects remain opaque.
            try
            {
                Unique(previous.RootElement);
                if (previous.RootElement.TryGetProperty("schemaVersion", out _))
                {
                    var retained = Read(previous.RootElement, "night-ops", out _);
                    if (Validate(retained) is { } invalid) throw new ArgumentException(invalid);
                    value = value with { Preferences = value.Preferences ?? retained.Preferences, Lamp = value.Lamp ?? retained.Lamp,
                        RecipeInstances = value.RecipeInstances ?? retained.RecipeInstances };
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or OverflowException or KeyNotFoundException)
            { throw new ArgumentException("Cannot overwrite an invalid presentation payload.", e); }
        }
        if (Validate(value) is { } retainedError) throw new ArgumentException(retainedError);
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", value.RecipeInstances is not null || value.Lamp is not null ? SchemaVersion : value.Preferences is null ? 1 : 2);
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
            if (value.Preferences is { } preferences)
            {
                writer.WritePropertyName("preferences"); writer.WriteStartObject();
                writer.WriteNumber("mappingVersion", preferences.MappingVersion);
                writer.WriteNumber("scale", preferences.Scale); writer.WriteNumber("spacing", preferences.Spacing);
                writer.WriteEndObject();
            }
            if (value.Lamp is { } lamp)
            {
                writer.WritePropertyName("templates"); writer.WriteStartObject();
                writer.WritePropertyName("lamp"); writer.WriteStartObject();
                writer.WriteString("lightColor", lamp.LightColor.ToUpperInvariant());
                writer.WriteNumber("softness", lamp.Softness); writer.WriteNumber("intensity", lamp.Intensity);
                writer.WriteString("background", lamp.Background.ToUpperInvariant());
                writer.WriteString("card", lamp.Card.ToUpperInvariant());
                writer.WriteNumber("backgroundOpacity", lamp.BackgroundOpacity);
                WriteMotion(writer, lamp);
                writer.WriteEndObject(); writer.WriteEndObject();
            }
            if (value.RecipeInstances is not null)
            {
                writer.WritePropertyName("recipes"); writer.WriteStartArray();
                foreach (var instance in value.RecipeInstances.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value))
                {
                    writer.WriteStartObject(); writer.WritePropertyName("definition");
                    using var recipe = JsonDocument.Parse(TemplateRecipeCodec.Serialize(instance.Recipe)); recipe.RootElement.WriteTo(writer);
                    writer.WritePropertyName("values"); writer.WriteStartObject();
                    foreach (var p in instance.Recipe.Schema.Properties)
                    {
                        writer.WritePropertyName(p.Key);
                        TemplateRecipeCodec.WriteValue(writer, instance.Values.Get(p.Binding));
                    }
                    writer.WriteEndObject(); writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        string result = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        if (result.Length > MaximumJsonLength) throw new ArgumentException("Presentation JSON is too large.");
        return result;
    }

    public static int ResolveColumns(double widthDip, PosterContentParameters content)
    {
        // Render metrics may include macro/item scale and are not raw v1 parameter ranges.
        if (!double.IsFinite(widthDip) || widthDip <= 0 || !double.IsFinite(content.ItemWidthDip) || content.ItemWidthDip <= 0 ||
            !double.IsFinite(content.GapDip) || content.GapDip < 0) return 1;
        return (int)Math.Clamp(Math.Floor((Math.Max(1, widthDip - HorizontalPaddingDip * 2) + content.GapDip) /
            (content.ItemWidthDip + content.GapDip)), 1, 64);
    }

    public static double CellWidth(double widthDip, int columns, double gapDip) =>
        Math.Max(1, (widthDip - HorizontalPaddingDip * 2 - gapDip * (columns - 1)) / columns);

    private static ResolvedPosterPresentation Resolved(string template, PosterPresentationParameters parameters, bool edit, bool legacy, string? warning, string? selected = null)
    {
        var presentation = new ResolvedPosterPresentation(template, parameters, PosterContentMetrics.Current, edit, legacy, warning)
            { SelectedRecipeId = selected };
        double top = presentation.HeaderHeightDip + presentation.MetaHeightDip + PosterContentMetrics.ItemsTopInsetDip;
        double row = PosterContentPreferencePolicy.Resolve(parameters.Content, parameters.Preferences).RowHeightDip;
        // Keep the legacy metric identity for identical geometry, independent of colors/title text.
        string version = top == PosterContentMetrics.Current.ReservedTopDip && row == PosterContentMetrics.Current.CompleteItemRowDip
            ? PosterContentMetrics.Current.Version
            : string.Create(CultureInfo.InvariantCulture, $"presentation-v1:{top:R}:{row:R}:{PosterContentMetrics.FooterHeightDip:R}");
        return presentation with { Metrics = new(version, top, row, PosterContentMetrics.FooterHeightDip) };
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
        if (!version.TryGetInt32(out int v) || v is not (1 or 2 or 3 or SchemaVersion)) throw new ArgumentException("Unknown presentation schema version.");
        if (v == 1) Members(root, "schemaVersion", "content", "theme", "legacy");
        else if (v == 2) Members(root, "schemaVersion", "content", "theme", "legacy", "preferences");
        else if (v == 3) Members(root, "schemaVersion", "content", "theme", "legacy", "preferences", "templates");
        else Members(root, "schemaVersion", "content", "theme", "legacy", "preferences", "templates", "recipes");
        if (root.TryGetProperty("legacy", out var retained) && retained.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Legacy payload must be an object.");
        if (v == 4 && !root.TryGetProperty("templates", out _) && !root.TryGetProperty("recipes", out _))
            throw new ArgumentException("Missing v4 template parameters.");
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
        PosterContentPreferences? preferences = null;
        if (v >= 2)
        {
            if (root.TryGetProperty("preferences", out var p)) preferences = ReadPreferences(p);
            else if (v == 2) throw new ArgumentException("Missing v2 preferences.");
        }
        LampTemplateParameters? lamp = null;
        if (v == 3 || v == 4 && root.TryGetProperty("templates", out _))
        {
            if (!root.TryGetProperty("templates", out var templates)) throw new ArgumentException("Missing v3 template parameters.");
            Members(templates, "lamp");
            if (!templates.TryGetProperty("lamp", out var node)) throw new ArgumentException("Missing v3 lamp parameters.");
            lamp = ReadLamp(node);
        }
        IReadOnlyDictionary<string, TemplateRecipeInstance>? recipes = null;
        if (v == 4 && root.TryGetProperty("recipes", out var recipeNodes))
        {
            if (recipeNodes.ValueKind != JsonValueKind.Array || recipeNodes.GetArrayLength() > TemplateRecipeCatalog.MaximumTemplates)
                throw new ArgumentException("Invalid recipe instance array.");
            var items = new Dictionary<string, TemplateRecipeInstance>(StringComparer.Ordinal);
            foreach (var entry in recipeNodes.EnumerateArray())
            {
                Members(entry, "definition", "values");
                var recipe = TemplateRecipeCodec.Parse(entry.GetProperty("definition").GetRawText(), stored: true);
                var values = entry.GetProperty("values"); Members(values, recipe.Schema.Properties.Select(p => p.Key).ToArray());
                var instance = TemplateRecipeInstancePolicy.DefaultValues(recipe);
                foreach (var p in recipe.Schema.Properties)
                {
                    var raw = values.GetProperty(p.Key);
                    var value = p.Kind switch
                    {
                        TemplateParameterKind.Color => TemplateParameterValue.FromColor(raw.GetString()!),
                        TemplateParameterKind.Boolean => TemplateParameterValue.FromBoolean(raw.GetBoolean()),
                        _ => TemplateParameterValue.FromNumber(raw.GetDouble())
                    };
                    if (!TemplateParameterSchemas.Accepts(p, value)) throw new ArgumentException("Invalid template instance value.");
                    instance = instance.Set(p.Binding, value);
                }
                if (!items.TryAdd(recipe.Id, new(recipe) { Values = instance })) throw new ArgumentException("Duplicate template instance.");
            }
            recipes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, TemplateRecipeInstance>(items);
        }
        return new(c, t, preferences, lamp, recipes);
    }

    private static LampTemplateParameters ReadLamp(JsonElement node)
    {
        string[] fields = ["lightColor", "softness", "intensity", "background", "card", "backgroundOpacity"];
        Members(node, fields.Concat(new[] { "motionEnabled", "followPointer", "idleSway", "motionSpeed" }).ToArray());
        if (fields.Any(key => !node.TryGetProperty(key, out _))) throw new ArgumentException("Incomplete lamp parameters.");
        return new(Text(node, "lightColor", ""), Number(node, "softness", 0), Number(node, "intensity", 0),
            Text(node, "background", ""), Text(node, "card", ""), Number(node, "backgroundOpacity", 0),
            Boolean(node, "motionEnabled", true), Boolean(node, "followPointer", true), Number(node, "idleSway", 35), Number(node, "motionSpeed", 40));
    }
    private static void WriteMotion(Utf8JsonWriter writer, LampTemplateParameters lamp)
    {
        writer.WriteBoolean("motionEnabled", lamp.MotionEnabled); writer.WriteBoolean("followPointer", lamp.FollowPointer);
        writer.WriteNumber("idleSway", lamp.IdleSway); writer.WriteNumber("motionSpeed", lamp.MotionSpeed);
    }
    private static bool Boolean(JsonElement node, string key, bool fallback) => !node.TryGetProperty(key, out var value) ? fallback :
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw new ArgumentException("Invalid boolean: " + key);
    private static PosterContentPreferences ReadPreferences(JsonElement node)
    {
        Members(node, "mappingVersion", "scale", "spacing");
        if (!node.TryGetProperty("mappingVersion", out var version) || !version.TryGetInt32(out int v) || v != 1 ||
            !node.TryGetProperty("scale", out _) || !node.TryGetProperty("spacing", out _))
            throw new ArgumentException("Unknown or incomplete preference mapping.");
        return new(Number(node, "scale", 1), Number(node, "spacing", 1), v);
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
