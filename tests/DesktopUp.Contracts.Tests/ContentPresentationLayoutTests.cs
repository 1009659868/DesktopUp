using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentPresentationLayoutTests
{
    private static readonly DisplayMonitorSnapshot Monitor = new("display", true,
        new(0, 0, 1400, 1000), new(0, 0, 1400, 1000), 96, 96);
    private static readonly ResolvedPosterPresentation Theme = new("basic-box", PosterPresentationPolicy.Defaults(),
        PosterContentMetrics.Current, true, true, null);
    private static ContentPresentationFrame Prepare(ContentPresentationLayout layout, ContentLayoutDocument doc,
        ContentLayoutViewport view) => layout.Prepare(doc, view, null, false, false, false, true);

    [Fact]
    public void IdenticalPreparationReusesFrameAndScrollOnlyReclips()
    {
        var owner = new ContentPresentationLayout();
        var doc = ContentLayoutSamples.Equal("p", 8);
        string original = ContentLayoutPolicy.Serialize(doc);
        var first = Prepare(owner, doc, new(300, 200));
        Assert.Same(first, Prepare(owner, doc, first.Viewport));
        var scrolled = Prepare(owner, doc, first.Viewport with { ScrollYDip = 90 });
        Assert.True(scrolled.Snapshot.ViewportGeneration > first.Snapshot.ViewportGeneration);
        Assert.Equal(first.Snapshot.Items.Select(x => x.Bounds), scrolled.Snapshot.Items.Select(x => x.Bounds));
        owner.Adopt(scrolled);
        Assert.Same(scrolled, Prepare(owner, doc, scrolled.Viewport));
        owner.Invalidate();
        Assert.NotSame(scrolled, Prepare(owner, doc, scrolled.Viewport));
        Assert.Equal(original, ContentLayoutPolicy.Serialize(doc));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void TilesClipsAndHitRegionsUseOneFrame(ContentFlowAxis axis)
    {
        var owner = new ContentPresentationLayout();
        var doc = ContentLayoutSamples.Equal("p", 8) with { FlowAxis = axis, LoopScroll = true };
        var frame = Prepare(owner, doc, new(300, 200, 75, 75));
        var input = ContentInputGeometry.Resolve(1, new(0, 0, 360, 300),
            new(Monitor, new(0, 0, 360, 300, false, false, 1), Theme, default, false, false,
                null, frame, 28, 60, false, 0));
        Assert.Equal(frame.Snapshot.ViewportGeneration, input.ContentSnapshotGeneration);
        var tiles = ContentLayoutFlow.ProjectTiles(frame.Snapshot, frame.Viewport, frame.BrowseMode)
            .Where(t => !t.Clip.IsEmpty).ToArray();
        var hits = input.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem).ToArray();
        Assert.Equal(tiles.Length, hits.Length);
        for (int i = 0; i < tiles.Length; i++)
        {
            Assert.Equal(tiles[i].ItemId, hits[i].ContentItemId);
            Assert.Equal(frame.Snapshot.ViewportGeneration, hits[i].ContentSnapshotGeneration);
            Assert.Equal((int)Math.Round(28 + tiles[i].Clip.X), hits[i].Bounds.X);
            Assert.Equal((int)Math.Round(60 + tiles[i].Clip.Y), hits[i].Bounds.Y);
        }
        Assert.Contains(input.Regions, r => r.Kind == PosterInputTargetKind.ContentViewport);
        Assert.Contains(input.Regions, r => r.Kind == PosterInputTargetKind.MoreButton);
    }

    [Fact]
    public void PreparationAndFailedNativePublicationDoNotAdvanceSpace()
    {
        var owner = new ContentPresentationLayout();
        var doc = ContentLayoutSamples.Equal("p", 2);
        var env = new SpatialEnvironment("display", new(0, 0, 1400, 1000), 96, 96, 1, 1, Guid.NewGuid());
        var surface = new ContentLayoutSurface(Guid.NewGuid(), 1, env, new(0, 0, 600, 600),
            "metrics", true, true, 600, 600);
        var first = owner.Prepare(doc, new(500, 400), surface, false, false, false, true);
        Assert.Null(owner.Space.Published);
        Assert.True(owner.Space.TryPublish(first.Space, surface.Witness(first.Snapshot, first.Viewport), true));
        var next = owner.Prepare(doc, first.Viewport with { WidthDip = 550 }, surface, false, false, false, true);
        Assert.False(owner.Space.TryPublish(next.Space, surface.Witness(next.Snapshot, next.Viewport), false));
        Assert.Same(first.Space, owner.Space.Published);
        Assert.False(owner.Space.TryPublish(next.Space, surface.Witness(first.Snapshot, first.Viewport), true));
        Assert.True(owner.Space.TryPublish(next.Space, surface.Witness(next.Snapshot, next.Viewport), true));
    }

    [Fact]
    public void InvalidProjectionKeepsAuthorAndProducesReadOnlyEmptyFallback()
    {
        var owner = new ContentPresentationLayout();
        var doc = ContentLayoutSamples.Equal("p", 1);
        var bad = doc with { Placements = [doc.Placements[0] with { ColumnSpan = -1 }] };
        var frame = Prepare(owner, bad, new(300, 200));
        Assert.NotNull(frame.Fault);
        Assert.Empty(frame.Snapshot.Items);
        Assert.Same(bad, frame.Document);
        Assert.Equal(-1, bad.Placements[0].ColumnSpan);
        Assert.Null(Prepare(owner, doc, frame.Viewport).Fault);
    }
}
