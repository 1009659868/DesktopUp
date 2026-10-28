using DesktopUp.Application;
using Xunit;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterTransitionViewportTests
{
    [Theory]
    [InlineData(60, 0, 60)]
    [InlineData(70, 10, 60)]
    [InlineData(90, 30, 60)]
    [InlineData(102, 42, 60)]
    [InlineData(220, 42, 178)]
    [InlineData(360, 42, 318)]
    public void VisibleFooterStaysAtCurrentBottomWithoutResizingContent(double height, double visible, double top)
    {
        var actual = PosterTransitionViewport.Resolve(height, 60, 42, false);
        Assert.Equal(visible, actual.FooterVisibleHeight); Assert.Equal(top, actual.FooterTop);
        Assert.Equal(height, actual.FooterTop + actual.FooterVisibleHeight);
        Assert.Equal(actual.FooterTop, actual.BodyBottom);
    }

    [Fact]
    public void CollapsedPeersNeverShowPartialFooterAndTraversalHasNoDrift()
    {
        foreach (double scale in new[] { 1, 1.25, 1.5, 2 })
        {
            var sequence = Enumerable.Range(60, 301).Select(h => h / scale).ToArray();
            foreach (double height in sequence.Concat(sequence.Reverse()))
            {
                var collapsed = PosterTransitionViewport.Resolve(height, 60 / scale, 42 / scale, true);
                Assert.Equal(height, collapsed.BodyBottom); Assert.Equal(0, collapsed.FooterVisibleHeight);
                var open = PosterTransitionViewport.Resolve(height, 60 / scale, 42 / scale, false);
                Assert.InRange(open.BodyBottom, 0, height);
                Assert.Equal(height, open.FooterTop + open.FooterVisibleHeight, 8);
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidGeometryIsRejected(double height) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        PosterTransitionViewport.Resolve(height, 60, 42, false));
}
