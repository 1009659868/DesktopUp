using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterGeometryTransitionSurfaceTests
{
    [Theory]
    [InlineData(540, 720, 240, 720, 96)]
    [InlineData(240, 720, 540, 720, 96)]
    [InlineData(540, 720, 240, 360, 96)]
    [InlineData(240, 360, 540, 720, 96)]
    [InlineData(540, 360, 240, 720, 144)]
    [InlineData(240, 720, 540, 360, 144)]
    public void ActualScheduleKeepsNativeCapacityWhileBothLogicalAxesAndClipFollowEachFrame(
        int fromWidth, int fromHeight, int toWidth, int toHeight, int dpi)
    {
        Assert.True(SpatialPixelProjection.TryProjectLocalDip(0, 0, fromWidth, fromHeight,
            -900, -120, (uint)dpi, (uint)dpi, out var before));
        Assert.True(SpatialPixelProjection.TryProjectLocalDip(0, 0, toWidth, toHeight,
            -900, -120, (uint)dpi, (uint)dpi, out var after));
        var allocation = PosterGeometryTransitionSurface.Create(Pixels(before), Pixels(after));
        var schedule = Schedule(before, after);
        Assert.NotEmpty(schedule.Frames);
        foreach (var frame in schedule.Frames)
        {
            var visible = Pixels(Assert.Single(frame.Changes).To);
            var surface = allocation.At(visible);
            Assert.Equal(Math.Max(before.Width, after.Width), surface.Width);
            Assert.Equal(Math.Max(before.Height, after.Height), surface.Height);
            Assert.Equal(visible.X, surface.X); Assert.Equal(visible.Y, surface.Y);
            var readback = DesktopVisualSurfaceReadback.Compare(surface, visible, surface,
                new(0, 0, surface.Width, surface.Height), new(0, 0, visible.Width, visible.Height), 3, 3);
            Assert.True(readback.Verified, readback.Failure);
            if (visible.Width != surface.Width || visible.Height != surface.Height)
                Assert.False(DesktopVisualSurfaceReadback.Compare(surface, visible, surface,
                    new(0, 0, surface.Width, surface.Height), new(0, 0, surface.Width, surface.Height), 3, 3).Verified);
        }
        Assert.Equal(after, Assert.Single(schedule.Frames[^1].Changes).To);
        // Cancellation restores the original visible frame inside this allocation before release.
        Assert.Equal(allocation.Width, allocation.At(Pixels(before)).Width);
        Assert.Equal(allocation.Height, allocation.At(Pixels(before)).Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualWideNarrowRoundTripAndMidwayCancelPreserveTheCompleteAuthorPicture(bool changeHeight)
    {
        var wide = new SpatialRect(-500, 30, 540, 720);
        var narrow = wide with { Width = 240, Height = changeHeight ? 420 : 720 };
        var automatic = ContentLayoutSamples.Equal("geometry", 100);
        var entered = ContentLayoutEditing.Apply(automatic,
            new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }, Viewport(wide));
        Assert.True(entered.Accepted, entered.Error);
        var author = entered.Document;
        string raw = ContentLayoutPolicy.Serialize(author);
        var original = ContentLayoutSolver.Resolve(author, Viewport(wide));
        var narrowPicture = ContentLayoutSolver.Resolve(author, Viewport(narrow));
        Assert.NotEqual(original.Items[1].Bounds, narrowPicture.Items[1].Bounds);
        var down = Schedule(wide, narrow);
        var up = Schedule(narrow, wide);
        foreach (var leg in down.Frames.Concat(up.Frames))
        {
            var visible = Assert.Single(leg.Changes).To;
            var snapshot = ContentLayoutSolver.Resolve(author, Viewport(visible));
            Assert.Equal(ContentLayoutSpaceStatus.Normal, snapshot.SpaceStatus);
            Assert.Equal(100, snapshot.Items.Count);
            Assert.Equal(author.Placements.Select(p => p.ItemId).Order(), snapshot.Items.Select(p => p.ItemId).Order());
            foreach (var item in snapshot.Items)
            {
                var source = author.Placements.Single(p => p.ItemId == item.ItemId);
                Assert.Equal(source.ColumnSpan, item.ColumnSpan); Assert.Equal(source.RowSpan, item.RowSpan);
                if (!item.Clip.IsEmpty)
                {
                    Assert.True(item.Clip.X >= 0 && item.Clip.Y >= 0);
                    Assert.True(item.Clip.Right <= Viewport(visible).WidthDip + .001);
                    Assert.True(item.Clip.Bottom <= Viewport(visible).HeightDip + .001);
                }
            }
            Assert.Equal(raw, ContentLayoutPolicy.Serialize(author));
        }
        // A failed native frame must re-resolve the committed picture, not retain the narrow projection.
        var interrupted = Assert.Single(down.Frames[down.Frames.Count / 2].Changes).To;
        _ = ContentLayoutSolver.Resolve(author, Viewport(interrupted));
        var restored = ContentLayoutSolver.Resolve(author, Viewport(wide));
        Assert.Equal(original.Items.ToArray(), restored.Items.ToArray());
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(author));
    }

    [Fact]
    public void BadVisibleFramesFailClosedAndPostureStillRequiresEqualWidth()
    {
        var wide = new DesktopPixelRect(10, 20, 540, 720);
        var narrow = wide with { Width = 240, Height = 360 };
        var allocation = PosterGeometryTransitionSurface.Create(wide, narrow);
        Assert.Throws<ArgumentException>(() => allocation.At(wide with { Width = 541 }));
        Assert.Throws<ArgumentException>(() => allocation.At(wide with { Height = 721 }));
        Assert.Throws<ArgumentException>(() => allocation.At(wide with { Width = 0 }));
        Assert.Throws<ArgumentException>(() => PosterGeometryTransitionSurface.Create(wide, narrow with { Height = 0 }));
        Assert.Throws<ArgumentException>(() => PosterTransitionSurface.Create(wide, narrow));
        Assert.Throws<ArgumentException>(() => PosterTransitionSurface.Create(narrow, wide));
    }

    private static SpatialAnimationSchedule Schedule(SpatialRect before, SpatialRect after)
    {
        var environment = new SpatialEnvironment("test", new(-2000, -2000, 4000, 4000), 96, 96, 1, 1, Guid.NewGuid());
        var scene = new SpatialSnapshot(environment, [new("geometry", before, before.Height, true, false, false, 1)]);
        return SpatialAnimationSchedule.Create(scene, [new([new("geometry", before, after)])], new(MaximumStepPixels: 12));
    }

    // Fixed test chrome insets exercise real content capacity without reproducing a layout algorithm.
    private static ContentLayoutViewport Viewport(SpatialRect frame) => new(frame.Width - 56, frame.Height - 98);
    private static DesktopPixelRect Pixels(SpatialRect r) => new(r.X, r.Y, r.Width, r.Height);
}
