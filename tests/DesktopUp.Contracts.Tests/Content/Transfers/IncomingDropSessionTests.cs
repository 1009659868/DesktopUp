using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class IncomingDropSessionTests
{
    internal static IncomingDropSurface Surface() => new(Guid.NewGuid(), "A", 7, 11, 13,
        new(-1000, -300, 600, 900), 2, 3, new(10, 30, 280, 250), ContentFlowAxis.Vertical, ScrollY: 25);
    private static readonly PointerScreenPoint Point = new(-900, 0);
    private static readonly string[] Paths = [@"G:\test-only\one.txt", @"G:\test-only\two.lnk"];

    [Theory][InlineData(0u,2u)][InlineData(8u,1u)][InlineData(4u,2u)][InlineData(12u,0u)][InlineData(32u,0u)]
    public void MappedSurfaceUsesMoveOrExplicitCopyButNeverLink(uint keys,uint expected)
    {
        var session=new IncomingDropSession();var surface=Surface() with {SupportsCopy=true};
        Assert.Equal(expected,session.Enter(Paths,surface,Point,keys,3)?.Effect??0);
        Assert.Equal(expected,session.Drop(Paths,surface,Point,keys,3)?.Preview.Effect??0);
        Assert.False(session.Active);
    }

    [Theory]
    [InlineData(0u, 2u, true)] [InlineData(4u, 2u, true)] [InlineData(8u, 3u, false)]
    [InlineData(32u, 3u, false)] [InlineData(2u, 3u, false)] [InlineData(0u, 1u, false)] [InlineData(0u, 0u, false)]
    public void OnlyMoveIntentCanOfferOrRelease(uint keys, uint effect, bool allowed)
    {
        var session = new IncomingDropSession(); var surface = Surface();
        Assert.Equal(allowed, session.Enter(Paths, surface, Point, keys, effect) is not null);
        Assert.Equal(allowed, session.Drop(Paths, surface, Point, keys, effect) is not null);
        Assert.False(session.Active); Assert.Null(session.Drop(Paths, surface, Point, keys, effect));
    }

    [Fact]
    public void NegativeScreenCoordinatesDpiPaddingAndScrollAreAppliedExactlyOnce()
    {
        var result = Surface().Map(Point)!;
        Assert.Equal(IncomingDropArea.Content, result.Area);
        Assert.Equal(40, result.ContentX); Assert.Equal(95, result.ContentY);
        Assert.Equal((40d, 70d), ContentScreenCoordinates.ToViewport(Point.X, Point.Y, -1000, -300, 2, 3, 10, 30));
    }

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void LoopCopyMapsToCanonicalPointWithoutPersistingItsCycle(ContentFlowAxis axis)
    {
        var surface = Surface() with { Axis = axis, ScrollX = axis == ContentFlowAxis.Vertical ? 0 : 165,
            ScrollY = axis == ContentFlowAxis.Vertical ? 135 : 0, LoopPeriod = 100 };
        var point = surface.Map(Point)!;
        Assert.Equal(2, point.LoopCycle);
        Assert.Equal(5, axis == ContentFlowAxis.Vertical ? point.ContentY : point.ContentX);
    }

    [Fact]
    public void TitleAndCollapsedFrameHaveNoInventedGridPointAndOutsideNeverClamps()
    {
        var surface = Surface();
        Assert.Equal(new(IncomingDropArea.Frame), surface.Map(new(-900, -290)));
        Assert.Null(surface.Map(new(-400, 0))); Assert.Null(surface.Map(new(-900, 600)));
        Assert.Null(surface.Map(new(-1001, -290)));
        surface = surface with { Frame = new(-1000, -300, 600, 60), ContentViewport = new(0, 0, 0, 0) };
        Assert.Equal(new(IncomingDropArea.Frame), surface.Map(new(-900, -290)));
        Assert.Null(surface.Map(Point));
    }

    [Theory]
    [InlineData("run")] [InlineData("poster")] [InlineData("entity")] [InlineData("layout")]
    [InlineData("projection")] [InlineData("frame")] [InlineData("dpi")] [InlineData("scroll")]
    public void ChangedBasisNeedsNewPreviewBeforeRelease(string field)
    {
        var session = new IncomingDropSession(); var old = Surface();
        var next = field switch {
            "run" => old with { RunId = Guid.NewGuid() }, "poster" => old with { PosterId = "B" },
            "entity" => old with { EntityVersion = 8 }, "layout" => old with { LayoutRevision = 12 },
            "projection" => old with { ProjectionGeneration = 14 }, "frame" => old with { Frame = old.Frame with { X = -1001 } },
            "dpi" => old with { ScaleY = 2.5 }, _ => old with { ScrollY = 35 } };
        Assert.NotNull(session.Enter(Paths, old, Point, 0, 2));
        Assert.Null(session.Drop(Paths, next, Point, 0, 2));
        session.Enter(Paths, old, Point, 0, 2);
        if (field is "run" or "poster")
        {
            Assert.Null(session.Over(next, Point, 0, 2)); Assert.False(session.Active);
            Assert.Null(session.Drop(Paths, next, Point, 0, 2));
        }
        else
        {
            Assert.NotNull(session.Over(next, Point, 0, 2));
            Assert.NotNull(session.Drop(Paths, next, Point, 0, 2));
        }
    }

    [Fact]
    public void LastPhysicalPointIsUsedAndReleaseIsConsumedBeforeAnAsyncConsumer()
    {
        var session = new IncomingDropSession(); var surface = Surface();
        session.Enter(Paths, surface, Point, 0, 2);
        var final = Point with { X = -800 };
        var ticket = session.Drop(Paths, surface, final, 0, 2)!;
        Assert.Equal(final, ticket.Preview.ScreenPoint); Assert.Equal(90, ticket.Preview.Location.ContentX);
        Assert.False(session.Active); Assert.Null(session.Drop(Paths, surface, final, 0, 2));
    }

    [Fact]
    public void PayloadIsFrozenAndChangedOrderOrReentryCannotReleaseOldIntent()
    {
        var session = new IncomingDropSession(); var surface = Surface(); var input = Paths.ToArray();
        session.Enter(input, surface, Point, 0, 2); input[0] = @"G:\changed.txt";
        Assert.Null(session.Drop(input, surface, Point, 0, 2));
        session.Enter(Paths, surface, Point, 0, 2);
        Assert.Null(session.Drop(Paths.Reverse().ToArray(), surface, Point, 0, 2));
        session.Enter(Paths, surface, Point, 0, 2); session.Leave();
        Assert.Null(session.Drop(Paths, surface, Point, 0, 2));
        session.Enter(Paths, surface, Point, 0, 2); session.Enter(input, surface, Point, 0, 2);
        Assert.Null(session.Drop(Paths, surface, Point, 0, 2));
    }

    [Theory] [InlineData(0)] [InlineData(101)]
    public void BoundedPayloadDoesNotTouchFileSystem(int count)
    {
        var paths = Enumerable.Range(0, count).Select(i => @"Z:\nonexistent\" + i).ToArray();
        var session = new IncomingDropSession();
        Assert.Null(session.Enter(paths, Surface(), Point, 0, 2)); Assert.False(session.Active);
    }

    [Fact]
    public void InvalidPayloadAndUnknownOrSuspendedSurfaceAreRefused()
    {
        var session = new IncomingDropSession(); var surface = Surface();
        foreach (var input in new string[][] { [""], ["bad\npath"], [Paths[0], Paths[0].ToUpperInvariant()] })
            Assert.Null(session.Enter(input, surface, Point, 0, 2));
        session.Enter(Paths, surface, Point, 0, 2);
        Assert.Null(session.Over(null, Point, 0, 2));
        Assert.Null(session.Drop(Paths, surface, Point, 0, 2));
        Assert.Null((surface with { ScaleX = double.NaN }).Map(Point));
        Assert.Null((surface with { ScaleX = 0 }).Map(Point));
        Assert.Null((surface with { ScrollX = 30 }).Map(Point));
    }
}
