using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed class UxContentPreferenceTests
{
    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")]
    public void Standard_card_matches_desktop_icon_and_heat_remains_an_independent_multiplier(string template)
    {
        var parameters = PosterPresentationPolicy.Defaults(template);
        var doc = ContentLayoutSamples.Equal("A", 1) with { AllowHeatDrivenSize = true };
        var view = new ContentLayoutViewport(600, 600).WithPresentation(parameters);
        var standard = Assert.Single(ContentLayoutSolver.Resolve(doc, view).Items);
        double label = Math.Max(20, parameters.Content.LabelHeightDip + 7);
        var image = ContentCardPresentationPolicy.ImageArea(standard.Bounds.Width, standard.Bounds.Height, label);
        Assert.Equal(48, Math.Min(image.Width, image.Height), 8);
        var heat = new ContentHeatProposal("A", doc.ItemSetVersion, 1,
            [new(doc.Placements[0].ItemId, 0, 1.5)], ContentLayoutProposals.ContinuousMappingVersion)
            { AuthorDocumentVersion = doc.DocumentVersion };
        var derived = ContentHeatSizing.Project(doc, heat, view);
        var hot = Assert.Single(ContentLayoutSolver.Resolve(derived, view).Items);
        Assert.Equal(standard.Bounds.Width * 1.5, hot.Bounds.Width, 8);
        Assert.Equal(4, doc.Placements[0].ColumnSpan);
        Assert.Equal(new[] { .4, 1d, 1.8 }, PosterContentPreferencePolicy.ScalePresets);
        // A metrics change must resolve again, never reuse old paint/hit coordinates.
        Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Reclip(
            ContentLayoutSolver.Resolve(doc, view), view with { BaseMetricsScale = 1 }));
    }

    [Theory]
    [InlineData(1.25, 1.25)] [InlineData(3, 1.8)]
    public void Legacy_scale_normalizes_without_losing_the_theme_or_accepting_invalid_new_values(double oldScale, double expected)
    {
        var parameters = PosterPresentationPolicy.Defaults("basic-box") with { Preferences = new() };
        var raw = JsonNode.Parse(PosterPresentationPolicy.Serialize(parameters))!;
        raw["preferences"]!["mappingVersion"] = 1;
        raw["preferences"]!["scale"] = oldScale;
        var poster = Seed() with { TemplateKey = "basic-box", LayoutParametersJson = raw.ToJsonString() };
        var read = PosterPresentationPolicy.Resolve(poster);
        Assert.True(read.CanEdit);
        Assert.Equal(expected, read.Parameters.Preferences!.Scale);
        Assert.Equal(2, read.Parameters.Preferences.MappingVersion);
        Assert.Equal(parameters.Theme, read.Parameters.Theme);
        Assert.Equal(parameters.Content, read.Parameters.Content);
        Assert.Equal(oldScale, JsonNode.Parse(poster.LayoutParametersJson)!["preferences"]!["scale"]!.GetValue<double>());
        raw["preferences"]!["mappingVersion"] = 2;
        raw["preferences"]!["scale"] = 1.801;
        Assert.False(PosterPresentationPolicy.Resolve(poster with { LayoutParametersJson = raw.ToJsonString() }).CanEdit);
    }
    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")]
    public void Legacy_neutral_is_exact_and_first_macro_write_is_versioned(string template)
    {
        var before = PosterPresentationPolicy.Defaults(template);
        string json = PosterPresentationPolicy.Serialize(before);
        Assert.Equal(1, JsonDocument.Parse(json).RootElement.GetProperty("schemaVersion").GetInt32());
        var old = Seed() with { TemplateKey = template, LayoutParametersJson = json };
        Assert.Equal(before.Content, PosterPresentationPolicy.Resolve(old).EffectiveContent);
        var changed = before with { Preferences = new(1.25, 1.3) };
        string current = PosterPresentationPolicy.Serialize(changed, json);
        var desired = old with { LayoutParametersJson = current };
        Assert.Null(PosterPresentationPolicy.ValidateEdit(old, desired));
        Assert.Equal(2, JsonDocument.Parse(current).RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(changed, PosterPresentationPolicy.Resolve(desired).Parameters);
        Assert.Equal(before.Content, changed.Content);
    }

    [Theory]
    [InlineData(.4, .5)] [InlineData(1, 1)] [InlineData(1.8, 1.5)] [InlineData(1.1735, .83)]
    public void Repeated_macro_changes_never_multiply_saved_base_or_drift(double scale, double spacing)
    {
        var p = PosterPresentationPolicy.Defaults("basic-box"); var basis = p.Content;
        for (int i = 0; i < 100; i++)
        {
            p = p with { Preferences = new(scale, spacing) };
            var poster = Seed() with { LayoutParametersJson = PosterPresentationPolicy.Serialize(p) };
            var resolved = PosterPresentationPolicy.Resolve(poster);
            Assert.True(resolved.CanEdit); Assert.Equal(basis, resolved.Parameters.Content);
            Assert.Equal(basis.IconSizeDip * scale, resolved.EffectiveContent.IconSizeDip);
            Assert.Equal(basis.GapDip * spacing, resolved.EffectiveContent.GapDip);
            p = resolved.Parameters;
        }
        Assert.Equal(basis, PosterContentPreferencePolicy.Resolve(basis, new()));
    }

    [Theory]
    [InlineData(.39, 1)] [InlineData(1.801, 1)] [InlineData(1, .49)] [InlineData(1, 1.51)]
    [InlineData(double.NaN, 1)] [InlineData(double.PositiveInfinity, 1)]
    public void Invalid_preferences_cannot_be_serialized(double scale, double spacing) =>
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults() with { Preferences = new(scale, spacing) }));

    [Fact]
    public void Heat_relative_size_order_and_pins_survive_macro_changes()
    {
        var items = new[] { new PosterItemVisualInput("hot", 2, 0), new("pinned", .5, 1, true), new("cold", 1, 2) };
        var parameters = PosterPresentationPolicy.Defaults() with { Preferences = new(1.2, .7) };
        var metrics = PosterContentPreferencePolicy.ResolveItems(parameters, items);
        Assert.Equal(items, metrics.Select(m => m.Item));
        Assert.Equal(4, metrics[0].Content.ItemWidthDip / metrics[1].Content.ItemWidthDip, 10);
        Assert.Equal(4, metrics[0].Content.IconSizeDip / metrics[1].Content.IconSizeDip, 10);
        var dense = PosterContentPreferencePolicy.ResolveItems(parameters with { Preferences = new(1.2, 1.5) }, items);
        Assert.Equal(metrics.Select(m => m.Content.IconSizeDip), dense.Select(m => m.Content.IconSizeDip));
        Assert.All(metrics, m => Assert.True(m.Content.LabelFontSizeDip >= 9));
    }

    [Fact]
    public void Unknown_versions_stay_read_only_and_legacy_payload_survives()
    {
        var old = Seed() with { LayoutParametersJson = "{\"custom\":\"preserve\"}" };
        var p = PosterPresentationPolicy.Resolve(old).Parameters with { Preferences = new() };
        var next = old with { LayoutParametersJson = PosterPresentationPolicy.Serialize(p, old.LayoutParametersJson) };
        Assert.Null(PosterPresentationPolicy.ValidateEdit(old, next));
        Assert.Contains("preserve", next.LayoutParametersJson);
        string bad = next.LayoutParametersJson.Replace("\"mappingVersion\":2", "\"mappingVersion\":99");
        Assert.False(PosterPresentationPolicy.Resolve(next with { LayoutParametersJson = bad }).CanEdit);
        Assert.False(PosterPresentationPolicy.Resolve(next with { LayoutParametersJson = "{\"schemaVersion\":999}" }).CanEdit);
    }
}
