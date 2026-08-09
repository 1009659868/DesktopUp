function positiveFinite(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : fallback;
}

export function resolveHostPresentation(hostSurface) {
  const logicalWidth = positiveFinite(hostSurface?.logicalSize?.width, 1);
  const logicalHeight = positiveFinite(hostSurface?.logicalSize?.height, 1);
  const responsiveScale = positiveFinite(hostSurface?.responsiveScale, 1);
  const userScale = positiveFinite(hostSurface?.userScale, 1);
  return {
    logicalWidth,
    logicalHeight,
    responsiveScale,
    userScale,
    visualScale: responsiveScale * userScale
  };
}

export function hostPresentationCssVariables(hostSurface) {
  const presentation = resolveHostPresentation(hostSurface);
  return {
    "--host-logical-width": `${presentation.logicalWidth}px`,
    "--host-logical-height": `${presentation.logicalHeight}px`,
    "--host-visual-scale": String(presentation.visualScale)
  };
}
