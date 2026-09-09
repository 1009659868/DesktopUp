using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterInteractionTests
{
    private static readonly PosterFrameGeometry Expanded = new(
        20,
        30,
        420,
        700,
        false,
        false,
        7);

    [Fact]
    [Trait("Contract", "LegacyFixedTopToggle-ReplacedByM04SpatialExpansion")]
    public void CollapsePreservesTopLeftWidthAndExpandedHeight()
    {
        PosterFrameGeometry collapsed =
            PosterFrameGeometryPolicy.ToggleCollapsed(Expanded);
        PosterFrameGeometry restored =
            PosterFrameGeometryPolicy.ToggleCollapsed(collapsed);

        Assert.True(collapsed.IsCollapsed);
        Assert.Equal(60, collapsed.HeightDip);
        Assert.Equal(Expanded.XDip, collapsed.XDip);
        Assert.Equal(Expanded.YDip, collapsed.YDip);
        Assert.Equal(Expanded.WidthDip, collapsed.WidthDip);
        Assert.Equal(Expanded.ExpandedHeightDip, collapsed.ExpandedHeightDip);
        Assert.False(restored.IsCollapsed);
        Assert.Equal(Expanded.ExpandedHeightDip, restored.HeightDip);
    }

    [Fact]
    public void TitleBarActionsUseOneDeskBoxAlignedGeometryContract()
    {
        PosterTitleBarLayout layout = PosterTitleBarLayoutPolicy.Resolve(420);

        Assert.Equal(46, layout.HeaderHeightDip);
        Assert.Equal(358, layout.CollapseAction.XDip);
        Assert.Equal(384, layout.MoreAction.XDip);
        Assert.Equal(12, layout.CollapseAction.YDip);
        Assert.Equal(24, layout.CollapseAction.SizeDip);
        Assert.Equal(2, layout.MoreAction.XDip - layout.CollapseAction.RightDip);
        Assert.Equal(12, 420 - layout.MoreAction.RightDip);
    }

    [Fact]
    public void LockBlocksMoveAndResizeButNotProtectedButtonsOrItems()
    {
        PosterFrameGeometry locked = PosterFrameGeometryPolicy.ToggleLocked(Expanded);

        Assert.False(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Using,
            locked,
            PosterGestureKind.Move));
        Assert.False(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Editing,
            locked,
            PosterGestureKind.ResizeBottomRight));
        Assert.True(LockPermissionPolicy.CanToggleLock(locked));
        Assert.True(LockPermissionPolicy.CanToggleCollapse(locked));
        Assert.True(LockPermissionPolicy.CanActivateItem(locked));
    }

    [Fact]
    public void ResizeRequiresEditingUnlockedExpandedState()
    {
        Assert.False(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Using,
            Expanded,
            PosterGestureKind.ResizeRight));
        Assert.True(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Editing,
            Expanded,
            PosterGestureKind.ResizeRight));
        Assert.False(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Editing,
            PosterFrameGeometryPolicy.ToggleCollapsed(Expanded),
            PosterGestureKind.ResizeRight));
    }

    [Theory]
    [InlineData(PosterGestureKind.ResizeLeft)]
    [InlineData(PosterGestureKind.ResizeRight)]
    [InlineData(PosterGestureKind.ResizeTop)]
    [InlineData(PosterGestureKind.ResizeBottom)]
    [InlineData(PosterGestureKind.ResizeTopLeft)]
    [InlineData(PosterGestureKind.ResizeTopRight)]
    [InlineData(PosterGestureKind.ResizeBottomLeft)]
    [InlineData(PosterGestureKind.ResizeBottomRight)]
    public void AllEightResizeDirectionsShareTheEditingPermissionContract(
        PosterGestureKind gesture)
    {
        Assert.True(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Editing,
            Expanded,
            gesture));
        Assert.False(LockPermissionPolicy.CanBeginGesture(
            PosterOperatingMode.Using,
            Expanded,
            gesture));
    }

    [Theory]
    [InlineData(PosterGestureKind.ResizeLeft, 50, 0, 150, 100, 350, 500)]
    [InlineData(PosterGestureKind.ResizeRight, 50, 0, 100, 100, 450, 500)]
    [InlineData(PosterGestureKind.ResizeTop, 0, 50, 100, 150, 400, 450)]
    [InlineData(PosterGestureKind.ResizeBottom, 0, 50, 100, 100, 400, 550)]
    [InlineData(PosterGestureKind.ResizeTopLeft, 50, 50, 150, 150, 350, 450)]
    [InlineData(PosterGestureKind.ResizeTopRight, 50, 50, 100, 150, 450, 450)]
    [InlineData(PosterGestureKind.ResizeBottomLeft, 50, 50, 150, 100, 350, 550)]
    [InlineData(PosterGestureKind.ResizeBottomRight, 50, 50, 100, 100, 450, 550)]
    public void EightWayResizeKeepsTheOppositeEdgesAnchored(
        PosterGestureKind gesture,
        int deltaX,
        int deltaY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        DesktopPixelRect resolved = PosterFrameResizePolicy.Resolve(
            gesture,
            new DesktopPixelRect(100, 100, 400, 500),
            deltaX,
            deltaY,
            new DesktopPixelRect(0, 0, 1000, 900),
            minimumWidth: 240,
            minimumHeight: 240);

        Assert.Equal(
            new DesktopPixelRect(
                expectedX,
                expectedY,
                expectedWidth,
                expectedHeight),
            resolved);
    }

    [Fact]
    public void LeftAndTopResizeClampToWorkAreaAndMinimumSize()
    {
        DesktopPixelRect maximum = PosterFrameResizePolicy.Resolve(
            PosterGestureKind.ResizeTopLeft,
            new DesktopPixelRect(100, 100, 400, 500),
            deltaX: -1000,
            deltaY: -1000,
            new DesktopPixelRect(0, 0, 1000, 900),
            minimumWidth: 240,
            minimumHeight: 240);
        DesktopPixelRect minimum = PosterFrameResizePolicy.Resolve(
            PosterGestureKind.ResizeTopLeft,
            new DesktopPixelRect(100, 100, 400, 500),
            deltaX: 1000,
            deltaY: 1000,
            new DesktopPixelRect(0, 0, 1000, 900),
            minimumWidth: 240,
            minimumHeight: 240);

        Assert.Equal(new DesktopPixelRect(50, 0, 450, 600), maximum);
        Assert.Equal(new DesktopPixelRect(260, 360, 240, 240), minimum);
    }

    [Fact]
    public void ResizeClampsWithoutChangingContentOrAnchorFields()
    {
        PosterFrameGeometry resized = PosterFrameGeometryPolicy.Resize(
            Expanded,
            100,
            100,
            900,
            1000);

        Assert.Equal(240, resized.WidthDip);
        Assert.Equal(240, resized.ExpandedHeightDip);
        Assert.Equal(Expanded.XDip, resized.XDip);
        Assert.Equal(Expanded.YDip, resized.YDip);
        Assert.Equal(Expanded.Revision + 1, resized.Revision);
    }

    [Fact]
    public void ItemViewportUsesContinuousOffsetForPartialRows()
    {
        double maximum = PosterItemViewportPolicy.ResolveMaximumOffset(
            itemCount: 6,
            columns: 2,
            rowHeightDip: 118,
            rowGapDip: 12,
            viewportHeightDip: 200);

        Assert.Equal(178, maximum);
        Assert.Equal(130, PosterItemViewportPolicy.ResolveWheelOffset(
            currentOffsetDip: 0,
            wheelDelta: -120,
            rowStrideDip: 130,
            maximumOffsetDip: maximum));
        Assert.Equal(178, PosterItemViewportPolicy.ResolveWheelOffset(
            currentOffsetDip: 130,
            wheelDelta: -120,
            rowStrideDip: 130,
            maximumOffsetDip: maximum));
    }

    [Fact]
    public void ItemViewportReturnsToTopAndClampsEmptyContent()
    {
        Assert.Equal(0, PosterItemViewportPolicy.ResolveMaximumOffset(
            itemCount: 0,
            columns: 1,
            rowHeightDip: 118,
            rowGapDip: 12,
            viewportHeightDip: 10));
        Assert.Equal(0, PosterItemViewportPolicy.ResolveWheelOffset(
            currentOffsetDip: 90,
            wheelDelta: 120,
            rowStrideDip: 130,
            maximumOffsetDip: 166));
    }
}
