using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.R7PortableFidelityTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentHeadingTests
{
    [Theory]
    [InlineData("night-ops", true)] [InlineData("lamp", true)]
    public void Heading_is_template_scoped_and_releases_metrics_without_changing_content(string template, bool baseline)
    {
        var poster = Poster() with { TemplateKey = template, LayoutParametersJson = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(template)) };
        var before = PosterPresentationPolicy.Resolve(poster);
        Assert.Equal(baseline, before.IsContentHeadingVisible);
        var parameters = before.Parameters with { HeadingVisibility = new TemplateHeadingVisibility([]).Set(template, false) };
        var json = PosterPresentationPolicy.Serialize(parameters, poster.LayoutParametersJson);
        var after = PosterPresentationPolicy.Resolve(poster with { LayoutParametersJson = json });
        Assert.True(after.CanEdit, after.Warning); Assert.False(after.IsContentHeadingVisible); Assert.Equal(0, after.MetaHeightDip);
        Assert.Equal(before.Metrics.ReservedTopDip - before.MetaHeightDip, after.Metrics.ReservedTopDip);
        Assert.Equal(before.Parameters.Content, after.Parameters.Content); Assert.Equal(before.Parameters.IsTitleVisible, after.Parameters.IsTitleVisible);
        Assert.Equal(before.HeaderHeightDip, after.HeaderHeightDip);
        Assert.False(TemplatePresentationRefreshPolicy.CanPaint(before, after, "a", "a", "b", "b"));
        Assert.Null(PosterPresentationPolicy.ValidateEdit(poster, poster with { LayoutParametersJson = json }));
        var roundtrip = PosterPresentationPolicy.ReadStoredParameters(json, template);
        Assert.Equal(parameters, roundtrip);
        var shown = PosterPresentationPolicy.Resolve(poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(
            roundtrip with { HeadingVisibility = roundtrip.HeadingVisibility!.Set(template, true) }, json) });
        Assert.True(shown.IsContentHeadingVisible); Assert.True(shown.MetaHeightDip > 0);
        if (template != "night-ops") Assert.True(PosterPresentationPolicy.Resolve(poster with { TemplateKey = "night-ops", LayoutParametersJson = json }).IsContentHeadingVisible);
    }

    [Fact]
    public void Basic_storage_does_not_mount_heading_even_with_an_old_true_override()
    {
        var settings = PosterPresentationPolicy.Defaults("basic-box") with {
            HeadingVisibility = new TemplateHeadingVisibility([]).Set("basic-box", true) };
        var poster = Poster() with { TemplateKey = "basic-box", LayoutParametersJson = PosterPresentationPolicy.Serialize(settings) };
        var resolved = PosterPresentationPolicy.Resolve(poster);
        Assert.True(resolved.CanEdit); Assert.False(resolved.CanShowContentHeading);
        Assert.False(resolved.IsContentHeadingVisible); Assert.Equal(0, resolved.MetaHeightDip);
        Assert.True(resolved.Parameters.HeadingVisibility!.Values["basic-box"]);
        Assert.Throws<ArgumentException>(() => new PosterSettingEdit(new(1, 1, poster, true, true, true),
            PosterSetting.ContentHeadingVisibility, 1).Command("unsupported"));
    }

    [Fact]
    public void Old_writers_retain_overrides_and_legacy_and_cannot_erase_one_template()
    {
        var settings = PosterPresentationPolicy.Defaults() with { HeadingVisibility = new TemplateHeadingVisibility([]).Set("lamp", false).Set("night-ops", true) };
        var json = PosterPresentationPolicy.Serialize(settings, "{\"retained\":\"raw\"}");
        var saved = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json);
        var read = PosterPresentationPolicy.ReadStoredParameters(saved, "lamp");
        Assert.Equal(settings.HeadingVisibility, read.HeadingVisibility);
        using var parsed = JsonDocument.Parse(saved);
        Assert.Equal(7, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("raw", parsed.RootElement.GetProperty("legacy").GetProperty("retained").GetString());
        var before = Poster() with { LayoutParametersJson = json };
        var desired = before with { LayoutParametersJson = PosterPresentationPolicy.Serialize(settings with { HeadingVisibility = new TemplateHeadingVisibility([]).Set("lamp", true) }, json) };
        Assert.NotNull(PosterPresentationPolicy.ValidateEdit(before, desired));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":7}")]
    [InlineData("{\"schemaVersion\":7,\"headingVisibility\":null}")]
    [InlineData("{\"schemaVersion\":7,\"headingVisibility\":{\"lamp\":1}}")]
    [InlineData("{\"schemaVersion\":7,\"headingVisibility\":{\"lamp\":false,\"lamp\":true}}")]
    [InlineData("{\"schemaVersion\":7,\"headingVisibility\":{\"../lamp\":false}}")]
    [InlineData("{\"schemaVersion\":6,\"headingVisibility\":{\"lamp\":false},\"showTitle\":true}")]
    [InlineData("{\"schemaVersion\":8,\"headingVisibility\":{\"lamp\":false}}")]
    public void Invalid_or_future_heading_payloads_are_read_only(string json)
    {
        Assert.False(PosterPresentationPolicy.Resolve(Poster() with { LayoutParametersJson = json }).CanEdit);
        Assert.Throws<ArgumentException>(() => PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(), json));
    }

    [Fact]
    public void Recipe_heading_default_and_cloud_title_remain_distinct()
    {
        var recipe = Recipe() with { Base = Recipe().Base with { Heading = false } };
        var presentation = PosterPresentationPolicy.Resolve(Poster(recipe));
        Assert.False(presentation.IsContentHeadingVisible); Assert.Equal(0, presentation.MetaHeightDip);
        var rain = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "rain");
        var parameters = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults(), rain) with {
            HeadingVisibility = new TemplateHeadingVisibility([]).Set("rain", true) };
        var cloud = PosterPresentationPolicy.Resolve(Poster() with { TemplateKey = "rain", LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) });
        Assert.False(cloud.CanShowContentHeading); Assert.Equal(0, cloud.MetaHeightDip);
        Assert.Equal(PosterTitleChromePolicy.ExpandedCloudHeightDip, cloud.HeaderHeightDip);
    }

    [Fact]
    public void Portable_conflict_remaps_heading_with_template_not_to_local_original()
    {
        var imported = Recipe(); var local = Recipe(color: "#FFFFFF");
        var poster = Poster(imported);
        poster = poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(
            PosterPresentationPolicy.Resolve(poster).Parameters with { HeadingVisibility = new TemplateHeadingVisibility([]).Set(imported.Id, false).Set("night-ops", false) }, poster.LayoutParametersJson) };
        var doc = Document(State([poster], Catalog(imported)));
        var current = State([Poster(local)], Catalog(local));
        var plan = PortableProfilePlanner.Plan(doc, current, Topology(), PortableImportMode.Append, false, () => "new");
        var created = Assert.Single(plan.Creates); Assert.NotEqual(imported.Id, created.TemplateKey);
        var read = PosterPresentationPolicy.Resolve(created);
        Assert.False(read.IsContentHeadingVisible);
        Assert.False(read.Parameters.HeadingVisibility!.Values.ContainsKey(imported.Id));
        Assert.False(read.Parameters.HeadingVisibility.Values[created.TemplateKey]);
        Assert.False(read.Parameters.HeadingVisibility.Values["night-ops"]);
        Assert.Null(PosterPresentationPolicy.Resolve(current.Catalog.Posters[0].Poster).Parameters.HeadingVisibility);
    }
}

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Content_heading_cancel_commit_template_switch_and_cold_reopen_keep_scoped_intent()
    {
        using var f = new Fixture(); var b = f.Add();
        Assert.True(f.Coordinator.Execute(new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Template, Text: "night-ops").Command("mount-heading")).Succeeded);
        var before = f.Current;
        var session = new ControlCenterSettingsSession();
        Assert.True(session.Update(new(before.PostersById["A"], PosterSetting.ContentHeadingVisibility, 0)));
        session.CancelPreview(); Assert.Null(session.TakeNext()); Assert.Equal(before.Revision, f.Current.Revision);
        Assert.True(session.Update(new(before.PostersById["A"], PosterSetting.ContentHeadingVisibility, 0)));
        session.Finish(); var command = session.TakeNext()!;
        var result = f.Coordinator.Execute(command); Assert.True(result.Succeeded, result.Error); Assert.True(session.Complete(result));
        Assert.Equal(before.Revision + 1, f.Current.Revision);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        var after = f.Current.PostersById["A"].Poster;
        Assert.Equal(before.PostersById["A"].Poster with { LayoutParametersJson = after.LayoutParametersJson }, after);
        Assert.Equal(before.PostersById[b].Poster, f.Current.PostersById[b].Poster);
        foreach (string template in new[] { "lamp", after.TemplateKey })
        {
            Assert.True(session.Update(new(f.Current.PostersById["A"], PosterSetting.Template, Text: template)));
            session.Finish(); Assert.True(session.Complete(f.Coordinator.Execute(session.TakeNext()!)));
        }
        using var store = new SqliteStateStore(f.Paths); Assert.True(store.Initialize().Succeeded);
        var reopened = new StateProjectionCoordinator(new SqliteStateCommandRepository(store)).Refresh();
        Assert.False(reopened.PostersById["A"].Presentation.IsContentHeadingVisible);
        Assert.Equal(before.PostersById[b].Poster, reopened.PostersById[b].Poster);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Content_heading_conflict_or_sql_failure_retains_original(bool sqlFailure)
    {
        using var f = new Fixture();
        Assert.True(f.Coordinator.Execute(new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Template, Text: "night-ops").Command("mount-heading")).Succeeded);
        var command = new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.ContentHeadingVisibility, 0).Command("heading-failure");
        if (sqlFailure) f.Sql("CREATE TRIGGER fail_heading BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected'); END;");
        else f.Rename("A", "external");
        var before = f.Current;
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before.PostersById["A"].Poster, f.Coordinator.Refresh().PostersById["A"].Poster);
        Assert.Equal(before.Revision, f.Current.Revision);
    }
}
