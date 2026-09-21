using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterContentInputContractTests
{
    private static readonly PosterInputSessionKey Session = new("content-poster", 7);
    private static PosterInputRegion Item(string id = "demo-a", long generation = 11, int index = 0) =>
        new(PosterInputTargetKind.ContentItem, index, new(20, 60, 100, 100))
        { ContentItemId = id, ContentSnapshotGeneration = generation };
    private static PosterInputLayoutSnapshot Layout(long revision, params PosterInputRegion[] regions) =>
        new(revision, 300, 400, regions);

    [Fact]
    public void ExistingInputEnumValuesAndLegacyMetadataStayCompatible()
    {
        Assert.Equal(4, (int)PosterInputTargetKind.Item);
        Assert.Equal(14, (int)PosterInputTargetKind.ResizeBottomLeft);
        Assert.Equal(4, (int)PosterInputEventKind.Scrolled);
        Assert.Equal(5, (int)PosterInputEventKind.Pressed);
        var legacy = new PosterInputRegion(PosterInputTargetKind.Item, 3, new(10, 10, 30, 30));
        Assert.Null(legacy.ContentItemId);
        Assert.Equal(0, legacy.ContentSnapshotGeneration);
        Assert.False(PosterContentInputPolicy.IsTarget(legacy.Kind));
    }

    [Theory]
    [InlineData(PosterInputTargetKind.ContentItem, true)]
    [InlineData(PosterInputTargetKind.ContentSpanHandle, true)]
    [InlineData(PosterInputTargetKind.ContentViewport, false)]
    [InlineData(PosterInputTargetKind.Item, false)]
    [InlineData(PosterInputTargetKind.EditSurface, false)]
    [InlineData(PosterInputTargetKind.TitleBar, false)]
    public void OnlyContentCardAndSpanHandleAreContentDragTargets(PosterInputTargetKind kind, bool expected) =>
        Assert.Equal(expected, PosterContentInputPolicy.CanDrag(kind));

    [Fact]
    public void ContentIdentityRequiresGenerationAndStableItemExceptBlankViewport()
    {
        Assert.True(PosterContentInputPolicy.HasValidIdentity(Item()));
        Assert.False(PosterContentInputPolicy.HasValidIdentity(Item(generation: 0)));
        Assert.False(PosterContentInputPolicy.HasValidIdentity(Item(id: "")));
        Assert.False(PosterContentInputPolicy.HasValidIdentity(Item() with { Kind = PosterInputTargetKind.Item }));
        Assert.True(PosterContentInputPolicy.HasValidIdentity(new(PosterInputTargetKind.ContentViewport, -1, new(0, 60, 300, 300))
            { ContentSnapshotGeneration = 1 }));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(4, 0, false)]
    [InlineData(-4, 0, false)]
    [InlineData(3, 3, false)]
    [InlineData(3, 4, true)]
    [InlineData(-5, 0, true)]
    [InlineData(0, 5, true)]
    [InlineData(10000, -10000, true)]
    public void ContentThresholdMatchesExistingFivePhysicalPixels(int x, int y, bool expected) =>
        Assert.Equal(expected, PosterContentInputPolicy.CrossedDragThreshold(x, y));

    [Fact]
    public void CaptureRetainsStableItemAndOriginalBasisAcrossOwnReflow()
    {
        var router = new PosterInputRouter();
        router.Register(Session);
        Assert.True(router.Publish(Session, new(-300, 0, 300, 400), Layout(1, Item())));
        PosterInputCapture capture = router.Begin(router.Resolve(-270, 90, [Session])!)!;
        Assert.True(router.Publish(Session, new(-300, 0, 300, 400), Layout(2,
            Item("demo-b", 12), Item("demo-a", 12, 1) with { Bounds = new(160, 60, 100, 100) })));
        Assert.True(router.Owns(capture));
        PosterInputEventArgs input = PosterContentInputPolicy.Event(capture, PosterInputEventKind.GesturePreview,
            new(-270, 90), new(-110, 210), 2, 2);
        Assert.Same(capture, input.Capture);
        Assert.Equal("demo-a", input.ContentItemId);
        Assert.Equal(11, input.ContentSnapshotGeneration);
        Assert.Equal(0, input.TargetIndex); // Historical index is not a new-layout lookup.
        Assert.Equal(1, input.Capture!.Press.LayoutRevision);
        Assert.Equal(2, input.LayoutRevision);
        Assert.Equal(160, input.DeltaX);
        Assert.Equal(120, input.DeltaY);
        Assert.Equal(new PointerScreenPoint(-110, 210), input.LatestPoint);
        Assert.Equal(Session, input.Session);
    }

    [Fact]
    public void SameInputRevisionCannotSilentlyChangeItemIdentityOrContentGeneration()
    {
        var router = new PosterInputRouter();
        router.Register(Session);
        var frame = new DesktopPixelRect(0, 0, 300, 400);
        Assert.True(router.Publish(Session, frame, Layout(1, Item())));
        Assert.False(router.Publish(Session, frame, Layout(1, Item("demo-b"))));
        Assert.False(router.Publish(Session, frame, Layout(1, Item(generation: 12))));
    }

    [Fact]
    public void SpanHandleWinsBeforeCardAndViewportWithoutAddingWindowOwnership()
    {
        var router = new PosterInputRouter();
        router.Register(Session);
        var handle = Item() with { Kind = PosterInputTargetKind.ContentSpanHandle, Bounds = new(96, 136, 24, 24) };
        var viewport = new PosterInputRegion(PosterInputTargetKind.ContentViewport, -1, new(0, 60, 300, 300))
            { ContentSnapshotGeneration = 11 };
        Assert.True(router.Publish(Session, new(0, 0, 300, 400), Layout(1, handle, Item(), viewport)));
        Assert.Equal(PosterInputTargetKind.ContentSpanHandle, router.Resolve(105, 145, [Session])!.Region.Kind);
        Assert.Equal(PosterInputTargetKind.ContentItem, router.Resolve(30, 80, [Session])!.Region.Kind);
        Assert.Equal(PosterInputTargetKind.ContentViewport, router.Resolve(250, 250, [Session])!.Region.Kind);
        Assert.Null(router.Resolve(250, 390, [Session]));
    }

    [Fact]
    public void ClippingKeepsContentIdentityAndGeneration()
    {
        var original = Item() with { Bounds = new(-10, 30, 100, 100) };
        PosterInputRegion clipped = Assert.Single(PosterInputRegionClipPolicy.Clip(300, 70, [original]));
        Assert.Equal(new DesktopPixelRect(0, 30, 90, 40), clipped.Bounds);
        Assert.Equal(original.ContentItemId, clipped.ContentItemId);
        Assert.Equal(original.ContentSnapshotGeneration, clipped.ContentSnapshotGeneration);
    }

    [Theory]
    [InlineData(TitleNativeEndReason.CaptureTransferred)]
    [InlineData(TitleNativeEndReason.CancelMode)]
    [InlineData(TitleNativeEndReason.RouteInvalidated)]
    [InlineData(TitleNativeEndReason.CursorUnavailable)]
    [InlineData(TitleNativeEndReason.CaptureUnavailable)]
    [InlineData(TitleNativeEndReason.Programmatic)]
    public void ContentCancellationPreservesObservedReasonAndCapturedBasis(TitleNativeEndReason reason)
    {
        var capture = new PosterInputCapture(Guid.NewGuid(), new(Session, 3, new(0, 0, 300, 400), Item()));
        var input = PosterContentInputPolicy.Event(capture, PosterInputEventKind.GestureCancelled,
            new(30, 90), new(40, 100), 8, 9, reason);
        Assert.Equal(reason, input.EndReason);
        Assert.Equal("demo-a", input.ContentItemId);
        Assert.Equal(11, input.ContentSnapshotGeneration);
        Assert.Equal(8, input.LayoutRevision);
        Assert.Equal(9, input.Sequence);
    }

    [Fact]
    public void PressMustBeExplicitlyClaimedAndReleaseUsesLatestPointerNotPressCoordinates()
    {
        var capture = new PosterInputCapture(Guid.NewGuid(), new(Session, 1, new(0, 0, 300, 400), Item()));
        var press = PosterContentInputPolicy.Event(capture, PosterInputEventKind.Pressed, new(30, 90), new(30, 90), 1, 1);
        Assert.False(press.Handled);
        press.Handled = true;
        Assert.True(press.Handled);
        var release = PosterContentInputPolicy.Event(capture, PosterInputEventKind.GestureCompleted,
            new(30, 90), new(-20, 80), 6, 12);
        Assert.Equal(-50, release.DeltaX);
        Assert.Equal(-10, release.DeltaY);
        Assert.Equal(new PointerScreenPoint(-20, 80), release.LatestPoint);
        Assert.Equal(TitleNativeEndReason.None, release.EndReason);
        Assert.False(release.Handled);
    }

    [Fact]
    public void DeletedSessionCannotRetargetCaptureToReplacementPoster()
    {
        var router = new PosterInputRouter();
        router.Register(Session);
        Assert.True(router.Publish(Session, new(0, 0, 300, 400), Layout(1, Item())));
        var capture = router.Begin(router.Resolve(30, 90, [Session])!)!;
        var replacement = Session with { Epoch = Session.Epoch + 1 };
        router.Register(replacement);
        Assert.True(router.Publish(replacement, new(0, 0, 300, 400), Layout(1, Item("new-demo"))));
        Assert.False(router.Owns(capture));
        var cancel = PosterContentInputPolicy.Event(capture, PosterInputEventKind.GestureCancelled,
            new(30, 90), new(40, 100), 1, 2, TitleNativeEndReason.RouteInvalidated);
        Assert.Equal(Session, cancel.Session);
        Assert.Equal("demo-a", cancel.ContentItemId);
    }
}

