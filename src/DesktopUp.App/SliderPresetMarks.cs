using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace DesktopUp;

// Decorative marks only: the native Slider retains pointer capture, thumb, keyboard and accessibility.
internal sealed class SliderPresetMarks
{
    private readonly Slider _slider;
    private readonly Canvas _marks, _labels;
    private readonly (string Label, double Value)[] _values;
    private readonly Ellipse[] _dots;
    private readonly TextBlock[] _texts;
    private FrameworkElement? _track, _thumb;
    private Point? _press;
    private uint _pointer;
    private double _travel;
    private readonly Point[] _centers;
    internal bool Ready { get; private set; }

    internal SliderPresetMarks(Slider slider, Canvas marks, Canvas labels, (string Label, double Value)[] values)
    {
        _slider = slider; _marks = marks; _labels = labels; _values = values;
        _centers = new Point[values.Length]; _dots = new Ellipse[values.Length]; _texts = new TextBlock[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            _texts[i] = new TextBlock { Text = values[i].Label, FontSize = 12, Opacity = .7 };
            _dots[i] = new Ellipse { Width = 7, Height = 7, Opacity = .8 };
            _labels.Children.Add(_texts[i]); _marks.Children.Add(_dots[i]);
        }
        slider.Loaded += (_, _) =>
        {
            _track = Find(slider, "HorizontalTrackRect"); _thumb = Find(slider, "HorizontalThumb");
            if (_track is not null) _track.SizeChanged += NativeSizeChanged;
            if (_thumb is not null) _thumb.SizeChanged += NativeSizeChanged;
            Refresh();
        };
        slider.SizeChanged += (_, _) => Refresh();
        slider.ValueChanged += (_, _) => Refresh();
        slider.ActualThemeChanged += (_, _) => Refresh();
        slider.Unloaded += (_, _) =>
        {
            if (_track is not null) _track.SizeChanged -= NativeSizeChanged;
            if (_thumb is not null) _thumb.SizeChanged -= NativeSizeChanged;
            Cancel(); Ready = false; _track = _thumb = null;
        };
    }

    private void NativeSizeChanged(object sender, SizeChangedEventArgs args) => Refresh();
    private Point ToMarks(Point point) => _slider.TransformToVisual(_marks).TransformPoint(point);

    internal void Refresh()
    {
        Ready = _track is not null && _thumb is not null && _track.ActualWidth > _thumb.ActualWidth && _thumb.ActualWidth > 0;
        if (!Ready) return; // No guessed hit targets if a future WinUI template changes.
        var origin = _track!.TransformToVisual(_marks).TransformPoint(new Point());
        for (int i = 0; i < _values.Length; i++)
        {
            double x = SliderPresetGeometry.Center(_slider.Minimum, _slider.Maximum, _values[i].Value,
                origin.X, _track.ActualWidth, _thumb!.ActualWidth);
            _centers[i] = new Point(x, origin.Y + _track.ActualHeight / 2);
            Canvas.SetLeft(_dots[i], x - 3.5); Canvas.SetTop(_dots[i], _centers[i].Y - 3.5);
            _dots[i].Fill = _texts[i].Foreground;
            // The native thumb itself represents the selected point; never paint over its center.
            _dots[i].Visibility = Math.Abs(_slider.Value - _values[i].Value) < .005 ? Visibility.Collapsed : Visibility.Visible;
            _texts[i].Measure(new Size(double.PositiveInfinity, 20));
            Canvas.SetLeft(_texts[i], Math.Clamp(x - _texts[i].DesiredSize.Width / 2, 0,
                Math.Max(0, _slider.ActualWidth - _texts[i].DesiredSize.Width)));
        }
        _marks.Opacity = _labels.Opacity = _slider.IsEnabled ? 1 : .4;
    }

    internal void Begin(uint pointer, Point point) { _pointer = pointer; _press = ToMarks(point); _travel = 0; Refresh(); }
    internal void Move(uint pointer, Point point)
    {
        point = ToMarks(point);
        if (_press is { } start && pointer == _pointer)
            _travel = Math.Max(_travel, Math.Max(Math.Abs(point.X - start.X), Math.Abs(point.Y - start.Y)));
    }
    internal double? Complete(uint pointer, Point point)
    {
        if (_press is not { } start || pointer != _pointer) return null;
        Move(pointer, point); point = ToMarks(point); Cancel();
        if (!Ready) return null;
        for (int i = 0; i < _values.Length; i++)
            if (SliderPresetGeometry.IsPresetClick(start.X, start.Y, point.X, point.Y, _travel, _centers[i].X, _centers[i].Y))
                return _values[i].Value;
        return null;
    }
    internal void Cancel() => _press = null;
    internal bool ThumbAlignedForProbe()
    {
        Refresh(); if (!Ready) return false;
        var origin = _track!.TransformToVisual(_marks).TransformPoint(new Point());
        double expected = SliderPresetGeometry.Center(_slider.Minimum, _slider.Maximum, _slider.Value,
            origin.X, _track.ActualWidth, _thumb!.ActualWidth);
        double actual = _thumb.TransformToVisual(_marks).TransformPoint(new Point()).X + _thumb.ActualWidth / 2;
        return Math.Abs(expected - actual) <= 1 / _slider.XamlRoot.RasterizationScale;
    }
    internal Point CenterForProbe(int index) { Refresh(); if (!Ready) throw new InvalidOperationException("Native slider track unavailable"); return _marks.TransformToVisual(_slider).TransformPoint(_centers[index]); }

    private static FrameworkElement? Find(DependencyObject parent, string name)
    {
        if (parent is FrameworkElement element && element.Name == name) return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (Find(VisualTreeHelper.GetChild(parent, i), name) is { } found) return found;
        return null;
    }
}
