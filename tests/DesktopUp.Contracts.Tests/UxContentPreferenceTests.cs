using System.Text.Json;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ControlCenterPosterEditorTests;

namespace DesktopUp.Contracts.Tests;

public sealed class UxContentPreferenceTests
{
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
    [InlineData(.8, .5)] [InlineData(1, 1)] [InlineData(1.5, 1.5)] [InlineData(1.17, .83)]
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
    [InlineData(.79, 1)] [InlineData(1.51, 1)] [InlineData(1, .49)] [InlineData(1, 1.51)]
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
        string bad = next.LayoutParametersJson.Replace("\"mappingVersion\":1", "\"mappingVersion\":99");
        Assert.False(PosterPresentationPolicy.Resolve(next with { LayoutParametersJson = bad }).CanEdit);
        Assert.False(PosterPresentationPolicy.Resolve(next with { LayoutParametersJson = "{\"schemaVersion\":999}" }).CanEdit);
    }
}
