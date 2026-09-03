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
