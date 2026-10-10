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
    private void BeginTransitionLayout(string requestId, DesktopPixelRect expandedBounds, bool responsiveGeometry = false)
    {
        // Replacing a live lease changes layout ownership, not the HWND allocation yet.
        // Keep that actual predecessor until the new native preparation succeeds.
        var previousNativeBounds = CurrentNativeBounds(_bounds);
        RestoreTransitionLayout();
        if (_surface is null || _contentStructure is null || _footer is null || _monitor is null)
            throw new InvalidOperationException("Poster layout is not ready for a transition.");
        var structure = _contentStructure;
        var footer = _footer;
        var canvas = new Canvas();
        var lease = new TransitionLayout(requestId, structure, footer, canvas,
            structure.Width, structure.Height, structure.Clip, footer.Width, footer.Height, footer.Clip,
            responsiveGeometry ? null : PosterTransitionSurface.Create(_bounds, expandedBounds),
            responsiveGeometry ? PosterGeometryTransitionSurface.Create(_bounds, expandedBounds) : null, previousNativeBounds);
        _transitionLayout = lease; // Every partial preparation can be unwound by the same path.
        _surface.Child = null;
        structure.Children.Remove(footer);
        canvas.Children.Add(structure);
        canvas.Children.Add(footer);
        _surface.Child = canvas;
        var border = _surface.BorderThickness;
        // Geometry starts with the visible composition, not the larger native allocation.
        // Posture retains its pre-expanded, constant content layout behind the changing clip.
        var logicalBounds = responsiveGeometry ? _bounds : expandedBounds;
        double width = logicalBounds.Width / _monitor.ScaleX - border.Left - border.Right;
        double height = logicalBounds.Height / _monitor.ScaleY - border.Top - border.Bottom;
        structure.Width = width; structure.Height = height;
        footer.Width = width; footer.Height = FooterHeightDip;
        RebuildItemGrid(FrameWidthDip(logicalBounds));
        UpdateItemScrollState(logicalBounds);
        structure.Measure(new Size(width, height));
        structure.Arrange(new Rect(0, 0, width, height));
        structure.UpdateLayout();
        lease.StableWidth = structure.ActualWidth; lease.StableHeight = structure.ActualHeight;
        ApplyTransitionViewport(_bounds);
        log.Write("info", "poster.transition_prepared", new { PosterId, requestId,
            width = lease.StableWidth, height = lease.StableHeight, expandedBounds, responsiveGeometry, collapsed = _geometry.IsCollapsed });
    }

    // Ownership transfer keeps a live R4 allocation/layout when it already covers the new lease.
    // No CancelPointerGesture and no restoration to committed geometry on this path.
    internal void BeginTitlePreview(string requestId, DesktopPixelRect capacity, bool source)
    {
        CancelScheduledGestureFrame();
        _activeCommandRequestId = requestId;
        if (_transitionLayout is { IsResponsiveGeometry: false, Surface: { } surface } existing &&
            surface.Width == capacity.Width && surface.Height >= capacity.Height)
        {
            existing.RequestId = requestId;
            existing.TitleSource = source;
            if (!ApplyTransitionViewport(_bounds)) throw new InvalidOperationException("Cannot retag title viewport.");
            return;
        }
        if (source) _geometry = _geometry with { IsCollapsed = false };
        UpdateVisualState();
        BeginTransitionLayout(requestId, capacity);
        _transitionLayout!.TitleSource = source;
        if (!ApplyTransitionViewport(_bounds)) throw new InvalidOperationException("Cannot prepare title viewport.");
    }

    private bool ApplyTransitionViewport(DesktopPixelRect frame)
    {
        if (_transitionLayout is not { } lease || lease.RequestId != _activeCommandRequestId || _monitor is null || _surface is null)
            return false;
        var border = _surface.BorderThickness;
        double width = frame.Width / _monitor.ScaleX - border.Left - border.Right;
        double height = frame.Height / _monitor.ScaleY - border.Top - border.Bottom;
        if (lease.IsResponsiveGeometry)
        {
            _ = lease.NativeBoundsFor(frame); // Validate capacity before changing live logical layout.
            lease.Structure.Width = width; lease.Structure.Height = height;
            lease.Footer.Width = width;
            lease.Structure.Measure(new Size(width, height));
            lease.Structure.Arrange(new Rect(0, 0, width, height));
            lease.Structure.UpdateLayout();
            // Responsive leases witness the current logical size; posture witnesses stay fixed.
            lease.StableWidth = width; lease.StableHeight = height;
        }
        var viewport = PosterTransitionViewport.Resolve(height,
            PosterFrameGeometryPolicy.CollapsedHeightDip - border.Top - border.Bottom, FooterHeightDip, _geometry.IsCollapsed);
        // Content is pre-expanded behind the clip; the title indicator follows the visible frame,
        // not that temporary layout flag (notably during a stationary compact press).
        if (lease.TitleSource && _collapseGlyph is not null)
            _collapseGlyph.Text = height <= PosterFrameGeometryPolicy.CollapsedHeightDip - border.Top - border.Bottom ? "⌃" : "⌄";
        lease.Canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        lease.Structure.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, viewport.BodyBottom) };
        Canvas.SetTop(lease.Footer, viewport.FooterTop);
        lease.Footer.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, viewport.FooterVisibleHeight) };
        // Posture changes only viewport/position. Geometry has already consumed and measured
        // the current frame's composition, without changing the native allocation.
        lease.Canvas.UpdateLayout();
        return Math.Abs(lease.Structure.ActualWidth - lease.StableWidth) < .5 &&
            Math.Abs(lease.Structure.ActualHeight - lease.StableHeight) < .5;
    }

    private DesktopPixelRect PreparedSurfaceBounds(DesktopPixelRect visible) =>
        _transitionLayout?.NativeBoundsFor(visible) ?? visible;

    // Geometry-only readback: inspect the objects just submitted with the prepared frame.
    // No fresh solve, cached author mutation, or smoke-only path can make a failed frame pass.
    private bool VerifyGeometryFramePresentation(PreparedSpatialFrame frame, DesktopFramePhase phase,
        bool nativeVerified, bool inputApplied)
    {
        if (_transitionLayout is not { IsResponsiveGeometry: true } lease || _monitor is null || _surface is null)
            return false;
        static bool Near(double a, double b) => double.IsFinite(a) && double.IsFinite(b) && Math.Abs(a - b) < .05;
        var snapshot = frame.Content; var viewport = frame.ContentViewport;
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport);
        double pitch = snapshot.CellDip + snapshot.GapDip;
        var painted = tiles.Where(t => t.Bounds.Bottom >= -pitch && t.Bounds.Y <= viewport.HeightDip + pitch).ToArray();
        bool content = ReferenceEquals(_contentSnapshot, snapshot) && _contentViewport == viewport &&
            _contentCanvas is not null && _contentCards.Count == painted.Length && painted.All(tile =>
                _contentCards.TryGetValue(TileKey(tile), out var card) && _contentCanvas.Children.Contains(card) &&
                Near(Canvas.GetLeft(card), tile.Bounds.X) && Near(Canvas.GetTop(card), tile.Bounds.Y) &&
                Near(card.Width, tile.Bounds.Width) && Near(card.Height, tile.Bounds.Height));
        var published = inputRouter.ReadSurface(inputSession);
        var hits = frame.Input.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem).ToArray();
        var visibleTiles = tiles.Where(t => !t.Clip.IsEmpty).ToArray();
        DesktopPixelRect HitBounds(ContentRect rect)
        {
            int x = (int)Math.Round((ContentLeftDip + rect.X) * _monitor.ScaleX);
            int y = (int)Math.Round((ContentTopDip + rect.Y) * _monitor.ScaleY);
            int right = (int)Math.Round((ContentLeftDip + rect.Right) * _monitor.ScaleX);
            int bottom = (int)Math.Round((ContentTopDip + rect.Bottom) * _monitor.ScaleY);
            return new(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
        }
        bool input = inputApplied && published is { } actual && actual.Frame == frame.Bounds &&
            actual.Layout.Revision == frame.Input.Revision && actual.Layout.FrameWidth == frame.Input.FrameWidth &&
            actual.Layout.FrameHeight == frame.Input.FrameHeight && actual.Layout.OuterCornerRadius == frame.Input.OuterCornerRadius &&
            actual.Layout.Regions.SequenceEqual(frame.Input.Regions) &&
            hits.Length == visibleTiles.Length && hits.All(h => h.ContentSnapshotGeneration == snapshot.ViewportGeneration) &&
            visibleTiles.All(tile => hits.Any(hit => hit.ContentItemId == tile.ItemId && hit.Bounds == HitBounds(tile.Clip)));
        double width = frame.Bounds.Width / _monitor.ScaleX - _surface.BorderThickness.Left - _surface.BorderThickness.Right;
        double height = frame.Bounds.Height / _monitor.ScaleY - _surface.BorderThickness.Top - _surface.BorderThickness.Bottom;
        var transition = PosterTransitionViewport.Resolve(height,
            PosterFrameGeometryPolicy.CollapsedHeightDip - _surface.BorderThickness.Top - _surface.BorderThickness.Bottom,
            FooterHeightDip, _geometry.IsCollapsed);
        bool clip = _contentCanvas?.Clip is RectangleGeometry contentClip &&
            Near(contentClip.Rect.Width, viewport.WidthDip) && Near(contentClip.Rect.Height, viewport.HeightDip) &&
            lease.Canvas.Clip is RectangleGeometry outer && Near(outer.Rect.Width, width) && Near(outer.Rect.Height, height) &&
            lease.Structure.Clip is RectangleGeometry body && Near(body.Rect.Width, width) && Near(body.Rect.Height, transition.BodyBottom) &&
            Near(lease.Structure.ActualWidth, width) && Near(lease.Structure.ActualHeight, height) &&
            Near(lease.Footer.Width, width) && (lease.Footer.Visibility != Visibility.Visible || Near(lease.Footer.ActualWidth, width)) &&
            Near(Canvas.GetTop(lease.Footer), transition.FooterTop);
        bool verified = nativeVerified && content && input && clip;
        log.Write(verified ? "info" : "warning", "content.geometry_frame", new
        {
            PosterId, requestId = _activeCommandRequestId, phase = phase.ToString(), frame.Bounds,
            snapshot.ViewportGeneration, snapshot.DocumentVersion, viewport.WidthDip, viewport.HeightDip,
            nativeVerified, content, input, clip, verified, items = snapshot.Items.Count, visibleTiles = visibleTiles.Length
        });
        return verified;
    }

    private DesktopPixelRect CurrentNativeBounds(DesktopPixelRect visible) =>
        _transitionLayout is { } lease
            ? lease.NativePrepared ? lease.NativeBoundsFor(visible) : lease.PreviousNativeBounds
            : visible;

    internal bool VerifyTransitionSurfaceReady()
    {
        if (_transitionLayout is not { NativePrepared: true } lease || _monitor is null ||
            _window?.Content is not FrameworkElement root) return false;
        var surface = lease.NativeBoundsFor(_bounds);
        return Math.Abs(root.ActualWidth * _monitor.ScaleX - surface.Width) < .5 &&
            Math.Abs(root.ActualHeight * _monitor.ScaleY - surface.Height) < .5 &&
            attachment.VerifyVisualSurface(_windowHandle, surface, _bounds);
    }

    private void RestoreTransitionLayout()
    {
        if (_transitionLayout is not { } lease) return;
        _transitionLayout = null;
        _solvedDocument = null;
        _contentSpaceCandidate = null;
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
            Math.Abs(lease.Structure.ActualWidth - lease.StableWidth) < .5 &&
            Math.Abs(lease.Structure.ActualHeight - lease.StableHeight) < .5 && VerifyTransitionSurfaceReady()
        : _transitionLayout is null && _contentStructure is { } structure && double.IsNaN(structure.Width) &&
            double.IsNaN(structure.Height) && structure.Clip is null && _footer?.Clip is null &&
            (!_geometry.IsCollapsed || (structure.RowDefinitions[1].Height.Value == 0 &&
                structure.RowDefinitions[3].Height.Value == 0 && _footer?.Visibility == Visibility.Collapsed));

    private sealed class TransitionLayout(string requestId, Grid structure, Grid footer, Canvas canvas,
        double structureWidth, double structureHeight, RectangleGeometry? structureClip,
        double footerWidth, double footerHeight, RectangleGeometry? footerClip, PosterTransitionSurface? surface,
        PosterGeometryTransitionSurface? geometrySurface,
        DesktopPixelRect previousNativeBounds)
    {
        public string RequestId { get; set; } = requestId;
        public PosterTransitionSurface? Surface { get; } = surface;
        public bool IsResponsiveGeometry => geometrySurface is not null;
        public DesktopPixelRect NativeBoundsFor(DesktopPixelRect visible) =>
            geometrySurface?.At(visible) ?? Surface!.At(visible);
        public DesktopPixelRect PreviousNativeBounds { get; } = previousNativeBounds;
        public bool NativePrepared { get; set; }
        public bool TitleSource { get; set; }
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
