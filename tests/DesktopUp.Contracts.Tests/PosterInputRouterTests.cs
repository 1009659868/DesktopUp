using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterInputRouterTests
{
    private static readonly PosterInputSessionKey A = new("A", 1), B = new("B", 2);
    private static PosterInputLayoutSnapshot Layout(long revision = 1, PosterInputTargetKind kind = PosterInputTargetKind.TitleBar) =>
        new(revision, 300, 300, new[] { new PosterInputRegion(kind, -1, new(0, 0, 300, 50)) });
    private static PosterInputRouter Router()
    {
        var r = new PosterInputRouter(); r.Register(A); r.Register(B);
        Assert.True(r.Publish(A, new(0, 0, 300, 300), Layout()));
        Assert.True(r.Publish(B, new(0, 0, 300, 300), Layout())); return r;
    }

    [Fact] public void PhysicalOrderWinsWithoutUsingCatalogAliasOrRegistrationOrder()
    {
        var r = Router();
        Assert.Equal(B, r.Resolve(10, 10, new[] { B, A })!.Session);
        Assert.Equal(A, r.Resolve(10, 10, new[] { A, B })!.Session);
        Assert.Null(r.Resolve(10, 100, new[] { A, B })); // Desktop hole.
    }
    [Fact] public void UpperHoleCanRouteToLowerInputAndRoundedCornerRemainsTransparent()
    {
        var r = Router();
        r.Publish(B, new(0, 0, 300, 300), Layout(2) with { Regions = new[] { new PosterInputRegion(PosterInputTargetKind.Item, 2, new(30, 80, 60, 80)) } });
        Assert.Equal(A, r.Resolve(10, 10, new[] { B, A })!.Session);
        Assert.Equal(B, r.Resolve(40, 90, new[] { B, A })!.Session);
        r.Publish(A, new(0, 0, 300, 300), Layout(2) with { OuterCornerRadius = 20 });
        Assert.Null(r.Resolve(0, 0, new[] { A }));
        Assert.NotNull(r.Resolve(20, 0, new[] { A }));
    }
    [Theory]
    [InlineData(PosterInputTargetKind.ResizeLeft)] [InlineData(PosterInputTargetKind.ResizeRight)]
    [InlineData(PosterInputTargetKind.ResizeTop)] [InlineData(PosterInputTargetKind.ResizeBottom)]
    [InlineData(PosterInputTargetKind.ResizeTopLeft)] [InlineData(PosterInputTargetKind.ResizeTopRight)]
    [InlineData(PosterInputTargetKind.ResizeBottomLeft)] [InlineData(PosterInputTargetKind.ResizeBottomRight)]
    public void ReflowKeepsOneCapturedIdentityUntilRelease(PosterInputTargetKind kind)
    {
        var r = Router(); r.Publish(A, new(0, 0, 300, 300), Layout(2, kind));
        var press = r.Resolve(10, 10, new[] { A, B })!;
        var capture = r.Begin(press)!;
        for (int i = 3; i < 30; i++)
        {
            r.Publish(A, new(i, i, 300, 300), Layout(i, kind));
            Assert.True(r.Owns(capture));
            Assert.Null(r.Resolve(10, 10, new[] { B, A }));
            Assert.Null(r.Begin(press));
            Assert.Equal(A, r.Capture!.Press.Session);
        }
        r.End(capture); Assert.Null(r.Capture);
        Assert.Equal(B, r.Resolve(10, 10, new[] { B, A })!.Session);
    }
    [Fact] public void ClickCannotInvokeAfterLayoutChangesOrReleaseOutsideButton()
    {
        var r = Router(); var token = r.Begin(r.Resolve(10, 10, new[] { A })!)!;
        Assert.True(r.CanInvoke(token, 10, 10)); Assert.False(r.CanInvoke(token, 301, 10));
        r.Publish(A, new(0, 0, 300, 300), Layout(2, PosterInputTargetKind.MoreButton));
        Assert.True(r.Owns(token)); Assert.False(r.CanInvoke(token, 10, 10));
    }
    [Fact] public void DeleteRecreateAndOldDisposeNeverAffectNewSession()
    {
        var r = Router(); var token = r.Begin(r.Resolve(10, 10, new[] { A })!)!;
        var newer = new PosterInputSessionKey("A", 3); r.Register(newer);
        Assert.False(r.Owns(token)); Assert.False(r.Publish(A, new(0, 0, 300, 300), Layout(9)));
        r.Publish(newer, new(0, 0, 300, 300), Layout());
        r.Remove(A); r.End(token);
        Assert.True(r.IsCurrent(newer, 1)); Assert.Equal(2, r.Count);
        Assert.Throws<InvalidOperationException>(() => r.Register(A));
    }
    [Fact] public void RecoveryInvalidatesCaptureAndUnreadyClusterWithoutBlockingReadyPeer()
    {
        var r = Router(); var token = r.Begin(r.Resolve(10, 10, new[] { A })!)!;
        r.Suspend(); r.Invalidate(A); r.Invalidate(B);
        Assert.False(r.Owns(token)); Assert.Null(r.Resolve(10, 10, new[] { A, B }));
        r.Publish(B, new(0, 0, 300, 300), Layout(3)); r.Resume();
        Assert.Equal(B, r.Resolve(10, 10, new[] { A, B })!.Session);
        Assert.False(r.IsCurrent(A, 1)); Assert.False(r.Owns(token));
    }
    [Fact] public void RegionsAreFrozenAndCoordinatesMayBeNegative()
    {
        var r = Router(); var regions = new[] { new PosterInputRegion(PosterInputTargetKind.MoreButton, -1, new(10, 0, 50, 50)) };
        Assert.True(r.Publish(A, new(-300, -100, 300, 300), Layout(2) with { Regions = regions }));
        regions[0] = new(PosterInputTargetKind.Item, 2, new(100, 100, 20, 20));
        Assert.Equal(PosterInputTargetKind.MoreButton, r.Resolve(-280, -90, new[] { A })!.Region.Kind);
        Assert.False(r.Publish(A, new(0, 0, 300, 300), Layout()));
        Assert.False(r.Publish(A, new(0, 0, 400, 300), Layout(3)));
    }
    [Fact] public void StaleHitCannotStartCaptureAndOldTokenCannotEndNewCapture()
    {
        var r = Router(); var hit = r.Resolve(10, 10, new[] { A })!;
        r.Publish(A, new(0, 0, 300, 300), Layout(2)); Assert.Null(r.Begin(hit));
        var old = r.Begin(r.Resolve(10, 10, new[] { A })!)!; r.End(old);
        var current = r.Begin(r.Resolve(10, 10, new[] { B })!)!; r.End(old);
        Assert.True(r.Owns(current)); r.Remove(B); Assert.Null(r.Capture);
    }
    [Fact] public void SameRevisionCannotPublishDifferentPixelsOrForgeAnInitialFrame()
    {
        var r = Router(); var hit = r.Resolve(10, 10, new[] { A })!;
        Assert.False(r.Publish(A, new(1, 0, 300, 300), Layout()));
        Assert.False(r.Publish(A, new(0, 0, 300, 300), Layout(1, PosterInputTargetKind.MoreButton)));
        Assert.Null(r.Begin(hit with { Frame = new(100, 100, 300, 300) }));
        Assert.True(r.Publish(A, new(0, 0, 300, 300), Layout()));
        Assert.NotNull(r.Begin(hit));
    }
    [Fact] public void InvalidatedGeometryCannotBeRepublishedToReviveOldCallbacks()
    {
        var r = Router(); var hit = r.Resolve(10, 10, new[] { A })!;
        r.Invalidate(A);
        Assert.False(r.Publish(A, new(0, 0, 300, 300), Layout()));
        Assert.True(r.Publish(A, new(0, 0, 300, 300), Layout(2)));
        Assert.False(r.IsCurrent(A, hit.LayoutRevision)); Assert.Null(r.Begin(hit));
    }
}
