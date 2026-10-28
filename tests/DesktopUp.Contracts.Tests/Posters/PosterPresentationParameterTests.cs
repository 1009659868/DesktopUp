using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;
using static DesktopUp.Contracts.Tests.PosterSpatialSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterPresentationParameterTests
{
    private static PosterDefinition Seed(string id = "A") => new(id, "My collection", "Independent content", "display",
        0, 600, 300, 600, 0, .6, .3, .6, false, true, false, "adaptive-grid", "{}", "night-ops");
    private static ResolvedPosterPresentation Read(string json, string template = "night-ops") =>
        PosterPresentationPolicy.Resolve(Seed() with { LayoutParametersJson = json, TemplateKey = template });

    [Fact]
    public void LegacyDefaultKeepsExistingMetricsAndDoesNotRewriteRawData()
    {
        const string raw = "{ \"itemGapDip\":12, \"cardHeightDip\":82, \"rowHeightDip\":118, \"future\":{\"source\":\"untouched\"} }";
        using var f = new Fixture(Seed() with { LayoutParametersJson = raw });
        var p = f.Current.PostersById["A"];
        Assert.True(p.Presentation.IsLegacy); Assert.True(p.Presentation.CanEdit);
        Assert.Equal(PosterContentMetrics.Current, p.Presentation.Metrics);
        Assert.Equal(raw, f.Coordinator.Refresh().PostersById["A"].Poster.LayoutParametersJson);
        var draft = PosterPresentationDraft.Open(p);
        var changed = draft with { Parameters = draft.Parameters with { Content = draft.Parameters.Content with { IconSizeDip = 40 } } };
        var result = f.Coordinator.Execute(changed.ToCommand("edit"));
        Assert.True(result.Succeeded, result.Error);
        using var json = JsonDocument.Parse(result.Projection!.PostersById["A"].Poster.LayoutParametersJson);
        using var old = JsonDocument.Parse(raw);
        Assert.True(JsonElement.DeepEquals(old.RootElement, json.RootElement.GetProperty("legacy")));
        Assert.False(result.Projection.PostersById["A"].Presentation.IsLegacy);
    }

    [Theory]
    [InlineData("night-ops", 346)]
    [InlineData("basic-box", 210)]
    public void BuiltInsHaveIndependentResolvedDefaultsAndCompleteRowMinimum(string key, int minimum)
    {
        var p = Read(PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(key)), key);
        Assert.True(p.CanEdit); Assert.Equal(minimum, p.Metrics.MinimumPixels(96));
        Assert.Equal(p.Parameters.Content.RowHeightDip, p.Metrics.CompleteItemRowDip);
        foreach (uint dpi in new uint[] { 96, 120, 144, 192 })
            Assert.Equal(Math.Ceiling(p.Metrics.ReservedTopDip * dpi / 96d) +
                Math.Ceiling(p.Metrics.CompleteItemRowDip * dpi / 96d) + Math.Ceiling(p.Metrics.ReservedBottomDip * dpi / 96d),
                p.Metrics.MinimumPixels(dpi));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"gapDip\":-1}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"iconSizeDip\":1000}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"labelFontSizeDip\":25}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"itemWidthDip\":72,\"iconSizeDip\":80}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"itemHeightDip\":48,\"iconSizeDip\":48}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"gapDip\":\"12\"}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"gapDip\":1e300}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{\"itemCount\":6}}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":{\"accent\":\"{x:Bind Dangerous}\"}}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":{\"background\":\"#00222222\"}}")]
    [InlineData("{\"schemaVersion\":1,\"content\":null}")]
    [InlineData("{\"schemaVersion\":1,\"content\":{},\"content\":{}}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":{\"accent\":\"#FFFFFF\",\"accent\":\"#000000\"}}")]
    [InlineData("{\"schemaVersion\":1,\"legacy\":[]}")]
    [InlineData("{\"rowHeightDip\":1}")]
    [InlineData("[]")]
    [InlineData("{")]
    public void InvalidOrUnknownDataUsesSafeReadOnlyFallbackWithoutExecutingOrClamping(string json)
    {
        var resolved = Read(json);
        Assert.True(resolved.IsFallback); Assert.False(resolved.CanEdit);
        Assert.NotNull(resolved.Warning); Assert.Equal("basic-box", resolved.EffectiveTemplateKey);
        Assert.Equal(PosterPresentationPolicy.Defaults("basic-box"), resolved.Parameters);
        Assert.Throws<ArgumentException>(() => PosterPresentationDraft.Open(new(1, 1,
            Seed() with { LayoutParametersJson = json }, true, true, true)));
    }

    [Fact]
    public void UnknownTemplateLayoutAndOversizedJsonDoNotDestroyOriginalConfiguration()
    {
        Assert.True(Read("{}", "future-template").IsFallback);
        Assert.True(PosterPresentationPolicy.Resolve(Seed() with { LayoutKind = "future-layout" }).IsFallback);
        Assert.True(Read(new string(' ', PosterPresentationPolicy.MaximumJsonLength + 1)).IsFallback);
        using var f = new Fixture(Seed() with { LayoutParametersJson = "{\"schemaVersion\":99,\"future\":true}", TemplateKey = "future-template" });
        Assert.True(f.Current.PostersById["A"].Presentation.IsFallback);
        Assert.Contains("99", f.Coordinator.Refresh().PostersById["A"].Poster.LayoutParametersJson);
    }

    [Fact]
    public void NonFiniteValuesAndInvalidColorsCannotBeSerialized()
    {
        var defaults = PosterPresentationPolicy.Defaults();
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(defaults with { Content = defaults.Content with { GapDip = bad } }));
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(defaults with { Theme = defaults.Theme with { Accent = "#GGFFFF" } }));
        var lower = defaults with { Theme = defaults.Theme with { Accent = "#aAbBcC" } };
        Assert.Equal("#AABBCC", Read(PosterPresentationPolicy.Serialize(lower)).Parameters.Theme.Accent);
    }

    [Fact]
    public void ParameterDefaultsAndOverridesAreDetachedValuesNotSharedMutableThemeState()
    {
        var a = PosterPresentationPolicy.Defaults("basic-box");
        var b = PosterPresentationPolicy.Defaults("basic-box");
        var changed = a with { Content = a.Content with { IconSizeDip = 32 }, Theme = a.Theme with { Accent = "#336699" } };
        Assert.NotEqual(changed, b); Assert.Equal(40, b.Content.IconSizeDip);
        Assert.Equal(210, Read(PosterPresentationPolicy.Serialize(changed), "basic-box").Metrics.MinimumPixels(96));
    }

    [Fact]
    public void GridColumnsAndCellWidthComeFromTheSameContentContract()
    {
        var content = new PosterContentParameters(120, 90, 48, 14, 12, 36);
        for (int width = 240; width <= 1600; width += 17)
        {
            int columns = PosterPresentationPolicy.ResolveColumns(width, content);
            double cell = PosterPresentationPolicy.CellWidth(width, columns, content.GapDip);
            Assert.True(columns >= 1 && columns <= 64);
            if (columns > 1) Assert.True(cell >= content.ItemWidthDip);
            Assert.Equal(width - PosterPresentationPolicy.HorizontalPaddingDip * 2,
                cell * columns + content.GapDip * (columns - 1), 8);
            Assert.Equal(48, content.IconSizeDip); // Resize never changes icon/content settings.
        }
    }

    [Fact]
    public void DraftStaysWithOriginalTargetAndOtherEntityEditsDoNotCreateFalseConflict()
    {
        using var f = new Fixture(); string b = f.Add("B");
        var a = f.Current.PostersById["A"]; var other = f.Current.PostersById[b];
        var draft = PosterPresentationDraft.Open(a) with { Title = "Games", Subtitle = "Per-poster data" };
        var bEdit = PosterPresentationDraft.Open(other) with { Title = "Documents", TemplateKey = "basic-box",
            Parameters = PosterPresentationPolicy.Defaults("basic-box") };
        Assert.True(f.Coordinator.Execute(bEdit.ToCommand("edit-b")).Succeeded);
        var command = draft.ToCommand("edit-a");
        Assert.Equal("A", command.PosterId); Assert.Single(command.ReadSet!.Posters);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Games", result.Projection!.PostersById["A"].Poster.Title);
        Assert.Equal("Documents", result.Projection.PostersById[b].Poster.Title);
        Assert.Equal("basic-box", result.Projection.PostersById[b].Presentation.EffectiveTemplateKey);
        Assert.Equal(a.Poster.ThemeInstanceId, result.Projection.PostersById["A"].Poster.ThemeInstanceId);
        Assert.Equal(result.Projection.Revision, result.Projection.ControlCenter.Revision);
        Assert.Equal(result.Projection.Revision, result.Projection.Tray.Revision);
    }

    [Fact]
    public void StaleDraftConflictsAndCancelledDraftHasNoWrite()
    {
        using var f = new Fixture(); var start = f.Current;
        var draft = PosterPresentationDraft.Open(start.PostersById["A"]) with { Title = "Unapplied" };
        Assert.Equal(start.Revision, f.Coordinator.Refresh().Revision);
        var command = PosterPresentationDraft.Open(start.PostersById["A"]).ToCommand("first");
        Assert.True(f.Coordinator.Execute(command).Succeeded);
        var after = f.Current;
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(draft.ToCommand("stale")).Status);
        Assert.Equal(after, f.Current); // Same immutable coordinator projection, no partial apply.
    }

    [Fact]
    public void ReplayedNoChangeAndReopenedRepositoryKeepIndependentParameters()
    {
        using var f = new Fixture();
        var draft = PosterPresentationDraft.Open(f.Current.PostersById["A"]);
        draft = draft with { Parameters = draft.Parameters with { Content = draft.Parameters.Content with { IconSizeDip = 32, GapDip = 8 } } };
        var command = draft.ToCommand("persist");
        var saved = f.Coordinator.Execute(command);
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        var reloaded = new StateProjectionCoordinator(new SqliteStateCommandRepository(f.Store)).Refresh();
        Assert.Equal(32, reloaded.PostersById["A"].Presentation.Parameters.Content.IconSizeDip);
        Assert.Equal(8, reloaded.PostersById["A"].Presentation.Parameters.Content.GapDip);
        Assert.Equal(600, reloaded.PostersById["A"].Poster.ExpandedHeightDip);
        Assert.Equal(StateCommandStatus.NoChange, f.Coordinator.Execute(PosterPresentationDraft.Open(reloaded.PostersById["A"]).ToCommand("identical")).Status);
        var clone = f.Coordinator.Execute(new ClonePosterCommand("clone", StateCommandSource.ControlCenter, f.Current.Revision,
            StateReadSet.ForPoster("A", f.Current.PostersById["A"].EntityVersion) with { CatalogVersion = f.Current.CatalogVersion }, "A"));
        Assert.True(clone.Succeeded);
        var copied = clone.Projection!.PostersById[clone.ResultPosterId!];
        Assert.NotEqual(copied.Poster.ThemeInstanceId, f.Current.PostersById["A"].Poster.ThemeInstanceId);
        var modify = PosterPresentationDraft.Open(copied);
        modify = modify with { Parameters = modify.Parameters with { Content = modify.Parameters.Content with { GapDip = 20 } } };
        Assert.True(f.Coordinator.Execute(modify.ToCommand("clone-edit")).Succeeded);
        Assert.Equal(8, f.Current.PostersById["A"].Presentation.Parameters.Content.GapDip);
    }

    [Theory]
    [InlineData("geometry")] [InlineData("instance")] [InlineData("source")] [InlineData("reads")]
    [InlineData("legacy-drop")] [InlineData("bad-json")] [InlineData("unknown-template")]
    public void ParameterCommandRejectsUnrelatedWritesOrInvalidPayloadAtomically(string fault)
    {
        using var f = new Fixture(); var before = f.Current;
        var valid = PosterPresentationDraft.Open(before.PostersById["A"]).ToCommand("invalid");
        var invalid = fault switch
        {
            "geometry" => valid with { DesiredState = valid.DesiredState with { WidthDip = 400 } },
            "instance" => valid with { DesiredState = valid.DesiredState with { ThemeInstanceId = "other-instance" } },
            "source" => valid with { Source = StateCommandSource.Tray },
            "reads" => valid with { ReadSet = null },
            "legacy-drop" => valid with { DesiredState = valid.DesiredState with { LayoutParametersJson = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults()) } },
            "unknown-template" => valid with { DesiredState = valid.DesiredState with { TemplateKey = "untrusted" } },
            _ => valid with { DesiredState = valid.DesiredState with { LayoutParametersJson = "{\"schemaVersion\":1,\"content\":{\"gapDip\":-1}}" } }
        };
        Assert.False(f.Coordinator.Execute(invalid).Succeeded);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Equal(before.PostersById["A"].Poster, f.Current.PostersById["A"].Poster);
    }

    [Fact]
    public void ParameterEditRetainsConstrainedBoundsAndUpdatesOnlyTheNextExpansionMinimum()
    {
        using var f = new Fixture(Seed() with { IsCollapsed = false });
        var old = f.Current.PostersById["A"];
        var cache = new EffectivePosterLayouts();
        var env = TestEnvironment with { WorkArea = new(0, 0, 1200, 1000) };
        var bounds = new SpatialRect(0, 500, 300, 450);
        cache.Set(old, bounds, env);
        var draft = PosterPresentationDraft.Open(old);
        draft = draft with { Parameters = draft.Parameters with { Content = draft.Parameters.Content with { ItemHeightDip = 200, LabelFontSizeDip = 24 } } };
        var result = f.Coordinator.Execute(draft.ToCommand("larger-content"));
        Assert.True(result.Succeeded, result.Error);
        var now = result.Projection!.PostersById["A"];
        Assert.True(now.Presentation.Metrics.MinimumPixels(96) > old.Presentation.Metrics.MinimumPixels(96));
        Assert.Equal(bounds, cache.Find(now)!.Bounds);
        Assert.Equal(now.Presentation.Metrics.Version, cache.Find(now)!.MetricsVersion);
        Assert.Equal(old.LayoutGeneration, now.LayoutGeneration);
        Assert.Equal(600, now.Poster.ExpandedHeightDip);
    }

    [Fact]
    public void SpatialCommitUsesTheTargetsMetricsAndRejectsAForeignTemplatesMetrics()
    {
        using var f = new Fixture(Seed() with { TemplateKey = "basic-box" });
        var state = f.Current; var target = state.PostersById["A"];
        var env = TestEnvironment with { WorkArea = new(0, 0, 1200, 1000) };
        var scene = new SpatialSnapshot(env, [new("A", new(0, 600, 300, 60), 600, true, false, true,
            target.LayoutGeneration, target.LastLayoutOperationId)]);
        var expansion = SpatialExpansionPolicy.Plan(scene, "A", 60, "expand", target.Presentation.Metrics);
        Assert.True(expansion.Group.Plan.Succeeded);
        var command = new PosterStateCommand("expand", StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, state.Revision,
            "A", SpatialGestureCommitPolicy.Capture(target.Poster, expansion.Group.Plan.Changes.Single().After, env, 600) with { IsCollapsed = false })
            { ReadSet = StateReadSet.ForPoster("A", target.EntityVersion) with { CatalogVersion = state.CatalogVersion },
                SpatialPosture = new(scene, false, 60, null) { AllowPush = true, ContentMetrics = target.Presentation.Metrics } };
        var foreign = command with { RequestId = "foreign", SpatialPosture = command.SpatialPosture with { ContentMetrics = PosterContentMetrics.Current } };
        Assert.False(f.Coordinator.Execute(foreign).Succeeded);
        Assert.Equal(state.Revision, f.Current.Revision);
        Assert.True(f.Coordinator.Execute(command).Succeeded);
    }

    [Fact]
    public void RecoveryRequiresAndUsesEachPostersOwnMetrics()
    {
        var env = TestEnvironment with { WorkArea = new(0, 0, 1000, 1000) };
        var scene = new SpatialSnapshot(env, [Poster("A", 0, 350, 240, 600),
            Poster("B", 0, 800, 240, 200) with { IsLocked = true }, Poster("U", 0, 0, 240, 350) with { IsLocked = true }]);
        var basic = Read("{}", "basic-box").Metrics;
        var huge = Read(PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults() with
            { Content = new(160, 240, 48, 12, 24, 48) })).Metrics;
        var metrics = new Dictionary<string, PosterContentMetrics> { ["A"] = basic, ["B"] = basic, ["U"] = huge };
        var restored = EffectivePosterLayouts.Rebuild(scene, metrics, 60);
        Assert.NotNull(restored);
        Assert.Equal(450, restored!.Posters.Single(p => p.PosterId == "A").Bounds.Height);
        metrics.Remove("A");
        Assert.Null(EffectivePosterLayouts.Rebuild(scene, metrics, 60));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.M05.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current { get { Assert.True(Coordinator.TryGetCurrent(out var p)); return p!; } }
        public Fixture(PosterDefinition? seed = null)
        {
            Store = new(new UserDataPaths(_root)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store));
            Coordinator.EnsureInitialized(seed ?? Seed());
        }
        public string Add(string title)
        {
            var created = Coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), Seed() with { Title = title }));
            Assert.True(created.Succeeded, created.Error); return created.ResultPosterId!;
        }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            // Only this fixture's own generated temporary directory is removed.
            Directory.Delete(_root, true);
        }
    }
}
