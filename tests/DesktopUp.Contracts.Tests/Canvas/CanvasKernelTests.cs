using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class CanvasKernelTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void SamplesRoundTripAndShareExactlyOneHostContentSlot(int index)
    {
        var design = CanvasSamples.All[index];
        var copy = CanvasDeclaration.Freeze(CanvasDeclaration.Read<CanvasDesign>(CanvasDeclaration.Serialize(design)));
        Assert.Equal(CanvasDeclaration.Digest(design), CanvasDeclaration.Digest(copy));
        Assert.Single(copy.Recipe.Nodes.Where(n => n.Type == "content.slot@1"));
        var layout = CanvasLayout.Resolve(copy, 540, 720);
        Assert.True(layout.Content.Width > 100 && layout.Content.Height > 100);
        Assert.Throws<NotSupportedException>(() => ((IList<CanvasNode>)copy.Recipe.Nodes).Clear());
        Assert.Throws<ArgumentException>(() => TemplateRecipeCodec.Parse(CanvasDeclaration.Serialize(copy.Recipe)));
    }
    [Theory]
    [InlineData("unknown-field")] [InlineData("unknown-capability")] [InlineData("duplicate-slot")]
    [InlineData("missing-slot")] [InlineData("cycle")] [InlineData("range")]
    [InlineData("mixed-value")] [InlineData("path")] [InlineData("version")]
    [InlineData("missing-format")] [InlineData("missing-protocol")]
    public void UntrustedDeclarationsCannotExpandCapabilities(string kind)
    {
        var root = JsonNode.Parse(CanvasDeclaration.Serialize(CanvasSamples.All[1]))!;
        var recipe = root["recipe"]!; var nodes = recipe["nodes"]!.AsArray();
        switch (kind)
        {
            case "unknown-field": recipe["execute"] = "anything"; break;
            case "unknown-capability": nodes[1]!["type"] = "shell@1"; break;
            case "duplicate-slot": nodes.Add(nodes.Single(n => n!["id"]!.GetValue<string>() == "members")!.DeepClone()); break;
            case "missing-slot": nodes.Remove(nodes.Single(n => n!["id"]!.GetValue<string>() == "members")); break;
            case "cycle": nodes[1]!["parent"] = "members"; break;
            case "range": root["overrides"]!["opacity"] = new JsonObject { ["number"] = 9 }; break;
            case "mixed-value": root["overrides"]!["opacity"] = new JsonObject { ["number"] = .5, ["text"] = "x" }; break;
            case "path": recipe["resources"] = "C:/user/file"; break;
            case "version": recipe["protocolVersion"] = 99; break;
            case "missing-format": recipe.AsObject().Remove("format"); break;
            case "missing-protocol": recipe.AsObject().Remove("protocolVersion"); break;
        }
        Assert.Throws<ArgumentException>(() => CanvasDeclaration.Freeze(CanvasDeclaration.Read<CanvasDesign>(root.ToJsonString())));
    }
    [Fact]
    public void FlowReservesLongTextAndRemovingComponentReclaimsItsSpace()
    {
        var rainy = CanvasSamples.All[1];
        var longTitle = CanvasDeclaration.Freeze(rainy with { Overrides = new Dictionary<string, CanvasValue> { ["caption"] = new(Text: new string('雨', 100)) } });
        var full = CanvasLayout.Resolve(longTitle, 360, 1000);
        var without = CanvasLayout.Resolve(CanvasSamples.Without(longTitle, "caption"), 360, 1000);
        Assert.True(without.Content.Y < full.Content.Y);
        Assert.True(without.Content.Height > full.Content.Height);
        Assert.DoesNotContain(CanvasSamples.Without(rainy, "caption").Recipe.Parameters, p => p.Key == "caption");
        Assert.Throws<ArgumentException>(() => CanvasLayout.Resolve(longTitle, 100, 100));
        var cache = new CanvasLayoutCache(); Assert.Same(cache.Resolve(rainy, 540, 720), cache.Resolve(rainy, 540, 720));
    }
    [Fact]
    public void PreviewCancelWritesNothingAndOldTokenCannotApply()
    {
        using var f = new Fixture(); long revision = f.Current.Revision;
        using var session = f.Open(); session.Revise(CanvasSamples.All[1]); session.Preview(); string old = session.PreviewId!;
        Assert.NotNull(f.Host.Previewed); Assert.Equal(revision, f.Current.Revision);
        session.Revise(CanvasSamples.All[0]); Assert.Null(f.Host.Previewed);
        Assert.Throws<ArgumentException>(() => session.Apply(old));
        session.Dispose(); Assert.Equal(revision, f.Current.Revision);
    }
    [Fact]
    public void InvalidDraftAlsoCancelsPreviouslyPreparedPreview()
    {
        using var f = new Fixture(); using var s = f.Open(); s.Revise(CanvasSamples.All[0]); s.Preview();
        string token = s.PreviewId!;
        var invalid = CanvasSamples.All[0] with { Overrides = new Dictionary<string, CanvasValue> { ["background"] = new(Text: "#bad") } };
        Assert.Throws<ArgumentException>(() => s.Revise(invalid));
        Assert.Null(f.Host.Previewed); Assert.Null(s.PreviewId);
        Assert.Throws<ArgumentException>(() => s.Apply(token));
    }
    [Fact]
    public void ApplySaveRestartUndoPreservesContentAndOtherPresentation()
    {
        using var f = new Fixture(); var before = f.Current.PostersById["A"].Poster;
        using (var s = f.Open()) { s.Revise(CanvasSamples.All[0]); s.Preview(); Assert.True(s.Apply(s.PreviewId!).Succeeded); }
        using (var s = f.Open()) { s.Revise(CanvasSamples.All[1]); s.Preview(); string request = s.RequestId!; Assert.True(s.Apply(s.PreviewId!).Succeeded); Assert.True(s.WasCommitted(request)); }
        f.Reopen(); Assert.Equal("rainy-desk", f.Current.PostersById["A"].Presentation.Parameters.Canvas!.Current!.Recipe.Id);
        var unrelated = new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.TitleColors, TitleColors: new("#123456", "#654321")).Command("ink");
        Assert.True(f.Coordinator.Execute(unrelated).Succeeded);
        using (var s = f.Open()) { s.Preview(undo: true); Assert.True(s.Apply(s.PreviewId!).Succeeded); }
        var after = f.Current.PostersById["A"];
        Assert.Equal("basic-storage", after.Presentation.Parameters.Canvas!.Current!.Recipe.Id);
        Assert.Equal(before.ContentLayoutJson, after.Poster.ContentLayoutJson);
        Assert.Equal(before.CollectionId, after.Poster.CollectionId);
        Assert.Equal("#123456", after.Presentation.Parameters.TitleColors!.Text);
        Assert.False(after.Presentation.Parameters.Canvas.CanUndo);
    }
    [Fact]
    public void NativeRejectionAndTargetConflictLeaveOriginalState()
    {
        using var f = new Fixture(); long revision = f.Current.Revision;
        using var s = f.Open(); s.Revise(CanvasSamples.All[1]); f.Host.Accept = false;
        Assert.Throws<ArgumentException>(() => s.Preview()); Assert.Equal(revision, f.Current.Revision); Assert.Null(f.Host.Previewed);
        f.Host.Accept = true; s.Preview(); string token = s.PreviewId!;
        Assert.True(f.Coordinator.Execute(new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Subtitle, Text: "changed").Command("manual")).Succeeded);
        Assert.Throws<ArgumentException>(() => s.Apply(token)); Assert.Null(f.Current.PostersById["A"].Presentation.Parameters.Canvas);
    }
    [Fact]
    public void DurableRequestReplayDoesNotApplyTwiceOrAcceptDifferentPayload()
    {
        using var f = new Fixture(); var basis = f.Current;
        var command = new CanvasDesignCommand("once", basis.Revision, StateReadSet.ForPoster("A", basis.PostersById["A"].EntityVersion), "A", 0, CanvasSamples.All[1]);
        Assert.True(f.Coordinator.Execute(command).Succeeded); f.Reopen(); long revision = f.Current.Revision;
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status); Assert.Equal(revision, f.Current.Revision);
        Assert.False(f.Coordinator.Execute(command with { Design = CanvasSamples.All[0] }).Succeeded);
    }
    [Fact]
    public void LegacyTemplateSelectionExplicitlyLeavesCanvasAndCanBeUndone()
    {
        using var f = new Fixture(); using (var s = f.Open()) { s.Revise(CanvasSamples.All[1]); s.Preview(); Assert.True(s.Apply(s.PreviewId!).Succeeded); }
        Assert.True(f.Coordinator.Execute(new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Template, Text: "night-ops").Command("legacy")).Succeeded);
        Assert.Null(f.Current.PostersById["A"].Presentation.Parameters.Canvas!.Current);
        using var undo = f.Open(); undo.Preview(undo: true); Assert.True(undo.Apply(undo.PreviewId!).Succeeded);
        Assert.NotNull(f.Current.PostersById["A"].Presentation.Parameters.Canvas!.Current);
    }
    [Fact]
    public void UnknownCanvasDataRemainsReadOnlyAndCannotBeSilentlyOverwritten()
    {
        using var f = new Fixture(); using (var s = f.Open()) { s.Revise(CanvasSamples.All[0]); s.Preview(); Assert.True(s.Apply(s.PreviewId!).Succeeded); }
        var poster = f.Current.PostersById["A"].Poster;
        var json = JsonNode.Parse(poster.LayoutParametersJson)!; json["canvas"]!["current"]!["recipe"]!["protocolVersion"] = 42;
        var unknown = poster with { LayoutParametersJson = json.ToJsonString() };
        Assert.False(PosterPresentationPolicy.Resolve(unknown).CanEdit);
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(unknown, poster));
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), unknown.LayoutParametersJson));
    }
    private sealed class Host : ICanvasPreviewHost
    {
        public bool Accept = true;
        public PosterStateCommand? Previewed;
        public bool Preview(PosterStateCommand command) { Previewed = command; return Accept; }
        public void Restore(string posterId) => Previewed = null;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "DesktopUp.Canvas.Tests", Guid.NewGuid().ToString("N"));
        private SqliteStateStore store;
        public StateProjectionCoordinator Coordinator { get; private set; }
        public readonly Host Host = new();
        public DesktopUpProjection Current => Coordinator.Refresh();
        public Fixture()
        {
            store = new(new UserDataPaths(path)); Assert.True(store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(store));
            var seed = new PosterDefinition("A", "Canvas sample", "Items", "display", 0, 720, 540, 720, 0, .7, .4, .7,
                false, true, false, "adaptive-grid", "{}", "basic-box")
                { IsCollapsed = false, ContentLayoutJson = ContentLayoutPolicy.Serialize(ContentLayoutSamples.Equal("A", 6)) };
            Coordinator.EnsureInitialized(seed);
        }
        public CanvasDesignSession Open() => new(Coordinator, Coordinator, Host, "A");
        public void Reopen() { store.Dispose(); store = new(new UserDataPaths(path)); Assert.True(store.Initialize().Succeeded); Coordinator = new(new SqliteStateCommandRepository(store)); Coordinator.Refresh(); }
        public void Dispose() { store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(path, true); }
    }
}
