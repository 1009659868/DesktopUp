using DesktopUp.Domain;

namespace DesktopUp.Application;

public static class PosterFrameResizePolicy
{
    private const double MaximumWidthRatio = 0.45;

    public static DesktopPixelRect Resolve(
        PosterGestureKind gesture,
        DesktopPixelRect initial,
        int deltaX,
        int deltaY,
        DesktopPixelRect workArea,
        int minimumWidth,
        int minimumHeight)
    {
        if (!initial.IsUsable || !workArea.IsUsable)
        {
            throw new ArgumentException("Initial and work-area bounds must be usable.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumHeight);

        if (gesture == PosterGestureKind.Move)
        {
            int maximumX = Math.Max(workArea.X, workArea.Right - initial.Width);
            int maximumY = Math.Max(workArea.Y, workArea.Bottom - initial.Height);
            return initial with
            {
                X = Math.Clamp(initial.X + deltaX, workArea.X, maximumX),
                Y = Math.Clamp(initial.Y + deltaY, workArea.Y, maximumY)
            };
        }

        bool movesLeft = gesture is
            PosterGestureKind.ResizeLeft or
            PosterGestureKind.ResizeTopLeft or
            PosterGestureKind.ResizeBottomLeft;
        bool movesRight = gesture is
            PosterGestureKind.ResizeRight or
            PosterGestureKind.ResizeTopRight or
            PosterGestureKind.ResizeBottomRight;
        bool movesTop = gesture is
            PosterGestureKind.ResizeTop or
            PosterGestureKind.ResizeTopLeft or
            PosterGestureKind.ResizeTopRight;
        bool movesBottom = gesture is
            PosterGestureKind.ResizeBottom or
            PosterGestureKind.ResizeBottomLeft or
            PosterGestureKind.ResizeBottomRight;

        if (!movesLeft && !movesRight && !movesTop && !movesBottom)
        {
            throw new ArgumentOutOfRangeException(nameof(gesture), gesture, "Unsupported resize gesture.");
        }

        int x = initial.X;
        int y = initial.Y;
        int width = initial.Width;
        int height = initial.Height;
        int configuredMaximumWidth = Math.Max(
            minimumWidth,
            checked((int)Math.Round(workArea.Width * MaximumWidthRatio)));

        if (movesLeft)
        {
            int anchoredRight = initial.Right;
            int availableWidth = anchoredRight - workArea.X;
            int maximumWidth = Math.Min(configuredMaximumWidth, availableWidth);
            width = Math.Clamp(initial.Width - deltaX, minimumWidth, maximumWidth);
            x = anchoredRight - width;
        }
        else if (movesRight)
        {
            int availableWidth = workArea.Right - initial.X;
            int maximumWidth = Math.Min(configuredMaximumWidth, availableWidth);
            width = Math.Clamp(initial.Width + deltaX, minimumWidth, maximumWidth);
        }

        if (movesTop)
        {
            int anchoredBottom = initial.Bottom;
            int maximumHeight = anchoredBottom - workArea.Y;
            height = Math.Clamp(initial.Height - deltaY, minimumHeight, maximumHeight);
            y = anchoredBottom - height;
        }
        else if (movesBottom)
        {
            int maximumHeight = workArea.Bottom - initial.Y;
            height = Math.Clamp(initial.Height + deltaY, minimumHeight, maximumHeight);
        }

        return new DesktopPixelRect(x, y, width, height);
    }
}
