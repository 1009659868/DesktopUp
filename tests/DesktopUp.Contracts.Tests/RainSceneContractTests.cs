using DesktopUp.Application;
using DesktopUp.Domain;
namespace DesktopUp.Contracts.Tests;

public sealed class RainSceneContractTests
{
    private static TemplateRecipe Recipe => TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "rain");
    private static PosterDefinition Poster(string json, string id = "rain") => new("rain-test", "Cloud", "Items", "display", 0, 0, 320, 600,
        .1, .1, .2, .6, false, false, false, "adaptive-grid", json, id);
    [Fact]
    public void Rain_is_registered_with_distinct_schema_and_no_opacity_control()
    {
        var schema = Recipe.Schema;
        Assert.Equal(8, schema.Properties.Count);
        Assert.Null(TemplateParameterSchemas.Validate(schema));
        Assert.DoesNotContain(schema.Properties, p => p.Binding.StartsWith("lamp.") || p.Binding == "surface.opacity");
        Assert.Equal(64, schema.Properties.Single(p => p.Binding == "rain.count").Maximum);
    }
    [Fact]
    public void Rain_generic_values_roundtrip_without_modifying_legacy_lamp()
    {
        var basis = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults() with { Lamp = new(Intensity: 11) }, Recipe);
        var values = TemplateRecipeInstancePolicy.CurrentValues(basis, Recipe.Id)
            .Set("rain.count", TemplateParameterValue.FromNumber(64)).Set("cloud.color", TemplateParameterValue.FromColor("#ABCDEF"));
        var changed = TemplateRecipeInstancePolicy.UpdateValues(basis, Recipe.Id, values);
        var read = PosterPresentationPolicy.Resolve(Poster(PosterPresentationPolicy.Serialize(changed)));
        Assert.True(read.CanEdit, read.Warning); Assert.Equal(values, read.TemplateValues); Assert.Equal(11, read.Parameters.Lamp!.Intensity);
        var paint = ScenePresentationAdapter.Resolve(read);
        Assert.Equal("rain.paint@1", paint.Capability); Assert.Equal(0, paint.Surface.BackgroundAlpha); Assert.True(paint.ReplacesMetaText);
    }
    [Fact]
    public void Rain_missing_module_keeps_raw_parameters_and_uses_basic_title_but_is_readonly()
    {
        var state = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults(), Recipe);
        string raw = PosterPresentationPolicy.Serialize(state);
        var before = PosterPresentationPolicy.Resolve(Poster(raw));
        var after = PosterPresentationPolicy.Resolve(Poster(raw.Replace("rain.paint@1", "missing.rain@1")));
        Assert.False(after.CanEdit); Assert.Equal(0, before.MetaHeightDip);
        Assert.Equal(PosterTitleChromeKind.Cloud, before.TitleChrome); Assert.Equal(84, before.HeaderHeightDip);
        Assert.Equal(PosterTitleChromeKind.Basic, after.TitleChrome); Assert.Equal(46, after.HeaderHeightDip);
        Assert.Equal(before.HeaderHeightDip + PosterContentMetrics.ItemsTopInsetDip, before.Metrics.ReservedTopDip);
        Assert.Equal(before.TemplateValues, after.TemplateValues);
        Assert.Equal(TemplateParameterSchemas.BasicCapability, ScenePresentationAdapter.Resolve(after).Capability);
    }
    [Theory]
    [InlineData("rain.count", 100)]
    [InlineData("rain.speed", 3)]
    public void Custom_recipe_cannot_expand_renderer_numeric_capacity(string binding, double max)
    {
        var recipe = Recipe with { Schema = Recipe.Schema with { TemplateId = "rain-copy",
            Properties = Recipe.Schema.Properties.Select(p => p.Binding == binding ? p with { Maximum = max } : p).ToArray() } };
        Assert.Throws<ArgumentException>(() => TemplateRecipeCodec.Freeze(recipe));
    }
    [Fact]
    public void Decoration_slot_cannot_be_removed_by_custom_recipe()
    { Assert.Throws<ArgumentException>(() => TemplateRecipeCodec.Freeze(Recipe with { Base = Recipe.Base with { Heading = false } })); }
    [Theory]
    [InlineData(240, 360)] [InlineData(400, 500)] [InlineData(1200, 700)]
    public void Wind_envelope_stays_in_chrome_and_clear_of_content(double width, double height)
    {
        var area = RainSceneMotion.Envelope(SceneFrameContext.Create(width, height, 0, null, true, 0) with
        { HeaderBottom = 60, ContentTop = 210, FooterTop = height - 42 });
        for (int i = 0; i < 190; i++)
        {
            var pose = RainSceneMotion.Cloud(i / 10.0, 100);
            double rotationEnvelope = area.Width * Math.Sin(Math.Abs(pose.Angle) * Math.PI / 180) / 2;
            Assert.True(area.Top + area.Height + pose.Y + rotationEnvelope <= 190);
            Assert.True(area.Left + pose.X - area.Height * .011 >= 0);
            Assert.True(area.Left + area.Width + pose.X + area.Height * .011 <= width);
        }
    }
    [Fact]
    public void Zero_wind_is_static_and_bad_inputs_remain_finite()
    {
        Assert.Equal(RainSceneMotion.Cloud(0, 0), RainSceneMotion.Cloud(50, 0));
        var f = SceneFrameContext.Create(double.NaN, double.PositiveInfinity, 0, null, true, 0);
        var a = RainSceneMotion.Envelope(f);
        Assert.True(double.IsFinite(a.Width) && a.Width >= 0 && a.Height >= 0);
        Assert.Equal(new RainCloudPose(0, 0, 0), RainSceneMotion.Cloud(double.NaN, double.NaN));
    }
    [Fact]
    public void Pool_is_bounded_deterministic_and_bad_indices_are_rejected()
    {
        var a = new RainSceneEnvelope(28, 64, 300, 104, 650);
        for (int i = 0; i < 64; i++)
        {
            var p = RainSceneMotion.Drop(i, 13, 2, 100, a);
            Assert.InRange(p.X, a.Left, a.Left + a.Width); Assert.InRange(p.Y, a.Top + a.Height - 51, a.Bottom);
            Assert.Equal(p, RainSceneMotion.Drop(i, 13, 2, 100, a));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => RainSceneMotion.Drop(64, 0, 1, 0, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => RainSceneMotion.Drop(-1, 0, 1, 0, a));
    }
    [Fact]
    public void Pause_resume_and_shared_clock_reset_do_not_catch_up()
    {
        var clock = new SceneActiveTime();
        Assert.Equal(0, clock.Step(4, true)); Assert.Equal(.05, clock.Step(4.05, true), 6);
        double before = clock.Step(5, false); Assert.Equal(before, clock.Step(100, true));
        Assert.Equal(before + .03, clock.Step(100.03, true), 6);
        before = clock.Seconds; Assert.Equal(before, clock.Step(0, true));
        clock.Stop(); Assert.Equal(before, clock.Step(800, true));
    }
    [Fact]
    public void Lightning_is_local_low_frequency_and_disabled_has_zero_output()
    {
        int active = 0;
        for (int i = 0; i < 2300; i++)
        {
            double value = RainSceneMotion.Lightning(i / 100.0, true);
            Assert.InRange(value, 0, .24); if (value > 0) active++;
            Assert.Equal(0, RainSceneMotion.Lightning(i / 100.0, false));
        }
        Assert.InRange(active, 65, 71);
    }
}
