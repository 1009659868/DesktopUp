using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasScriptTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Editor_newlines_end_comments_preserve_commands_and_report_the_correct_line(string newline)
    {
        string source = string.Join(newline, "// 样例说明", "poster.toggle();", "// 第二段说明", "wait(10);");
        var program = CanvasScript.Compile(source);
        Assert.Equal(new[] { "poster.toggle", "wait" }, program.Instructions.Select(i => i.Op));
        Assert.Contains("第2行", Assert.Throws<ArgumentException>(() => CanvasScript.Compile("// 注释" + newline + "bad();")).Message);
        Assert.Equal(source.Replace(newline, "\n"), CanvasScript.NormalizeLineEndings(source));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_default_templates_toggle_once_only_from_the_v_control(bool initiallyCollapsed)
    {
        foreach (var template in CanvasBehaviorPrefabs.Templates)
        {
            var design = CanvasAuthoring.NewArtwork();
            var title = design.Recipe.Nodes.Single(n => n.Type == "host.title@1");
            var toggle = CanvasBehaviorPrefabs.ToggleSource(design);
            design = CanvasDeclaration.Freeze(design with { Recipe = design.Recipe with { Behavior = new(
                Prefabs: [template], Bindings: [new("toggle", toggle, "pointer.click", template.Id, "poster")]) } });
            bool collapsed = initiallyCollapsed; int calls = 0;
            var runtime = new CanvasScriptRuntime(design, () => collapsed, next => { calls++; collapsed = next; return Task.FromResult(true); });
            Assert.False(runtime.Fire("pointer.click", title.Id, true));
            Assert.False(runtime.Fire("pointer.click", title.Id + "/name", true));
            Assert.False(runtime.Fire("pointer.click", title.Id + "/more", true));
            Assert.True(runtime.Fire("pointer.click", toggle, true));
            Assert.Equal(!initiallyCollapsed, collapsed); Assert.Equal(1, calls);
            runtime.Fire("pointer.click", toggle, true);
            Assert.Equal(initiallyCollapsed, collapsed); Assert.Equal(2, calls);
        }
    }
    [Fact]
    public void Host_controls_describe_the_same_native_geometry_and_preserve_old_bindings()
    {
        var design = CanvasAuthoring.NewArtwork();
        var behavior = CanvasBehaviorPrefabs.ForEditing(design);
        design = design with { Recipe = design.Recipe with { Behavior = behavior } };
        Assert.Same(behavior, CanvasBehaviorPrefabs.ForEditing(design));
        var packet = CanvasSceneDescription.Read(design, 1, 1);
        var title = CanvasSurfaceGeometry.Title(design, design.Recipe.Artboard!.Width, design.Recipe.Artboard.Height, false, PosterTitleChromeKind.Basic);
        Assert.Equal(3, packet.HostControls.Count);
        var control = Assert.Single(packet.HostControls.Where(c => c.CanBind));
        Assert.Equal(CanvasBehaviorPrefabs.ToggleSource(design), control.Id);
        Assert.Equal(new CanvasRect(title.Collapse.X, title.Collapse.Y, title.Collapse.Width, title.Collapse.Height), control.BoundsDip);
        var invalid = design with { Recipe = design.Recipe with { Behavior = behavior with {
            Bindings = [behavior.Bindings!.Single() with { Source = control.Parent }] } } };
        Assert.Throws<ArgumentException>(() => CanvasDeclaration.Validate(invalid));
    }
    [Fact]
    public void Sample_catalog_does_not_consume_legacy_rule_migration_budget()
    {
        var design = Design("poster.toggle();");
        design = design with { Recipe = design.Recipe with { Behavior = new(Rules:
            Enumerable.Range(0, 32).Select(i => new CanvasEventRule("r" + i, "pointer.enter", "poster", "plate", "opacity", .5, 0)).ToArray()) } };
        var converted = CanvasBehaviorPrefabs.ForEditing(design);
        Assert.Equal(33, converted.Prefabs!.Count);
        CanvasBehaviorPrefabs.Validate(converted, design.Recipe.Nodes);
    }
    private static CanvasDesign Design(string code, params CanvasBehaviorBinding[] bindings)
    {
        var draft = new CanvasAuthoring(CanvasAuthoring.NewArtwork());
        draft.Set(draft.WithNodes(draft.Design.Recipe.Nodes.Append(new("plate", "root", "draw.panel@1", new(.1, .3, .3, .2),
            new Dictionary<string, CanvasValue> { ["fill"] = new(Text: "#112233"), ["opacity"] = new(Number: 1), ["radius"] = new(Number: 0) }))));
        return CanvasDeclaration.Freeze(draft.Design with { Recipe = draft.Design.Recipe with { Behavior = new(Prefabs: [new("custom", "自写行为", code)], Bindings: bindings) } });
    }
    [Theory]
    [InlineData("while (true) { poster.toggle(); }")]
    [InlineData("System.IO.File.Delete(\"x\");")]
    [InlineData("fetch(\"https://example.com\");")]
    [InlineData("set(\"opacity\", 2);")]
    [InlineData("wait(2001);")]
    [InlineData("if (unknown) { poster.toggle(); }")]
    [InlineData("animate(\"opacity\", 1, 20, \"bounce\");")]
    [InlineData("if (collapsed) { poster.toggle();")]
    public void Compiler_rejects_unknown_operations_and_reports_a_line(string code)
    { Assert.Contains("行", Assert.Throws<ArgumentException>(() => CanvasScript.Compile(code)).Message); }

    [Fact]
    public void Sequential_prefabs_wait_for_host_completion_and_share_real_state()
    {
        string code = "if (expanded) { poster.collapse(); } else { poster.expand(); }\nwait(100); set(\"opacity\", 0.4);";
        var design = Design(code, new CanvasBehaviorBinding("a", "plate", "pointer.click", "custom", "plate"));
        bool collapsed = false; var completion = new TaskCompletionSource<bool>();
        var runtime = new CanvasScriptRuntime(design, () => collapsed, desired => { Assert.True(desired); return completion.Task; });
        Assert.True(runtime.Fire("pointer.click", "plate", true)); runtime.Advance(.1, true);
        Assert.Equal(1, runtime.Values.Value("plate", "opacity"));
        collapsed = true; completion.SetResult(true); runtime.Advance(.1, true); runtime.Advance(.1, true);
        Assert.Equal(.4, runtime.Values.Value("plate", "opacity")); Assert.False(runtime.Active);
        string original = CanvasDeclaration.Digest(design); runtime.Stop(); Assert.Equal(original, CanvasDeclaration.Digest(design));
    }
    [Fact]
    public void Ordered_bindings_conditions_repeat_cancel_and_failure_do_not_continue()
    {
        var scene = Design("wait(200); set(\"opacity\", 0.2);", new CanvasBehaviorBinding("a", "plate", "pointer.enter", "custom", "plate"));
        var b = scene.Recipe.Behavior!;
        scene = CanvasDeclaration.Freeze(scene with { Recipe = scene.Recipe with { Behavior = b with {
            Prefabs = b.Prefabs!.Append(new("leave", "离开", "set(\"opacity\", 0.8);")).ToArray(),
            Bindings = b.Bindings!.Concat([new("b", "plate", "pointer.leave", "leave", "plate"), new("c", "plate", "pointer.leave", "custom", "plate", "collapsed")]).ToArray() } } });
        var runtime = new CanvasScriptRuntime(scene, () => false, _ => Task.FromResult(false));
        runtime.Fire("pointer.enter", "plate", true); runtime.Advance(.1, true);
        runtime.Fire("pointer.leave", "plate", true); runtime.Advance(.1, true); runtime.Advance(.1, true);
        Assert.Equal(.8, runtime.Values.Value("plate", "opacity"));
        var failed = new CanvasScriptRuntime(Design("poster.toggle(); set(\"opacity\", 0.1);", new CanvasBehaviorBinding("a", "plate", "pointer.click", "custom", "plate")), () => false, _ => Task.FromResult(false));
        failed.Fire("pointer.click", "plate", true); Assert.NotNull(failed.Error); Assert.Equal(1, failed.Values.Value("plate", "opacity"));
    }
    [Fact]
    public void Budgets_semantic_targets_and_automatic_posture_recursion_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => CanvasScript.Compile(new string(' ', 8193)));
        Assert.Throws<ArgumentException>(() => CanvasScript.Compile(string.Concat(Enumerable.Repeat("wait(1);", 129))));
        Assert.Throws<ArgumentException>(() => Design("poster.toggle();", new CanvasBehaviorBinding("a", "poster", "poster.expanded", "custom", "poster")));
        Assert.Throws<ArgumentException>(() => Design("set(\"opacity\", 0);", new CanvasBehaviorBinding("a", "poster", "pointer.click", "custom", "poster")));
        var scene = Design("set(\"opacity\", 0.2);", new CanvasBehaviorBinding("a", "plate", "pointer.click", "custom", "plate"));
        var author = new CanvasAuthoring(scene); author.Delete(["plate"]); Assert.Empty(author.Design.Recipe.Behavior!.Bindings!);
        author.Undo(); Assert.Single(author.Design.Recipe.Behavior!.Bindings!);
    }
    [Fact]
    public void Prefab_library_roundtrip_does_not_rewrite_an_existing_design_snapshot()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.Canvas.Script.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new CanvasWorkspace(new UserDataPaths(root)); var scene = Design("poster.toggle();");
            var prefab = scene.Recipe.Behavior!.Prefabs!.Single(); workspace.SaveBehavior(prefab);
            Assert.Equal(prefab, new CanvasWorkspace(new UserDataPaths(root)).Behaviors().Single());
            workspace.SaveBehavior(prefab with { Script = "poster.expand();" });
            Assert.Equal("poster.toggle();", scene.Recipe.Behavior.Prefabs!.Single().Script);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void Canvas_UI_does_not_replace_native_context_or_wheel_target()
    {
        var action = new PosterInputRegion(PosterInputTargetKind.CanvasAction, 2, new(0, 0, 100, 100));
        var content = new PosterInputRegion(PosterInputTargetKind.ContentViewport, -1, new(0, 0, 100, 100)) { ContentSnapshotGeneration = 7 };
        var key = new PosterInputSessionKey("poster", 1); var router = new PosterInputRouter(); router.Register(key);
        router.Publish(key, new(0, 0, 100, 100), new(1, 100, 100, [action, content]));
        Assert.Equal(action, router.Resolve(50, 50, [key])!.Region);
        var context = router.Resolve(50, 50, [key], includeCanvasActions: false)!;
        Assert.Equal(content, context.Region); Assert.NotNull(router.ResolveContext(context, new(50, 50), [key]));
        router.Publish(key, new(0, 0, 100, 100), new(2, 100, 100, [action, content]));
        Assert.Null(router.ResolveContext(context, new(50, 50), [key]));
    }
    [Fact]
    public void Locked_UI_protects_its_binding_and_shared_script_code()
    {
        var design = Design("poster.toggle();", new CanvasBehaviorBinding("a", "plate", "pointer.click", "custom", "poster"));
        design = design with { Recipe = design.Recipe with { Nodes = design.Recipe.Nodes.Select(n => n.Id == "plate" ? n with { Locked = true } : n).ToArray() } };
        var changed = design.Recipe.Behavior! with { Prefabs = [new("custom", "自写行为", "poster.expand();")] };
        Assert.Throws<ArgumentException>(() => CanvasBehaviorPrefabs.RequireUnlockedEdit(design, changed));
        CanvasBehaviorPrefabs.RequireUnlockedEdit(design, design.Recipe.Behavior);
    }
    [Fact]
    public void Reusable_UI_carries_independent_behavior_ids_and_rejects_external_targets()
    {
        var design = Design("poster.toggle();", new CanvasBehaviorBinding("a", "plate", "pointer.click", "custom", "poster"));
        var source = new CanvasAuthoring(design); var behavior = source.ExportBehavior("plate");
        var destination = new CanvasAuthoring(CanvasAuthoring.NewArtwork());
        string id = destination.Insert(source.Export("plate"), "root", behavior: behavior);
        var binding = destination.Design.Recipe.Behavior!.Bindings!.Single(b => b.Source == id);
        Assert.NotEqual("custom", binding.Prefab); Assert.Equal("poster", binding.Target);
        Assert.Equal("poster.toggle();", destination.Design.Recipe.Behavior.Prefabs!.Single(p => p.Id == binding.Prefab).Script);
        destination.Delete([id]); Assert.DoesNotContain(destination.Design.Recipe.Behavior.Bindings!, b => b.Source == id);
        var invalid = behavior! with { Bindings = [new("bad", "outside", "pointer.click", "custom", "poster")] };
        string digest = CanvasDeclaration.Digest(destination.Design);
        Assert.Throws<ArgumentException>(() => destination.Insert(source.Export("plate"), "root", behavior: invalid));
        Assert.Equal(digest, CanvasDeclaration.Digest(destination.Design));
    }
    [Fact]
    public void Posture_title_hits_use_fixed_composition_not_the_clipped_viewport_height()
    {
        var design = CanvasAuthoring.NewArtwork();
        var monitor = new DisplayMonitorSnapshot("display", true, new(0, 0, 1400, 1000), new(0, 0, 1400, 1000), 96, 96);
        var parameters = PosterPresentationPolicy.Defaults() with { Canvas = new(1, design, null, false, "test") };
        var theme = new ResolvedPosterPresentation("basic-box", parameters, PosterContentMetrics.Current, true, true, null);
        var expected = CanvasSurfaceGeometry.Title(design, 720, 540, false, PosterTitleChromeKind.Basic);
        foreach (int height in new[] { 46, 80, 260, 540 })
        {
            var input = ContentInputGeometry.Resolve(1, new(0, 0, 720, height),
                new(monitor, new(0, 0, 720, 540, false, false, 1), theme, default, false, false, null, null, 0, 0, true, 0, 540));
            var hit = input.Regions.Single(r => r.Kind == PosterInputTargetKind.CollapseButton);
            Assert.Equal((int)Math.Round(expected.Collapse.Y), hit.Bounds.Y);
        }
    }
    [Fact]
    public void Rotated_behavior_hit_uses_shape_and_stale_release_remains_rejected()
    {
        var region = new PosterInputRegion(PosterInputTargetKind.CanvasAction, 2, new(0, 0, 100, 100)) { CanvasAction = new(new(20, 40, 60, 20), 45, 1, 1) };
        Assert.True(PosterInputGeometry.Contains(region, 50, 50)); Assert.False(PosterInputGeometry.Contains(region, 10, 10));
        var router = new PosterInputRouter(); var key = new PosterInputSessionKey("poster", 1); router.Register(key);
        router.Publish(key, new(0, 0, 100, 100), new(1, 100, 100, [region]));
        var capture = router.Begin(router.Resolve(50, 50, [key])!)!;
        Assert.False(router.CanInvoke(capture, 10, 10));
        router.Publish(key, new(0, 0, 100, 100), new(2, 100, 100, [region])); Assert.False(router.CanInvoke(capture, 50, 50));
    }
}
