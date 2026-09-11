using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ControlCenterAutoPostureEditorTests
{
    private static PosterSurfaceProjection Target(string id = "A", long revision = 1, long version = 1, int delay = 3) =>
        new(revision, revision, new PosterDefinition(id, "Box", "", "DISPLAY1", 0, 0, 300, 600,
            0, 0, .3, .6, false, true, false, "grid", "{}", "basic-box") { AutoCollapseDelaySeconds = delay }, true, true, true)
            { EntityVersion = version };
    private static ControlCenterAutoPostureEditor Open()
    {
        var e = new ControlCenterAutoPostureEditor(); e.Synchronize(Target()); return e;
    }

    [Fact]
    public void Opening_is_clean_and_does_not_protect_or_issue_commands()
    {
        var e = Open(); Assert.False(e.Enabled); Assert.Equal("3", e.DelayText);
        Assert.False(e.ProtectsInteraction); Assert.False(e.CanApply); Assert.False(e.CanCancel);
        Assert.Null(e.BeginApply("empty"));
    }
    [Theory]
    [InlineData("")] [InlineData("0")] [InlineData("301")] [InlineData("-1")]
    [InlineData("1.5")] [InlineData("3.0")] [InlineData("1e2")] [InlineData("abc")]
    [InlineData(" 3")] [InlineData("+3")] [InlineData("３")] [InlineData("9999999999")]
    public void Invalid_raw_input_is_preserved_and_cannot_be_submitted(string text)
    {
        var e = Open(); e.Edit(false, text); Assert.Equal(text, e.DelayText);
        Assert.NotNull(e.ValidationError); Assert.Null(e.BeginApply("invalid"));
        Assert.False(e.CanApply); Assert.True(e.CanCancel); Assert.True(e.ProtectsInteraction);
        e.Cancel(); Assert.Equal("3", e.DelayText); Assert.False(e.ProtectsInteraction);
    }
    [Theory]
    [InlineData("1", 1)] [InlineData("3", 3)] [InlineData("300", 300)] [InlineData("03", 3)]
    public void Valid_draft_freezes_target_and_entity_reads(string text, int expected)
    {
        var e = Open(); e.Edit(true, text); var c = e.BeginApply("apply")!;
        Assert.Equal("A", c.PosterId); Assert.Equal(expected, c.DesiredState.AutoCollapseDelaySeconds);
        Assert.Equal(PosterStateCommandKind.SetAutoPosture, c.Kind); Assert.Equal(StateCommandSource.ControlCenter, c.Source);
        Assert.Equal(new KeyValuePair<string, long>("A", 1), Assert.Single(c.ReadSet!.Posters)); Assert.True(c.DesiredState.IsCollapsed);
        Assert.True(e.IsPending); Assert.False(e.CanApply); Assert.False(e.CanCancel);
        Assert.Null(e.BeginApply("duplicate")); e.Edit(false, "20"); e.Cancel(); Assert.Equal(text, e.DelayText);
    }
    [Fact]
    public void Repeated_session_and_unrelated_entity_projections_do_not_reset_dirty_text()
    {
        var e = Open(); e.Edit(true, "30");
        for (int n = 1; n < 100; n++) e.Synchronize(Target(revision: n));
        Assert.Equal("30", e.DelayText); Assert.False(e.IsStale); Assert.Equal(1, e.BeginApply("apply")!.ExpectedRevision);
    }
    [Fact]
    public void Changed_target_does_not_rebase_and_cancel_loads_latest_committed_values()
    {
        var e = Open(); e.Edit(true, "10"); e.Synchronize(Target(revision: 2, version: 2, delay: 80));
        Assert.True(e.IsStale); Assert.Equal("10", e.DelayText);
        var command = e.BeginApply("stale")!; Assert.Equal(new KeyValuePair<string, long>("A", 1), Assert.Single(command.ReadSet!.Posters));
        Assert.True(e.Complete(new(StateCommandStatus.Conflict, "stale", 1, 2, null, "Entity changed")));
        Assert.True(e.IsDirty); Assert.NotNull(e.Error); Assert.Equal("10", e.DelayText);
        e.Cancel(); Assert.False(e.IsStale); Assert.False(e.ProtectsInteraction); Assert.Equal("80", e.DelayText);
    }
    [Fact]
    public void Switch_and_late_result_cannot_redirect_or_clear_another_target()
    {
        var e = Open(); e.Edit(true, "10"); var a = e.BeginApply("old-a")!;
        e.Synchronize(Target("B")); e.Edit(false, "22");
        Assert.False(e.Complete(new(StateCommandStatus.Conflict, "old-a", 1, 2, null, "stale")));
        Assert.Equal("A", a.PosterId); Assert.Equal("B", e.PosterId); Assert.Equal("22", e.DelayText); Assert.Null(e.Error);
        Assert.Equal("B", e.BeginApply("b")!.PosterId);
    }
    [Fact]
    public void Delete_or_zero_selection_discards_draft_and_disables_controls()
    {
        var e = Open(); e.Edit(true, "9"); e.Synchronize(null);
        Assert.Null(e.PosterId); Assert.False(e.CanApply); Assert.False(e.CanCancel); Assert.False(e.ProtectsInteraction);
        e.Edit(true, "10"); Assert.Null(e.BeginApply("none")); Assert.Equal("", e.DelayText);
    }
    [Fact]
    public void Clean_draft_tracks_latest_but_ignores_older_projection()
    {
        var e = Open(); e.Synchronize(Target(revision: 8, version: 3, delay: 14));
        e.Synchronize(Target()); Assert.Equal("14", e.DelayText); Assert.False(e.IsDirty);
    }
    [Fact]
    public void Editing_back_to_basis_releases_protection_and_loads_newest()
    {
        var e = Open(); e.Edit(true, "3"); e.Synchronize(Target(revision: 2, version: 2, delay: 7));
        e.Edit(false, "3"); Assert.False(e.IsDirty); Assert.Equal("7", e.DelayText); Assert.False(e.ProtectsInteraction);
    }
    [Fact]
    public void Success_without_authoritative_projection_does_not_claim_old_values_were_saved()
    {
        var e = Open(); e.Edit(true, "10"); e.BeginApply("a");
        e.Complete(new(StateCommandStatus.Committed, "a", 1, 2, null, null));
        Assert.NotNull(e.Error); Assert.True(e.IsDirty); Assert.False(e.IsPending);
    }
}

public sealed partial class AutoPostureSettingsTests
{
    [Fact]
    public void Editor_apply_survives_reentrant_projection_and_cancel_does_not_write()
    {
        using var f = new Fixture(); string b = f.Add(); var e = new ControlCenterAutoPostureEditor();
        e.Synchronize(f.Current.PostersById["A"]); long revision = f.Current.Revision;
        e.Edit(true, "7"); Assert.Equal(revision, f.Current.Revision);
        e.Cancel(); Assert.Equal(revision, f.Current.Revision);
        e.Edit(true, "300");
        f.Coordinator.ProjectionChanged += (_, args) => e.Synchronize(args.Projection.PostersById["A"]);
        var r = f.Coordinator.Execute(e.BeginApply("editor-apply")!);
        Assert.True(e.IsPending); Assert.True(e.IsDirty); Assert.True(e.Complete(r));
        Assert.False(e.IsDirty); Assert.False(e.ProtectsInteraction); Assert.Equal("300", e.DelayText);
        Assert.Equal(revision + 1, f.Current.Revision); Assert.Equal(3, f.Current.PostersById[b].AutoPosture.LeaveDelaySeconds);
        var reopened = new ControlCenterAutoPostureEditor(); reopened.Synchronize(f.Current.PostersById["A"]);
        Assert.True(reopened.Enabled); Assert.Equal("300", reopened.DelayText);
    }
    [Fact]
    public void Editor_stale_apply_reports_conflict_without_overwrite_then_cancel_recovers()
    {
        using var f = new Fixture(); var e = new ControlCenterAutoPostureEditor();
        var target = f.Current.PostersById["A"]; e.Synchronize(target); e.Edit(true, "8");
        Assert.True(f.Coordinator.Execute((PosterAutoPostureDraft.Open(target) with { LeaveDelaySeconds = 60 }).ToCommand("external")).Succeeded);
        e.Synchronize(f.Current.PostersById["A"]); long revision = f.Current.Revision;
        var result = f.Coordinator.Execute(e.BeginApply("stale-editor")!);
        Assert.Equal(StateCommandStatus.Conflict, result.Status); e.Complete(result);
        Assert.Equal(revision, f.Current.Revision); Assert.Equal("8", e.DelayText); Assert.NotNull(e.Error);
        e.Cancel(); Assert.Equal("60", e.DelayText); Assert.False(e.Enabled); Assert.False(e.ProtectsInteraction);
    }
    [Fact]
    public void Editor_equivalent_numeric_text_nochange_releases_pending_without_revision()
    {
        using var f = new Fixture(); var e = new ControlCenterAutoPostureEditor();
        e.Synchronize(f.Current.PostersById["A"]); long revision = f.Current.Revision; e.Edit(false, "03");
        var result = f.Coordinator.Execute(e.BeginApply("same-numeric")!);
        Assert.Equal(StateCommandStatus.NoChange, result.Status); e.Complete(result);
        Assert.Equal(revision, f.Current.Revision); Assert.False(e.IsDirty); Assert.False(e.IsPending); Assert.Equal("3", e.DelayText);
    }
}
