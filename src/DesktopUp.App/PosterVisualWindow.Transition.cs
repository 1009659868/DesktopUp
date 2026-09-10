using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private Grid? _contentStructure;
    private StackPanel? _metaPanel;
    private Grid? _footer;
    private TransitionLayout? _transitionLayout;

    // DeskBox's responsive-content lease, adapted to our separate footer and input HWND.
    // Canvas keeps the full content's desired size from enlarging the current root/client area.
    private void BeginTransitionLayout(string requestId, DesktopPixelRect expandedBounds)
    {
        RestoreTransitionLayout();
        if (_surface is null || _contentStructure is null || _footer is null || _monitor is null)
            throw new InvalidOperationException("Poster layout is not ready for a transition.");
        var structure = _contentStructure;
        var footer = _footer;
        var canvas = new Canvas();
        var lease = new TransitionLayout(requestId, structure, footer, canvas,
            structure.Width, structure.Height, structure.Clip, footer.Width, footer.Height, footer.Clip,
            PosterTransitionSurface.Create(_bounds, expandedBounds));
        _transitionLayout = lease; // Every partial preparation can be unwound by the same path.
        _surface.Child = null;
        structure.Children.Remove(footer);
        canvas.Children.Add(structure);
        canvas.Children.Add(footer);
        _surface.Child = canvas;
        var border = _surface.BorderThickness;
        double width = expandedBounds.Width / _monitor.ScaleX - border.Left - border.Right;
        double height = expandedBounds.Height / _monitor.ScaleY - border.Top - border.Bottom;
        structure.Width = width; structure.Height = height;
        footer.Width = width; footer.Height = FooterHeightDip;
        RebuildItemGrid(FrameWidthDip(expandedBounds));
        UpdateItemScrollState(expandedBounds);
        structure.Measure(new Size(width, height));
        structure.Arrange(new Rect(0, 0, width, height));
        structure.UpdateLayout();
        lease.StableWidth = structure.ActualWidth; lease.StableHeight = structure.ActualHeight;
        ApplyTransitionViewport(_bounds);
        log.Write("info", "poster.transition_prepared", new { PosterId, requestId,
            width = lease.StableWidth, height = lease.StableHeight, expandedBounds, collapsed = _geometry.IsCollapsed });
    }

    private bool ApplyTransitionViewport(DesktopPixelRect frame)
    {
        if (_transitionLayout is not { } lease || lease.RequestId != _activeCommandRequestId || _monitor is null || _surface is null)
            return false;
        var border = _surface.BorderThickness;
        double width = frame.Width / _monitor.ScaleX - border.Left - border.Right;
        double height = frame.Height / _monitor.ScaleY - border.Top - border.Bottom;
        var viewport = PosterTransitionViewport.Resolve(height,
            PosterFrameGeometryPolicy.CollapsedHeightDip - border.Top - border.Bottom, FooterHeightDip, _geometry.IsCollapsed);
        lease.Canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        lease.Structure.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, viewport.BodyBottom) };
        Canvas.SetTop(lease.Footer, viewport.FooterTop);
        lease.Footer.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, viewport.FooterVisibleHeight) };
        // Only viewport/position layout changes; the content is never remeasured to each frame's height.
        lease.Canvas.UpdateLayout();
        return Math.Abs(lease.Structure.ActualWidth - lease.StableWidth) < .5 &&
            Math.Abs(lease.Structure.ActualHeight - lease.StableHeight) < .5;
    }

    private DesktopPixelRect PreparedSurfaceBounds(DesktopPixelRect visible) =>
        _transitionLayout?.Surface.At(visible) ?? visible;

    private DesktopPixelRect CurrentNativeBounds(DesktopPixelRect visible) =>
        _transitionLayout is { NativePrepared: true } ? PreparedSurfaceBounds(visible) : visible;

    internal bool VerifyTransitionSurfaceReady()
    {
        if (_transitionLayout is not { NativePrepared: true } lease || _monitor is null ||
            _window?.Content is not FrameworkElement root) return false;
        var surface = lease.Surface.At(_bounds);
        return Math.Abs(root.ActualWidth * _monitor.ScaleX - surface.Width) < .5 &&
            Math.Abs(root.ActualHeight * _monitor.ScaleY - surface.Height) < .5 &&
            attachment.VerifyVisualSurface(_windowHandle, surface, _bounds);
    }

    private void RestoreTransitionLayout()
    {
        if (_transitionLayout is not { } lease) return;
        _transitionLayout = null;
        if (_surface?.Child == lease.Canvas) _surface.Child = null;
        lease.Canvas.Children.Clear();
        lease.Structure.Width = lease.StructureWidth; lease.Structure.Height = lease.StructureHeight;
        lease.Structure.Clip = lease.StructureClip;
        lease.Footer.Width = lease.FooterWidth; lease.Footer.Height = lease.FooterHeight;
        lease.Footer.Clip = lease.FooterClip;
        Canvas.SetTop(lease.Footer, 0);
        if (!lease.Structure.Children.Contains(lease.Footer)) lease.Structure.Children.Add(lease.Footer);
        if (_surface is not null) _surface.Child = lease.Structure;
        log.Write("info", "poster.transition_released", new { PosterId, lease.RequestId });
    }

    internal bool VerifyTransitionLayout(bool active) => active
        ? _transitionLayout is { } lease && lease.RequestId == _activeCommandRequestId &&
            Math.Abs(lease.Structure.ActualHeight - lease.StableHeight) < .5 && VerifyTransitionSurfaceReady()
        : _transitionLayout is null && _contentStructure is { } structure && double.IsNaN(structure.Width) &&
            double.IsNaN(structure.Height) && structure.Clip is null && _footer?.Clip is null &&
            (!_geometry.IsCollapsed || (structure.RowDefinitions[1].Height.Value == 0 &&
                structure.RowDefinitions[3].Height.Value == 0 && _footer?.Visibility == Visibility.Collapsed));

    private sealed class TransitionLayout(string requestId, Grid structure, Grid footer, Canvas canvas,
        double structureWidth, double structureHeight, RectangleGeometry? structureClip,
        double footerWidth, double footerHeight, RectangleGeometry? footerClip, PosterTransitionSurface surface)
    {
        public string RequestId { get; } = requestId;
        public PosterTransitionSurface Surface { get; } = surface;
        public bool NativePrepared { get; set; }
        public Grid Structure { get; } = structure;
        public Grid Footer { get; } = footer;
        public Canvas Canvas { get; } = canvas;
        public double StructureWidth { get; } = structureWidth;
        public double StructureHeight { get; } = structureHeight;
        public RectangleGeometry? StructureClip { get; } = structureClip;
        public double FooterWidth { get; } = footerWidth;
        public double FooterHeight { get; } = footerHeight;
        public RectangleGeometry? FooterClip { get; } = footerClip;
        public double StableWidth { get; set; }
        public double StableHeight { get; set; }
    }
}
