using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Fact]
    public void Empty_default_survives_restart_without_creating_fake_content_or_writing_state()
    {
        using var f = new Fixture(Seed());
        var before = f.Repository.Read();
        Assert.True(ContentLayoutPolicy.TryRead(null, "A", out var document, out _));
        Assert.Empty(document!.Placements);
        Assert.Empty(ContentSourceProjection.Read(ContentSourcePorts.Demo.Views, document));
        Assert.Empty(ContentLayoutSolver.Resolve(document, new(300, 240)).Items);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var after = new SqliteStateCommandRepository(reopened).Read();
        Assert.Equal(before.Revision, after.Revision);
        Assert.Null(after.Catalog.Posters[0].Poster.ContentLayoutJson);
        Assert.Empty(after.ContentCatalog.Items);
        Assert.Empty(after.ContentCatalog.PosterItems);
    }

    [Fact]
    public void Creating_another_poster_does_not_seed_six_cards()
    {
        using var f = new Fixture(Seed());
        var before = f.Coordinator.Refresh();
        var result = f.Coordinator.Execute(new CreatePosterCommand("empty-create", StateCommandSource.ControlCenter,
            before.Revision, StateReadSet.ForCatalog(before.CatalogVersion),
            PosterConfigurationFactory.NewBasicBox("新的收纳", "DISPLAY1")));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, f.Repository.Read().Catalog.Posters.Count);
        foreach (var entry in f.Repository.Read().Catalog.Posters)
        {
            Assert.True(ContentLayoutPolicy.TryRead(entry.Poster.ContentLayoutJson, entry.Poster.PosterId, out var doc, out _));
            Assert.Empty(doc!.Placements);
        }
    }

    [Fact]
    public void Empty_poster_accepts_layout_preferences_without_inventing_members()
    {
        using var f = new Fixture(Seed());
        var before = f.Coordinator.Refresh(); var entry = before.PostersById["A"];
        Assert.True(ContentLayoutPolicy.TryRead(null, "A", out var doc, out _));
        var desired = doc! with { DocumentVersion = doc.DocumentVersion + 1, ShowBaselineGrid = !doc.ShowBaselineGrid };
        var result = f.Coordinator.Execute(new ContentLayoutCommand("empty-guides", StateCommandSource.ControlCenter,
            before.Revision, "A", StateReadSet.ForPoster("A", entry.EntityVersion), doc.DocumentVersion, doc.ItemSetVersion, desired));
        Assert.True(result.Succeeded, result.Error);
        Assert.True(ContentLayoutPolicy.TryRead(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson, "A", out var after, out _));
        Assert.Empty(after!.Placements);
        Assert.Equal(desired.ShowBaselineGrid, after.ShowBaselineGrid);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Explicit_saved_layouts_are_not_erased_when_default_content_is_removed(bool realIdentity)
    {
        var layout = ContentLayoutSamples.Covers("A");
        if (realIdentity) layout = layout with { Placements = layout.Placements.Select((p, i) => p with { ItemId = "A:content-" + i }).ToArray() };
        string raw = ContentLayoutPolicy.Serialize(layout);
        Assert.True(ContentLayoutPolicy.TryRead(raw, "A", out var read, out _));
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(read!));
    }
}
