using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleVisibilityTests
{
    private static PosterDefinition Poster(string json, string template = "basic-box") =>
        PosterConfigurationFactory.NewBasicBox("Named poster", "DISPLAY1") with { PosterId = "A", LayoutParametersJson = json, TemplateKey = template };

    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")] [InlineData("lamp")] [InlineData("rain")]
    public void Visibility_is_host_only_versioned_paint_and_preserves_name_geometry_and_template(string template)
    {
        var original = PosterPresentationPolicy.Defaults(template);
        if (TemplateRecipeCatalog.BuiltIns.FirstOrDefault(r => r.Id == template) is { } recipe)
            original = TemplateRecipeInstancePolicy.Select(original, recipe);
        var source = Poster(PosterPresentationPolicy.Serialize(original), template);
        var before = PosterPresentationPolicy.Resolve(source);
        Assert.True(before.CanEdit, before.Warning);
        Assert.True(before.Parameters.IsTitleVisible); Assert.Null(before.Parameters.ShowTitle);
        var hidden = PosterPresentationPolicy.Serialize(before.Parameters with { ShowTitle = false }, source.LayoutParametersJson);
        var after = PosterPresentationPolicy.Resolve(source with { LayoutParametersJson = hidden });
        Assert.True(after.CanEdit, after.Warning); Assert.False(after.Parameters.IsTitleVisible);
        using var doc = JsonDocument.Parse(hidden);
        Assert.Equal(6, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(before.Metrics, after.Metrics);
        Assert.Equal(before.TitleChrome, after.TitleChrome);
        Assert.Equal(before.Parameters.Content, after.Parameters.Content);
        Assert.Equal(before.Parameters.Theme, after.Parameters.Theme);
        Assert.True(TemplatePresentationRefreshPolicy.CanPaint(before, after, source.Title, source.Title, "", ""));
        Assert.Null(PosterPresentationPolicy.ValidateEdit(source, source with { LayoutParametersJson = hidden }));
        var shown = PosterPresentationPolicy.Resolve(source with { LayoutParametersJson =
            PosterPresentationPolicy.Serialize(after.Parameters with { ShowTitle = true }, hidden) });
        Assert.True(shown.Parameters.IsTitleVisible);
        Assert.Equal("Named poster", source.Title);
    }

    [Fact]
    public void Older_writer_retains_visibility_colors_and_opaque_legacy_without_silent_downgrade()
    {
        const string legacy = """{"custom":{"keep":[1,2,3]}}""";
        var value = PosterPresentationPolicy.Defaults() with { ShowTitle = false, TitleColors = new("#123456", "#ABCDEF"), Lamp = new() };
        var first = PosterPresentationPolicy.Serialize(value, legacy);
        var second = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), first);
        var read = PosterPresentationPolicy.Resolve(Poster(second));
        Assert.True(read.CanEdit); Assert.False(read.Parameters.IsTitleVisible);
        Assert.Equal(value.TitleColors, read.Parameters.TitleColors); Assert.Equal(value.Lamp, read.Parameters.Lamp);
        using var doc = JsonDocument.Parse(second); using var old = JsonDocument.Parse(legacy);
        Assert.True(JsonElement.DeepEquals(old.RootElement, doc.RootElement.GetProperty("legacy")));
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(Poster(first),
            Poster(PosterPresentationPolicy.Serialize(value with { ShowTitle = null }, legacy))));
    }

    [Theory]
    [InlineData("""{"schemaVersion":6}""")]
    [InlineData("""{"schemaVersion":6,"showTitle":null}""")]
    [InlineData("""{"schemaVersion":6,"showTitle":0}""")]
    [InlineData("""{"schemaVersion":6,"showTitle":"false"}""")]
    [InlineData("""{"schemaVersion":6,"showTitle":false,"showTitle":true}""")]
    [InlineData("""{"schemaVersion":6,"showTitle":false,"unknown":0}""")]
    [InlineData("""{"schemaVersion":5,"showTitle":false}""")]
    [InlineData("""{"schemaVersion":7,"showTitle":false}""")]
    public void Malformed_or_future_visibility_is_read_only_and_not_rewritten(string json)
    {
        Assert.False(PosterPresentationPolicy.Resolve(Poster(json)).CanEdit);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json));
    }
}

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Title_visibility_cancel_commit_replay_switch_template_and_reopen_are_isolated()
    {
        using var f = new Fixture(); var b = f.Add(); var original = f.Current;
        var session = new ControlCenterSettingsSession();
        Assert.True(session.Update(new(original.PostersById["A"], PosterSetting.TitleVisibility, 0)));
        session.CancelPreview(); Assert.Null(session.TakeNext()); Assert.Equal(original.Revision, f.Current.Revision);
        Assert.True(session.Update(new(original.PostersById["A"], PosterSetting.TitleVisibility, 0)));
        session.Finish(); var command = session.TakeNext()!;
        Assert.Single(command.ReadSet!.Posters);
        Assert.True(session.Complete(f.Coordinator.Execute(command)));
        Assert.Equal(original.Revision + 1, f.Current.Revision);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        var saved = f.Current.PostersById["A"].Poster;
        Assert.Equal(original.PostersById["A"].Poster with { LayoutParametersJson = saved.LayoutParametersJson }, saved);
        Assert.Equal(original.PostersById[b].Poster, f.Current.PostersById[b].Poster);
        foreach (var edit in new[] { PosterSetting.Scale, PosterSetting.Template, PosterSetting.TitleColors })
        {
            Assert.True(session.Update(new(f.Current.PostersById["A"], edit, 1.2, "basic-box", TitleColors: new("#123456", "#ABCDEF"))));
            session.Finish(); Assert.True(session.Complete(f.Coordinator.Execute(session.TakeNext()!)));
        }
        f.Rename("A", "New name");
        using var reopenedStore = new SqliteStateStore(f.Paths);
        Assert.True(reopenedStore.Initialize().Succeeded);
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(reopenedStore)).Refresh();
        Assert.False(reopened.PostersById["A"].Presentation.Parameters.IsTitleVisible);
        Assert.Equal("New name", reopened.PostersById["A"].Poster.Title);
        Assert.Equal(new("#123456", "#ABCDEF"), reopened.PostersById["A"].Presentation.Parameters.TitleColors);
        Assert.True(reopened.PostersById[b].Presentation.Parameters.IsTitleVisible);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Title_visibility_conflict_or_write_failure_does_not_overwrite(bool sqlFailure)
    {
        using var f = new Fixture(); var session = new ControlCenterSettingsSession();
        Assert.True(session.Update(new(f.Current.PostersById["A"], PosterSetting.TitleVisibility, 0)));
        session.Finish();
        if (sqlFailure) f.Sql("CREATE TRIGGER fail_visibility BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected'); END;");
        else f.Rename("A", "external");
        var before = f.Current;
        var result = f.Coordinator.Execute(session.TakeNext()!);
        Assert.False(result.Succeeded); Assert.True(session.Complete(result));
        Assert.Equal(before.PostersById["A"].Poster, f.Coordinator.Refresh().PostersById["A"].Poster);
        Assert.Equal(before.Revision, f.Current.Revision); Assert.Null(session.TakeNext());
    }

    [Theory] [InlineData(-1)] [InlineData(0.5)] [InlineData(2)]
    public void Title_visibility_rejects_non_boolean_values(double number)
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => new PosterSettingEdit(f.Current.PostersById["A"],
            PosterSetting.TitleVisibility, number).Command("invalid"));
    }
}
