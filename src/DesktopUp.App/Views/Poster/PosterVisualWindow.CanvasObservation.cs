using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal CanvasContentObservation? ReadCanvasObservation()
    {
        // UI-thread, read only after successful native input AND visual publication. No solving or IO.
        if (_geometry.IsCollapsed || HasContentGesture || _transitionLayout is not null || _spatialGesture is not null ||
            _contentSession.IsSuspended || _presenter?.Frame is not { } frame || frame.ProjectionFault ||
            inputRouter.ReadSurface(inputSession)?.Layout.ContentSnapshotGeneration != frame.Snapshot.ViewportGeneration ||
            !ReferenceEquals(frame.Snapshot, _contentLayout.Current?.Snapshot)) return null;
        var area = new CanvasRect(ContentLeftDip, ContentTopDip, frame.Viewport.WidthDip, frame.Viewport.HeightDip);
        var visible = new List<CanvasAnonymousItem>(); bool truncated = false;
        foreach (var tile in ContentLayoutFlow.ProjectTiles(frame.Snapshot, frame.Viewport, frame.BrowseMode))
        {
            double x = Math.Max(0, tile.Bounds.X), y = Math.Max(0, tile.Bounds.Y);
            double right = Math.Min(area.Width, tile.Bounds.X + tile.Bounds.Width), bottom = Math.Min(area.Height, tile.Bounds.Y + tile.Bounds.Height);
            if (right <= x || bottom <= y) continue;
            if (visible.Count == 128) { truncated = true; break; }
            visible.Add(new("item-" + (visible.Count + 1), new(area.X + x, area.Y + y, right - x, bottom - y)));
        }
        return new(_posterEntityVersion, frame.Presentation.Parameters.Canvas?.Current is { } design ? CanvasDeclaration.Digest(design) : null,
            frame.Snapshot.DocumentVersion, frame.Snapshot.ViewportGeneration, area, frame.Snapshot.Items.Count, truncated, visible.AsReadOnly());
    }
}
