using System.Text;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutPortableTests
{
    [Theory] [InlineData("equal")] [InlineData("illustration")] [InlineData("covers")]
    public void Independent_layout_and_preferences_round_trip_without_persistent_ids_or_versions(string kind)
    {
        var d = Sample(kind, "private-poster") with { DocumentVersion = 47, ItemSetVersion = 23,
            ShowBaselineGrid = false, ShowSpanLabels = true };
        d = d with { Placements = d.Placements.Select(p => p with { Version = 17 }).ToArray() };
        var original = State(Poster(d));
        byte[] bytes = PortableProfileCodec.Export(original);
        string json = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("private-poster", json); Assert.DoesNotContain("documentVersion", json);
        Assert.DoesNotContain("itemSetVersion", json); Assert.DoesNotContain("entityVersion", json);
        var parsed = PortableProfileCodec.Parse(bytes);
        Assert.Equal(2, parsed.Version);
        var plan = PortableProfilePlanner.Plan(parsed, State(), Topology(), PortableImportMode.Append, false, () => "fresh");
        var created = Assert.Single(plan.Creates);
        Assert.True(ContentLayoutPolicy.TryRead(created.ContentLayoutJson, created.PosterId, out var imported, out var error), error);
        Assert.Equal(1, imported!.DocumentVersion); Assert.Equal(1, imported.ItemSetVersion);
        Assert.All(imported.Placements, p => { Assert.StartsWith("poster-fresh:", p.ItemId); Assert.Equal(1, p.Version); });
        Assert.Equal(d.ReservedSpaces, imported.ReservedSpaces);
        Assert.Equal(d.Mode, imported.Mode); Assert.False(imported.ShowBaselineGrid); Assert.True(imported.ShowSpanLabels);
        Assert.Equal(bytes, PortableProfileCodec.Export(State(created)));
    }

    [Fact]
    public void V1_still_imports_virtual_default_but_cannot_claim_new_required_content_payload()
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("equal", "A")))))!;
        tree["version"] = 1; tree["posters"]![0]!.AsObject().Remove("contentLayout");
        var v1 = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var plan = PortableProfilePlanner.Plan(v1, State(), Topology(), PortableImportMode.Append, false, () => "v1");
        Assert.Null(Assert.Single(plan.Creates).ContentLayoutJson);
        tree["posters"]![0]!["contentLayout"] = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("equal", "B")))))!["posters"]![0]!["contentLayout"]!.DeepClone();
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Theory]
    [InlineData("engineId", "\"future\"")] [InlineData("schemaVersion", "99")]
    [InlineData("engineVersion", "99")] [InlineData("nativePath", "\"C:/private/file\"")]
    public void Unknown_independent_payload_rejects_before_planning_without_silent_equal_fallback(string field, string value)
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("covers", "A")))))!;
        tree["posters"]![0]!["contentLayout"]![field] = JsonNode.Parse(value);
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Fact]
    public void Local_unknown_content_refuses_export_and_does_not_mutate_original()
    {
        const string raw = "{\"engineId\":\"future\"}";
        var state = State(Poster(Sample("equal", "A")) with { ContentLayoutJson = raw });
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(state));
        Assert.Equal(raw, state.Catalog.Posters[0].Poster.ContentLayoutJson);
    }

    [Fact]
    public void Append_and_replace_with_new_membership_identity_are_single_revision_and_replay_safe()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.ContentPortable.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new UserDataPaths(root); using var store = new SqliteStateStore(paths); Assert.True(store.Initialize().Succeeded);
            var repository = new SqliteStateCommandRepository(store); var coordinator = new StateProjectionCoordinator(repository);
            coordinator.EnsureInitialized(Poster(Sample("illustration", "A"))); coordinator.BindPortableRuntime(_ => true);
            var original = repository.Read(); var portable = PortableProfileCodec.Parse(PortableProfileCodec.Export(original));
            var first = PortableProfilePlanner.Plan(portable, original, Topology(), PortableImportMode.Append, false, () => "first");
            var c = new PortableImportCommand("layout-append", first); var result = coordinator.ExecutePortable(c);
            Assert.True(result.Succeeded, result.Error); Assert.Equal(original.Revision + 1, result.CommittedRevision);
            Assert.Equal(StateCommandStatus.Replayed, coordinator.ExecutePortable(c).Status);
            Assert.Equal(original.Catalog.Posters[0].Poster, repository.Read().Catalog.Posters[0].Poster);
            var second = PortableProfilePlanner.Plan(portable, repository.Read(), Topology(), PortableImportMode.Append, false, () => "second");
            Assert.True(coordinator.ExecutePortable(new("layout-append-again", second)).Succeeded);
            var all = repository.Read(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in all.Catalog.Posters)
            {
                Assert.True(ContentLayoutPolicy.TryRead(row.Poster.ContentLayoutJson, row.Poster.PosterId, out var doc, out _));
                Assert.All(doc!.Placements, p => Assert.True(ids.Add(p.ItemId)));
            }
            var replace = PortableProfilePlanner.Plan(portable, all, Topology(), PortableImportMode.Replace, false, () => "replacement");
            var replaced = coordinator.ExecutePortable(new("layout-replace", replace));
            Assert.True(replaced.Succeeded, replaced.Error); Assert.Equal(all.Revision + 1, replaced.CommittedRevision);
            Assert.Single(repository.Read().Catalog.Posters);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Invalid_fixed_collision_and_overcapacity_cannot_enter_v2_profile()
    {
        var d = ContentLayoutSamples.Covers("A");
        var collision = d with { Placements = d.Placements.Select((p, i) => i == 1 ? p with { Column = 0, Row = 0 } : p).ToArray() };
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Export(State(Poster(collision))));
        var count = ContentLayoutSamples.Equal("A", 1001);
        Assert.Throws<ArgumentException>(() => ContentLayoutPolicy.Serialize(count));
    }

    private static ContentLayoutDocument Sample(string kind, string id) => kind switch
    { "illustration" => ContentLayoutSamples.Illustration(id), "covers" => ContentLayoutSamples.Covers(id), _ => ContentLayoutSamples.Equal(id) };
    private static PosterDefinition Poster(ContentLayoutDocument document) => PosterConfigurationFactory.NewBasicBox("Example", "DISPLAY1") with
    { PosterId = document.PosterId, ThemeInstanceId = "theme-private", ContentLayoutJson = ContentLayoutPolicy.Serialize(document) };
    private static DesktopStateSnapshot State(params PosterDefinition[] posters) => new(new(12, 4, true,
        Array.AsReadOnly(posters.Select(p => new PosterStateSnapshot(12, 11, p, DateTimeOffset.UnixEpoch, 7)).ToArray())),
        new(12, 10, new(false, DesktopUpPerformanceMode.Automatic, false), DateTimeOffset.UnixEpoch, 5));
    private static DisplayTopologySnapshot Topology() => new("layout-topology", Array.AsReadOnly(new[]
    { new DisplayMonitorSnapshot("DISPLAY1", true, new(0,0,1920,1080), new(0,0,1920,1080), 96,96) }), 1, DateTimeOffset.UnixEpoch);
}
