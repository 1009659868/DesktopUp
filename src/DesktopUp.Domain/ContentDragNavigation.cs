namespace DesktopUp.Domain;

/// <summary>Transient frozen-sheet navigation for one pointer owner. A folded render offset is
/// never used as a movement delta. No tile/cycle, anchor or navigation value is persisted.</summary>
public sealed class ContentDragNavigation
{
    private ContentDragNavigation(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentLayoutTile tile, ContentBrowseAnchor anchor, double travel)
    { Snapshot = snapshot; Viewport = viewport; Tile = tile; PressAnchor = anchor; TravelDip = travel; }

    public ContentLayoutSnapshot Snapshot { get; }
    public ContentLayoutViewport Viewport { get; }
    public ContentLayoutTile Tile { get; }
    public ContentBrowseAnchor PressAnchor { get; }
    public double TravelDip { get; }
    private bool Vertical => Snapshot.FlowAxis == ContentFlowAxis.Vertical;
    public double ScrollDeltaXDip => Vertical ? 0 : TravelDip;
    public double ScrollDeltaYDip => Vertical ? TravelDip : 0;

    public static ContentDragNavigation Begin(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        ContentLayoutTile tile, double pointerX, double pointerY) =>
        new(snapshot, viewport, tile, ContentLayoutFlow.CaptureAnchor(snapshot, viewport, tile, pointerX, pointerY), 0);

    public ContentDragNavigation Advance(double mainDeltaDip)
    {
        RequireBounded(mainDeltaDip);
        double next = TravelDip + mainDeltaDip;
        RequireBounded(next);
        if (!ContentLayoutFlow.IsLooping(Snapshot, Viewport))
        {
            double start = Vertical ? Snapshot.ScrollYDip : Snapshot.ScrollXDip;
            double maximum = Math.Max(0, Vertical ? Snapshot.ExtentHeightDip - Viewport.HeightDip
                : Snapshot.ExtentWidthDip - Viewport.WidthDip);
            next = Math.Clamp(start + next, 0, maximum) - start;
        }
        return new(Snapshot, Viewport, Tile, PressAnchor, next);
    }

    public ContentBrowseOffsets ProjectOffsets(ContentLayoutSnapshot presented, ContentLayoutViewport viewport) =>
        ContentLayoutFlow.NormalizeOffsets(presented, viewport with
        { ScrollXDip = Snapshot.ScrollXDip + ScrollDeltaXDip, ScrollYDip = Snapshot.ScrollYDip + ScrollDeltaYDip });

    public ContentRect Ghost(double pointerDeltaX, double pointerDeltaY)
    {
        RequireBounded(pointerDeltaX); RequireBounded(pointerDeltaY);
        return Tile.Bounds with { X = Tile.Bounds.X + pointerDeltaX, Y = Tile.Bounds.Y + pointerDeltaY };
    }

    /// <summary>The actual grabbed identity/internal point at the last pointer position. Invalid
    /// releases outside the viewport are clamped for browsing only, never turned into valid edits.</summary>
    public ContentBrowseAnchor EndAnchor(double pointerDeltaX, double pointerDeltaY)
    {
        _ = Ghost(pointerDeltaX, pointerDeltaY);
        return PressAnchor with
        {
            ViewportXDip = Math.Clamp(PressAnchor.ViewportXDip + pointerDeltaX, 0, Math.Max(0, Math.BitDecrement(Viewport.WidthDip))),
            ViewportYDip = Math.Clamp(PressAnchor.ViewportYDip + pointerDeltaY, 0, Math.Max(0, Math.BitDecrement(Viewport.HeightDip)))
        };
    }

    private static void RequireBounded(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > ContentDragPolicy.MaximumDisplacementDip)
            throw new ContentLayoutException("Drag navigation exceeded its finite safety budget.");
    }
}
