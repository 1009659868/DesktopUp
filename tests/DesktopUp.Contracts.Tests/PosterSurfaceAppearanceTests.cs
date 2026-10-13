using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.R7PortableFidelityTests;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterSurfaceAppearanceTests
{
    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")] [InlineData("lamp")] [InlineData("rain")]
    public void Common_surface_changes_only_background_and_survives_roundtrip(string template)
    {
        var recipe = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == template);
        var parameters = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults(template), recipe);
        var poster = Poster() with { TemplateKey = template, LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) };
        var before = PosterPresentationPolicy.Resolve(poster);
        var paint = ScenePresentationAdapter.Resolve(before);
        foreach (double opacity in new[] { 0d, 25d, 100d })
        {
            var saved = poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters with { Surface = new("#123456", opacity) }, poster.LayoutParametersJson) };
            var after = PosterPresentationPolicy.Resolve(saved);
            Assert.True(after.CanEdit, after.Warning); Assert.Null(PosterPresentationPolicy.ValidateEdit(poster, saved));
            var scene = ScenePresentationAdapter.Resolve(after);
            Assert.Equal("#123456", scene.Surface.Background);
            Assert.Equal((byte)Math.Round(opacity * 255 / 100), scene.Surface.BackgroundAlpha);
            Assert.Equal(paint.Surface.Card, scene.Surface.Card); Assert.Equal(paint.Capability, scene.Capability);
            Assert.Equal(before.TemplateValues, after.TemplateValues); Assert.Equal(before.Metrics, after.Metrics);
            Assert.Equal(before.Parameters.Content, after.Parameters.Content);
        }
        // Unmodified old settings retain the original template's paint.
        Assert.Null(before.Parameters.Surface);
        if (template == "rain") Assert.Equal(0, paint.Surface.BackgroundAlpha);
    }

    [Fact]
    public void Older_writer_retains_surface_legacy_and_heading_and_explicit_erase_is_rejected()
    {
        var parameters = PosterPresentationPolicy.Defaults() with { Surface = new("#ABCDEF", 25),
            HeadingVisibility = new TemplateHeadingVisibility([]).Set("night-ops", false) };
        string json = PosterPresentationPolicy.Serialize(parameters, "{\"original\":true}");
        string rewrite = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json);
        var read = PosterPresentationPolicy.ReadStoredParameters(rewrite, "night-ops");
        Assert.Equal(parameters.Surface, read.Surface); Assert.Equal(parameters.HeadingVisibility, read.HeadingVisibility);
        using var doc = JsonDocument.Parse(rewrite);
        Assert.Equal(8, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(doc.RootElement.GetProperty("legacy").GetProperty("original").GetBoolean());
        var poster = Poster() with { LayoutParametersJson = json };
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(poster, poster with {
            LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters with { Surface = null }) }));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":8}")]
    [InlineData("{\"schemaVersion\":8,\"surface\":{\"color\":\"#112233\"}}")]
    [InlineData("{\"schemaVersion\":8,\"surface\":{\"color\":\"#112233\",\"opacityPercent\":101}}")]
    [InlineData("{\"schemaVersion\":8,\"surface\":{\"color\":\"red\",\"opacityPercent\":25}}")]
    [InlineData("{\"schemaVersion\":8,\"surface\":{\"color\":\"#112233\",\"opacityPercent\":25,\"future\":true}}")]
    public void Invalid_surface_is_read_only_without_rewriting(string json)
    {
        Assert.False(PosterPresentationPolicy.Resolve(Poster() with { LayoutParametersJson = json }).CanEdit);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json));
    }

    [Fact]
    public void Portable_append_keeps_common_surface()
    {
        var poster = Poster() with { LayoutParametersJson = PosterPresentationPolicy.Serialize(
            PosterPresentationPolicy.Defaults() with { Surface = new("#654321", 34) }) };
        var plan = PortableProfilePlanner.Plan(Document(State([poster])), State([]), Topology(), PortableImportMode.Append, false, () => "new");
        Assert.Equal(new("#654321", 34), PosterPresentationPolicy.Resolve(Assert.Single(plan.Creates)).Parameters.Surface);
    }
}

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Surface_preview_cancel_save_template_switch_and_reopen_are_isolated()
    {
        using var f = new Fixture(); var peer = f.Add(); var before = f.Current;
        var session = new ControlCenterSettingsSession();
        var value = new PosterSurfaceAppearance("#876543", 25);
        Assert.True(session.Update(new(before.PostersById["A"], PosterSetting.Surface, Surface: value)));
        session.CancelPreview(); Assert.Null(session.TakeNext()); Assert.Equal(before.Revision, f.Current.Revision);
        Assert.True(session.Update(new(before.PostersById["A"], PosterSetting.Surface, Surface: value)));
        session.Finish(); Assert.True(session.Complete(f.Coordinator.Execute(session.TakeNext()!)));
        Assert.True(session.Update(new(f.Current.PostersById["A"], PosterSetting.Template, Text: "lamp")));
        session.Finish(); Assert.True(session.Complete(f.Coordinator.Execute(session.TakeNext()!)));
        using var store = new SqliteStateStore(f.Paths); Assert.True(store.Initialize().Succeeded);
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(store)).Refresh();
        Assert.Equal(value, reopened.PostersById["A"].Presentation.Parameters.Surface);
        Assert.Equal(before.PostersById[peer].Poster, reopened.PostersById[peer].Poster);
    }
}
