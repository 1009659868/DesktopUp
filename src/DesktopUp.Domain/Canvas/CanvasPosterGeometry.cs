namespace DesktopUp.Domain;

public static class CanvasPosterGeometry
{
    public static CanvasRect Content(ResolvedPosterPresentation presentation, double width, double height) =>
        presentation.Parameters.Canvas?.Current is { } design ? CanvasLayout.Resolve(design, width, height).Content :
        new(PosterPresentationPolicy.HorizontalPaddingDip, presentation.Metrics.ReservedTopDip,
            Math.Max(0, width - PosterPresentationPolicy.HorizontalPaddingDip * 2),
            Math.Max(0, height - presentation.Metrics.ReservedTopDip - presentation.Metrics.ReservedBottomDip));
}
