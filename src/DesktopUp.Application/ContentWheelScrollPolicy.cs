using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Maps signed wheel input to the current content axis without writing author state.</summary>
public static class ContentWheelScrollPolicy
{
    public static ContentBrowseOffsets Advance(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport, int wheelDelta)
    {
        var current = ContentLayoutFlow.NormalizeOffsets(snapshot, viewport);
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        double step = Math.Max(1, snapshot.CellDip + snapshot.GapDip);
        double delta = -wheelDelta / 120d * step;
        double offset = vertical ? current.ScrollYDip : current.ScrollXDip;
        // Preserve the sign until wrapping. Clamping before modulo traps the head at zero.
        // Keep fractional deltas in the browse offset: six 20-unit events equal one notch.
        double target = ContentLayoutFlow.IsLooping(snapshot, viewport)
            ? ContentLayoutFlow.AdvanceScrollX(offset, delta, ContentLayoutFlow.LoopPeriodDip(snapshot, viewport))
            : Math.Clamp(offset + delta, 0, Math.Max(0, vertical
                ? snapshot.ExtentHeightDip - viewport.HeightDip : snapshot.ExtentWidthDip - viewport.WidthDip));
        return vertical ? new(0, target) : new(target, 0);
    }
}
