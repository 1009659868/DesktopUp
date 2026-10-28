namespace DesktopUp.Application;

// DIP coordinates, independent of XAML and persistence. Marks do not quantize dragging.
public static class SliderPresetGeometry
{
    public static double Center(double minimum, double maximum, double value, double left, double width, double thumbWidth)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum ||
            !double.IsFinite(value) || value < minimum || value > maximum || !double.IsFinite(left) ||
            !double.IsFinite(width) || !double.IsFinite(thumbWidth) || thumbWidth <= 0 || width <= thumbWidth)
            throw new ArgumentOutOfRangeException(nameof(value));
        return left + thumbWidth / 2 + (width - thumbWidth) * ((value - minimum) / (maximum - minimum));
    }

    public static bool IsPresetClick(double pressX, double pressY, double endX, double endY,
        double furthestTravel, double markX, double markY) =>
        new[] { pressX, pressY, endX, endY, furthestTravel, markX, markY }.All(double.IsFinite) &&
        furthestTravel >= 0 && furthestTravel <= 4 &&
        Math.Abs(pressX - markX) <= 7 && Math.Abs(pressY - markY) <= 12 &&
        Math.Abs(endX - pressX) <= 4 && Math.Abs(endY - pressY) <= 4;
}
