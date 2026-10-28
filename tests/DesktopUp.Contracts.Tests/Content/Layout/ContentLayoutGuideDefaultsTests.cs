using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ContentLayoutPersistenceTests
{
    [Fact]
    public void New_poster_guides_are_off_without_a_read_time_database_write()
    {
        using var f = new Fixture(Seed());
        var before = f.Repository.Read();
        var poster = before.Catalog.Posters[0].Poster;
        Assert.Null(poster.ContentLayoutJson);
        Assert.True(ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var layout, out var error), error);
        Assert.False(layout!.ShowBaselineGrid);
        Assert.False(layout.ShowSpanLabels);
        Assert.Equal(before.Revision, f.Repository.Read().Revision);
        Assert.Null(f.Repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var saved = new SqlitePosterRepository(reopened).Read("A")!.Poster;
        Assert.True(ContentLayoutPolicy.TryRead(saved.ContentLayoutJson, "A", out var restored, out error), error);
        Assert.False(restored!.ShowBaselineGrid);
        Assert.False(restored.ShowSpanLabels);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Explicit_existing_guide_preferences_survive_restart_and_clone(bool grid, bool spans)
    {
        var original = ContentLayoutSamples.Equal("A") with { ShowBaselineGrid = grid, ShowSpanLabels = spans };
        string raw = ContentLayoutPolicy.Serialize(original);
        using var f = new Fixture(seed: Seed() with { ContentLayoutJson = raw });
        var before = f.Coordinator.Refresh();
        var entry = before.PostersById["A"];
        var result = f.Coordinator.Execute(new ClonePosterCommand("clone-guide-preferences", StateCommandSource.ControlCenter,
            before.Revision, StateReadSet.ForPoster("A", entry.EntityVersion) with { CatalogVersion = before.CatalogVersion }, "A"));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(raw, f.Repository.Read().Catalog.Posters.Single(p => p.Poster.PosterId == "A").Poster.ContentLayoutJson);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var savedPosters = new SqliteStateCommandRepository(reopened).Read().Catalog.Posters;
        Assert.Equal(2, savedPosters.Count);
        foreach (var saved in savedPosters)
        {
            Assert.True(ContentLayoutPolicy.TryRead(saved.Poster.ContentLayoutJson, saved.Poster.PosterId, out var layout, out var error), error);
            Assert.Equal(grid, layout!.ShowBaselineGrid);
            Assert.Equal(spans, layout.ShowSpanLabels);
        }
    }
}
