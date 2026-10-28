using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Pure import-scoped reconciliation. Ordinary template registration remains strict.
// Identity derives from the complete declaration and the frozen destination read set,
// so preview and transactional revalidation cannot allocate different template copies.
internal static class PortableTemplateMerge
{
    internal sealed record Result(IReadOnlyList<PortablePosterProfile> Posters, string Catalog, IReadOnlyList<string> Warnings);

    internal static IReadOnlyList<TemplateRecipe> ReadCatalog(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() > TemplateRecipeCatalog.MaximumTemplates ||
            Encoding.UTF8.GetByteCount(node.GetRawText()) > TemplateRecipeCatalog.MaximumCatalogBytes)
            throw new ArgumentException("Invalid or oversized portable template catalog.");
        var recipes = node.EnumerateArray().Select(e => TemplateRecipeCodec.Parse(e.GetRawText(), stored: true)).ToArray();
        if (recipes.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != recipes.Length)
            throw new ArgumentException("Duplicate portable template identity.");
        return Array.AsReadOnly(recipes);
    }

    internal static Result Merge(PortableProfileDocument document, DesktopStateSnapshot current)
    {
        if (document.Version < 5) return new(document.Posters, current.Settings.Settings.TemplateCatalogJson, []);
        var incoming = ReadCatalog(document.TemplateCatalog!.Value);
        var local = TemplateRecipeCatalog.Read(current.Settings.Settings.TemplateCatalogJson, stored: true).ToList();
        var known = TemplateRecipeCatalog.BuiltIns.Concat(local).ToDictionary(r => r.Id, StringComparer.Ordinal);
        var reserved = known.Keys.Concat(incoming.Select(r => r.Id))
            .Concat(current.Catalog.Posters.Select(p => p.Poster.TemplateKey))
            .Concat(document.Posters.Select(p => p.TemplateKey)).ToHashSet(StringComparer.Ordinal);
        // Existing or incoming unselected instances must not be accidentally captured by a new identity.
        foreach (var json in current.Catalog.Posters.Select(p => p.Poster.LayoutParametersJson)
            .Concat(document.Posters.Select(p => p.LayoutParametersJson)))
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.TryGetProperty("headingVisibility", out var headings) && headings.ValueKind == JsonValueKind.Object)
                foreach (var entry in headings.EnumerateObject()) reserved.Add(entry.Name);
            if (parsed.RootElement.TryGetProperty("recipes", out var entries) && entries.ValueKind == JsonValueKind.Array)
                foreach (var entry in entries.EnumerateArray())
                    if (entry.TryGetProperty("definition", out var definition) && definition.TryGetProperty("id", out var id) &&
                        id.ValueKind == JsonValueKind.String) reserved.Add(id.GetString()!);
        }
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        var catalogMapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        bool changed = false;
        string Register(TemplateRecipe recipe, bool fromCatalog)
        {
            string canonical = TemplateRecipeCodec.Serialize(recipe, stored: true);
            if (mapped.TryGetValue(canonical, out var previous)) return previous;
            var module = SceneCapabilityRegistry.Find(recipe.Schema.RendererCapability);
            bool trustedBuiltIn = TemplateRecipeCatalog.BuiltIns.Concat(module?.PreviousBuiltIns ?? [])
                .Any(r => r.Id == recipe.Id && TemplateRecipeCodec.Serialize(r, stored: true) == canonical);
            if (trustedBuiltIn) { mapped.Add(canonical, recipe.Id); return recipe.Id; }
            if (known.TryGetValue(recipe.Id, out var existing) && Equivalent(existing, recipe))
            { mapped.Add(canonical, recipe.Id); return recipe.Id; }

            // A missing module's embedded built-in cannot be inserted under its original ID:
            // reinstalling that module would then collide with the local catalog. Use a safe
            // import identity; its declared capability/values remain exact and read-only.
            bool copy = known.ContainsKey(recipe.Id) ||
                !fromCatalog && (!SceneCapabilityRegistry.Available(recipe.Schema.RendererCapability) || reserved.Contains(recipe.Id));
            string target = recipe.Id;
            if (copy)
            {
                string basis = "import-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24].ToLowerInvariant();
                for (int suffix = 0; ; suffix++)
                {
                    if (suffix > TemplateRecipeCatalog.MaximumTemplates + reserved.Count)
                        throw new ArgumentException("Cannot allocate a safe template identity.");
                    target = suffix == 0 ? basis : basis + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (known.TryGetValue(target, out var candidate) && Equivalent(candidate, recipe))
                    { mapped.Add(canonical, target); return target; }
                    if (!reserved.Contains(target)) break;
                }
                warnings.Add($"模板“{recipe.Name}”：保留本机定义，导入另建副本 {recipe.Id} → {target}。");
            }
            var imported = TemplateRecipeCodec.Freeze(recipe with { Schema = recipe.Schema with { TemplateId = target } }, stored: true);
            local.Add(imported); known.Add(target, imported); reserved.Add(target); changed = true;
            if (local.Count + TemplateRecipeCatalog.BuiltIns.Count > TemplateRecipeCatalog.MaximumTemplates)
                throw new ArgumentException("合并后的模板册超过上限；未导入，请先整理模板册。");
            if (!SceneCapabilityRegistry.Available(recipe.Schema.RendererCapability))
                warnings.Add($"模板“{recipe.Name}”：模块 {recipe.Schema.RendererCapability} 未安装，保留声明与参数、只读显示；不会加载代码。");
            mapped.Add(canonical, target);
            return target;
        }
        foreach (var recipe in incoming) catalogMapping.Add(recipe.Id, Register(recipe, true));
        var posters = new List<PortablePosterProfile>();
        foreach (var poster in document.Posters)
        {
            var root = JsonNode.Parse(poster.LayoutParametersJson)!.AsObject();
            string selected = catalogMapping.GetValueOrDefault(poster.TemplateKey, poster.TemplateKey);
            var instanceMapping = new Dictionary<string, string>(catalogMapping, StringComparer.Ordinal);
            bool rewritten = false;
            if (root["recipes"] is JsonArray instances)
                foreach (var entry in instances)
                {
                    var definition = entry!["definition"]!.AsObject();
                    var recipe = TemplateRecipeCodec.Parse(definition.ToJsonString(), stored: true);
                    string target = Register(recipe, false);
                    instanceMapping[recipe.Id] = target;
                    // An embedded variant wins over a different same-ID source catalog definition.
                    if (recipe.Id == poster.TemplateKey) selected = target;
                    if (recipe.Id != target) { definition["id"] = target; rewritten = true; }
                }
            if (root["headingVisibility"] is JsonObject headings)
            {
                var mappedHeadings = new JsonObject();
                foreach (var pair in headings)
                {
                    string id = instanceMapping.GetValueOrDefault(pair.Key, pair.Key);
                    if (mappedHeadings.ContainsKey(id)) throw new ArgumentException("Conflicting heading identities after template merge.");
                    mappedHeadings.Add(id, pair.Value?.DeepClone()); rewritten |= id != pair.Key;
                }
                root["headingVisibility"] = mappedHeadings;
            }
            posters.Add(poster with { TemplateKey = selected,
                LayoutParameters = rewritten ? JsonSerializer.SerializeToElement(root) : poster.LayoutParameters });
        }
        string catalog = changed ? "[" + string.Join(",", local.Select(r => TemplateRecipeCodec.Serialize(r, stored: true))) + "]"
            : current.Settings.Settings.TemplateCatalogJson;
        int missingCapabilities = local.Select(r => r.Schema.RendererCapability).Distinct(StringComparer.Ordinal)
            .Count(capability => !SceneCapabilityRegistry.Available(capability));
        if (changed && local.Count + TemplateRecipeCatalog.BuiltIns.Count + missingCapabilities > TemplateRecipeCatalog.MaximumTemplates)
            throw new ArgumentException("模板册没有为缺失模块恢复预留足够容量；未导入，原配置不变。");
        _ = TemplateRecipeCatalog.Read(catalog, stored: true);
        return new(posters.AsReadOnly(), catalog, warnings.AsReadOnly());
    }

    private static bool Equivalent(TemplateRecipe a, TemplateRecipe b) =>
        TemplateRecipeCodec.Serialize(a with { Schema = a.Schema with { TemplateId = "comparison" } }, stored: true) ==
        TemplateRecipeCodec.Serialize(b with { Schema = b.Schema with { TemplateId = "comparison" } }, stored: true);
}
