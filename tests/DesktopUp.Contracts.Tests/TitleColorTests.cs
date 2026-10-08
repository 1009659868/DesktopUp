using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleColorTests
{
    private static PosterDefinition Poster(string json, string template = "basic-box") =>
        PosterConfigurationFactory.NewBasicBox("A", "A") with { LayoutParametersJson = json, TemplateKey = template };

    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")] [InlineData("lamp")]
    public void Title_only_edit_is_versioned_without_content_geometry_or_theme_change(string template)
    {
        var original = PosterPresentationPolicy.Defaults(template);
        var before = PosterPresentationPolicy.Resolve(Poster(PosterPresentationPolicy.Serialize(original), template));
        var json = PosterPresentationPolicy.Serialize(original with { TitleColors = new("#abcdef", "#123456") });
        var after = PosterPresentationPolicy.Resolve(Poster(json, template));
        Assert.True(after.CanEdit, after.Warning);
        Assert.Equal(5, JsonDocument.Parse(json).RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(new("#ABCDEF", "#123456"), after.Parameters.TitleColors);
        Assert.Equal(before.Parameters.Content, after.Parameters.Content);
        Assert.Equal(before.Parameters.Theme, after.Parameters.Theme);
        Assert.Equal(before.Metrics, after.Metrics);
        Assert.True(TemplatePresentationRefreshPolicy.CanPaint(before, after, "A", "A", "", ""));
        Assert.False(TemplatePresentationRefreshPolicy.CanPaint(before, after, "A", "renamed", "", ""));
    }

    [Fact]
    public void Older_writer_retains_title_colors_lamp_and_opaque_legacy()
    {
        const string legacy = """{"retained":{"unknown":"value"}}""";
        var value = PosterPresentationPolicy.Defaults("lamp") with { Lamp = new(), TitleColors = new("#010203", "#040506") };
        var first = PosterPresentationPolicy.Serialize(value, legacy);
        var second = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), first);
        var read = PosterPresentationPolicy.Resolve(Poster(second, "lamp"));
        Assert.True(read.CanEdit); Assert.Equal(value.TitleColors, read.Parameters.TitleColors);
        Assert.Equal(value.Lamp, read.Parameters.Lamp);
        using var doc = JsonDocument.Parse(second); using var old = JsonDocument.Parse(legacy);
        Assert.True(JsonElement.DeepEquals(old.RootElement, doc.RootElement.GetProperty("legacy")));
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(Poster(first, "lamp"),
            Poster(PosterPresentationPolicy.Serialize(value with { TitleColors = null }, legacy), "lamp")));
    }

    [Theory]
    [InlineData("""{"schemaVersion":5}""")]
    [InlineData("""{"schemaVersion":5,"titleColors":{"text":"#123456"}}""")]
    [InlineData("""{"schemaVersion":5,"titleColors":{"text":"#123456","actions":"#00FFFFFF"}}""")]
    [InlineData("""{"schemaVersion":5,"titleColors":{"text":"#123456","actions":"#FFFFFF","extra":true}}""")]
    [InlineData("""{"schemaVersion":5,"titleColors":{"text":"#123456","text":"#FFFFFF","actions":"#FFFFFF"}}""")]
    [InlineData("""{"schemaVersion":5,"titleColors":null}""")]
    [InlineData("""{"schemaVersion":6,"titleColors":{"text":"#123456","actions":"#FFFFFF"}}""")]
    [InlineData("""{"schemaVersion":4,"titleColors":{"text":"#123456","actions":"#FFFFFF"}}""")]
    public void Malformed_unknown_or_wrong_version_is_read_only_and_never_rewritten(string json)
    {
        Assert.False(PosterPresentationPolicy.Resolve(Poster(json)).CanEdit);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json));
    }

    [Fact]
    public void Unchanged_legacy_versions_are_not_eagerly_upgraded()
    {
        var value = PosterPresentationPolicy.Defaults();
        Assert.Contains("\"schemaVersion\":1", PosterPresentationPolicy.Serialize(value));
        Assert.Contains("\"schemaVersion\":2", PosterPresentationPolicy.Serialize(value with { Preferences = new() }));
        Assert.Contains("\"schemaVersion\":4", PosterPresentationPolicy.Serialize(value with { Lamp = new() }));
    }
}

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Title_colors_preview_cancel_commit_reopen_and_other_edits_share_existing_transaction()
    {
        using var f = new Fixture(); var b = f.Add(); var original = f.Current;
        var session = new ControlCenterSettingsSession(); var colors = new PosterTitleColors("#123456", "#ABCDEF");
        Assert.True(session.Update(new(original.PostersById["A"], PosterSetting.TitleColors, TitleColors: colors)));
        Assert.Null(session.TakeNext()); Assert.Equal(original.Revision, f.Current.Revision);
        session.CancelPreview(); Assert.Null(session.Preview); Assert.Null(session.TakeNext());
        Assert.True(session.Update(new(original.PostersById["A"], PosterSetting.TitleColors, TitleColors: colors)));
        session.Finish(); var command = session.TakeNext()!;
        Assert.Single(command.ReadSet!.Posters);
        Assert.True(session.Complete(f.Coordinator.Execute(command)));
        Assert.Equal(original.Revision + 1, f.Current.Revision);
        Assert.Equal(original.PostersById[b].Poster, f.Current.PostersById[b].Poster);
        Assert.Equal(original.PostersById["A"].Poster.ContentLayoutJson, f.Current.PostersById["A"].Poster.ContentLayoutJson);
        f.Rename("A", "renamed");
        Assert.True(session.Update(new(f.Current.PostersById["A"], PosterSetting.Scale, 1.2)));
        session.Finish(); Assert.True(session.Complete(f.Coordinator.Execute(session.TakeNext()!)));
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(f.Store)).Refresh();
        Assert.Equal(colors, reopened.PostersById["A"].Presentation.Parameters.TitleColors);
        Assert.Equal("renamed", reopened.PostersById["A"].Poster.Title);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Title_colors_conflict_or_sql_failure_preserves_committed_state(bool sqlFailure)
    {
        using var f = new Fixture(); var session = new ControlCenterSettingsSession();
        Assert.True(session.Update(new(f.Current.PostersById["A"], PosterSetting.TitleColors, TitleColors: new("#123456", "#ABCDEF"))));
        session.Finish();
        if (sqlFailure) f.Sql("CREATE TRIGGER fail_ink BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected'); END;");
        else f.Rename("A", "external");
        var before = f.Current; var result = f.Coordinator.Execute(session.TakeNext()!);
        Assert.False(result.Succeeded); session.Complete(result);
        Assert.Equal(before.Revision, f.Coordinator.Refresh().Revision);
        Assert.Equal(before.PostersById["A"].Poster, f.Current.PostersById["A"].Poster);
        Assert.Null(session.TakeNext());
    }
}
