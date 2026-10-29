using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasBehaviorTests
{
    private static CanvasDesign Design(params CanvasEventRule[] rules)
    {
        var draft = new CanvasAuthoring(CanvasAuthoring.NewArtwork());
        draft.Set(draft.WithNodes(draft.Design.Recipe.Nodes.Append(new("plate", "root", "draw.panel@1", new(),
            new Dictionary<string, CanvasValue> { ["fill"] = new(Text: "#123456"), ["opacity"] = new(Number: .43), ["radius"] = new(Number: 0) }))));
        return CanvasDeclaration.Freeze(draft.Design with { Recipe = draft.Design.Recipe with { Behavior = new(Rules: rules) } });
    }
    [Fact]
    public void Collapsed_style_inherits_actual_paint_or_explicit_custom_and_remains_optional()
    {
        var design = Design();
        Assert.Equal(new CanvasStatePaint("#123456", .43), CanvasBehaviorPolicy.Collapsed(design, "#000000", 1));
        var custom = design with { Recipe = design.Recipe with { Behavior = new("custom", "#FFFFFF", .2) } };
        Assert.Equal(new CanvasStatePaint("#FFFFFF", .2), CanvasBehaviorPolicy.Collapsed(custom, "#000000", 1));
        Assert.Null(CanvasDeclaration.Freeze(CanvasAuthoring.NewArtwork()).Recipe.Behavior);
    }
    [Fact]
    public void Event_values_are_finite_repeatable_and_paused_motion_reaches_end_without_mutating_design()
    {
        var design = Design(new("enter", "pointer.enter", "poster", "plate", "opacity", 1, 200),
            new("leave", "pointer.leave", "poster", "plate", "opacity", .43, 200));
        string digest = CanvasDeclaration.Digest(design); var runtime = new CanvasScriptRuntime(design, () => false, _ => Task.FromResult(false));
        runtime.Observe(new HashSet<string> { "poster" }, true); runtime.Advance(.1, true);
        Assert.InRange(runtime.Values.Value("plate", "opacity"), .7, .8);
        runtime.Observe(new HashSet<string> { "poster" }, true); runtime.Advance(.1, true);
        Assert.Equal(1, runtime.Values.Value("plate", "opacity")); Assert.False(runtime.Values.Animating);
        runtime.Observe(new HashSet<string>(), true); runtime.Advance(0, false);
        Assert.Equal(.43, runtime.Values.Value("plate", "opacity")); Assert.False(runtime.Values.Animating);
        Assert.Equal(digest, CanvasDeclaration.Digest(design)); runtime.Stop(); Assert.Equal(.43, runtime.Values.Value("plate", "opacity"));
    }
    [Theory]
    [InlineData("pointer.click", "offsetX", 25)]
    [InlineData("poster.expanded", "offsetY", -35)]
    [InlineData("pointer.enter", "rotation", 45)]
    public void Event_actions_are_absolute_offsets_and_last_rule_wins(string trigger, string action, double value)
    {
        var runtime = new CanvasScriptRuntime(Design(new("a", trigger, "poster", "plate", action, value, 0),
            new("b", trigger, "poster", "plate", action, value / 2, 0)), () => false, _ => Task.FromResult(false));
        runtime.Fire(trigger, "poster", true); runtime.Fire(trigger, "poster", true);
        Assert.Equal(value / 2, runtime.Values.Value("plate", action)); Assert.False(runtime.Values.Animating);
    }
    [Theory]
    [InlineData("file.delete", "opacity", .5)]
    [InlineData("pointer.enter", "script", 0)]
    [InlineData("pointer.enter", "opacity", 2)]
    [InlineData("pointer.enter", "offsetX", 257)]
    public void Unknown_and_out_of_budget_rules_fail_closed(string trigger, string action, double value) =>
        Assert.Throws<ArgumentException>(() => Design(new CanvasEventRule("bad", trigger, "poster", "plate", action, value)));
    [Fact]
    public void Native_slots_and_container_ancestors_cannot_be_animated_and_delete_cleans_bindings()
    {
        var design = Design(new CanvasEventRule("ok", "pointer.click", "poster", "plate", "opacity", .9));
        var content = design.Recipe.Nodes.Single(n => n.Type == "content.slot@1");
        Assert.Throws<ArgumentException>(() => CanvasDeclaration.Freeze(design with { Recipe = design.Recipe with
            { Behavior = new(Rules: [new("bad", "pointer.click", "poster", content.Id, "offsetY", 10)]) } }));
        var author = new CanvasAuthoring(design); author.Delete(["plate"]); Assert.Empty(author.Design.Recipe.Behavior!.Rules!);
        author.Undo(); Assert.Single(author.Design.Recipe.Behavior!.Rules!);
    }
    [Fact]
    public void Anonymous_observation_requires_matching_target_design_and_published_area()
    {
        var area = new CanvasRect(30, 40, 400, 600);
        var observation = new CanvasContentObservation(3, "digest", 7, 9, area, 200, true, [new("item-1", new(30, 40, 80, 90))]);
        Assert.Equal("current", CanvasContentContextPolicy.Match(observation, 3, "digest", area).Status);
        foreach (var stale in new[] { observation with { TargetVersion = 4 }, observation with { DesignDigest = "other" }, observation with { AreaDip = area with { X = 31 } } })
        { var result = CanvasContentContextPolicy.Match(stale, 3, "digest", area); Assert.Equal("stale", result.Status); Assert.Null(result.Observation); }
        Assert.Equal("unavailable", CanvasContentContextPolicy.Match(null, 3, "digest", area).Status);
        Assert.Contains("offsetX", CanvasSceneDescription.Describe().Actions);
    }
}
