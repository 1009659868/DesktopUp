namespace DesktopUp.Domain;

public enum PosterOperatingMode
{
    Using = 0,
    Editing = 1
}

public enum PosterGestureKind
{
    Move = 0,
    ResizeRight = 1,
    ResizeBottom = 2,
    ResizeBottomRight = 3,
    ResizeLeft = 4,
    ResizeTop = 5,
    ResizeTopLeft = 6,
    ResizeTopRight = 7,
    ResizeBottomLeft = 8
}

public readonly record struct PosterTitleBarActionLayout(
    double XDip,
    double YDip,
    double SizeDip)
{
    public double RightDip => XDip + SizeDip;
}

public readonly record struct PosterTitleBarLayout(
    double HeaderHeightDip,
    PosterTitleBarActionLayout CollapseAction,
    PosterTitleBarActionLayout MoreAction);

public static class PosterTitleBarLayoutPolicy
{
    public const double HeaderHeightDip = 46;
    public const double LeftPaddingDip = 14;
    public const double TopPaddingDip = 7;
    public const double RightPaddingDip = 12;
    public const double BottomPaddingDip = 5;
    public const double ActionSizeDip = 24;
    public const double ActionGapDip = 2;

    public static PosterTitleBarLayout Resolve(double frameWidthDip)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            frameWidthDip,
            PosterFrameGeometryPolicy.MinimumWidthDip);

        double actionTop = TopPaddingDip +
            ((HeaderHeightDip - TopPaddingDip - BottomPaddingDip - ActionSizeDip) / 2);
        double moreX = frameWidthDip - RightPaddingDip - ActionSizeDip;
        double collapseX = moreX - ActionGapDip - ActionSizeDip;
        return new PosterTitleBarLayout(
            HeaderHeightDip,
            new PosterTitleBarActionLayout(collapseX, actionTop, ActionSizeDip),
            new PosterTitleBarActionLayout(moreX, actionTop, ActionSizeDip));
    }
}

public readonly record struct PosterFrameGeometry(
    double XDip,
    double YDip,
    double WidthDip,
    double ExpandedHeightDip,
    bool IsLocked,
    bool IsCollapsed,
    long Revision)
{
    public double HeightDip => IsCollapsed
        ? PosterFrameGeometryPolicy.CollapsedHeightDip
        : ExpandedHeightDip;
}

public static class PosterFrameGeometryPolicy
{
    public const double MinimumWidthDip = 240;
    public const double MinimumExpandedHeightDip = 240;
    public const double CollapsedHeightDip = 60;

    public static PosterFrameGeometry Move(
        PosterFrameGeometry current,
        double xDip,
        double yDip) => current with
    {
        XDip = xDip,
        YDip = yDip,
        Revision = checked(current.Revision + 1)
    };

    public static PosterFrameGeometry Resize(
        PosterFrameGeometry current,
        double widthDip,
        double heightDip,
        double maximumWidthDip,
        double maximumHeightDip)
    {
        if (current.IsLocked || current.IsCollapsed)
        {
            return current;
        }

        double width = Math.Clamp(
            widthDip,
            MinimumWidthDip,
            Math.Max(MinimumWidthDip, maximumWidthDip));
        double height = Math.Clamp(
            heightDip,
            MinimumExpandedHeightDip,
            Math.Max(MinimumExpandedHeightDip, maximumHeightDip));
        return current with
        {
            WidthDip = width,
            ExpandedHeightDip = height,
            Revision = checked(current.Revision + 1)
        };
    }

    public static PosterFrameGeometry ToggleLocked(
        PosterFrameGeometry current) => current with
    {
        IsLocked = !current.IsLocked,
        Revision = checked(current.Revision + 1)
    };

    public static PosterFrameGeometry ToggleCollapsed(
        PosterFrameGeometry current) => current with
    {
        IsCollapsed = !current.IsCollapsed,
        Revision = checked(current.Revision + 1)
    };
}

public static class LockPermissionPolicy
{
    public static bool CanBeginGesture(
        PosterOperatingMode mode,
        PosterFrameGeometry frame,
        PosterGestureKind gesture) => gesture switch
    {
        PosterGestureKind.Move => !frame.IsLocked,
        PosterGestureKind.ResizeRight or
        PosterGestureKind.ResizeBottom or
        PosterGestureKind.ResizeBottomRight or
        PosterGestureKind.ResizeLeft or
        PosterGestureKind.ResizeTop or
        PosterGestureKind.ResizeTopLeft or
        PosterGestureKind.ResizeTopRight or
        PosterGestureKind.ResizeBottomLeft =>
            mode == PosterOperatingMode.Editing &&
            !frame.IsLocked &&
            !frame.IsCollapsed,
        _ => false
    };

    public static bool CanToggleLock(PosterFrameGeometry frame) => true;

    public static bool CanToggleCollapse(PosterFrameGeometry frame) => true;

    public static bool CanActivateItem(PosterFrameGeometry frame) =>
        !frame.IsCollapsed;
}

public static class PosterItemViewportPolicy
{
    public static double ResolveMaximumOffset(
        int itemCount,
        int columns,
        double rowHeightDip,
        double rowGapDip,
        double viewportHeightDip)
    {
        if (itemCount <= 0)
        {
            return 0;
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowHeightDip);
        ArgumentOutOfRangeException.ThrowIfNegative(rowGapDip);

        int rows = (int)Math.Ceiling(itemCount / (double)columns);
        double contentHeight =
            (rows * rowHeightDip) + (Math.Max(0, rows - 1) * rowGapDip);
        return Math.Max(0, contentHeight - Math.Max(0, viewportHeightDip));
    }

    public static double ResolveWheelOffset(
        double currentOffsetDip,
        int wheelDelta,
        double rowStrideDip,
        double maximumOffsetDip)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowStrideDip);
        double wheelSteps = wheelDelta / 120.0;
        return Math.Clamp(
            currentOffsetDip - (wheelSteps * rowStrideDip),
            0,
            Math.Max(0, maximumOffsetDip));
    }
}
