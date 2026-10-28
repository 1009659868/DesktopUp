using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterTransitionSurfaceTests
{
    private static DesktopPixelRect Pixels(SpatialRect r) => new(r.X, r.Y, r.Width, r.Height);

    [Theory]
    [InlineData(1)] [InlineData(6)] [InlineData(12)] [InlineData(64)]
    public void BothNestedOrdersNeverResizeAllocatedSurfacesWhileVisibleFramesRemainUnchanged(int pixels)
    {
        foreach (bool innerFirst in new[] { false, true })
        {
            var model = new SpatialContinuityTests.Model(SpatialContinuityTests.Chain());
            foreach (var op in new[] { ("C", false), ("B", false), (innerFirst ? "B" : "C", true), (innerFirst ? "C" : "B", true) })
            {
                var initial = model.Scene;
                var group = model.Apply(op.Item1, op.Item2).Group;
                var surfaces = group.Plan.Changes.ToDictionary(c => c.PosterId, c =>
                    PosterTransitionSurface.Create(Pixels(initial.Posters.Single(p => p.PosterId == c.PosterId).Bounds), Pixels(c.After)));
                var schedule = SpatialAnimationSchedule.Create(initial, group.Frames, new(MaximumStepPixels: pixels));
                foreach (var frame in schedule.Frames)
                    foreach (var change in frame.Changes)
                    {
                        var visible = Pixels(change.To); var allocation = surfaces[change.PosterId];
                        var surface = allocation.At(visible);
                        Assert.Equal(visible.X, surface.X); Assert.Equal(visible.Y, surface.Y);
                        Assert.Equal(allocation.Height, surface.Height); Assert.Equal(visible.Width, surface.Width);
                        Assert.True(surface.Height >= visible.Height);
                        Assert.Equal(change.To, new SpatialRect(visible.X, visible.Y, visible.Width, visible.Height));
                    }
                SpatialContinuityTests.AssertSafety(initial, group with { Frames = schedule.Frames }, op.Item1);
            }
        }
    }

    [Fact]
    public void UpwardRevealCanReserveOutsideWorkspaceButOnlyVisibleBoundsParticipateInSpace()
    {
        var compact = new DesktopPixelRect(-500, 940, 300, 60);
        var expanded = new DesktopPixelRect(-500, 640, 300, 360);
        var allocation = PosterTransitionSurface.Create(compact, expanded);
        Assert.Equal(1300, allocation.At(compact).Bottom);
        Assert.Equal(1000, compact.Bottom);
        for (int h = 60; h <= 360; h++)
        {
            var visible = compact with { Y = 1000 - h, Height = h };
            Assert.Equal(360, allocation.At(visible).Height);
            Assert.Equal(1000, visible.Bottom);
        }
        Assert.Equal(expanded, allocation.At(expanded));
    }

    [Fact]
    public void EffectiveHeightNotPreferredHeightDefinesCapacityAndInvalidFramesFailClosed()
    {
        var compact = new DesktopPixelRect(0, 0, 300, 60);
        var allocation = PosterTransitionSurface.Create(compact, compact with { Height = 210 });
        Assert.Equal(210, allocation.Height);
        Assert.Throws<ArgumentException>(() => allocation.At(compact with { Height = 211 }));
        Assert.Throws<ArgumentException>(() => allocation.At(compact with { Width = 301 }));
        Assert.Throws<ArgumentException>(() => allocation.At(compact with { Height = 0 }));
        Assert.Throws<ArgumentException>(() => PosterTransitionSurface.Create(compact, compact with { Width = 0 }));
        Assert.Throws<ArgumentException>(() => PosterTransitionSurface.Create(compact, compact with { Width = 301 }));
    }
}
