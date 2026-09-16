using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PortableProfileModelTests
{
    private static PosterDefinition Poster(string id = "a", double anchor = 0) =>
        PosterConfigurationFactory.NewBasicBox("Poster " + id, "DISPLAY1") with
        {
            PosterId = id, ThemeInstanceId = "private-theme-" + id, CollectionId = "private-collection",
            HorizontalAnchor = anchor, StackOrder = anchor == 0 ? 0 : 1,
            AutoCollapseDelaySeconds = 2, Purpose = "games"
        };
    private static DesktopStateSnapshot State(params PosterDefinition[] posters) => new(
        new(12, 4, true, Array.AsReadOnly(posters.Select(p => new PosterStateSnapshot(12, 11, p, DateTimeOffset.UnixEpoch, 7)).ToArray())),
        new(12, 10, new(true, DesktopUpPerformanceMode.FramesPerSecond30, true), DateTimeOffset.UnixEpoch, 5));
    private static DisplayTopologySnapshot Topology(int width = 1920, int height = 1080, uint dpi = 96, int x = 0, int y = 0) =>
        new("topology-1", Array.AsReadOnly(new[] { new DisplayMonitorSnapshot("DISPLAY1", true,
            new(x, y, width, height), new(x, y, width, height), dpi, dpi) }), 8, DateTimeOffset.UnixEpoch);
    private static PortableProfileDocument Document(params PosterDefinition[] posters) => PortableProfileCodec.Parse(PortableProfileCodec.Export(State(posters)));
    private static Func<string> Ids() { int number = 0; return () => $"import-{++number}"; }
    private static PortableImportPlan Plan(PortableProfileDocument document, DesktopStateSnapshot? current = null,
        DisplayTopologySnapshot? topology = null, PortableImportMode mode = PortableImportMode.Replace) =>
        PortableProfilePlanner.Plan(document, current ?? State(), topology ?? Topology(), mode, true, Ids());
    private static JsonObject Json(params PosterDefinition[] posters) => JsonNode.Parse(PortableProfileCodec.Export(State(posters)))!.AsObject();
    private static PortableProfileDocument Parse(JsonObject node) => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()));

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void Empty_single_three_profiles_round_trip_without_identity_or_revision(int count)
    {
        var original = State(Enumerable.Range(0, count).Select(i => Poster("s" + i, i / 3d) with { StackOrder = i }).ToArray());
        byte[] first = PortableProfileCodec.Export(original);
        var plan = Plan(PortableProfileCodec.Parse(first));
        Assert.Equal(count, plan.Creates.Count); Assert.Empty(plan.PendingPosterIds);
        Assert.All(plan.Creates, p => { Assert.Null(p.CollectionId); Assert.StartsWith("poster-import-", p.PosterId); Assert.StartsWith("theme-import-", p.ThemeInstanceId); });
        Assert.Equal(first, PortableProfileCodec.Export(State(plan.Creates.ToArray())));
        string json = Encoding.UTF8.GetString(first);
        Assert.DoesNotContain("private-", json); Assert.DoesNotContain("revision", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("launchAtStartup", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("placementPending", json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Parse_encode_is_deterministic_and_freezes_collections()
    {
        var doc = Document(Poster()); var frozen = PortableProfileCodec.Freeze(doc);
        Assert.Equal(PortableProfileCodec.Encode(doc), PortableProfileCodec.Encode(frozen));
        Assert.Throws<NotSupportedException>(() => ((IList<PortablePosterProfile>)frozen.Posters).Clear());
    }
    [Theory]
    [InlineData("version", "2")] [InlineData("format", "\"Wrong\"")]
    [InlineData("unknown", "true")] [InlineData("posters", "null")] [InlineData("settings", "null")]
    public void Unknown_outer_format_or_fields_are_rejected(string field, string value)
    {
        var json = Json(Poster()); json[field] = JsonNode.Parse(value);
        Assert.Throws<ArgumentException>(() => Parse(json));
    }
    [Theory]
    [InlineData("layoutKind", "\"future-canvas\"")]
    [InlineData("widthDip", "32769")] [InlineData("expandedHeightDip", "239")]
    [InlineData("horizontalAnchor", "1.1")] [InlineData("verticalAnchor", "-0.1")]
    [InlineData("autoCollapseDelaySeconds", "-1")] [InlineData("title", "null")]
    [InlineData("stackOrder", "-1")] [InlineData("nativeHandle", "123")]
    public void Invalid_or_unknown_poster_fields_are_rejected(string field, string value)
    {
        var json = Json(Poster()); json["posters"]![0]![field] = JsonNode.Parse(value);
        Assert.Throws<ArgumentException>(() => Parse(json));
    }
    [Theory] [InlineData("schemaVersion", "99")] [InlineData("script", "\"C:\\\\secret\\\\run.exe\"")]
    [InlineData("legacy", "{\"path\":\"C:\\\\Users\\\\secret\"}")]
    public void Unknown_parameter_version_or_private_payload_is_rejected(string field, string value)
    {
        var json = Json(Poster()); json["posters"]![0]!["layoutParameters"]![field] = JsonNode.Parse(value);
        Assert.Throws<ArgumentException>(() => Parse(json));
    }
    [Fact]
    public void Missing_required_field_duplicate_key_or_duplicate_member_are_rejected()
    {
        var missing = Json(Poster()); missing["posters"]![0]!.AsObject().Remove("isLocked");
        Assert.Throws<ArgumentException>(() => Parse(missing));
        var duplicate = Json(Poster()); duplicate["posters"]!.AsArray().Add(duplicate["posters"]![0]!.DeepClone());
        Assert.Throws<ArgumentException>(() => Parse(duplicate));
        byte[] text = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(PortableProfileCodec.Export(State())).Replace("\"version\": 1", "\"version\": 1, \"version\": 1"));
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(text));
    }
    [Fact]
    public void Utf8_depth_byte_and_count_limits_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(new byte[] { 0xC0, 0xAF }));
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(new byte[PortableProfileCodec.MaximumBytes + 1]));
        string deep = new('[', 33); deep += "0" + new string(']', 33);
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Parse(Encoding.UTF8.GetBytes(deep)));
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(State(Enumerable.Range(0, 129).Select(i => Poster(i.ToString())).ToArray())));
    }
    [Fact]
    public void Maximum_poster_count_and_fractional_macro_preferences_are_preserved()
    {
        var poster = Poster() with { LayoutParametersJson = PosterPresentationPolicy.Serialize(
            PosterPresentationPolicy.Defaults("basic-box") with { Preferences = new(1.13, .73) }) };
        var parsed = Document(Enumerable.Range(0, 128).Select(i => poster with { PosterId = i.ToString(), StackOrder = i }).ToArray());
        Assert.Equal(128, parsed.Posters.Count);
        Assert.Equal(poster.LayoutParametersJson, parsed.Posters[0].LayoutParametersJson.Replace("\r", "").Replace("\n", "").Replace(" ", ""));
    }
    [Fact]
    public void Unknown_theme_is_preserved_with_explicit_fallback_warning()
    {
        var doc = Document(Poster() with { TemplateKey = "custom-missing" }); var plan = Plan(doc);
        Assert.Equal("custom-missing", plan.Creates[0].TemplateKey);
        Assert.Equal(doc.Posters[0].LayoutParametersJson, plan.Creates[0].LayoutParametersJson);
        Assert.Contains(plan.Warnings, w => w.Contains("主题"));
        Assert.False(PosterPresentationPolicy.Resolve(plan.Creates[0]).CanEdit);
    }
    [Fact]
    public void Source_only_metadata_is_not_exported_and_unknown_legacy_is_not_silently_removed()
    {
        var poster = Poster() with { LayoutParametersJson = "{\"privatePath\":\"C:\\\\Users\\\\secret\"}" };
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(State(poster)));
        var known = Document(Poster() with { LayoutParametersJson = "{\"itemGapDip\":14}" });
        Assert.Equal(1, known.Posters[0].LayoutParameters.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(14, known.Posters[0].LayoutParameters.GetProperty("content").GetProperty("gapDip").GetInt32());
    }
    [Fact]
    public void Export_rejects_non_coherent_snapshot()
    {
        var state = State();
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(state with { Settings = state.Settings with { Revision = 13 } }));
    }

    [Fact]
    public void Append_keeps_existing_locked_obstacle_and_never_moves_it()
    {
        var blocker = Poster("locked") with { IsLocked = true };
        var current = State(blocker); var plan = Plan(Document(Poster()), current, mode: PortableImportMode.Append);
        Assert.Empty(plan.Removes); Assert.Empty(plan.Updates); Assert.False(plan.Creates[0].PlacementPending);
        Assert.True(plan.Creates[0].FallbackXDip >= blocker.WidthDip || plan.Creates[0].FallbackYDip >= blocker.ExpandedHeightDip);
        Assert.Equal(blocker, current.Catalog.Posters[0].Poster);
        Assert.True(plan.Creates[0].StackOrder > blocker.StackOrder);
    }
    [Fact]
    public void Replace_is_explicit_and_new_identities_do_not_alias_existing()
    {
        var plan = Plan(Document(Poster()), State(Poster("old")));
        Assert.Equal(new[] { "old" }, plan.Removes); Assert.NotEqual("old", plan.Creates[0].PosterId);
        Assert.Equal(0, plan.Creates[0].FallbackXDip);
    }
    [Fact]
    public void Empty_replace_removes_all_and_append_preserves_all()
    {
        var empty = Document(); var current = State(Poster());
        Assert.Single(Plan(empty, current).Removes);
        var append = Plan(empty, current, mode: PortableImportMode.Append);
        Assert.Empty(append.Removes); Assert.Empty(append.Creates);
    }
    [Theory] [InlineData(96u)] [InlineData(120u)] [InlineData(144u)] [InlineData(192u)]
    public void Dpi_and_negative_origin_reproject_anchors_without_changing_dip_dimensions(uint dpi)
    {
        var doc = Document(Poster() with { WidthDip = 301.25, ExpandedHeightDip = 361.75, HorizontalAnchor = 1, VerticalAnchor = 1 });
        var topology = Topology(2000, 1800, dpi, -2000, -200);
        var result = Plan(doc, topology: topology).Creates[0];
        Assert.False(result.PlacementPending); Assert.Equal(301.25, result.WidthDip); Assert.Equal(361.75, result.ExpandedHeightDip);
        Assert.Equal(1, result.HorizontalAnchor); Assert.Equal(1, result.VerticalAnchor);
        Assert.Equal((2000 - Math.Round(301.25 * dpi / 96d)) / (dpi / 96d), result.FallbackXDip);
        for (int i = 0; i < 20; i++) result = Plan(Document(result), topology: topology).Creates[0];
        Assert.Equal(301.25, result.WidthDip); Assert.Equal(361.75, result.ExpandedHeightDip);
    }
    [Fact]
    public void Missing_monitor_maps_to_primary_and_is_reported()
    {
        var result = Plan(Document(Poster() with { MonitorDeviceName = "MISSING" }));
        Assert.Equal("DISPLAY1", result.Creates[0].MonitorDeviceName);
        Assert.Contains(result.Warnings, w => w.Contains("原屏幕缺失"));
    }
    [Fact]
    public void Matched_secondary_monitor_is_not_forced_to_primary()
    {
        var top = Topology(); var second = top.Monitors[0] with { DeviceName = "DISPLAY2", IsPrimary = false, WorkArea = new(-1920, 0, 1920, 1080) };
        top = top with { Monitors = Array.AsReadOnly(new[] { top.Monitors[0], second }) };
        var result = Plan(Document(Poster() with { MonitorDeviceName = "DISPLAY2" }), topology: top);
        Assert.Equal("DISPLAY2", result.Creates[0].MonitorDeviceName); Assert.Empty(result.Warnings);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void Too_small_workarea_preserves_all_intent_and_marks_pending(bool visible)
    {
        var original = Poster() with { IsVisible = visible, IsLocked = true, WidthDip = 600, ExpandedHeightDip = 800 };
        var plan = Plan(Document(original), topology: Topology(500, 400)); var poster = plan.Creates[0];
        Assert.True(poster.PlacementPending); Assert.Equal(visible, poster.IsVisible);
        Assert.True(poster.IsLocked); Assert.False(poster.IsCollapsed);
        Assert.Equal(600, poster.WidthDip); Assert.Equal(800, poster.ExpandedHeightDip); Assert.Single(plan.PendingPosterIds);
    }
    [Fact]
    public void Collapsed_intent_can_fit_without_losing_saved_expanded_height()
    {
        var poster = Plan(Document(Poster() with { IsCollapsed = true, ExpandedHeightDip = 5000 }), topology: Topology(500, 70)).Creates[0];
        Assert.True(poster.IsCollapsed); Assert.False(poster.PlacementPending); Assert.Equal(5000, poster.ExpandedHeightDip);
    }
    [Fact]
    public void Persistent_visible_obstacles_are_reserved_while_temporarily_globally_hidden()
    {
        var state = State(Poster()); var plan = Plan(Document(Poster()), state, Topology(300, 360), PortableImportMode.Append);
        Assert.True(plan.Creates[0].PlacementPending); Assert.True(plan.Creates[0].IsVisible);
        Assert.Equal(state.Catalog.CatalogVersion, plan.ReadSet.CatalogVersion);
        Assert.Equal(state.Settings.EntityVersion, plan.ReadSet.SettingsVersion);
        Assert.Equal(7, plan.ReadSet.Posters["a"]);
    }
    [Fact]
    public void Pending_does_not_block_retry_and_success_preserves_identity_and_visibility()
    {
        var pending = Poster("pending") with { PlacementPending = true, IsLocked = true };
        var state = State(pending); var plan = PortableProfilePlanner.TryPlacePending(state, Topology());
        Assert.True(plan.IsRetry); Assert.Empty(plan.Creates); Assert.Empty(plan.Removes); Assert.Empty(plan.PendingPosterIds);
        var update = Assert.Single(plan.Updates); Assert.Equal("pending", update.PosterId);
        Assert.Equal(pending.ThemeInstanceId, update.ThemeInstanceId); Assert.Equal(pending.CollectionId, update.CollectionId);
        Assert.True(update.IsVisible); Assert.True(update.IsLocked); Assert.False(update.PlacementPending);
    }
    [Fact]
    public void Failed_retry_does_not_mutate_configuration()
    {
        var pending = Poster("pending") with { PlacementPending = true, MonitorDeviceName = "OLD" };
        var plan = PortableProfilePlanner.TryPlacePending(State(pending), Topology(200, 200));
        Assert.Empty(plan.Updates); Assert.Equal(new[] { "pending" }, plan.PendingPosterIds);
    }
    [Fact]
    public void Placement_is_deterministic_bounded_and_nonoverlapping()
    {
        var doc = Document(Enumerable.Range(0, 128).Select(i => Poster(i.ToString()) with { StackOrder = i }).ToArray());
        var a = Plan(doc, topology: Topology(1920, 1080)); var b = Plan(doc, topology: Topology(1920, 1080));
        Assert.Equal(a.Creates, b.Creates); Assert.Equal(a.PendingPosterIds, b.PendingPosterIds);
        var rectangles = a.Creates.Where(p => !p.PlacementPending).Select(p => new SpatialRect((int)p.FallbackXDip, (int)p.FallbackYDip, (int)p.WidthDip, (int)p.ExpandedHeightDip)).ToArray();
        for (int i = 0; i < rectangles.Length; i++) for (int j = i + 1; j < rectangles.Length; j++) Assert.False(rectangles[i].Overlaps(rectangles[j]));
        Assert.NotEmpty(a.PendingPosterIds);
    }
    [Fact]
    public void Settings_import_is_optional_and_never_changes_local_startup_registration_intent()
    {
        var current = State() with { Settings = State().Settings with { Settings = new(false, DesktopUpPerformanceMode.Static, false) } };
        var doc = Document(); var enabled = PortableProfilePlanner.Plan(doc, current, Topology(), PortableImportMode.Append, true, Ids());
        Assert.NotNull(enabled.Settings); Assert.True(enabled.Settings.DynamicEffectsPaused);
        Assert.False(enabled.Settings.LaunchAtStartupRequested);
        Assert.Null(PortableProfilePlanner.Plan(doc, current, Topology(), PortableImportMode.Append, false, Ids()).Settings);
    }
    [Fact]
    public void Duplicate_ids_bad_topology_or_bad_mode_are_rejected()
    {
        var doc = Document(Poster("a"), Poster("b"));
        Assert.Throws<ArgumentException>(() => PortableProfilePlanner.Plan(doc, State(), Topology(), PortableImportMode.Append, false, () => "same"));
        Assert.Throws<ArgumentException>(() => PortableProfilePlanner.Plan(doc, State(), Topology() with { Monitors = [] }, PortableImportMode.Append, false));
        Assert.Throws<ArgumentException>(() => PortableProfilePlanner.Plan(doc, State(), Topology(), (PortableImportMode)99, false));
    }
    [Fact]
    public void New_theme_identity_cannot_alias_existing_theme_instance()
    {
        var current = State(Poster("existing") with { ThemeInstanceId = "theme-same" });
        Assert.Throws<ArgumentException>(() => PortableProfilePlanner.Plan(Document(Poster()), current, Topology(), PortableImportMode.Replace, false, () => "same"));
    }
    [Fact]
    public void Oversized_layout_payload_is_rejected_before_export_parsing()
    {
        var oversized = Poster() with { LayoutParametersJson = "{\"schemaVersion\":1,\"legacy\":{\"text\":\"" + new string('x', 32768) + "\"}}" };
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(State(oversized)));
    }
}
