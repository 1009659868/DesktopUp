namespace DesktopUp.ContentWorker;

// A small opaque body alone is NOT evidence of padding. Only prefer an alternative
// when a separate Shell request returns the same unscaled body on a smaller canvas.
// This policy selects whole images; it never crops, edits or invents pixels.
internal static class ShellIconQuality
{
    private readonly record struct Bounds(int X, int Y, int Width, int Height);

    internal static bool IsPossiblyPadded(byte[] pixels, int width, int height)
    {
        var body = OpaqueBody(pixels, width, height);
        return width >= 96 && height >= 96 && body is { } b && b.Width <= width * .35 && b.Height <= height * .35;
    }

    internal static bool PreferSmaller(byte[] large, int width, int height, byte[] small, int smallWidth, int smallHeight)
    {
        if (!IsPossiblyPadded(large, width, height) || smallWidth >= width || smallHeight >= height ||
            OpaqueBody(large, width, height) is not { } a || OpaqueBody(small, smallWidth, smallHeight) is not { } b ||
            b.Width < smallWidth * .55 || b.Height < smallHeight * .55 ||
            Math.Abs(a.Width - b.Width) > Math.Max(2, a.Width * .15) || Math.Abs(a.Height - b.Height) > Math.Max(2, a.Height * .15)) return false;
        // Compare body colour/alpha, not just box sizes (avoid substituting a different icon).
        long difference = 0;
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            int first = ((a.Y + y * a.Height / 16) * width + a.X + x * a.Width / 16) * 4;
            int second = ((b.Y + y * b.Height / 16) * smallWidth + b.X + x * b.Width / 16) * 4;
            for (int channel = 0; channel < 4; channel++) difference += Math.Abs(large[first + channel] - small[second + channel]);
        }
        return difference <= 12 * 16 * 16 * 4;
    }

    private static Bounds? OpaqueBody(byte[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height * 4 != pixels.Length) return null;
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            // The affected Shell bitmap has a faint full-canvas border; alpha != 0 misses it.
            if (pixels[(y * width + x) * 4 + 3] <= 127) continue;
            left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        return right < left ? null : new(left, top, right - left + 1, bottom - top + 1);
    }
}
