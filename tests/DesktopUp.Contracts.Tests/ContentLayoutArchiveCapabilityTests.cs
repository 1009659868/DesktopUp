using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutArchiveCapabilityTests
{
    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Known_read_only_layout_can_be_archived_without_rewriting_its_sqlite_source(ContentFlowAxis axis)
    {
        var source = LegacyLayout("private-source", axis);
        using var fixture = new Fixture(source);
        var before = fixture.Repository.Read();
        string raw = before.Catalog.Posters[0].Poster.ContentLayoutJson!;
        byte[] bytes = PortableProfileCodec.Export(before);
        var archive = PortableProfileCodec.Parse(bytes);
        var archived = Assert.Single(archive.Posters).ContentLayout!;
        var decoded = PortableContentLayoutCodec.Import(archived, "decoded");

        Assert.Equal(ContentLayoutPolicy.Serialize(ContentLayoutPolicy.Remap(source, "decoded")), ContentLayoutPolicy.Serialize(decoded));
        Assert.Equal(source.ReservedSpaces, decoded.ReservedSpaces);
        Assert.Equal(source.Regions, decoded.Regions);
        Assert.Equal(bytes, PortableProfileCodec.Encode(archive));
        Assert.DoesNotContain("private-source", Encoding.UTF8.GetString(bytes));
        Assert.Equal(before.Revision, fixture.Repository.Read().Revision);
        Assert.Equal(raw, fixture.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM state_command_receipts;"));

        var backup = fixture.Store.CreateBackup("archive-capability");
        Assert.True(backup.Succeeded, backup.Error);
        using var reopened = new SqliteStateStore(fixture.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(raw, new SqlitePosterRepository(reopened).Read(source.PosterId)!.Poster.ContentLayoutJson);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical, PortableImportMode.Append)]
    [InlineData(ContentFlowAxis.Vertical, PortableImportMode.Replace)]
    [InlineData(ContentFlowAxis.HorizontalLoop, PortableImportMode.Append)]
    [InlineData(ContentFlowAxis.HorizontalLoop, PortableImportMode.Replace)]
    public void Read_only_archive_is_parsed_but_cannot_be_planned_as_an_applicable_configuration(ContentFlowAxis axis, PortableImportMode mode)
    {
        using var fixture = new Fixture();
        var archive = PortableProfileCodec.Parse(ReadOnlyArchiveBytes(axis));
        var before = fixture.Repository.Read();
        int allocated = 0;
        var error = Assert.ThrowsAny<ArgumentException>(() => PortableProfilePlanner.Plan(archive, before, Topology(), mode, false,
            () => "should-not-allocate-" + ++allocated));
        Assert.Contains("archiv", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, allocated);
        Assert.Equal(before.Revision, fixture.Repository.Read().Revision);
        Assert.Equal(before.Catalog.Posters, fixture.Repository.Read().Catalog.Posters);
    }

    [Theory] [InlineData(PortableImportMode.Append)] [InlineData(PortableImportMode.Replace)]
    public void Transaction_replanning_rejects_a_forged_read_only_plan_without_rows_or_receipts(PortableImportMode mode)
    {
        using var fixture = new Fixture();
        var before = fixture.Repository.Read();
        var supported = PortableProfileCodec.Parse(PortableProfileCodec.Export(before));
        var plan = PortableProfilePlanner.Plan(supported, before, Topology(), mode, false, () => "imported");
        var archive = PortableProfileCodec.Parse(ReadOnlyArchiveBytes(ContentFlowAxis.Vertical));
        var forged = plan with
        {
            Document = archive,
            Creates = plan.Creates.Select(p => p with
            {
                ContentLayoutJson = ContentLayoutPolicy.Serialize(PortableContentLayoutCodec.Import(archive.Posters[0].ContentLayout!, p.PosterId))
            }).ToArray()
        };
        var result = fixture.Coordinator.ExecutePortable(new("readonly-plan", forged));
        Assert.Equal(StateCommandStatus.ValidationFailed, result.Status);
        Assert.Contains("archiv", result.Error!, StringComparison.OrdinalIgnoreCase);
        var after = fixture.Repository.Read();
        Assert.Equal(before.Revision, after.Revision); Assert.Equal(before.Catalog.CatalogVersion, after.Catalog.CatalogVersion);
        Assert.Equal(before.Settings, after.Settings); Assert.Equal(before.Catalog.Posters, after.Catalog.Posters);
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM state_command_receipts WHERE request_id='readonly-plan';"));
        Assert.Equal(0L, fixture.Scalar("SELECT COUNT(*) FROM state_operations WHERE operation_id='readonly-plan';"));
    }

    [Theory]
    [InlineData("unknown-schema")] [InlineData("unknown-member")] [InlineData("null-name")]
    [InlineData("fixed-collision")] [InlineData("invalid-region")]
    public void Archive_capability_does_not_accept_unknown_or_invalid_intent(string invalid)
    {
        var tree = JsonNode.Parse(ReadOnlyArchiveBytes(ContentFlowAxis.Vertical))!;
        var content = tree["posters"]![0]!["contentLayout"]!;
        switch (invalid)
        {
            case "unknown-schema": content["schemaVersion"] = 99; break;
            case "unknown-member": content["futureGeometry"] = new JsonObject(); break;
            case "null-name": content["reservedSpaces"]![0]!["name"] = null; break;
            case "fixed-collision":
                content["placements"]![1]!["regionId"] = "main";
                content["placements"]![1]!["column"] = 7;
                content["placements"]![1]!["row"] = 0;
                content["placements"]![1]!["fixedPosition"] = true;
                break;
            case "invalid-region": content["placements"]![0]!["regionId"] = "missing"; break;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(tree.ToJsonString());
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(bytes));
    }

    [Fact]
    public void Capability_report_separates_lossless_archive_from_canonical_authoring_permission()
    {
        var legacy = ContentLayoutCapabilities.Inspect(LegacyLayout("legacy", ContentFlowAxis.Vertical));
        Assert.True(legacy.CanArchive); Assert.False(legacy.CanInstantiate); Assert.False(legacy.CanAuthorOnCanonicalBoard);
        Assert.Equal(ContentLayoutSpaceStatus.LegacyConstraintConflict, legacy.SpaceStatus);
        Assert.NotNull(legacy.Restriction);
        var ordinary = ContentLayoutCapabilities.Inspect(ContentLayoutSamples.Equal("ordinary"));
        Assert.True(ordinary.CanArchive); Assert.True(ordinary.CanInstantiate); Assert.True(ordinary.CanAuthorOnCanonicalBoard);
        Assert.Null(ordinary.Restriction);
    }

    [Fact]
    public void Version_four_preserves_manual_return_state_through_identity_remap_sqlite_and_reopen()
    {
        using var fixture = new Fixture();
        var source = ManualWithReturnState("private-manual");
        byte[] bytes = PortableProfileCodec.Export(State(Poster(source)));
        var archive = PortableProfileCodec.Parse(bytes);
        Assert.Equal(4, archive.Version);
        Assert.Equal(source.SavedAutomaticMode, archive.Posters[0].ContentLayout!.SavedAutomaticMode);
        Assert.Equal(source.Placements.Select(p => p.SavedAutomaticState),
            archive.Posters[0].ContentLayout!.Placements.Select(p => p.SavedAutomaticState));
        Assert.Equal(source.Placements.Select(p => p.AutomaticSizeBasis),
            archive.Posters[0].ContentLayout!.Placements.Select(p => p.AutomaticSizeBasis));
        Assert.DoesNotContain("private-manual", Encoding.UTF8.GetString(bytes));

        var before = fixture.Repository.Read();
        var plan = PortableProfilePlanner.Plan(archive, before, Topology(), PortableImportMode.Append, false, () => "manual-copy");
        var command = new PortableImportCommand("manual-return-state", plan);
        var result = fixture.Coordinator.ExecutePortable(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CommittedRevision);
        Assert.Equal(StateCommandStatus.Replayed, fixture.Coordinator.ExecutePortable(command).Status);
        using var reopened = new SqliteStateStore(fixture.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var copied = new SqlitePosterRepository(reopened).Read("poster-manual-copy")!.Poster;
        Assert.True(ContentLayoutPolicy.TryRead(copied.ContentLayoutJson, copied.PosterId, out var restored, out var error), error);
        Assert.Equal(ContentLayoutPolicy.Serialize(ContentLayoutPolicy.Remap(source, copied.PosterId)), ContentLayoutPolicy.Serialize(restored!));
        // Append may relocate the poster and assign stack order; its portable content must remain identical.
        Assert.Equal(JsonSerializer.Serialize(archive.Posters[0].ContentLayout),
            JsonSerializer.Serialize(PortableContentLayoutCodec.Export(restored!)));
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Schema_three_return_state_cannot_be_disguised_as_an_older_portable_version(int version)
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(ManualWithReturnState("manual")))))!;
        tree["version"] = version;
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Theory] [InlineData(2)] [InlineData(3)]
    public void Legacy_portable_versions_without_saved_return_state_remain_readable(int version)
    {
        var source = ContentLayoutSamples.Equal("legacy") with { SchemaVersion = 2 };
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(source))))!;
        tree["version"] = version;
        if (version == 2) tree["posters"]![0]!["contentLayout"]!.AsObject().Remove("flowAxis");
        var archive = PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        var restored = PortableContentLayoutCodec.Import(archive.Posters[0].ContentLayout!, "restored");
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Null(restored.SavedAutomaticMode);
        Assert.All(restored.Placements, p => Assert.Null(p.SavedAutomaticState));
        Assert.All(restored.Placements, p => Assert.Null(p.AutomaticSizeBasis));
    }

    [Fact]
    public void Unknown_members_inside_saved_return_state_are_rejected()
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(ManualWithReturnState("manual")))))!;
        tree["posters"]![0]!["contentLayout"]!["placements"]![0]!["savedAutomaticState"]!["futureRank"] = 77;
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Theory] [InlineData("placement")] [InlineData("saved-state")]
    public void Invalid_or_unknown_automatic_size_basis_is_rejected(string target)
    {
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(ManualWithReturnState("manual")))))!;
        var placement = tree["posters"]![0]!["contentLayout"]!["placements"]![0]!;
        var basis = target == "placement" ? placement["automaticSizeBasis"]! : placement["savedAutomaticState"]!["automaticSizeBasis"]!;
        basis["columnSpan"] = 0;
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
        basis["columnSpan"] = 3;
        basis["futureMetric"] = 7;
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Theory] [InlineData(2)] [InlineData(3)]
    public void Automatic_size_basis_cannot_be_smuggled_under_an_old_content_schema(int version)
    {
        var source = ContentLayoutSamples.Equal("ordinary") with { SchemaVersion = 2 };
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(source))))!;
        tree["version"] = version;
        tree["posters"]![0]!["contentLayout"]!["placements"]![0]!["automaticSizeBasis"] =
            new JsonObject { ["columnSpan"] = 4, ["rowSpan"] = 4 };
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    private static ContentLayoutDocument ManualWithReturnState(string id) => new(id,
    [
        new(id + ":first", 0, 4, 3) { Column = 0, Row = 0, ManualSize = true, Version = 11,
            AutomaticSizeBasis = new(2, 3),
            SavedAutomaticState = new(1, 3, 2, true, 2, 7, 0, true) { AutomaticSizeBasis = new(2, 2) } },
        new(id + ":second", 1, 2, 2) { Column = 4, Row = 0, ManualSize = true, Version = 7,
            SavedAutomaticState = new(0, 2, 2, false, 1) }
    ])
    {
        SchemaVersion = 3, Mode = ContentLayoutMode.Manual, SavedAutomaticMode = ContentLayoutMode.Anchored,
        MaximumColumns = 16, DocumentVersion = 17, ItemSetVersion = 9, LoopScroll = true
    };

    private static byte[] ReadOnlyArchiveBytes(ContentFlowAxis axis)
    {
        // Construct an old archive independently of the read-only Export path.
        var source = LegacyLayout("archive-source", axis) with { MaximumColumns = 16 };
        source = source with { Placements = source.Placements.Select(p => p with { PinnedIndex = null }).ToArray() };
        var tree = JsonNode.Parse(PortableProfileCodec.Export(State(Poster(source))))!;
        var content = tree["posters"]![0]!["contentLayout"]!;
        content["maximumColumns"] = 8;
        content["placements"]![0]!["pinnedIndex"] = 1;
        content["placements"]![1]!["pinnedIndex"] = 2;
        return Encoding.UTF8.GetBytes(tree.ToJsonString());
    }

    private static ContentLayoutDocument LegacyLayout(string id, ContentFlowAxis axis)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        return new(id,
        [
            new(id + ":fixed", 3, 4, 4) { Column = vertical ? 7 : 0, Row = vertical ? 0 : 7,
                FixedPosition = true, ManualSize = true, PinnedIndex = 1, ImageFit = ContentImageFit.Cover, Version = 17 },
            new(id + ":sized", 1, vertical ? 3 : 2, vertical ? 2 : 3)
            { RegionId = "side", ManualSize = true, PinnedIndex = 2, Version = 11 },
            new(id + ":flow", 5, 3, 3) { Version = 9 }
        ])
        {
            Mode = ContentLayoutMode.Anchored, MaximumColumns = 8, FlowAxis = axis, LoopScroll = true,
            DocumentVersion = 47, ItemSetVersion = 23, ShowBaselineGrid = false, ShowSpanLabels = true,
            Regions = [new("main", 0), new("side", 1, vertical ? 0 : 10, 4, vertical ? 10 : 0)],
            ReservedSpaces =
            [
                new(vertical ? 0 : 6, vertical ? 6 : 0, 2, 2) { Name = "explicit gap" },
                new(vertical ? 7 : 0, vertical ? 0 : 7, 4, 4) { Kind = ContentReservedKind.Decorative, Name = "card backdrop" }
            ]
        };
    }

    private static PosterDefinition Poster(ContentLayoutDocument document) => PosterConfigurationFactory.NewBasicBox("Archive", "DISPLAY1") with
    { PosterId = document.PosterId, ThemeInstanceId = "theme-source", ContentLayoutJson = ContentLayoutPolicy.Serialize(document) };
    private static DesktopStateSnapshot State(PosterDefinition poster) => new(new(1, 1, true,
        [new PosterStateSnapshot(1, 1, poster, DateTimeOffset.UnixEpoch, 1)]),
        new(1, 1, new(false, DesktopUpPerformanceMode.Automatic, false), DateTimeOffset.UnixEpoch, 1));
    private static DisplayTopologySnapshot Topology() => new("archive-topology",
        [new DisplayMonitorSnapshot("DISPLAY1", true, new(0, 0, 1920, 1080), new(0, 0, 1920, 1080), 96, 96)],
        1, DateTimeOffset.UnixEpoch);

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.ArchiveCapability.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        public SqliteStateStore Store { get; }
        public SqliteStateCommandRepository Repository { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture(ContentLayoutDocument? document = null)
        {
            Paths = new(_root); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Repository = new(Store); Coordinator = new(Repository);
            Coordinator.EnsureInitialized(Poster(document ?? ContentLayoutSamples.Equal("source")));
            Coordinator.BindPortableRuntime(_ => true);
        }
        public object? Scalar(string sql)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(Paths.State, "desktopup.sqlite3"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
            return command.ExecuteScalar();
        }
        public void Dispose()
        {
            Store.Dispose(); SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
