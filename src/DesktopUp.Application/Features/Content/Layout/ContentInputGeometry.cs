using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record PosterInputGeometryContext(DisplayMonitorSnapshot Monitor, PosterFrameGeometry Geometry,
    ResolvedPosterPresentation Presentation, TitleCloudPose TitlePose, bool Editing, bool ReadOnly,
    string? SelectedItem, ContentPresentationFrame? Content, double ContentLeft, double ContentTop,
    bool Transition, int CornerRadius, double? CanvasCompositionHeight = null);

// Local hit geometry does not depend on screen position. Each publication still gets
// a fresh revision: an old hit cannot remain valid after its surface has moved.
public sealed class ContentInputGeometryCache
{
    private PosterInputGeometryContext? _context;
    private PosterInputLayoutSnapshot? _layout;

    public PosterInputLayoutSnapshot Resolve(long revision, DesktopPixelRect frame, PosterInputGeometryContext context)
    {
        if (_layout is not null && _context == context &&
            _layout.FrameWidth == frame.Width && _layout.FrameHeight == frame.Height)
            return _layout with { Revision = revision };
        var resolved = ContentInputGeometry.Resolve(revision, frame, context);
        _context = context;
        return _layout = resolved;
    }
}

/// <summary>All title/action, content/viewport and resize hit geometry for a prepared frame.
/// The platform consumes these regions; it does not independently reinterpret layout.</summary>
public static class ContentInputGeometry
{
    public static PosterInputLayoutSnapshot Resolve(long revision, DesktopPixelRect frame, PosterInputGeometryContext context)
    {
        double scaleX = context.Monitor.ScaleX;
        var design = context.Presentation.Parameters.Canvas?.Current;
        var titleBar = CanvasSurfaceGeometry.Title(design, frame.Width / scaleX, context.CanvasCompositionHeight ?? frame.Height / context.Monitor.ScaleY,
            context.Geometry.IsCollapsed, context.Presentation.TitleChrome);
        DesktopPixelRect ToTitlePixels(TitleChromeRect rect) => new(checked((int)Math.Round(rect.X * context.Monitor.ScaleX)), checked((int)Math.Round(rect.Y * context.Monitor.ScaleY)),
            Math.Max(1, ToPhysicalX(rect.Width, context.Monitor)), Math.Max(1, ToPhysicalY(rect.Height, context.Monitor)));
        DesktopPixelRect collapseBounds = ToTitlePixels(titleBar.Collapse);
        DesktopPixelRect moreBounds = ToTitlePixels(titleBar.More);
        var regions = new List<PosterInputRegion>
        {
            new(PosterInputTargetKind.CollapseButton, -1,
                collapseBounds),
            new(PosterInputTargetKind.MoreButton, -1,
                moreBounds)
        };

        AddContentRegions(context, regions);
        AddNavigationRegions(context, regions);


        if (context.Editing &&
            !context.Geometry.IsLocked &&
            !context.Geometry.IsCollapsed)
        {
            int edgeX = Math.Max(6, ToPhysicalX(8, context.Monitor));
            int edgeY = Math.Max(6, ToPhysicalY(8, context.Monitor));
            int cornerX = Math.Max(16, ToPhysicalX(20, context.Monitor));
            int cornerY = Math.Max(16, ToPhysicalY(20, context.Monitor));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTopLeft,
                -1,
                new DesktopPixelRect(0, 0, cornerX, cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTopRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - cornerX,
                    0,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottomLeft,
                -1,
                new DesktopPixelRect(
                    0,
                    frame.Height - cornerY,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottomRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - cornerX,
                    frame.Height - cornerY,
                    cornerX,
                    cornerY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeLeft,
                -1,
                new DesktopPixelRect(
                    0,
                    cornerY,
                    edgeX,
                    Math.Max(1, frame.Height - (cornerY * 2)))));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeRight,
                -1,
                new DesktopPixelRect(
                    frame.Width - edgeX,
                    cornerY,
                    edgeX,
                    Math.Max(1, frame.Height - (cornerY * 2)))));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeTop,
                -1,
                new DesktopPixelRect(
                    cornerX,
                    0,
                    Math.Max(1, frame.Width - (cornerX * 2)),
                    edgeY)));
            regions.Add(new PosterInputRegion(
                PosterInputTargetKind.ResizeBottom,
                -1,
                new DesktopPixelRect(
                    cornerX,
                    frame.Height - edgeY,
                    Math.Max(1, frame.Width - (cornerX * 2)),
                    edgeY)));
        }

        if (titleBar.Kind == PosterTitleChromeKind.Cloud)
            foreach (var band in TitleCloudInputGeometry.Resolve(frame.Width / scaleX, context.Geometry.IsCollapsed,
                context.TitlePose, scaleX, context.Monitor.ScaleY))
                regions.Add(new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, band));
        else foreach (var band in titleBar.GripBands)
            regions.Add(new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, ToTitlePixels(band)));

        if (design is not null && !context.Geometry.IsCollapsed && !context.Editing && !context.Transition)
        {
            // Real items and protected title controls win. Design UI precedes only blank space.
            var blanks = regions.Where(r => r.Kind == PosterInputTargetKind.ContentViewport).ToArray();
            regions.RemoveAll(r => r.Kind == PosterInputTargetKind.ContentViewport);
            AddCanvasActions(design, frame, context, regions);
            regions.AddRange(blanks);
        }
        return new PosterInputLayoutSnapshot(
            revision,
            frame.Width,
            frame.Height,
            PosterInputRegionClipPolicy.Clip(frame.Width, frame.Height, regions),
            context.CornerRadius) { ContentSnapshotGeneration = context.Content?.Snapshot.ViewportGeneration ?? 0,
                Surface = !context.Geometry.IsCollapsed && design?.Recipe.Surface is { } surface
                    ? surface with { Radius = Math.Round(surface.Radius * Math.Min(frame.Width / design.Recipe.Artboard!.Width, frame.Height / design.Recipe.Artboard.Height)) } : null };
    }

    private static void AddCanvasActions(CanvasDesign design, DesktopPixelRect frame, PosterInputGeometryContext context, List<PosterInputRegion> regions)
    {
        var sources = (design.Recipe.Behavior?.Bindings ?? []).Where(b => b.Enabled && b.Event == "pointer.click").Select(b => b.Source).ToHashSet();
        var indices = design.Recipe.Nodes.Select((node, index) => (node, index)).Where(p => sources.Contains(p.node.Id) && CanvasBehaviorPrefabs.CanBind(p.node)).ToDictionary(p => p.node.Id, p => p.index);
        if (indices.Count == 0) return;
        var hidden = CanvasLayerState.Resolve(design).Hidden;
        var layout = CanvasLayout.Resolve(design, frame.Width / context.Monitor.ScaleX, context.CanvasCompositionHeight ?? frame.Height / context.Monitor.ScaleY, false);
        foreach (var placement in layout.Nodes.Reverse().Where(p => sources.Contains(p.Node.Id) && !hidden.Contains(p.Node.Id)))
        {
            var box = placement.Bounds;
            var corners = new[] { (-box.Width / 2, -box.Height / 2), (box.Width / 2, -box.Height / 2), (box.Width / 2, box.Height / 2), (-box.Width / 2, box.Height / 2) }
                .Select(p => CanvasEditingGeometry.Rotate(p.Item1, p.Item2, placement.Rotation)).ToArray();
            int left = (int)Math.Floor((box.X + box.Width / 2 + corners.Min(p => p.X)) * context.Monitor.ScaleX),
                top = (int)Math.Floor((box.Y + box.Height / 2 + corners.Min(p => p.Y)) * context.Monitor.ScaleY),
                right = (int)Math.Ceiling((box.X + box.Width / 2 + corners.Max(p => p.X)) * context.Monitor.ScaleX),
                bottom = (int)Math.Ceiling((box.Y + box.Height / 2 + corners.Max(p => p.Y)) * context.Monitor.ScaleY);
            int index = indices[placement.Node.Id];
            regions.Add(new(PosterInputTargetKind.CanvasAction, index, new(left, top, right - left, bottom - top))
            { CanvasAction = new(box, placement.Rotation, context.Monitor.ScaleX, context.Monitor.ScaleY) });
        }
    }


    private static void AddContentRegions(PosterInputGeometryContext context, List<PosterInputRegion> regions)
    {
        if (context.Geometry.IsCollapsed || context.Content is null) return;
        var snapshot=context.Content.Snapshot; var viewport=context.Content.Viewport;
        DesktopPixelRect Pixels(ContentRect r)
        {
            int x=(int)Math.Round((context.ContentLeft+r.X)*context.Monitor.ScaleX), y=(int)Math.Round((context.ContentTop+r.Y)*context.Monitor.ScaleY);
            int right=(int)Math.Round((context.ContentLeft+r.Right)*context.Monitor.ScaleX), bottom=(int)Math.Round((context.ContentTop+r.Bottom)*context.Monitor.ScaleY);
            return new(x,y,Math.Max(1,right-x),Math.Max(1,bottom-y));
        }
        // Hit regions follow the same tiles that were drawn, so a card crossing the seam is clickable
        // on both sides while still resolving to one ItemId.
        foreach (var tile in ContentLayoutFlow.ProjectTiles(snapshot, viewport, context.Content!.BrowseMode))
        {
            if (tile.Clip.IsEmpty) continue;
            if (context.Editing && !context.Geometry.IsLocked && !context.ReadOnly && context.SelectedItem==tile.ItemId)
            {
                var handle=new ContentRect(tile.Bounds.Right-16,tile.Bounds.Bottom-16,16,16).Intersect(new(0,0,viewport.WidthDip,viewport.HeightDip));
                if(!handle.IsEmpty) regions.Add(new(PosterInputTargetKind.ContentSpanHandle,tile.SourceIndex,Pixels(handle))
                { ContentItemId=tile.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
            }
            regions.Add(new(PosterInputTargetKind.ContentItem,tile.SourceIndex,Pixels(tile.Clip))
            { ContentItemId=tile.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
        }
        if (viewport.WidthDip>0 && viewport.HeightDip>0) regions.Add(new(PosterInputTargetKind.ContentViewport,-1,Pixels(new(0,0,viewport.WidthDip,viewport.HeightDip)))
        {ContentSnapshotGeneration=snapshot.ViewportGeneration});
    }

    private static void AddNavigationRegions(PosterInputGeometryContext context, List<PosterInputRegion> regions)
    {
        if (context.Content?.BrowseMode != ContentBrowseMode.ReadOnlyOverflow ||
            context.Geometry.IsCollapsed || context.Transition) return;
        // Navigation lives below the same content viewport that supplies card hits.
        double width = context.Content.Viewport.WidthDip;
        for (int i = 0; i < 4; i++)
        {
            var r = new ContentRect(context.ContentLeft + i * width / 4,
                context.ContentTop + context.Content.Viewport.HeightDip + 16, width / 4, 24);
            int left = (int)Math.Round(r.X * context.Monitor.ScaleX), top = (int)Math.Round(r.Y * context.Monitor.ScaleY);
            int right = (int)Math.Round(r.Right * context.Monitor.ScaleX), bottom = (int)Math.Round(r.Bottom * context.Monitor.ScaleY);
            regions.Add(new(PosterInputTargetKind.ContentBrowseNavigation, i, new(left, top, right-left, bottom-top)));
        }
    }
    private static int ToPhysicalX(double value, DisplayMonitorSnapshot monitor) => Math.Max(1, checked((int)Math.Round(value * monitor.ScaleX)));
    private static int ToPhysicalY(double value, DisplayMonitorSnapshot monitor) => Math.Max(1, checked((int)Math.Round(value * monitor.ScaleY)));
}
