using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentResourceDemandTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical, false)] [InlineData(ContentFlowAxis.Vertical, true)]
    [InlineData(ContentFlowAxis.HorizontalLoop, false)] [InlineData(ContentFlowAxis.HorizontalLoop, true)]
    public void DemandFollowsActualTilesAtBothEndsAndIsDeterministic(ContentFlowAxis axis, bool loop)
    {
        var doc = ContentLayoutSamples.Equal("assets", 1000) with { FlowAxis = axis, LoopScroll = loop };
        var source = new Source();
        foreach (double offset in new[] { 0, 1, 100, 11999, -1, 1e6 })
        {
            var viewport = new ContentLayoutViewport(340, 300, ScrollXDip: offset, ScrollYDip: offset);
            var snapshot = ContentLayoutSolver.Resolve(doc, viewport);
            var views = source.Read(doc);
            var demand = ContentAssetDemand.Build(doc, snapshot, viewport, ContentBrowseMode.SingleAxis, views, 1, 120, 144, 1, 1);
            var expected = ContentLayoutFlow.ProjectTiles(snapshot, viewport, ContentBrowseMode.SingleAxis, snapshot.CellDip + snapshot.GapDip)
                .OrderBy(t => t.Clip.IsEmpty).ThenBy(t => t.ReadOrder).ThenBy(t => t.LoopIndex)
                .Select(t => t.ItemId).Distinct().Take(64);
            Assert.Equal(expected, demand.Select(k => k.ItemId)); Assert.InRange(demand.Count, 1, 64);
            Assert.Equal(demand, ContentAssetDemand.Build(doc, snapshot, viewport, ContentBrowseMode.SingleAxis, views, 1, 120, 144, 1, 1));
            Assert.All(ContentLayoutFlow.ProjectTiles(snapshot, viewport), t => Assert.Contains(demand, k => k.ItemId == t.ItemId));
        }
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void SeamCopiesShareOneKeyAndPreloadDoesNotChangeLoopEligibility(ContentFlowAxis axis)
    {
        var doc = ContentLayoutSamples.Equal("assets", 6) with { FlowAxis = axis, LoopScroll = true };
        var initial = new ContentLayoutViewport(250, 250);
        var basis = ContentLayoutSolver.Resolve(doc, initial);
        double extent = axis == ContentFlowAxis.Vertical ? basis.ExtentHeightDip : basis.ExtentWidthDip;
        var view = axis == ContentFlowAxis.Vertical ? initial with { HeightDip = extent - 1, ScrollYDip = 1 }
            : initial with { WidthDip = extent - 1, ScrollXDip = 1 };
        var snapshot = ContentLayoutSolver.Resolve(doc, view);
        Assert.True(ContentLayoutFlow.IsLooping(snapshot, view));
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, view, ContentBrowseMode.SingleAxis, snapshot.CellDip + snapshot.GapDip);
        Assert.Contains(tiles.GroupBy(t => t.ItemId), group => group.Count() > 1);
        var keys = ContentAssetDemand.Build(doc, snapshot, view, ContentBrowseMode.SingleAxis, new Source().Read(doc), 1, 96, 96, 1, 1);
        Assert.Equal(keys.Count, keys.Select(k => k.ItemId).Distinct().Count());
        Assert.All(ContentLayoutFlow.ProjectTiles(snapshot, view), tile => Assert.Contains(tile, tiles));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void RestrictedTwoAxisTailStillHasAssetsAndFullPixelDimensions(ContentFlowAxis axis)
    {
        var doc = ContentLayoutSamples.Equal("assets", 100) with { FlowAxis = axis, LoopScroll = true };
        var view = new ContentLayoutViewport(40, 40, ScrollXDip: 10000, ScrollYDip: 10000);
        var snapshot = ContentLayoutSolver.Resolve(doc, view);
        var keys = ContentAssetDemand.Build(doc, snapshot, view, ContentBrowseMode.ReadOnlyOverflow, new Source().Read(doc), 2, 144, 120, 3, 4);
        Assert.NotEmpty(keys);
        Assert.All(ContentLayoutFlow.ProjectTiles(snapshot, view, ContentBrowseMode.ReadOnlyOverflow), t =>
        {
            Assert.Equal(0, t.LoopIndex);
            var key = Assert.Single(keys, k => k.ItemId == t.ItemId);
            Assert.Equal((int)Math.Ceiling(t.Bounds.Width * 1.5), key.PixelWidth);
            Assert.Equal((int)Math.Ceiling(t.Bounds.Height * 1.25), key.PixelHeight);
            Assert.Equal(4, key.SourceVersion);
        });
    }

    [Fact]
    public void ResourceBudgetDoesNotReduceVisibleContentAndVisibleOutranksOverscan()
    {
        var doc = ContentLayoutSamples.Equal("assets", 100);
        var view = new ContentLayoutViewport(400, 500, ScrollYDip: 400);
        var snapshot = ContentLayoutSolver.Resolve(doc, view);
        var visible = ContentLayoutFlow.ProjectTiles(snapshot, view);
        var keys = ContentAssetDemand.Build(doc, snapshot, view, ContentBrowseMode.SingleAxis, new Source().Read(doc), 1, 96, 96, 1, 1, 1);
        Assert.Single(keys); Assert.True(visible.Count > 1);
        Assert.Contains(visible, t => t.ItemId == keys[0].ItemId);
        Assert.Throws<ContentLayoutException>(() => ContentLayoutFlow.ProjectTiles(snapshot, view, overscanDip: 10000));
    }

    [Fact]
    public void SourceOrderMissingAvailabilityAndVersionNeverAuthorPlacements()
    {
        var doc = ContentLayoutSamples.Equal("assets", 6);
        string raw = ContentLayoutPolicy.Serialize(doc);
        var source = new Source();
        var reversed = ContentSourceProjection.Read(source, doc, 1);
        Assert.Equal(doc.Placements.Select(p => p.ItemId), reversed.Select(p => p.ItemId));
        Assert.All(ContentSourceProjection.Read(source, doc, 2), v => Assert.False(v.IsAvailable));
        source.Bad = true;
        var missing = ContentSourceProjection.Read(source, doc, 1);
        Assert.Equal(6, missing.Count); Assert.All(missing, v => Assert.False(v.IsAvailable));
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(doc));
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("throw")] [InlineData("version-race")]
    [InlineData("name")] [InlineData("monogram")] [InlineData("oversized")]
    public void UntrustworthyMetadataCannotChangeMembershipOrReturnAnOpenableTarget(string failure)
    {
        var doc = ContentLayoutSamples.Equal("assets", 6);
        var source = new FaultSource(failure);
        var views = ContentSourceProjection.Read(source, doc, 1);
        Assert.Equal(doc.Placements.Select(p => p.ItemId), views.Select(p => p.ItemId));
        Assert.All(views, v => Assert.False(v.IsAvailable));
    }

    private sealed class FaultSource(string failure) : IContentItemViewSource
    {
        public long Version { get; private set; } = 1;
        public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument doc)
        {
            if (failure == "throw") throw new InvalidOperationException("Controlled adapter failure.");
            if (failure == "version-race") Version++;
            var valid = doc.Placements.Select(p => new ContentItemView(p.ItemId,
                failure == "name" ? "bad\nname" : "Name", failure == "monogram" ? new string('X', 17) : "X", 0)).ToArray();
            return failure switch
            {
                "duplicate" => valid.Concat(valid).ToArray(),
                "oversized" => Enumerable.Repeat(valid[0], 1001).ToArray(),
                _ => valid
            };
        }
    }

    private sealed class Source : IContentItemViewSource
    {
        public long Version => 1;
        public bool Bad;
        public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument doc) => Bad
            ? [new("foreign:0", "foreign", "X", 0)]
            : doc.Placements.Reverse().Select(p => new ContentItemView(p.ItemId, p.ItemId, "X", 0, "same-source")).ToArray();
    }
}
