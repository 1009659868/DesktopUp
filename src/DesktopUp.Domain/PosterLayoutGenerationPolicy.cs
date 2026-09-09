namespace DesktopUp.Domain;

public static class PosterLayoutGenerationPolicy
{
    public static bool Changed(PosterDefinition a, PosterDefinition b) =>
        a.MonitorDeviceName != b.MonitorDeviceName || a.FallbackXDip != b.FallbackXDip || a.FallbackYDip != b.FallbackYDip ||
        a.WidthDip != b.WidthDip || a.ExpandedHeightDip != b.ExpandedHeightDip || a.HorizontalAnchor != b.HorizontalAnchor ||
        a.VerticalAnchor != b.VerticalAnchor || a.NormalizedWidth != b.NormalizedWidth || a.NormalizedExpandedHeight != b.NormalizedExpandedHeight ||
        a.IsLocked != b.IsLocked || a.IsCollapsed != b.IsCollapsed || a.IsVisible != b.IsVisible;
}
