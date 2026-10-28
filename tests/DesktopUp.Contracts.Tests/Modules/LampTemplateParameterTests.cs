using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class LampTemplateParameterTests
{
    private static PosterDefinition Seed(string id = "A") => new(id, "Desk", "Items", "display",
        0, 600, 300, 600, 0, .6, .3, .6, false, true, false, "adaptive-grid", "{}", "night-ops");
    private static readonly LampTemplateParameters Custom = new("#123456", 17, 89, "#264831", "#405162", 38);
    private static PosterPresentationParameters Parameters(LampTemplateParameters? lamp = null) =>
        PosterPresentationPolicy.Defaults() with { Lamp = lamp };
    private static ResolvedPosterPresentation Read(string raw, string key = "lamp") =>
        PosterPresentationPolicy.Resolve(Seed() with { TemplateKey = key, LayoutParametersJson = raw });
    private static JsonObject Serialized()
    {
        string raw = PosterPresentationPolicy.Serialize(Parameters(Custom));
        Assert.True(Read(raw).CanEdit, "The valid source must resolve before a malformed variant is constructed.");
        var root = JsonNode.Parse(raw)!.AsObject();
        Assert.Equal(4, root["schemaVersion"]!.GetValue<int>());
        Assert.NotNull(root["templates"]?["lamp"]);
        return root;
    }

    [Fact]
    public void LampIsADistinctBuiltInAndItsDefaultsMatchCWithoutChangingNightOpsOrGeometry()
    {
        Assert.True(PosterPresentationPolicy.IsBuiltIn("lamp"));
        Assert.Equal(new LampTemplateParameters("#FFDC99", 60, 70, "#17221E", "#354237", 100), LampTemplatePolicy.Defaults);
        var lamp = Read("{}"); var night = Read("{}", "night-ops");
        Assert.True(lamp.CanEdit); Assert.Equal("lamp", lamp.EffectiveTemplateKey);
        Assert.Equal("night-ops", night.EffectiveTemplateKey);
        Assert.Equal(night.Metrics, lamp.Metrics);
        Assert.Null(night.Parameters.Lamp);
    }

    [Fact]
    public void PaintAndMotionFieldsUseSchemaFourAndRoundTripIndependentlyOfPreferences()
    {
        foreach (var preferences in new PosterContentPreferences?[] { null, new(1.2, .7) })
        {
            var value = Parameters(Custom) with { Preferences = preferences };
            string raw = PosterPresentationPolicy.Serialize(value);
            using var json = JsonDocument.Parse(raw);
            Assert.Equal(4, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(10, json.RootElement.GetProperty("templates").GetProperty("lamp").EnumerateObject().Count());
            var read = Read(raw); Assert.True(read.CanEdit, read.Warning);
            Assert.Equal(value, read.Parameters);
            Assert.Equal(Read("{}").Metrics, (Read(PosterPresentationPolicy.Serialize(Parameters(Custom)))).Metrics);
        }
    }

    [Fact]
    public void OldCallerWithNoLampCannotEraseStoredLampOrPreferences()
    {
        var old = Parameters(Custom) with { Preferences = new(1.2, .7) };
        string raw = PosterPresentationPolicy.Serialize(old);
        var earlierCaller = Parameters() with { Theme = old.Theme with { Accent = "#ABCDEF" } };
        var saved = Read(PosterPresentationPolicy.Serialize(earlierCaller, raw));
        Assert.True(saved.CanEdit, saved.Warning); Assert.Equal(Custom, saved.Parameters.Lamp);
        Assert.Equal(old.Preferences, saved.Parameters.Preferences);
        Assert.Equal("#ABCDEF", saved.Parameters.Theme.Accent);
    }

    [Fact]
    public void ExistingV1V2RemainAtTheirVersionsAndOpaqueLegacyMeaningIsRetained()
    {
        const string legacy = "{\"future\":{\"value\":[1,\"keep\",true]},\"cardHeightDip\":82}";
        foreach (var preference in new PosterContentPreferences?[] { null, new(1.2, .7) })
        {
            var value = Parameters() with { Preferences = preference };
            string raw = PosterPresentationPolicy.Serialize(value, legacy);
            using var json = JsonDocument.Parse(raw); using var original = JsonDocument.Parse(legacy);
            Assert.Equal(preference is null ? 1 : 2, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.False(json.RootElement.TryGetProperty("templates", out _));
            Assert.True(JsonElement.DeepEquals(original.RootElement, json.RootElement.GetProperty("legacy")));
            string upgraded = PosterPresentationPolicy.Serialize(value with { Lamp = Custom }, raw);
            using var next = JsonDocument.Parse(upgraded);
            Assert.True(JsonElement.DeepEquals(original.RootElement, next.RootElement.GetProperty("legacy")));
            Assert.Equal(value with { Lamp = Custom }, Read(upgraded).Parameters);
        }
    }

    [Theory]
    [InlineData("root")] [InlineData("template")] [InlineData("field")] [InlineData("missing")]
    [InlineData("null")] [InlineData("number")] [InlineData("range")] [InlineData("color")]
    [InlineData("version")] [InlineData("version-type")] [InlineData("v2-lamp")]
    [InlineData("missing-templates")] [InlineData("empty-templates")]
    public void UnknownMalformedAndIncompleteLampDataIsReadOnlyAndCannotBeOverwritten(string fault)
    {
        var root = Serialized(); var lamp = root["templates"]!["lamp"]!.AsObject();
        switch (fault)
        {
            case "root": root["future"] = true; break;
            case "template": root["templates"]!["rain"] = new JsonObject(); break;
            case "field": lamp["future"] = 3; break;
            case "missing": lamp.Remove("softness"); break;
            case "null": root["templates"]!["lamp"] = null; break;
            case "number": lamp["softness"] = "60"; break;
            case "range": lamp["intensity"] = 101; break;
            case "color": lamp["lightColor"] = "url(file:///secret)"; break;
            case "version": root["schemaVersion"] = 99; break;
            case "version-type": root["schemaVersion"] = "3"; break;
            case "v2-lamp": root["schemaVersion"] = 2; root["preferences"] = new JsonObject
                { ["mappingVersion"] = 1, ["scale"] = 1, ["spacing"] = 1 }; break;
            case "missing-templates": root.Remove("templates"); break;
            case "empty-templates": root["templates"] = new JsonObject(); break;
        }
        string raw = root.ToJsonString(); var read = Read(raw);
        Assert.True(read.IsFallback); Assert.NotNull(read.Warning);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(Parameters(), raw));
    }

    [Fact]
    public void DuplicateTemplateOrFieldCannotBeSilentlyAccepted()
    {
        string raw = PosterPresentationPolicy.Serialize(Parameters(Custom));
        foreach (string invalid in new[] { raw.Replace("\"softness\":17", "\"softness\":17,\"softness\":17"),
            raw.Replace("\"templates\":{", "\"templates\":{},\"templates\":{") })
        {
            Assert.NotEqual(raw, invalid); Assert.True(Read(invalid).IsFallback);
            Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(Parameters(), invalid));
        }
    }

    [Fact]
    public void AllPercentEndpointsAreValidAndNonFiniteOrOutOfRangeIsRejectedWithoutClamping()
    {
        foreach (double endpoint in new[] { 0d, 100d })
        {
            var lamp = Custom with { Softness = endpoint, Intensity = endpoint, BackgroundOpacity = endpoint };
            Assert.Equal(lamp, Read(PosterPresentationPolicy.Serialize(Parameters(lamp))).Parameters.Lamp);
        }
        foreach (double bad in new[] { -.01, 100.01, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
            foreach (var lamp in new[] { Custom with { Softness = bad }, Custom with { Intensity = bad }, Custom with { BackgroundOpacity = bad } })
                Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(Parameters(lamp)));
        foreach (var lamp in new[] { Custom with { LightColor = "#00000000" }, Custom with { Background = "red" }, Custom with { Card = null! } })
            Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(Parameters(lamp)));
    }

    [Fact]
    public void ExplicitSchemaDowngradeCannotDropSavedLampOverride()
    {
        var previous = Seed() with { TemplateKey = "lamp", LayoutParametersJson = PosterPresentationPolicy.Serialize(Parameters(Custom)) };
        var desired = previous with { TemplateKey = "basic-box", LayoutParametersJson = PosterPresentationPolicy.Serialize(Parameters()) };
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(previous, desired));
        var reset = previous with { LayoutParametersJson = PosterPresentationPolicy.Serialize(Parameters(LampTemplatePolicy.Defaults), previous.LayoutParametersJson) };
        Assert.Null(PosterPresentationPolicy.ValidateEdit(previous, reset));
    }

    [Fact]
    public void TemplateSwitchAndOtherPosterEditsPreserveIndependentLampValuesThroughRealCasAndSqliteReopen()
    {
        using var f = new Fixture(); string b = f.Add("B");
        var start = f.Current; var draft = PosterPresentationDraft.Open(start.PostersById["A"]);
        var edit = draft with { TemplateKey = "lamp", Parameters = draft.Parameters with { Lamp = Custom } };
        var command = edit.ToCommand("lamp-save"); var saved = f.Coordinator.Execute(command);
        Assert.True(saved.Succeeded, saved.Error); Assert.Equal(start.Revision + 1, saved.CurrentRevision);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(Custom, f.Current.PostersById["A"].Presentation.Parameters.Lamp);
        Assert.Null(f.Current.PostersById[b].Presentation.Parameters.Lamp);
        var stale = edit.ToCommand("lamp-stale");
        foreach (string key in new[] { "basic-box", "night-ops", "lamp" })
        {
            var current = PosterPresentationDraft.Open(f.Current.PostersById["A"]);
            var switched = f.Coordinator.Execute((current with { TemplateKey = key }).ToCommand("switch-" + key));
            Assert.True(switched.Succeeded, switched.Error);
            Assert.Equal(Custom, switched.Projection!.PostersById["A"].Presentation.Parameters.Lamp);
        }
        var other = PosterPresentationDraft.Open(f.Current.PostersById[b]);
        var bLamp = Custom with { Intensity = 3, Card = "#AABBCC" };
        Assert.True(f.Coordinator.Execute((other with { TemplateKey = "lamp", Parameters = other.Parameters with { Lamp = bLamp } }).ToCommand("b-lamp")).Succeeded);
        long revision = f.Current.Revision;
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(stale).Status);
        Assert.Equal(revision, f.Current.Revision);
        Assert.Equal(Custom, f.Current.PostersById["A"].Presentation.Parameters.Lamp);
        f.Store.Dispose(); SqliteConnection.ClearAllPools();
        using var reopened = new SqliteStateStore(new UserDataPaths(f.DirectoryPath));
        Assert.True(reopened.Initialize().Succeeded);
        var refreshed = new StateProjectionCoordinator(new SqliteStateCommandRepository(reopened)).Refresh();
        Assert.Equal(revision, refreshed.Revision);
        Assert.Equal(Custom, refreshed.PostersById["A"].Presentation.Parameters.Lamp);
        Assert.Equal(bLamp, refreshed.PostersById[b].Presentation.Parameters.Lamp);
        Assert.Equal("lamp", refreshed.PostersById["A"].Poster.TemplateKey);
    }

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp.Lamp.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public DesktopUpProjection Current { get { Assert.True(Coordinator.TryGetCurrent(out var p)); return p!; } }
        public Fixture()
        {
            Store = new(new UserDataPaths(DirectoryPath)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store)); Coordinator.EnsureInitialized(Seed());
        }
        public string Add(string title)
        {
            var result = Coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), Seed() with { Title = title }));
            Assert.True(result.Succeeded, result.Error); return result.ResultPosterId!;
        }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(DirectoryPath, true);
        }
    }
}
