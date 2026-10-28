using System.Collections.ObjectModel;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ControlCenterPosterEditorTests
{
    internal static PosterDefinition Seed(string id = "A") => new(id, id, "subtitle", "display",
        0, 0, 300, 600, 0, 0, .3, .6, false, false, false, "adaptive-grid",
        PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults("basic-box")), "basic-box");

    private static PosterSurfaceProjection Target(string id = "A", long version = 1, PosterDefinition? definition = null)
    {
        var p = definition ?? Seed(id);
        return new(1, version, p, !p.IsLocked, !p.IsLocked && !p.IsCollapsed, true) { EntityVersion = version };
    }
    private static DesktopUpProjection View(long revision, params PosterSurfaceProjection[] targets)
    {
        var rows = targets.Select(t => t with { Revision = revision }).ToArray();
        var first = rows.FirstOrDefault();
        var settings = new SettingsProjection(revision, 1, new(false, DesktopUpPerformanceMode.Automatic, false)) { EntityVersion = 1 };
        var center = new ControlCenterProjection(revision, first, settings)
        {
            Posters = rows, PostersById = new ReadOnlyDictionary<string, PosterSurfaceProjection>(rows.ToDictionary(p => p.Poster.PosterId)),
            CatalogVersion = 1
        };
        return new(revision, first, center, new(revision, rows.Length, false, false, ""));
    }
    private static ControlCenterPosterEditor Open(PosterDefinition? seed = null)
    {
        var e = new ControlCenterPosterEditor(); e.Synchronize(View(1, Target(definition: seed)), "A"); return e;
    }
    internal static bool Title(ControlCenterPosterEditor e, string title)
    {
        var p = e.Presentation!;
        return e.EditPresentation(title, p.Subtitle, p.TemplateKey, p.Parameters);
    }
    private static StateCommandResult Reply(PosterStateCommand c, DesktopUpProjection? projection,
        StateCommandStatus status = StateCommandStatus.Committed) => new(status, c.RequestId, c.ExpectedRevision,
        projection?.Revision ?? 2, projection, null) { ResultPosterId = c.PosterId, CommittedRevision = 2 };

    [Fact]
    public void Open_is_read_only_clean_and_has_no_application_or_immediate_action_without_target()
    {
        var e = new ControlCenterPosterEditor();
        Assert.Null(e.BeginGeometryApply()); Assert.False(e.CanRunImmediateAction);
        e.Synchronize(View(1, Target()), "A");
        Assert.False(e.IsDirty); Assert.False(e.ProtectsInteraction); Assert.False(e.CanApply);
        Assert.True(e.CanRunImmediateAction); Assert.Null(e.BeginPresentationApply());
        Assert.Equal("600", e.Geometry!.ExpandedHeight);
    }

    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("1e3")]
    [InlineData("1,000")] [InlineData(" 1")] [InlineData("1 ")] [InlineData("１２")] [InlineData("1..2")]
    public void Invalid_geometry_text_is_retained_without_a_committable_request(string text)
    {
        var e = Open(); Assert.True(e.EditGeometry(e.Geometry! with { X = text }));
        Assert.Equal(text, e.Geometry!.X); Assert.NotNull(e.ValidationError);
        Assert.Null(e.BeginGeometryApply()); Assert.True(e.CanCancel); Assert.True(e.ProtectsInteraction);
        e.Cancel(); Assert.Equal("0", e.Geometry.X); Assert.False(e.ProtectsInteraction);
    }

    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("239.9")]
    public void Geometry_size_uses_existing_domain_minimum(string width)
    {
        var e = Open(); e.EditGeometry(e.Geometry! with { Width = width });
        Assert.NotNull(e.ValidationError); Assert.Null(e.BeginGeometryApply());
    }

    [Theory]
    [InlineData("12.5", 12.5)] [InlineData("-10", -10)] [InlineData("+3", 3)] [InlineData("00", 0)]
    public void Geometry_parsing_is_explicit_and_never_clamps_before_spatial_validation(string text, double value)
    {
        var e = Open(); e.EditGeometry(e.Geometry! with { X = text });
        var r = e.BeginGeometryApply()!; Assert.Equal(value, r.Desired.X);
        Assert.Equal("display", r.Basis.Poster.MonitorDeviceName);
        Assert.Equal(600, r.Desired.ExpandedHeight);
        Assert.DoesNotContain(typeof(IStateCommandService).GetMethods(),
            m => m.GetParameters().Any(p => p.ParameterType == typeof(ControlCenterGeometryRequest)));
        Assert.Null(typeof(ControlCenterGeometryRequest).GetMethod("ToCommand"));
    }

    [Fact]
    public void Geometry_and_presentation_cannot_be_silently_partially_applied_together()
    {
        var e = Open(); e.EditGeometry(e.Geometry! with { X = "40" });
        Assert.False(Title(e, "bad")); Assert.NotNull(e.Error);
        Assert.Null(e.BeginPresentationApply()); Assert.Equal("A", e.Presentation!.Title);
        e.Cancel(); Assert.True(Title(e, "good"));
        Assert.False(e.EditGeometry(e.Geometry! with { X = "50" }));
        Assert.Null(e.BeginGeometryApply()); Assert.Equal("0", e.Geometry!.X);
    }

    [Fact]
    public void Projection_refresh_keeps_raw_draft_and_frozen_target_version()
    {
        var e = Open(); Title(e, "draft");
        for (int i = 2; i < 50; i++) e.Synchronize(View(i, Target(), Target("B", i)), "A");
        Assert.Equal("draft", e.Presentation!.Title); Assert.False(e.IsStale);
        var c = e.BeginPresentationApply()!;
        Assert.Equal(1, c.ExpectedRevision); Assert.Equal("A", c.PosterId);
        Assert.Equal(1, c.ReadSet!.Posters["A"]); Assert.Single(c.ReadSet.Posters);
    }

    [Fact]
    public void Same_target_external_change_marks_stale_without_rebasing_and_cancel_loads_latest()
    {
        var e = Open(); Title(e, "draft");
        e.Synchronize(View(2, Target(version: 2, definition: Seed() with { Title = "external" })), "A");
        Assert.True(e.IsStale); Assert.Equal("draft", e.Presentation!.Title);
        Assert.Equal("external", e.LatestCommitted!.Poster.Title);
        e.Cancel(); Assert.False(e.IsDirty); Assert.False(e.IsStale); Assert.Equal("external", e.Presentation.Title);
    }

    [Fact]
    public void Pending_freezes_edits_cancel_and_duplicate_submit()
    {
        var e = Open(); Title(e, "draft"); var command = e.BeginPresentationApply()!;
        Assert.True(e.IsPending); Assert.False(e.CanRunImmediateAction);
        Assert.False(Title(e, "changed")); Assert.Null(e.BeginPresentationApply()); e.Cancel();
        Assert.Equal("draft", e.Presentation!.Title); Assert.True(e.IsPending);
        Assert.True(e.Complete(Reply(command, View(2, Target(version: 2, definition: command.DesiredState)))));
        Assert.False(e.IsPending); Assert.False(e.IsDirty); Assert.True(e.CanRunImmediateAction);
    }

    [Fact]
    public void Switching_and_reentering_same_target_does_not_accept_old_request()
    {
        var e = Open(); Title(e, "old"); var a = e.BeginPresentationApply()!;
        e.Synchronize(View(1, Target(), Target("B")), "B"); Title(e, "B draft");
        Assert.False(e.Complete(Reply(a, View(2, Target(version: 2, definition: a.DesiredState), Target("B")))));
        Assert.Equal("B draft", e.Presentation!.Title);
        e.Synchronize(View(2, Target(version: 2), Target("B")), "A"); Title(e, "new");
        var newer = e.BeginPresentationApply()!; Assert.NotEqual(a.RequestId, newer.RequestId);
        Assert.False(e.Complete(Reply(a, View(3, Target(version: 3)))));
        Assert.True(e.IsPending); Assert.Equal("new", e.Presentation!.Title);
    }

    [Fact]
    public void Older_projection_cannot_reselect_deleted_target()
    {
        var e = Open(); Title(e, "draft"); var old = View(1, Target());
        e.Synchronize(View(3), "A");
        Assert.Null(e.PosterId); Assert.False(e.CanApply); Assert.False(e.ProtectsInteraction);
        Assert.False(e.Synchronize(old, "A")); Assert.Null(e.PosterId);
    }

    [Fact]
    public void Close_discards_unapplied_state_and_ignores_late_results_and_projections()
    {
        var e = Open(); Title(e, "draft"); var c = e.BeginPresentationApply()!;
        e.Close(); Assert.Null(e.PosterId); Assert.False(e.ProtectsInteraction);
        Assert.False(e.Complete(Reply(c, View(2, Target(version: 2, definition: c.DesiredState)))));
        Assert.False(e.Synchronize(View(3, Target()), "A"));
        var reopened = Open(); Assert.Equal("A", reopened.Presentation!.Title);
    }

    [Fact]
    public void Geometry_freezes_complete_catalog_reads_but_keeps_stale_own_basis()
    {
        var e = Open(); e.EditGeometry(e.Geometry! with { X = "60" });
        var current = View(5, Target(version: 2), Target("B", 4));
        e.Synchronize(current, "A"); var r = e.BeginGeometryApply()!;
        Assert.Equal(5, r.ObservedRevision); Assert.Equal(1, r.ReadSet.Posters["A"]);
        Assert.Equal(4, r.ReadSet.Posters["B"]); Assert.Equal(1, r.ReadSet.CatalogVersion);
        Assert.False(r.ReadsCurrent(current));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, long>)r.ReadSet.Posters)["A"] = 100);
    }

    [Fact]
    public void Geometry_reads_reject_obstacle_change_or_membership_change()
    {
        var e = Open(); var start = View(2, Target(), Target("B"));
        e.Synchronize(start, "A"); e.EditGeometry(e.Geometry! with { X = "10" });
        var r = e.BeginGeometryApply()!;
        Assert.True(r.ReadsCurrent(start));
        Assert.False(r.ReadsCurrent(View(3, Target(), Target("B", 2))));
        Assert.False(r.ReadsCurrent(View(3, Target())));
        Assert.False(r.ReadsCurrent(start with { ControlCenter = start.ControlCenter with { CatalogVersion = 2 } }));
    }

    [Fact]
    public void Effective_height_is_separate_and_cannot_replace_saved_intent()
    {
        var e = Open(); var projection = View(1, Target()); var p = projection.PostersById["A"];
        var env = new SpatialEnvironment("display", new(0, 0, 1200, 900), 96, 96, 1, 1, Guid.NewGuid());
        var temporary = new EffectivePosterLayout(p.Poster, new(0, 0, 300, 300), env, p.Presentation.Metrics.Version, 1, 0);
        e.Synchronize(projection, "A", temporary);
        Assert.True(e.EffectiveLayout!.IsConstrained); Assert.Equal("600", e.Geometry!.ExpandedHeight);
        e.EditGeometry(e.Geometry with { X = "20" });
        e.Synchronize(projection, "A", temporary with { Bounds = new(0, 0, 300, 260) });
        Assert.Equal("20", e.Geometry.X); Assert.Equal(260, e.EffectiveLayout!.HeightDip);
        Assert.Equal(600, e.BeginGeometryApply()!.Desired.ExpandedHeight);
    }

    [Fact]
    public void Foreign_or_stale_effective_layout_is_not_displayed()
    {
        var e = Open(); var v = View(1, Target());
        var env = new SpatialEnvironment("display", new(0, 0, 1000, 800), 96, 96, 1, 1, Guid.NewGuid());
        var wrong = new EffectivePosterLayout(Seed("B"), new(0, 0, 300, 300), env, "x", 1, 0);
        e.Synchronize(v, "A", wrong); Assert.Null(e.EffectiveLayout);
        e.Synchronize(v, "A", wrong with { Intent = Seed(), Revision = 0 }); Assert.Null(e.EffectiveLayout);
    }

    [Fact]
    public void Locked_geometry_cannot_apply_but_presentation_is_independent()
    {
        var e = Open(Seed() with { IsLocked = true });
        e.EditGeometry(e.Geometry! with { X = "3" }); Assert.Null(e.BeginGeometryApply());
        e.Cancel(); Title(e, "Allowed"); Assert.NotNull(e.BeginPresentationApply());
    }

    [Fact]
    public void Collapsed_position_request_does_not_change_posture_or_allow_resize()
    {
        var e = Open(Seed() with { IsCollapsed = true });
        e.EditGeometry(e.Geometry! with { Width = "350" }); Assert.Null(e.BeginGeometryApply());
        e.Cancel(); e.EditGeometry(e.Geometry! with { X = "10" });
        Assert.True(e.BeginGeometryApply()!.Basis.Poster.IsCollapsed);
    }

    [Theory]
    [InlineData(StateCommandStatus.Conflict)] [InlineData(StateCommandStatus.Failed)]
    [InlineData(StateCommandStatus.PermissionDenied)] [InlineData(StateCommandStatus.InsufficientSpace)]
    public void Failure_retains_draft_and_releases_pending_without_success_claim(StateCommandStatus status)
    {
        var e = Open(); Title(e, "draft"); var c = e.BeginPresentationApply()!;
        e.Complete(Reply(c, View(2, Target(version: 2)), status));
        Assert.False(e.IsPending); Assert.True(e.IsDirty); Assert.NotNull(e.Error);
        Assert.Equal("draft", e.Presentation!.Title); e.Cancel(); Assert.Null(e.Error);
    }

    [Fact]
    public void Missing_or_wrong_target_success_is_not_a_saved_draft()
    {
        foreach (var projection in new DesktopUpProjection?[] { null, View(2, Target("B")) })
        {
            var e = Open(); Title(e, "draft"); var c = e.BeginPresentationApply()!;
            e.Complete(Reply(c, projection));
            Assert.True(e.IsDirty); Assert.NotNull(e.Error); Assert.False(e.IsPending);
        }
    }

    [Fact]
    public void Wrong_expected_revision_reply_does_not_end_the_current_request()
    {
        var e = Open(); Title(e, "draft"); var c = e.BeginPresentationApply()!;
        Assert.False(e.Complete(Reply(c, View(2, Target())) with { ExpectedRevision = 99 }));
        Assert.True(e.IsPending);
    }

    [Fact]
    public void Delayed_success_does_not_roll_back_a_newer_projection()
    {
        var e = Open(); Title(e, "draft"); var c = e.BeginPresentationApply()!;
        e.Synchronize(View(5, Target(version: 4, definition: Seed() with { Title = "newest" })), "A");
        e.Complete(Reply(c, View(2, Target(version: 2, definition: c.DesiredState))));
        Assert.Equal("newest", e.Presentation!.Title); Assert.False(e.IsDirty);
    }

    [Fact]
    public void Editing_back_to_basis_reloads_latest_and_releases_protection()
    {
        var e = Open(); Title(e, "draft");
        e.Synchronize(View(2, Target(version: 2, definition: Seed() with { Title = "external" })), "A");
        Title(e, "A"); Assert.Equal("external", e.Presentation!.Title); Assert.False(e.ProtectsInteraction);
    }

    [Fact]
    public void Unsupported_theme_is_read_only_without_erasing_raw_configuration()
    {
        var seed = Seed() with { LayoutParametersJson = "{\"schemaVersion\":999}" };
        var e = Open(seed);
        Assert.Null(e.Presentation);
        Assert.False(e.EditPresentation("new", "", "basic-box", PosterPresentationPolicy.Defaults("basic-box")));
        e.EditGeometry(e.Geometry! with { X = "1" });
        Assert.Equal(seed.LayoutParametersJson, e.BeginGeometryApply()!.Basis.Poster.LayoutParametersJson);
    }

    [Fact]
    public void Invalid_presentation_fields_are_not_sent_to_repository()
    {
        var e = Open(); Title(e, "");
        Assert.NotNull(e.ValidationError); Assert.Null(e.BeginPresentationApply());
        e.Cancel(); var p = e.Presentation!;
        e.EditPresentation(p.Title, p.Subtitle, p.TemplateKey,
            p.Parameters with { Content = p.Parameters.Content with { IconSizeDip = 10000 } });
        Assert.NotNull(e.ValidationError); Assert.Null(e.BeginPresentationApply());
    }
}
