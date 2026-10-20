using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record PosterInputGeometryContext(DisplayMonitorSnapshot Monitor, PosterFrameGeometry Geometry,
    ResolvedPosterPresentation Presentation, TitleCloudPose TitlePose, bool Editing, bool ReadOnly,
    string? SelectedItem, ContentPresentationFrame? Content, double ContentLeft, double ContentTop,
    bool Transition, int CornerRadius);

/// <summary>All title/action, content/viewport and resize hit geometry for a prepared frame.
/// The platform consumes these regions; it does not independently reinterpret layout.</summary>
public static class ContentInputGeometry
{
    public static PosterInputLayoutSnapshot Resolve(long revision, DesktopPixelRect frame, PosterInputGeometryContext context)
    {
        double scaleX = context.Monitor.ScaleX;
        var titleBar = PosterTitleChromePolicy.Resolve(frame.Width / scaleX, context.Geometry.IsCollapsed, context.Presentation.TitleChrome);
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

        return new PosterInputLayoutSnapshot(
            revision,
            frame.Width,
            frame.Height,
            PosterInputRegionClipPolicy.Clip(frame.Width, frame.Height, regions),
            context.CornerRadius) { ContentSnapshotGeneration = context.Content?.Snapshot.ViewportGeneration ?? 0 };
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
