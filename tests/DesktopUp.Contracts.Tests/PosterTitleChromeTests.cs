using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterTitleChromeTests
{
    [Theory]
    [InlineData(240, true)] [InlineData(240, false)]
    [InlineData(300, true)] [InlineData(300, false)]
    [InlineData(600, true)] [InlineData(600, false)]
    public void CloudSlotsStayInsideStableGripAndSeparateFromName(double width, bool collapsed)
    {
        var value = PosterTitleChromePolicy.Resolve(width, collapsed, PosterTitleChromeKind.Cloud);
        Assert.Equal(collapsed ? 60 : 84, value.Height);
        Assert.True(value.Name.Width >= 140);
        Assert.True(value.Name.Right < value.Collapse.X && value.Collapse.Right < value.More.X);
        foreach (var r in new[] { value.Name, value.Collapse, value.More })
        {
            Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= width && r.Bottom <= value.Height);
            if (r == value.Name) Assert.Equal(PosterTitleChromeTarget.Drag, value.HitTest(r.X + r.Width / 2, r.Y + 12));
        }
        Assert.Equal(PosterTitleChromeTarget.Collapse, value.HitTest(value.Collapse.X + 12, 40));
        Assert.Equal(PosterTitleChromeTarget.More, value.HitTest(value.More.X + 12, 40));
        Assert.Equal(PosterTitleChromeTarget.Drag, value.HitTest(30, 40));
        Assert.Equal(PosterTitleChromeTarget.Desktop, value.HitTest(0, 40));
        Assert.Equal(PosterTitleChromeTarget.Desktop, value.HitTest(30, 5));
        Assert.Equal(PosterTitleChromeTarget.Desktop, value.HitTest(width / 2, value.Height));
    }

    [Theory] [InlineData(240)] [InlineData(300)] [InlineData(800)]
    public void PostureNeverMovesTheActionOrGrabAnchors(double width)
    {
        var a = PosterTitleChromePolicy.Resolve(width, true, PosterTitleChromeKind.Cloud);
        var b = PosterTitleChromePolicy.Resolve(width, false, PosterTitleChromeKind.Cloud);
        Assert.Equal(a.Name, b.Name); Assert.Equal(a.Collapse, b.Collapse); Assert.Equal(a.More, b.More);
        for (double x = 0; x < width; x += 2)
            for (double y = 0; y < 60; y += 2) Assert.Equal(a.HitTest(x, y), b.HitTest(x, y));
    }

    [Theory] [InlineData(240)] [InlineData(300)]
    public void MissingCapabilityFallsBackWithoutChangingExistingTitleContract(double width)
    {
        var a = PosterTitleChromePolicy.Resolve(width, true, PosterTitleChromeKind.Cloud, false);
        var old = PosterTitleBarLayoutPolicy.Resolve(width);
        Assert.Equal(PosterTitleChromeKind.Basic, a.Kind);
        Assert.Equal(old.HeaderHeightDip, a.Height);
        Assert.Equal(old.MoreAction.XDip, a.More.X);
        Assert.Equal(old.CollapseAction.YDip, a.Collapse.Y);
        Assert.Equal(PosterTitleChromeTarget.Drag, a.HitTest(1, 1));
    }

    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(239)] [InlineData(-1)]
    public void InvalidWidthsCannotProduceUnboundedTitleHitRegions(double width) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PosterTitleChromePolicy.Resolve(width, false, PosterTitleChromeKind.Cloud));

    [Fact]
    public void UnknownStyleFallsBackAndNonFinitePointsDoNotHit()
    {
        var value = PosterTitleChromePolicy.Resolve(300, false, (PosterTitleChromeKind)99);
        Assert.Equal(PosterTitleChromeKind.Basic, value.Kind);
        Assert.Equal(PosterTitleChromeTarget.Desktop, value.HitTest(double.NaN, 30));
        Assert.Equal(PosterTitleChromeTarget.Desktop, value.HitTest(30, double.PositiveInfinity));
    }

    [Theory] [InlineData(240)] [InlineData(300)] [InlineData(800)]
    public void EveryGripStripIsInsideTheSamePaintedSolidGeometry(double width)
    {
        bool Painted(double x, double y) => PosterTitleChromePolicy.CloudBody(width).Contains(x, y) ||
            PosterTitleChromePolicy.CloudEllipses(width).Any(e =>
                Math.Pow((x - e.X - e.Width / 2) / (e.Width / 2), 2) +
                Math.Pow((y - e.Y - e.Height / 2) / (e.Height / 2), 2) <= 1);
        foreach (var b in PosterTitleChromePolicy.Resolve(width, false, PosterTitleChromeKind.Cloud).GripBands)
            foreach (var (x, y) in new[] { (b.X, b.Y), (b.Right, b.Y), (b.X, b.Bottom), (b.Right, b.Bottom) })
                Assert.True(Painted(x, y));
    }
}
