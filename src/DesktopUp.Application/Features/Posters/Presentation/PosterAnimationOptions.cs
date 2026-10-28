namespace DesktopUp.Application;

// Startup-only presentation settings. Not poster intent, a database revision, or a hot setting.
public sealed record PosterAnimationOptions(
    int MaximumStepPixels = 12, int FrameIntervalMs = 16, int DurationMs = 240,
    string Easing = "ease-in-out")
{
    public void Validate()
    {
        if (MaximumStepPixels is < 1 or > 64 || FrameIntervalMs is < 4 or > 100 ||
            DurationMs is < 80 or > 2000 || Easing is not ("linear" or "ease-in-out"))
            throw new ArgumentException("Animation requires maximumStepPixels 1..64, frameIntervalMs 4..100, durationMs 80..2000, easing linear/ease-in-out.");
    }
}
