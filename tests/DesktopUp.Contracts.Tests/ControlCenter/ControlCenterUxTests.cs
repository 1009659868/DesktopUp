using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ControlCenterEditTransactionTests
{

    [Theory]
    [InlineData(PosterGeometryFields.Width)]
    [InlineData(PosterGeometryFields.Height)]
    [InlineData(PosterGeometryFields.X)]
    [InlineData(PosterGeometryFields.Y)]
    public void Ux_actual_intent_uses_single_existing_space_transaction(PosterGeometryFields field)
    {
        using var f = new Fixture(); var before = f.Current; var target = before.PostersById["A"];
        var original = SceneForCenter(before);
        var source = original.Posters[0] with { Bounds = original.Posters[0].Bounds with { X = -1500, Y = 50 } };
        var scene = new SpatialSnapshot(original.Environment, new[] { source });
        var basis = new ControlCenterGeometryBasis("A", target.EntityVersion, target.LayoutGeneration, scene.Environment, source.Bounds);
        Assert.True(basis.Matches(scene, target));
        var intent = basis.Resolve(target.Poster, new(430, 160, 320, 580), field);
        var result = f.Coordinator.Execute(CenterCommand(before, scene, intent));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(before.Revision + 1, result.CurrentRevision);
        var actual = result.Projection!.PostersById["A"].Poster;
        Assert.Equal(field == PosterGeometryFields.X ? 430 : 420, actual.FallbackXDip);
        Assert.Equal(field == PosterGeometryFields.Y ? 160 : 150, actual.FallbackYDip);
        Assert.Equal(field == PosterGeometryFields.Width ? 320 : 300, actual.WidthDip);
        Assert.Equal(field == PosterGeometryFields.Height ? 580 : 600, actual.ExpandedHeightDip);
    }

    [Theory]
    [InlineData("bounds")] [InlineData("dpi")] [InlineData("generation")] [InlineData("runtime")]
    public void Ux_geometry_evidence_rejects_stale_actual_frame_or_environment(string changed)
    {
        using var f = new Fixture(); var target = f.Current.PostersById["A"]; var scene = SceneForCenter(f.Current);
        var basis = new ControlCenterGeometryBasis("A", target.EntityVersion, target.LayoutGeneration, scene.Environment, scene.Posters[0].Bounds);
        var stale = changed switch
        {
            "bounds" => basis with { Bounds = basis.Bounds with { X = basis.Bounds.X + 1 } },
            "dpi" => basis with { Environment = basis.Environment with { DpiX = 144 } },
            "generation" => basis with { LayoutGeneration = basis.LayoutGeneration + 1 },
            _ => basis with { Environment = basis.Environment with { RuntimeSessionId = Guid.NewGuid() } }
        };
        Assert.False(stale.Matches(scene, target));
    }

    [Theory]
    [InlineData(96, 600, 180)] [InlineData(144, -600, 240)] [InlineData(192, 800, 360)]
    public void Ux_actual_geometry_not_fallback_and_width_only_keeps_position(int dpi, int x, int y)
    {
        using var f = new Fixture(); var target = f.Current.PostersById["A"];
        var env = new SpatialEnvironment("display", new(-1000, 0, 4000, 2000), (uint)dpi, (uint)dpi, 1, 1, Guid.NewGuid());
        var basis = new ControlCenterGeometryBasis("A", target.EntityVersion, target.LayoutGeneration, env, new(x, y, 300, 450));
        var editor = new ControlCenterPosterEditor(requireActualGeometry: true);
        editor.Synchronize(f.Current, "A", actualGeometry: basis);
        Assert.Equal((x + 1000d) * 96 / dpi, double.Parse(editor.Geometry!.X, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("600", editor.Geometry.ExpandedHeight); // not the 450px temporary height
        editor.EditGeometry(editor.Geometry with { Width = "320" });
        var request = editor.BeginGeometryApply()!;
        Assert.Equal(PosterGeometryFields.Width, request.ChangedFields);
        var resolved = basis.Resolve(target.Poster, request.Desired with { X = 0, Y = 0 }, request.ChangedFields);
        Assert.Equal(request.Desired.X, resolved.X); Assert.Equal(request.Desired.Y, resolved.Y);
        Assert.Equal(600, resolved.ExpandedHeight);
        Assert.False(basis.Matches(target with { EntityVersion = target.EntityVersion + 1 }));
    }

    [Fact]
    public void Ux_unavailable_geometry_has_no_fake_zero_fields_or_command()
    {
        using var f = new Fixture(); var e = new ControlCenterPosterEditor(true); e.Synchronize(f.Current, "A");
        Assert.Null(e.Geometry); Assert.False(e.HasActualGeometry);
        e.EditGeometry(new("0", "0", "300", "600")); Assert.Null(e.BeginGeometryApply());
    }

    [Fact]
    public void Ux_gesture_preview_is_not_a_write_then_one_atomic_revision_and_reopen()
    {
        using var f = new Fixture(); string b = f.Add(); var original = f.Current;
        var s = new ControlCenterSettingsSession(); var target = original.PostersById["A"];
        for (int i = 0; i < 50; i++) Assert.True(s.Update(new(target, PosterSetting.Scale, 1 + i / 100d)));
        Assert.Null(s.TakeNext()); Assert.Equal(original.Revision, f.Current.Revision);
        s.Finish(); var command = s.TakeNext()!;
        Assert.Null(s.TakeNext()); var r = f.Coordinator.Execute(command); Assert.True(s.Complete(r)); Assert.True(r.Succeeded, r.Error);
        Assert.Equal(original.Revision + 1, f.Current.Revision);
        Assert.Equal(1.49, f.Current.PostersById["A"].Presentation.Parameters.Preferences!.Scale);
        Assert.Equal(original.PostersById[b], f.Current.PostersById[b] with { Revision = original.Revision });
        Assert.Equal(target.Poster.WidthDip, f.Current.PostersById["A"].Poster.WidthDip);
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(f.Store)).Refresh();
        Assert.Equal(f.Current.PostersById["A"].Poster, reopened.PostersById["A"].Poster);
    }

    [Fact]
    public void Ux_same_poster_waits_for_own_receipt_then_starts_from_fresh_basis()
    {
        using var f = new Fixture(); var a = f.Current.PostersById["A"]; var s = new ControlCenterSettingsSession();
        s.Update(new(a, PosterSetting.Scale, 1.2)); s.Finish(); var first = s.TakeNext()!;
        foreach (double value in new[] { .6, .8, 1.4 }) Assert.False(s.Update(new(a, PosterSetting.Spacing, value)));
        Assert.False(s.Update(new(a, PosterSetting.AutoEnabled, 1)));
        Assert.Equal(0, s.QueuedCount);
        Assert.True(s.Complete(f.Coordinator.Execute(first)));
        Assert.True(s.Update(new(f.Current.PostersById["A"], PosterSetting.Spacing, 1.4))); s.Finish();
        Assert.True(s.Complete(f.Coordinator.Execute(s.TakeNext()!)));
        Assert.True(s.Update(new(f.Current.PostersById["A"], PosterSetting.AutoEnabled, 1))); s.Finish();
        Assert.True(s.Complete(f.Coordinator.Execute(s.TakeNext()!)));
        Assert.Equal(new PosterContentPreferences(1.2, 1.4), f.Current.PostersById["A"].Presentation.Parameters.Preferences);
        Assert.True(f.Current.PostersById["A"].Poster.AutoCollapseEnabled);
    }

    [Fact]
    public void Ux_external_change_conflicts_and_does_not_overwrite_or_retry()
    {
        using var f = new Fixture(); var s = new ControlCenterSettingsSession(); var a = f.Current.PostersById["A"];
        s.Update(new(a, PosterSetting.Scale, 1.2)); s.Finish(); f.Rename("A", "external");
        var before = f.Current; var r = f.Coordinator.Execute(s.TakeNext()!); Assert.Equal(StateCommandStatus.Conflict, r.Status);
        s.Complete(r); Assert.Null(s.TakeNext()); Assert.NotNull(s.Error);
        Assert.Equal(before.Revision, f.Current.Revision); Assert.Equal("external", f.Current.PostersById["A"].Poster.Title);
    }

    [Fact]
    public void Ux_switch_close_and_late_reply_keep_completed_input_on_original_target()
    {
        using var f = new Fixture(); string b = f.Add(); var s = new ControlCenterSettingsSession();
        s.Update(new(f.Current.PostersById["A"], PosterSetting.Scale, 1.2)); s.Finish(); var aCommand = s.TakeNext()!;
        s.CancelPreview(); s.Update(new(f.Current.PostersById[b], PosterSetting.Spacing, 1.4)); s.Finish();
        s.Update(new(f.Current.PostersById[b], PosterSetting.Scale, 1.5)); s.Close();
        var aResult = f.Coordinator.Execute(aCommand);
        Assert.False(s.Complete(aResult with { RequestId = "old" })); Assert.True(s.IsPending);
        Assert.True(s.Complete(aResult));
        var bCommand = s.TakeNext()!; Assert.Equal(b, bCommand.PosterId);
        Assert.True(s.Complete(f.Coordinator.Execute(bCommand)));
        Assert.Equal(1, f.Current.PostersById[b].Presentation.Parameters.Preferences!.Scale);
        Assert.Equal(1.4, f.Current.PostersById[b].Presentation.Parameters.Preferences!.Spacing);
        Assert.False(s.ProtectsInteraction);
    }

    [Theory]
    [InlineData("")] [InlineData("0")] [InlineData("301")] [InlineData("1.5")] [InlineData("abc")]
    public void Ux_invalid_delay_is_not_queued(string text)
    {
        using var f = new Fixture(); var s = new ControlCenterSettingsSession();
        Assert.False(s.Update(new(f.Current.PostersById["A"], PosterSetting.AutoDelay, Text: text)));
        s.Finish(); Assert.Null(s.TakeNext()); Assert.NotNull(s.Error);
    }

    [Fact]
    public void Ux_sql_failure_rolls_back_revision_and_macro_payload()
    {
        using var f = new Fixture(); var before = f.Current; var s = new ControlCenterSettingsSession();
        s.Update(new(before.PostersById["A"], PosterSetting.Scale, 1.3)); s.Finish();
        f.Sql("CREATE TRIGGER fail_ux BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected'); END;");
        var r = f.Coordinator.Execute(s.TakeNext()!); Assert.False(r.Succeeded); s.Complete(r);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Equal(before.PostersById["A"].Poster.LayoutParametersJson, f.Current.PostersById["A"].Poster.LayoutParametersJson);
        Assert.False(s.ProtectsInteraction); Assert.NotNull(s.Error);
    }
}
