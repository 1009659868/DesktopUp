using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace DesktopUp;

// Behind Item ink, alongside rain. Two reusable strokes, no full-area white rectangle.
internal sealed class RainLightningDecoration
{
    internal Canvas Root { get; } = new() { IsHitTestVisible = false };
    private readonly ShapePath _halo, _core;
    private RainSceneEnvelope? _area;
    private uint _strokeSeed;
    internal double Opacity => _core.Opacity;
    internal int Nodes => 1 + Root.Children.Count;
    internal RainLightningDecoration(Brush color)
    {
        _halo = new() { Stroke = color, StrokeThickness = 7, Opacity = 0, IsHitTestVisible = false };
        _core = new() { Stroke = color, StrokeThickness = 1.4, Opacity = 0, IsHitTestVisible = false };
        Root.Children.Add(_halo); Root.Children.Add(_core);
    }
    internal void Update(RainSceneEnvelope area, double pulse, uint strokeSeed = 0)
    {
        Root.Width = Math.Max(0, area.Left * 2 + area.Width); Root.Height = Math.Max(0, area.Bottom);
        if (_area != area || _strokeSeed != strokeSeed)
        {
            _area = area; _strokeSeed=strokeSeed;
            var paths = RainLightningGeometry.Resolve(area,strokeSeed);
            PathGeometry Geometry()
            {
                var geometry = new PathGeometry();
                foreach (var points in paths)
                {
                    var figure = new PathFigure { StartPoint = new(points[0].X, points[0].Y), IsClosed = false, IsFilled = false };
                    for (int i = 1; i < points.Count; i++) figure.Segments.Add(new LineSegment { Point = new(points[i].X, points[i].Y) });
                    geometry.Figures.Add(figure);
                }
                return geometry;
            }
            _halo.Data = Geometry(); _core.Data = Geometry();
        }
        _halo.Opacity = pulse * .45;
        _core.Opacity = pulse * 2.3;
    }
    internal void Stop() => _halo.Opacity = _core.Opacity = 0;
    internal void Dispose() { Stop(); Root.Children.Clear(); }
}
