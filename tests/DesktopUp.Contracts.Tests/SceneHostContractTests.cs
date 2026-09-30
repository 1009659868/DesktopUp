using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class SceneHostContractTests
{
    [Theory]
    [InlineData(ScenePauseReason.Unavailable)]
    [InlineData(ScenePauseReason.Editing)]
    [InlineData(ScenePauseReason.ContentGesture)]
    [InlineData(ScenePauseReason.FrameGesture)]
    [InlineData(ScenePauseReason.SharedPolicy)]
    public void Every_host_pause_reason_prevents_motion(ScenePauseReason reason)
    {
        var frame = SceneFrameContext.Create(300, 400, 5, new(20, 30), true, reason);
        Assert.False(frame.CanAnimate);
        Assert.Equal(new ScenePointer(20, 30), frame.Pointer);
        Assert.Equal(300, frame.Width);
    }

    [Theory]
    [InlineData(false, ScenePauseReason.None, false)]
    [InlineData(true, ScenePauseReason.None, true)]
    [InlineData(true, ScenePauseReason.Editing | ScenePauseReason.SharedPolicy, false)]
    public void Visibility_and_pause_are_separate(bool visible, ScenePauseReason reasons, bool expected) =>
        Assert.Equal(expected, SceneFrameContext.Create(300, 400, 1, null, visible, reasons).CanAnimate);

    [Fact]
    public void Invalid_geometry_time_and_pointer_are_bounded()
    {
        var frame = SceneFrameContext.Create(double.NaN, double.PositiveInfinity, double.NaN,
            new(double.NaN, 10), true, ScenePauseReason.None);
        Assert.Equal(0, frame.Width); Assert.Equal(0, frame.Height); Assert.Equal(0, frame.Seconds);
        Assert.Null(frame.Pointer);
        Assert.Null(SceneFrameContext.Create(100, 100, -1, new(-1, 0), true, ScenePauseReason.None).Pointer);
        Assert.Null(SceneFrameContext.Create(100, 100, 0, new(101, 0), true, ScenePauseReason.None).Pointer);
    }

    [Fact]
    public void Failed_instance_does_not_retry_each_frame_or_fault_other_instances()
    {
        var failed = new SceneFailureBoundary(); var healthy = new SceneFailureBoundary(); int calls = 0;
        var error = new InvalidOperationException("paint fault");
        Assert.False(failed.TryRun(() => { calls++; throw error; }));
        for (int frame = 0; frame < 60; frame++) Assert.False(failed.TryRun(() => calls++));
        Assert.Equal(1, calls); Assert.Same(error, failed.Failure);
        Assert.True(healthy.TryRun(() => calls++)); Assert.Null(healthy.Failure); Assert.Equal(2, calls);
        Assert.True(new SceneFailureBoundary().TryRun(() => calls++));
    }

    [Theory]
    [InlineData("night-ops")]
    [InlineData("basic-box")]
    public void Minimal_surface_needs_no_lamp_painter_or_motion_bindings(string key)
    {
        var source = new ResolvedPosterPresentation(key, PosterPresentationPolicy.Defaults(key),
            PosterContentMetrics.Current, true, false, null);
        var result = ScenePresentationAdapter.Resolve(source);
        Assert.Equal(TemplateParameterSchemas.BasicCapability, result.Capability);
        Assert.Empty(result.Bindings);
        Assert.Equal(source.Parameters.Theme.Background, result.Surface.Background);
        Assert.Equal(245, result.Surface.BackgroundAlpha);
        Assert.Null(result.Surface.Card);
    }

    [Fact]
    public void Custom_minimal_material_survives_without_a_decorative_module()
    {
        var schema = TemplateParameterSchemas.ForTemplate("basic-box")! with { TemplateId = "custom-paper", Name = "Paper" };
        var recipe = new TemplateRecipe(schema, "Custom", new("#102030", "#405060", false));
        var value = new LampTemplateParameters(Background: "#102030", Card: "#405060", BackgroundOpacity: 40);
        var parameters = PosterPresentationPolicy.Defaults("basic-box") with
        {
            RecipeInstances = new Dictionary<string, TemplateRecipeInstance> { [recipe.Id] = new(recipe, value) }
        };
        var presentation = new ResolvedPosterPresentation("basic-box", parameters,
            PosterContentMetrics.Current, true, false, null) { SelectedRecipeId = recipe.Id };
        var result = ScenePresentationAdapter.Resolve(presentation);
        Assert.Empty(result.Bindings);
        Assert.Equal("#102030", result.Surface.Background);
        Assert.Equal("#405060", result.Surface.Card);
        Assert.Equal(102, result.Surface.BackgroundAlpha);
        Assert.Equal(value, presentation.Parameters.RecipeInstances![recipe.Id].Parameters);
        Assert.Same(parameters.RecipeInstances![recipe.Id].Values, presentation.TemplateValues);
    }

    [Fact]
    public void Legacy_lamp_bridge_preserves_values_without_writing_or_scaling_items()
    {
        var value = new LampTemplateParameters("#C1D2E3", 18, 81, "#123456", "#345678", 25,
            false, false, 21, 19);
        var source = new ResolvedPosterPresentation("lamp",
            PosterPresentationPolicy.Defaults() with { Lamp = value }, PosterContentMetrics.Current, true, false, null);
        var result = ScenePresentationAdapter.Resolve(source);
        Assert.Equal(TemplateParameterSchemas.LampCapability, result.Capability);
        Assert.Equal(10, result.Bindings.Count);
        Assert.Equal(value.LightColor, result.Bindings["lamp.color"].Color);
        Assert.Equal(value.MotionSpeed, result.Bindings["lamp.speed"].Number);
        Assert.False(result.Bindings["scene.motion"].Boolean);
        Assert.Equal(64, result.Surface.BackgroundAlpha);
        Assert.Equal(value.Background, result.Surface.Background); Assert.Equal(value.Card, result.Surface.Card);
        Assert.Same(value, source.Parameters.Lamp);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, TemplateParameterValue>)result.Bindings)
            .Add("unknown", TemplateParameterValue.FromNumber(9)));
    }
}
