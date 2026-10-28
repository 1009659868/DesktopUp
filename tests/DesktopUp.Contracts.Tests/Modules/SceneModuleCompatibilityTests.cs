using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

// This declaration exists only in the test assembly; it adds no product template or executable code path.
internal static class ModuleContractFixture
{
    internal static readonly TemplateRecipe Weather = new(new("test-weather", "Weather", "fixture.weather@1",
    [
        new("drops", "weather.drops", TemplateParameterKind.Range, "Drops", "Weather", TemplateParameterValue.FromNumber(12),
            Unit: "", Minimum: 0, Maximum: 48, Step: 1),
        new("ink", "weather.ink", TemplateParameterKind.Color, "Ink", "Weather", TemplateParameterValue.FromColor("#93C9E9")),
        new("wind", "weather.wind", TemplateParameterKind.Boolean, "Wind", "Weather", TemplateParameterValue.FromBoolean(true))
    ]), "Fixture", new("#17212A", "#294256", true));
    [ModuleInitializer]
    internal static void Register() => SceneCapabilityRegistry.Register(new("fixture.weather@1", Weather, ExposeTemplate: false));
}

public sealed class SceneModuleCompatibilityTests
{
    private static TemplateRecipe Recipe => ModuleContractFixture.Weather;
    private static PosterDefinition Poster(string id, string json) =>
        new("module-test", "Module", "Items", "display", 0, 0, 320, 600, .1, .1, .2, .6,
            false, false, false, "adaptive-grid", json, id);
    private static PosterPresentationParameters Parameters() => TemplateRecipeInstancePolicy.Select(
        PosterPresentationPolicy.Defaults() with { Lamp = new(Intensity: 13, BackgroundOpacity: 25) }, Recipe);
    [Fact]
    public void Second_schema_has_no_lamp_fields_and_round_trips_independently()
    {
        var state = Parameters();
        var values = TemplateRecipeInstancePolicy.CurrentValues(state, Recipe.Id);
        Assert.DoesNotContain(values.Bindings.Keys, key => key.StartsWith("lamp.", StringComparison.Ordinal));
        values = values.Set("weather.drops", TemplateParameterValue.FromNumber(31)).Set("weather.wind", TemplateParameterValue.FromBoolean(false));
        var changed = TemplateRecipeInstancePolicy.UpdateValues(state, Recipe.Id, values);
        var resolved = PosterPresentationPolicy.Resolve(Poster(Recipe.Id, PosterPresentationPolicy.Serialize(changed)));
        Assert.True(resolved.CanEdit, resolved.Warning);
        Assert.Equal(values, resolved.TemplateValues);
        Assert.Equal(13, resolved.Parameters.Lamp!.Intensity);
        Assert.Equal(12, TemplateRecipeInstancePolicy.CurrentValues(state, Recipe.Id).Number("weather.drops", -1));
        Assert.Equal(3, TemplateParameterBindingAdapter.Read(Recipe.Schema, resolved.TemplateValues).Count);
    }
    [Fact]
    public void Forms_compose_and_reset_only_declared_values_for_a_non_lamp_schema()
    {
        var basis = TemplateRecipeInstancePolicy.DefaultValues(Recipe);
        var ui = TemplateParameterBindingAdapter.Read(Recipe.Schema, basis).ToDictionary(p => p.Key, p => p.Value);
        ui["ink"] = TemplateParameterValue.FromColor("#010203");
        var changed = TemplateParameterBindingAdapter.ComposeDraft(Recipe.Schema, ui, basis);
        Assert.Equal("#010203", changed.Color("weather.ink", ""));
        Assert.Equal(basis, TemplateParameterBindingAdapter.Reset(Recipe.Schema, changed));
        Assert.Equal("#294256", changed.Color("item.color", ""));
    }
    [Fact]
    public void Values_are_copied_and_structurally_equal_not_dictionary_identity()
    {
        var source = new Dictionary<string, TemplateParameterValue> { ["fixture.value"] = TemplateParameterValue.FromNumber(1) };
        var a = new TemplateParameterValues(source); var b = new TemplateParameterValues(source);
        source["fixture.value"] = TemplateParameterValue.FromNumber(2);
        Assert.Equal(a, b); Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(1, a.Number("fixture.value", -1));
        Assert.NotEqual(a, a.Set("fixture.value", source["fixture.value"]));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, TemplateParameterValue>)a.Bindings).Clear());
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(49)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Generic_values_cannot_bypass_schema_bounds(double number)
    {
        var state = Parameters();
        var invalid = TemplateRecipeInstancePolicy.CurrentValues(state, Recipe.Id).Set("weather.drops", TemplateParameterValue.FromNumber(number));
        Assert.Throws<ArgumentException>(() => TemplateRecipeInstancePolicy.UpdateValues(state, Recipe.Id, invalid));
    }
    [Fact]
    public void Undeclared_values_and_wrong_types_are_rejected_before_persistence()
    {
        var state = Parameters(); var values = TemplateRecipeInstancePolicy.CurrentValues(state, Recipe.Id);
        foreach (var invalid in new[] { values.Set("weather.secret", TemplateParameterValue.FromNumber(1)),
            values.Set("weather.drops", TemplateParameterValue.FromColor("#000000")) })
            Assert.Throws<ArgumentException>(() => TemplateRecipeInstancePolicy.UpdateValues(state, Recipe.Id, invalid));
    }
    [Fact]
    public void Unknown_module_is_archived_read_only_not_executed_or_rewritten()
    {
        var state = Parameters();
        state = state with { Content = state.Content with { GapDip = 21 } };
        string raw = PosterPresentationPolicy.Serialize(state).Replace("fixture.weather@1", "missing.weather@9", StringComparison.Ordinal);
        var resolved = PosterPresentationPolicy.Resolve(Poster(Recipe.Id, raw));
        Assert.False(resolved.CanEdit); Assert.Contains("模块未安装", resolved.Warning);
        Assert.Equal(21, resolved.Parameters.Content.GapDip);
        Assert.Equal(12, resolved.TemplateValues.Number("weather.drops", -1));
        Assert.Equal(TemplateParameterSchemas.BasicCapability, ScenePresentationAdapter.Resolve(resolved).Capability);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(resolved.Parameters, raw));
        var definition = JsonNode.Parse(raw)!["recipes"]![0]!["definition"]!.ToJsonString();
        Assert.Throws<ArgumentException>(() => TemplateRecipeCodec.Parse(definition));
        Assert.Equal(Recipe.Id, TemplateRecipeCodec.Parse(definition, stored: true).Id);
        Assert.Equal(raw, Poster(Recipe.Id, raw).LayoutParametersJson);
    }
    [Fact]
    public void Stored_catalog_is_available_for_backup_when_module_is_missing_but_new_import_is_rejected()
    {
        var raw = "[" + TemplateRecipeCodec.Serialize(Recipe).Replace("fixture.weather@1", "missing.weather@9", StringComparison.Ordinal) + "]";
        Assert.Single(TemplateRecipeCatalog.Read(raw, stored: true));
        Assert.Null(AuthoritativeStateValidationPolicy.Validate(new DesktopUpSettings(true, DesktopUpPerformanceMode.Static, false)
            { TemplateCatalogJson = raw }));
        Assert.Throws<ArgumentException>(() => TemplateRecipeCatalog.Read(raw));
        Assert.Throws<ArgumentException>(() => TemplateRecipeCatalog.Add("[]", TemplateRecipeCodec.Parse(
            TemplateRecipeCodec.Serialize(Recipe).Replace("fixture.weather@1", "missing.weather@9", StringComparison.Ordinal), stored: true)));
    }
    [Fact]
    public void Generic_scene_adapter_uses_the_second_capability_not_a_template_id_branch()
    {
        var state = Parameters();
        var resolved = PosterPresentationPolicy.Resolve(Poster(Recipe.Id, PosterPresentationPolicy.Serialize(state)));
        var scene = ScenePresentationAdapter.Resolve(resolved);
        Assert.Equal("fixture.weather@1", scene.Capability);
        Assert.Equal(12, scene.Bindings["weather.drops"].Number);
        Assert.Equal("#294256", scene.Surface.Card);
    }
}
