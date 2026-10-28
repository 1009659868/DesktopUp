using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterContentContextRouterTests
{
    private static readonly PosterInputSessionKey A = new("poster-A", 1), B = new("poster-B", 2);
    private static readonly DesktopPixelRect Frame = new(-320, 80, 300, 200);
    private static readonly PointerScreenPoint Point = new(-290, 120);

    private static PosterInputRegion Item(string id = "item-A", long generation = 7,
        DesktopPixelRect? bounds = null) =>
        new(PosterInputTargetKind.ContentItem, 3, bounds ?? new(20, 30, 60, 70))
            { ContentItemId = id, ContentSnapshotGeneration = generation };

    private static PosterInputLayoutSnapshot Layout(long revision = 1, params PosterInputRegion[] regions) =>
        new(revision, Frame.Width, Frame.Height, regions.Length == 0 ? new[] { Item() } : regions);

    private static PosterInputRouter Router(PosterInputLayoutSnapshot? layout = null)
    {
        var router = new PosterInputRouter();
        router.Register(A);
        Assert.True(router.Publish(A, Frame, layout ?? Layout()));
        return router;
    }

    private static PosterInputHit Hit(PosterInputRouter router) =>
        Assert.IsType<PosterInputHit>(router.Resolve(Point.X, Point.Y, new[] { A }));

    [Fact]
    public void ContextRequestRetainsFullCurrentWitnessWithoutCreatingLeftCapture()
    {
        var router = Router();
        var press = Hit(router);
        var release = Point with { X = Point.X + 5 };
        var request = Assert.IsType<PosterContentContextRequest>(router.ResolveContext(press, release, new[] { A }));
        Assert.Equal(A, request.Session);
        Assert.Equal("poster-A", request.PosterId);
        Assert.Equal("item-A", request.ContentItemId);
        Assert.Equal(7, request.ContentSnapshotGeneration);
        Assert.Equal(1, request.LayoutRevision);
        Assert.Equal(Frame, request.Frame);
        Assert.Equal(press, request.Hit);
        Assert.Equal(release, request.ScreenPoint);
        Assert.Null(router.Capture);
        Assert.Equal(press, Hit(router));
    }

    [Theory]
    [InlineData(PosterInputTargetKind.Item)]
    [InlineData(PosterInputTargetKind.ContentSpanHandle)]
    [InlineData(PosterInputTargetKind.TitleBar)]
    [InlineData(PosterInputTargetKind.MoreButton)]
    public void ContextOnlyTargetsContentItemNotLegacyItemsHandlesOrPosterChrome(PosterInputTargetKind kind)
    {
        var router = Router(Layout(1, Item() with { Kind = kind }));
        var hit = new PosterInputHit(A, 1, Frame, Item() with { Kind = kind });
        Assert.Null(router.ResolveContext(hit, Point, new[] { A }));
        Assert.Null(router.Capture);
    }

    [Fact]
    public void BlankContextUsesPublishedViewportWithoutSelectionOrCapture()
    {
        var viewport = new PosterInputRegion(PosterInputTargetKind.ContentViewport, 4, new(0, 0, 300, 200))
            { ContentSnapshotGeneration = 7 };
        var router = Router(Layout(1, Item(), viewport));
        var point = new PointerScreenPoint(Frame.X + 200, Frame.Y + 120);
        var hit = Assert.IsType<PosterInputHit>(router.Resolve(point.X, point.Y, [A]));
        Assert.True(router.ResolveContext(hit, point, [A], point)!.IsBackground);
        Assert.Null(router.Capture);
        Assert.Null(router.ResolveContext(hit, Point, [A], point));
        router.Invalidate(A);
        Assert.Null(router.ResolveContext(hit, point, [A], point));
    }

    [Fact]
    public void ContextPreservesVerifiedPressPointAndRejectsPointOutsideOriginalTile()
    {
        var router = Router(); var hit = Hit(router);
        var request = Assert.IsType<PosterContentContextRequest>(router.ResolveContext(hit, Point, [A], Point));
        Assert.Equal(Point, request.PressScreenPoint);
        Assert.Null(router.ResolveContext(hit, Point, [A], new(Frame.X + 190, Point.Y)));
        Assert.Null(router.ResolveContext(hit, Point, [A])!.PressScreenPoint);
    }

    [Theory]
    [InlineData(null, 7)]
    [InlineData("", 7)]
    [InlineData(" ", 7)]
    [InlineData("item-A", 0)]
    [InlineData("item-A", -1)]
    public void MissingContentIdentityOrGenerationCannotRequestMenu(string? id, long generation)
    {
        var router = Router(Layout(1, Item() with { ContentItemId = id, ContentSnapshotGeneration = generation }));
        Assert.Null(router.ResolveContext(Hit(router), Point, new[] { A }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingCaptureIsNeitherCancelledNorReplacedByContextRequest(bool peer)
    {
        var router = Router();
        var press = Hit(router);
        router.Register(B);
        Assert.True(router.Publish(B, Frame, Layout()));
        var capturedHit = Assert.IsType<PosterInputHit>(router.Resolve(Point.X, Point.Y, new[] { peer ? B : A }));
        var capture = Assert.IsType<PosterInputCapture>(router.Begin(capturedHit));
        Assert.Null(router.ResolveContext(press, Point, new[] { A, B }));
        Assert.Equal(capture, router.Capture);
        Assert.True(router.Owns(capture));
        router.End(capture);
        Assert.NotNull(router.ResolveContext(press, Point, new[] { A }));
    }

    [Fact]
    public void RepublishedLayoutOrContentGenerationRejectsRightPressWitness()
    {
        var router = Router();
        var press = Hit(router);
        Assert.True(router.Publish(A, Frame, Layout(2, Item(generation: 8))));
        Assert.Null(router.ResolveContext(press, Point, new[] { A }));
        var next = Assert.IsType<PosterContentContextRequest>(router.ResolveContext(Hit(router), Point, new[] { A }));
        Assert.Equal(8, next.ContentSnapshotGeneration);
        Assert.Equal(2, next.LayoutRevision);
    }

    [Fact]
    public void SameItemDifferentTileOrDifferentItemDoesNotRetargetContextRelease()
    {
        var otherTile = Item(bounds: new(100, 30, 60, 70));
        var otherItem = Item("item-B", bounds: new(180, 30, 60, 70));
        var router = Router(Layout(1, Item(), otherTile, otherItem));
        var press = Hit(router);
        Assert.Null(router.ResolveContext(press, new(Frame.X + 110, Point.Y), new[] { A }));
        Assert.Null(router.ResolveContext(press, new(Frame.X + 190, Point.Y), new[] { A }));
        Assert.NotNull(router.ResolveContext(press, Point, new[] { A }));
    }

    [Fact]
    public void ReleaseMustBeWithinPublishedClipAndRoundedFrame()
    {
        var router = Router();
        var press = Hit(router);
        Assert.Null(router.ResolveContext(press, new(Frame.X + 80, Point.Y), new[] { A }));
        Assert.Null(router.ResolveContext(press, new(Point.X, Frame.Y + 100), new[] { A }));
        Assert.Null(router.ResolveContext(press, new(Frame.X - 1, Point.Y), new[] { A }));

        var full = Item(bounds: new(0, 0, Frame.Width, Frame.Height));
        router = Router(Layout(1, full) with { OuterCornerRadius = 30 });
        press = Hit(router);
        Assert.Null(router.ResolveContext(press, new(Frame.X, Frame.Y), new[] { A }));
        Assert.NotNull(router.ResolveContext(press, new(Frame.X + 30, Frame.Y), new[] { A }));
    }

    [Fact]
    public void ActualFrontHitBlocksLowerItemEvenWhenThatHitIsNotContextEligible()
    {
        var router = Router();
        var press = Hit(router);
        router.Register(B);
        Assert.True(router.Publish(B, Frame, Layout(1, Item() with { Kind = PosterInputTargetKind.MoreButton })));
        Assert.Null(router.ResolveContext(press, Point, new[] { B, A }));
        Assert.NotNull(router.ResolveContext(press, Point, new[] { A, B }));
        Assert.Null(router.ResolveContext(press, Point, Array.Empty<PosterInputSessionKey>()));
    }

    [Fact]
    public void PeerBlankBlocksLowerItemUntilPeerIsHidden()
    {
        var router = Router();
        var press = Hit(router);
        router.Register(B);
        Assert.True(router.Publish(B, Frame, Layout(1, Item(bounds: new(200, 120, 40, 40)))));
        Assert.Null(router.ResolveContext(press, Point, new[] { B, A }));
        router.Invalidate(B);
        Assert.NotNull(router.ResolveContext(press, Point, new[] { B, A }));
    }

    [Fact]
    public void ForgedFrameRegionAndSessionCannotOpenMenuForCurrentItem()
    {
        var router = Router();
        var press = Hit(router);
        Assert.Null(router.ResolveContext(press with { Frame = Frame with { X = Frame.X - 1 } }, Point, new[] { A }));
        Assert.Null(router.ResolveContext(press with { Region = Item(generation: 99) }, Point, new[] { A }));
        Assert.Null(router.ResolveContext(press with { Region = Item("other") }, Point, new[] { A }));
        Assert.Null(router.ResolveContext(press with { Session = new(A.PosterId, 99) }, Point, new[] { A }));
    }

    [Fact]
    public void MoveInvalidateSuspendAndSessionReplacementRejectOldContextWitness()
    {
        var router = Router();
        var press = Hit(router);
        router.Suspend();
        Assert.Null(router.ResolveContext(press, Point, new[] { A }));
        router.Resume();
        Assert.NotNull(router.ResolveContext(press, Point, new[] { A }));
        Assert.True(router.Publish(A, Frame with { X = Frame.X + 1 }, Layout(2)));
        Assert.Null(router.ResolveContext(press, Point, new[] { A }));
        press = Hit(router);
        router.Invalidate(A);
        Assert.Null(router.ResolveContext(press, Point, new[] { A }));
        Assert.True(router.Publish(A, Frame, Layout(3)));
        press = Hit(router);
        var newer = new PosterInputSessionKey(A.PosterId, 2);
        router.Register(newer);
        Assert.True(router.Publish(newer, Frame, Layout()));
        Assert.Null(router.ResolveContext(press, Point, new[] { newer, A }));
        Assert.Null(router.Capture);
    }
}
