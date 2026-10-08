namespace DesktopUp.Application;

[Flags]
public enum ScenePauseReason
{
    None = 0, Unavailable = 1, Editing = 2, ContentGesture = 4, FrameGesture = 8,
    SharedPolicy = 16
}

public readonly record struct ScenePointer(double X, double Y);
public readonly record struct SceneFrameContext(double Width, double Height, double Seconds,
    ScenePointer? Pointer, bool Visible, ScenePauseReason PauseReasons)
{
    public bool CanAnimate => Visible && PauseReasons == ScenePauseReason.None;
    // Read-only chrome fences; decoration cannot change these layout/space-owner values.
    public double HeaderBottom { get; init; }
    public double ContentTop { get; init; }
    public double FooterTop { get; init; }
    public DesktopUp.Domain.PosterTitleChromeKind TitleChrome { get; init; }
    public bool TitleMotionPaused { get; init; }

    public static SceneFrameContext Create(double width, double height, double seconds,
        ScenePointer? pointer, bool visible, ScenePauseReason pauseReasons)
    {
        width = Size(width); height = Size(height);
        if (pointer is { } p && (!double.IsFinite(p.X) || !double.IsFinite(p.Y) ||
            p.X < 0 || p.Y < 0 || p.X > width || p.Y > height)) pointer = null;
        return new(width, height, double.IsFinite(seconds) ? Math.Max(0, seconds) : 0,
            pointer, visible, pauseReasons);
    }
    private static double Size(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
}

/// <summary>A failed decoration stays detached until its host explicitly creates a new instance.
/// Failure never retries once per frame or cancels another poster's scene.</summary>
public sealed class SceneFailureBoundary
{
    public Exception? Failure { get; private set; }
    public bool TryRun(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Failure is not null) return false;
        try { action(); return true; }
        catch (Exception error) { Failure = error; return false; }
    }
}
