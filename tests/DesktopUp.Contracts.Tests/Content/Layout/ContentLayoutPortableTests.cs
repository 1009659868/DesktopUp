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
        Assert.Equal(PortableProfileCodec.Version, parsed.Version);
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
        tree.AsObject().Remove("templateCatalog"); tree["settings"]!.AsObject().Remove("centerTheme");
        var v1 = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var plan = PortableProfilePlanner.Plan(v1, State(), Topology(), PortableImportMode.Append, false, () => "v1");
        Assert.Null(Assert.Single(plan.Creates).ContentLayoutJson);
        tree["posters"]![0]!["contentLayout"] = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("equal", "B")))))!["posters"]![0]!["contentLayout"]!.DeepClone();
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Fact]
    public void V3_carries_flow_axis_and_optional_heat_size_switch()
    {
        var d = Sample("equal", "A") with { FlowAxis = ContentFlowAxis.HorizontalLoop, AllowHeatDrivenSize = true };
        byte[] bytes = PortableProfileCodec.Export(State(Poster(d)));
        var content = PortableProfileCodec.Parse(bytes).Posters[0].ContentLayout!;
        Assert.Equal(ContentFlowAxis.HorizontalLoop, content.FlowAxis);
        Assert.True(content.AllowHeatDrivenSize);

        var plan = PortableProfilePlanner.Plan(PortableProfileCodec.Parse(bytes), State(), Topology(), PortableImportMode.Append, false, () => "fresh");
        var created = Assert.Single(plan.Creates);
        Assert.True(ContentLayoutPolicy.TryRead(created.ContentLayoutJson, created.PosterId, out var imported, out var error), error);
        Assert.Equal(ContentFlowAxis.HorizontalLoop, imported!.FlowAxis);
        Assert.True(imported.AllowHeatDrivenSize);
    }

    [Fact]
    public void V3_requires_an_explicit_flow_axis()
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("equal", "A")))))!;
        tree["posters"]![0]!["contentLayout"]!.AsObject().Remove("flowAxis");
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Fact]
    public void V2_archive_without_axis_imports_as_vertical_with_heat_sizing_off()
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(Sample("equal", "A") with { FlowAxis = ContentFlowAxis.HorizontalLoop }))))!;
        tree["version"] = 2; tree["posters"]![0]!["contentLayout"]!.AsObject().Remove("flowAxis");
        tree.AsObject().Remove("templateCatalog"); tree["settings"]!.AsObject().Remove("centerTheme");
        tree["posters"]![0]!["contentLayout"]!["schemaVersion"] = 2;
        var v2 = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var plan = PortableProfilePlanner.Plan(v2, State(), Topology(), PortableImportMode.Append, false, () => "v2");
        var created = Assert.Single(plan.Creates);
        Assert.True(ContentLayoutPolicy.TryRead(created.ContentLayoutJson, created.PosterId, out var imported, out var error), error);
        Assert.Equal(ContentFlowAxis.Vertical, imported!.FlowAxis);
        Assert.False(imported.AllowHeatDrivenSize);
    }

    [Fact]
    public void Portable_snapshots_retain_reserved_intent_when_the_callers_buffer_changes()
    {
        var expected = AuthoredIntent("A");
        var exportBuffer = expected.ReservedSpaces.ToArray();
        var portable = PortableContentLayoutCodec.Export(expected with { ReservedSpaces = exportBuffer });
        exportBuffer[0] = exportBuffer[0] with { Name = "later edit" };
        Assert.Equal(expected.ReservedSpaces, portable.ReservedSpaces);

        var importBuffer = expected.ReservedSpaces.ToArray();
        var imported = PortableContentLayoutCodec.Import(portable with { ReservedSpaces = importBuffer }, "fresh");
        importBuffer[0] = importBuffer[0] with { Name = "later edit" };
        AssertAuthoredIntent(expected, imported);
    }

    [Fact]
    public void Supported_authored_intent_round_trips_with_explicit_and_decorative_spaces_and_fresh_identity()
    {
        var original = AuthoredIntent("private-source");
        string originalJson = ContentLayoutPolicy.Serialize(original);
        byte[] bytes = PortableProfileCodec.Export(State(Poster(original)));
        var portable = PortableProfileCodec.Parse(bytes);
        Assert.Equal(original.ReservedSpaces, portable.Posters[0].ContentLayout!.ReservedSpaces);

        var plan = PortableProfilePlanner.Plan(portable, State(), Topology(), PortableImportMode.Append, false, () => "fresh");
        var created = Assert.Single(plan.Creates);
        Assert.True(ContentLayoutPolicy.TryRead(created.ContentLayoutJson, created.PosterId, out var restored, out var error), error);
        AssertAuthoredIntent(original, restored!);
        Assert.Equal(originalJson, ContentLayoutPolicy.Serialize(original));
        Assert.DoesNotContain("private-source", Encoding.UTF8.GetString(bytes));

        // Re-export exercises the persisted, remapped document rather than the first in-memory DTO.
        var again = PortableProfileCodec.Parse(PortableProfileCodec.Export(State(created)));
        Assert.Equal(original.ReservedSpaces, again.Posters[0].ContentLayout!.ReservedSpaces);
        Assert.Equal(bytes, PortableProfileCodec.Export(State(created)));
    }

    [Theory] [InlineData(2)] [InlineData(3)]
    public void Existing_archive_with_nonempty_reserved_spaces_is_imported_without_dropping_intent(int version)
    {
        var expected = AuthoredIntent("archive-source") with { SchemaVersion = 2 };
        // Inject the original archive payload independently of Export, so fixing only Export cannot pass.
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(expected))))!;
        tree["version"] = version;
        tree.AsObject().Remove("templateCatalog"); tree["settings"]!.AsObject().Remove("centerTheme");
        var content = tree["posters"]![0]!["contentLayout"]!;
        content["schemaVersion"] = 2;
        content["reservedSpaces"] = ReservedSpacePayload();
        if (version == 2) content.AsObject().Remove("flowAxis");
        var parsed = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var plan = PortableProfilePlanner.Plan(parsed, State(), Topology(), PortableImportMode.Append, false, () => "archive");
        var created = Assert.Single(plan.Creates);
        Assert.True(ContentLayoutPolicy.TryRead(created.ContentLayoutJson, created.PosterId, out var restored, out var error), error);
        AssertAuthoredIntent(expected, restored!);
    }

    [Theory]
    [InlineData("negative-column")] [InlineData("unknown-kind")] [InlineData("long-name")]
    [InlineData("null-entry")] [InlineData("null-name")] [InlineData("too-many")] [InlineData("fixed-collision")]
    public void Invalid_reserved_space_payload_is_rejected_instead_of_being_cleared(string invalid)
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(AuthoredIntent("A")))))!;
        var spaces = ReservedSpacePayload();
        switch (invalid)
        {
            case "negative-column": spaces[0]!["column"] = -1; break;
            case "unknown-kind": spaces[0]!["kind"] = 99; break;
            case "long-name": spaces[0]!["name"] = new string('x', ContentLayoutPolicy.MaximumReservedNameLength + 1); break;
            case "null-entry": spaces[0] = null; break;
            case "null-name": spaces[0]!["name"] = null; break;
            case "too-many":
                while (spaces.Count <= ContentLayoutPolicy.MaximumReservedSpaces) spaces.Add(spaces[0]!.DeepClone());
                break;
            case "fixed-collision": spaces[0]!["column"] = 0; spaces[0]!["row"] = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalid));
        }
        tree["posters"]![0]!["contentLayout"]!["reservedSpaces"] = spaces;
        byte[] bytes = Encoding.UTF8.GetBytes(tree.ToJsonString());
        byte[] originalBytes = bytes.ToArray();
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(bytes));
        Assert.Equal(originalBytes, bytes);
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
    public void Append_replace_replay_and_store_reopen_preserve_authored_intent_with_fresh_membership_identity()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.ContentPortable.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new UserDataPaths(root);
            var expected = AuthoredIntent("A");
            long savedRevision;
            PosterDefinition savedPoster;
            using (var store = new SqliteStateStore(paths))
            {
                Assert.True(store.Initialize().Succeeded);
                var repository = new SqliteStateCommandRepository(store); var coordinator = new StateProjectionCoordinator(repository);
                coordinator.EnsureInitialized(Poster(expected)); coordinator.BindPortableRuntime(_ => true);
                var original = repository.Read(); var portable = PortableProfileCodec.Parse(PortableProfileCodec.Export(original));
                var first = PortableProfilePlanner.Plan(portable, original, Topology(), PortableImportMode.Append, false, () => "first");
                var c = new PortableImportCommand("layout-append", first); var result = coordinator.ExecutePortable(c);
                Assert.True(result.Succeeded, result.Error); Assert.Equal(original.Revision + 1, result.CommittedRevision);
                Assert.Equal(StateCommandStatus.Replayed, coordinator.ExecutePortable(c).Status);
                Assert.Equal(original.Revision + 1, repository.Read().Revision);
                Assert.Equal(original.Catalog.Posters[0].Poster, repository.Read().Catalog.Posters[0].Poster);
                var second = PortableProfilePlanner.Plan(portable, repository.Read(), Topology(), PortableImportMode.Append, false, () => "second");
                Assert.True(coordinator.ExecutePortable(new("layout-append-again", second)).Succeeded);
                var all = repository.Read(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in all.Catalog.Posters)
                {
                    Assert.True(ContentLayoutPolicy.TryRead(row.Poster.ContentLayoutJson, row.Poster.PosterId, out var doc, out _));
                    var document = doc!;
                    Assert.All(document.Placements, p => Assert.True(ids.Add(p.ItemId)));
                    if (document.PosterId != expected.PosterId) AssertAuthoredIntent(expected, document);
                }
                var replace = PortableProfilePlanner.Plan(portable, all, Topology(), PortableImportMode.Replace, false, () => "replacement");
                var replaceCommand = new PortableImportCommand("layout-replace", replace);
                var replaced = coordinator.ExecutePortable(replaceCommand);
                Assert.True(replaced.Succeeded, replaced.Error); Assert.Equal(all.Revision + 1, replaced.CommittedRevision);
                Assert.Equal(StateCommandStatus.Replayed, coordinator.ExecutePortable(replaceCommand).Status);
                var saved = repository.Read(); savedRevision = saved.Revision;
                Assert.Equal(all.Revision + 1, savedRevision);
                savedPoster = Assert.Single(saved.Catalog.Posters).Poster;
            }

            using var reopened = new SqliteStateStore(paths);
            Assert.True(reopened.Initialize().Succeeded);
            var restoredState = new SqliteStateCommandRepository(reopened).Read();
            Assert.Equal(savedRevision, restoredState.Revision);
            var restoredPoster = Assert.Single(restoredState.Catalog.Posters).Poster;
            Assert.Equal(savedPoster, restoredPoster);
            Assert.True(ContentLayoutPolicy.TryRead(restoredPoster.ContentLayoutJson, restoredPoster.PosterId, out var restored, out var error), error);
            AssertAuthoredIntent(expected, restored!);
            Assert.Equal(expected.ReservedSpaces, PortableProfileCodec.Parse(PortableProfileCodec.Export(restoredState)).Posters[0].ContentLayout!.ReservedSpaces);
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

    private static ContentLayoutDocument AuthoredIntent(string id) => new(id, Array.AsReadOnly(new[]
    {
        new ContentPlacement(id + ":fixed", 2, 3, 2)
        { Column = 0, Row = 0, FixedPosition = true, ManualSize = true, PinnedIndex = 2, ImageFit = ContentImageFit.Cover, Version = 17 },
        new ContentPlacement(id + ":manual", 0, 2, 3)
        { Column = 3, Row = 0, ManualSize = true, ImageFit = ContentImageFit.Contain, Version = 11 },
        new ContentPlacement(id + ":side", 1, 2, 2)
        { RegionId = "side", Column = 0, Row = 0, PinnedIndex = 1, Version = 5 }
    }))
    {
        Mode = ContentLayoutMode.Manual, DocumentVersion = 47, ItemSetVersion = 23,
        MaximumColumns = 16, BaseCellDip = 28, BaseGapDip = 4,
        ShowBaselineGrid = false, ShowSpanLabels = true, AllowHeatDrivenSize = true, LoopScroll = true,
        Regions = Array.AsReadOnly(new[] { new ContentLayoutRegion("main", 1, 0, 8), new ContentLayoutRegion("side", 0, 8, 8, 8) }),
        ReservedSpaces = Array.AsReadOnly(new[]
        {
            new ContentReservedSpace(8, 0, 2, 3) { Kind = ContentReservedKind.Explicit, Name = "keep-out" },
            new ContentReservedSpace(0, 0, 3, 2) { Kind = ContentReservedKind.Decorative, Name = "decoration behind card" }
        })
    };

    private static JsonArray ReservedSpacePayload() => new(
        new JsonObject { ["column"] = 8, ["row"] = 0, ["columnSpan"] = 2, ["rowSpan"] = 3,
            ["kind"] = (int)ContentReservedKind.Explicit, ["name"] = "keep-out" },
        new JsonObject { ["column"] = 0, ["row"] = 0, ["columnSpan"] = 3, ["rowSpan"] = 2,
            ["kind"] = (int)ContentReservedKind.Decorative, ["name"] = "decoration behind card" });

    private static void AssertAuthoredIntent(ContentLayoutDocument expected, ContentLayoutDocument actual)
    {
        Assert.NotEqual(expected.PosterId, actual.PosterId);
        Assert.Equal(1, actual.DocumentVersion); Assert.Equal(1, actual.ItemSetVersion);
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion); Assert.Equal(expected.EngineId, actual.EngineId);
        Assert.Equal(expected.EngineVersion, actual.EngineVersion); Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.BaseCellDip, actual.BaseCellDip); Assert.Equal(expected.BaseGapDip, actual.BaseGapDip);
        Assert.Equal(expected.MaximumColumns, actual.MaximumColumns); Assert.Equal(expected.ShowBaselineGrid, actual.ShowBaselineGrid);
        Assert.Equal(expected.ShowSpanLabels, actual.ShowSpanLabels); Assert.Equal(expected.FlowAxis, actual.FlowAxis);
        Assert.Equal(expected.AllowHeatDrivenSize, actual.AllowHeatDrivenSize); Assert.Equal(expected.LoopScroll, actual.LoopScroll);
        Assert.Equal(expected.Regions, actual.Regions); Assert.Equal(expected.ReservedSpaces, actual.ReservedSpaces);
        Assert.Equal(expected.Placements.Count, actual.Placements.Count);
        for (int i = 0; i < expected.Placements.Count; i++)
        {
            var remapped = expected.Placements[i] with
            { ItemId = actual.PosterId + ":item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Version = 1 };
            Assert.Equal(remapped, actual.Placements[i]);
        }
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
